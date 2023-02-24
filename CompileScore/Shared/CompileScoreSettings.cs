
using System;
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
        private List<uint> optionValueSeverities = new List<uint> { 250u, 1000u, 25000u, 100000u };
        private List<float> optionNormalizedSeverities = new List<float> { 50.0f, 75.0f, 90.0f, 98.0f };

        public List<uint> GetOptionValueSeverities() { return optionValueSeverities; }
        public List<float> GetOptionNormalizedSeverities() { return optionNormalizedSeverities; }

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
            get { return optionValueSeverities[0]; }
            set { bool hasChanged = optionValueSeverities[0] != value; optionValueSeverities[0] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged();  } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 2")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold2
        {
            get { return optionValueSeverities[1]; }
            set { bool hasChanged = optionValueSeverities[1] != value; optionValueSeverities[1] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 3")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category.")]
        public uint OptionSeveritiesThreshold3
        {
            get { return optionValueSeverities[2]; }
            set { bool hasChanged = optionValueSeverities[2] != value; optionValueSeverities[2] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Absolute")]
        [DisplayName("Severity 4")]
        [Description("For non normalized severity this defines the maximum value in microseconds(μs) to be considered for this category. Severity 5 will be anything bigger than this number")]
        public uint OptionSeveritiesThreshold4
        {
            get { return optionValueSeverities[3]; }
            set { bool hasChanged = optionValueSeverities[3] != value; optionValueSeverities[3] = value; if (hasChanged && !optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityChanged(); } }
        }

        [Category("Thresholds Normalized")]
        [DisplayName("Severity 1")]
        [Description("For normalized severity this defines the maximum percentage [1..100] to be considered for this category.")]
        public float OptionSeveritiesNormalized1
        {
            get { return optionNormalizedSeverities[0]; }
            set { bool hasChanged = optionNormalizedSeverities[0] != value; optionNormalizedSeverities[0] = Math.Max(Math.Min(value, 100.0f), 0.0f); if (hasChanged && optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityCriteriaChanged(); } }
        }

        [Category("Thresholds Normalized")]
        [DisplayName("Severity 2")]
        [Description("For normalized severity this defines the maximum percentage [1..100] to be considered for this category.")]
        public float OptionSeveritiesNormalized2
        {
            get { return optionNormalizedSeverities[1]; }
            set { bool hasChanged = optionNormalizedSeverities[1] != value; optionNormalizedSeverities[1] = Math.Max(Math.Min(value, 100.0f), 0.0f); if (hasChanged && optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityCriteriaChanged(); } }
        }

        [Category("Thresholds Normalized")]
        [DisplayName("Severity 3")]
        [Description("For normalized severity this defines the maximum percentage [1..100] to be considered for this category.")]
        public float OptionSeveritiesNormalized3
        {
            get { return optionNormalizedSeverities[2]; }
            set { bool hasChanged = optionNormalizedSeverities[2] != value; optionNormalizedSeverities[2] = Math.Max(Math.Min(value, 100.0f), 0.0f); if (hasChanged && optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityCriteriaChanged(); } }
        }

        [Category("Thresholds Normalized")]
        [DisplayName("Severity 4")]
        [Description("For normalized severity this defines the maximum percentage [1..100] to be considered for this category. Severity 5 will be anything bigger than this number")]
        public float OptionSeveritiesNormalized4
        {
            get { return optionNormalizedSeverities[3]; }
            set { bool hasChanged = optionNormalizedSeverities[3] != value; optionNormalizedSeverities[3] = Math.Max(Math.Min(value,100.0f),0.0f); if (hasChanged && optionNormalizedSeverity) { CompilerData.Instance.OnSettingsSeverityCriteriaChanged(); } }
        }
    }

}
