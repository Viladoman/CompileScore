using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Windows;
using System.Windows.Shapes;

namespace CompileScore.Glyph
{
    internal class ScoreGlyphFactory : IGlyphFactory
    {
        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            ScoreGlyphTag scoreTag = tag as ScoreGlyphTag;
                       
            if (scoreTag == null || !scoreTag.IsVisible)
            {
                return null;
            } 

            var severityColor = Common.Colors.GetSeverityBrush((uint)scoreTag.Value.Severity);
            severityColor.Opacity = Math.Max(0.25,severityColor.Opacity);

            var lineHeight = line.Height;
            var grid = new System.Windows.Controls.Grid()
            {
                Width = lineHeight,
                Height = lineHeight,
                MinWidth = lineHeight,
            };

            grid.Children.Add(new Rectangle()
            {
                Fill = System.Windows.Media.Brushes.Transparent,
                Width = lineHeight,
                Height = lineHeight,
                HorizontalAlignment = HorizontalAlignment.Right
            });

            grid.Children.Add(new Rectangle()
            {
                Fill = severityColor,
                Width = Math.Max(0, (lineHeight / 4) * (scoreTag.Value.Severity - 1)),
                Height = lineHeight,
                HorizontalAlignment = HorizontalAlignment.Right
            });

            grid.PreviewMouseLeftButtonDown += (sender, e) => { ThreadHelper.ThrowIfNotOnUIThread(); e.Handled = true; Glyph_OnClick(scoreTag.Value); };
            grid.PreviewMouseRightButtonDown += (sender, e) => { ThreadHelper.ThrowIfNotOnUIThread(); e.Handled = true; Glyph_OnClick(scoreTag.Value); };
            
            return grid;
        }

        private void Glyph_OnClick(CompileValue value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (value == null)
                return;

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open File", (a, b) => EditorUtils.OpenFile(value)));
            contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline",      (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Self Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.SelfMaxUnit, value)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph",     (a, b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }

    }
}
