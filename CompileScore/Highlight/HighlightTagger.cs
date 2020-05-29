namespace CompileScore
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;
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

    public class HighlightTag : TextMarkerTag
    {
        public HighlightTag(CompileValue value) : base("SeverityDefinition" + value.Severity)
        {
            Value = value;
        }

        public CompileValue Value { get; }
    }

    class HighlightTagger : ITagger<HighlightTag>
    {
        private ITagAggregator<ScoreGlyphTag> _tagAggregator;
        private ITextBuffer _buffer;
        private bool _enabled = false;

        public HighlightTagger(ITextBuffer sourceBuffer, ITagAggregator<ScoreGlyphTag> tagAggregator)
        {
            _buffer = sourceBuffer;
            _tagAggregator = tagAggregator;
            _tagAggregator.TagsChanged += OnTagsChanged;

            CompilerData.Instance.HighlightEnabledChanged += OnEnabledChanged;

            RefreshEnable();
        }

        private void OnTagsChanged(object sender, TagsChangedEventArgs e)
        {
            TagsChanged?.Invoke(sender, new SnapshotSpanEventArgs(e.Span.GetSnapshotSpan()));
        }

        public void OnEnabledChanged()
        {
            if (RefreshEnable())
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length - 1))));
            }
        }

        private bool RefreshEnable()
        {
            GeneralSettingsPageGrid settings = CompilerData.Instance.GetGeneralSettings();
            bool isEnabled = settings != null? settings.OptionHighlightEnabled : false; 
            if (isEnabled != _enabled)
            {
                _enabled = isEnabled;
                return true; 
            }
            return false;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<HighlightTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0 || !_enabled)
                yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;

            var tags = _tagAggregator.GetTags(spans);
            foreach (IMappingTagSpan<ScoreGlyphTag> tag in tags)
            {
                NormalizedSnapshotSpanCollection colorTagSpans = tag.Span.GetSpans(snapshot);

                // Ignore data tags that are split by projection.
                // This is theoretically possible but unlikely in current scenarios.
                if (colorTagSpans.Count != 1)
                    continue;
                if (tag.Span.GetSpan().Length == 0)
                    continue;

                yield return new TagSpan<HighlightTag>(tag.Span.GetSnapshotSpan(), new HighlightTag(tag.Tag.Value));
            }            
        }
    }
}
