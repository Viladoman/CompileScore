namespace CompileScore.Requirements
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

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
    [Guid("E2E4BD12-544B-4450-8F20-42C6B8C19E7B")]
    public class RequirementsWindow : Common.WindowProxy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineWindow"/> class.
        /// </summary>
        public RequirementsWindow()
        {
            this.Caption = "Compile Score Requirements";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new RequirementsWindowControl();
        }

        public void SetRequirements(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
        
            (this.Content as RequirementsWindowControl).SetRequirements(fullPath); 
        }

    }
}
