namespace CompileScore
{
    using Microsoft.VisualStudio.Core.Imaging;
    using Microsoft.VisualStudio.Imaging;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Language.StandardClassification;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Adornments;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ScoreAsyncQuickInfoSource : IAsyncQuickInfoSource
    {
        private static readonly ImageId _icon = KnownMonikers.CompiledHelpFile.ToImageId();
        private static readonly ImageId _severityOnIcon = KnownMonikers.HotSpot.ToImageId();
        private static readonly ImageId _severityOffIcon = KnownMonikers.Small.ToImageId();

        private ITextBuffer _textBuffer;

        public ScoreAsyncQuickInfoSource(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        private bool IsEnabled()
        {
            GeneralSettingsPageGrid settings = CompilerData.Instance.GetGeneralSettings();
            return settings != null ? settings.OptionTooltipEnabled : false;
        }

        // This is called on a background thread.
        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            if (IsEnabled())
            {
                var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
                if (triggerPoint != null)
                {
                    var line = triggerPoint.Value.GetContainingLine();
                    string lineStr = line.GetText();

                    Match match = Regex.Match(lineStr, @"#\s*include\s*[<""]([a-zA-Z0-9\\/\.]+)[>""]");
                    if (match.Success)
                    {
                        string file = match.Groups[1].Value;
                        string fileName = Path.GetFileName(file);
                        string lowerFilename = fileName.ToLower();
                        CompileValue value = CompilerData.Instance.GetValue(lowerFilename);

                        Span span = new Span(line.Extent.Start + match.Index, match.Length);
                        var trackingSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);

                        var headerElm = new ContainerElement(
                            ContainerElementStyle.Wrapped,
                            new ImageElement(_icon),
                            new ClassifiedTextElement(
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.PreprocessorKeyword, fileName)
                            ));

                        if (value != null && value.Severity > 0)
                        {
                            var scoreElm = new ContainerElement(
                                ContainerElementStyle.Wrapped,
                                new ClassifiedTextElement(
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Compile Score:  ")
                                    ),
                                new ImageElement(value.Severity > 0 ? _severityOnIcon : _severityOffIcon),
                                new ImageElement(value.Severity > 1 ? _severityOnIcon : _severityOffIcon),
                                new ImageElement(value.Severity > 2 ? _severityOnIcon : _severityOffIcon),
                                new ImageElement(value.Severity > 3 ? _severityOnIcon : _severityOffIcon),
                                new ImageElement(value.Severity > 4 ? _severityOnIcon : _severityOffIcon)
                            );

                            //Found tooltip
                            var fullElm = new ContainerElement(
                                ContainerElementStyle.Stacked,
                                headerElm,
                                scoreElm,
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "Max: "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Max)),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Min: "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Min)),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Average: "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Mean)),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Count: "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $"{value.Count}")
                                ));

                            return Task.FromResult(new QuickInfoItem(trackingSpan, fullElm));
                        }
                        else
                        {
                            var fullElm = new ContainerElement(
                                ContainerElementStyle.Stacked,
                                headerElm,
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Compile Score: "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.ExcludedCode, " - ")
                                ));

                            return Task.FromResult(new QuickInfoItem(trackingSpan, fullElm));
                        }
                    }
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose()
        {
            // This provider does not perform any cleanup.
        }
    }
}
