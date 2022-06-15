using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CompileScore.Overview
{
    /// <summary>
    /// Interaction logic for CompileDataTable.xaml
    /// </summary>
    public partial class CompileDataTable : UserControl
    {
        private ICollectionView dataView;

        private CompilerData.CompileCategory Category { set; get; }

        public CompileDataTable()
        {
            InitializeComponent();

            compileDataGrid.MouseRightButtonDown += DataGridRow_ContextMenu;

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        public void SetCategory(CompilerData.CompileCategory category)
        {
            Category = category;
            OnDataChanged();
        }

        private static bool FilterCompileValue(CompileValue value, string filterText)
        {
            return value.Name.Contains(filterText);
        }

        private void UpdateFilterFunction()
        {
            string filterText = searchTextBox.Text.ToLower();
            this.dataView.Filter = d => FilterCompileValue((CompileValue)d, filterText);
        }

        private void OnDataChanged()
        {
            this.dataView = CollectionViewSource.GetDefaultView(CompilerData.Instance.GetCollection(Category));
            UpdateFilterFunction();
            compileDataGrid.ItemsSource = this.dataView;
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            UpdateFilterFunction();
            this.dataView.Refresh();
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ChangedButton != MouseButton.Left) return;

            DataGridRow row = (sender as DataGridRow);
            if (row == null) return;

            CompileValue value = (row.Item as CompileValue);
            if (value == null) return;

            Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit,value);
        }   
        
        private void DataGridRow_ContextMenu(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dataGrid = (DataGrid)sender;
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, Mouse.GetPosition(dataGrid));
            DataGridRow row = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
            if (row == null) return;

            dataGrid.SelectedItem = row.Item;

            CompileValue value = (row.Item as CompileValue);
            if (value == null) return;

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            bool isVisualStudio = EditorContext.IsEnvironment(EditorContext.ExecutionEnvironment.VisualStudio);

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));

            if (Category == CompilerData.CompileCategory.Include)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph", (a,b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
            }

            if (Category == CompilerData.CompileCategory.Include)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open File", (a, b) => EditorUtils.OpenFile(value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Full Path", (a, b) => Clipboard.SetText(CompilerData.Instance.Folders.GetValuePathSafe(CompilerData.CompileCategory.Include, value))));
            }

            if (value.Name.Length > 0)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Name", (a, b) => Clipboard.SetText(value.Name)));
            }

            //TODO ~ add more options 

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }

    }
}
