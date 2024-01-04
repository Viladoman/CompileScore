using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GelUtilities = Microsoft.Internal.VisualStudio.PlatformUI.Utilities;

namespace CompileScore.Common
{
    public enum MonikerType
    {
        ScoreOff = 0,
        ScoreOn,

        Count
    }

    class MonikerProxy : Grid
    {
        private class BitmapMoniker
        {
            public Bitmap Image { set; get; }
            public BitmapSource Source { set; get; }
        }

        private CrispImage Shape { get; set; } = new CrispImage();

        public static int Dpi { get; set; } = 1;

        private static BitmapMoniker[] Bitmaps { set; get; } = new BitmapMoniker[(int)MonikerType.Count];

        public double MonikerSize
        {
            get { return Math.Max(Shape.Width, Shape.Height); }
            set { Shape.Height = value; Shape.Width = value; }
        }

        public static ImageMoniker GetMoniker(MonikerType type)
        {
            switch (type)
            {
                case MonikerType.ScoreOff: return KnownMonikers.Small; 
                case MonikerType.ScoreOn: return KnownMonikers.HotSpot; 
                default: return KnownMonikers.QuestionMark; 
            }
        }

        public void SetMoniker(MonikerType type)
        {
            Shape.Moniker = GetMoniker(type);

            //Add as children if we haven't done so yet!
            if (Children.Count == 0)
            {
                Children.Add(Shape);
            }
        }

        public static void DrawTo(DrawingContext drawingContext, Rect rect, MonikerType type)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //create if not already created
            if (Bitmaps[(int)type] == null)
            {
                //load bitmap
                Bitmap b = CreateMonikerBitmap(GetMoniker(type));
                BitmapSource source = CreateBitmapSourceFromGdiBitmap(b);

                if (source != null)
                {
                    Bitmaps[(int)type] = new BitmapMoniker() { Image = b, Source = source };
                }
            }

            BitmapMoniker bitmapMoniker = Bitmaps[(int)type];

            if (bitmapMoniker == null || bitmapMoniker.Source == null || bitmapMoniker.Image == null)
                return;

            //Perform draw
            drawingContext.DrawImage(bitmapMoniker.Source, rect);
        }

        private static Bitmap CreateMonikerBitmap( ImageMoniker moniker )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsImageService2 imageService = (IVsImageService2)(Package.GetGlobalService(typeof(SVsImageService)));

            ImageAttributes attributes = new ImageAttributes
            {
                StructSize = Marshal.SizeOf(typeof(ImageAttributes)),
                ImageType = (uint)_UIImageType.IT_Bitmap,
                Format = (uint)_UIDataFormat.DF_WinForms,
                LogicalWidth = 16,
                LogicalHeight = 16,
                Dpi = Dpi,
                Flags = unchecked((uint)_ImageAttributesFlags.IAF_RequiredFlags)
            };

            IVsUIObject uIObj = imageService.GetImage(moniker, attributes);
            return (Bitmap)GelUtilities.GetObjectData(uIObj);
        }

        private static BitmapSource CreateBitmapSourceFromGdiBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var bitmapData = bitmap.LockBits(
                rect,
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                var size = (rect.Width * rect.Height) * 4;

                return BitmapSource.Create(
                    bitmap.Width,
                    bitmap.Height,
                    bitmap.HorizontalResolution,
                    bitmap.VerticalResolution,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    bitmapData.Scan0,
                    size,
                    bitmapData.Stride);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }

}
