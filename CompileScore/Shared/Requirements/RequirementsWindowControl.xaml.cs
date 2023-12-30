using Microsoft.VisualStudio.Shell;
using System.Windows.Controls;

namespace CompileScore.Requirements
{
    /// <summary>
    /// Interaction logic for TimelineWindowControl.
    /// </summary>
    public partial class RequirementsWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementsWindowControl"/> class.
        /// </summary>
        public RequirementsWindowControl()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CompilerData compilerData = CompilerData.Instance;
            compilerData.Hydrate(CompilerData.HydrateFlag.Main);

            this.InitializeComponent();
        }

        public void SetRequirements(ParserUnit parserUnit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            graph.SetUnit(parserUnit);
        }
    }
}