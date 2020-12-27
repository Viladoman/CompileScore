using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Security.Principal;

namespace CompileScore
{
    public class Profiler
    {
        private static readonly Lazy<Profiler> lazy = new Lazy<Profiler>(() => new Profiler());

        public enum Compiler
        {
            MSVC, 
            Clang,
        }

        public enum ExtractorDetail
        {
            None     = 0, 
            Basic    = 1, 
            Frontend = 2,
            Full     = 3,
        }

        private enum StateType
        {
            Idle, 
            Triggering,
            Preparing,
            Building, 
            Canceling,
            Gathering,
            Extracting,
            
            BuildingExternal,
        }

        public static Profiler Instance { get { return lazy.Value; } }

        private MacroEvaluator Evaluator { set; get; } = new MacroEvaluator();

        private IServiceProvider ServiceProvider { set; get; }
        private BuildEvents BuildEvents { set; get; }

        private StateType State { set; get; } = StateType.Idle;
        private Compiler CompilerSource { set; get; } = Compiler.MSVC;
        private ExtractorDetail OverviewDetail { set; get; } = ExtractorDetail.Basic;
        private ExtractorDetail TimelineDetail { set; get; } = ExtractorDetail.Basic;
        private uint TimelinePacking { set; get; } = 100;

        static bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        public void Initialize(IServiceProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ServiceProvider = provider;

            //Hook to build events
            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);

            BuildEvents = applicationObject.Events.BuildEvents;
            BuildEvents.OnBuildBegin += OnBuildBegin;
            BuildEvents.OnBuildDone  += OnBuildDone;
        }

        public bool CleanSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                applicationObject.Solution.SolutionBuild.Clean(true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public void RebuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetGeneratorProperties();
            if (ValidateBuild())
            {
                CleanSolution();
                TriggerBuildSolution();
            }
        }

        public void BuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetGeneratorProperties();
            if (ValidateBuild())
            {
                TriggerBuildSolution();
            }
        }

        public void PLACEHOLDER_GenerateScore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetGeneratorProperties();
            if (ValidateGenerator())
            {
                _ = PLACEHOLDER_TriggerGeneratorAsync();
            }
        }

        public bool IsAvailable()
        {
            return State == StateType.Idle;
        }

        private void SetGeneratorProperties()
        {
            ScoreGeneratorSettings generatorSettings = SettingsManager.Instance.Settings.ScoreGenerator;
            CompilerSource = generatorSettings.Compiler;
            OverviewDetail = generatorSettings.OverviewDetail;
            TimelineDetail = generatorSettings.TimelineDetail;
            TimelinePacking = generatorSettings.TimelinePacking;
        }

        private bool CreateDirectory(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                DisplayError("Unable to create directory "+path+". " + e.ToString());
                return false;
            }

            return true;
        }

        private void TriggerBuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Focus();
            OutputLog.Clear();
            Evaluator.Clear();

            SetState(StateType.Triggering);

            try
            {
                //TODO ~ ramonv ~ find a way to call Build All in 'Open Folder' projects
                //DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;

                //applicationObject.ExecuteCommand("Build.BuildSolution");
                //applicationObject.ExecuteCommand("Build.RebuildSolution");

                //TODO ~ Ramonv ~ 'Open Folder' does not trigger build events! 

                DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                applicationObject.Solution.SolutionBuild.Build();

                /*
                // Rebuild - direct call alternative for .sln projects  
                IVsSolutionBuildManager2 buildManager = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
                if (ErrorHandler.Failed(buildManager.StartSimpleUpdateSolutionConfiguration((uint)(VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE | VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD),
                    (uint)VSSOLNBUILDQUERYRESULTS.VSSBQR_OUTOFDATE_QUERY_YES, 0)))
                {
                    //handle the error
                }
                */

            }
            catch(Exception e)
            {
                DisplayError("Unable to Trigger the build. " + e.Message);
                SetState(StateType.Idle);
            }
        }

        private async System.Threading.Tasks.Task PLACEHOLDER_TriggerGeneratorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputLog.Focus();
            OutputLog.Clear();
            Evaluator.Clear();

            SetState(StateType.Gathering);

            await PLACEHOLDER_GenerateScoreAsync();

            SetState(StateType.Idle);
        }

        private void DisplayError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Error(message);
            MessageWindow.Display(new MessageContent(message));
        }

        private bool ValidateBuild()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (CompilerSource == Compiler.MSVC && !IsElevated)
            {
                DisplayError("Visual Studio needs to be running in administrator mode. Microsoft Build Insights requirement.");
                return false;
            }          

            //TODO ~ ramonv ~ placeholder while we find a soltuion for 'open folder' build events 
            if (EditorContext.Instance.Mode == EditorContext.EditorMode.Folder)
            {
                DisplayError("Build and Profile is not supported on 'Open Folder' projects.\n" +
                    "I have been unable to get build events from the VS SDK on 'Open Folder' mode.\n" +
                    "\n" +
                    "CLANG WORKAROUND:\n" +
                    "When usingclang with -ftime-trace flag, run the 'Clang Full Score Generation' command once the build process has finished.\n" +
                    "Can be found under 'Extensions' -> 'Compile Score'");
                return false;
            }

            return ValidateGenerator();
        }

        private bool ValidateGenerator()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (GetScoreExtractorToolPath() == null)
            {
                DisplayError("Unable to find the score extractor program.");
                return false;
            }

            if (State != StateType.Idle)
            {
                DisplayError("Build Process already running!");
                return false;
            }

            string outputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.OutputPath);

            if (Path.GetExtension(outputPath) != ".scor")
            {
                DisplayError("'Output File' is not a .scor file.\nCurrent Value: "+ outputPath);
                return false;
            }

            if (CompilerSource == Compiler.Clang )
            {
                string inputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath);

                if (!File.Exists(inputPath))
                {
                    DisplayError("The 'Clang Traces Path' does not exist.\nCurrent value: " + inputPath);
                    return false;
                }

                FileAttributes attr = File.GetAttributes(inputPath);
                if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                {
                    DisplayError("The 'Clang Traces Path' is not a directory.\nCurrent value: " + inputPath);
                    return false;
                }
            }

             return true;
        }

        private void SetState(StateType newState)
        {
            State = newState;
        }
        private void OnBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            switch (State)
            {
                case StateType.Idle:      
                    SetState(StateType.BuildingExternal); 
                    break;
                case StateType.Triggering: 
                    PrepareGathering();
                    OutputLog.Log("Building...");
                    SetState(StateType.Building); 
                    break;
            }
        }

        private void OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (State == StateType.Building)
            {
                //async call for the Data Gathering
                if (Action == vsBuildAction.vsBuildActionClean)
                {
                    _ = CancelGatheringAsync();
                }
                else if (Action == vsBuildAction.vsBuildActionBuild || Action == vsBuildAction.vsBuildActionRebuildAll)
                {
                    _ = GatherAsync();
                }
            }
            else
            {
                SetState(StateType.Idle);
            }
        }

        public string GetExtensionInstallationDirectory()
        {
            try
            {
                var uri = new Uri(typeof(CompileScorePackage).Assembly.CodeBase, UriKind.Absolute);
                return Path.GetDirectoryName(uri.LocalPath);
            }
            catch
            {
                return null;
            }
        }

        private string GetToolPath(string localPath)
        {
            string installDirectory = GetExtensionInstallationDirectory();
            string ret = installDirectory == null? null : installDirectory + '\\' + localPath;
            return File.Exists(ret) ? ret : null;
        }

        private string GetScoreExtractorToolPath()
        {
            return GetToolPath(@"External\ScoreExtractor\ScoreDataExtractor.exe");
        }

        private string GetPlatformFlag()
        {
            return CompilerSource == Compiler.MSVC ? "-msvc" : "-clang";
        }

        private void PrepareGathering()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetState(StateType.Preparing);

            string extraArgs = "";
            if (CompilerSource == Compiler.MSVC)
            {
                extraArgs += " -d " + (int)OverviewDetail + (TimelinePacking == 0 ? "" : " -td " + (int)TimelineDetail);
            } 
            else
            {
                extraArgs += " -i " + FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));
            }

            string commandLine = GetPlatformFlag() + " -start" + extraArgs;

            OutputLog.Log("Calling ScoreDataExtractor with " + commandLine);
            int exitCode = ExternalProcess.ExecuteSync(GetScoreExtractorToolPath(), commandLine);

            if (exitCode != 0)
            {
                DisplayError("Score Data Extractor failed to start the recording session with code " + exitCode + ". The current build data won't be captured. Please check the output pane for more information.");
            }
        }

        private async System.Threading.Tasks.Task CancelGatheringAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            SetState(StateType.Canceling);

            string extraArgs = "";
            if (CompilerSource == Compiler.Clang)
            {
                extraArgs += " -i " + FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));
            }

            string commandLine = GetPlatformFlag() + " -cancel" + extraArgs;

            OutputLog.Log("Calling ScoreDataExtractor with " + commandLine);
            int exitCode = ExternalProcess.ExecuteSync(GetScoreExtractorToolPath(), commandLine);

            if (exitCode != 0)
            {
                DisplayError("Score Data Extractor failed to cancel the recording session with code " + exitCode + ".");
            }

            SetState(StateType.Idle);
        }

        private async System.Threading.Tasks.Task GatherAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputLog.Focus();

            SetState(StateType.Gathering);

            DocumentLifetimeManager.UnWatchFile();

            string inputPath = CompilerSource == Compiler.Clang ? FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath)) : "";
            string inputCommand = inputPath.Length > 0 ? " -i " + inputPath : "";

            string outputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.OutputPath);
            string outputCommand = outputPath.Length > 0 ? " -o " + outputPath : "";

            string detail = " -d " + (int)OverviewDetail;
            string timeline = TimelinePacking == 0 ? " -nt" : " -tp " + TimelinePacking + " -td " + (int)TimelineDetail;

            string commandLine = GetPlatformFlag() + " -stop" + timeline + detail + inputCommand + outputCommand;

            OutputLog.Log("Calling ScoreDataExtractor with " + commandLine);
            int exitCode = ExternalProcess.ExecuteSync(GetScoreExtractorToolPath(), commandLine);

            if (exitCode != 0)
            {
                DisplayError("Score Data Extractor process failed with code " + exitCode + ". Please check the output pane for more information.");
            }

            CompilerData.Instance.ForceLoadFromFilename(outputPath);
            EditorUtils.FocusOverviewWindow();

            OutputLog.Log("Score generation completed!");
            SetState(StateType.Idle);
        }

        private async System.Threading.Tasks.Task PLACEHOLDER_GenerateScoreAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (CompilerSource != Compiler.Clang)
            {
                return;
            } 

            //Stop watching as the data extractor might modify the watched file
            DocumentLifetimeManager.UnWatchFile();

            //Process Data
            string inputPath = FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));
            string outputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.OutputPath);
            CreateDirectory(Path.GetDirectoryName(outputPath));

            string detail = " -d " + (int)OverviewDetail;
            string timeline = TimelinePacking == 0 ? " -nt" : " -tp " + TimelinePacking + " -td " + (int)TimelineDetail;

            string commandLine = "-clang -extract" + timeline + detail + " -i " + inputPath + " -o " + outputPath;

            OutputLog.Log("Calling ScoreDataExtractor with " + commandLine);
            int exitCode = await ExternalProcess.ExecuteAsync(GetScoreExtractorToolPath(), commandLine);

            if (exitCode != 0)
            {
                DisplayError("Compile Score Data Extractor process failed with code " + exitCode + ". Please check the output pane for more information.");
            }

            CompilerData.Instance.ForceLoadFromFilename(outputPath);
            EditorUtils.FocusOverviewWindow();

            OutputLog.Log("Score generation completed!");
        }

        private string FixPath(string path)
        {
            return path == null? "" : (path.Length > 0 && path[path.Length - 1] != '\\' && path[path.Length - 1] != '/' ? path + '\\' : path);
        }
    }
}
