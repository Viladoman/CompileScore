using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CompileScore
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CustomCommands
    {
        public const int CommandId_OverviewWindow = 0x0100;
        public static readonly Guid CommandSet_OverviewWindow = new Guid("e5262ec1-fb68-442d-92f7-0b4a66774209");

        public const int CommandId_TimelineWindow = 257;
        public static readonly Guid CommandSet_TimelineWindow = new Guid("e5262ec1-fb68-442d-92f7-0b4a66774209");

        public const int CommandId_Build         = 256;
        public const int CommandId_Rebuild       = 257;
        public const int CommandId_PLACEHOLDER_Generate      = 259;

        public const int CommandId_LoadDefault   = 260;
        public const int CommandId_Settings      = 261;
        public const int CommandId_Documentation = 262;
        public const int CommandId_About         = 264;

        public static readonly Guid CommandSet_Custom = new Guid("f76ad68f-41c2-4f8d-945e-427b0d092da1");

        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in Build's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(commandService);

            commandService.AddCommand(new MenuCommand(Execute_OverviewWindow,   new CommandID(CommandSet_OverviewWindow, CommandId_OverviewWindow)));
            commandService.AddCommand(new MenuCommand(Execute_TimelineWindow,   new CommandID(CommandSet_TimelineWindow, CommandId_TimelineWindow)));

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
                var menuItem = new OleMenuCommand(Execute_PLACEHOLDER_Generate, new CommandID(CommandSet_Custom, CommandId_PLACEHOLDER_Generate));
                menuItem.BeforeQueryStatus += Query_PLACHOLDER_Generate_CanBuild;
                commandService.AddCommand(menuItem);
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

        private static void Query_PLACHOLDER_Generate_CanBuild(object sender, EventArgs args)
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

        private static void Execute_Build(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.BuildSolution();
        }

        private static void Execute_Rebuild(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.RebuildSolution();
        }

        private static void Execute_PLACEHOLDER_Generate(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Profiler.Instance.PLACEHOLDER_GenerateScore();
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
