// File that will handle the triggering of the parser

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSLangProj;
using EnvDTE;
using Newtonsoft.Json;

namespace CompileScore
{
    public class ParseResult
    {
        public enum StatusCode
        {
            //TODO ~ ramonv ~ rethink these messages
            Unknown,
            ToolMissing,
            InvalidLocation,
            InvalidProject,
            InvalidOutputDir,
            VersionMismatch,
            InvalidInput,
            ParseFailed,
            NotFound,
            Found
        }

        //TODO ~ ramonv ~ add unbinarized data here

        public StatusCode Status { set; get; } = StatusCode.Unknown;
        public string Message { set; get; } = "";
    }

    public class Parser
    {
        public async Task<ParseResult> ParseClangAsync(ProjectProperties projProperties, string inputFilename, string outputDirectory)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (inputFilename == null || inputFilename.Length == 0)
            {
                OutputLog.Error("No file provided for parsing");
                return new ParseResult { Status = ParseResult.StatusCode.InvalidInput };
            }

            string toolPath = GetParserToolPath();
            if (toolPath == null)
            {
                OutputLog.Error("Unable to find the parser tool");
                return new ParseResult { Status = ParseResult.StatusCode.ToolMissing };
            }

            string errorStr = CreateDirectory(outputDirectory);
            if (errorStr != null)
            {
                return new ParseResult { Status = ParseResult.StatusCode.InvalidOutputDir, Message = errorStr };
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
                return new ParseResult { Status = ParseResult.StatusCode.InvalidOutputDir, Message = errorStr };
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

            OutputLog.Focus();
            OutputLog.Log("Searching Code Requirements for " + inputFilename + "...");

            if (ParserProcessor.GetParserSettings().OptionParserShowCommandLine)
            {
                OutputLog.Log($"TOOL ARGUMENTS: {toolCmd}");
                OutputLog.Log($"CLANG ARGUMENTS: {clangCmd}");
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            ExternalProcess externalProcess = new ExternalProcess();
            int exitCode = await externalProcess.ExecuteAsync(toolPath, toolCmd);
            watch.Stop();

            if (exitCode != 0)
            {
                OutputLog.Error("The Compile Score parser failed to parse this document. (" + GetTimeStr(watch) + ")");
                return new ParseResult { Status = ParseResult.StatusCode.ParseFailed };
            }

            OutputLog.Log("Parsing completed! (" + GetTimeStr(watch) + ")");

            //TODO ~ ramonv ~ process outputPath file 

            return new ParseResult { Status = ParseResult.StatusCode.Found };
        }
        private string GetStandardFlag(ProjectProperties.StandardVersion standard)
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

        private string GetParserToolPath()
        {
            return EditorUtils.GetToolPath(@"External\Parser\CompileScoreParser.exe");
        }

        private string CreateDirectory(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                string errorStr = "Unable to create directory '" + path + "'.\n" + e.ToString();
                OutputLog.Error(errorStr);
                return errorStr;
            }

            return null;
        }

        private string AdjustPath(string input)
        {
            return input.Contains(' ') ? '"' + input + '"' : input;
        }

        private void AdjustPaths(List<string> list)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                list[i] = AdjustPath(list[i]);
            }
        }

        private string GenerateCommandStr(string prefix, List<string> args)
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
        static public string GetTimeStr(ulong uSeconds)
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

        static public string GetTimeStr(System.Diagnostics.Stopwatch watch)
        {
            const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
            return GetTimeStr((ulong)(watch.ElapsedTicks / TicksPerMicrosecond));
        }

    }


    public class ParserProcessor
    {
        private static readonly Lazy<ParserProcessor> lazy = new Lazy<ParserProcessor>(() => new ParserProcessor());
        public static ParserProcessor Instance { get { return lazy.Value; } }

        Parser parser = new Parser();


        static public ParserSettingsPageGrid GetParserSettings() { return (ParserSettingsPageGrid)EditorUtils.Package.GetDialogPage(typeof(ParserSettingsPageGrid)); }

        public async System.Threading.Tasks.Task ParseAtCurrentLocationAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputLog.Clear(); // TODO ~ Ramonv ~ we want our own pane for the parser 

            ParseResult result;
            Document activeDocument = EditorUtils.GetActiveDocument(); 
            if ( activeDocument == null )
            {
                //OutputLog.Error(GetErrorMessage(ParseResult.StatusCode.InvalidLocation));
                result = new ParseResult { Status = ParseResult.StatusCode.InvalidLocation };
                return;
            }

            EditorUtils.SaveActiveDocument();

            ProjectProperties properties = GetProjectData();
            if (properties == null)
            {
                //OutputLog.Error(GetErrorMessage(ParseResult.StatusCode.InvalidProject));
                result = new ParseResult { Status = ParseResult.StatusCode.InvalidProject };
            }
            else
            {
                result = await parser.ParseClangAsync(properties, activeDocument.FullName, GetParserOutputDirectory());
            }

            //TODO ~ digest result and pipe it to the new window - Process result

        }

        private string GetParserOutputDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings.ParserSettings;
            string dir = customSettings == null ? null : customSettings.ParserOutputFolder;

            if (dir == null || dir.Length == 0)
            {
                //default to the extension installation directory
                dir = @"$(ExtensionInstallationDir)Generated";
            }

            dir = GetProjectExtractor().EvaluateMacros(dir);

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
        private IExtractor GetProjectExtractor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings.ParserSettings;
            if (customSettings == null || customSettings.AutomaticExtraction)
            {
                switch (EditorUtils.GetEditorMode())
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

        private ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var extractor = GetProjectExtractor();
            return extractor == null ? null : extractor.GetProjectData();
        }

    }

}