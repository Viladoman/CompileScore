using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.VisualStudio;

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

        const string ETLFileName = "buildTraceFile.etl";        

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
            if (Validate())
            {
                CleanSolution();
                TriggerBuildSolution();
            }
        }

        public void BuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetGeneratorProperties();
            if (Validate())
            {
                TriggerBuildSolution();
            }
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

            //TODO ~ ramonv ~ ask if possible to add a way to query MS build insights for a session in progress. If in progress STOP it here.

            SetState(StateType.Triggering);

            try
            {
                //TODO ~ ramonv ~ find a way to call Build All in CMake projects
                //DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;

                //applicationObject.ExecuteCommand("Build.BuildSolution");
                //applicationObject.ExecuteCommand("Build.RebuildSolution");

                //TODO ~ Ramonv ~ CMAKE does not trigger build events! 

                DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                applicationObject.Solution.SolutionBuild.Build();
            }
            catch(Exception e)
            {
                DisplayError("Unable to Trigger the build. " + e.Message);
                SetState(StateType.Idle);
            }
        }

        private void DisplayError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Error(message);
            MessageWindow.Display(new MessageContent(message));
        }

        private bool Validate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (CompilerSource == Compiler.MSVC && !IsElevated)
            {
                DisplayError("Visual Studio needs to be running in administrator mode. Microsoft Build Insights requirement.");
                return false;
            }

            if (GetScoreExtractorToolPath() == null)
            {
                DisplayError("Unable to find the score extractor program.");
                return false;
            }

            if (GetVCPerfToolPath() == null)
            {
                DisplayError("Unable to find the vcperf program.");
                return false;
            }

            if (State != StateType.Idle)
            {
                DisplayError("Build Process already running!");
                return false;
            }

            return true;
        }

        private void SetState(StateType newState)
        {
            if (State != newState)
            {
                State = newState;

                //TODO ~ Notify menu items commands to enable/disable
            }
        }
        private void OnBuildBegin(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            switch (State)
            {
                case StateType.Idle:      
                    SetState(StateType.BuildingExternal); 
                    break;
                case StateType.Triggering: 
                    PrepareGathering(); 
                    SetState(StateType.Building); 
                    break;
            }
        }

        private void OnBuildDone(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action)
        {
            //TODO ~ ramonv ~ call Async here

            ThreadHelper.ThrowIfNotOnUIThread();

            if (State == StateType.Building)
            {
                if (Action == EnvDTE.vsBuildAction.vsBuildActionClean)
                {
                    CancelGathering();
                }
                else if (Action == EnvDTE.vsBuildAction.vsBuildActionBuild || Action == EnvDTE.vsBuildAction.vsBuildActionRebuildAll)
                {
                    Gather();
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

        private string GetVCPerfToolPath()
        {
            return GetToolPath(@"External\VCPerf\vcperf.exe");
        }

        private int TriggerProcess(string toolPath, string arguments)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            System.Diagnostics.Process process = new System.Diagnostics.Process();

            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            
            // Start process and handlers
            process.Start();

            //Handle output
            while (!process.StandardOutput.EndOfStream || !process.StandardError.EndOfStream)
            {
                if (!process.StandardOutput.EndOfStream)
                {
                    OutputLog.LogLine(process.StandardOutput.ReadLine());
                }

                if (!process.StandardError.EndOfStream)
                {
                    OutputLog.LogLine(process.StandardError.ReadLine());
                }
            }

            process.WaitForExit();

            return process.ExitCode;
        }

        private void PrepareGathering()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetState(StateType.Preparing);

            if (CompilerSource == Compiler.MSVC)
            {
                string level = OverviewDetail >= ExtractorDetail.Frontend || (TimelinePacking > 0 && TimelineDetail >= ExtractorDetail.Frontend) ? " /level3" : "";
                
                string commandLine = "/start" + level + " CompileScore";

                OutputLog.Log("Executing VCPERF " + commandLine);
                var exitCode = TriggerProcess(GetVCPerfToolPath(), commandLine);

                if (exitCode != 0)
                {
                    DisplayError("VCPerf process failed to start the gathering session with code " + exitCode + ". The current build data won't be captured. Please check the output pane for more information.");
                }
            }

            OutputLog.Log("Building...");
        }

        private void CancelGathering()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetState(StateType.Canceling);

            StopGathering();

            SetState(StateType.Idle);
        }

        private void Gather()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Focus();

            SetState(StateType.Gathering);

            StopGathering();

            //Stop watching as the data extractor might modify the watched file
            DocumentLifetimeManager.UnWatchFile();

            //Process Data
            string inputPath = FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));
            string outputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.OutputPath);

            CreateDirectory(Path.GetDirectoryName(outputPath));

            string finalInputPath = CompilerSource == Compiler.MSVC ? inputPath + ETLFileName : inputPath;
            string platform       = CompilerSource == Compiler.MSVC? "-msvc" : "-clang";
            string detail         = " -d " + (int)OverviewDetail;
            string timeline       = TimelinePacking == 0? " -nt" : " -tp " + TimelinePacking + " -td " + (int)TimelineDetail;

            string commandLine = platform + timeline + detail + " -i " + finalInputPath + " -o " + outputPath;

            OutputLog.Log("Executing Compile Score Extractor " + commandLine);
            var exitCode = TriggerProcess(GetScoreExtractorToolPath(), commandLine);
            
            if (exitCode != 0)
            {
                DisplayError("Compile Score Data Extractor process failed with code "+ exitCode +". Please check the output pane for more information.");
            }
            
            CompilerData.Instance.ForceLoadFromFilename(outputPath);
            EditorUtils.FocusOverviewWindow();

            SetState(StateType.Idle);
        }

        private void StopGathering()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (CompilerSource == Compiler.MSVC)
            {
                string path = FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));

                CreateDirectory(path);

                string commandLine = "/stopnoanalyze CompileScore " + path + ETLFileName;

                OutputLog.Log("Executing VCPERF " + commandLine);
                var exitCode = TriggerProcess(GetVCPerfToolPath(), commandLine);

                if (exitCode != 0)
                {
                    DisplayError("VCPerf process failed to stop the data gathering with code " + exitCode + ". Please check the output pane for more information.");
                }
            }
        }

        private string FixPath(string path)
        {
            return path == null? "" : (path.Length > 0 && path[path.Length - 1] != '\\' && path[path.Length - 1] != '/' ? path + '\\' : path);
        }
    }
}
