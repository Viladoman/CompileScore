using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Xml;

namespace CompileScore
{
    class ExtractorUnreal : ExtractorVisualStudio
    {
        public static string GetModulePath(string path)
        {
            var dirInfo = Directory.GetParent(path);
            while (dirInfo != null)
            {
                path = dirInfo.FullName;
                string folderName = Path.GetFileName(path);
                if (File.Exists(path + "\\" + folderName + ".Build.cs"))
                {
                    return path;
                }

                dirInfo = Directory.GetParent(path); //Next folder
            }

            return null;
        }

        protected override void CaptureExtraProperties(ProjectProperties projProperties, IMacroEvaluator evaluator, ProjectItem projItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projProperties == null) return;

            Parser.Log("Capturing Extra configuration from Unreal...");

            //Basic VS context
            ProjectItem item = projItem;
            if ( item == null)
            {
                Document doc = EditorUtils.GetActiveDocument();
                item = doc == null ? null : doc.ProjectItem;
            }

            if (item == null)
            {
                Parser.Log("Unable to capture Extra configuration from Unreal, no project Item provided.");
                return;
            }

            Project project = item.ContainingProject;

            if (project == null)
            {
                Parser.Log("Unable to capture Extra configuration from Unreal, no project found.");
                return;
            }

            //Find module path & name for the given file 
            string modulePath = GetModulePath( EditorUtils.GetProjectItemFullPath(item) );
            string moduleName = modulePath == null ? null : Path.GetFileName(modulePath);
            Parser.Log(moduleName == null ? "Unable to find Unreal Engine Module." : "Unreal Engine Module Name: " + moduleName);

            //Open project vcxproj as xml 
            //Find the first .cpp file from the given module & steal its configuration
            AppendFileConfiguration(projProperties, SearchInProjectFile(project, modulePath), evaluator);

            //Add basic preprocessor definition
            projProperties.PrepocessorDefinitions.Add("UNREAL_CODE_ANALYZER");
        }

        protected override void ProcessPostProjectData(ProjectProperties projProperties)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            for (int i = 0; i < projProperties.ForceIncludes.Count; ++i)
            {
                string pchFile = projProperties.ForceIncludes[i] + ".pch";
                if (File.Exists(pchFile))
                {
                    Parser.Log("Found incompatible pch file, generating alias for: " + pchFile);

                    string originalName = projProperties.ForceIncludes[i];
                    string newName = Path.GetDirectoryName(originalName) + @"\SL_" + Path.GetFileName(originalName);
                    projProperties.ForceIncludes[i] = newName;

                    File.Copy(originalName, newName, true);
                }
            }
        }

        private XmlNode SearchInProjectFile(Project project, string modulePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get Relative path to modulePath from projectPath
            string projectPath = Path.GetDirectoryName(project.FullName) + '\\';
            string relativePath = MakeRelativePath(projectPath,modulePath);
            if (relativePath == null) return null;
            relativePath += "\\";

            XmlDocument doc = new XmlDocument();
            doc.Load(project.FullName);
            if (doc == null) return null;

            XmlNodeList compileUnits = doc.GetElementsByTagName("ClCompile");
            foreach (XmlNode tu in compileUnits)
            {
                XmlNode fileName = tu.Attributes.GetNamedItem("Include");
                if (fileName != null && fileName.InnerText.StartsWith(relativePath))
                {
                    string fileRelativeToModule = fileName.InnerText.Substring(relativePath.Length);
                    Parser.Log("Found compilation unit in same module: " + fileRelativeToModule);
                    return tu;
                } 
            }

            Parser.Log("Could not find any compilation unit in the same Unreal module.");
            return null;
        }

        private void AppendFileConfiguration(ProjectProperties projProperties, XmlNode ClCompileNode, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ClCompileNode != null)
            {
                foreach (XmlNode child in ClCompileNode.ChildNodes)
                {
                    if (child.Name == "AdditionalIncludeDirectories")
                    {
                        AppendMSBuildStringToList(projProperties.IncludeDirectories, evaluator.Evaluate(child.InnerText));
                    }
                    else if (child.Name == "ForcedIncludeFiles")
                    {
                        AppendMSBuildStringToList(projProperties.ForceIncludes, evaluator.Evaluate(child.InnerText));
                    }
                }
            }
        }

        private static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath)) return null;

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }
    }
}
