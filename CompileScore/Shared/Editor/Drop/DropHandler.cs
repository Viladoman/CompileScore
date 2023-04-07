using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor.DragDrop;

namespace CompileScore
{
    public class DropHandler : IDropHandler
    {
        //TODO ~ ramonv add support for .ctl and .etl for here and open file. Also add the option to generate the trace on generation
        private static readonly List<string> supportedExtensions = new List<string> { ".scor" }; 

        public DragDropPointerEffects HandleDragStarted(DragDropInfo dragDropInfo)
        {
            return DragDropPointerEffects.Link;
        }

        public DragDropPointerEffects HandleDraggingOver(DragDropInfo dragDropInfo)
        {
            return DragDropPointerEffects.Link;
        }

        public DragDropPointerEffects HandleDataDropped(DragDropInfo dragDropInfo)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string filename = GetScoreFilename(dragDropInfo);
            if (filename != null)
            {
                CompilerData.Instance.ForceLoadFromFilename(filename);
                EditorUtils.FocusOverviewWindow();
            }
            return DragDropPointerEffects.Link;
        }
     
        public bool IsDropEnabled(DragDropInfo dragDropInfo)
        {
            return GetScoreFilename(dragDropInfo) != null;
        }

        private static string GetScoreFilename(DragDropInfo Info)
        {
            DataObject Data = new DataObject(Info.Data);
            if (Info.Data.GetDataPresent(DropHandlerProvider.FileDropDataFormat))
            {
                StringCollection Files = Data.GetFileDropList();
                if (Files != null && Files.Count == 1 && !string.IsNullOrEmpty(Files[0]) && supportedExtensions.Contains(Path.GetExtension(Files[0])))
                {
                    return Files[0];
                }
            }

            return null;
        }

        public void HandleDragCanceled()
        {
        }
    }
}