
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

    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(string name, uint accumulated, uint min, uint max, uint count)
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
        public uint Accumulated { get; }
        public uint Mean { get { return Accumulated / Count; }  }
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
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());

        public enum CompileCategory
        {
            Include = 0,
            ParseClass,
            ParseTemplate,
            InstanceClass, 
            InstanceFunction,
            CodeGeneration, 
            OptimizeFunction,
            OptimizeModule, 
            Other
        }

        private CompileScorePackage _package;
        private IServiceProvider _serviceProvider;

        private string _path = "";
        private string _includeFileName = "";
        private string _solutionDir = "";

        public ObservableCollection<FullUnitValue> _unitsCollection = new ObservableCollection<FullUnitValue>();

        public class CompileDataset
        {
            public ObservableCollection<CompileValue> collection = new ObservableCollection<CompileValue>();
            public Dictionary<string, CompileValue>   dictionary = new Dictionary<string, CompileValue>();
            public List<uint>                         normalizedThresholds = new List<uint>();
        }

        //private CompileDataset _includeDataset = new CompileDataset();

        private CompileDataset[] _datasets = new CompileDataset[Enum.GetNames(typeof(CompileCategory)).Length].Select(h => new CompileDataset()).ToArray();

        //events
        public event Notify IncludeDataChanged;
        public event Notify HighlightEnabledChanged;

        public static CompilerData Instance { get { return lazy.Value; } }
        private CompilerData(){}

        public void Initialize(CompileScorePackage package, IServiceProvider serviceProvider)
        {
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
                    if (SetPath(settings.OptionPath) || SetIncludeFileName(settings.OptionIncludeFileName))
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
                return true;
            }
            return false;
        }

        private bool SetIncludeFileName(string input)
        {
            if (_includeFileName != input)
            {
                _includeFileName = input;
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
            string realPath = _solutionDir + _path;

            DocumentLifetimeManager.WatchFile(realPath, _includeFileName);
            LoadSeverities(realPath + _includeFileName);
        }

        private void ParseCompileUnit(string line, ObservableCollection<FullUnitValue> units)
        {
            Match match = Regex.Match(line, @"(.*)\:(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+):(\d+)");
            if (match.Success)
            {

                var name = match.Groups[1].Value.ToLower();
                var compileData = new FullUnitValue(name);

                compileData.Source               = UInt32.Parse(match.Groups[2].Value);
                compileData.ParseClass           = UInt32.Parse(match.Groups[3].Value);
                compileData.ParseTemplate        = UInt32.Parse(match.Groups[4].Value);
                compileData.InstantiateClass     = UInt32.Parse(match.Groups[5].Value);
                compileData.InstantiateFunction  = UInt32.Parse(match.Groups[6].Value);
                compileData.Codegen              = UInt32.Parse(match.Groups[7].Value);
                compileData.OptModule            = UInt32.Parse(match.Groups[8].Value);
                compileData.OptFunction          = UInt32.Parse(match.Groups[9].Value);
                compileData.Other                = UInt32.Parse(match.Groups[10].Value);
                compileData.RunPass              = UInt32.Parse(match.Groups[11].Value);
                compileData.PendingInstantations = UInt32.Parse(match.Groups[12].Value);
                compileData.Frontend             = UInt32.Parse(match.Groups[13].Value);
                compileData.Backend              = UInt32.Parse(match.Groups[14].Value);

                units.Add(compileData);
            }
        }

        private void ParseCompileValue(string line, CompileDataset dataset)
        {
            Match match = Regex.Match(line, @"(.*)\:(\d+):(\d+):(\d+):(\d+)");
            if (match.Success)
            {
                uint acc = UInt32.Parse(match.Groups[2].Value);
                uint min = UInt32.Parse(match.Groups[3].Value);
                uint max = UInt32.Parse(match.Groups[4].Value);
                uint count = UInt32.Parse(match.Groups[5].Value);

                var name = match.Groups[1].Value.ToLower();
                var compileData = new CompileValue(name, acc, min, max, count);
                dataset.collection.Add(compileData);
            }
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
            ClearDatasets();

            if (File.Exists(fullPath))
            {
                CompileDataset currentDataset = null;

                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader streamReader = new StreamReader(fileStream);
                while (streamReader.Peek() > -1)
                {
                    String line = streamReader.ReadLine();
                    if (line.Length > 0 && line[0] == ':')
                    {
                        //Change the current dataset
                        if (line.ToLower() == ":includes")              { currentDataset = _datasets[(int)CompileCategory.Include]; }
                        else if (line.ToLower() == ":parseclass")       { currentDataset = _datasets[(int)CompileCategory.ParseClass]; }
                        else if (line.ToLower() == ":parsetemplate")    { currentDataset = _datasets[(int)CompileCategory.ParseTemplate]; }
                        else if (line.ToLower() == ":instanceclass")    { currentDataset = _datasets[(int)CompileCategory.InstanceClass]; }
                        else if (line.ToLower() == ":instancefunction") { currentDataset = _datasets[(int)CompileCategory.InstanceFunction]; }
                        else if (line.ToLower() == ":codegen")          { currentDataset = _datasets[(int)CompileCategory.CodeGeneration]; }
                        else if (line.ToLower() == ":optfunction")      { currentDataset = _datasets[(int)CompileCategory.OptimizeFunction]; }
                        else if (line.ToLower() == ":optmodule")        { currentDataset = _datasets[(int)CompileCategory.OptimizeModule]; }
                        else if (line.ToLower() == ":other")            { currentDataset = _datasets[(int)CompileCategory.Other]; }
                        else { currentDataset = null; }
                    }
                    else
                    {
                        if (currentDataset == null)
                        {
                            ParseCompileUnit(line, _unitsCollection);
                        } 
                        else 
                        {
                            ParseCompileValue(line, currentDataset);
                        }
                    } 
                }

                //Post process on read data
                PostProcessLoadedData();
            }

            RecomputeSeverities();
            IncludeDataChanged?.Invoke();
        }

        private void PostProcessLoadedData()
        {
            for(int i = 0; i < Enum.GetNames(typeof(CompileCategory)).Length; ++i)
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
            LoadSeverities(_solutionDir + _path + _includeFileName);
        } 

        public void OnSettingsPathChanged()
        {
            if (SetPath(GetGeneralSettings().OptionPath))
            {
                ReloadSeverities(); 
            }
        }

        public void OnSettingsIncludeFileNameChanged()
        {
            if (SetIncludeFileName(GetGeneralSettings().OptionIncludeFileName))
            {
                ReloadSeverities();
            }
        }

        public void OnSettingsSeverityChanged()
        {
            RecomputeSeverities();
            IncludeDataChanged?.Invoke();
        }

        public void OnHighlightEnabledChanged()
        {
            HighlightEnabledChanged?.Invoke();
        }
    }
}
