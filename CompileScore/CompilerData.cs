
namespace CompileScore
{
    using EnvDTE80;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Security.Permissions;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    public delegate void Notify();  // delegate

    public class CompileValue
    {
        public CompileValue(uint mean, uint min, uint max, uint count)
        {
            Mean = mean;
            Min = min;
            Max = max;
            Count = count;
            Severity = 0;
        }

        public uint Mean { get; }
        public uint Min { get; }
        public uint Max { get; }
        public uint Count { get; }
        public uint Severity { set; get; }
    }

    public sealed class CompilerData
    {
        private static readonly Lazy<CompilerData> lazy = new Lazy<CompilerData>(() => new CompilerData());

        private CompileScorePackage _package;


        private string _path = "";
        private string _includeFileName = "";
        private string _solutionDir = "";

        private Dictionary<string, CompileValue> _values = new Dictionary<string, CompileValue>();
        private List<uint> _normalizedThresholds = new List<uint>();

        public event Notify IncludeDataChanged; // event

        public static CompilerData Instance { get { return lazy.Value; } }
        private CompilerData(){}

        public void Initialize(CompileScorePackage package, IServiceProvider servicePRovider)
        {
            _package = package;

            //Extract the solution Dir
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE2 applicationObject = servicePRovider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            _solutionDir = Path.GetDirectoryName(applicationObject.Solution.FullName) + '\\';

            //Get the information from the settings
            GeneralSettingsPageGrid settings = _package.GetGeneralSettings();
            if (SetPath(settings.OptionPath) || SetIncludeFileName(settings.OptionIncludeFileName))
            {
                ReloadSeverities();
            }

            DocumentLifetimeManager.FileWatchedChanged += OnFileWatchedChanged;
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
            if (_values.ContainsKey(fileName)) { return _values[fileName]; }
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
            _values.Clear();
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
                        _values.Add(match.Groups[1].Value.ToLower(),new CompileValue(mean,min,max,count));
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
            GeneralSettingsPageGrid settings = _package.GetGeneralSettings();
            List<uint> thresholdList = settings.OptionNormalizedSeverity ? _normalizedThresholds : settings.GetOptionSeverities();

            foreach (KeyValuePair<string, CompileValue> entry in _values)
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
            if (SetPath(_package.GetGeneralSettings().OptionPath))
            {
                ReloadSeverities(); 
            }

            //TODO ~ ramonv ~ change other services too 
        }

        public void OnSettingsIncludeFileNameChanged()
        {
            if (SetIncludeFileName(_package.GetGeneralSettings().OptionIncludeFileName))
            {
                ReloadSeverities();
            }
        }

        public void OnSettingsSeverityChanged()
        {
            RecomputeSeverities();
            IncludeDataChanged?.Invoke();
        }
    }
}
