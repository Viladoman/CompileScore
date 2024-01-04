using Microsoft.VisualStudio.Imaging;
using System;
using System.Windows.Controls;

namespace CompileScore.Common
{
    public enum MonikerType
    {
        ScoreOff,
        ScoreOn,
    }

    class MonikerProxy : Grid
    {
        private CrispImage Shape {  get; set; } = new CrispImage();

        //private static

        //store bitmaps here on demand
        //store image sources here on demand
        //Add stuff for fake VS too but just drawing a quad

        //Add monikers for the requirement levels

        public double MonikerSize 
        {
            get { return Math.Max(Shape.Width, Shape.Height); }
            set { Shape.Height = value; Shape.Width = value; } 
        }

        public void SetMoniker(MonikerType type)
        {
            switch (type)
            {
                case MonikerType.ScoreOff: Shape.Moniker = KnownMonikers.Small; break;
                case MonikerType.ScoreOn: Shape.Moniker = KnownMonikers.HotSpot; break;
                default: Shape.Moniker = KnownMonikers.QuestionMark; break;
            }

            //Add as children if we haven't done so yet!
            if (Children.Count == 0)
            {
                Children.Add(Shape);
            }
        }
    }

    /*
    public static BitmapSource CreateBitmapSourceFromGdiBitmap(Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException("bitmap");

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

        var bitmapData = bitmap.LockBits(
            rect,
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);

        try
        {
            var size = (rect.Width * rect.Height) * 4;

            return BitmapSource.Create(
                bitmap.Width,
                bitmap.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgra32,
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
    */

}
