using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CompileScore
{
    public interface IMacroEvaluator
    {
        string Evaluate(string input);
        void   Clear();
    }

    public class MacroEvaluatorVisualPlatform : IMacroEvaluator
    {
        private VCPlatform Platform { set; get; }

        public MacroEvaluatorVisualPlatform(VCPlatform platform)
        {
            Platform = platform;
        }

        public string Evaluate(string input)
        {
            return Platform.Evaluate(input);
        }

        public void Clear() { }
    }

    public abstract class MacroEvaluatorDict : IMacroEvaluator
    {
        private Dictionary<string, string> dict = new Dictionary<string, string>();

        protected string MacroRegexPattern { set; get; } = @"(\$\([a-zA-Z0-9_]+\))";

        public abstract string ComputeMacro(string macroStr);

        public string Evaluate(string input)
        {
            return Regex.Replace(input, MacroRegexPattern, delegate (Match m)
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

        public void Clear() 
        {
            dict.Clear();
        }
    }

    public class MacroEvaluatorCMake : MacroEvaluatorDict
    {
        public override string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"$(SolutionDir)")
            {
                return EditorUtils.GetSolutionPath();
            }
            else if (macroStr == @"$(Configuration)")
            {
                return ExtractorCMake.GetActiveConfigurationName();
            }

            return null;
        }
    }

    public class MacroEvaluatorCMakeArgs : MacroEvaluatorDict
    {
        public MacroEvaluatorCMakeArgs()
        {
            MacroRegexPattern = @"(\$\{[a-zA-Z0-9_]+\})";
        }

        public override string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"${projectDir}")
            {
                return EditorUtils.GetSolutionPath();
            }
            else if (macroStr == @"${name}")
            {
                return ExtractorCMake.GetActiveConfigurationName();
            }

            return null;
        }
    }

    public class MacroEvaluatorExtra : MacroEvaluatorDict
    {
        public override string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"$(UE4ModuleName)")
            {
                Document doc = EditorUtils.GetActiveDocument();
                if (doc == null) return null;

                string modulePath = ExtractorUnreal.GetModulePath(doc.FullName);
                if (modulePath == null) return null;

                string moduleName = Path.GetFileName(modulePath);
                OutputLog.Log("UE4 Module Name: " + moduleName);
                return moduleName;
            }

            if (macroStr == @"$(ExtensionInstallationDir)")
            {
                return EditorUtils.GetExtensionInstallationDirectory();
            }

            return null;
        }
    }

    public class MacroEvaluatorProfiler : MacroEvaluatorDict
    {
        public override string ComputeMacro(string macroStr)
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
            else if (macroStr == "$(Generator_OverviewDetail)")
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
