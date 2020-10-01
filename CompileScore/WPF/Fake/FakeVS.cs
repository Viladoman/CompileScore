using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

//SUPER FAKE VOID IMPLEMENTATION OF THE VS SDK 

namespace EnvDTE { }
namespace EnvDTE80 { }
namespace Microsoft.VisualStudio.Shell.Interop { }
namespace Microsoft.VisualStudio.PlatformUI { }

namespace CompileScore
{
    public class IVsOutputWindowPane
    {
        public void OutputString(string input)
        {
            Trace.WriteLine(input);
        }
    };

    public class IVsOutputWindow 
    {
        IVsOutputWindowPane Pane { set; get; }

        public void CreatePane(ref Guid guid, string title, int visible, int clearWithSolution)
        {
            Pane = new IVsOutputWindowPane();
        }

        public void GetPane(ref Guid guid, out IVsOutputWindowPane pane)
        {
            pane = Pane;
        }

    };

    public class SVsOutputWindow : IVsOutputWindow { };

    public class ThreadHelper 
    {
        public static class JoinableTaskFactory
        {
            public static void Run(Func<System.Threading.Tasks.Task> asyncMethod) { asyncMethod.Invoke(); }

            private static async System.Threading.Tasks.Task DummyTask() { await System.Threading.Tasks.Task.Delay(10); return; }

            public static System.Threading.Tasks.Task SwitchToMainThreadAsync() { return DummyTask(); }
        }

        public static void ThrowIfNotOnUIThread() {}
    };

    public class Document 
    {
        public string FullName = "";
    };

    public class DocumentEvents 
    {
        public delegate void NotifyDocument(Document document);
        public event NotifyDocument DocumentSaved;

        public void FakeNotification() { DocumentSaved.Invoke(new Document()); }     
    };
    public class Events 
    {
        public DocumentEvents DocumentEvents = new DocumentEvents();
    }; 

    public class CompileScorePackage 
    {
        private GeneralSettingsPageGrid settings = new GeneralSettingsPageGrid();
        public GeneralSettingsPageGrid GetGeneralSettings() { return settings; }

        public Window FindToolWindow(Type toolWindowType, int id, bool create)
        {
            
            if (toolWindowType == typeof(CompileScore.Timeline.TimelineWindow))
            {
                return new CompileScore.Timeline.TimelineWindow();
            }
            
            //TODO ~ ramonv ~ fix here window management

            return null;
        }
    };
    
    public class SDTE { };
    public class DTE2 : SDTE
    {
        public Events Events = new Events();
        public Solution Solution = new Solution();
    };

    public class Assumes
    {
        public static void Present(object obj) { }
    };

    public class VSFakeServiceProvider : IServiceProvider
    {
        private SVsOutputWindow outputWindow = new SVsOutputWindow();
        private SDTE sdte = new DTE2();

        public object GetService(Type serviceType)
        {
            if (typeof(SVsOutputWindow) == serviceType) { return outputWindow; }
            if (typeof(SDTE) == serviceType) { return sdte; }
            return null;
        }
    }
    public class Solution
    {
        public string FullName = "";
    };

    public class GeneralSettingsPageGrid
    {
        public string OptionPath = "";
        public string OptionScoreFileName = "compileData.scor";
        public bool OptionPathRelativeToSolution = true;
        public bool OptionNormalizedSeverity = true;

        private List<uint> fakeSeverities = new List<uint>();
        public List<uint> GetOptionSeverities() { return fakeSeverities; }
    };

}
