using Microsoft.Win32;
using System;
using System.Windows;

namespace CompileScore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Common.ColorTheme.AddThemeToApplicationResources();

            InitSystems();

            RefreshToolVisibility();
            CompilerData.Instance.ScoreDataChanged += RefreshToolVisibility;

            this.AllowDrop = true;
            this.Drop += OnDrop;

            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.GetLength(0) >= 2)
            {
                OpenFile(arguments[1]);
            }
        }

        private void InitSystems()
        {
            var package = new CompileScorePackage();
            var serviceProvider = new VSFakeServiceProvider();
            OutputLog.Initialize(serviceProvider);
            CompilerData.Instance.Initialize(package, serviceProvider);
            Timeline.CompilerTimeline.Instance.Initialize(package);
            Includers.CompilerIncluders.Instance.Initialize(package);
            DocumentLifetimeManager.Initialize(serviceProvider);
        }

        private void RefreshToolVisibility()
        {
            bool hasData = CompilerData.Instance.GetTotals().Count > 0; 
            placeholder.Visibility   = hasData? Visibility.Collapsed : Visibility.Visible;
            overview.Visibility      = hasData? Visibility.Visible   : Visibility.Collapsed;
            ReloadMenuItem.IsEnabled = hasData;
        }

        private void OpenFile(string filename)
        {
            if (System.IO.Path.GetExtension(filename) == ".scor")
            {
                CompilerData.Instance.ForceLoadFromFilename(filename);
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                OpenFile(files[0]);
            }
        }

        private void OnMenuFileOpen(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Compile Score files (*.scor)|*.scor";
            openFileDialog.InitialDirectory = @"C:\";
            openFileDialog.Title = "Please select a Compile Score file to inspect.";

            if (openFileDialog.ShowDialog() == true)
            {
                OpenFile(openFileDialog.FileName);
            }
        }

        private void OnMenuFileExit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void OnReloadFile(object sender, RoutedEventArgs e)
        {
            CompilerData.Instance.ReloadSeverities();
        }

        private void OnHelp(object sender, RoutedEventArgs e)
        {
            Documentation.OpenLink(Documentation.Link.MainPage);
        }

        private void OnMenuAbout(object sender, RoutedEventArgs e)
        {
            AboutWindow dlg = new AboutWindow();
            dlg.Owner = this;
            dlg.ShowDialog();
        }
    }
}
