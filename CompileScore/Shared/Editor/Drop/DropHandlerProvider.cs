
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor.DragDrop;

namespace CompileScore
{
    [Export(typeof(IDropHandlerProvider))]
    [DropFormat(DropHandlerProvider.FileDropDataFormat)]
    [Name("Score Loader Drop Handler")]
    [Order(Before = "DefaultFileDropHandler")]
    internal class DropHandlerProvider : IDropHandlerProvider
    {
        internal const string FileDropDataFormat = "FileDrop";

        public IDropHandler GetAssociatedDropHandler(IWpfTextView TextView)
        {
            return TextView.Properties.GetOrCreateSingletonProperty<DropHandler>(() => new DropHandler());
        }
    }
}
