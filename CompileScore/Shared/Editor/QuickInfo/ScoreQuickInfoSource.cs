using CompileScore.Includers;
using EnvDTE;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.CodeCleanUp;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Newtonsoft.Json.Linq;
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

            var thisDocCompilerData = EditorUtils.SeekObjectFromFullPath(documentPath);
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

                            //retrieve concrete information on this include 
                            string docFilePath = GetDocumentPath(session.TextView);

                            Microsoft.VisualStudio.Text.Span span = new Microsoft.VisualStudio.Text.Span(line.Extent.Start + match.Index, match.Length);
                            var trackingSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);

                            List<ContainerElement> elements = new List<ContainerElement>();

                            elements.Add( new ContainerElement(
                                ContainerElementStyle.Wrapped,
                                new ImageElement(_icon),
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.PreprocessorKeyword, fileName)
                                )) );

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
                    new ImageElement(value.Severity > 0 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity > 1 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity > 2 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity > 3 ? _severityOnIcon : _severityOffIcon),
                    new ImageElement(value.Severity > 4 ? _severityOnIcon : _severityOffIcon)
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
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, " Units: "),
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
            if ( file == null)  
                return null;

            List< ContainerElement > elements = new List< ContainerElement >();
            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                List<ParserCodeRequirement> requirements = file.Global[i];
                if (requirements != null && requirements.Count > 0) 
                {
                    List<ContainerElement> reqElems = new List<ContainerElement>();

                    string categoryName = ((ParserEnums.GlobalRequirement)i).ToString();

                    reqElems.Add(new ContainerElement(ContainerElementStyle.Wrapped, new ClassifiedTextElement( new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $"{categoryName}: "))));

                    foreach (ParserCodeRequirement req in requirements)
                    {
                        string tag = req.UseLocations.Count > 1 ? $"{req.Name} ({req.UseLocations.Count}) " : $"{req.Name} ";
//#if VS17
//ClassifiedTextElement text = new ClassifiedTextElement( ClassifiedTextElement.CreateHyperlink(tag, null,  => Parser.Log(loc.ToString()) ) );
//#else
                        ClassifiedTextElement text = new ClassifiedTextElement( new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, tag) );
//#endif

                        reqElems.Add( new ContainerElement( ContainerElementStyle.Wrapped, text ));
                    }

                    elements.Add(new ContainerElement(ContainerElementStyle.Wrapped, reqElems));
                }
            }

            if (elements.Count > 0)
            {
                elements.Insert(0, new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Globals:")
                    )));
            }

            if (file.Structures != null && file.Structures.Count > 0)
            {
                elements.Add(new ContainerElement(
                   ContainerElementStyle.Wrapped,
                   new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Required Structures:")
                   )));

                foreach (ParserStructureRequirement structReq in file.Structures)
                {
                    elements.Add(new ContainerElement(
                            ContainerElementStyle.Wrapped,
                            new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, $"{structReq.Name}:")
                            )));

                    {
                        List<ContainerElement> reqElems = new List<ContainerElement>();
                        for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
                        {
                            List<ulong> simpleReq = structReq.Simple[i];
                            if (simpleReq != null && simpleReq.Count > 0)
                            {
                                string categoryName = ((ParserEnums.StructureSimpleRequirement)i).ToString();

                                reqElems.Add(new ContainerElement(
                                    ContainerElementStyle.Wrapped,
                                    new ClassifiedTextElement(
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, categoryName),
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $": {simpleReq.Count} ")
                                   )));
                            }
                        }

                        if (reqElems.Count > 0)
                        {
                            reqElems.Insert(0, new ContainerElement(
                               ContainerElementStyle.Wrapped,
                               new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "- ")
                               )));
                            elements.Add(new ContainerElement(ContainerElementStyle.Wrapped, reqElems));
                        }
                    }

                    for (int i = 0; i < (int)ParserEnums.StructureNamedRequirement.Count; ++i)
                    {
                        List<ParserCodeRequirement> named = structReq.Named[i];
                        if (named != null && named.Count > 0)
                        {
                            List<ContainerElement> reqElems = new List<ContainerElement>();

                            string categoryName = ((ParserEnums.StructureNamedRequirement)i).ToString();

                            reqElems.Add(new ContainerElement(ContainerElementStyle.Wrapped, new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, $"- {categoryName}: "))));

                            foreach (ParserCodeRequirement req in named)
                            {
                                string tag = req.UseLocations.Count > 1 ? $"{req.Name} ({req.UseLocations.Count}) " : $"{req.Name} ";
                                ClassifiedTextElement text = new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, tag));
                                reqElems.Add(new ContainerElement(ContainerElementStyle.Wrapped, text));
                            }

                            elements.Add(new ContainerElement(ContainerElementStyle.Wrapped, reqElems));
                        }
                    }
                }
            }

            if (elements.Count == 0 )
            {
                elements.Add(new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "No requirements")
                    )));
            }
            
            return new ContainerElement(ContainerElementStyle.Stacked, elements);

        }

        public void Dispose()
        {
            // This provider does not perform any cleanup.
        }
    }
}
