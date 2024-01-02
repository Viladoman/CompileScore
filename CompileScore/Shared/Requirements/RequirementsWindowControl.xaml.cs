using Microsoft.VisualStudio.Shell;
using System.Windows.Controls;

namespace CompileScore.Requirements
{
    public partial class RequirementsWindowControl : UserControl
    {
        public RequirementsWindowControl()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CompilerData compilerData = CompilerData.Instance;
            compilerData.Hydrate(CompilerData.HydrateFlag.Main);

            this.InitializeComponent();

            graph.OnGraphNodeSelected += OnGraphNodeSelected;
        }

        public void SetRequirements(ParserUnit parserUnit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            graph.SetUnit(parserUnit);
            details.RootFullPath = parserUnit == null ? null : parserUnit.Filename;
        }

        private void OnGraphNodeSelected(object graphNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            details.SetRequirements(graphNode);
        }   

    }
}