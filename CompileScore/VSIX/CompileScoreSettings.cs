
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    public class GeneralSettingsPageGrid : DialogPage
    {
        public enum HighlightMode
        {
            Disabled,
            Simple, 
            Full,
        }

        public enum SeverityCriteria
        {
            //This names need to match the function names of CompileValue
            Max,
            Min,
            Average,
            Count,
        }

        private SeverityCriteria optionSeverityCriteria = SeverityCriteria.Max;
        private bool optionNormalizedSeverity = true;
        private HighlightMode optionHighlightMode = HighlightMode.Full;
        private List<uint> optionSeverities = new List<uint> { 250u, 1000u, 25000u, 100000u, 500000u };

        public List<uint> GetOptionSeverities() { return optionSeverities; }

        [Category("Display")]
        [DisplayName("Text Highlight")]
        [Description("Select the highlight mode for the text editor")]
        public HighlightMode OptionHighlightMode 
        {
            get { return optionHighlightMode; }
            set { bool hasChanged = optionHighlightMode != value; optionHighlightMode = value; if (hasChanged) { CompilerData.Instance.OnHighlightModeChanged(); } }
        }

        [Category("Display")]
        [DisplayName("Tooltip")]
        [Description("If true, a tooltip will show up when hovering with the mouse")]
        public bool OptionTooltipEnabled { set; get; } = true;

        [Category("Tags")]
        [DisplayName("Severity Criteria")]
        [Description("Select the data used to sort and define the severity levels")]
        public SeverityCriteria OptionSeverityCriteria
        {
            get { return optionSeverityCriteria; }
            set { bool hasChanged = optionSeverityCriteria != value; optionSeverityCriteria = value; if (hasChanged) { CompilerData.Instance.OnSettingsSeverityCriteriaChanged(); } }
        }

        [Category("Tags")]
        [DisplayName("Normalized Severity")]
        [Description("If true, the severity levels will be defined based on the min-max found")]
        public bool OptionNormalizedSeverity
        {
            get { return optionNormalizedSeverity; }
            set { bool hasChanged = optionNormalizedSeverity != value; optionNormalizedSeverity = value; if (hasChanged) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 1")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold1
        {
            get { return optionSeverities[0]; }
            set { bool hasChanged = optionSeverities[0] != value;  optionSeverities[0] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged();  } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 2")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold2
        {
            get { return optionSeverities[1]; }
            set { bool hasChanged = optionSeverities[1] != value; optionSeverities[1] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 3")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold3
        {
            get { return optionSeverities[2]; }
            set { bool hasChanged = optionSeverities[2] != value; optionSeverities[2] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 4")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold4
        {
            get { return optionSeverities[3]; }
            set { bool hasChanged = optionSeverities[3] != value; optionSeverities[3] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 5")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold5
        {
            get { return optionSeverities[4]; }
            set { bool hasChanged = optionSeverities[4] != value; optionSeverities[4] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }
    }

}
