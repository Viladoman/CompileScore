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
        private static readonly Lazy<SettingsManager> lazy = new Lazy<SettingsManager>(() => new SettingsManager());
        public static SettingsManager Instance { get { return lazy.Value; } }

        public SolutionSettings Settings { set; get; } = new SolutionSettings();

        public void Initialize(string solutionDir) { }
    }
}
