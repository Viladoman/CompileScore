using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CompileScore
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(CompileScorePackage.PackageGuidString)]
    [ProvideOptionPage(typeof(GeneralSettingsPageGrid), "Compile Score", "General", 0, 0, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(CompileScore.Overview.OverviewWindow))]
    [ProvideToolWindow(typeof(CompileScore.Timeline.TimelineWindow))]
    [ProvideToolWindow(typeof(CompileScore.Includers.IncludersWindow))]
    public sealed class CompileScorePackage : AsyncPackage
    {
        /// <summary>
        /// CompileScorePackage GUID string.
        /// </summary>
        public const string PackageGuidString = "b55e42c2-29b6-44c4-9ebc-da319e3301d2";

#region Package Members

        public GeneralSettingsPageGrid GetGeneralSettings() { return (GeneralSettingsPageGrid)GetDialogPage(typeof(GeneralSettingsPageGrid)); }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            OutputLog.Initialize(this);

            DocumentLifetimeManager.Initialize(this);
            SettingsManager.Instance.Initialize();
            CompilerData.Instance.Initialize(this, this);
            EditorUtils.Initialize(this,this);
            Profiler.Instance.Initialize(this);
            Timeline.CompilerTimeline.Instance.Initialize(this);
            Includers.CompilerIncluders.Instance.Initialize(this);

            EditorContext.Instance.Initialize(this);

            await CustomCommands.InitializeAsync(this,this);
        }

#endregion
    }
}
