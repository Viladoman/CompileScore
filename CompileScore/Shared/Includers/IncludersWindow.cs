using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace CompileScore.Includers
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("9b49a2ac-bed5-4ca8-bcb7-a6643783b566")]
    public class IncludersWindow : Common.WindowProxy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IncludersWindow"/> class.
        /// </summary>
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
