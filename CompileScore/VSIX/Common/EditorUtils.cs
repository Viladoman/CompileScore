using CompileScore.Overview;
using Microsoft.VisualStudio.Shell;
using System;

namespace CompileScore
{
    static public class EditorUtils
    {
        static private AsyncPackage Package { get; set; }

        static public void Initialize(AsyncPackage package)
        {
            Package = package;
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
    }
}
