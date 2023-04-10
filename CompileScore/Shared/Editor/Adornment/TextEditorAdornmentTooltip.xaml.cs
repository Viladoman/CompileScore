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
                
                int unitCount = CompilerData.Instance.GetUnits().Count;
                float unitImpactPercent = unitCount > 0 ? ((float)value.Count * 100) / CompilerData.Instance.GetUnits().Count : 0;
                descriptionText.Text = "Edit Impact (Units): " + value.Count + " (" + unitImpactPercent.ToString("n2") + "%)";

                detailsBorder.Visibility = Visibility.Visible;
                detailsPanel.Visibility = Visibility.Visible;

                detailsText.Text = "Max: " + Common.UIConverters.GetTimeStr(value.Max)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(value.SelfMax) + ")"
                                 + " Acc: " + Common.UIConverters.GetTimeStr(value.Accumulated)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(value.SelfAccumulated) + ")";
            }
            else if ( node is UnitValue )
            {
                UnitValue value = (UnitValue)node;

                descriptionText.Visibility = Visibility.Collapsed;
                detailsBorder.Visibility = Visibility.Collapsed;

                detailsPanel.Visibility = Visibility.Visible;
                detailsText.Text = "Duration: " + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.ExecuteCompiler])
                                 + " (Includes: " + Common.UIConverters.GetTimeStr(value.ValuesList[(int)CompilerData.CompileCategory.Include]) + ")";
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
