
namespace CompileScore
{
    using EnvDTE80;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

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
            Gather  = CompileCategory.PendingInstantiations,
            Display = CompileCategory.RunPass,
        }

        private CompileScorePackage _package;
        private IServiceProvider _serviceProvider;

        private string _path = "";
        private string _scoreFileName = "";
        private string _solutionDir = "";
        private bool _relativeToSolution = true;

        private List<UnitValue> _unitsCollection = new List<UnitValue>();
        private List<UnitTotal> _totals = new List<UnitTotal>();

        public class CompileDataset
        {
            public List<CompileValue>                 collection = new List<CompileValue>();
            public Dictionary<string, CompileValue>   dictionary = new Dictionary<string, CompileValue>();
            public List<uint>                         normalizedThresholds = new List<uint>();
        }

        private CompileDataset[] _datasets = new CompileDataset[(int)CompileThresholds.Gather].Select(h => new CompileDataset()).ToArray();

        //events
        public event Notify ScoreDataChanged;
        public event Notify HighlightEnabledChanged;

        public static CompilerData Instance { get { return lazy.Value; } }
        private CompilerData(){}

        public void Initialize(CompileScorePackage package, IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _package = package;
            _serviceProvider = serviceProvider;

            DocumentLifetimeManager.FileWatchedChanged += OnFileWatchedChanged;

            RefreshInstance();
        }

        public void RefreshInstance()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_solutionDir.Length == 0 && _serviceProvider != null)
            {    
                DTE2 applicationObject = _serviceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                string solutionDirRaw = applicationObject.Solution.FullName;

                if (solutionDirRaw.Length > 0)
                {
                    //A valid solution folder was found
                    _solutionDir = (Path.HasExtension(solutionDirRaw)? Path.GetDirectoryName(solutionDirRaw) : solutionDirRaw) + '\\';

                    //Get the information from the settings
                    GeneralSettingsPageGrid settings = GetGeneralSettings();
                    if (SetPath(settings.OptionPath) || SetScoreFileName(settings.OptionScoreFileName) || SetRelativeToSolution(settings.OptionPathRelativeToSolution))
                    {
                        ReloadSeverities();
                    }

                    //Trigger settings refresh
                    OnHighlightEnabledChanged();
                }
            }
        }

        public GeneralSettingsPageGrid GetGeneralSettings()
        {
            return _package == null? null : _package.GetGeneralSettings();
        }
        public List<UnitTotal> GetTotals()
        {
            return _totals;
        } 

        public UnitTotal GetTotal(CompileCategory category)
        {
            return (int)category < (int)CompileThresholds.Display && (int)category < _totals.Count? _totals[(int)category] : null;
        }

        public List<UnitValue> GetUnits()
        {
            return _unitsCollection;
        }

        public UnitValue GetUnitByIndex(uint index)
        {
            return index < _unitsCollection.Count ? _unitsCollection[(int)index] : null;
        }

        public UnitValue GetUnitByName(string name)
        {
            foreach(UnitValue unit in _unitsCollection)
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
            return _datasets[(int)category].collection;
        }

        public string GetScoreFullPath() { return GetRealPath() + _scoreFileName; }

        private string GetRealPath() { return _relativeToSolution ? _solutionDir + _path : _path; }

        private bool SetRelativeToSolution(bool input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_relativeToSolution != input)
            {
                _relativeToSolution = input;
                OutputLog.Log(_relativeToSolution? "Path is relative To Solution" : "Path is Global");
                return true;
            }
            return false;
        }

        private bool SetPath(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_path != input)
            {
                _path = input;
                OutputLog.Log("Settings - Score Path: " + _path);
                return true;
            }
            return false;
        }

        private bool SetScoreFileName(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_scoreFileName != input)
            {
                _scoreFileName = input;
                OutputLog.Log("Settings - Score File: " + _scoreFileName);
                return true;
            }
            return false;
        }
         
        public CompileValue GetValue(CompileCategory category,string fileName)
        {
            CompileDataset dataset = _datasets[(int)category];
            if (dataset.dictionary.ContainsKey(fileName)) { return dataset.dictionary[fileName]; }
            return null;
        }

        public CompileValue GetValue(CompileCategory category, int index)
        {
            if ((int)category < (int)CompileThresholds.Gather)
            {
                CompileDataset dataset = _datasets[(int)category];
                return index >= 0 && index < dataset.collection.Count ? dataset.collection[index] : null;
            }
            return null;
        }
        public UnitValue GetUnit(int index)
        {
            return index >= 0 && index < _unitsCollection.Count ? _unitsCollection[index] : null;
        }

        public void ForceLoadFromFilename(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Only call this from the standalone app (this craetes a desync from the VS settings)
            _relativeToSolution = false;
            _path = "";
            _scoreFileName = filename;

            ReloadSeverities();
        }

        public void ReloadSeverities()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string realPath = GetRealPath();

            DocumentLifetimeManager.WatchFile(realPath, _scoreFileName);
            LoadSeverities(realPath + _scoreFileName);
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
                CompileDataset dataset = _datasets[i];
                dataset.collection.Clear();
                dataset.dictionary.Clear();
                dataset.normalizedThresholds.Clear();
            }
        }

        private void LoadSeverities(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _unitsCollection.Clear();
            _totals.Clear();
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
                    
                        _unitsCollection = new List<UnitValue>(unitList);

                        //Read Datasets
                        for(int i = 0; i < (int)CompileThresholds.Gather; ++i)
                        {
                            uint dataLength = reader.ReadUInt32();
                            var thislist = new List<CompileValue>((int)dataLength);
                            for (uint k = 0; k < dataLength; ++k)
                            {
                                ReadCompileValue(reader, thislist);
                            }
                            _datasets[i].collection = new List<CompileValue>(thislist);
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
                CompileDataset dataset = _datasets[i];
                List<uint> onlyValues = new List<uint>();
                foreach (CompileValue entry in dataset.collection)
                {
                    onlyValues.Add(entry.Max);
                    dataset.dictionary.Add(entry.Name, entry);
                }
                ComputeNormalizedThresholds(dataset.normalizedThresholds, onlyValues);
            }

            //Process Totals
            _totals = new List<UnitTotal>();
            for (int k = 0; k < (int)CompileThresholds.Display; ++k)
            {
                _totals.Add(new UnitTotal((CompileCategory)k));
            }
            
            foreach (UnitValue unit in _unitsCollection)
            {  
                
                for(int k = 0; k < (int)CompileThresholds.Display;++k)
                {
                    _totals[k].Total += unit.ValuesList[k];
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
                CompileDataset dataset = _datasets[i];
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

        public void OnSettingsRelativePathChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (SetRelativeToSolution(GetGeneralSettings().OptionPathRelativeToSolution))
            {
                ReloadSeverities();
            }
        }

        public void OnSettingsPathChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (SetPath(GetGeneralSettings().OptionPath))
            {
                ReloadSeverities(); 
            }
        }

        public void OnSettingsScoreFileNameChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (SetScoreFileName(GetGeneralSettings().OptionScoreFileName))
            {
                ReloadSeverities();
            }
        }

        public void OnSettingsSeverityChanged()
        {
            RecomputeSeverities();
            ScoreDataChanged?.Invoke();
        }

        public void OnHighlightEnabledChanged()
        {
            HighlightEnabledChanged?.Invoke();
        }
    }
}
