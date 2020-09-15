namespace CompileScore.Common
{
    using System.Windows.Media;

    class Colors
    {
        static Brush IncludeBrush = new SolidColorBrush(Color.FromArgb(255, 85, 0, 85));
        static Brush ParseClassBrush = new SolidColorBrush(Color.FromArgb(255, 170, 115, 0));
        static Brush ParseTemplateBrush = new SolidColorBrush(Color.FromArgb(255, 170, 115, 51));
        static Brush InstantiateClassBrush = new SolidColorBrush(Color.FromArgb(255, 0, 119, 0));
        static Brush InstantiateFuncBrush = new SolidColorBrush(Color.FromArgb(255, 0, 119, 51));
        static Brush CodeGenBrush = new SolidColorBrush(Color.FromArgb(255, 3, 71, 54));
        static Brush PendingInstantiationBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 119));
        static Brush OptModuleBrush = new SolidColorBrush(Color.FromArgb(255, 119, 51, 17));
        static Brush OptFunctionBrush = new SolidColorBrush(Color.FromArgb(255, 45, 66, 98));
        static Brush RunPassBrush = new SolidColorBrush(Color.FromArgb(255, 0, 85, 85));
        static Brush FrontEndBrush = new SolidColorBrush(Color.FromArgb(255, 136, 136, 0));
        static Brush BackEndBrush = new SolidColorBrush(Color.FromArgb(255, 136, 81, 0));
        static Brush ExecuteCompilerBrush = new SolidColorBrush(Color.FromArgb(255, 51, 119, 102));
        static Brush OtherBrush = new SolidColorBrush(Color.FromArgb(255, 119, 0, 0));

        static public Brush GetCategoryBackground(CompilerData.CompileCategory category)
        {
            switch (category)
            {
                case CompilerData.CompileCategory.Include: return IncludeBrush;
                case CompilerData.CompileCategory.ParseClass: return ParseClassBrush;
                case CompilerData.CompileCategory.ParseTemplate: return ParseTemplateBrush;
                case CompilerData.CompileCategory.InstanceClass: return InstantiateClassBrush;
                case CompilerData.CompileCategory.InstanceFunction: return InstantiateFuncBrush;
                case CompilerData.CompileCategory.CodeGeneration: return CodeGenBrush;
                case CompilerData.CompileCategory.PendingInstantiations: return PendingInstantiationBrush;
                case CompilerData.CompileCategory.OptimizeModule: return OptModuleBrush;
                case CompilerData.CompileCategory.OptimizeFunction: return OptFunctionBrush;
                case CompilerData.CompileCategory.RunPass: return RunPassBrush;
                case CompilerData.CompileCategory.CodeGenPasses: return CodeGenBrush; //repeated color
                case CompilerData.CompileCategory.PerModulePasses: return RunPassBrush; //repeated color
                case CompilerData.CompileCategory.DebugType: return OtherBrush; //repeated color
                case CompilerData.CompileCategory.DebugGlobalVariable: return OtherBrush; //repeated color
                case CompilerData.CompileCategory.FrontEnd: return FrontEndBrush;
                case CompilerData.CompileCategory.BackEnd: return BackEndBrush;
                case CompilerData.CompileCategory.ExecuteCompiler: return ExecuteCompilerBrush;
                case CompilerData.CompileCategory.Other: return OtherBrush;
            }

            return OtherBrush;
        }

        static public Brush GetCategoryForeground()
        {
            return Brushes.White;
        }

        public static Brush GetSeverityBrush(uint severity)
        {
            switch (severity)
            {
                case 1: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)200, (byte)200, (byte)200));
                case 2: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)30,  (byte)255, (byte)0));
                case 3: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)0,   (byte)112, (byte)221));
                case 4: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)163, (byte)53,  (byte)238));
                case 5: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)255, (byte)128, (byte)0)); 
            }

            return new SolidColorBrush(Color.FromArgb((byte)255, (byte)0, (byte)0, (byte)0));
        }

    }
}
