// File that will handle the triggering of the parser

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Newtonsoft.Json;

namespace CompileScore
{
    public static class Parser
    {
        static public void LogClear() { ThreadHelper.ThrowIfNotOnUIThread(); OutputLog.Clear( OutputLog.PaneInstance.Parser); }
        static public void LogFocus() { ThreadHelper.ThrowIfNotOnUIThread(); OutputLog.Focus( OutputLog.PaneInstance.Parser); }
        static public void Log(string input) { ThreadHelper.ThrowIfNotOnUIThread(); OutputLog.Log(input, OutputLog.PaneInstance.Parser); }
        static public void LogError(string input) { ThreadHelper.ThrowIfNotOnUIThread(); OutputLog.Error(input, OutputLog.PaneInstance.Parser); }

        public static async Task<string> ParseClangAsync(ProjectProperties projProperties, string inputFilename, string outputDirectory)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (inputFilename == null || inputFilename.Length == 0)
            {
                LogError("No file provided for parsing");
                return null;
            }

            string toolPath = GetParserToolPath();
            if (toolPath == null)
            {
                LogError("Unable to find the parser tool");
                return null;
            }

            string errorStr = CreateDirectory(outputDirectory);
            if (errorStr != null)
            {
                LogError("Invalid output directory");
                return null;
            }

            AdjustPaths(projProperties.IncludeDirectories);
            AdjustPaths(projProperties.ForceIncludes);

            string includes = GenerateCommandStr("-I", projProperties.IncludeDirectories);
            string forceInc = GenerateCommandStr("-include", projProperties.ForceIncludes);
            string defines = GenerateCommandStr("-D", projProperties.PrepocessorDefinitions);
            string workDir = projProperties.WorkingDirectory.Length == 0 ? "" : " -working-directory=" + AdjustPath(projProperties.WorkingDirectory);
            string flags = projProperties.ShowWarnings ? "" : " -w";
            string extra = projProperties.ExtraArguments.Length == 0 ? "" : " " + projProperties.ExtraArguments;

            string standard = GetStandardFlag(projProperties.Standard);
            string language = Path.GetExtension(inputFilename) == ".c" ? "" : " -x c++"; //do not force c++ on .c files 
            string archStr = projProperties != null && projProperties.Target == ProjectProperties.TargetType.x86 ? " -m32" : " -m64";

            string clangCmd = language + archStr + standard + flags + defines + includes + forceInc + workDir + extra;

            string outputPath = Path.Combine(outputDirectory, "tempResult.cspbin");

            string compileCommandsDir = Path.Combine(outputDirectory, "compile_commands");
            errorStr = CreateDirectory(compileCommandsDir);
            if (errorStr != null)
            {
                LogError("Invalid output directory: " + errorStr);
                return null;
            }

            string compileCommandsFilePath = Path.Combine(compileCommandsDir, "compile_commands.json");
            using (StreamWriter compileCommandsFile = File.CreateText(compileCommandsFilePath))
            using (JsonTextWriter writer = new JsonTextWriter(compileCommandsFile))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartArray();
                writer.WriteStartObject();
                if (projProperties.WorkingDirectory != null)
                {
                    writer.WritePropertyName("directory");
                    writer.WriteValue(projProperties.WorkingDirectory);
                }
                writer.WritePropertyName("command");
                writer.WriteValue($"clang {clangCmd} {AdjustPath(inputFilename)}");
                writer.WritePropertyName("file");
                writer.WriteValue(inputFilename);
            }

            //TODO ~ at the moment just output on the output pane ( binarize the data to the output file and build a proper UI for this )
            string toolCmd = $"-print -o={AdjustPath(outputPath)} -p {AdjustPath(compileCommandsDir)} {AdjustPath(inputFilename)}";

            LogFocus();
            Log("Searching Code Requirements for " + inputFilename + "...");

            if (ParserProcessor.GetParserSettings().OptionParserShowDetailedCommandLine)
            {
                Log($"TOOL ARGUMENTS: {toolCmd}");
                Log($"CLANG ARGUMENTS: {clangCmd}");
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            ExternalProcess externalProcess = new ExternalProcess();
            externalProcess.LogPane = OutputLog.PaneInstance.Parser;
            int exitCode = await externalProcess.ExecuteAsync(toolPath, toolCmd);
            watch.Stop();

            if (exitCode != 0)
            {
                LogError("The Compile Score parser failed to parse this document. (" + GetTimeStr(watch) + ")");
                return null;
            }

            Log("Parsing completed! (" + GetTimeStr(watch) + ")");
            return outputPath;
        }
        private static string GetStandardFlag(ProjectProperties.StandardVersion standard)
        {
            switch (standard)
            {
                case ProjectProperties.StandardVersion.Cpp98: return " -std=c++98";
                case ProjectProperties.StandardVersion.Cpp03: return " -std=c++03";
                case ProjectProperties.StandardVersion.Cpp14: return " -std=c++14";
                case ProjectProperties.StandardVersion.Cpp17: return " -std=c++17";
                case ProjectProperties.StandardVersion.Cpp20: return " -std=c++20";
                case ProjectProperties.StandardVersion.Gnu98: return " -std=gnu++98";
                case ProjectProperties.StandardVersion.Gnu03: return " -std=gnu++03";
                case ProjectProperties.StandardVersion.Gnu14: return " -std=gnu++14";
                case ProjectProperties.StandardVersion.Gnu17: return " -std=gnu++17";
                case ProjectProperties.StandardVersion.Gnu20: return " -std=gnu++20";
                case ProjectProperties.StandardVersion.Latest: return " -std=c++2b";
                default: return "";
            }
        }

        private static string GetParserToolPath()
        {
            return EditorUtils.GetToolPath(@"External\Parser\CompileScoreParser.exe");
        }

        private static string CreateDirectory(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                string errorStr = "Unable to create directory '" + path + "'.\n" + e.ToString();
                LogError(errorStr);
                return errorStr;
            }

            return null;
        }

        private static string AdjustPath(string input)
        {
            return input.Contains(' ') ? '"' + input + '"' : input;
        }

        private static void AdjustPaths(List<string> list)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                list[i] = AdjustPath(list[i]);
            }
        }

        private static string GenerateCommandStr(string prefix, List<string> args)
        {
            string ret = "";
            if (args != null)
            {
                foreach (string value in args)
                {
                    ret += " " + prefix + value;
                }
            }

            return ret;
        }
        private static string GetTimeStr(ulong uSeconds)
        {
            ulong ms = uSeconds / 1000;
            ulong us = uSeconds - (ms * 1000);
            ulong sec = ms / 1000;
            ms = ms - (sec * 1000);
            ulong min = sec / 60;
            sec = sec - (min * 60);
            ulong hour = min / 60;
            min = min - (hour * 60);

            if (hour > 0) { return hour + " h " + min + " m"; }
            if (min > 0) { return min + " m " + sec + " s"; }
            if (sec > 0) { return sec + "." + ms.ToString().PadLeft(4, '0') + " s"; }
            if (ms > 0) { return ms + "." + us.ToString().PadLeft(4, '0') + " ms"; }
            if (us > 0) { return us + " μs"; }
            return "< 1 μs";
        }

        private static string GetTimeStr(System.Diagnostics.Stopwatch watch)
        {
            const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
            return GetTimeStr((ulong)(watch.ElapsedTicks / TicksPerMicrosecond));
        }

    }


    public class ParserProcessor
    {
        private static readonly Lazy<ParserProcessor> lazy = new Lazy<ParserProcessor>(() => new ParserProcessor());
        public static ParserProcessor Instance { get { return lazy.Value; } }

        static public ParserSettingsPageGrid GetParserSettings() { return (ParserSettingsPageGrid)EditorUtils.Package.GetDialogPage(typeof(ParserSettingsPageGrid)); }

        public static void ParsePath(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ProjectItem item = EditorUtils.FindFilenameInProject(fullPath);
            if ( item == null )
            {
                MessageWindow.Display(new MessageContent("Unable to find file: " + fullPath));
                return;
            }

            _ = ParseProjectItemAsync(item);
        }

        public static void ParseActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document activeDocument = EditorUtils.GetActiveDocument(); 
            if ( activeDocument == null )
            {
                MessageWindow.Display(new MessageContent("Unable to retrieve active document."));
                return;
            }

            EditorUtils.SaveActiveDocument();

            _ = ParseProjectItemAsync(activeDocument.ProjectItem);
        }

        private static async System.Threading.Tasks.Task ParseProjectItemAsync(ProjectItem item)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Parser.LogClear();

            ProjectProperties properties = GetProjectData(item);
            if (properties == null)
            {
                Parser.LogError("Unable to retrieve the project configuration");
                return;
            }

            string itemFullPath = EditorUtils.GetProjectItemFullPath(item);

            string outputFilePath = await Parser.ParseClangAsync(properties, itemFullPath, GetParserOutputDirectory(item.ContainingProject));
            ParserData.Instance.LoadUnitFile(outputFilePath);

            Requirements.RequirementsWindow window = ParserData.FocusRequirementsWindow();
            window.SetRequirements(itemFullPath);
        }

        private static string GetParserOutputDirectory(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings.ParserSettings;
            string dir = customSettings == null ? null : customSettings.ParserOutputFolder;

            if (dir == null || dir.Length == 0)
            {
                //default to the extension installation directory
                dir = @"$(ExtensionInstallationDir)Generated";
            }

            dir = GetProjectExtractor(project).EvaluateMacros(dir,project);

            if (dir != null && dir.Length > 0)
            {
                //make sure the directory has a proper format
                char lastchar = dir[dir.Length - 1];
                if (lastchar != '\\' && lastchar != '/')
                {
                    dir += '\\';
                }
            }

            return dir;
        }
        private static IExtractor GetProjectExtractor(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings.ParserSettings;
            if (customSettings == null || customSettings.AutomaticExtraction)
            {
                switch (EditorUtils.GetEditorMode(project))
                {
                    case EditorUtils.EditorMode.UnrealEngine: return new ExtractorUnreal();
                    case EditorUtils.EditorMode.VisualStudio: return new ExtractorVisualStudio();
                    case EditorUtils.EditorMode.CMake: return new ExtractorCMake();
                }
            }
            else
            {
                return new ExtractorManual();
            }

            return null;
        }

        private static ProjectProperties GetProjectData(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var extractor = GetProjectExtractor(item.ContainingProject);
            return extractor == null ? null : extractor.GetProjectData(item);
        }

    }

}