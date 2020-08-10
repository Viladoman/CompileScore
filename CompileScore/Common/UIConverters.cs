using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CompileScore.Common
{ 
    class UIConverters
    { 
        static public string ToSentenceCase(string str)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {m.Value[1]}");
        }

        static public string GetTimeStr(uint uSeconds)
        {
            //TODO ~ ramonv ~ improve the viewer 

            if (uSeconds == 0)
            {
                return "-";
            }

            if (uSeconds < 1000)
            {
                return uSeconds + "μs";
            }

            uint ms = uSeconds / 1000;
            uint sec = ms / 1000;
            ms = ms - (sec * 1000);
            uint min = sec / 60;
            sec = sec - (min * 60);
            uint hour = min / 60;
            min = min - (hour * 60);

            string ret = ms + "ms";
            ret = (sec > 0 ?  sec  + "s " : "") + ret;
            ret = (min > 0 ?  min  + "m " : "") + ret;
            ret = (hour > 0 ? hour + "h " : "") + ret;          

            return ret;
        }
    }

    public class UITimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try 
            {
                return UIConverters.GetTimeStr((uint)value);
            }
            catch (Exception)
            {
                return "ERROR";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
