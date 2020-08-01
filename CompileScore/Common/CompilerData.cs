
namespace CompileScore
{
    using EnvDTE80;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.RegularExpressions;
    using System.Security.Permissions;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using EnvDTE;

    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(string name, uint mean, uint min, uint max, uint count)
        {
            Name = name;
            Mean = mean;
            Min = min;
            Max = max;
            Count = count;
            Severity = 0;
        }

        public string Name { get; }
        public uint Max { get; }
        public uint Min { get; }
        public uint Mean { get; }
        public uint Count { get; }
        public uint Severity { set; get; }
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());

        private CompileScorePackage _package;
        private IServiceProvider _serviceProvider;

        private string _path = "";
        private string _includeFileName = "";
        private string _solutionDir = "";

        private ObservableCollection<CompileValue> _includeCollection = new ObservableCollection<CompileValue>();

        private Dictionary<string, CompileValue> _includeDict = new Dictionary<string, CompileValue>();
        private List<uint> _normalizedThresholds = new List<uint>();

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

        public ObservableCollection<CompileValue> GetIncludeCollection()
        {
            return _includeCollection;
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
         
        public CompileValue GetValue(string fileName)
        {
            if (_includeDict.ContainsKey(fileName)) { return _includeDict[fileName]; }
            return null;
        }

        private void ReloadSeverities()
        {
            string realPath = _solutionDir + _path;

            DocumentLifetimeManager.WatchFile(realPath, _includeFileName);
            LoadSeverities(realPath + _includeFileName);
        }

        private void LoadSeverities(string fullPath)
        {
            _includeDict.Clear();
            _includeCollection.Clear();
            List<uint> onlyValues = new List<uint>();

            if (File.Exists(fullPath))
            {
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader streamReader = new StreamReader(fileStream);
                while (streamReader.Peek() > -1)
                {
                    String line = streamReader.ReadLine();
                    Match match = Regex.Match(line, @"(.*)\:(\d+):(\d+):(\d+):(\d+)");
                    if (match.Success)
                    {
                        uint mean = UInt32.Parse(match.Groups[2].Value);
                        uint min = UInt32.Parse(match.Groups[3].Value);
                        uint max = UInt32.Parse(match.Groups[4].Value);
                        uint count = UInt32.Parse(match.Groups[5].Value);

                        onlyValues.Add(max);
                        var name = match.Groups[1].Value.ToLower();
                        var compileData = new CompileValue(name, mean, min, max, count);
                        _includeCollection.Add(compileData);
                        _includeDict.Add(name, compileData);
                    }
                }

                ComputeNormalizedThresholds(onlyValues);
            }

            RecomputeSeverities();

            IncludeDataChanged?.Invoke();
        }

        private void ComputeNormalizedThresholds(List<uint> inputList)
        {
            const int numSeverities = 5; //this should be a constant somewhere else 

            _normalizedThresholds.Clear();
            inputList.Sort();

            float division = (float)inputList.Count / (float)numSeverities;
            int elementsPerBucket = (int)Math.Round(division);

            int index = elementsPerBucket;

            for (int i = 0; i < numSeverities; ++i)
            {
                if (index < inputList.Count)
                {
                    _normalizedThresholds.Add(inputList[index]);
                }
                else
                {
                    _normalizedThresholds.Add(uint.MaxValue);
                }

                index += elementsPerBucket;
            }
        }

        private void RecomputeSeverities()
        {
            //Get table and options from 
            GeneralSettingsPageGrid settings = GetGeneralSettings();
            List<uint> thresholdList = settings.OptionNormalizedSeverity ? _normalizedThresholds : settings.GetOptionSeverities();

            foreach (KeyValuePair<string, CompileValue> entry in _includeDict)
            {
                entry.Value.Severity = ComputeSeverity(thresholdList, entry.Value.Max);
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
