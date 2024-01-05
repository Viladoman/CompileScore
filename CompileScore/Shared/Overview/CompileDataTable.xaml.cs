using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CompileScore.Overview
{
    public partial class CompileDataTable : UserControl
    {
        private class IncludeViewerProxy
        {
            public IncludeViewerProxy(CompileValue original)
            {
                Value = original;
                FullPath = "...";
            }

            public CompileValue Value { get; } 
            public string FullPath { set;  get; }
        }

        private ICollectionView dataView;

        private System.Threading.CancellationTokenSource TokenSource = new System.Threading.CancellationTokenSource();
        private List<IncludeViewerProxy> originalIncludeValues = new List<IncludeViewerProxy>();
        private HashSet<CompileValue> FilterSet = new HashSet<CompileValue>();

        private CompilerData.CompileCategory Category { set; get; }

        public CompileDataTable(CompilerData.CompileCategory category)
        {
            InitializeComponent();

            compileDataGrid.MouseRightButtonDown += DataGridRow_ContextMenu;
            compileDataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

            SetCategory(category);
            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        public void SetCategory(CompilerData.CompileCategory category)
        {
            Category = category;

            string prefix = category == CompilerData.CompileCategory.Include ? "Value." : "";

            //Create columns
            CreateDataGridColumn("Name",               prefix + "Name",             300);
            CreateDataGridColumn("Units",              prefix + "UnitCount",        75);
            CreateDataGridColumn("Accumulated",        prefix + "Accumulated",      140, "uiTimeConverter");
            CreateDataGridColumn("Accumulated Self",   prefix + "SelfAccumulated",  140, "uiTimeConverter");
            CreateDataGridColumn("Accumulated Unit",   prefix + "UnitAccumulated",  140, "uiTimeConverterZero");
            CreateDataGridColumn("Max",                prefix + "Max",              80,  "uiTimeConverter");
            CreateDataGridColumn("Max Self",           prefix + "SelfMax",          80,  "uiTimeConverter");
            CreateDataGridColumn("Min",                prefix + "Min",              80,  "uiTimeConverter");
            CreateDataGridColumn("Avg",                prefix + "Average",          80,  "uiTimeConverter");
            CreateDataGridColumn("Max location",       prefix + "MaxUnit.Name",     170);
            CreateDataGridColumn("Max Self location",  prefix + "SelfMaxUnit.Name", 170);

            if (category == CompilerData.CompileCategory.Include)
            {
                CreateDataGridColumn("Full Path", "FullPath", 600);
                searchTextTarget.Visibility = Visibility.Visible;
            }
            else
            {
                searchTextTarget.Visibility = Visibility.Collapsed;
            }
        }

        private void CreateDataGridColumn(string header, string bindingName, DataGridLength width, string converter = null)
        {
            Binding binding = bindingName == null? new Binding() : new Binding(bindingName);
            if (converter != null)
            {
                binding.Converter = this.Resources[converter] as IValueConverter;
            }

            var textColumn = new DataGridTextColumn();
            textColumn.Binding = binding;
            textColumn.Header = header;
            textColumn.IsReadOnly = true;
            textColumn.Width = width;
            compileDataGrid.Columns.Add(textColumn);
        }

        private static bool FilterTextValue(string value, string[] filterWords)
        {
            bool result = true;
            foreach(string word in filterWords)
            {
                result = result && value.ContainsNoCase(word);
            }
            return result;
        }

        private async System.Threading.Tasks.Task FilterEntriesAsync(string filterText, bool targetFullPath, System.Threading.CancellationToken token)
        {
            HashSet<CompileValue> newSet; 

            if (Category == CompilerData.CompileCategory.Include)
            {
                newSet = new HashSet<CompileValue>(originalIncludeValues.Count);

                if (filterText.Length > 0)
                {
                    string[] filterWords = filterText.Split(' ');
                    foreach (IncludeViewerProxy value in originalIncludeValues)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!FilterTextValue(targetFullPath? value.FullPath : value.Value.Name, filterWords))
                        {
                            newSet.Add(value.Value);
                        }
                    }
                }
            }
            else
            {
                List<CompileValue> collectionValues = CompilerData.Instance.GetCollection(Category);
                newSet = new HashSet<CompileValue>(collectionValues.Count);

                if (filterText.Length > 0)
                {
                    string[] filterWords = filterText.Split(' ');
                    foreach (CompileValue value in collectionValues)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!FilterTextValue(value.Name, filterWords))
                        {
                            newSet.Add(value);
                        }
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

        public async System.Threading.Tasks.Task SearchAsync(string filterText, bool targetFullPath)
        {
            var newTokenSource = new System.Threading.CancellationTokenSource();
            var oldTokenSource = System.Threading.Interlocked.Exchange(ref TokenSource, newTokenSource);

            if (!oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
            }

            try
            {
                await ThreadUtils.ForkAsync(() => FilterEntriesAsync(filterText, targetFullPath, newTokenSource.Token));
            }
            catch (System.OperationCanceledException)
            {
                //Search got cancelled
            }
        }

        private async System.Threading.Tasks.Task PopulateProxyDataAsync(List<IncludeViewerProxy> data)
        {
            foreach (IncludeViewerProxy val in data)
            {
                val.FullPath = CompilerData.Instance.Folders.GetValuePathSafe(val.Value);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (dataView != null)
            {
                dataView.Refresh();
                RefreshSearch();
            }
        }

        private object CreateCollection()
        {
            if (Category == CompilerData.CompileCategory.Include)
            {
                List<CompileValue> originalValues = CompilerData.Instance.GetCollection(Category);

                originalIncludeValues = new List<IncludeViewerProxy>(originalValues.Count);

                foreach(CompileValue val in originalValues)
                {
                    originalIncludeValues.Add(new IncludeViewerProxy(val));
                }

                ThreadUtils.Fork( async delegate { await PopulateProxyDataAsync(originalIncludeValues); });

                return originalIncludeValues;
            }
            else
            {
                return CompilerData.Instance.GetCollection(Category);
            }
        }

        private void OnDataChanged()
        {
            dataView = CollectionViewSource.GetDefaultView(CreateCollection());

            if (Category == CompilerData.CompileCategory.Include)
            {
                dataView.Filter = d => !FilterSet.Contains(((IncludeViewerProxy)d).Value);
            }
            else
            {
                dataView.Filter = d => !FilterSet.Contains((CompileValue)d);
            }

            compileDataGrid.ItemsSource = dataView;

            RefreshSearch();
        }

        private void RefreshSearch()
        {
            _ = SearchAsync(searchTextBox.Text, searchTextTarget.IsChecked.HasValue && searchTextTarget.IsChecked.Value);
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            RefreshSearch();
        }

        private void SearchTextTargetChangedEventHandler(object sender, RoutedEventArgs e)
        {
            RefreshSearch();
        }

        private CompileValue GetValueFromRowItem(DataGridRow row)
        {
            if (row == null) return null;

            if (row.Item is IncludeViewerProxy)
            {
                return ((IncludeViewerProxy)row.Item).Value;
            }

            return (row.Item as CompileValue);
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ChangedButton != MouseButton.Left) return;

            CompileValue value = GetValueFromRowItem(sender as DataGridRow);
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

            CompileValue value = GetValueFromRowItem(row);
            if (value == null) return;

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Self Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.SelfMaxUnit, value)));

            if (Category == CompilerData.CompileCategory.Include)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph", (a,b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
#if VS17 || VS16
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Requirements Graph", (a, b) => ParserData.DisplayRequirements(CompilerData.Instance.Folders.GetValuePathSafe(value))));
#endif
            }

            if (Category == CompilerData.CompileCategory.Include)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open File", (a, b) => EditorUtils.OpenFile(value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Full Path", (a, b) => Clipboard.SetDataObject(CompilerData.Instance.Folders.GetValuePathSafe(value))));
            }

            if (value.Name.Length > 0)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Name", (a, b) => Clipboard.SetDataObject(value.Name)));
            }

            //TODO ~ add more options 

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }
    }
}
