using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace CompileScore
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("Score Async Quick Info Provider")]
    [ContentType("C/C++")]
    [Order]
    internal sealed class ScoreAsyncQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            // This ensures only one instance per textbuffer is created
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new ScoreAsyncQuickInfoSource(textBuffer));
        }
    }
}
