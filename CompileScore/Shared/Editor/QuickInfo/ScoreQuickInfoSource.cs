using CompileScore.Includers;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CompileScore
{
    internal sealed class ScoreAsyncQuickInfoSource : IAsyncQuickInfoSource
    {
        private static readonly ImageId _icon = KnownMonikers.CompiledHelpFile.ToImageId();
        private static readonly ImageId _severityOnIcon = KnownMonikers.HotSpot.ToImageId();
        private static readonly ImageId _severityOffIcon = KnownMonikers.Small.ToImageId();

        //TODO ~ Ramonv ~ add requirements monikers "StatusRequired" and similar for requirement strength

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

        private static string GetDocumentPath(ITextView view)
        {
            if (view.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument) && textDocument != null)
            {
                return textDocument.FilePath;
            }

            return null;
        }

        private object GetIncluderData(string documentPath, CompileValue includeeValue)
        {
            if (includeeValue == null || documentPath == null)
                return null; 

            int IncludeeIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, includeeValue);
            if (IncludeeIndex < 0)
                return null; 

            var thisDocCompilerData = CompilerData.Instance.SeekProfilerValueFromFullPath(documentPath);
            if (thisDocCompilerData is UnitValue)
            {
                int includerIndex = CompilerData.Instance.GetIndexOf(thisDocCompilerData as UnitValue);
                return Includers.CompilerIncluders.Instance.GetIncludeUnitValue(includerIndex, IncludeeIndex);
            }
            else if (thisDocCompilerData is CompileValue )
            {
                int includerIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, thisDocCompilerData as CompileValue);
                return Includers.CompilerIncluders.Instance.GetIncludeInclValue(includerIndex, IncludeeIndex);
            }

            return null;
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

                    Match match = Regex.Match(lineStr, EditorUtils.IncludeRegex);
                    if (match.Success)
                    {
                        string file = match.Groups[1].Value;
                        string fileName = EditorUtils.GetFileNameSafe(file);

                        if (fileName != null)
                        {
                            string lowerFilename = fileName?.ToLower();

                            CompilerData compilerData = CompilerData.Instance;
                            compilerData.Hydrate(CompilerData.HydrateFlag.Main);
                            CompileValue value = compilerData.GetValueByName(CompilerData.CompileCategory.Include,lowerFilename);

                            Microsoft.VisualStudio.Text.Span span = new Microsoft.VisualStudio.Text.Span(line.Extent.Start + match.Index, match.Length);
                            var trackingSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);

                            List<ContainerElement> elements = new List<ContainerElement>();

                            elements.Add( new ContainerElement(
                                ContainerElementStyle.Wrapped,
                                new ImageElement(_icon),
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.PreprocessorKeyword, fileName)
                                )) );

                            string docFilePath = GetDocumentPath(session.TextView);
                            CreateCompileDataElements(elements, value, docFilePath);

                            if ( docFilePath != null)
                            {
                                ContainerElement requirementElem = CreateParserElement(ParserData.Instance.GetFileRequirements(docFilePath.ToLower(), lowerFilename));
                                if (requirementElem != null)
                                {
                                    elements.Add (requirementElem);
                                }
                            }

                            return Task.FromResult(new QuickInfoItem(trackingSpan, new ContainerElement(ContainerElementStyle.Stacked, elements)));
                        }
                    }
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        private void CreateCompileDataElements(List<ContainerElement> elements, CompileValue value, string documentPath )
        {
            if (value != null && value.Severity > 0)
            {
                var criteria = CompilerData.Instance.GetSeverityCriteria();
                elements.Add(new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                            new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, $"Compile Score ({criteria}):  ")
                        ),
                    new ImageElement(value.Severity >= 1 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity >= 2 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity >= 3 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity >= 4 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity >= 5 ? _severityOnIcon : _severityOffIcon)
                ));

                CreateIncludersElement(elements, value, documentPath);

                int unitCount = CompilerData.Instance.GetUnits().Count;
                float unitImpactPercent = unitCount > 0 ? ((float)value.UnitCount * 100) / unitCount : 0;

                //Found tooltip
                elements.Add(new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "Global Max: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Max)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " (Self: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.SelfMax)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, ") Min: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Min)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Average: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Average)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Accumulated: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.Accumulated)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " (Self: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(value.SelfAccumulated)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, ") Units: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $"{value.Count}"),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " ("),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, unitImpactPercent.ToString("n2")),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "%)")
                    )));
            }
            else
            {
                elements.Add(new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Compile Score: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.ExcludedCode, " - ")
                    )));
            }
        }

        private void CreateIncludersElement(List<ContainerElement> elements, CompileValue value, string documentPath)
        {
            var includerData = GetIncluderData(documentPath, value);         

            if ( includerData is IncludersUnitValue )
            {
                IncludersUnitValue includeInfo = includerData as IncludersUnitValue;

                elements.Add(new ContainerElement(
                   ContainerElementStyle.Wrapped,
                   new ClassifiedTextElement(
                       new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "This duration: "),
                       new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(includeInfo.Duration))
                   )));
            }
            else if ( includerData is IncludersInclValue )
            {
                IncludersInclValue includeInfo = includerData as IncludersInclValue;

                elements.Add(new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "This Max: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(includeInfo.Max)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Accumulated: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, Common.UIConverters.GetTimeStr(includeInfo.Accumulated)),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Units: "),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $"{includeInfo.Count}")
                    )));
            }
            else
            {
                elements.Add(new ContainerElement(
                   ContainerElementStyle.Wrapped,
                   new ClassifiedTextElement(
                       new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "This duration: - ")
                   )));
            }

        }

        private ContainerElement CreateParserElement(ParserFileRequirements file)
        {
            //TODO ~ Ramonv ~ add option in settings to display or not this information 

            if ( file == null )  
                return null;

            string globalsTxt         = Requirements.RequirementsGraphTooltip.GetGlobalsRequirementRecap(file);
            string structureUsageTxt  = Requirements.RequirementsGraphTooltip.GetStructureUsageRequirementRecap(file);
            string structureAccessTxt = Requirements.RequirementsGraphTooltip.GetStructureAccessRequirementRecap(file);

            List<ContainerElement> elements = new List<ContainerElement>();

            if (globalsTxt.Length > 0)
            {
                elements.Add(new ContainerElement(
                                    ContainerElementStyle.Wrapped,
                                    new ClassifiedTextElement(
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Globals:"),
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, globalsTxt)
                                   )));
            }

            if (structureUsageTxt.Length > 0)
            {
                elements.Add(new ContainerElement(
                                   ContainerElementStyle.Wrapped,
                                   new ClassifiedTextElement(
                                       new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Structure Usage:"),
                                       new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, structureUsageTxt)
                                  )));
            }

            if (structureAccessTxt.Length > 0)
            {
                elements.Add(new ContainerElement(
                                  ContainerElementStyle.Wrapped,
                                  new ClassifiedTextElement(
                                      new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Structure Access:"),
                                      new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, structureAccessTxt)
                                 )));
            }

            if (elements.Count == 0 ) 
            {
                elements.Add(new ContainerElement(ContainerElementStyle.Wrapped, new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, "-- No Direct Requirements Found --"))));
            }

            if (file.Includes != null && file.Includes.Count > 0)
            {
                elements.Add(new ContainerElement(
                   ContainerElementStyle.Wrapped,
                   new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Sub Includes:")
                   )));

                foreach (ParserFileRequirements fileReq in file.Includes)
                {
                    elements.Add(new ContainerElement(
                            ContainerElementStyle.Wrapped,
                            new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, fileReq.Name)
                            )));
                }
            }

            //TODO ~ Ramonv ~ add option to open requirement window from here on VS17

            //#if VS17
            //ClassifiedTextElement text = new ClassifiedTextElement( ClassifiedTextElement.CreateHyperlink(tag, null,  => Parser.Log(loc.ToString()) ) );
            //#else
            //ClassifiedTextElement text = new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, tag));
            //#endif

            return new ContainerElement(ContainerElementStyle.Stacked, elements);

        }

        public void Dispose()
        {
            // This provider does not perform any cleanup.
        }
    }
}
