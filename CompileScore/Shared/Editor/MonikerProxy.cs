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
}
