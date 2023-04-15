using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CompileScore.Overview
{
    public partial class OverviewTable : UserControl
    {
        private class UnitProxyValue
        { 
            public UnitProxyValue(UnitValue unit)
            {
                Unit = unit;
                FullPath = "...";
            }

            public UnitValue Unit { get; }
            public string FullPath { set; get; }
        }

        private ICollectionView dataView;

        private System.Threading.CancellationTokenSource TokenSource = new System.Threading.CancellationTokenSource();
        private HashSet<UnitValue> FilterSet = new HashSet<UnitValue>();

        private int originalColumns = 0;

        public OverviewTable()
        {
            InitializeComponent();

            compileDataGrid.MouseRightButtonDown += DataGridRow_ContextMenu;
            compileDataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

            originalColumns = compileDataGrid.Columns.Count;

            foreach (CompilerData.CompileCategory category in Common.Order.CategoryDisplay)
            {
                CreateColumn(category);
            }

            CreateFullPathColumn();

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        private void CreateColumn(CompilerData.CompileCategory category)
        {
            string header = Common.UIConverters.GetHeaderStr(category);
            string bindingText = "Unit.ValuesList[" + (int)category + "]";
            
            Binding binding = new Binding(bindingText);
            binding.Converter = this.Resources["uiTimeConverter"] as IValueConverter;

            var textColumn = new DataGridTextColumn();
            textColumn.Binding = binding;
            textColumn.Header = header;
            textColumn.IsReadOnly = true;
            textColumn.Width = Math.Max(75,header.Length * 8);
            compileDataGrid.Columns.Add(textColumn);
        }

        private void CreateFullPathColumn()
        {
            var textColumn = new DataGridTextColumn();
            textColumn.Header = "Full Path";
            textColumn.Binding = new Binding("FullPath");
            textColumn.IsReadOnly = true;
            textColumn.Width = 600;
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

        private static bool FilterUnitValue(UnitValue value, string[] filterWords)
        {
            bool result = true;
            foreach (string word in filterWords)
            {
                result = result && value.Name.ContainsNoCase(word);
            }
            return result;
        }

        private async System.Threading.Tasks.Task FilterEntriesAsync(string filterText, System.Threading.CancellationToken token)
        {
            List<UnitValue> originalValues = CompilerData.Instance.GetUnits();
            HashSet<UnitValue> newSet = new HashSet<UnitValue>(originalValues.Count);

            if (filterText.Length > 0)
            {
                string[] filterWords = filterText.Split(' ');
                foreach (UnitValue value in originalValues)
                {
                    token.ThrowIfCancellationRequested();

                    if (!FilterUnitValue(value, filterWords))
                    {
                        newSet.Add(value);
                    }
                }
            }

            token.ThrowIfCancellationRequested();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //swap hashes and filter in dataview
            FilterSet = newSet;
            if (dataView != null)
            {
                dataView.Refresh();
            }
        }

        public async System.Threading.Tasks.Task SearchAsync(string filterText)
        {
            var newTokenSource = new System.Threading.CancellationTokenSource();
            var oldTokenSource = System.Threading.Interlocked.Exchange(ref TokenSource, newTokenSource);

            if (!oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
            }

            try
            {
                await ThreadUtils.ForkAsync(() => FilterEntriesAsync(filterText, newTokenSource.Token));
            }
            catch (System.OperationCanceledException)
            {
                //Search got cancelled
            }
        }

        private async System.Threading.Tasks.Task PopulateProxyDataAsync(List<UnitProxyValue> data)
        {
            foreach (UnitProxyValue val in data)
            {
                val.FullPath = CompilerData.Instance.Folders.GetUnitPathSafe(val.Unit);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (dataView != null)
            {
                dataView.Refresh();
            }
        }

        private void OnDataChanged()
        {
            RefreshColumns();

            //create proxy structures for display
            List<UnitValue> original = CompilerData.Instance.GetUnits();
            List<UnitProxyValue> proxy = new List<UnitProxyValue>(original.Count);
            foreach ( UnitValue value in original)
            {
                proxy.Add(new UnitProxyValue(value)); 
            }

            ThreadUtils.Fork(async delegate { await PopulateProxyDataAsync(proxy); });

            this.dataView = CollectionViewSource.GetDefaultView(proxy);
            dataView.Filter = d => !FilterSet.Contains(((UnitProxyValue)d).Unit);
            compileDataGrid.ItemsSource = this.dataView;
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            _ = SearchAsync(searchTextBox.Text);
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ChangedButton != MouseButton.Left) return;

            DataGridRow row = (sender as DataGridRow);
            if (row == null) return;

            UnitProxyValue value = (row.Item as UnitProxyValue);
            if (value == null) return;

            Timeline.CompilerTimeline.Instance.DisplayTimeline(value.Unit);
        }

        private void DataGridRow_ContextMenu(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dataGrid = (DataGrid)sender;
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, Mouse.GetPosition(dataGrid));
            DataGridRow row = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
            if (row == null) return;

            dataGrid.SelectedItem = row.Item;
            UnitProxyValue proxyValue = (row.Item as UnitProxyValue);
            if (proxyValue == null) return;

            UnitValue value = proxyValue.Unit;

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
