using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Common
{
    public enum MonikerType
    {
        ScoreOff,
        ScoreOn, 
    }

    public class MonikerProxy : Grid
    {
        private Border Shape {  get; set; } = new Border();

        public double MonikerSize 
        { 
            get { return Math.Max(Width,Height); } 
            set 
            { 
                Width = value; Height = value; MinHeight = value; MaxHeight = value; 
                Shape.Width = value; Shape.Height = value; Shape.MinHeight = value; Shape.MinWidth = value; 
            } 
        }

        public void SetMoniker(MonikerType type)
        {
            switch (type)
            { 
                case MonikerType.ScoreOn:  
                    Shape.Background = Colors.OtherBrush; 
                    Shape.BorderThickness = new Thickness(1);
                    break;
                default: 
                    Shape.Background = Colors.ThreadBrush;
                    Shape.BorderThickness = new Thickness(5);
                    break;
            }

            //Add as children if we haven't done so yet!
            if (Children.Count == 0)
            {
                Children.Add(Shape);
            }
        }
    }
}