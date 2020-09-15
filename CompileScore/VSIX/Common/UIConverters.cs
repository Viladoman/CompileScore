namespace CompileScore.Common
{
    using System;
    using System.Text.RegularExpressions;
    using System.Windows.Data;

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
