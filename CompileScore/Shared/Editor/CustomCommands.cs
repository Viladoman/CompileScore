using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CompileScore
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CustomCommands
    {
        public const int CommandId_OverviewWindow = 0x0100;
        public const int CommandId_TimelineWindow = 257;
        public const int CommandId_IncludersWindow = 258;
        public static readonly Guid CommandSet_Windows = new Guid("e5262ec1-fb68-442d-92f7-0b4a66774209");

        public const int CommandId_Build          = 256;
        public const int CommandId_Rebuild        = 257;
        public const int CommandId_BuildProject   = 267;
        public const int CommandId_RebuildProject = 268;
		public const int CommandId_StartTrace     = 269;
		public const int CommandId_StopTrace      = 270;

		public const int CommandId_Generate       = 259;
		public const int CommandId_Clean          = 258;

        public const int CommandId_LoadDefault    = 260;
        public const int CommandId_Settings       = 261;
        public const int CommandId_Documentation  = 262;
        public const int CommandId_About          = 264;

        public const int CommandId_ShowTimeline   = 265;
        public const int CommandId_ShowIncluders  = 266;

        public static readonly Guid CommandSet_Custom = new Guid("f76ad68f-41c2-4f8d-945e-427b0d092da1");

        private static IServiceProvider ServiceProvider { set; get; }

        public static async Task InitializeAsync(AsyncPackage package, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            // Switch to the main thread - the call to AddCommand in Build's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(commandService);

            commandService.AddCommand(new MenuCommand(Execute_OverviewWindow,   new CommandID(CommandSet_Windows, CommandId_OverviewWindow)));
            commandService.AddCommand(new MenuCommand(Execute_TimelineWindow,   new CommandID(CommandSet_Windows, CommandId_TimelineWindow)));
            commandService.AddCommand(new MenuCommand(Execute_IncludersWindow,  new CommandID(CommandSet_Windows, CommandId_IncludersWindow)));

            {
                var menuItem = new OleMenuCommand(Execute_ShowTimeline, new CommandID(CommandSet_Custom, CommandId_ShowTimeline));
                menuItem.BeforeQueryStatus += Query_CanShowTimeline;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_ShowIncluders, new CommandID(CommandSet_Custom, CommandId_ShowIncluders));
                menuItem.BeforeQueryStatus += Query_CanShowIncluders;
                commandService.AddCommand(menuItem);
            }

            commandService.AddCommand(new MenuCommand(Execute_Settings,      new CommandID(CommandSet_Custom, CommandId_Settings)));
            commandService.AddCommand(new MenuCommand(Execute_Documentation, new CommandID(CommandSet_Custom, CommandId_Documentation)));
            commandService.AddCommand(new MenuCommand(Execute_About,         new CommandID(CommandSet_Custom, CommandId_About)));

            {
                var menuItem = new OleMenuCommand(Execute_LoadDefault, new CommandID(CommandSet_Custom, CommandId_LoadDefault));
                menuItem.BeforeQueryStatus += Query_CanLoadDefault;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_Build, new CommandID(CommandSet_Custom, CommandId_Build));
                menuItem.BeforeQueryStatus += Query_CanBuild;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_Rebuild, new CommandID(CommandSet_Custom, CommandId_Rebuild));
                menuItem.BeforeQueryStatus += Query_CanBuild;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_BuildProject, new CommandID(CommandSet_Custom, CommandId_BuildProject));
                menuItem.BeforeQueryStatus += Query_CanBuild;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_RebuildProject, new CommandID(CommandSet_Custom, CommandId_RebuildProject));
                menuItem.BeforeQueryStatus += Query_CanBuild;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_Clang_Generate, new CommandID(CommandSet_Custom, CommandId_Generate));
                menuItem.BeforeQueryStatus += Query_Is_Clang_Available;
                commandService.AddCommand(menuItem);
            }

            {
                var menuItem = new OleMenuCommand(Execute_Clang_Clean, new CommandID(CommandSet_Custom, CommandId_Clean));
                menuItem.BeforeQueryStatus += Query_Is_Clang_Available;
                commandService.AddCommand(menuItem);
            }

            {
				var menuItem = new OleMenuCommand(Execute_StartTrace, new CommandID(CommandSet_Custom, CommandId_StartTrace));
				commandService.AddCommand(menuItem);
			}

			{
				var menuItem = new OleMenuCommand(Execute_StopTrace, new CommandID(CommandSet_Custom, CommandId_StopTrace));
				commandService.AddCommand(menuItem);
			}
		}

		private static void Query_CanShowTimeline(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Visible = menuCommand.Enabled = CompilerData.Instance.GetUnits().Count > 0;
            }
        }
        private static void Query_CanShowIncluders(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                bool canTrigger = CompilerData.Instance.GetUnits().Count > 0 && EditorUtils.GetElementUnderActiveCursor() != null;
                menuCommand.Visible = menuCommand.Enabled = canTrigger;
            }
        }

        private static void Query_CanBuild(object sender, EventArgs args)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Visible = menuCommand.Enabled = Profiler.Instance.IsAvailable();
            }
        }

        private static void Query_Is_Clang_Available(object sender, EventArgs args)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Visible = menuCommand.Enabled = Profiler.Instance.IsAvailable() && SettingsManager.Instance.Settings.ScoreGenerator.Compiler == Profiler.Compiler.Clang;
            }
        }

        private static void Query_CanLoadDefault(object sender, EventArgs args)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Visible = menuCommand.Enabled = CompilerData.Instance.Source == CompilerData.DataSource.Forced;
            }
        }

        //Command executions

        private static void Execute_OverviewWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EditorUtils.FocusOverviewWindow();
        }

        private static void Execute_TimelineWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Timeline.CompilerTimeline.Instance.FocusTimelineWindow();
        }

        private static void Execute_IncludersWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Includers.CompilerIncluders.Instance.FocusIncludersWindow();
        }

        private static void Execute_ShowTimeline(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CompileValue value = EditorUtils.GetElementUnderActiveCursor();
            if ( value != null )
            {
                Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value);
            }
            else
            {
                EditorUtils.ShowActiveTimeline();
            }
        }

        private static void Execute_ShowIncluders(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CompileValue value = EditorUtils.GetElementUnderActiveCursor();
            if (value != null)
            {
                Includers.CompilerIncluders.Instance.DisplayIncluders(value);
            }
        }

        private static void Execute_Build(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.TriggerOperation(Profiler.BuildOperation.Build);
        }

        private static void Execute_Rebuild(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.TriggerOperation(Profiler.BuildOperation.Rebuild);
        }

        private static string GetSolutionExplorerSelectedName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);

            var selectedItems = applicationObject.ToolWindows.SolutionExplorer.SelectedItems as UIHierarchyItem[];
            if (selectedItems == null || selectedItems.Length == 0)
            {
                OutputLog.Error("Unable to retrieve the selected item");
                return null;
            }

            return selectedItems[0].Name;
        }

        private static void Execute_BuildProject(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string selected = GetSolutionExplorerSelectedName();
            if (selected != null)
            {
                Profiler.Instance.TriggerOperation(Profiler.BuildOperation.Build, selected);
            }
        }

        private static void Execute_RebuildProject(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string selected = GetSolutionExplorerSelectedName();
            if (selected != null)
            {
                Profiler.Instance.TriggerOperation(Profiler.BuildOperation.Rebuild, selected);
            }
        }

        private static void Execute_StartTrace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

			Profiler.Instance.TriggerStartTrace();
		}

		private static void Execute_StopTrace(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Profiler.Instance.TriggerStopTrace();
		}

		private static void Execute_Clang_Generate(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.TriggerOperation(Profiler.BuildOperation.GenerateClang);
        }

        private static void Execute_Clang_Clean(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.TriggerOperation(Profiler.BuildOperation.CleanClang);
        }

        private static void Execute_LoadDefault(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            CompilerData.Instance.LoadDefaultSource();
        }

        private static void Execute_Settings(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SettingsManager.Instance.OpenSettingsWindow();
        }

        private static void Execute_Documentation(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Documentation.OpenLink(Documentation.Link.MainPage);
        }

        private static void Execute_About(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            AboutWindow dlg = new AboutWindow();
            dlg.ShowDialog();
        }
    }
}
