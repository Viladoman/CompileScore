using Microsoft.VisualStudio.Shell;
using System.Windows;
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

            SetRequirements(null);
        }

        public void SetRequirements(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string realFullPath = EditorUtils.RemapFullPath(fullPath);
            realFullPath = realFullPath == null ? null : realFullPath.ToLower();

            ParserUnit unit = ParserData.Instance.GetParserUnit(realFullPath);
            SetRequirements(unit, realFullPath);

            if (unit == null && realFullPath != null)
            {
                //we have a query without any sort of data. Trigger a parse
                ParserProcessor.OpenAndParsePath(realFullPath);
            }
        }

        private void SetRequirements(ParserUnit parserUnit, string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            graph.SetUnit(parserUnit);
            details.RootFullPath = fullPath;

            if ( fullPath == null )
            {
                StatusText.Text = "Inspecting: <None>";
                StatusText.ToolTip = null;
                buttonParse.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusText.Text = $"Inspecting: {EditorUtils.GetFileNameSafe(fullPath)}";

                if (parserUnit == null )
                {
                    StatusText.Text += " (No data found)";
                }

                StatusText.ToolTip = fullPath;
                buttonParse.Visibility = Visibility.Visible;
            }
        }

        private void OnGraphNodeSelected(object graphNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            details.SetRequirements(graphNode);
        }

        public void ButtonParse_OnClick(object sender, object e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ParserProcessor.OpenAndParsePath(details.RootFullPath);
        }

        public void ButtonParseActiveDocument_OnClick(object sender, object e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ParserProcessor.ParseActiveDocument();
        }

    }
}