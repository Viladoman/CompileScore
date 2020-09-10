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
                descriptionText.Text = node.Value != null ? node.Value.Name : "";
                durationText.Text = "Duration: " + Common.UIConverters.GetTimeStr(node.Duration);
                //TODO ~ ramonv ~ placeholder data 
            }
        }
            
    }
}
