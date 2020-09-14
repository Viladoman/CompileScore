namespace CompileScore.Common
{
    using System;
    using System.Text.RegularExpressions;
    using System.Windows.Data;
    using System.Windows.Media;

    class UIColors
    {
        static Brush IncludeBrush              = new SolidColorBrush(Color.FromArgb(255, 85,  0,   85));
        static Brush ParseClassBrush           = new SolidColorBrush(Color.FromArgb(255, 170, 115, 0));
        static Brush ParseTemplateBrush        = new SolidColorBrush(Color.FromArgb(255, 170, 115, 51));
        static Brush InstantiateClassBrush     = new SolidColorBrush(Color.FromArgb(255, 0,   119, 0));
        static Brush InstantiateFuncBrush      = new SolidColorBrush(Color.FromArgb(255, 0,   119, 51));
        static Brush CodeGenBrush              = new SolidColorBrush(Color.FromArgb(255, 3,   71,  54));
        static Brush PendingInstantiationBrush = new SolidColorBrush(Color.FromArgb(255, 0,   0,   119));
        static Brush OptModuleBrush            = new SolidColorBrush(Color.FromArgb(255, 119, 51,  17));
        static Brush OptFunctionBrush          = new SolidColorBrush(Color.FromArgb(255, 45,  66,  98));
        static Brush RunPassBrush              = new SolidColorBrush(Color.FromArgb(255, 0,   85,  85));
        static Brush FrontEndBrush             = new SolidColorBrush(Color.FromArgb(255, 136, 136, 0));
        static Brush BackEndBrush              = new SolidColorBrush(Color.FromArgb(255, 136, 81,  0));
        static Brush ExecuteCompilerBrush      = new SolidColorBrush(Color.FromArgb(255, 51,  119, 102));
        static Brush OtherBrush                = new SolidColorBrush(Color.FromArgb(255, 119, 0,   0));
        
        static public Brush GetCategoryBackground(CompilerData.CompileCategory category)
        {
            switch (category)
            {
                case CompilerData.CompileCategory.Include:               return IncludeBrush;
                case CompilerData.CompileCategory.ParseClass:            return ParseClassBrush;
                case CompilerData.CompileCategory.ParseTemplate:         return ParseTemplateBrush;
                case CompilerData.CompileCategory.InstanceClass:         return InstantiateClassBrush;
                case CompilerData.CompileCategory.InstanceFunction:      return InstantiateFuncBrush;
                case CompilerData.CompileCategory.CodeGeneration:        return CodeGenBrush;
                case CompilerData.CompileCategory.PendingInstantiations: return PendingInstantiationBrush;
                case CompilerData.CompileCategory.OptimizeModule:        return OptModuleBrush;
                case CompilerData.CompileCategory.OptimizeFunction:      return OptFunctionBrush;
                case CompilerData.CompileCategory.RunPass:               return RunPassBrush;
                case CompilerData.CompileCategory.FrontEnd:              return FrontEndBrush;
                case CompilerData.CompileCategory.BackEnd:               return BackEndBrush;
                case CompilerData.CompileCategory.ExecuteCompiler:       return ExecuteCompilerBrush;
                case CompilerData.CompileCategory.Other:                 return OtherBrush;  
            }

            return OtherBrush; 
        }

        static public Brush GetCategoryForeground()
        {
            return Brushes.White;
        }
    }

    class UIConverters
    {
      
        static public string ToSentenceCase(string str)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {m.Value[1]}");
        }
        static public string GetTimeStr(ulong uSeconds)
        {
            ulong ms = uSeconds / 1000;
            ulong us = uSeconds - (ms * 1000);
            ulong sec = ms / 1000;
            ms = ms - (sec * 1000);
            ulong min = sec / 60;
            sec = sec - (min * 60);
            ulong hour = min / 60;
            min = min - (hour * 60);

            if (hour > 0) { return hour + " h " + min + " m"; }
            if (min > 0)  { return min  + " m " + sec + " s"; }
            if (sec > 0)  { return sec  + "." + ms.ToString().PadLeft(4, '0') + " s"; }
            if (ms > 0)   { return ms + "." + us.ToString().PadLeft(4, '0')+" ms"; }
            if (us > 0)   { return us + " μs"; }
            return "-";
        }
    }

    public class UITimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try 
            {
                return value is uint? UIConverters.GetTimeStr((uint)value) : UIConverters.GetTimeStr((ulong)value);
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
