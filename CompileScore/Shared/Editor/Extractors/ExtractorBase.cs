using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompileScore
{
    public class ProjectProperties
    {
        public enum TargetType
        {
            x86,
            x64,
        }

        public enum StandardVersion
        {
            Default,
            Cpp98,
            Cpp03,
            Cpp14,
            Cpp17,
            Cpp20,
            Gnu98,
            Gnu03,
            Gnu14,
            Gnu17,
            Gnu20,
            Latest,
        }

        public List<string> IncludeDirectories { set; get; } = new List<string>();
        public List<string> ForceIncludes { set; get; } = new List<string>();
        public List<string> PrepocessorDefinitions { set; get; } = new List<string>();
        public string WorkingDirectory { set; get; } = "";
        public string ExtraArguments { set; get; } = "";
        public bool ShowWarnings { set; get; } = false;

        public TargetType Target { set; get; } = TargetType.x64;
        public StandardVersion Standard { set; get; } = StandardVersion.Default;
    }

    public abstract class IExtractor 
    {
        public abstract ProjectProperties GetProjectData(ProjectItem projItem);
        public abstract string EvaluateMacros(string input, Project inputProject);

        protected static void AppendMSBuildStringToList(List<string> list, string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (input == null)
                return;

            var split = input.Split(';').ToList(); //Split
            split.RemoveAll(s => IsMSBuildStringInvalid(s)); //Validate

            foreach (string str in split)
            {
                string trimmedStr = str.Trim();
                if (!list.Contains(trimmedStr))
                {
                    list.Add(trimmedStr);
                }
            }
        }

        protected static void RemoveMSBuildStringFromList(List<string> list, string input)
        {
            var split = input.Split(';').ToList(); //Split

            foreach (string str in split)
            {
                list.Remove(str.Trim());
            }
        }

        private static bool StringHasContent(string input)
        {
            foreach (char c in input)
            {
                if ( c != ' ' && c != '"' && c != '\\' && c != '/')
                {
                    return true;
                }
            }

            return false;
        } 

        protected static bool IsMSBuildStringInvalid(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();     

            if (!StringHasContent(input))
            {
                return true;
            }

            if (input.Contains('$'))
            {
                OutputLog.Log("Dropped " + input + ". It contains an unknown MSBuild macro");
                return true;
            }
            return false;
        }

        protected void AddCustomSettings(ProjectProperties projProperties, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ParserSettings customSettings = SettingsManager.Instance.Settings.ParserSettings;
            if (customSettings != null)
            {
                var evaluatorExtra = new MacroEvaluatorExtra();
                AppendMSBuildStringToList(projProperties.IncludeDirectories, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalIncludeDirs)));
                AppendMSBuildStringToList(projProperties.ForceIncludes, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalForceIncludes)));
                AppendMSBuildStringToList(projProperties.PrepocessorDefinitions, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalPreprocessorDefinitions)));
                projProperties.ExtraArguments = evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalCommandLine));
                projProperties.ShowWarnings = customSettings.EnableWarnings;
            }
        }
    }
}
