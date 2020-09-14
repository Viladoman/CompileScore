namespace CompileScore.Timeline
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
    [Guid("47d73012-ee0b-4100-864e-89f4cedbd77a")]
    public class TimelineWindow : Common.WindowProxy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineWindow"/> class.
        /// </summary>
        public TimelineWindow()
        {
            this.Caption = "Compile Score Timeline";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new TimelineWindowControl();
        }

        public void SetTimeline(FullUnitValue unit, CompileValue value = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            (this.Content as TimelineWindowControl).SetTimeline(unit, value);
        }
    }
}
