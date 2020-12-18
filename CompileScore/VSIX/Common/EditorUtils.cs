using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;

namespace CompileScore
{
    static public class EditorUtils
    {
        public enum EditorMode
        {
            None, 
            VisualStudio,
            CMake,
        }

        static private AsyncPackage Package { get; set; }
        static public IServiceProvider ServiceProvider { get; set; }

        static public void Initialize(AsyncPackage package)
        {
            Package = package;
            ServiceProvider = package;
        }

        static public GeneralSettingsPageGrid GetGeneralSettings()
        {
            return (GeneralSettingsPageGrid)Package.GetDialogPage(typeof(GeneralSettingsPageGrid));
        }

        static public Document GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.ActiveDocument;
        }

        static public Project GetActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            if (applicationObject.ActiveDocument == null || applicationObject.ActiveDocument.ProjectItem == null) return null;
            return applicationObject.ActiveDocument.ProjectItem.ContainingProject;
        }

        static public Solution GetActiveSolution()
        {
            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.Solution;
        }

        static public string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Solution solution = GetActiveSolution();
            if (solution == null) return null;
            return (Path.HasExtension(solution.FullName) ? Path.GetDirectoryName(solution.FullName) : solution.FullName) + '\\';
        }

        static public EditorMode GetEditorMode()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = EditorUtils.GetActiveProject();
            if (project == null)
            {
                return EditorMode.None;
            } 

            if (project.Object == null)
            {
                return EditorMode.CMake;
            }

            return EditorMode.VisualStudio;
        }

        static public Overview.OverviewWindow FocusOverviewWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            Overview.OverviewWindow window = Package.FindToolWindow(typeof(Overview.OverviewWindow), 0, true) as Overview.OverviewWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }
    }
}
