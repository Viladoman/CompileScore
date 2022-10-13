using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
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
            None = 0,
            Basic = 1,
            Frontend = 2,
            Full = 3,
        }

        public enum BuildOperation
        {
            Build,
            Rebuild,
            GenerateClang
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

        private string SpecificBuildProjectName { set; get; } 
        private IVsHierarchy SpecificBuildProject { set; get; }
        private BuildOperation Operation { set; get; } = BuildOperation.Build;
        private StateType State { set; get; } = StateType.Idle;
        private Compiler CompilerSource { set; get; } = Compiler.MSVC;
        private ExtractorDetail OverviewDetail { set; get; } = ExtractorDetail.Basic;
        private ExtractorDetail TimelineDetail { set; get; } = ExtractorDetail.Basic;
        private uint TimelinePacking { set; get; } = 100;
        private bool ExtracIncluders { set; get; } = true;

        public void Initialize(IServiceProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ServiceProvider = provider;

            //Hook to build events
            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);

            BuildEvents = applicationObject.Events.BuildEvents;
            BuildEvents.OnBuildBegin += OnBuildBegin;
            BuildEvents.OnBuildDone += OnBuildDone;
        }

        public void TriggerOperation(BuildOperation operation, string specificProject = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SpecificBuildProjectName = specificProject;
            SpecificBuildProject = GetProjectNodeFromName(SpecificBuildProjectName);
            Operation = operation;
            SetGeneratorProperties();

            switch (operation)
            {
                case BuildOperation.Build: TriggerBuildSolution(); break;
                case BuildOperation.Rebuild: TriggerBuildSolution(); break;
                case BuildOperation.GenerateClang: _ = TriggerClangGeneratorAsync(); break;
            }
        }

        public void TriggerStartTrace()
        {
			ThreadHelper.ThrowIfNotOnUIThread();

			//OutputLog.Focus();
			OutputLog.Clear();
			Evaluator.Clear();

			PrepareGathering();
		}

		public void TriggerStopTrace()
		{
			_ = GatherAsync(false);
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
            ExtracIncluders = generatorSettings.ExtractIncluders;
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
                DisplayError("Unable to create directory " + path + ". " + e.ToString());
                return false;
            }

            return true;
        }

        private IEnumerable<Project> GetSolutionFolderProjects(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            List<Project> projects = new List<Project>();
            var y = (project.ProjectItems as ProjectItems).Count;
            for (var i = 1; i <= y; i++)
            {
                var x = project.ProjectItems.Item(i).SubProject;
                var subProject = x as Project;
                if (subProject != null)
                {
                    projects.Add(subProject);
                }
            }

            return projects;
        }

        private string GetProjectUniqueName(Projects projects, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (Project project in projects)
            {
                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    var innerProjects = GetSolutionFolderProjects(project);
                    foreach (var innerProject in innerProjects)
                    {
                        if (innerProject.Name == name)
                        {
                            return innerProject.UniqueName;
                        }
                    }
                }
                else if (project.Name == name)
                {
                    return project.UniqueName;
                }
            }

            return null;
        }

        private IVsHierarchy GetProjectNodeFromName(string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            IVsSolution solutionService = (IVsSolution)ServiceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            if (name == null || applicationObject == null || solutionService == null) return null;

            string uniqueProjName = GetProjectUniqueName(applicationObject.Solution.Projects,name);

            IVsHierarchy projectHierarchy = null;
            solutionService.GetProjectOfUniqueName(uniqueProjName, out projectHierarchy);
            return projectHierarchy;
        }


        private void TriggerBuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!ValidateBuild()) return;

            OutputLog.Focus();
            OutputLog.Clear();
            Evaluator.Clear();

            SetState(StateType.Triggering);

            try
            {
                //TODO ~ ramonv ~ find a way to call Build All in 'Open Folder' projects
                //TODO ~ ramonv ~ 'Open Folder' does not trigger build events! 

                DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);

                if (SpecificBuildProject == null) 
                {
                    // entire solution
                    applicationObject.ExecuteCommand(Operation == BuildOperation.Rebuild? "Build.RebuildSolution" : "Build.BuildSolution");
                }
                else
                {
                    IVsSolutionBuildManager2 buildManager = ServiceProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
                    Assumes.Present(buildManager);
                    uint flags = Operation == BuildOperation.Rebuild ? (uint)(VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE | VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD) : (uint)(VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD);
                    buildManager.StartSimpleUpdateProjectConfiguration(SpecificBuildProject, null, null, flags, 0, 0);
                }
            }
            catch (Exception e)
            {
                DisplayError("Unable to Trigger the build. " + e.Message);
                SetState(StateType.Idle);
            }
        }

        private async System.Threading.Tasks.Task TriggerClangGeneratorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!ValidateGenerator()) return;

            OutputLog.Focus();
            OutputLog.Clear();
            Evaluator.Clear();

            SetState(StateType.Gathering);

            await GenerateClangScoreAsync();

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

            if (SpecificBuildProject == null && SpecificBuildProjectName != null)
            {
                DisplayError("Unable to find the project node to build for " + SpecificBuildProjectName);
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

                try
                {
                    FileAttributes attr = File.GetAttributes(inputPath);
                    if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                    {
                        DisplayError("The 'Clang Traces Path' is not a directory.\nCurrent value: " + inputPath);
                        return false;
                    }
                }
                catch(Exception e)
                {
                    DisplayError("The 'Clang Traces Path' is invalid.\n"+e.Message);
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
                    _ = GatherAsync(true);
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

        private async System.Threading.Tasks.Task GatherAsync(bool focus)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (focus)
                OutputLog.Focus();

            SetState(StateType.Gathering);

            DocumentLifetimeManager.UnWatchFile();

            string inputPath = CompilerSource == Compiler.Clang ? FixPath(Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.InputPath)) : "";
            string inputCommand = inputPath.Length > 0 ? " -i " + inputPath : "";

            string outputPath = Evaluator.Evaluate(SettingsManager.Instance.Settings.ScoreGenerator.OutputPath);
            string quotes = outputPath.IndexOf(' ') >= 0? "\"" : "";
            string outputCommand = outputPath.Length > 0 ? " -o " + quotes + outputPath + quotes : "";
            
            string detail = " -d " + (int)OverviewDetail;
            string timeline = TimelinePacking == 0 ? " -nt" : " -tp " + TimelinePacking + " -td " + (int)TimelineDetail;
            string includers = ExtracIncluders ? "" : " -ni ";

            string commandLine = GetPlatformFlag() + " -stop" + includers + timeline + detail + inputCommand + outputCommand;

            CreateDirectory(Path.GetDirectoryName(outputPath));

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

        private async System.Threading.Tasks.Task GenerateClangScoreAsync()
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
            string quotes = outputPath.IndexOf(' ') >= 0 ? "\"" : "";
            CreateDirectory(Path.GetDirectoryName(outputPath));

            string detail = " -d " + (int)OverviewDetail;
            string timeline = TimelinePacking == 0 ? " -nt" : " -tp " + TimelinePacking + " -td " + (int)TimelineDetail;
            string includers = ExtracIncluders ? "" : " -ni ";

            string commandLine = "-clang -extract" + includers + timeline + detail + " -i " + inputPath + " -o " + quotes + outputPath + quotes;

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
