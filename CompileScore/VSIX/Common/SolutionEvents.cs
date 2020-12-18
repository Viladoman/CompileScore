
using System;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;

namespace CompileScore
{
    internal static class CMakeConfigurationUtils
    {
        public class CMakeActiveConfiguration
        {
            public string CurrentProjectSetting { set; get; }
        }

        static public string GetActiveConfigurationFileName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = EditorUtils.GetSolutionPath();
            if (solutionPath == null || solutionPath.Length == 0) return null;
            return solutionPath + @".vs\ProjectSettings.json";
        }

        static public string GetActiveConfigurationName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string activeConfigFilename = GetActiveConfigurationFileName();
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
                    OutputLog.Error(e.Message);
                }

                if (activeConfig != null)
                {
                    return activeConfig.CurrentProjectSetting;
                }
            }

            //TODO ~ ramonv ~ if unable to find it here... load the CMAKe configurations and return the first one 

            return null;
        }
    }
     

    public delegate void NotifySolution(Solution solution);  // delegate

    internal class SolutionEventsListener : IVsSolutionEvents3, IVsSolutionEvents4, IVsUpdateSolutionEvents2, IVsUpdateSolutionEvents3, IDisposable
    {
        private static readonly Lazy<SolutionEventsListener> lazy = new Lazy<SolutionEventsListener>(() => new SolutionEventsListener());
        public static SolutionEventsListener Instance { get { return lazy.Value; } }

        private IVsSolution solution;
        private IVsSolutionBuildManager3 buildManager;
        private uint cookie1 = VSConstants.VSCOOKIE_NIL;
        private uint cookie2 = VSConstants.VSCOOKIE_NIL;
        private uint cookie3 = VSConstants.VSCOOKIE_NIL;

        private IServiceProvider Provider;

        private bool IsSolutionReady { set; get; } = false;
        private Common.FileWatcher FileWatcher { get; } = new Common.FileWatcher();

        private string configurationName;
        private string platformName;

        public string ConfigurationName 
        { 
            set 
            {
                if (configurationName != value)
                {
                    configurationName = value;
                    if (IsSolutionReady)
                    {
                        ActiveSolutionConfigurationChanged?.Invoke();
                    }
                }
            }

            get { return configurationName; } 
        }

        public string PlatformName
        {
            set
            {
                if (platformName != value)
                {
                    platformName = value;
                    if (IsSolutionReady)
                    {
                        ActiveSolutionConfigurationChanged?.Invoke();
                    }
                }
            }

            get { return platformName; }
        }

        public event NotifySolution SolutionReady;
        public event Notify         ActiveSolutionConfigurationChanged;

        private SolutionEventsListener() { }

        public void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Provider = serviceProvider;

            this.solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            Assumes.Present(this.solution);
            this.buildManager = serviceProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager3;
            Assumes.Present(this.buildManager);

            FileWatcher.FileWatchedChanged += OnCMakeActiveConfigurationChanged;
            FileWatcher.Verbosity = false;

            StartListeningForChanges();
        }

        private void StartListeningForChanges()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ErrorHandler.ThrowOnFailure(this.solution.AdviseSolutionEvents(this, out this.cookie1));
            if (this.buildManager != null)
            {
                if (this.buildManager is IVsSolutionBuildManager2 bm2)
                {
                    _ = ErrorHandler.ThrowOnFailure(bm2.AdviseUpdateSolutionEvents(this, out this.cookie2));
                }
                _ = ErrorHandler.ThrowOnFailure(this.buildManager.AdviseUpdateSolutionEvents3(this, out this.cookie3));
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            FileWatcher.Unwatch();

            // Ignore failures in UnadviseSolutionEvents
            if (this.cookie1 != VSConstants.VSCOOKIE_NIL)
            {
                _ = this.solution.UnadviseSolutionEvents(this.cookie1);
                this.cookie1 = VSConstants.VSCOOKIE_NIL;
            }
            if (this.cookie2 != VSConstants.VSCOOKIE_NIL)
            {
                _ = ((IVsSolutionBuildManager2)this.buildManager).UnadviseUpdateSolutionEvents(this.cookie2);
                this.cookie2 = VSConstants.VSCOOKIE_NIL;
            }
            if (this.cookie3 != VSConstants.VSCOOKIE_NIL)
            {
                _ = this.buildManager.UnadviseUpdateSolutionEvents3(this.cookie3);
                this.cookie3 = VSConstants.VSCOOKIE_NIL;
            }
        }

        public void CheckForOpenedSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsSolutionReady && Provider != null)
            {
                DTE2 applicationObject = Provider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(applicationObject);
                string solutionDirRaw = applicationObject.Solution.FullName;
                if (solutionDirRaw.Length > 0)
                {
                    //Add file watcher for configuration change notification on CMake projects
                    if (EditorUtils.GetEditorMode() == EditorUtils.EditorMode.CMake)
                    {
                        ConfigurationName = CMakeConfigurationUtils.GetActiveConfigurationName();
                        FileWatcher.Watch(CMakeConfigurationUtils.GetActiveConfigurationFileName());
                    }

                    IsSolutionReady = true;
                    SolutionReady?.Invoke(applicationObject.Solution);
                }
            }
        }

        private void OnCMakeActiveConfigurationChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ConfigurationName = CMakeConfigurationUtils.GetActiveConfigurationName();
        }

        int IVsUpdateSolutionEvents2.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) => VSConstants.E_NOTIMPL;

        int IVsUpdateSolutionEvents2.UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel) => VSConstants.E_NOTIMPL;

        int IVsUpdateSolutionEvents2.UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel) => VSConstants.E_NOTIMPL;

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) => VSConstants.E_NOTIMPL;

        public int UpdateSolution_Begin(ref int pfCancelUpdate) => VSConstants.E_NOTIMPL;
        
        public int UpdateSolution_Cancel() => VSConstants.E_NOTIMPL;

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) => VSConstants.E_NOTIMPL;

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) => VSConstants.E_NOTIMPL;

        int IVsUpdateSolutionEvents3.OnAfterActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string newConfigName; 
            if (pNewActiveSlnCfg != null && pNewActiveSlnCfg.get_DisplayName(out newConfigName) == VSConstants.S_OK)
            {
                var configSplit = newConfigName.Split('|');
                ConfigurationName = configSplit.Length > 0? configSplit[0] : null;
                PlatformName      = configSplit.Length > 1? configSplit[1] : null;
            }
            else
            {
                ConfigurationName = null;
                PlatformName      = null;
            }

            return VSConstants.S_OK;
        }

        int IVsUpdateSolutionEvents3.OnBeforeActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg) => VSConstants.E_NOTIMPL;

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            IsSolutionReady = false;
            return VSConstants.S_OK;
        }

        public int OnAfterClosingChildren(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.E_NOTIMPL;

        public int OnAfterMergeSolution(object pUnkReserved) => VSConstants.E_NOTIMPL;

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.E_NOTIMPL;

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            CheckForOpenedSolution();
            return VSConstants.S_OK;
        }

        public int OnAfterOpeningChildren(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.E_NOTIMPL;

        public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.E_NOTIMPL;

        public int OnBeforeClosingChildren(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnBeforeOpeningChildren(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.E_NOTIMPL;

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.E_NOTIMPL;

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.E_NOTIMPL;

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.E_NOTIMPL;

        public int OnAfterAsynchOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.E_NOTIMPL;

        public int OnAfterChangeProjectParent(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnAfterRenameProject(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnQueryChangeProjectParent(IVsHierarchy pHierarchy, IVsHierarchy pNewParentHier, ref int pfCancel) => VSConstants.E_NOTIMPL;
    }
}