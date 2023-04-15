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

        static public Color[] SeverityColors { set; get; } = { kRPGColors[0], kRPGColors[1], kRPGColors[2], kRPGColors[3], kRPGColors[4] };


        public enum SeverityTheme
        {
            Custom,
            RPG,
            Fire,
            Aqua,
        }

        private SeverityTheme optionSeverityTheme = SeverityTheme.RPG;

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
    }

}
