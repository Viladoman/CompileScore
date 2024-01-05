using System;
using System.Windows.Media;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    public class ThemeSettingsPageGrid : DialogPage
    {
        static private readonly Color[] kRPGColors = { (Color)ColorConverter.ConvertFromString("#40C8C8C8"),
                                                       (Color)ColorConverter.ConvertFromString("#401EFF00"),
                                                       (Color)ColorConverter.ConvertFromString("#400070DD"),
                                                       (Color)ColorConverter.ConvertFromString("#40A335EE"),
                                                       (Color)ColorConverter.ConvertFromString("#40FF8000") };


        static private readonly Color[] kFireColors = { (Color)ColorConverter.ConvertFromString("#32757575"),
                                                        (Color)ColorConverter.ConvertFromString("#25FFEE00"),
                                                        (Color)ColorConverter.ConvertFromString("#4FE69500"),
                                                        (Color)ColorConverter.ConvertFromString("#69E74300"), 
                                                        (Color)ColorConverter.ConvertFromString("#86E70000") };

        static private readonly Color[] kAquaColors = { (Color)ColorConverter.ConvertFromString("#32757575"),
                                                        (Color)ColorConverter.ConvertFromString("#2500EEFF"),
                                                        (Color)ColorConverter.ConvertFromString("#4F0095E6"),
                                                        (Color)ColorConverter.ConvertFromString("#500053E7"),
                                                        (Color)ColorConverter.ConvertFromString("#690000E7") };


        static private readonly Color[] kStrWarningColors ={ (Color)ColorConverter.ConvertFromString("#FF606060"),
                                                             (Color)ColorConverter.ConvertFromString("#FF309730"),
                                                             (Color)ColorConverter.ConvertFromString("#FF309797"),
                                                             (Color)ColorConverter.ConvertFromString("#FF979700"),
                                                             (Color)ColorConverter.ConvertFromString("#FF970000") };

        static private readonly Color[] kStrFireColors ={ (Color)ColorConverter.ConvertFromString("#FF606060"),
                                                          (Color)ColorConverter.ConvertFromString("#FF97B700"),
                                                          (Color)ColorConverter.ConvertFromString("#FF978000"),
                                                          (Color)ColorConverter.ConvertFromString("#FF975000"),
                                                          (Color)ColorConverter.ConvertFromString("#FF970000") };

        static private readonly Color[] kStrAquaColors ={ (Color)ColorConverter.ConvertFromString("#FF606060"),
                                                          (Color)ColorConverter.ConvertFromString("#FF00A7A7"),
                                                          (Color)ColorConverter.ConvertFromString("#FF0090A7"),
                                                          (Color)ColorConverter.ConvertFromString("#FF0050A7"),
                                                          (Color)ColorConverter.ConvertFromString("#FF0020A7") };



        static public Color[] SeverityColors { set; get; } = { kRPGColors[0], kRPGColors[1], kRPGColors[2], kRPGColors[3], kRPGColors[4] };

        static public Color[] StrengthColors { set; get; } = { kStrWarningColors[0], kStrWarningColors[1], kStrWarningColors[2], kStrWarningColors[3], kStrWarningColors[4] };

        public enum SeverityTheme
        {
            Custom,
            RPG,
            Fire,
            Aqua,
        }

        public enum StrengthTheme
        {
            Custom,
            Warning,
            Fire,
            Aqua,
        }

        private SeverityTheme optionSeverityTheme = SeverityTheme.RPG;
        private StrengthTheme optionStrengthTheme = StrengthTheme.Warning;

        private void ChangeSeverityColor(int slot, Color value)
        {
            bool hasChanged = !SeverityColors[slot].Equals(value);
            SeverityColors[slot] = value;

            if (hasChanged)
            {
                optionSeverityTheme = SeverityTheme.Custom;
                CompilerData.Instance.OnThemeChanged();
            }
        }

        private void ChangeStrengthColor(int slot, Color value)
        {
            bool hasChanged = !StrengthColors[slot].Equals(value);
            StrengthColors[slot] = value;

            if (hasChanged)
            {
                optionStrengthTheme = StrengthTheme.Custom;
                ParserData.Instance.OnThemeChanged();
            }
        }

        [Category("Global")]
        [DisplayName("Score Theme")]
        [Description("Set the color theme for the text highlight and glyphs based on severity")]
        public SeverityTheme OptionSeverityTheme
        {
            get { return optionSeverityTheme; }
            set
            {
                bool hasChanged = optionSeverityTheme != value;
                optionSeverityTheme = value;
                switch (value)
                {
                    case SeverityTheme.RPG: Array.Copy(kRPGColors, SeverityColors, 5); break;
                    case SeverityTheme.Fire: Array.Copy(kFireColors, SeverityColors, 5); break;
                    case SeverityTheme.Aqua: Array.Copy(kAquaColors, SeverityColors, 5); break;
                    case SeverityTheme.Custom: break;
                }

                if (hasChanged)
                {
                    CompilerData.Instance.OnThemeChanged();
                }
            }
        }

        [Category("Global")]
        [DisplayName("Requirement Strength Theme")]
        [Description("Set the color theme for the different requirement strengths")]
        public StrengthTheme OptionStrengthTheme
        {
            get { return optionStrengthTheme; }
            set
            {
                bool hasChanged = optionStrengthTheme != value;
                optionStrengthTheme = value;
                switch (value)
                {
                    case StrengthTheme.Warning: Array.Copy(kStrWarningColors, StrengthColors, 5); break;
                    case StrengthTheme.Fire:    Array.Copy(kStrFireColors, StrengthColors, 5); break;
                    case StrengthTheme.Aqua:    Array.Copy(kStrAquaColors, StrengthColors, 5); break;
                    case StrengthTheme.Custom: break;
                }

                if (hasChanged)
                {
                    ParserData.Instance.OnThemeChanged();
                }
            }
        }


        [Category("Severity")]
        [DisplayName("Score 1")]
        [Description("Color used to indicate a score/severity of level 1")]
        public Color OptionSeverityColor1
        {
            get { return SeverityColors[0]; }
            set { ChangeSeverityColor(0,value);}
        }

        [Category("Severity")]
        [DisplayName("Score 2")]
        [Description("Color used to indicate a score/severity of level 2")]
        public Color OptionSeverityColor2
        {
            get { return SeverityColors[1]; }
            set { ChangeSeverityColor(1, value); }
        }

        [Category("Severity")]
        [DisplayName("Score 3")]
        [Description("Color used to indicate a score/severity of level 3")]
        public Color OptionSeverityColor3
        {
            get { return SeverityColors[2]; }
            set { ChangeSeverityColor(2, value); }
        }

        [Category("Severity")]
        [DisplayName("Score 4")]
        [Description("Color used to indicate a score/severity of level 4")]
        public Color OptionSeverityColor4
        {
            get { return SeverityColors[3]; }
            set { ChangeSeverityColor(3, value); }
        }

        [Category("Severity")]
        [DisplayName("Score 5")]
        [Description("Color used to indicate a score/severity of level 5")]
        public Color OptionSeverityColor5
        {
            get { return SeverityColors[4]; }
            set { ChangeSeverityColor(4, value); }
        }

        [Category("Strength")]
        [DisplayName("Strength 1 (None)")]
        [Description("Color used to indicate a requirement strength of level 1")]
        public Color OptionStrengthColor1
        {
            get { return StrengthColors[0]; }
            set { ChangeStrengthColor(0, value); }
        }

        [Category("Strength")]
        [DisplayName("Strength 2 (Minimal)")]
        [Description("Color used to indicate a requirement strength of level 2")]
        public Color OptionStrengthColor2
        {
            get { return StrengthColors[1]; }
            set { ChangeStrengthColor(1, value); }
        }

        [Category("Strength")]
        [DisplayName("Strength 3 (Weak)")]
        [Description("Color used to indicate a requirement strength of level 3")]
        public Color OptionStrengthColor3
        {
            get { return StrengthColors[2]; }
            set { ChangeStrengthColor(2, value); }
        }

        [Category("Strength")]
        [DisplayName("Strength 4 (Medium)")]
        [Description("Color used to indicate a requirement strength of level 4")]
        public Color OptionStrengthColor4
        {
            get { return StrengthColors[3]; }
            set { ChangeStrengthColor(3, value); }
        }

        [Category("Strength")]
        [DisplayName("Strength 5 (Strong)")]
        [Description("Color used to indicate a requirement strength of level 5")]
        public Color OptionStrengthColor5
        {
            get { return StrengthColors[4]; }
            set { ChangeStrengthColor(4, value); }
        }

    }

}
