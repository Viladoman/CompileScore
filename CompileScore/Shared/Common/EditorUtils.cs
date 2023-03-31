using CompileScore.Overview;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore
{
    static public class EditorUtils
    {
        public const string IncludeRegex = @"#\s*include\s*[<""]([^>""]+)[>""]";

        static private AsyncPackage Package { get; set; }
        static private IServiceProvider ServiceProvider { get; set; }

        static public void Initialize(AsyncPackage package, IServiceProvider serviceProvider)
        {
            Package = package;
            ServiceProvider = serviceProvider;
        }

        static public string NormalizePath(string input)
        {
            return Path.IsPathRooted(input) ? Path.GetFullPath(input) : input;
        }

        static public string GetFileNameSafe(string input)
        {
            try
            {
                return Path.GetFileName(input);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static public OverviewWindow FocusOverviewWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            OverviewWindow window = Package.FindToolWindow(typeof(OverviewWindow), 0, true) as OverviewWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }
        
        static private IEnumerable<ProjectItem> EnumerateProjectItems(ProjectItems items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (items != null)
            {
                foreach (ProjectItem itm in items)
                {
                    yield return itm;

                    foreach (var res in EnumerateProjectItems(itm.ProjectItems))
                    {
                        yield return res;
                    }

                    Project subProject = itm.SubProject;
                    if (subProject != null)
                    {
                        foreach (var res in EnumerateProjectItems(subProject.ProjectItems))
                        {
                            yield return res;
                        }
                    }
                }
            }
        }

        static private ProjectItem FindFilenameInProject(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //TODO ~ ramonv ~ improve this for Open Folder and External Tool
            //TODO ~ ramonv ~ this won't work on Open Folder projects
            if (EditorContext.Instance.Mode != EditorContext.EditorMode.VisualStudio) return null;

            DTE2 dte = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(dte);
            Projects projects = dte.Solution.Projects;

            var projectEnumerator = projects.GetEnumerator();
            while (projectEnumerator.MoveNext())
            {
                var project = projectEnumerator.Current as Project;
                if (project == null)
                {
                    continue;
                }

                foreach (var item in EnumerateProjectItems(project.ProjectItems))
                {
                    if (String.Equals(item.Name, filename, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        static private void OpenFileSearch(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var item = FindFilenameInProject(filename);
            if (item != null)
            {
                var win = item.Open();
                if (win != null)
                {
                    win.Activate();
                }
            }
            else
            {
                MessageWindow.Display(new MessageContent("Unable to find the file: "+filename));
            }
        }

        static public void OpenFile(UnitValue unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = CompilerData.Instance.Folders.GetUnitPath(unit);
            if (fullPath != null && File.Exists(fullPath))
            {
                var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                Assumes.Present(applicationObject);
                applicationObject.ItemOperations.OpenFile(fullPath);
            }
            else
            {
                //Fallback to try to find this document in the solution
                OpenFileSearch(unit.Name);
            }

        }

        static public void OpenFile(CompileValue value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = CompilerData.Instance.Folders.GetValuePath(CompilerData.CompileCategory.Include, value);
            if (fullPath != null && File.Exists(fullPath))
            {
                var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                Assumes.Present(applicationObject);
                applicationObject.ItemOperations.OpenFile(fullPath);
            }
            else
            {
                //Fallback to try to find this document in the solution
                OpenFileSearch(value.Name);
            }
        }

        static public Document GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.ActiveDocument;
        }

        static public void ShowActiveTimeline()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document doc = GetActiveDocument();
            if (doc == null)
            {
                MessageWindow.Display(new MessageContent("Unable to get the active document."));
                return;
            }

            string path = doc.FullName.ToLower();
            string filename = Path.GetFileName(path);

            var compilerData = CompilerData.Instance;

            var unit = compilerData.Folders.GetUnitByPath(path);
            if (unit == null)
            { 
                unit = compilerData.GetUnitByName(filename); //fallback to just match by name 
            }
            if (unit != null) 
            {   
                Timeline.CompilerTimeline.Instance.DisplayTimeline(unit);
                return;
            }

            var value = compilerData.Folders.GetValueByPath(CompilerData.CompileCategory.Include, path);
            if (value == null)
            {
                value = compilerData.GetValueByName(CompilerData.CompileCategory.Include, filename); //fallback to just match by name 
            }
            if (value != null)
            {
                Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value);
                return;
            }
            
            MessageWindow.Display(new MessageContent("Unable to find the compile score timeline for "+ filename));
        }
    }
}
