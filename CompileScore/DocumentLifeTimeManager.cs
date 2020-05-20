using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    using EnvDTE;
    using System;
    using Microsoft.VisualStudio.Shell.Interop;
   
    public delegate void NotifyFile(string filename);  // delegate

    public static class DocumentLifetimeManager
    {   
        private static Events _events;
        private static DocumentEvents _documentEvents;

        public static event NotifyFile DocumentSavedTrigger;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE2 applicationObject = serviceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            _events = applicationObject.Events;
            

            //setup document events
            _documentEvents = _events.DocumentEvents;
            _documentEvents.DocumentSaved += OnDocumentSaved;
        }

        private static void OnDocumentSaved(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DocumentSavedTrigger?.Invoke(document.FullName);
        }
    }
}
