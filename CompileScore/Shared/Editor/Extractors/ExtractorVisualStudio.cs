﻿using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.IO;

namespace CompileScore
{
    public class ExtractorVisualStudio : IExtractor
    {
        public override ProjectProperties GetProjectData(ProjectItem projItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Parser.Log("Capturing configuration from VS projects...");

            Project project = projItem == null ? EditorUtils.GetActiveProject() : projItem.ContainingProject;

            VCProject prj = project.Object as VCProject;
            if (prj == null) return null;

            VCConfiguration config = prj.ActiveConfiguration;
            if (config == null) return null;

            VCPlatform platform = config.Platform as VCPlatform;
            if (platform == null) return null;

            var vctools = config.Tools as IVCCollection;
            if (vctools == null) return null;

            var midl = vctools.Item("VCMidlTool") as VCMidlTool;

            var evaluator = new MacroEvaluatorVisualPlatform(platform);

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;
            ret.Standard = GetStandardVersion(config);

            //Working directory (always local to processed file)
            ret.WorkingDirectory = Path.GetDirectoryName(project.FullName);

            //Include dirs / files and preprocessor

            //CAUTION: platform.IncludeDirectories might be corrupted and it will crash Visual Studio ( platform.IncludeDirectories are equivalent to the INCLUDE environment var according to the documentation)
            //string includeDirectories = platform.IncludeDirectories;

            AppendMSBuildStringToList(ret.IncludeDirectories, evaluator.Evaluate("$(IncludePath)"));

            AppendProjectProperties(ret, vctools.Item("VCCLCompilerTool") as VCCLCompilerTool, vctools.Item("VCNMakeTool") as VCNMakeTool, evaluator);

            //Get settings from the single file (this might fail badly if there are no settings to capture)
            ProjectItem item = projItem;
            VCFile vcfile = item != null ? item.Object as VCFile : null;
            IVCCollection fileCfgs = vcfile != null ? (IVCCollection)vcfile.FileConfigurations : null;
            VCFileConfiguration fileConfig = fileCfgs != null ? fileCfgs.Item(config.Name) as VCFileConfiguration : null;
            VCCLCompilerTool fileToolCL = null;
            VCNMakeTool fileToolNMake = null;

            try
            {
                fileToolCL = fileConfig.Tool as VCCLCompilerTool;
                fileToolNMake = fileConfig.Tool as VCNMakeTool;
            }
            catch (Exception e)
            {
                //If we really need this data we can always parse the vcxproj as an xml 
                Parser.Log("File specific properties not found, only project properties used (" + e.Message + ")");
            }

            AppendProjectProperties(ret, fileToolCL, fileToolNMake, evaluator);

            CaptureExtraProperties(ret, evaluator, projItem);

            AddCustomSettings(ret, evaluator);

            ProcessPostProjectData(ret);

            return ret;
        }

        public override string EvaluateMacros(string input, Project inputProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = inputProject == null ? EditorUtils.GetActiveProject() : inputProject;
            VCProject prj = project == null ? null : project.Object as VCProject;
            VCConfiguration config = prj == null? null : prj.ActiveConfiguration;
            VCPlatform platform = config == null? null : config.Platform as VCPlatform;

            return EvaluateMacros(input, platform);
        }

        private string EvaluateMacros(string input, VCPlatform platform)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var evaluatorExtra = new MacroEvaluatorExtra();
            string output = evaluatorExtra.Evaluate(input);
            var evaluatorVS = platform == null ? null : new MacroEvaluatorVisualPlatform(platform);
            return evaluatorVS == null ? output : evaluatorVS.Evaluate(output);
        }

        protected virtual void CaptureExtraProperties(ProjectProperties projProperties, IMacroEvaluator evaluator, ProjectItem projItem) { }
        protected virtual void ProcessPostProjectData(ProjectProperties projProperties) { }

        private ProjectProperties.StandardVersion GetStandardVersion(VCConfiguration config)
        {
            IVCRulePropertyStorage generalRule = config.Rules.Item("ConfigurationGeneral") as IVCRulePropertyStorage;
            string value = null;

            try { value = generalRule == null ? null : generalRule.GetEvaluatedPropertyValue("LanguageStandard"); } catch (Exception) { }

            if (value == "Default") { return ProjectProperties.StandardVersion.Default; }
            else if (value == "stdcpp14") { return ProjectProperties.StandardVersion.Cpp14; }
            else if (value == "stdcpp17") { return ProjectProperties.StandardVersion.Cpp17; }
            else if (value == "stdcpp20") { return ProjectProperties.StandardVersion.Cpp20; }
            else if (value == "stdcpplatest") { return ProjectProperties.StandardVersion.Latest; }

            return ProjectProperties.StandardVersion.Latest;
        }

        private void AppendProjectProperties(ProjectProperties properties, VCCLCompilerTool cl, VCNMakeTool nmake, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cl != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories, evaluator.Evaluate(cl.FullIncludePath));
                AppendMSBuildStringToList(properties.ForceIncludes, evaluator.Evaluate(cl.ForcedIncludeFiles));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(cl.PreprocessorDefinitions));

                //PCH
                if (cl.UsePrecompiledHeader != pchOption.pchNone)
                {
                    AppendMSBuildStringToList(properties.ForceIncludes, evaluator.Evaluate(cl.PrecompiledHeaderThrough));
                }
            }
            else if (nmake != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories, evaluator.Evaluate(nmake.IncludeSearchPath));
                AppendMSBuildStringToList(properties.ForceIncludes, evaluator.Evaluate(nmake.ForcedIncludes));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(nmake.PreprocessorDefinitions));
            }
        }
    }
}
