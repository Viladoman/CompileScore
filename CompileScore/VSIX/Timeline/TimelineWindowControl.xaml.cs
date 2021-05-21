using Microsoft.VisualStudio.Shell;
using System.Windows.Controls;

namespace CompileScore.Timeline
{
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

            timeline.SetMode(Timeline.Mode.Timeline);
        }

        public void SetTimeline(UnitValue unit, CompileValue value = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            timeline.SetUnit(unit);
            timeline.FocusNode(value);
        }
    }
}