namespace CompileScore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Shell;

    public class ScoreGlyphTag : IGlyphTag
    {
        public ScoreGlyphTag(CompileValue value)
        {
            Value = value;
        }

        public CompileValue Value { get; }
    }

    class ScoreGlyphTagger : ITagger<ScoreGlyphTag>
    {
        private ITextBuffer _buffer;
        private readonly string _filename;
        private Dictionary<ITrackingSpan, CompileValue> _trackingSpans;

        public ScoreGlyphTagger(ITextView view, ITextBuffer sourceBuffer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _buffer = sourceBuffer;
            _filename = GetFileName(sourceBuffer);

            CompilerData.Instance.RefreshInstance();
            CompilerData.Instance.IncludeDataChanged += OnDataChanged;
            DocumentLifetimeManager.DocumentSavedTrigger += OnDocumentSaved;

            CreateTrackingSpans();
        }

        private string GetFileName(ITextBuffer buffer)
        {
            buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document);
            return document == null ? null : document.FilePath;
        }

        private void OnDocumentSaved(string filename)
        {
            if (filename == _filename)
            {
                RefreshTags();
            }
        }

        private void OnDataChanged()
        {
            RefreshTags();
        }

        private void RefreshTags()
        {
            CreateTrackingSpans();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length - 1))));
        }

        private void CreateTrackingSpans()
        {
            _trackingSpans = new Dictionary<ITrackingSpan, CompileValue>();
            var currentSnapshot = _buffer.CurrentSnapshot;
            MatchCollection matches = Regex.Matches(currentSnapshot.GetText(), @"#\s*include\s*[<""]([a-zA-Z0-9\\/\.]+)[>""]");
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    string file = match.Groups[1].Value;
                    string fileName = Path.GetFileName(file).ToLower();
                    CompileValue value = CompilerData.Instance.GetValue(CompilerData.CompileCategory.Include,fileName);

                    if (value != null && value.Severity > 0)
                    {
                        Span span = new Span(match.Index, match.Length);
                        var trackingSpan = currentSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);
                        _trackingSpans.Add(trackingSpan, value);
                    }
                }
            }
        }
        private void RemoveEmptyTrackingSpans()
        {
            var currentSnapshot = _buffer.CurrentSnapshot;
            var keysToRemove = _trackingSpans.Keys.Where(ts => ts.GetSpan(currentSnapshot).Length == 0).ToList();
            foreach (var key in keysToRemove)
            {
                _trackingSpans.Remove(key);
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ScoreGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            RemoveEmptyTrackingSpans();

            List<ITagSpan<ScoreGlyphTag>> res = new List<ITagSpan<ScoreGlyphTag>>();

            var currentSnapshot = _buffer.CurrentSnapshot;
            foreach (KeyValuePair<ITrackingSpan, CompileValue> item in _trackingSpans)
            {
                var spanInCurrentSnapshot = item.Key.GetSpan(currentSnapshot);
                if (spans.Any(sp => spanInCurrentSnapshot.IntersectsWith(sp)))
                {
                    var snapshotSpan = new SnapshotSpan(currentSnapshot, spanInCurrentSnapshot);
                    res.Add(new TagSpan<ScoreGlyphTag>(snapshotSpan, new ScoreGlyphTag(item.Value)));
                }
            }

            return res;
        }
    }
}
