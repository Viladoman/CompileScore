using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CompileScore
{
    internal class HighlightTagDefinitionBase : MarkerFormatDefinition
    {
        static public IEditorFormatMapService EditorFormatMapService { set; get; } = null;

        private uint Severity { get; }

        public HighlightTagDefinitionBase(uint severity)
        {
            Severity = severity;
            DisplayName = "CompileScore Severity " + severity;
            ZOrder = 5;

            CompilerData.Instance.ThemeChanged += OnThemeChanged;
            OnThemeChanged();
        }

        private void OnThemeChanged()
        {
            Fill = Common.Colors.GetSeverityBrush(Severity);

            if (EditorFormatMapService != null)
            {
                IEditorFormatMap formatMap = EditorFormatMapService.GetEditorFormatMap(category: "text");
                if (formatMap != null)
                {
                    formatMap.SetProperties("SeverityDefinition" + Severity, CreateResourceDictionaryFromDefinition());
                }
            }
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition1")]
    [UserVisible(false)]
    internal class SeverityDefinition0 : HighlightTagDefinitionBase
    {
        public SeverityDefinition0() : base(1){}
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition2")]
    [UserVisible(false)]
    internal class SeverityDefinition1 : HighlightTagDefinitionBase
    {
        public SeverityDefinition1() : base(2){}
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition3")]
    [UserVisible(false)]
    internal class SeverityDefinition2 : HighlightTagDefinitionBase
    {
        public SeverityDefinition2() : base(3) { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition4")]
    [UserVisible(false)]
    internal class SeverityDefinition3 : HighlightTagDefinitionBase
    {
        public SeverityDefinition3() : base(4) { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition5")]
    [UserVisible(false)]
    internal class SeverityDefinition4 : HighlightTagDefinitionBase
    {
        public SeverityDefinition4() : base(5) { }
    }
}
