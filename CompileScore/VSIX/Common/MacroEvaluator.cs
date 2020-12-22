using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CompileScore
{
    public class MacroEvaluator
    { 
        private Dictionary<string, string> dict = new Dictionary<string, string>();

        public void Clear()
        {
            dict.Clear();
        }

        public string Evaluate(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return Regex.Replace(input, @"(\$\([a-zA-Z0-9_]+\))", delegate (Match m)
            {
                if (dict.ContainsKey(m.Value))
                {
                    return dict[m.Value];
                }

                string macroValue = ComputeMacro(m.Value);

                if (macroValue != null)
                {
                    dict[m.Value] = macroValue;
                    return macroValue;
                }

                return m.Value;
            });
        }

        private string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"$(SolutionDir)")
            {
                return EditorContext.Instance.RootPath;
            }
            else if (macroStr == @"$(SolutionName)")
            {
                return EditorContext.Instance.GetWorkspaceName();
            }
            else if (macroStr == @"$(Configuration)")
            {
                return EditorContext.Instance.ConfigurationName;
            }
            else if (macroStr == "$(Platform)")
            {
                return EditorContext.Instance.PlatformName;
            }
            else if ( macroStr == "$(Generator_OverviewDetail)")
            {
                return SettingsManager.Instance.Settings.ScoreGenerator.OverviewDetail.ToString();
            }
            else if (macroStr == "$(Generator_TimelineDetail)")
            {
                return SettingsManager.Instance.Settings.ScoreGenerator.TimelineDetail.ToString();
            }

            return null;
        }
    }
}
