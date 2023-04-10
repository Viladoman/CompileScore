using System.Windows;
using System.Windows.Controls;

namespace CompileScore
{
    public partial class TextEditorAdornmentTooltip : UserControl
    {
        private object node = null;
        public object Reference
        { 
            set { node = value; OnNode(); }  
            get { return node; } 
        }

        public TextEditorAdornmentTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if ( node is CompileValue )
            {
                CompileValue value = (CompileValue)node;
                headerText.Text = value.Name;

                int unitCount = CompilerData.Instance.GetUnits().Count;
                float unitImpactPercent = unitCount > 0 ? ((float)value.Count * 100) / CompilerData.Instance.GetUnits().Count : 0;
                descriptionText.Text = "Edit Impact (Units): " + value.Count + " (" + unitImpactPercent.ToString("n2") + "%)";

                detailsBorder.Visibility = Visibility.Visible;
                detailsPanel.Visibility = Visibility.Visible;

                detailsText.Text = "Max: " + Common.UIConverters.GetTimeStr(value.Max)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(value.SelfMax) + ")"
                                 + " Min: " + Common.UIConverters.GetTimeStr(value.Min)
                                 + " Avg: " + Common.UIConverters.GetTimeStr(value.Average)
                                 + " Acc: " + Common.UIConverters.GetTimeStr(value.Accumulated)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(value.SelfAccumulated) + ")";
            }
            else if ( node is UnitValue )
            {
                UnitValue value = (UnitValue)node;
                headerText.Text = value.Name;

                descriptionText.Visibility = Visibility.Collapsed;

                //description : Impact edit
                detailsBorder.Visibility = Visibility.Visible;
                detailsPanel.Visibility = Visibility.Visible;

                detailsText.Text = "Duration: "  + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.ExecuteCompiler])
                                 + " FrontEnd: " + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.FrontEnd])
                                 + " Includes: " + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.Include])
                                 + " BackEnd: "  + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.BackEnd]);
            }
            else
            {
                descriptionText.Visibility = Visibility.Collapsed;
                detailsBorder.Visibility = Visibility.Collapsed;
                detailsPanel.Visibility = Visibility.Collapsed;
            }


            /*
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
            */
        }
            
    }
}
