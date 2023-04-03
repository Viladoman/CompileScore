using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;

namespace CompileScore
{
    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(string name, ulong accumulated, ulong selfAccumulated, uint min, uint max, uint selfMax, uint count, UnitValue maxUnit, UnitValue selfMaxUnit)
        {
            Name = name;
            Accumulated = accumulated;
            SelfAccumulated = selfAccumulated;
            Min = min;
            Max = max;
            SelfMax = selfMax;
            Count = count;
            MaxUnit = maxUnit;
            SelfMaxUnit = selfMaxUnit;
            Severity = 0;
        }

        public string Name { get; }
        public uint Max { get; }
        public uint Min { get; }
        public uint SelfMax { get; }
		public ulong Accumulated { get; }
		public ulong SelfAccumulated { get; }
		public uint Average { get { return (uint)(Accumulated / Count); }  }
        public uint Count { get; }
        public uint Severity { set; get; }
        public UnitValue MaxUnit { get; }
        public UnitValue SelfMaxUnit { get; }
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

    public class CompileSession
    {
        public uint  Version { set; get; } = 0;
        public uint  TimelinePacking { set; get; } = 0; 
        public ulong FullDuration { set; get; } = 0;
        public uint  NumThreads { set; get; } = 0;
    }

    public class CompileDataset
    {
        public List<CompileValue> collection = new List<CompileValue>();
        public Dictionary<string, CompileValue> dictionary = new Dictionary<string, CompileValue>();
        public List<uint> normalizedThresholds = new List<uint>();
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());
        public static CompilerData Instance { get { return lazy.Value; } }

        public const uint VERSION_MIN = 9;
        public const uint VERSION = 9;

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

        public enum HydrateFlag
        {
            Main = 1,
            Globals = 2,
        }

        private CompileScorePackage Package { set; get; }
        private IServiceProvider ServiceProvider { set; get; }

        private string ScoreLocation { set; get; } = "";

        private List<UnitValue> UnitsCollection { set; get; } = new List<UnitValue>();
        private List<UnitTotal> Totals { set; get; } = new List<UnitTotal>();
        private CompileSession Session { set; get; } = new CompileSession();

        private uint HydrationFlags { set; get; } = 0;
        private uint LoadingFlags { set; get; } = 0;
        private uint LoadingBatch { set; get; } = 0;

        public DataSource Source { private set; get; } = DataSource.Default;

        public CompileFolders Folders { private set; get; } = new CompileFolders();

        private CompileDataset[] Datasets { set; get; } = new CompileDataset[(int)CompileThresholds.Gather].Select(h => new CompileDataset()).ToArray();

        //load structures 

        private class MainLoadChunk
        {
            public uint LoadingBatch { set; get; } = 0;
            public CompileSession Session { set; get; } = new CompileSession();
            public List<UnitTotal> Totals { set; get; } = new List<UnitTotal>();
            public List<UnitValue> Units { set; get; } = new List<UnitValue>();
            public CompileDataset[] Datasets { set; get; } = new CompileDataset[(int)CompileThresholds.Gather].Select(h => new CompileDataset()).ToArray();
            public CompileFolders Folders { set; get; }  = new CompileFolders();
        }

        private class GlobalsChunk
        {
            public uint LoadingBatch { set; get; } = 0;
            public CompileDataset[] Datasets { set; get; } = new CompileDataset[(int)CompileThresholds.Gather].Select(h => new CompileDataset()).ToArray();
        }

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

        public static UnitValue GetUnitByIndex(uint index, List<UnitValue> units)
        {
            return index < units.Count ? units[(int)index] : null;
        }

        public UnitValue GetUnitByIndex(uint index)
        {
            return GetUnitByIndex(index, UnitsCollection);
        }

        public UnitValue GetUnitByName(string name)
        {
            //This function should be the last resort as it might confuse files with the same name and different paths
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

        public CompileValue GetValueByName(CompileCategory category, string fileName)
        {
            //Caution: This might return a different value if 2 values have the same name
            CompileDataset dataset = Datasets[(int)category];
            if (dataset.dictionary.ContainsKey(fileName)) { return dataset.dictionary[fileName]; }
            return null;
        }

        public static CompileValue GetValue(CompileCategory category, int index, CompileDataset[] datasets)
        {
            if ((int)category < (int)CompileThresholds.Gather)
            {
                CompileDataset dataset = datasets[(int)category];
                return index >= 0 && index < dataset.collection.Count ? dataset.collection[index] : null;
            }
            return null;
        }

        public CompileValue GetValue(CompileCategory category, int index)
        {
            return GetValue(category, index, Datasets);
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
            LoadScore(ScoreLocation);
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

                LoadScore(ScoreLocation);
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
                    LoadScore(ScoreLocation);
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

        private static void ReadSession(BinaryReader reader, uint version,  CompileSession session, List<UnitTotal> totals)
        {
            session.Version = version;
            session.TimelinePacking = reader.ReadUInt32();
            session.FullDuration = reader.ReadUInt64();
            session.NumThreads = reader.ReadUInt32();

            for (int k = 0; k < (int)CompileThresholds.Display; ++k)
            {
                UnitTotal total = new UnitTotal((CompileCategory)k);
                total.Total = reader.ReadUInt64();
                totals.Add(total);
            }
        }

        private static void ReadCompileUnit(BinaryReader reader, List<UnitValue> list, uint index)
        {
            var name = reader.ReadString();
            var compileData = new UnitValue(name, index);

            for (CompileCategory category = 0; (int)category < (int)CompileThresholds.Display; ++category)
            {
                compileData.SetValue(category, reader.ReadUInt32());
            }

            list.Add(compileData);
        }

        private static void ReadCompileValue(BinaryReader reader, List<CompileValue> list, List<UnitValue> units)
        {
            var name = reader.ReadString();
            ulong acc = reader.ReadUInt64();
            ulong selfAcc = reader.ReadUInt64();
            uint min = reader.ReadUInt32();
            uint max = reader.ReadUInt32();
            uint selfMax = reader.ReadUInt32();
            uint count = reader.ReadUInt32();
            UnitValue maxUnit = GetUnitByIndex(reader.ReadUInt32(), units);
            UnitValue selfMaxUnit = GetUnitByIndex(reader.ReadUInt32(), units);

            var compileData = new CompileValue(name, acc, selfAcc, min, max, selfMax,count, maxUnit, selfMaxUnit);
            list.Add(compileData);
        }

        private void ClearDatasets()
        {
            for (int i = 0; i < (int)CompileThresholds.Gather; ++i)
            {
                CompileDataset dataset = Datasets[i];
                dataset.collection.Clear();
                dataset.dictionary.Clear();
                dataset.normalizedThresholds.Clear();
            }
        }

        public void ReloadScore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            LoadScore(ScoreLocation);
        }

        static public bool CheckVersion(uint version)
        {
            if (version < VERSION_MIN || version > VERSION )
            {
                _ = OutputLog.ErrorGlobalAsync("Trying to load an unsupported file Version! Expected a version between " + VERSION_MIN + " and " + VERSION + " - Found " + version + " - Please export again with matching Data Exporter");
                return false;
            }

            return true;
        }

        public void Hydrate(HydrateFlag flag)
        {
            if ((HydrationFlags & (uint)flag) == 0)
            {
                HydrationFlags |= (uint)flag;

                switch (flag)
                {
                    case HydrateFlag.Main:
                        LoadMainScore(ScoreLocation);
                        break;
                    case HydrateFlag.Globals: 
                        LoadGlobals(ScoreLocation); 
                        break;
                }
            }
        }

        public bool IsLoadingData()
        {
            return LoadingFlags != 0;
        }

        private void TryNotifyDataChanged()
        {
            if ( !IsLoadingData() )
            {
                ScoreDataChanged?.Invoke(); 
            }
        }

        private void LoadScore(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            uint restoreHydration = HydrationFlags;

            //Increase the score Instance number
            ++LoadingBatch;

            //Clean the data
            Session = new CompileSession();
            Folders = new CompileFolders();
            HydrationFlags = 0;
            UnitsCollection.Clear();
            Totals.Clear();
            ClearDatasets();

            foreach (HydrateFlag flag in (HydrateFlag[])Enum.GetValues(typeof(HydrateFlag)))
            {
                if ((restoreHydration & (uint)flag) != 0)
                {
                    Hydrate(flag);
                }
            }

            TryNotifyDataChanged();
        }

        private static void ReadMainScore(string fullPath, MainLoadChunk chunk)
        {
            if (File.Exists(fullPath))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    // Read version
                    uint version = reader.ReadUInt32(); 
                    if (CheckVersion(version))
                    {
                        // Read Session
                        ReadSession(reader, version, chunk.Session, chunk.Totals);

                        // Read Units 
                        uint unitsLength = reader.ReadUInt32();
                        chunk.Units = new List<UnitValue>((int)unitsLength);
                        for (uint i = 0; i < unitsLength; ++i)
                        {
                            ReadCompileUnit(reader, chunk.Units, i);
                        }

                        //Read Main Datasets
                        for (int i = 0; i < (int)CompileThresholds.Severity; ++i)
                        {
                            uint dataLength = reader.ReadUInt32();
                            var thislist = new List<CompileValue>((int)dataLength);
                            for (uint k = 0; k < dataLength; ++k)
                            {
                                ReadCompileValue(reader, thislist, chunk.Units);
                            }
                            chunk.Datasets[i].collection = new List<CompileValue>(thislist);
                        }

                        chunk.Folders.ReadFolders(reader, chunk.Units, chunk.Datasets);
                    }
                }

                fileStream.Close();

                //Post process on read data
                PostProcessLoadedData(chunk.Datasets);

                watch.Stop();
                const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
                ulong microseconds = (ulong)(watch.ElapsedTicks / TicksPerMicrosecond);
                _ = OutputLog.LogGlobalAsync("Score file main processed in " + Common.UIConverters.GetTimeStr(microseconds));
            }
        }

        private static void ReadGlobals( string fullPath, GlobalsChunk chunk, List<UnitValue> Units )
        {
            string gblFullPath = fullPath + ".gbl";
            if (File.Exists(gblFullPath))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                FileStream fileStream = File.Open(gblFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint version = reader.ReadUInt32();
                    if (CheckVersion(version))
                    {
                        //Read Remaining Datasets
                        for (int i = (int)CompileThresholds.Severity; i < (int)CompileThresholds.Gather; ++i)
                        {
                            uint dataLength = reader.ReadUInt32();
                            var thislist = new List<CompileValue>((int)dataLength);
                            for (uint k = 0; k < dataLength; ++k)
                            {
                                ReadCompileValue(reader, thislist, Units);
                            }
                            chunk.Datasets[i].collection = new List<CompileValue>(thislist);
                        }
                    }
                }

                fileStream.Close();

                watch.Stop();
                const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
                ulong microseconds = (ulong)(watch.ElapsedTicks / TicksPerMicrosecond);
                _ = OutputLog.LogGlobalAsync("Score file globals processed in " + Common.UIConverters.GetTimeStr(microseconds));
            }
        }

        private void ApplyLoadChunk(MainLoadChunk chunk)
        {
            Session = chunk.Session;
            Totals = chunk.Totals; 
            UnitsCollection = chunk.Units;
            Datasets = chunk.Datasets;
            Folders = chunk.Folders;

            //Propagate session information
            if ( Session.TimelinePacking > 0 )
            {
                Timeline.CompilerTimeline.Instance.TimelinePacking = Session.TimelinePacking; 
            }

            //Compute Severities
            ProcessSeverityData();
            RecomputeSeverities();
        }

        private void ApplyLoadChunk(GlobalsChunk chunk)
        {
            //Read Remaining Datasets
            for (int i = (int)CompileThresholds.Severity; i < (int)CompileThresholds.Gather; ++i)
            {
                Datasets[i] = chunk.Datasets[i];
            }
        }
             

        private void LoadMainScore(string fullPath)
        {
            MainLoadChunk chunk = new MainLoadChunk();
            chunk.LoadingBatch = LoadingBatch;
            LoadingFlags |= (uint)HydrateFlag.Main;

            Common.ThreadUtils.Fork(async delegate
            {
                ReadMainScore(fullPath, chunk);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (LoadingBatch == chunk.LoadingBatch)
                {
                    ApplyLoadChunk(chunk);
                    LoadingFlags &= ~(uint)HydrateFlag.Main;
                    TryNotifyDataChanged();
                }
            });
        }

        private void LoadGlobals(string fullPath)
        {
            //This depends on the main info being ready
            Hydrate(HydrateFlag.Main);

            GlobalsChunk chunk = new GlobalsChunk();
            chunk.LoadingBatch = LoadingBatch;
            LoadingFlags |= (uint)HydrateFlag.Globals;

            Common.ThreadUtils.Fork(async delegate
            {
                //Wait for the Main data to be ready and applied as we need it to load and populate the globals
                while ((LoadingFlags & (uint)HydrateFlag.Main) != 0)
                {
                    await System.Threading.Tasks.Task.Delay(50);
                }

                if (Totals.Count > 0)
                {
                    //Only read the globals if there is data to read
                    ReadGlobals(fullPath, chunk, UnitsCollection);
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                //Only finish the job if this is the latest request
                if (LoadingBatch == chunk.LoadingBatch)
                {
                    ApplyLoadChunk(chunk);
                    LoadingFlags &= ~(uint)HydrateFlag.Globals;
                    TryNotifyDataChanged();
                }
            });
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

        private static void PostProcessLoadedData(CompileDataset[] datasets)
        {
            //Build the mapping between names and entries for fast queries
            for (int i = 0; i < (int)CompileThresholds.Severity; ++i)
            {
                CompileDataset dataset = datasets[i];
                foreach (CompileValue entry in dataset.collection)
                {
                    //Unique insert ( this will cause incorrect results when dealing with multiple files with the same name )
                    if ( !dataset.dictionary.ContainsKey(entry.Name) )
                    {
                        dataset.dictionary.Add(entry.Name, entry); 
                    }
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

            if ( settings == null )
            {
                return;
            }

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
            LoadScore(GetScoreFullPath());
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
                    LoadScore(ScoreLocation);
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
