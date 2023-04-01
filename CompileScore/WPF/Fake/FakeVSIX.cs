using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompileScore
{
    public class MacroEvaluator
    {
        public string Evaluate(string input) { return input; }
    }

    public class ScoreGeneratorSettings
    {
        public string OutputPath { get; set; } = "";
    }

    static public class EditorUtils
    {
        static public string NormalizePath(string input) { return input; }

        static private void OpenFile(string fullPath)
        {
            if (fullPath != null && File.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
        }

        static public void OpenFile(UnitValue unit) 
        {
            OpenFile(CompilerData.Instance.Folders.GetUnitPath(unit));
        }

        static public void OpenFile(CompileValue value)
        {
            OpenFile(CompilerData.Instance.Folders.GetValuePath(value));
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
}
