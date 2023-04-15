using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace CompileScore.Includers
{
    [Guid("9b49a2ac-bed5-4ca8-bcb7-a6643783b566")]
    public class IncludersWindow : Common.WindowProxy
    {
        public IncludersWindow()
        {
            this.Caption = "Compile Score Includers";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new IncludersWindowControl();
        }

        public void SetIncluders(CompileValue value = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            (this.Content as IncludersWindowControl).SetIncluders(value);
        }
    }
}
