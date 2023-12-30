using CompileScore.Includers;
using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Requirements
{
    public partial class RequirementsGraphTooltip : UserControl
    {
        private object node = null;
        public object ReferenceNode
        { 
            set { node = value; OnNode(); }  
            get { return node; } 
        }

        public RequirementGraphRoot RootNode { set; get; }

        public RequirementsGraphTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if (node == null)
                return;

            if (node is RequirementGraphRoot)
            {
                RequirementGraphRoot root = node as RequirementGraphRoot;
                headerText.Text = root.Label;
                descriptionText.Text = root.Value.Filename;
                detailsText.Text = Timeline.TimelineNodeTooltip.GetDetailsText(root.ProfilerValue);
            }
            else if (node is RequirementGraphNode)
            {
                RequirementGraphNode graphNode = node as RequirementGraphNode;
                headerText.Text = graphNode.Label;
                descriptionText.Text = graphNode.Value.Name;
                detailsText.Text = Timeline.TimelineNodeTooltip.GetDetailsText(graphNode.ProfilerValue, graphNode.IncluderValue, RootNode == null ? "??" : RootNode.Label);
            }

            if (detailsText.Text.Length == 0)
            {
                detailsBorder.Visibility = Visibility.Collapsed;
                detailsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                detailsBorder.Visibility = Visibility.Visible;
                detailsPanel.Visibility = Visibility.Visible;
            }

            //TODO ~ ramonv ~ add requirements info 
        }
    }
}
