using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Security.Permissions;

namespace CompileScore
{
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
                UnWatchFile();
            }
        }

        public static void UnWatchFile()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher = null;
            }
        }

        private static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        private static void OnWatchedFileChanged(object source, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(_fileWatcherFullPath);

            if ((lastWriteTime-_fileWatcherLastRead).Milliseconds > 100)
            {
                var fileInfo = new FileInfo(_fileWatcherFullPath);
                while (File.Exists(_fileWatcherFullPath) && IsFileLocked(fileInfo))
                {
                    //File is still locked, meaning the writing stream is still writing to the file,
                    // we need to wait until that process is done before trying to refresh it here. 
                    System.Threading.Thread.Sleep(500);
                }

                ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    OutputLog.Log("File change detected.");
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
