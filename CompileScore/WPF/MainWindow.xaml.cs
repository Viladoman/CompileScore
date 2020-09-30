using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

            InitSystems();

            RefreshToolVisibility();
            CompilerData.Instance.ScoreDataChanged += RefreshToolVisibility;

            this.AllowDrop = true;
            this.Drop += OnDrop;
        }

        private void InitSystems()
        {
            var package = new CompileScorePackage();
            var serviceProvider = new VSFakeServiceProvider();
            OutputLog.Initialize(serviceProvider);
            CompilerData.Instance.Initialize(package, serviceProvider);
            Timeline.CompilerTimeline.Instance.Initialize(package);
            DocumentLifetimeManager.Initialize(serviceProvider);
        }

        private void RefreshToolVisibility()
        {
            bool hasData = CompilerData.Instance.GetTotals().Count > 0; 
            placeholder.Visibility = hasData? Visibility.Collapsed : Visibility.Visible;
            overview.Visibility    = hasData? Visibility.Visible   : Visibility.Collapsed;
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
            System.Windows.Application.Current.Shutdown();
        }
        
    }
}
