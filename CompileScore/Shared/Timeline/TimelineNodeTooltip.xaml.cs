using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Timeline
{
    /// <summary>
    /// Interaction logic for TimelineNodeTooltip.xaml
    /// </summary>
    public partial class TimelineNodeTooltip : UserControl
    {
        private TimelineNode node = null;
        public TimelineNode ReferenceNode
        { 
            set { node = value; OnNode(); }  
            get { return node; } 
        }

        public Timeline.Mode Mode { set; get; }

        public TimelineNodeTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if (node != null)
            {
                headerText.Text = Common.UIConverters.ToSentenceCase(node.Category.ToString());

                if (Mode == Timeline.Mode.Includers)
                {
                    durationText.Text = (node.Duration/CompileScore.Includers.CompilerIncluders.durationMultiplier).ToString();
                    durationText.Visibility = node.Category == CompilerData.CompileCategory.Include? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    durationText.Text = Common.UIConverters.GetTimeStr(node.Duration);
                    durationText.Visibility = Visibility.Visible;
                }

                if (node.Value is CompileValue)
                {
                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Visible;
                    detailsPanel.Visibility = Visibility.Visible;

                    CompileValue val = (node.Value as CompileValue);
                    descriptionText.Text = val.Name;
                    detailsText.Text = "Max: "   + Common.UIConverters.GetTimeStr(val.Max)
                                     +" (Self: " + Common.UIConverters.GetTimeStr(val.SelfMax) + ")"
                                     +" Min: "   + Common.UIConverters.GetTimeStr(val.Min)
                                     +" Avg: "   + Common.UIConverters.GetTimeStr(val.Average) 
                                     +" Count: " + val.Count;
                }
                else if (node.Value is UnitValue)
                {
                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Collapsed;
                    detailsPanel.Visibility = Visibility.Collapsed;

                    descriptionText.Text = (node.Value as UnitValue).Name;
                }
                else
                {
                    descriptionText.Visibility = Visibility.Collapsed;
                    detailsBorder.Visibility = Visibility.Collapsed;
                    detailsPanel.Visibility = Visibility.Collapsed;
                }              
            }
        }
            
    }
}
