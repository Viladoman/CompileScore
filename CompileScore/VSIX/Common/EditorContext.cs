using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;

namespace CompileScore
{
    internal static class FolderConfigurationUtils
    {
        private class FolderActiveConfiguration
        {
            public string CurrentProjectSetting { set; get; }
        }

        private class FolderConfiguration
        {
            public string name { set; get; }
        }

        private class FolderSettings
        {
            public List<FolderConfiguration> configurations { set; get; }
        }

        static public string GetActiveConfigurationFileName(string rootPath)
        {
             return rootPath + @".vs\ProjectSettings.json";
        }

        static public string GetActiveConfigurationName(string rootPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string activeConfigFilename = GetActiveConfigurationFileName(rootPath);
            if (File.Exists(activeConfigFilename))
            {
                var activeConfig = new FolderActiveConfiguration();

                try
                {
                    string jsonString = File.ReadAllText(activeConfigFilename);
                    activeConfig = JsonConvert.DeserializeObject<FolderActiveConfiguration>(jsonString);
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

            //try to return the first config if the active file is not present
            FolderSettings configs = GetConfigurations(rootPath);
            return configs != null && configs.configurations.Count > 0? configs.configurations[0].name : null;
        }

        static private FolderSettings GetConfigurations(string rootPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            FolderSettings settings = null;

            string settingsFilename = rootPath + "CMakeSettings.json";
            if (File.Exists(settingsFilename))
            {
                try
                {
                    string jsonString = File.ReadAllText(settingsFilename);
                    settings = JsonConvert.DeserializeObject<FolderSettings>(jsonString);
                }
                catch (Exception e)
                {
                    OutputLog.Error(e.Message);
                }
            }

            return settings;
        }
    }

    [ComVisible(true)]
    public class EditorContext : IVsSolutionEvents3, IVsSolutionEvents7, IVsUpdateSolutionEvents2, IVsUpdateSolutionEvents3, IDisposable
    {
        private static readonly Lazy<EditorContext> lazy = new Lazy<EditorContext>(() => new EditorContext());
        public static EditorContext Instance { get { return lazy.Value; } }

        public enum ExecutionEnvironment
        {
            Standalone, 
            VisualStudio
        }

        public enum EditorMode
        {
            None,
            VisualStudio,
            Folder,
        }

        public const ExecutionEnvironment Environment = ExecutionEnvironment.VisualStudio;

        public string RootPath { private set; get; }

        private IVsSolution solution;
        private IVsSolutionBuildManager3 buildManager;
        private uint cookie1 = VSConstants.VSCOOKIE_NIL;
        private uint cookie2 = VSConstants.VSCOOKIE_NIL;
        private uint cookie3 = VSConstants.VSCOOKIE_NIL;

        private IServiceProvider ServiceProvider;

        public EditorMode Mode { private set; get; }

        private Common.FileWatcher FileWatcher { get; } = new Common.FileWatcher(); //TODO ~ ramonv ~ try using the new api for this 

        public string ConfigurationName { private set; get; }

        public string PlatformName { private set; get; }

        public event Notify ModeChanged;
        public event Notify ConfigurationChanged;

        private EditorContext() { }

        public void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ServiceProvider = serviceProvider;

            this.solution = ServiceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            Assumes.Present(this.solution);
            this.buildManager = ServiceProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager3;
            Assumes.Present(this.buildManager);

            FileWatcher.FileWatchedChanged += OnFolderActiveConfigurationChanged;
            FileWatcher.Verbosity = false;

            //Start Listening for events
            this.solution.AdviseSolutionEvents(this, out this.cookie1);
            if (this.buildManager != null)
            {
                if (this.buildManager is IVsSolutionBuildManager2 bm2)
                {
                    bm2.AdviseUpdateSolutionEvents(this, out this.cookie2);
                }
                this.buildManager.AdviseUpdateSolutionEvents3(this, out this.cookie3);
            }

            CheckAlreadyOpenedContext(); 
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

        public string GetWorkspaceName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            switch (Mode)
            {
                case EditorMode.VisualStudio:
                    {
                        DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
                        Assumes.Present(applicationObject);
                        return Path.GetFileNameWithoutExtension(applicationObject.Solution.FullName);
                    }
                case EditorMode.Folder:
                    return Path.GetFileName(Path.GetDirectoryName(RootPath));
            }
            return null;
        } 

        private void SetMode(EditorMode input)
        {
            if (Mode != input)
            {
                Mode = input;
                ModeChanged?.Invoke();
            }
        }

        private void SetConfiguration(string input)
        {
            if (ConfigurationName != input)
            {
                ConfigurationName = input;
                if (Mode != EditorMode.None)
                {
                    ConfigurationChanged?.Invoke();
                }
            }
        }

        private void SetPlatform(string input)
        {
            if (PlatformName != input)
            {
                PlatformName = input;
                if (Mode != EditorMode.None)
                {
                    ConfigurationChanged?.Invoke();
                }
            }
        }
        private void CheckAlreadyOpenedContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            solution.GetSolutionInfo(out string dir, out string file, out string ops);
            RootPath = dir;
            
            solution.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object open);
            if ((bool)open)
            {

                solution.GetProperty((int)__VSPROPID7.VSPROPID_IsInOpenFolderMode, out object folderMode);
                if ((bool)folderMode)
                {
                    GatherFolderConfiguration();
                    SetMode(EditorMode.Folder);
                }
                else
                {
                    GatherSolutionConfiguration();
                    SetMode(EditorMode.VisualStudio);
                }
            }
            else
            {
                SetMode(EditorMode.None);
            }
        }

        private void GatherFolderConfiguration()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Add file watcher for configuration change notification on CMake projects  
            SetConfiguration(FolderConfigurationUtils.GetActiveConfigurationName(RootPath));
            FileWatcher.Watch(FolderConfigurationUtils.GetActiveConfigurationFileName(RootPath));
        }

        private void GatherSolutionConfiguration()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2 dte = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            if (dte != null && dte.Solution != null && dte.Solution.Projects.Count > 0 && dte.Solution.Projects.Item(1).ConfigurationManager != null)
            {
                ConfigurationManager configmgr = dte.Solution.Projects.Item(1).ConfigurationManager;
                Configuration config = configmgr.ActiveConfiguration;

                SetConfiguration(config.ConfigurationName);
                SetPlatform(config.PlatformName);
            }
        }

        private void OnFolderActiveConfigurationChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetConfiguration(FolderConfigurationUtils.GetActiveConfigurationName(RootPath));
        }

        //EVENTS

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
                SetConfiguration(configSplit.Length > 0? configSplit[0] : null);
                SetPlatform(configSplit.Length > 1? configSplit[1] : null);
            }
            else
            {
                SetConfiguration(null);
                SetPlatform(null);
            }

            return VSConstants.S_OK;
        }

        int IVsUpdateSolutionEvents3.OnBeforeActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg) => VSConstants.E_NOTIMPL;

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            SetMode(EditorMode.None);
            return VSConstants.S_OK;
        }

        public int OnAfterClosingChildren(IVsHierarchy pHierarchy) => VSConstants.E_NOTIMPL;

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.E_NOTIMPL;

        public int OnAfterMergeSolution(object pUnkReserved) => VSConstants.E_NOTIMPL;

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.E_NOTIMPL;

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            RootPath = Path.GetDirectoryName(applicationObject.Solution.FullName)+'\\';

            GatherSolutionConfiguration();

            SetMode(EditorMode.VisualStudio);

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

        public void OnAfterOpenFolder(string folderPath) 
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            RootPath = folderPath + '\\';
            GatherFolderConfiguration();
            SetMode(EditorMode.Folder);
        }
        public void OnBeforeCloseFolder(string folderPath) 
        {
            SetMode(EditorMode.None);
        }

        public void OnQueryCloseFolder(string folderPath, ref int pfCancel) { }
        public void OnAfterCloseFolder(string folderPath) { }
        public void OnAfterLoadAllDeferredProjects() { }
    }
}