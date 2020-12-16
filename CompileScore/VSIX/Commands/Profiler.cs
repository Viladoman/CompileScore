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

//TODO ~ ramonv ~ add Rebuild and Profile + ScoreExtractor only + Clean Profile Data commands

namespace CompileScore
{
    public class Profiler
    {
        private static readonly Lazy<Profiler> lazy = new Lazy<Profiler>(() => new Profiler());

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

        public void BuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (State != StateType.Idle) return;
            OutputLog.Focus();
            OutputLog.Clear();
            Evaluator.Clear();

            if (!Validate())
            {
                return;
            }

            SetState(StateType.Triggering);

            try
            {
                DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                applicationObject.Solution.SolutionBuild.Build();
            }
            catch(Exception)
            {
                SetState(StateType.Idle);
            }
        }

        private bool Validate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsElevated)
            {
                OutputLog.Error("Visual Studio needs to be running in administrator mode. Command cancelled!");
                //TODO ~ ramonv ~ display message
                return false;
            }

            if (GetScoreExtractorToolPath() == null)
            {
                OutputLog.Error("Unable to find the score extractor program. Command cancelled!");
                //TODO ~ ramonv ~ display message
                return false;
            }

            if (GetVCPerfToolPath() == null)
            {
                OutputLog.Error("Unable to find the vcperf program. Command cancelled!");
                //TODO ~ ramonv ~ display message
                return false;
            }

            //TODO ~ ramonv ~ maybe here stop vcperf session just in case it is in a desync state

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
            //TODO ~ ramonv ~ call Async here

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

            string commandLine = "/start CompileScore";

            //TODO ~ Add \level flags if we need templates

            OutputLog.Log("Executing VCPERF " + commandLine);
            var exitCode = TriggerProcess(GetVCPerfToolPath(), commandLine);

            //TODO ~ ramonv ~ process exit code 
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

            string platform = true? "-msvc" : "-clang"; //TODO ~ ramonv ~ based on platform 

            string commandLine = platform + " -i " + inputPath + ETLFileName + " -o " + outputPath;

            OutputLog.Log("Executing Compile Score Extractor " + commandLine);
            var exitCode = TriggerProcess(GetScoreExtractorToolPath(), commandLine);
            //TODO ~ ramonv ~ process exitcode
            
            // Potentially allow configuration and generator level macros

            CompilerData.Instance.ForceLoadFromFilename(outputPath);
            EditorUtils.FocusOverviewWindow();

            SetState(StateType.Idle);
        }

        private void StopGathering()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string path = FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath));
            string commandLine = "/stopnoanalyze CompileScore " + path + ETLFileName;

            OutputLog.Log("Executing VCPERF " + commandLine);
            var exitCode = TriggerProcess(GetVCPerfToolPath(), commandLine);
         
            //TODO ~ ramonv ~ process exit code 
        }

        private string FixPath(string path)
        {
            return path == null? "" : (path.Length > 0 && path[path.Length - 1] != '\\' && path[path.Length - 1] != '/' ? path + '\\' : path);
        }
    }
}
