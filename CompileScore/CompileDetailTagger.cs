namespace CompileScore
{ 
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;

    internal static class SpanExtensions
    {
        private const PositionAffinity positionAffinity = PositionAffinity.Successor;

        public static Span GetSpan(this IMappingSpan mappingTagSpan)
        {
            var buffer = mappingTagSpan.AnchorBuffer;
            var startSnapshotPoint = mappingTagSpan.Start.GetPoint(buffer, positionAffinity).Value;
            var endSnapshotPoint = mappingTagSpan.End.GetPoint(buffer, positionAffinity).Value;
            var length = endSnapshotPoint.Position - startSnapshotPoint.Position;
            return new Span(startSnapshotPoint.Position, length);
        }

        public static SnapshotSpan GetSnapshotSpan(this IMappingSpan mappingTagSpan)
        {
            var buffer = mappingTagSpan.AnchorBuffer;
            var span = GetSpan(mappingTagSpan);
            var snapshot = mappingTagSpan.Start.GetPoint(buffer, positionAffinity).Value.Snapshot;
            return new SnapshotSpan(snapshot, span);
        }
    }

    internal sealed class CompilerDetailTagger : IntraTextAdornmentTagger<HighlightWordTag, CompileDetailAdornment>
    {
        private ITagAggregator<HighlightWordTag> _tagAggregator;

        public CompilerDetailTagger(IWpfTextView view, ITagAggregator<HighlightWordTag> tagAggregator)
            : base(view)
        {
            this._tagAggregator = tagAggregator;
            _tagAggregator.TagsChanged += OnTagsChanged;
        }

        private void OnTagsChanged(object sender, TagsChangedEventArgs e)
        {
            var snapshotSpan = e.Span.GetSnapshotSpan();//Extension method
            InvokeTagsChanged(sender, new SnapshotSpanEventArgs(snapshotSpan));

        }

        public void Dispose()
        {
            _tagAggregator.Dispose();
        }

        // To produce adornments that don't obscure the text, the adornment tags
        // should have zero length spans. Overriding this method allows control
        // over the tag spans.
        protected override IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, HighlightWordTag>> GetAdornmentData(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;

            var tags = _tagAggregator.GetTags(spans);
            foreach (IMappingTagSpan<HighlightWordTag> tag in tags)
            {
                NormalizedSnapshotSpanCollection colorTagSpans = tag.Span.GetSpans(snapshot);

                // Ignore data tags that are split by projection.
                // This is theoretically possible but unlikely in current scenarios.
                if (colorTagSpans.Count != 1)
                    continue;
                if (tag.Span.GetSpan().Length == 0)
                    continue;

                SnapshotSpan adornmentSpan = new SnapshotSpan(colorTagSpans[0].End, 0);

                yield return Tuple.Create(adornmentSpan, (PositionAffinity?)PositionAffinity.Successor, tag.Tag);
            }
        }

        protected override CompileDetailAdornment CreateAdornment(HighlightWordTag originalTag, SnapshotSpan span)
        {
            return new CompileDetailAdornment(originalTag);
        }

        protected override bool UpdateAdornment(CompileDetailAdornment adornment, HighlightWordTag additionalData)
        {
            return false;
        }
    }
}
