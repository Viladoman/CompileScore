using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        public TimelineNodeTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if (node != null)
            {
                headerText.Text = Common.UIConverters.ToSentenceCase(node.Category.ToString());
                durationText.Text = Common.UIConverters.GetTimeStr(node.Duration);

                if (node.Value is CompileValue)
                {
                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Visible;
                    detailsPanel.Visibility = Visibility.Visible;

                    CompileValue val = (node.Value as CompileValue);
                    descriptionText.Text = val.Name;
                    detailsText.Text = "Max: "+Common.UIConverters.GetTimeStr(val.Max)
                                     +" Min: "+ Common.UIConverters.GetTimeStr(val.Min)
                                     +" Avg: "+ Common.UIConverters.GetTimeStr(val.Mean) 
                                     +" Count: "+ val.Count;
                }
                else if (node.Value is FullUnitValue)
                {
                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Collapsed;
                    detailsPanel.Visibility = Visibility.Collapsed;

                    descriptionText.Text = (node.Value as FullUnitValue).Name;
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
