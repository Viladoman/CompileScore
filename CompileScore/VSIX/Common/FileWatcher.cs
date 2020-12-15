using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Security.Permissions;

namespace CompileScore.Common
{
    public delegate void NotifyFileChanged();  // delegate

    public class FileWatcher
    {
        private FileSystemWatcher Watcher { set; get; }
        private DateTime WatcherLastRead { set; get; } = DateTime.MinValue;
        private string WatcherFullPath { set; get; } = "";

        public event NotifyFileChanged FileWatchedChanged;

        public void Watch(string fullPath)
        {
            if (fullPath == null)
            {
                Unwatch();
            }
            else
            {
                WatchImpl(Path.GetDirectoryName(fullPath),Path.GetFileName(fullPath));
            }
        }

        public void Unwatch()
        {
            if (Watcher != null)
            {
                Watcher.EnableRaisingEvents = false;
                Watcher = null;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchImpl(string path, string filename)
        {
            if (path != null && filename != null && Directory.Exists(path))
            {
                WatcherFullPath = path + '\\' + filename;
                if (Watcher == null)
                {
                    Watcher = new FileSystemWatcher();
                }

                Watcher.Path = path;
                Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                Watcher.Filter = filename;
                Watcher.Changed += OnWatchedFileChanged;
                Watcher.Created += OnWatchedFileChanged;
                Watcher.Deleted += OnWatchedFileChanged;
                Watcher.EnableRaisingEvents = true; // Begin watching.
            }
            else
            {
                Unwatch();
            }
        }

        private bool IsFileLocked(FileInfo file)
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

        private void OnWatchedFileChanged(object source, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(WatcherFullPath);

            if ((lastWriteTime - WatcherLastRead).Milliseconds > 100)
            {
                var fileInfo = new FileInfo(WatcherFullPath);
                while (File.Exists(WatcherFullPath) && IsFileLocked(fileInfo))
                {
                    //File is still locked, meaning the writing stream is still writing to the file,
                    // we need to wait until that process is done before trying to refresh it here. 
                    System.Threading.Thread.Sleep(500);
                }

                ThreadHelper.JoinableTaskFactory.Run(async delegate {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    OutputLog.Log("External file change detected for "+Path.GetFileName(WatcherFullPath));
                    FileWatchedChanged?.Invoke();
                });

                WatcherLastRead = lastWriteTime;
            }
        }
    }
}
