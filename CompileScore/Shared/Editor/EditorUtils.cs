using CompileScore.Overview;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CompileScore
{
    static public class EditorUtils
    {
        public enum EditorMode
        {
            None,
            VisualStudio,
            CMake,
            UnrealEngine,
        }

        public const string IncludeRegex = @"#\s*include\s*[<""]([^>""]+)[>""]";

        static public AsyncPackage Package { get; set; }
        static public IServiceProvider ServiceProvider { get; set; }

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

        static public ProjectItem FindFilenameInProject(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ProjectItem item = FindFilenameInProjectSingle(filename);

            if (item != null)
                return item;

            if (Path.HasExtension(filename))
                return null;

            item = FindFilenameInProjectSingle(filename + ".cpp");
            if (item != null) 
                return item;

            item = FindFilenameInProjectSingle(filename + ".cxx");
            if (item != null)
                return item;

            item = FindFilenameInProjectSingle(filename + ".c");
            if (item != null)
                return item;

            return null;
        }

        static private ProjectItem FindFilenameInProjectSingle(string filename)
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
                    if (String.Equals(item.Name, filename, StringComparison.OrdinalIgnoreCase) || 
                        String.Equals(GetProjectItemFullPath(item), filename, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        static public string GetProjectItemFullPath(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var properties = item.Properties;
            if (properties == null)
                return item.Name;

            try
            {
                return properties.Item("FullPath").Value.ToString();
            }
            catch (Exception)
            {
                return item.Name;
            }
        }

        static public string RemapFullPath(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (fullPath == null)
                return null;

            if (File.Exists(fullPath))
            {
                return fullPath.Replace('/', '\\');
            }

            string filename = GetFileNameSafe(fullPath);

            if ( filename != null)
            {
                ProjectItem item = FindFilenameInProject(filename);

                if ( item != null)
                {
                    string finalPath = GetProjectItemFullPath(item);
                    return finalPath == null ? fullPath.Replace('/', '\\') : finalPath; 
                }
            }

            return fullPath.Replace('/', '\\');
        }

        static private Window OpenFileSearch(string filename)
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
                else
                {
                    MessageWindow.Display(new MessageContent("Unable to open file: " + filename));
                }

                return win;
            }
            
            MessageWindow.Display(new MessageContent("Unable to find the file: " + filename));
            return null;
        }

        static public Window OpenFileByName(string fullPath, string fileName = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (fullPath != null && File.Exists(fullPath))
            {
                var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
                Assumes.Present(applicationObject);
                return applicationObject.ItemOperations.OpenFile(fullPath.Replace('/', '\\'));
            }
            else if ( fileName != null )
            {
                //Fallback to try to find this document in the solution
                OpenFileSearch(fileName);
            }

            return null;
        }

        static public void OpenFile(UnitValue unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = CompilerData.Instance.Folders.GetUnitPath(unit);
            OpenFileByName(fullPath, unit.Name);
        }

        static public bool OpenFileAtLocation(string fullPath, uint line, uint column)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string filename = GetFileNameSafe(fullPath);

            Document doc = null;
            Window window = OpenFileByName(fullPath, filename );
            if (window == null)
            {
                //sometimes it opens but it does not give a window element ( check if opened already )
                Document activeDoc = GetActiveDocument();
                if (activeDoc != null && GetFileNameSafe(activeDoc.FullName) == filename)
                {
                    doc = activeDoc;
                }
            }
            else
            {
                window.Activate();
                doc = window.Document;
            }

            if (doc != null)
            {
                TextSelection sel = (TextSelection)doc.Selection;
                sel.MoveTo((int)line, (int)column);
            }

            return doc != null;
        }

        static public void OpenFile(CompileValue value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = CompilerData.Instance.Folders.GetValuePath(value);
            OpenFileByName(fullPath, value.Name);
        }

        static public string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Solution solution = GetActiveSolution();
            if (solution == null) return null;
            return (Path.HasExtension(solution.FullName) ? Path.GetDirectoryName(solution.FullName) : solution.FullName) + '\\';
        }

        static public Document GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
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

        static public IVsTextView GetActiveView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var textManager = ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);
            return view;
        }

        static public string GetExtensionInstallationDirectory()
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

        static public string GetToolPath(string localPath)
        {
            string installDirectory = GetExtensionInstallationDirectory();
            string ret = installDirectory == null ? null : installDirectory + '\\' + localPath;
            return File.Exists(ret) ? ret : null;
        }

        static public EditorMode GetEditorMode(Project inputProject = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = inputProject == null ? GetActiveProject() : inputProject;
            if (project == null)
            {
                return EditorMode.None;
            }

            if (project.Object == null)
            {
                return EditorMode.CMake;
            }

            Solution solution = GetActiveSolution();
            if (solution != null)
            {
                //Check for unreal project ( $(SolutionName.uproject) || UE4.sln + Engine/Source/UE4Editor.target )
                var uproject = Path.ChangeExtension(solution.FullName, "uproject");
                if (File.Exists(uproject))
                {
                    return EditorMode.UnrealEngine;
                }
                else if (Path.GetFileNameWithoutExtension(solution.FullName) == "UE4" && File.Exists(GetSolutionPath() + @"Engine/Source/UE4Editor.Target.cs"))
                {
                    return EditorMode.UnrealEngine;
                }
                else if (Path.GetFileNameWithoutExtension(solution.FullName) == "UE5" && File.Exists(GetSolutionPath() + @"Engine/Source/UnrealEditor.Target.cs"))
                {
                    return EditorMode.UnrealEngine;
                }
            }

            return EditorMode.VisualStudio;
        }

        static public void SaveActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);

            Document doc = applicationObject.ActiveDocument;
            if (doc != null && !doc.ReadOnly && !doc.Saved)
            {
                doc.Save();
            }
        }

        static public CompileValue GetElementUnderActiveCursor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document doc = GetActiveDocument();
            IVsTextView view = GetActiveView();
            if (doc == null || view == null)
                return null;

            view.GetCaretPos(out int line, out int col);

#if ALLOW_INTELLISENSE
            //Intellisense search disabled to avoid stalls due to VS code parsing
            const string LANGUAGE_VISUAL_C = "{B5E9BD32-6D3E-4B5D-925E-8A43B79820B4}"; 
            ProjectItem projItem = doc == null ? null : doc.ProjectItem;
            FileCodeModel model = projItem == null ? null : projItem.FileCodeModel;
            CodeElements elements = model == null ? null : model.CodeElements;

            //Try to get the info from visual studio internal understand of the file
            if (elements != null && model.Language == LANGUAGE_VISUAL_C)
            {
                int modelLine = line+1;
                foreach (CodeElement element in elements)
                {
                    if (modelLine >= element.StartPoint.Line && modelLine <= element.EndPoint.Line)
                    {
                        if (element.Kind == vsCMElement.vsCMElementIncludeStmt)
                        {
                            string fileName = GetFileNameSafe(element.Name).ToLower();
                            return fileName != null ? CompilerData.Instance.GetValueByName(CompilerData.CompileCategory.Include, fileName) : null;
                        }
                        //TODO ~ ramonv ~ add more elements
                    }
                }

                return null;
            }
#endif
            //if we don't have a model perform a silly regex check
            view.GetBuffer(out IVsTextLines textBuffer);
            if ( textBuffer != null)
            {
                textBuffer.GetLineText(line,0,line+1,0,out string text);
                if ( text != null )
                {
                    MatchCollection matches = Regex.Matches(text, IncludeRegex);
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            string fileName = GetFileNameSafe(match.Groups[1].Value).ToLower();
                            return fileName != null ? CompilerData.Instance.GetValueByName(CompilerData.CompileCategory.Include, fileName) : null;
                        }
                    }
                }

            }

            return null;
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

            object found = CompilerData.Instance.SeekProfilerValueFromFullPath(doc.FullName);

            if (found is UnitValue)
            {
                Timeline.CompilerTimeline.Instance.DisplayTimeline((UnitValue)found);
            }
            else if ( found is CompileValue)
            {
                CompileValue value = (CompileValue)found;
                Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value);
            }
            else
            {
                MessageWindow.Display(new MessageContent("Unable to find the compile score timeline for "+ doc.FullName));
            }

        }
    }
}
