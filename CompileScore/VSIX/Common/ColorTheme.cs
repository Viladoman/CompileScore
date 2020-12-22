using Microsoft.VisualStudio.Shell;

namespace CompileScore.Common
{
    static public class ColorTheme
    {
        public static readonly object Background                  = VsBrushes.WindowKey;
        public static readonly object Foreground                  = VsBrushes.WindowTextKey;

        public static readonly object ProgressBar_Background      = VsBrushes.WindowKey;
        
        public static readonly object Grid_Line                   = VsBrushes.GridLineKey;
        public static readonly object Grid_HeaderBackground       = VsBrushes.GridHeadingBackgroundKey;
        public static readonly object Grid_HeaderForeground       = VsBrushes.GridHeadingTextKey;
        public static readonly object Grid_HeaderSeparator        = VsBrushes.GridHeadingTextKey;
        public static readonly object Grid_HeaderArrow            = VsBrushes.CommandBarSelectedBorderKey;
        public static readonly object Grid_CellSelectedBackground = VsBrushes.CommandBarSelectedBorderKey;

        public static readonly object TabItem_Background          = VsBrushes.AutoHideTabBackgroundBeginKey;
        public static readonly object TabItem_Foreground          = VsBrushes.AutoHideTabTextKey;
        public static readonly object TabItem_SelectedBackground  = VsBrushes.ToolWindowTabSelectedTabKey;
        public static readonly object TabItem_SelectedForeground  = VsBrushes.ToolWindowTabSelectedTextKey;
        public static readonly object TabItem_MouseOverBackground = VsBrushes.ToolWindowTabMouseOverBackgroundBeginKey;
        public static readonly object TabItem_MouseOverForeground = VsBrushes.ToolWindowTabMouseOverTextKey;

        public static readonly object ComboBox_MouseOverBackground = VsBrushes.ToolWindowTabMouseOverBackgroundBeginKey;
        public static readonly object ComboBox_MouseOverForeground = VsBrushes.ToolWindowTabMouseOverTextKey;
    }
}
