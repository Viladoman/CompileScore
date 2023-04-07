using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CompileScore.Common
{
    public class WindowProxy : ToolWindowPane
    {
        public WindowProxy() : base(null) { }

        public object GetFrame() { return this.Frame; }

        public void ProxyShow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure((this.Frame as IVsWindowFrame).Show());
        }
    }
}
