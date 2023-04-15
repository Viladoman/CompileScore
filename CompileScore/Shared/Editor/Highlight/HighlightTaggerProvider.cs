using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CompileScore
{   
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("C/C++")]
    [TagType(typeof(HighlightTag))]
    public class HighlightTaggerProvider : IViewTaggerProvider
    {
#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
        [Import]
        internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;

        [Import]
        internal Microsoft.VisualStudio.Text.Classification.IEditorFormatMapService editorFormatMapService;
#pragma warning restore 649

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            // Only provide highlighting on the top-level buffer
            if (textView.TextBuffer != buffer)
                return null;

            if (editorFormatMapService != null)
            {
                HighlightTagDefinitionBase.EditorFormatMapService = editorFormatMapService;
            }

            ITagAggregator<ScoreGlyphTag> tagAggregator = ViewTagAggregatorFactoryService.CreateTagAggregator<ScoreGlyphTag>(textView);
            return new HighlightTagger(buffer, tagAggregator) as ITagger<T>;
        }
    }
}