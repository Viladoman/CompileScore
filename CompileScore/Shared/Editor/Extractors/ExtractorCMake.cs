using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore
{
    public class ExtractorCMake : IExtractor
    {
        public class CMakeActiveConfiguration
        {
            public string CurrentProjectSetting { set; get; }
        }

        class CMakeCommandEntry
        {
            public string directory { set; get; }
            public string command { set; get; }
            public string file { set; get; }
        }

        public class CMakeConfiguration
        {
            public string name { set; get; }
            public string buildRoot { set; get; }
            public string installRoot { set; get; }
        }

        public class CMakeSettings
        { 
            public List<CMakeConfiguration> configurations { set; get; }
        }

        static public string GetActiveConfigurationName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = EditorUtils.GetSolutionPath();
            if (solutionPath == null || solutionPath.Length == 0) return null;

            string activeConfigFilename = solutionPath + @".vs\ProjectSettings.json";
            if (File.Exists(activeConfigFilename))
            {
                var activeConfig = new CMakeActiveConfiguration();

                try
                {
                    string jsonString = File.ReadAllText(activeConfigFilename);
                    activeConfig = JsonConvert.DeserializeObject<CMakeActiveConfiguration>(jsonString);
                }
                catch (Exception e)
                {
                    Parser.LogError(e.Message);
                }

                if (activeConfig != null)
                {
                    return activeConfig.CurrentProjectSetting;
                }
            }

            return null;
        }

        public override ProjectProperties GetProjectData(ProjectItem projItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Parser.Log("Capturing configuration from CMake...");

            if (projItem == null) 
                return null; 

            var evaluator = new MacroEvaluatorCMake();
            var customSettings = SettingsManager.Instance.Settings.ParserSettings;
            string commandsFile = customSettings == null ? null : customSettings.CMakeCommandsFile;
            commandsFile = commandsFile == null ? null : evaluator.Evaluate(commandsFile);

            if (commandsFile == null || commandsFile.Length == 0)
            {
                var activeConfig = GetActiveConfiguration();
                if (activeConfig != null)
                {
                    var cmakeArgsEvaluator = new MacroEvaluatorCMakeArgs();
                    commandsFile = cmakeArgsEvaluator.Evaluate(activeConfig.buildRoot);
                    if(commandsFile.Length > 0)
                    {
                        char lastchar = commandsFile[commandsFile.Length - 1];
                        if (lastchar != '\\' && lastchar != '/')
                        {
                            commandsFile += '\\';
                        }
                        commandsFile += "compile_commands.json";
                    }
                }
            }

            var ret = CaptureCMakeCommands(commandsFile, EditorUtils.GetProjectItemFullPath( projItem ) );

            AddCustomSettings(ret, evaluator);

            return ret;
        }

        public override string EvaluateMacros(string input, Project project)
        {
            var evaluatorExtra = new MacroEvaluatorExtra();          
            var evaluatorPlatform = new MacroEvaluatorCMake();
            return evaluatorPlatform.Evaluate(evaluatorExtra.Evaluate(input));
        }

        private CMakeConfiguration GetActiveConfiguration()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = EditorUtils.GetSolutionPath();
            if (solutionPath == null || solutionPath.Length == 0) return null;

            string settingsFilename = solutionPath + "CMakeSettings.json";
            if (File.Exists(settingsFilename))
            {
                CMakeSettings settings = null;

                try
                {
                    string jsonString = File.ReadAllText(settingsFilename);
                    settings = JsonConvert.DeserializeObject<CMakeSettings>(jsonString);
                }
                catch (Exception e)
                {
                    Parser.LogError(e.Message);
                }

                string activeConfigName = GetActiveConfigurationName();
                if (activeConfigName != null && activeConfigName.Length > 0 && settings != null && settings.configurations != null)
                {
                    foreach(CMakeConfiguration config in settings.configurations)
                    {
                        if (config.name == activeConfigName)
                        {
                            return config;
                        }
                    }
                }
            }

            return null;
        } 

        private List<string> ParseCommands(string input)
        {
            List<string> ret = new List<string>();

            bool inQuotes = false;
            string current = "";

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        ret.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0)
            {
                ret.Add(current);
            }

            return ret;
        }

        private void ExtractCMakeProjectProperties(ProjectProperties inout, CMakeCommandEntry command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (command == null) return;

            Parser.Log("Extracting commands from Translation Unit: " + command.file);

            inout.WorkingDirectory = command.directory;

            List<string> commands = ParseCommands(command.command);

            for (int i = 0; i < commands.Count; ++i)
            {
                string com = commands[i];

                if (com.Length > 2 && com[0] == '-' || com[0] == '/')
                {
                    if (com[1] == 'D')
                    {
                        inout.PrepocessorDefinitions.Add(com.Substring(2, com.Length - 2));
                    }
                    else if (com[1] == 'I')
                    {
                        inout.IncludeDirectories.Add(com.Substring(2, com.Length - 2));
                    }
                    else if (com == "-m32")
                    {
                        inout.Target = ProjectProperties.TargetType.x86;
                    }
                    else if (com == "-m64")
                    {
                        inout.Target = ProjectProperties.TargetType.x64;
                    }
                    else if (com.StartsWith("-std"))
                    {
                        string standard = com.Substring(5, com.Length - 5);
                        if (standard == "c++98") inout.Standard = ProjectProperties.StandardVersion.Cpp98;
                        else if (standard == "c++03") inout.Standard = ProjectProperties.StandardVersion.Cpp03;
                        else if (standard == "c++14") inout.Standard = ProjectProperties.StandardVersion.Cpp14;
                        else if (standard == "c++17") inout.Standard = ProjectProperties.StandardVersion.Cpp17;
                        else if (standard == "c++20") inout.Standard = ProjectProperties.StandardVersion.Cpp20;
                        else if (standard == "gnu++98") inout.Standard = ProjectProperties.StandardVersion.Gnu98;
                        else if (standard == "gnu++03") inout.Standard = ProjectProperties.StandardVersion.Gnu03;
                        else if (standard == "gnu++14") inout.Standard = ProjectProperties.StandardVersion.Gnu14;
                        else if (standard == "gnu++17") inout.Standard = ProjectProperties.StandardVersion.Gnu17;
                        else if (standard == "gnu++20") inout.Standard = ProjectProperties.StandardVersion.Gnu20;
                        else inout.Standard = ProjectProperties.StandardVersion.Latest;
                    }
                    else if (com.StartsWith("-include"))
                    {
                        inout.IncludeDirectories.Add(com.Substring(8, com.Length - 8));
                    }
                }
            }
        }

        private int PathCompareScore(string a, string b)
        {
            int ret = 0;
            if (a != null && b != null)
            {
                int max = Math.Min(a.Length, b.Length);
                for (; ret < max && a[ret] == b[ret]; ++ret) { }
            }

            return ret;
        }

        private CMakeCommandEntry FindBestCMakeEntry(List<CMakeCommandEntry> commands, string documentName)
        {
            if (commands == null || documentName == null) { return null; }

            string documentFolder = Path.GetDirectoryName(documentName) + '/';
            int documentFolderLength = (documentFolder.Length - 1);
            string documentFileName = Path.GetFileNameWithoutExtension(documentName);
            string lowerInput = documentFolder.Replace('\\', '/').ToLower();
            int bestScoreMatch = 0;
            int bestUpperFolder = Int32.MaxValue;
            CMakeCommandEntry bestEntry = null;

            foreach (CMakeCommandEntry entry in commands)
            {
                //This assumes no ../../ in those file and documentName paths
                string entryLower = entry.file.Replace('\\', '/').ToLower();
                int scoreMatch = PathCompareScore(lowerInput, entryLower);
                if (scoreMatch >= bestScoreMatch)
                {
                    string entryDir = Path.GetDirectoryName(entry.file);
                    int thisUpperFolder = entryDir.Length - documentFolderLength;

                    if (thisUpperFolder == 0 && documentFileName == Path.GetFileNameWithoutExtension(entry.file))
                    {
                        //Same file with different extension match
                        //Early exit 
                        return entry;
                    }

                    if (scoreMatch > bestScoreMatch || (thisUpperFolder >= 0 && thisUpperFolder < bestUpperFolder))
                    {
                        bestScoreMatch = scoreMatch;
                        bestUpperFolder = thisUpperFolder;
                        bestEntry = entry;
                    }
                }
            }

            return bestEntry;
        }

        private ProjectProperties CaptureCMakeCommands(string commandsFile, string documentName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var ret = new ProjectProperties();

            if (commandsFile == null || commandsFile.Length == 0)
            {
                Parser.Log("Unable to retrieve a CMake commands file.\nMake sure the cmake flag -DCMAKE_EXPORT_COMPILE_COMMANDS=1 is set and the options are pointing to the proper file.");
            }
            else
            {
                if (File.Exists(commandsFile))
                {
                    Parser.Log("Capturing commands from CMake commands file: " + commandsFile);

                    List<CMakeCommandEntry> allCommands = null;
                    try
                    {
                        string jsonString = File.ReadAllText(commandsFile);
                        allCommands = JsonConvert.DeserializeObject<List<CMakeCommandEntry>>(jsonString);
                    }
                    catch (Exception e)
                    {
                        Parser.LogError(e.Message);
                    }

                    ExtractCMakeProjectProperties(ret, FindBestCMakeEntry(allCommands, documentName));
                }
                else
                {
                    Parser.Log("Unable to find CMake commands file at " + commandsFile + ".\nMake sure the cmake flag -DCMAKE_EXPORT_COMPILE_COMMANDS=1 is set and the options are pointing to the proper file.");
                }
            }

            return ret;
        }

    }
}
