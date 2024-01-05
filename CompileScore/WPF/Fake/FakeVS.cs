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

        public void Activate() { }
        public void Clear() { }
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
            public static async System.Threading.Tasks.Task SwitchToMainThreadAsync() { await System.Threading.Tasks.Task.Delay(10); } // Dummy switch as the App is sync
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
        private GeneralSettingsPageGrid generalSettings = new GeneralSettingsPageGrid();
        private ThemeSettingsPageGrid   themeSettings   = new ThemeSettingsPageGrid();

        public GeneralSettingsPageGrid GetGeneralSettings() { return generalSettings; }
        public ThemeSettingsPageGrid GetThemeSettings() { return themeSettings; }

        public Window FindToolWindow(Type toolWindowType, int id, bool create)
        {
            
            if (toolWindowType == typeof(Timeline.TimelineWindow))
            {
                return new Timeline.TimelineWindow();
            }

            if (toolWindowType == typeof(Includers.IncludersWindow))
            {
                return new Includers.IncludersWindow();
            }
            
            if (toolWindowType == typeof(Requirements.RequirementsWindow))
            {
                return new Requirements.RequirementsWindow();
            }

            //TODO ~ ramonv ~ fix here window management

            return null;
        }
    };
    
    public class SDTE { };
    public class DTE2 : SDTE
    {
        public Events Events = new Events();
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

    public class GeneralSettingsPageGrid
    {
        public enum SeverityCriteria{}

        public bool OptionNormalizedSeverity = true;

        public SeverityCriteria OptionSeverityCriteria { set; get; }

        private List<uint> fakeSeveritiesValue = new List<uint>();
        private List<float> fakeSeveritiesNormalized = new List<float> { 50.0f, 75.0f, 90.0f, 98.0f };

        public List<uint> GetOptionValueSeverities() { return fakeSeveritiesValue; }
        public List<float> GetOptionNormalizedSeverities() { return fakeSeveritiesNormalized; }

        public Includers.IncludersDisplayMode OptionIncludersDefaultDisplayMode { get { return Includers.IncludersDisplayMode.Once; } }

    };

    public class ThemeSettingsPageGrid
    {
        static public readonly Color[] SeverityColors = { (Color)ColorConverter.ConvertFromString("#40C8C8C8"),
                                                          (Color)ColorConverter.ConvertFromString("#401EFF00"),
                                                          (Color)ColorConverter.ConvertFromString("#400070DD"),
                                                          (Color)ColorConverter.ConvertFromString("#40A335EE"),
                                                          (Color)ColorConverter.ConvertFromString("#40FF8000") };

        static public readonly Color[] StrengthColors ={ (Color)ColorConverter.ConvertFromString("#FF606060"),
                                                         (Color)ColorConverter.ConvertFromString("#FF309730"),
                                                         (Color)ColorConverter.ConvertFromString("#FF309797"),
                                                         (Color)ColorConverter.ConvertFromString("#FF979700"),
                                                         (Color)ColorConverter.ConvertFromString("#FF970000") };

    }

}
