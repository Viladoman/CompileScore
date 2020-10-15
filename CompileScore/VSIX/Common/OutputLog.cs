namespace CompileScore
{
    using EnvDTE80;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using System;

    public static class OutputLog
    {
        private static IVsOutputWindowPane pane;
        public static void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            CreatePane(serviceProvider, Guid.NewGuid(), "Compile Score", true, false);
        }

        public static void Log(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write(text);
        }

        public static void Error(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write("[ERROR] "+text);
        }

        private static void Write(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DateTime currentTime = DateTime.Now;
            pane.OutputString("["+ String.Format("{0:HH:mm:ss}", currentTime) + "] "+ text + "\n");
        }

        private static void CreatePane(IServiceProvider serviceProvider, Guid paneGuid, string title, bool visible, bool clearWithSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsOutputWindow output = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
            Assumes.Present(output);

            // Create a new pane.
            output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));

            // Retrieve the new pane.
            output.GetPane(ref paneGuid, out pane);
        }
    }
}
