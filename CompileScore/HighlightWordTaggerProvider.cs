namespace CompileScore
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(HighlightWordTag))]
    public class HighlightWordTaggerProvider : IViewTaggerProvider
    {
        #region ITaggerProvider Members

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            // Only provide highlighting on the top-level buffer
            if (textView.TextBuffer != buffer)
                return null;

            return new HighlightWordTagger(textView, buffer) as ITagger<T>;
        }

        #endregion
    }
}