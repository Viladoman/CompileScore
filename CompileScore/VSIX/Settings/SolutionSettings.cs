using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.IO;

namespace CompileScore
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UIDescription : Attribute
    {
        public string Label { get; set; }
        public string Tooltip { get; set; }
        public string FilterMethod { get; set; }
    }

    static public class UISettingsFilters
    {
        static public bool IsClangCompiler(SolutionSettings input) 
        { 
            return input.ScoreGenerator.Compiler == Profiler.Compiler.Clang; 
        }
    }

    public class ScoreGeneratorSettings
    {
        //Attributes
        [UIDescription(Label = "Compiler", Tooltip = "Sets the system to use the Clang (.json traces) or MSVC (.etl traces) generator")]
        public Profiler.Compiler Compiler { set; get; } = Profiler.Compiler.MSVC;

        [UIDescription(Label = "Clang Traces Path", Tooltip = "Path to the Clang '-ftime-trace' compiler data.", FilterMethod = "IsClangCompiler")]
        public string InputPath { set; get; } = "$(SolutionDir)";

        [UIDescription(Label = "Output File", Tooltip = "Location where the .scor file will be generated")]
        public string OutputPath { set; get; } = @"$(SolutionDir)compileData.scor";

        [UIDescription(Label="Overview Detail", Tooltip = "The exported detail level for the overview and globals tables.")]
        public Profiler.ExtractorDetail OverviewDetail { set; get; } = Profiler.ExtractorDetail.Basic;

        [UIDescription(Label="Timeline Detail", Tooltip = "The exported detail level for the timeline nodes. Useful to reduce the .scor.txxxx file sizes or improve packing")]
        public Profiler.ExtractorDetail TimelineDetail { set; get; } = Profiler.ExtractorDetail.Basic;

        [UIDescription(Label="Timeline Packing", Tooltip = "The number of timelines packed in the same file. if 0 no timeline will be created.")]
        public uint TimelinePacking { set; get; } = 100;
    }

    public class SolutionSettings
    {
        [UIDescription(Label = "Score File Location")]
        public string ScoreLocation { set; get; } = @"$(SolutionDir)compileData.scor";

        [UIDescription(Label = "Generator")]
        public ScoreGeneratorSettings ScoreGenerator { set; get; } = new ScoreGeneratorSettings();
    }

    public class SettingsManager
    {
        public static event Notify SettingsChanged;

        private static readonly Lazy<SettingsManager> lazy = new Lazy<SettingsManager>(() => new SettingsManager());
        public static SettingsManager Instance { get { return lazy.Value; } }

        const string SettingsName = "CompileScoreSettings.json";

        public SolutionSettings Settings { get; set; } = new SolutionSettings();
        private string Filename{ set; get; }
        private Common.FileWatcher Watcher { set; get; }  = new Common.FileWatcher();

        public void Initialize()
        {
            Watcher.FileWatchedChanged += Load;
            EditorContext.Instance.ModeChanged += OnEditorModeChanged;
        }

        private void OnEditorModeChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var EditorContextInstance = EditorContext.Instance;
            SetFilename(EditorContextInstance.Mode == EditorContext.EditorMode.None? null : EditorContextInstance.RootPath + SettingsName);
        }

        private void SetFilename(string str)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Filename != str)
            {
                Watcher.Unwatch();
                Filename = str;
                Load();
                Watcher.Watch(Filename);
            }
        }

        private void Load()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Filename != null && File.Exists(Filename))
            {
                try
                {
                    string jsonString = File.ReadAllText(Filename);
                    Settings = JsonConvert.DeserializeObject<SolutionSettings>(jsonString);
                    SettingsChanged?.Invoke();
                }
                catch(Exception e)
                {
                    OutputLog.Error(e.Message);
                }
            }
        }

        public void Save()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Filename != null && Settings != null)
            {
                try
                {
                    string jsonString = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                    File.WriteAllText(Filename, jsonString);
                    SettingsChanged?.Invoke();
                }
                catch (Exception e)
                {
                    OutputLog.Error(e.Message);
                }
            }
        }

        private static T CloneJson<T>(T source)
        {
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public void OpenSettingsWindow()
        {
            SettingsWindow optionsWindow = new SettingsWindow(CloneJson<SolutionSettings>(Settings));
            optionsWindow.ShowDialog();
        }
    }
}
