using CompileScore.Common;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

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

                score0.SetMoniker(severity >= 1 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score1.SetMoniker(severity >= 2 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score2.SetMoniker(severity >= 3 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score3.SetMoniker(severity >= 4 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
                score4.SetMoniker(severity >= 5 ? MonikerType.ScoreOn : MonikerType.ScoreOff);
            }
            
            scoreGrid.Visibility = value is CompileValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public static string GetGlobalsRequirementRecap(ParserFileRequirements fileReq)
        {
            string globalsTxt = "";
            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                if (ParserData.HasLinkFlag(fileReq, (ParserEnums.GlobalRequirement)i))
                {
                    globalsTxt += (globalsTxt.Length > 0 ? "," : "") + " " + RequirementLabel.GetLabel((ParserEnums.GlobalRequirement)i);
                }
            }
            return globalsTxt;
        }

        public static string GetStructureUsageRequirementRecap(ParserFileRequirements fileReq)
        {
            string structureUsageTxt = "";
            for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
            {
                if (ParserData.HasLinkFlag(fileReq, (ParserEnums.StructureSimpleRequirement)i))
                {
                    structureUsageTxt += (structureUsageTxt.Length > 0 ? "," : "") + " " + RequirementLabel.GetLabel((ParserEnums.StructureSimpleRequirement)i);
                }
            }
            return structureUsageTxt;
        }

        public static string GetStructureAccessRequirementRecap(ParserFileRequirements fileReq)
        {
            string structureAccessTxt = "";
            for (int i = 0; i < (int)ParserEnums.StructureNamedRequirement.Count; ++i)
            {
                if (ParserData.HasLinkFlag(fileReq, (ParserEnums.StructureNamedRequirement)i))
                {
                    structureAccessTxt += (structureAccessTxt.Length > 0 ? "," : "") + " " + RequirementLabel.GetLabel((ParserEnums.StructureNamedRequirement)i);
                }
            }
            return structureAccessTxt;
        }

        public static string GetSubIncludesRequirementRecap(ParserFileRequirements fileReq)
        {
            string txt = "";
            foreach(ParserFileRequirements subfile in fileReq.Includes ?? Enumerable.Empty<ParserFileRequirements>())
            {
                txt += (txt.Length > 0 ? "," : "") + " " + EditorUtils.GetFileNameSafe(subfile.Name);
            }
            return txt;
        }

        private void SetRequirements(object value)
        {
            requirementPanel.Children.Clear();
            requirementBorder.Visibility = Visibility.Collapsed;

            if ( value == null || !(value is ParserFileRequirements) )
                return;

            requirementBorder.Visibility = Visibility.Visible;

            ParserFileRequirements fileReq = value as ParserFileRequirements;

            string globalsTxt = GetGlobalsRequirementRecap(fileReq);
            if (globalsTxt.Length > 0)
            {
                TextBlock txtblock = new TextBlock();
                txtblock.Inlines.Add(new Run("Globals:") { TextDecorations = TextDecorations.Underline });
                txtblock.Inlines.Add(globalsTxt);
                requirementPanel.Children.Add(txtblock);

            }

            string structureUsageTxt = GetStructureUsageRequirementRecap(fileReq);
            if (structureUsageTxt.Length > 0)
            {
                TextBlock txtblock = new TextBlock();
                txtblock.Inlines.Add(new Run("Structure Usage:") { TextDecorations = TextDecorations.Underline });
                txtblock.Inlines.Add(structureUsageTxt);
                requirementPanel.Children.Add(txtblock);
            }

            string structureAccessTxt = GetStructureAccessRequirementRecap(fileReq);
            if (structureAccessTxt.Length > 0)
            {
                TextBlock txtblock = new TextBlock();
                txtblock.Inlines.Add(new Run("Structure Access:") { TextDecorations = TextDecorations.Underline });
                txtblock.Inlines.Add(structureAccessTxt);
                requirementPanel.Children.Add(txtblock);
            }

            if ( requirementPanel.Children.Count == 0 ) 
            {
                requirementPanel.Children.Add(new TextBlock(){ Text = "-- No Direct Requirements Found --" });
            }

            //set Strength
            TextBlock strengthText = new TextBlock();
            strengthText.Inlines.Add(new Run("Requirement Strength:") { TextDecorations = TextDecorations.Underline });
            strengthText.Inlines.Add(new Run(" " + BasicUILabel.GetLabel(fileReq.Strength)) { FontWeight = FontWeights.Bold });
            requirementPanel.Children.Insert(0, strengthText);
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
                SetRequirements(root.Value);
            }
            else if (node is RequirementGraphNode)
            {
                RequirementGraphNode graphNode = node as RequirementGraphNode;
                headerText.Text = graphNode.Label;
                descriptionText.Text = graphNode.Value.Name;
                detailsText.Text = Timeline.TimelineNodeTooltip.GetDetailsText(graphNode.ProfilerValue, graphNode.IncluderValue, RootNode == null ? "??" : RootNode.Label);
                SetScore(graphNode.ProfilerValue);
                SetRequirements(graphNode.Value);
            }

            inclusionBucketText.Text = RequirementsDetails.GetInclusionBucketText(node);

            inclusionBucketText.Visibility = inclusionBucketText.Text == null ? Visibility.Collapsed : Visibility.Visible;  
            profilerGrid.Visibility = detailsText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            profilerBorder.Visibility = detailsText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            //TODO ~ ramonv ~ add requirements info 

        }
    }
}
