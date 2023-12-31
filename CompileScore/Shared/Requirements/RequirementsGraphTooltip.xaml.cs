using CompileScore.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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

        private void SetScore(object value)
        {
            if (value == null )
                return;

            if ( value is CompileValue)
            {
                float severity = (value as CompileValue).Severity;

                score0.SetMoniker(severity > 0 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score1.SetMoniker(severity > 1 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score2.SetMoniker(severity > 2 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score3.SetMoniker(severity > 3 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score4.SetMoniker(severity > 4 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
            }
            
            scoreGrid.Visibility = value is CompileValue ? Visibility.Visible : Visibility.Collapsed;
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
                SetScore(root.ProfilerValue);
            }
            else if (node is RequirementGraphNode)
            {
                RequirementGraphNode graphNode = node as RequirementGraphNode;
                headerText.Text = graphNode.Label;
                descriptionText.Text = graphNode.Value.Name;
                detailsText.Text = Timeline.TimelineNodeTooltip.GetDetailsText(graphNode.ProfilerValue, graphNode.IncluderValue, RootNode == null ? "??" : RootNode.Label);
                SetScore(graphNode.ProfilerValue);
            }

            profilerGrid.Visibility = detailsText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            profilerBorder.Visibility = detailsText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            //TODO ~ ramonv ~ add requirements info 

        }
    }
}
