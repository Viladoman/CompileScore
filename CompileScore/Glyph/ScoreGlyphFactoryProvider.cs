using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompileScore.Glyph
{
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using System.ComponentModel.Composition;

    [Export(typeof(IGlyphFactoryProvider))]
    [Name("ScoreGlyph")]
    [Order(Before = "VsTextMarker")]
    [ContentType("C/C++")]
    [TagType(typeof(ScoreGlyphTag))]
    internal sealed class ScoreGlyphFactoryProvider : IGlyphFactoryProvider
    {
        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
        {
            return new ScoreGlyphFactory();
        }

    }
}
