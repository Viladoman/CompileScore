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

        static public void AddThemeToApplicationResources()
        {
            ResourceDictionary resources = new ResourceDictionary();

            resources["Background"]                  = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            resources["Foreground"]                  = new SolidColorBrush(System.Windows.Media.Colors.White);

            resources["ProgressBar_Background"]      = new SolidColorBrush(System.Windows.Media.Colors.Gray);

            resources["Grid_Line"]                   = new SolidColorBrush(System.Windows.Media.Colors.White);
            resources["Grid_HeaderBackground"]       = new SolidColorBrush(System.Windows.Media.Colors.Black);
            resources["Grid_HeaderForeground"]       = new SolidColorBrush(System.Windows.Media.Colors.White);
            resources["Grid_HeaderSeparator"]        = new SolidColorBrush(System.Windows.Media.Colors.White);
            resources["Grid_HeaderArrow"]            = new SolidColorBrush(System.Windows.Media.Colors.Yellow);
            resources["Grid_CellSelectedBackground"] = new SolidColorBrush(System.Windows.Media.Colors.DarkBlue);

            resources["TabItem_Background"]          = new SolidColorBrush(System.Windows.Media.Colors.DarkSlateGray);
            resources["TabItem_Foreground"]          = new SolidColorBrush(System.Windows.Media.Colors.White);
            resources["TabItem_SelectedBackground"]  = new SolidColorBrush(System.Windows.Media.Colors.DarkBlue);
            resources["TabItem_SelectedForeground"]  = new SolidColorBrush(System.Windows.Media.Colors.White);
            resources["TabItem_MouseOverBackground"] = new SolidColorBrush(System.Windows.Media.Colors.DarkGray);
            resources["TabItem_MouseOverForeground"] = new SolidColorBrush(System.Windows.Media.Colors.Blue);

            Application.Current.Resources.MergedDictionaries.Add(resources);


        }
    }
}
