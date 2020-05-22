using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    using EnvDTE;
    using System;
    using System.IO;
    using Microsoft.VisualStudio.Shell.Interop;
    using System.Security.Permissions;

    public delegate void NotifyFile(string filename);  // delegate

    public static class DocumentLifetimeManager
    {   
        private static Events _events;
        private static DocumentEvents _documentEvents;
        public static event NotifyFile DocumentSavedTrigger;

        private static FileSystemWatcher _fileWatcher = null;
        private static DateTime _fileWatcherLastRead = DateTime.MinValue;
        private static string _fileWatcherFullPath = "";
        public static event Notify FileWatchedChanged; 

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

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void WatchFile(string path, string filename)
        {
            if (Directory.Exists(path))
            {
                _fileWatcherFullPath = path + filename;
                if (_fileWatcher == null)
                {
                    _fileWatcher = new FileSystemWatcher();
                }

                _fileWatcher.Path = path;
                _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                _fileWatcher.Filter = filename;
                _fileWatcher.Changed += OnWatchedFileChanged;
                _fileWatcher.Created += OnWatchedFileChanged;
                _fileWatcher.Deleted += OnWatchedFileChanged;
                _fileWatcher.EnableRaisingEvents = true; // Begin watching.
            }
            else
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher = null;
                }
            }
        }

        private static void OnWatchedFileChanged(object source, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(_fileWatcherFullPath);
            if (lastWriteTime != _fileWatcherLastRead)
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    FileWatchedChanged?.Invoke();
                });

                _fileWatcherLastRead = lastWriteTime;
            }
        }

        private static void OnDocumentSaved(Document document)
        { 
            ThreadHelper.ThrowIfNotOnUIThread();
            DocumentSavedTrigger?.Invoke(document.FullName);
        }
    }
}
