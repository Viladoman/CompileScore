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

        static public bool IsCustomScoreSource(SolutionSettings input)
        {
            return input.ScoreSource == SolutionSettings.ScoreOrigin.Custom;
        }

        static public bool DisplayCMakeCommandsFile(SolutionSettings input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return EditorUtils.GetEditorMode() == EditorUtils.EditorMode.CMake;
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

        [UIDescription(Label = "Extract Includers", Tooltip = "If true it will extract the includers data (.incl file). This data contains which elements include other elements.")]
        public bool ExtractIncluders { set; get; } = true;

        [UIDescription(Label = "Collapse Template Arguments", Tooltip = "If true it will collapse all template arguments aggregating all instances of the same template to the same entry.")]
        public bool CollapseTemplateArgs{ set; get; } = true;
    }

    public class ParserSettings
    {
        [UIDescription(Label = "Automatic Extraction", Tooltip = "If true, it will try to extract the architecture, include paths, preprocessor macros... from the current solution.")]
        public bool AutomaticExtraction { set; get; } = true;

        [UIDescription(Label = "Explicit Commands File", FilterMethod = "DisplayCMakeCommandsFile", Tooltip = "File location for the build commands exported by CMAKE_EXPORT_COMPILE_COMMANDS=1 (This fields allows a limited set of $(SolutionDir) style macros)")]
        public string CMakeCommandsFile { set; get; } = "";

        [UIDescription(Label = "Extra Preprocessor Defintions", Tooltip = "Additional preprocessor definitions on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalPreprocessorDefinitions { set; get; } = "";

        [UIDescription(Label = "Extra Include Dirs", Tooltip = "Additional include directories on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalIncludeDirs { set; get; } = "";

        [UIDescription(Label = "Extra Force Includes", Tooltip = "Additional files to force include on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalForceIncludes { set; get; } = "";

        [UIDescription(Label = "Extra Parser Args", Tooltip = "Additional command line arguments passed in to the clang parser. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalCommandLine { set; get; } = "";

        [UIDescription(Label = "Enable Warnings", Tooltip = "If true, the clang parser will output the warnings found.")]
        public bool EnableWarnings { set; get; } = false;

        [UIDescription(Label = "Parser Output Folder", Tooltip = "File location where the Clang Parser will output the layout results. This files are temporary. This field will default to the extension installation folder. (This fields allows $(SolutionDir) style macros)")]
        public string ParserOutputFolder { set; get; } = "";

    };


    public class SolutionSettings
    {
        public enum ScoreOrigin
        {
            Generator,
            Custom
        }

        [UIDescription(Label = "Score File Source")]
        public ScoreOrigin ScoreSource { set; get; } = ScoreOrigin.Custom;

        [UIDescription(Label = "Score File Location", FilterMethod = "IsCustomScoreSource")]
        public string ScoreLocation { set; get; } = @"$(SolutionDir)compileData.scor";

        [UIDescription(Label = "Generator")]
        public ScoreGeneratorSettings ScoreGenerator { set; get; } = new ScoreGeneratorSettings();

        [UIDescription(Label = "Parser")]
        public ParserSettings ParserSettings { set; get; } = new ParserSettings();
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
