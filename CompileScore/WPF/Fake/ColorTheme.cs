using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CompileScore.Common
{
    static public class ColorTheme
    {
        public static readonly object Background                  = "Background";
        public static readonly object Foreground                  = "Foreground";

        public static readonly object ProgressBar_Background      = "ProgressBar_Background";
        
        public static readonly object Grid_Line                   = "Grid_Line";
        public static readonly object Grid_HeaderBackground       = "Grid_HeaderBackground";
        public static readonly object Grid_HeaderForeground       = "Grid_HeaderForeground";
        public static readonly object Grid_HeaderSeparator        = "Grid_HeaderSeparator";
        public static readonly object Grid_HeaderArrow            = "Grid_HeaderArrow";
        public static readonly object Grid_CellSelectedBackground = "Grid_CellSelectedBackground";

        public static readonly object TabItem_Background          = "TabItem_Background";
        public static readonly object TabItem_Foreground          = "TabItem_Foreground";
        public static readonly object TabItem_SelectedBackground  = "TabItem_SelectedBackground";
        public static readonly object TabItem_SelectedForeground  = "TabItem_SelectedForeground";
        public static readonly object TabItem_MouseOverBackground = "TabItem_MouseOverBackground";
        public static readonly object TabItem_MouseOverForeground = "TabItem_MouseOverForeground";
        
        public static readonly object Slider_Background = "Slider_Background";
        public static readonly object Slider_Foreground = "Slider_Foreground";

        static public void AddThemeToApplicationResources()
        {
            ResourceDictionary resources = new ResourceDictionary();

            resources["Background"]                  = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            resources["Foreground"]                  = new SolidColorBrush(Color.FromRgb(241, 241, 241));

            resources["ProgressBar_Background"]      = new SolidColorBrush(Color.FromRgb(37, 37, 38));

            resources["Grid_Line"]                   = new SolidColorBrush(Color.FromRgb(0,   0,   0));
            resources["Grid_HeaderBackground"]       = new SolidColorBrush(Color.FromRgb(45,  45,  48));
            resources["Grid_HeaderForeground"]       = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            resources["Grid_HeaderSeparator"]        = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            resources["Grid_HeaderArrow"]            = new SolidColorBrush(Color.FromRgb(51,  153, 255));
            resources["Grid_CellSelectedBackground"] = new SolidColorBrush(Color.FromRgb(51,  153, 255));

            resources["TabItem_Background"]          = new SolidColorBrush(Color.FromRgb(45,  45,  48));
            resources["TabItem_Foreground"]          = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            resources["TabItem_SelectedBackground"]  = new SolidColorBrush(Color.FromRgb(37,  37,  38));
            resources["TabItem_SelectedForeground"]  = new SolidColorBrush(Color.FromRgb(14,  151, 221));
            resources["TabItem_MouseOverBackground"] = new SolidColorBrush(Color.FromRgb(62,  62,  64));
            resources["TabItem_MouseOverForeground"] = new SolidColorBrush(Color.FromRgb(77,  170, 228));

            resources["Slider_Background"]           = new SolidColorBrush(Color.FromRgb(62,  62,  66));
            resources["Slider_Foreground"]           = new SolidColorBrush(Color.FromRgb(104, 104, 104));

            Application.Current.Resources.MergedDictionaries.Add(resources);
        }
    }
}
