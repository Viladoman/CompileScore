
namespace CompileScore
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.VisualStudio.Shell;

    public class GeneralSettingsPageGrid : DialogPage
    {
        private string optionPath = @"";
        private string optionScoreFileName = @"compileData.scor";
        private bool optionNormalizedSeverity = true;
        private bool optionHighlightEnabled = true;
        private bool optionTooltipEnabled = true; 
        private List<uint> optionSeverities = new List<uint> { 250u, 1000u, 25000u, 100000u, 500000u };

        public List<uint> GetOptionSeverities() { return optionSeverities; }

        [Category("File")]
        [DisplayName("Input Path")]
        [Description("Path to where the input data is located (relative to SolutionDir)")]
        public string OptionPath
        {
            get { return optionPath; }
            set { optionPath = value; ThreadHelper.ThrowIfNotOnUIThread(); CompilerData.Instance.OnSettingsPathChanged(); }
        }

        [Category("File")]
        [DisplayName("Score Filename")]
        [Description("Filename that contains the compilation data")]
        public string OptionScoreFileName
        {
            get { return optionScoreFileName; }
            set { optionScoreFileName = value; ThreadHelper.ThrowIfNotOnUIThread(); CompilerData.Instance.OnSettingsScoreFileNameChanged(); }
        }

        [Category("Display")]
        [DisplayName("Text Highlight")]
        [Description("If true, the text will be highlighted with the severity color")]
        public bool OptionHighlightEnabled
        {
            get { return optionHighlightEnabled; }
            set { bool hasChanged = optionHighlightEnabled != value; optionHighlightEnabled = value; if (hasChanged) { CompilerData.Instance.OnHighlightEnabledChanged(); } }
        }

        [Category("Display")]
        [DisplayName("Tooltip")]
        [Description("If true, a tooltip will show up when hovering with the mouse")]
        public bool OptionTooltipEnabled
        {
            get { return optionTooltipEnabled; }
            set { optionTooltipEnabled = value; }
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
