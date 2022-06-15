using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CompileScore.Overview
{
    /// <summary>
    /// Interaction logic for OverviewTable.xaml
    /// </summary>
    public partial class OverviewTable : UserControl
    {
        private ICollectionView dataView;

        private int originalColumns = 0;

        public OverviewTable()
        {
            InitializeComponent();

            compileDataGrid.MouseRightButtonDown += DataGridRow_ContextMenu;

            originalColumns = compileDataGrid.Columns.Count;

            foreach (CompilerData.CompileCategory category in Common.Order.CategoryDisplay)
            {
                CreateColumn(category);
            }
            
            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        private void CreateColumn(CompilerData.CompileCategory category)
        {
            string header = Common.UIConverters.GetHeaderStr(category);
            string bindingText = "ValuesList[" + (int)category + "]";
            
            Binding binding = new Binding(bindingText);
            binding.Converter = this.Resources["uiTimeConverter"] as IValueConverter;

            var textColumn = new DataGridTextColumn();
            textColumn.Binding = binding;
            textColumn.Header = header;
            textColumn.IsReadOnly = true;
            textColumn.Width = Math.Max(75,header.Length * 8);
            compileDataGrid.Columns.Add(textColumn);
        }

        private void RefreshColumns()
        {
            int index = originalColumns;
            foreach (CompilerData.CompileCategory category in Common.Order.CategoryDisplay)
            {
                UnitTotal total = CompilerData.Instance.GetTotal(category);
                compileDataGrid.Columns[index].Visibility = total != null && total.Total > 0 ? Visibility.Visible : Visibility.Collapsed;
                ++index;
            }
        }

        private static bool FilterCompileValue(UnitValue value, string filterText)
        {
            return value.Name.Contains(filterText);
        }

        private void UpdateFilterFunction()
        {
            string filterText = searchTextBox.Text.ToLower();
            this.dataView.Filter = d => FilterCompileValue((UnitValue)d, filterText);
        }

        private void OnDataChanged()
        {
            RefreshColumns();
            this.dataView = CollectionViewSource.GetDefaultView(CompilerData.Instance.GetUnits());
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

            UnitValue value = (row.Item as UnitValue);
            if (value == null) return;

            Timeline.CompilerTimeline.Instance.DisplayTimeline(value);
        }

        private void DataGridRow_ContextMenu(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dataGrid = (DataGrid)sender;
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, Mouse.GetPosition(dataGrid));
            DataGridRow row = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
            if (row == null) return;

            dataGrid.SelectedItem = row.Item;
            UnitValue value = (row.Item as UnitValue);
            if (value == null) return;

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            bool isVisualStudio = EditorContext.IsEnvironment(EditorContext.ExecutionEnvironment.VisualStudio);

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value)));

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open File", (a, b) => EditorUtils.OpenFile(value)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Full Path", (a, b) => Clipboard.SetText(CompilerData.Instance.Folders.GetUnitPathSafe(value))));

            if (value.Name.Length > 0)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Name", (a, b) => Clipboard.SetText(value.Name)));
            }

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }
    }
}
