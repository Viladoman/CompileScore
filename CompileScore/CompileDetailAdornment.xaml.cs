using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CompileScore
{
    /// <summary>
    /// Interaction logic for CompileDetailAdornment.xaml
    /// </summary>
    public partial class CompileDetailAdornment : UserControl
    {
        public CompileDetailAdornment(HighlightWordTag markupTag)
        {
            InitializeComponent();
            SetPopupPlacementCallbacks();

            PopupText.Text = BuildDescription(markupTag);
        }
        
        private string GetDescriptionTime(uint ms)
        {
            if (ms == 0)
            {
                return @"<1ms";
            }

            if (ms > 1000)
            {
                uint sec = ms / 1000;
                uint min = sec / 60;
                sec = sec - (min * 60);

                return min == 0 ? sec + "s" : min + "m" + sec + "s";
            }

            return ms + "ms";
        }

        private string BuildDescription(HighlightWordTag markupTag)
        {
            return "Source Include: Avg " + GetDescriptionTime(markupTag.Value.Mean) + " - Min " + GetDescriptionTime(markupTag.Value.Min) + " - Max " + GetDescriptionTime(markupTag.Value.Max) + " - Count: " + markupTag.Value.Count;
        }

        private void SetPopupPlacementCallbacks()
        {
            HoverPopup.CustomPopupPlacementCallback += (popupSize, targetSize, offset) => PopupPlacement.PlacePopup(popupSize, targetSize, offset, VerticalPlacement.Bottom, HorizontalPlacement.Center);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            HoverPopup.IsOpen = true;            
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            ClosePopupIfNecessary();            
        }

        private void ClosePopupIfNecessary()
        {
            HoverPopup.IsOpen = ExpandLabel.IsMouseOver || HoverPopup.IsMouseOver;
        }

        private void OnMouseLeavePopup(object sender, MouseEventArgs e)
        {
            ClosePopupIfNecessary();
        }
    }
}
