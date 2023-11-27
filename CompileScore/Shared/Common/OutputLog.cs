using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace CompileScore
{
    public static class OutputLog
    {
        public enum PaneInstance
        { 
            Default = 0, 
            Parser,
        };

        private static string GetPaneInstanceTitle(PaneInstance instance)
        {
            switch (instance)
            { 
                case PaneInstance.Parser:  
                    return "Compile Score Parser";

                case PaneInstance.Default: 
                default:
                    return "Compile Score";
            }
        }

        private static IServiceProvider ServiceProvider { set; get; }

        private static IVsOutputWindowPane[] paneInstances = { null, null };

        public static IVsOutputWindowPane GetPane(PaneInstance instance = PaneInstance.Default)
        {
            //Lazy creation to reduce noise. It will be created on the first request
            ThreadHelper.ThrowIfNotOnUIThread();
            int instanceIndex = (int)instance;
            if (paneInstances[instanceIndex] == null)
            {
                paneInstances[instanceIndex] = CreatePane(ServiceProvider, Guid.NewGuid(), GetPaneInstanceTitle(instance), true, false);
            }
            return paneInstances[instanceIndex];
        }

        public static IVsOutputWindowPane GetPaneUnsafe(PaneInstance instance = PaneInstance.Default)
        {
            //Lazy creation to reduce noise. It will be created on the first request
            return paneInstances[(int)instance];
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public static void Clear(PaneInstance instance = PaneInstance.Default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetPane(instance).Clear();
        }

        public static void Focus(PaneInstance instance = PaneInstance.Default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetPane(instance).Activate();
        }

        public static void Log(string text, PaneInstance instance = PaneInstance.Default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write(text, instance);
        }

        public static void Error(string text, PaneInstance instance = PaneInstance.Default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write("[ERROR] " + text, instance);
        }

        private static void Write(string text, PaneInstance instance)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DateTime currentTime = DateTime.Now;
            GetPane(instance).OutputString("[" + String.Format("{0:HH:mm:ss}", currentTime) + "] " + text + "\n");
        }

        private static IVsOutputWindowPane CreatePane(IServiceProvider serviceProvider, Guid paneGuid, string title, bool visible, bool clearWithSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsOutputWindow output = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
            Assumes.Present(output);

            // Create a new pane.
            output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));

            // Retrieve the new pane.
            IVsOutputWindowPane pane;
            output.GetPane(ref paneGuid, out pane);
            return pane;
        }

        public static async System.Threading.Tasks.Task ErrorGlobalAsync(string text, PaneInstance instance = PaneInstance.Default)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Error(text, instance);
        }
        public static async System.Threading.Tasks.Task LogGlobalAsync(string text, PaneInstance instance = PaneInstance.Default)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Log(text, instance);
        }
    }
}
