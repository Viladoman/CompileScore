using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.Windows;
using System.Windows.Shapes;

namespace CompileScore.Glyph
{
    internal class ScoreGlyphFactory : IGlyphFactory
    {
        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            ScoreGlyphTag scoreTag = tag as ScoreGlyphTag;
            
            if (scoreTag == null)
            {
                return null;
            } 

            var lineHeight = line.Height;
            var grid = new System.Windows.Controls.Grid()
            {
                Width = lineHeight,
                Height = lineHeight
            };

            grid.Children.Add(new Rectangle()
            {
                Fill = Common.Colors.GetSeverityBrush(scoreTag.Value.Severity),
                Width = lineHeight / 3,
                Height = lineHeight,
                HorizontalAlignment = HorizontalAlignment.Right
            });
            
            return grid;
        }

    }
}
