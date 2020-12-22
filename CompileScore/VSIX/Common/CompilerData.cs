using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;

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
        public uint Mean { get { return (uint)(Accumulated / Count); }  }
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

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());
        public static CompilerData Instance { get { return lazy.Value; } }

        public const uint VERSION = 4;

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
        public UnitValue GetUnit(int index)
        {
            return index >= 0 && index < UnitsCollection.Count ? UnitsCollection[index] : null;
        }

        public void LoadDefaultSource()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetSource(DataSource.Default);
            MacroEvaluator evaluator = new MacroEvaluator();
            SetScoreLocation(evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreLocation));
            WatchScoreFile();
            LoadSeverities(ScoreLocation);
        }

        public void ForceLoadFromFilename(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool locationChanged = SetScoreLocation(filename);

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
                MacroEvaluator evaluator = new MacroEvaluator();
                string defaultPath = evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreLocation);
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

        private void LoadSeverities(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
                    uint thisVersion = reader.ReadUInt32();
                    if (thisVersion == VERSION)
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
                    else
                    {
                        OutputLog.Error("Version mismatch! Expected "+ VERSION + " - Found "+ thisVersion + " - Please export again with matching Data Exporter");
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

        private void PostProcessLoadedData()
        {
            //For the time being we are only using Include data for this
            //Only store dictionary for it for now
            const int i = (int)CompileCategory.Include;

            //for(int i = 0; i < (int)CompileCategory.GahterCount; ++i)
            {
                CompileDataset dataset = Datasets[i];
                List<uint> onlyValues = new List<uint>();
                foreach (CompileValue entry in dataset.collection)
                {
                    onlyValues.Add(entry.Max);
                    dataset.dictionary.Add(entry.Name, entry);
                }
                ComputeNormalizedThresholds(dataset.normalizedThresholds, onlyValues);
            }

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
            }
        }

        private void ComputeNormalizedThresholds(List<uint> normalizedThresholds, List<uint> inputList)
        {
            const int numSeverities = 5; //this should be a constant somewhere else 

            normalizedThresholds.Clear();
            inputList.Sort();

            float division = (float)inputList.Count / (float)numSeverities;
            int elementsPerBucket = (int)Math.Round(division);

            int index = elementsPerBucket;

            for (int i = 0; i < numSeverities; ++i)
            {
                if (index < inputList.Count)
                {
                    normalizedThresholds.Add(inputList[index]);
                }
                else
                {
                    normalizedThresholds.Add(uint.MaxValue);
                }

                index += elementsPerBucket;
            }
        }

        private void RecomputeSeverities()
        {
            GeneralSettingsPageGrid settings = GetGeneralSettings();

            //For the time being we are only using Include data for this
            //Only compute it for include for now 
            const int i = (int)CompileCategory.Include;

            //for (int i = 0; i < (int)CompileCategory.GatherCount; ++i)
            {
                CompileDataset dataset = Datasets[i];
                List<uint> thresholdList = settings.OptionNormalizedSeverity ? dataset.normalizedThresholds : settings.GetOptionSeverities();
                foreach (CompileValue entry in dataset.collection)
                {
                    entry.Severity = ComputeSeverity(thresholdList, entry.Max);
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
                    ret = i+1;
                    break;
                }
            }

            return Convert.ToUInt32(ret);
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
                MacroEvaluator evaluator = new MacroEvaluator();
                if (SetScoreLocation(evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreLocation)))
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

        public void OnHighlightModeChanged()
        {
            HighlightModeChanged?.Invoke();
        }
    }
}
