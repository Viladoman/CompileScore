/*
namespace CompileScore
{
    using System;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("code")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class CompileDetailTaggerProvider : IViewTaggerProvider
    {

#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
        [Import]
        internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;
#pragma warning restore 649

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                throw new ArgumentNullException("textView");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer != textView.TextBuffer)
                return null;

            ITagAggregator<HighlightWordTag> tagAggregator = ViewTagAggregatorFactoryService.CreateTagAggregator<HighlightWordTag>(textView);

            return textView.Properties.GetOrCreateSingletonProperty(() => new CompilerDetailTagger((IWpfTextView)textView, tagAggregator) as ITagger<T>);
        }
    }
}
*/