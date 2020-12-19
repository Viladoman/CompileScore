using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompileScore
{
    public class MacroEvaluator
    {
        public string Evaluate(string input) { return input; }
    }

    public class SolutionSettings
    {
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
        private static readonly Lazy<EditorContext> lazy = new Lazy<EditorContext>(() => new EditorContext());
        public static EditorContext Instance { get { return lazy.Value; } }

        public string RootPath { get; } = "";

        public event Notify ModeChanged;
        public event Notify ConfigurationChanged;

        public void DummyFunction() { ModeChanged?.Invoke(); ConfigurationChanged?.Invoke(); }

    }
}
