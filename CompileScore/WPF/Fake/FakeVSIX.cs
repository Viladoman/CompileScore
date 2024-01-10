using System;
using System.IO;

namespace CompileScore
{
    public class IMacroEvaluator
    {
        public string Evaluate(string input) { return input; }
    }

    public class MacroEvaluatorProfiler : IMacroEvaluator { }

    public class ScoreGeneratorSettings
    {
        public string OutputPath { get; set; } = "";
    }

    static public class EditorUtils
    {
        static public string NormalizePath(string input) { return input; }

        static public string RemapFullPath(string input) { return input; }

        static private void OpenFile(string fullPath)
        {
            if (fullPath != null && File.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
        }

        static public void OpenFileByName(string fullPath, string fileName = null)
        {
            OpenFile(fullPath);
        }

        static public void OpenFile(UnitValue unit) 
        {
            OpenFile(CompilerData.Instance.Folders.GetUnitPath(unit));
        }

        static public void OpenFile(CompileValue value)
        {
            OpenFile(CompilerData.Instance.Folders.GetValuePath(value));
        }

        static public bool OpenFileAtLocation(string fullPath, uint line, uint column)
        {
            OpenFile(fullPath);
            return true;
        }

        static public string GetFileNameSafe(string input)
        {
            try
            {
                return Path.GetFileName(input);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    public class SolutionSettings
    {
        public enum ScoreOrigin
        {
            Generator, 
            Custom
        }

        public ScoreGeneratorSettings ScoreGenerator { set; get; } = new ScoreGeneratorSettings();

        public ScoreOrigin ScoreSource { set; get; } = ScoreOrigin.Generator;

        public string ScoreLocation { set; get; } = "";
    }

    public class SettingsManager
    {
        public static event Notify SettingsChanged;

        private static readonly Lazy<SettingsManager> lazy = new Lazy<SettingsManager>(() => new SettingsManager());
        public static SettingsManager Instance { get { return lazy.Value; } }

        public SolutionSettings Settings { set; get; } = new SolutionSettings();

        public void Initialize(string rootPath) { }

        public void DummyFunction() { SettingsChanged?.Invoke(); }
    }

    public class EditorContext
    {
        public enum EditorMode
        {
            None,
        }

        public enum ExecutionEnvironment
        {
            Standalone,
            VisualStudio, 
        }

        public const ExecutionEnvironment Environment = ExecutionEnvironment.Standalone;
        static public bool IsEnvironment(ExecutionEnvironment input) { return Environment == input; }

        private static readonly Lazy<EditorContext> lazy = new Lazy<EditorContext>(() => new EditorContext());
        public static EditorContext Instance { get { return lazy.Value; } }

        public EditorMode Mode { set; get; } = EditorMode.None;

        public string RootPath { get; } = "";

        public event Notify ModeChanged;
        public event Notify ConfigurationChanged;

        public void DummyFunction() { ModeChanged?.Invoke(); ConfigurationChanged?.Invoke(); }

    }

    public class ParserProcessor
    {
        public static void ParsePath(string fullPath) { }
        public static void ParseActiveDocument() { }
    }
}
