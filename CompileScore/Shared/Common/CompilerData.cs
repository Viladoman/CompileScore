using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CompileScore
{
    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(string name, ulong accumulated, uint min, uint max, uint count, UnitValue maxUnit)
        {
            Name = name;
            Accumulated = accumulated;
            Min = min;
            Max = max;
            Count = count;
            MaxUnit = maxUnit; 
            Severity = 0;
        }

        public string Name { get; }
        public uint Max { get; }
        public uint Min { get; }
        public ulong Accumulated { get; }
        public uint Average { get { return (uint)(Accumulated / Count); }  }
        public uint Count { get; }
        public uint Severity { set; get; }
        public UnitValue MaxUnit { get; }
    }

    public class UnitTotal
    {
        public UnitTotal(CompilerData.CompileCategory category)
        {
            Category = category;
            Total = 0;
        }

        public CompilerData.CompileCategory Category { get; }
        public ulong  Total { set; get; }
        public double Ratio 
        {
            set {}
            get 
            {
                UnitTotal compilerTotal = CompilerData.Instance.GetTotal(CompilerData.CompileCategory.ExecuteCompiler);
                return compilerTotal != null && compilerTotal.Total > 0? ((double)Total)/compilerTotal.Total : 0;
            } 
        }
    }

    public class UnitValue
    {
        private uint[] values = new uint[(int)CompilerData.CompileThresholds.Display];

        public UnitValue(string name, uint index)
        {
            Name = name;
            Index = index;
        }

        public string Name { get; }

        public uint Thread { set; get; } = 0;
        public ulong Start { set; get; } = 0;

        public uint Index { get; }

        public List<uint> ValuesList { get { return values.ToList(); } }

        public void SetValue(CompilerData.CompileCategory category, uint input)
        {
            if ((int)category < (int)CompilerData.CompileThresholds.Display)
            {
                values[(int)category] = input;
            }
        }
    }
    public class CompileSession
    {
        public uint  Version { set; get; } = 0;
        public ulong FullDuration { set; get; } = 0;
        public uint  NumThreads { set; get; } = 0;
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());
        public static CompilerData Instance { get { return lazy.Value; } }

        public const uint VERSION_MIN = 4;
        public const uint VERSION     = 5;

        //Keep this in sync with the data exporter
        public enum CompileCategory
        {
            Include = 0,
            ParseClass,
            ParseTemplate,
            InstanceClass,
            InstanceFunction,
            InstanceVariable,
            InstanceConcept,
            CodeGeneration,
            OptimizeFunction,

            PendingInstantiations,
            OptimizeModule,
            FrontEnd,
            BackEnd,
            ExecuteCompiler,
            Other,

            RunPass,
            CodeGenPasses,
            PerFunctionPasses,
            PerModulePasses,
            DebugType,
            DebugGlobalVariable,
            Invalid,

            //Meta categories only for the visualizers
            Thread,
            Timeline,

            //Global Counters
            FullCount,
        }

        public enum CompileThresholds
        {
            Severity = CompileCategory.ParseClass,
            Gather = CompileCategory.PendingInstantiations,
            Display = CompileCategory.RunPass,
        }

        public enum DataSource
        {
            Default,
            Forced,
        }

        private CompileScorePackage Package { set; get; }
        private IServiceProvider ServiceProvider { set; get; }

        private string ScoreLocation { set; get; } = "";
        private string SolutionDir { set; get; } = "";
        private List<UnitValue> UnitsCollection { set; get; } = new List<UnitValue>();
        private List<UnitTotal> Totals { set; get; } = new List<UnitTotal>();

        private CompileSession Session { set; get; } = new CompileSession();

        public DataSource Source { private set; get; } = DataSource.Default;

        public class CompileDataset
        {
            public List<CompileValue> collection = new List<CompileValue>();
            public Dictionary<string, CompileValue> dictionary = new Dictionary<string, CompileValue>();
            public List<uint> normalizedThresholds = new List<uint>();
        }

        private CompileDataset[] Datasets { set; get; } = new CompileDataset[(int)CompileThresholds.Gather].Select(h => new CompileDataset()).ToArray();

        //events
        public event Notify ScoreDataChanged;
        public event Notify HighlightModeChanged;

        private CompilerData() { }

        public void Initialize(CompileScorePackage package, IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Package = package;
            ServiceProvider = serviceProvider;

            DocumentLifetimeManager.FileWatchedChanged += OnFileWatchedChanged;
            SettingsManager.SettingsChanged += OnSolutionSettingsChanged;

            var EditorContextInstance = EditorContext.Instance;
            EditorContextInstance.ModeChanged += OnEditorModeChanged;
            EditorContextInstance.ConfigurationChanged += OnSolutionSettingsChanged; //Refresh settings for potential macro variables change
        }

        private void OnEditorModeChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (EditorContext.Instance.Mode != EditorContext.EditorMode.None)
            {
                OnSolutionSettingsChanged();
                OnHighlightModeChanged(); 
            }
        }

        public GeneralSettingsPageGrid GetGeneralSettings()
        {
            return Package == null ? null : Package.GetGeneralSettings();
        }

        public string GetSettingsScoreLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var settings = SettingsManager.Instance.Settings;
            string rawPath = settings.ScoreSource == SolutionSettings.ScoreOrigin.Generator ? settings.ScoreGenerator.OutputPath : settings.ScoreLocation;

            MacroEvaluator evaluator = new MacroEvaluator();
            return EditorUtils.NormalizePath(evaluator.Evaluate(rawPath));
        }

        public CompileSession GetSession()
        {
            return Session;
        }

        public List<UnitTotal> GetTotals()
        {
            return Totals;
        }

        public UnitTotal GetTotal(CompileCategory category)
        {
            return (int)category < (int)CompileThresholds.Display && (int)category < Totals.Count ? Totals[(int)category] : null;
        }

        public List<UnitValue> GetUnits()
        {
            return UnitsCollection;
        }

        public UnitValue GetUnitByIndex(uint index)
        {
            return index < UnitsCollection.Count ? UnitsCollection[(int)index] : null;
        }

        public UnitValue GetUnitByName(string name)
        {
            foreach (UnitValue unit in UnitsCollection)
            {
                if (unit.Name == name)
                {
                    return unit;
                }
            }
            return null;
        }

        public List<CompileValue> GetCollection(CompileCategory category)
        {
            return Datasets[(int)category].collection;
        }

        public string GetScoreFullPath() { return ScoreLocation; }

        private bool SetScoreLocation(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ScoreLocation != input)
            {
                ScoreLocation = input;
                OutputLog.Log("Settings - Score File: " + ScoreLocation);
                return true;
            }
            return false;
        }

        public List<CompileValue> GetValues(CompileCategory category)
        {
            CompileDataset dataset = Datasets[(int)category];
            return dataset.collection;
        }

        public CompileValue GetValue(CompileCategory category, string fileName)
        {
            CompileDataset dataset = Datasets[(int)category];
            if (dataset.dictionary.ContainsKey(fileName)) { return dataset.dictionary[fileName]; }
            return null;
        }

        public CompileValue GetValue(CompileCategory category, int index)
        {
            if ((int)category < (int)CompileThresholds.Gather)
            {
                CompileDataset dataset = Datasets[(int)category];
                return index >= 0 && index < dataset.collection.Count ? dataset.collection[index] : null;
            }
            return null;
        }

        public int GetIndexOf(CompileCategory category, CompileValue value)
        {
            if ((int)category < (int)CompileThresholds.Gather)
            {
                CompileDataset dataset = Datasets[(int)category];
                return dataset.collection.IndexOf(value);
            }
            return -1;
        }

        public UnitValue GetUnit(int index)
        {
            return index >= 0 && index < UnitsCollection.Count ? UnitsCollection[index] : null;
        }

        public void LoadDefaultSource()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetSource(DataSource.Default);
            SetScoreLocation(GetSettingsScoreLocation());
            WatchScoreFile();
            LoadSeverities(ScoreLocation);
        }

        public void ForceLoadFromFilename(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool locationChanged = SetScoreLocation(EditorUtils.NormalizePath(filename));

            if (Source == DataSource.Default)
            {
                if (locationChanged)
                {
                    //requested a force file different than current ( force it and disable file watching ) 
                    SetSource(DataSource.Forced);
                    DocumentLifetimeManager.UnWatchFile();
                }
                else
                {
                    //if the forced file is the same as the current deafult file, then just reenabled the file watcher
                    WatchScoreFile();
                }

                LoadSeverities(ScoreLocation);
            }
            else
            {
                string defaultPath = GetSettingsScoreLocation();
                if (defaultPath == filename)
                {
                    //We are forcing back to default
                    LoadDefaultSource();
                }
                else
                {
                    LoadSeverities(ScoreLocation);
                }
            }
        }

        private void SetSource(DataSource input)
        {
            if (Source != input)
            {
                Source = input; 
            }
        }

        private void ReadCompileUnit(BinaryReader reader, List<UnitValue> list, uint index)
        {
            var name = reader.ReadString();
            var compileData = new UnitValue(name, index);

            if ( Session.Version >= 5 )
            {
                compileData.Start = reader.ReadUInt64();
                compileData.Thread = reader.ReadUInt32();
            }

            for(CompileCategory category = 0; (int)category < (int)CompileThresholds.Display; ++category)
            {
                compileData.SetValue(category, reader.ReadUInt32());
            }

            list.Add(compileData);
        }

        private void ReadCompileValue(BinaryReader reader, List<CompileValue> list)
        {
            var name = reader.ReadString();
            ulong acc = reader.ReadUInt64();
            uint min = reader.ReadUInt32();
            uint max = reader.ReadUInt32();
            uint count = reader.ReadUInt32();
            UnitValue maxUnit = GetUnitByIndex(reader.ReadUInt32());

            var compileData = new CompileValue(name, acc, min, max, count, maxUnit);
            list.Add(compileData);
        }
        private void ClearDatasets()
        {
            for (int i=0;i< (int)CompileThresholds.Gather; ++i)
            {
                CompileDataset dataset = Datasets[i];
                dataset.collection.Clear();
                dataset.dictionary.Clear();
                dataset.normalizedThresholds.Clear();
            }
        }

        public void ReloadSeverities()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            LoadSeverities(ScoreLocation);
        }

        static public bool CheckVersion(uint version)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (version < VERSION_MIN)
            {
                OutputLog.Error("Trying to load an unsupported file Version! Expected a minumum version of " + VERSION_MIN + " - Found " + version + " - Please export again with matching Data Exporter");
                return false;
            }
            
            if (version > VERSION)
            {
                OutputLog.Error("Trying to load an unsupported file Version! Expected a maximum version of " + VERSION + " - Found " + version + " - Please export again with matching Data Exporter");
                return false;
            }

            return true;
        }

        private void LoadSeverities(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Session = new CompileSession();
            UnitsCollection.Clear();
            Totals.Clear();
            ClearDatasets();

            if (File.Exists(fullPath))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    // Read version
                    Session.Version = reader.ReadUInt32();
                    if (CheckVersion(Session.Version))
                    {
                        // Read Header
                        Timeline.CompilerTimeline.Instance.TimelinePacking = reader.ReadUInt32();

                        // Read Units 
                        uint unitsLength = reader.ReadUInt32();
                        var unitList = new List<UnitValue>((int)unitsLength);
                        for (uint i = 0; i < unitsLength; ++i)
                        {
                            ReadCompileUnit(reader, unitList, i);
                        }
                    
                        UnitsCollection = new List<UnitValue>(unitList);

                        //Read Datasets
                        for(int i = 0; i < (int)CompileThresholds.Gather; ++i)
                        {
                            uint dataLength = reader.ReadUInt32();
                            var thislist = new List<CompileValue>((int)dataLength);
                            for (uint k = 0; k < dataLength; ++k)
                            {
                                ReadCompileValue(reader, thislist);
                            }
                            Datasets[i].collection = new List<CompileValue>(thislist);
                        }
                    }
                }

                fileStream.Close();

                //Post process on read data
                PostProcessLoadedData();

                watch.Stop();
                const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
                ulong microseconds = (ulong)(watch.ElapsedTicks/TicksPerMicrosecond);
                OutputLog.Log("Score file processed in "+ Common.UIConverters.GetTimeStr(microseconds));
            }

            RecomputeSeverities();

            ScoreDataChanged?.Invoke();
        }

        public string GetSeverityCriteria()
        {
            string propertyName = GetGeneralSettings().OptionSeverityCriteria.ToString();
            return typeof(CompileValue).GetProperty(propertyName) == null? "Max" : propertyName;
        }

        private void ProcessSeverityData()
        {
            //retrieve the property that we will use for the severity sorting
            PropertyInfo valueCriteria = typeof(CompileValue).GetProperty(GetSeverityCriteria());

            for (int i = 0; i < (int)CompileThresholds.Severity; ++i)
            {
                CompileDataset dataset = Datasets[i];
                List<uint> onlyValues = new List<uint>();
                foreach (CompileValue entry in dataset.collection)
                {
                    onlyValues.Add((uint)valueCriteria.GetValue(entry));
                }
                ComputeNormalizedThresholds(dataset.normalizedThresholds, onlyValues);
            }
        }

        private void PostProcessLoadedData()
        {
            //Build the mapping between names and entries for fast queries
            for (int i = 0; i < (int)CompileThresholds.Severity; ++i)
            {
                CompileDataset dataset = Datasets[i];
                foreach (CompileValue entry in dataset.collection)
                {
                    dataset.dictionary.Add(entry.Name, entry);
                }
            }

            //Compute Severities
            ProcessSeverityData();

            //Process Totals
            Totals = new List<UnitTotal>();
            for (int k = 0; k < (int)CompileThresholds.Display; ++k)
            {
                Totals.Add(new UnitTotal((CompileCategory)k));
            }
            
            foreach (UnitValue unit in UnitsCollection)
            {  
                for(int k = 0; k < (int)CompileThresholds.Display;++k)
                {
                    Totals[k].Total += unit.ValuesList[k];
                }

                if (Session.Version >= 5)
                {
                    Session.FullDuration = Math.Max(Session.FullDuration, unit.Start + unit.ValuesList[(int)CompileCategory.ExecuteCompiler]);
                    Session.NumThreads   = Math.Max(Session.NumThreads,   unit.Thread + 1 );
                }
            }
        }

        private void ComputeNormalizedThresholds(List<uint> normalizedThresholds, List<uint> inputList)
        {
            const int numSeverities = 5; //this should be a constant somewhere else 

            normalizedThresholds.Clear();
            inputList.Sort();

            //Get the theshold values based on the normalized serveirties percentages
            List<float> normalizedSeverites = GetGeneralSettings().GetOptionNormalizedSeverities();

            for (int i = 0; i < numSeverities; ++i)
            {

                float percent = i < normalizedSeverites.Count ? normalizedSeverites[i] : 100.0f;
                float ratio = Math.Max(Math.Min(percent, 100.0f), 0.0f) * 0.01f;
                int index = (int)Math.Round(inputList.Count * ratio);

                if (index < inputList.Count)
                {
                    normalizedThresholds.Add(inputList[index]);
                }
                else
                {
                    normalizedThresholds.Add(uint.MaxValue);
                }
            }
        }

        private void RecomputeSeverities()
        {
            GeneralSettingsPageGrid settings = GetGeneralSettings();

            PropertyInfo valueCriteria = typeof(CompileValue).GetProperty(GetSeverityCriteria());
            
            for (int i = 0; i < (int)CompileThresholds.Severity; ++i)
            {
                CompileDataset dataset = Datasets[i];
                List<uint> thresholdList = settings.OptionNormalizedSeverity ? dataset.normalizedThresholds : settings.GetOptionValueSeverities();
                foreach (CompileValue entry in dataset.collection)
                {
                    entry.Severity = ComputeSeverity(thresholdList, (uint)valueCriteria.GetValue(entry));
                }
            }
        }

        private uint ComputeSeverity(List<uint> thresholds, uint value)
        {
            int ret = thresholds.Count;
            for (int i=0;i<thresholds.Count;++i)
            {
                if ( value < thresholds[i] )
                {
                    ret = i;
                    break;
                }
            }

            return Convert.ToUInt32(ret+1);
        }

        private void OnFileWatchedChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            LoadSeverities(GetScoreFullPath());
        }

        private void WatchScoreFile()
        {
            string realPath = Path.GetDirectoryName(ScoreLocation) + '\\';
            string filename = Path.GetFileName(ScoreLocation);
            DocumentLifetimeManager.WatchFile(realPath, filename);
        }

        private void OnSolutionSettingsChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Source == DataSource.Default)
            {
                if (SetScoreLocation(GetSettingsScoreLocation()))
                {
                    WatchScoreFile();
                    LoadSeverities(ScoreLocation);
                }
            }
        }

        public void OnSettingsSeverityChanged()
        {
            RecomputeSeverities();
            ScoreDataChanged?.Invoke();
        }

        public void OnSettingsSeverityCriteriaChanged()
        {
            ProcessSeverityData();
            OnSettingsSeverityChanged();
        }

        public void OnHighlightModeChanged()
        {
            HighlightModeChanged?.Invoke();
        }
    }
}
