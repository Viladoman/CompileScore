namespace CompileScore.Timeline
{
    using Microsoft.VisualStudio.Shell;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for TimelineWindowControl.
    /// </summary>
    public partial class TimelineWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineWindowControl"/> class.
        /// </summary>
        public TimelineWindowControl()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.InitializeComponent();
        }

        public void SetTimeline(FullUnitValue unit, CompileValue value = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            timeline.SetUnit(unit);
            timeline.FocusNode(value);
        }
    }
}