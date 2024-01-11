using System.Windows.Media;

namespace CompileScore.Common
{
    class Colors
    {
        public static Brush IncludeBrush              = new SolidColorBrush(Color.FromRgb(85,  0,   85));
        public static Brush ParseClassBrush           = new SolidColorBrush(Color.FromRgb(170, 115, 0));
        public static Brush ParseTemplateBrush        = new SolidColorBrush(Color.FromRgb(170, 115, 51));
        public static Brush InstantiateClassBrush     = new SolidColorBrush(Color.FromRgb(0,   119, 0));
        public static Brush InstantiateFuncBrush      = new SolidColorBrush(Color.FromRgb(0,   119, 51));
        public static Brush InstantiateVariableBrush  = new SolidColorBrush(Color.FromRgb(51,  119, 0));
        public static Brush InstantiateConceptBrush   = new SolidColorBrush(Color.FromRgb(51,  119, 51));
        public static Brush CodeGenBrush              = new SolidColorBrush(Color.FromRgb(3,   71,  54));
        public static Brush PendingInstantiationBrush = new SolidColorBrush(Color.FromRgb(0,   0,   119));
        public static Brush OptModuleBrush            = new SolidColorBrush(Color.FromRgb(119, 51,  17));
        public static Brush OptFunctionBrush          = new SolidColorBrush(Color.FromRgb(45,  66,  98));
        public static Brush RunPassBrush              = new SolidColorBrush(Color.FromRgb(0,   85,  85));
        public static Brush FrontEndBrush             = new SolidColorBrush(Color.FromRgb(136, 136, 0));
        public static Brush BackEndBrush              = new SolidColorBrush(Color.FromRgb(136, 81,  0));
        public static Brush ExecuteCompilerBrush      = new SolidColorBrush(Color.FromRgb(51,  119, 102));
        public static Brush OtherBrush                = new SolidColorBrush(Color.FromRgb(119, 0,   0));
        public static Brush ThreadBrush               = new SolidColorBrush(Color.FromRgb(75,  75,  75));
        public static Brush TimelineBrush             = new SolidColorBrush(Color.FromRgb(51,  51,  51));

        static public Brush GetCategoryBackground(CompilerData.CompileCategory category)
        {
            switch (category)
            {
                case CompilerData.CompileCategory.Include:               return IncludeBrush;
                case CompilerData.CompileCategory.ParseClass:            return ParseClassBrush;
                case CompilerData.CompileCategory.ParseTemplate:         return ParseTemplateBrush;
                case CompilerData.CompileCategory.InstanceClass:         return InstantiateClassBrush;
                case CompilerData.CompileCategory.InstanceFunction:      return InstantiateFuncBrush;
                case CompilerData.CompileCategory.InstanceVariable:      return InstantiateVariableBrush;
                case CompilerData.CompileCategory.InstanceConcept:       return InstantiateConceptBrush;
                case CompilerData.CompileCategory.CodeGeneration:        return CodeGenBrush;
                case CompilerData.CompileCategory.PendingInstantiations: return PendingInstantiationBrush;
                case CompilerData.CompileCategory.OptimizeModule:        return OptModuleBrush;
                case CompilerData.CompileCategory.OptimizeFunction:      return OptFunctionBrush;
                case CompilerData.CompileCategory.RunPass:               return RunPassBrush;
                case CompilerData.CompileCategory.CodeGenPasses:         return CodeGenBrush; //repeated color
                case CompilerData.CompileCategory.PerFunctionPasses:     return RunPassBrush; //repeated color
                case CompilerData.CompileCategory.PerModulePasses:       return RunPassBrush; //repeated color
                case CompilerData.CompileCategory.FrontEnd:              return FrontEndBrush;
                case CompilerData.CompileCategory.BackEnd:               return BackEndBrush;
                case CompilerData.CompileCategory.ExecuteCompiler:       return ExecuteCompilerBrush;
                case CompilerData.CompileCategory.Other:                 return OtherBrush;
                case CompilerData.CompileCategory.Thread:                return ThreadBrush;
                case CompilerData.CompileCategory.Timeline:              return TimelineBrush;
            }

            return OtherBrush;
        }

        static public Brush GetCategoryForeground()
        {
            return Brushes.White;
        }

        public static Color GetSeverityColor(uint severity)
        {
            int severityIndex = ((int)severity) - 1;
            if (severityIndex >= 0 && severityIndex < ThemeSettingsPageGrid.SeverityColors.Length)
            {
                return ThemeSettingsPageGrid.SeverityColors[severityIndex];
            }

            return Color.FromArgb((byte)255, (byte)0, (byte)0, (byte)0);
        }

        public static Brush GetSeverityBrush(uint severity)
        {
            return new SolidColorBrush(GetSeverityColor(severity));
        }

        public static Brush GetRequirementStrengthBrush(ParserEnums.LinkStrength strength)
        {
            int strengthIndex = ((int)strength);
            if (strengthIndex >= 0 && strengthIndex < ThemeSettingsPageGrid.StrengthColors.Length)
            {
                return new SolidColorBrush(ThemeSettingsPageGrid.StrengthColors[strengthIndex]);
            }
            return new SolidColorBrush(Color.FromArgb((byte)255, (byte)0, (byte)0, (byte)0));
        }

    }
}
