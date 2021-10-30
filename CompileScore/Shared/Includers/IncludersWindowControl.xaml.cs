using Microsoft.VisualStudio.Shell;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Includers
{
    /// <summary>
    /// Interaction logic for IncludersWindowControl.
    /// </summary>
    public partial class IncludersWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IncludersWindowControl"/> class.
        /// </summary>
        public IncludersWindowControl()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.InitializeComponent();

            timeline.SetMode(Timeline.Timeline.Mode.Includers);
        }

        public void SetIncluders(CompileValue value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            timeline.SetIncluders(value);
        }
    }
}