namespace CompileScore
{
    using System.ComponentModel.Composition;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    internal class SeverityDefinitionBase : MarkerFormatDefinition
    {
        public SeverityDefinitionBase()
        {
            this.DisplayName = "Highlight Word";
            this.ZOrder = 5;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition1")]
    [UserVisible(true)]
    internal class SeverityDefinition0 : SeverityDefinitionBase
    {
        public SeverityDefinition0() { this.Fill = new SolidColorBrush(Color.FromArgb(70, (byte)0, (byte)255, (byte)255)); }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition2")]
    [UserVisible(true)]
    internal class SeverityDefinition1 : SeverityDefinitionBase
    {
        public SeverityDefinition1() { this.Fill = new SolidColorBrush(Color.FromArgb(70, (byte)0, (byte)255, (byte)0)); }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition3")]
    [UserVisible(true)]
    internal class SeverityDefinition2 : SeverityDefinitionBase
    {
        public SeverityDefinition2() { this.Fill = new SolidColorBrush(Color.FromArgb(70, (byte)255, (byte)255, (byte)0)); }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition4")]
    [UserVisible(true)]
    internal class SeverityDefinition3 : SeverityDefinitionBase
    {
        public SeverityDefinition3() { this.Fill = new SolidColorBrush(Color.FromArgb(70, (byte)159, (byte)0, (byte)255)); }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name("SeverityDefinition5")]
    [UserVisible(true)]
    internal class SeverityDefinition4 : SeverityDefinitionBase
    {
        public SeverityDefinition4() { this.Fill = new SolidColorBrush(Color.FromArgb(70, (byte)255, (byte)0, (byte)0)); }
    }
}
