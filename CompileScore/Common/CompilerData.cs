
namespace CompileScore
{
    using EnvDTE;
    using EnvDTE80;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.RegularExpressions;
    using System.Security.Permissions;
    using System.Linq;
    using System.Windows;
    using System.Windows.Forms;

    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(string name, ulong accumulated, uint min, uint max, uint count)
        {
            Name = name;
            Accumulated = accumulated;
            Min = min;
            Max = max;
            Count = count;
            Severity = 0;
        }

        public string Name { get; }
        public uint Max { get; }
        public uint Min { get; }
        public ulong Accumulated { get; }
        public uint Mean { get { return (uint)(Accumulated / Count); }  }
        public uint Count { get; }
        public uint Severity { set; get; }
    }

    public class FullUnitValue
    {
        public FullUnitValue(string name)
        {
            Name = name;
        }

        public string Name { get; }

        //TODO ~ ramonv ~ Convert all the list below in an array for better scalability

        public uint Frontend { set; get; }
        public uint Backend { set; get; }
        public uint Source { set; get; }
        public uint ParseClass { set; get; }
        public uint ParseTemplate { set; get; }
        public uint InstantiateClass { set; get; }
        public uint InstantiateFunction { set; get; }
        public uint PendingInstantations { set; get; }
        public uint Codegen { set; get; }
        public uint RunPass { set; get; }
        public uint OptModule { set; get; }
        public uint OptFunction { set; get; }
        public uint Other { set; get; }
        public uint Duration { set; get; } 
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());

        const uint dataVersion = 1;

        //Keep this in sync with the data exporter
        public enum CompileCategory
        {
            Include = 0,
            ParseClass,
            ParseTemplate,
            InstanceClass, 
            InstanceFunction,
            CodeGeneration, 
            OptimizeModule, 
            OptimizeFunction,
            Other
        }

        private CompileScorePackage _package;
        private IServiceProvider _serviceProvider;

        private string _path = "";
        private string _scoreFileName = "";
        private string _solutionDir = "";

        public ObservableCollection<FullUnitValue> _unitsCollection = new ObservableCollection<FullUnitValue>();

        public class CompileDataset
        {
            public ObservableCollection<CompileValue> collection = new ObservableCollection<CompileValue>();
            public Dictionary<string, CompileValue>   dictionary = new Dictionary<string, CompileValue>();
            public List<uint>                         normalizedThresholds = new List<uint>();
        }

        private CompileDataset[] _datasets = new CompileDataset[Enum.GetNames(typeof(CompileCategory)).Length].Select(h => new CompileDataset()).ToArray();

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
                    if (SetPath(settings.OptionPath) || SetScoreFileName(settings.OptionScoreFileName))
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

        public ObservableCollection<FullUnitValue> GetUnits()
        {
            return _unitsCollection;
        }

        public ObservableCollection<CompileValue> GetCollection(CompileCategory category)
        {
            return _datasets[(int)category].collection;
        } 

        private bool SetPath(string input)
        {
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

        private void ReloadSeverities()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string realPath = _solutionDir + _path;

            DocumentLifetimeManager.WatchFile(realPath, _scoreFileName);
            LoadSeverities(realPath + _scoreFileName);
        }

        private void ReadCompileUnit(BinaryReader reader, List<FullUnitValue> list)
        {
            var name = reader.ReadString();
            var compileData = new FullUnitValue(name.ToLower()); //TODO ~ ramonv ~ move this to export on next data version upgrade

            compileData.Source = reader.ReadUInt32();
            compileData.ParseClass = reader.ReadUInt32();
            compileData.ParseTemplate = reader.ReadUInt32();
            compileData.InstantiateClass = reader.ReadUInt32();
            compileData.InstantiateFunction = reader.ReadUInt32();
            compileData.Codegen = reader.ReadUInt32();
            compileData.OptModule = reader.ReadUInt32();
            compileData.OptFunction = reader.ReadUInt32();
            compileData.Other = reader.ReadUInt32();
            compileData.RunPass = reader.ReadUInt32();
            compileData.PendingInstantations = reader.ReadUInt32();
            compileData.Frontend = reader.ReadUInt32();
            compileData.Backend = reader.ReadUInt32();
            compileData.Duration = reader.ReadUInt32();

            list.Add(compileData);
        }

        private void ReadCompileValue(BinaryReader reader, List<CompileValue> list)
        {
            var name = reader.ReadString();
            ulong acc = reader.ReadUInt64();
            uint min = reader.ReadUInt32();
            uint max = reader.ReadUInt32();
            uint count = reader.ReadUInt32();

            var compileData = new CompileValue(name, acc, min, max, count);
            list.Add(compileData);
        }
        private void ClearDatasets()
        {
            for (int i=0;i< Enum.GetNames(typeof(CompileCategory)).Length;++i)
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
            ClearDatasets();

            if (File.Exists(fullPath))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint version = reader.ReadUInt32();
                    if (version == dataVersion)
                    {
                        // Read Units 
                        uint unitsLength = reader.ReadUInt32();
                        var unitList = new List<FullUnitValue>((int)unitsLength);
                        for (uint i = 0; i < unitsLength; ++i)
                        {
                            ReadCompileUnit(reader, unitList);
                        }
                    
                        _unitsCollection = new ObservableCollection<FullUnitValue>(unitList);

                        //Read Datasets
                        for(int i = 0; i < Enum.GetNames(typeof(CompileCategory)).Length; ++i)
                        {
                            uint dataLength = reader.ReadUInt32();
                            var thislist = new List<CompileValue>((int)dataLength);
                            for (uint k = 0; k < dataLength; ++k)
                            {
                                ReadCompileValue(reader, thislist);
                            }
                            _datasets[i].collection = new ObservableCollection<CompileValue>(thislist);
                        }
                    }
                    else
                    {
                        OutputLog.Error("Version mismatch! Expected "+version+" - Found "+dataVersion);
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
            //TODO ~ ramonv ~ for the time being we are only using Include data for this
            //                Only store dictionary for it for now
            const int i = (int)CompileCategory.Include;

            //for(int i = 0; i < Enum.GetNames(typeof(CompileCategory)).Length; ++i)
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

            for (int i = 0; i < Enum.GetNames(typeof(CompileCategory)).Length; ++i)
            {
                CompileDataset dataset = _datasets[(int)CompileCategory.Include];
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
            LoadSeverities(_solutionDir + _path + _scoreFileName);
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
