using CompileScore.Includers;
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

        public static string GetDetailsText(object value, object includerValue = null, string includerName = "")
        {
            string ret = null;

            if (value is CompileValue)
            {
                CompileValue val = (value as CompileValue);
                ret = "Max: " + Common.UIConverters.GetTimeStr(val.Max)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(val.SelfMax) + ")"
                                 + " Min: " + Common.UIConverters.GetTimeStr(val.Min)
                                 + " Avg: " + Common.UIConverters.GetTimeStr(val.Average)
                                 + " Acc: " + Common.UIConverters.GetTimeStr(val.Accumulated)
                                 + " (Self: " + Common.UIConverters.GetTimeStr(val.SelfAccumulated) + ")"
                                 + " Units: " + val.Count;

                string thisDetailsTxt = GetIncluderDetailsText(includerValue, includerName, val.Name);
                if (thisDetailsTxt != null)
                {
                    ret = thisDetailsTxt + "\nGlobal:\n- " + ret;
                }
            }

            return ret;
        }

        private static string GetIncluderDetailsText(object includerValue, string includerName, string includeeName)
        {
            if (includerValue == null)
                return null;

            if (includerValue is IncludersInclValue)
            {
                IncludersInclValue inclValue = includerValue as IncludersInclValue;
                return includerName + " => " + includeeName + ":\n-"
                        + " Max: " + Common.UIConverters.GetTimeStr(inclValue.Max)
                        + " Avg: " + Common.UIConverters.GetTimeStr(inclValue.Average)
                        + " Acc: " + Common.UIConverters.GetTimeStr(inclValue.Accumulated)
                        + " Units: " + inclValue.Count;
            }
            else if (includerValue is IncludersUnitValue)
            {
                IncludersUnitValue inclValue = includerValue as IncludersUnitValue;
                return includerName + " => " + includeeName + ":\n-"
                           + " Duration: " + Common.UIConverters.GetTimeStr(inclValue.Duration);
            }

            return null;
        }

        private IncludersInclValue GetIncluderData(TimelineNode node)
        {
            if (node.Category == CompilerData.CompileCategory.Include && node.Value is CompileValue && node.Parent != null &&
                node.Parent.Category == CompilerData.CompileCategory.Include && node.Parent.Value is CompileValue)
            {
                CompileValue includeeValue = (node.Value as CompileValue);
                CompileValue includerValue = (node.Parent.Value as CompileValue);

                int includeeIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, includeeValue);
                int includerIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, includerValue);

                return CompilerIncluders.Instance.GetIncludeInclValue(includerIndex, includeeIndex);
            }

            return null;
        }

        private void OnNode()
        {
            if (node != null)
            {
                headerText.Text = Common.UIConverters.ToSentenceCase(node.Category.ToString());

                if (node.Category == CompilerData.CompileCategory.Other)
                {
                    string tag = node.Value as string;
                    headerText.Text = tag != null ? tag : headerText.Text;
                }

                if (Mode == Timeline.Mode.Includers)
                {
                    durationText.Text = "Stacks: " + (node.Duration/CompileScore.Includers.CompilerIncluders.durationMultiplier).ToString();
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

                    string parentName = node.Parent.Value != null && node.Parent.Value is CompileValue ? (node.Parent.Value as CompileValue).Name : "??";

                    CompileValue val = (node.Value as CompileValue);
                    descriptionText.Text = val.Name;
                    detailsText.Text = GetDetailsText(val, GetIncluderData(node), parentName);
                }
                else if (node.Value is UnitValue)
                {
                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Collapsed;
                    detailsPanel.Visibility = Visibility.Collapsed;

                    descriptionText.Text = (node.Value as UnitValue).Name;
                }
                else if (node.Value is IncluderTreeLink)
                {
                    IncluderTreeLink treeLink = node.Value as IncluderTreeLink;
                    UnitValue unit = treeLink.Includer as UnitValue;

                    descriptionText.Visibility = Visibility.Visible;
                    detailsBorder.Visibility = Visibility.Visible;
                    detailsPanel.Visibility = Visibility.Visible;

                    if (treeLink.Includer is CompileValue)
                    {
                        CompileValue globalVal = (treeLink.Includer as CompileValue);
                        descriptionText.Text = globalVal.Name;

                        detailsText.Text = "Global:\n- Max: " + Common.UIConverters.GetTimeStr(globalVal.Max)
                                         + " (Self: " + Common.UIConverters.GetTimeStr(globalVal.SelfMax) + ")"
                                         + " Min: " + Common.UIConverters.GetTimeStr(globalVal.Min)
                                         + " Avg: " + Common.UIConverters.GetTimeStr(globalVal.Average)
                                         + " Acc: " + Common.UIConverters.GetTimeStr(globalVal.Accumulated)
                                         + " (Self: " + Common.UIConverters.GetTimeStr(globalVal.SelfAccumulated) + ")"
                                         + " Units: " + globalVal.Count;

                        if (treeLink.Includee != null && treeLink.Value != null && treeLink.Value is IncludersInclValue)
                        {
                            IncludersInclValue thisVal = (treeLink.Value as IncludersInclValue);
                            string thisDetailsTxt = globalVal.Name + " => " + treeLink.Includee.Name + ":\n-"
                                                  + " Max: " + Common.UIConverters.GetTimeStr(thisVal.Max)
                                                  + " Avg: " + Common.UIConverters.GetTimeStr(thisVal.Average)
                                                  + " Acc: " + Common.UIConverters.GetTimeStr(thisVal.Accumulated)
                                                  + " Units: " + thisVal.Count;

                            detailsText.Text = thisDetailsTxt + "\n" + detailsText.Text;
                        }

                    }
                    else if(treeLink.Includer is UnitValue)
                    {
                        UnitValue unitVal = treeLink.Includer as UnitValue;
                        descriptionText.Text = unitVal.Name;

                        detailsText.Text = "Unit:\n- Duration: " + Common.UIConverters.GetTimeStr(unitVal.ValuesList[(int)CompilerData.CompileCategory.ExecuteCompiler])
                                         + " (Includes: " + Common.UIConverters.GetTimeStr(unitVal.ValuesList[(int)CompilerData.CompileCategory.Include]) + ")";

                        if (treeLink.Value != null && treeLink.Value is IncludersUnitValue)
                        {
                            string thisDetailsTxt = unitVal.Name + " => " + treeLink.Includee.Name + ":\n-"
                                                  + " Duration: " + Common.UIConverters.GetTimeStr( (treeLink.Value as IncludersUnitValue).Duration );
                            detailsText.Text = thisDetailsTxt + "\n" + detailsText.Text;
                        }
                    }
                    else
                    {
                        descriptionText.Text = "-- Unknwon --";
                    }
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
