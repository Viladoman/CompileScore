using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CompileScore
{
    internal class HighlightTagDefinitionBase : MarkerFormatDefinition
    {
        public HighlightTagDefinitionBase(uint severity)
        {
            var color = Common.Colors.GetSeverityBrush(severity);
            color.Opacity = 0.25;
            this.Fill = color;
            this.DisplayName = "Highlight Word";
            this.ZOrder = 5;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition1")]
    [UserVisible(true)]
    internal class SeverityDefinition0 : HighlightTagDefinitionBase
    {
        public SeverityDefinition0() : base(1){}
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition2")]
    [UserVisible(true)]
    internal class SeverityDefinition1 : HighlightTagDefinitionBase
    {
        public SeverityDefinition1() : base(2){}
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition3")]
    [UserVisible(true)]
    internal class SeverityDefinition2 : HighlightTagDefinitionBase
    {
        public SeverityDefinition2() : base(3) { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition4")]
    [UserVisible(true)]
    internal class SeverityDefinition3 : HighlightTagDefinitionBase
    {
        public SeverityDefinition3() : base(4) { }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition5")]
    [UserVisible(true)]
    internal class SeverityDefinition4 : HighlightTagDefinitionBase
    {
        public SeverityDefinition4() : base(5) { }
    }
}
