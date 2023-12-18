using CompileScore.Common;
using CompileScore.Includers;
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
    public partial class IncludersTable : UserControl
    {
        private class IncluderProxyValue
        {
            public IncluderProxyValue( object includer, CompileValue includee, object value )
            {
                Includee = includee;
                Includer = includer;
                Value = value;
                FullPathIncludee = "...";
                FullPathIncluder = "...";
            }

            public CompileValue Includee { get; }
            public object Includer { get; }
            public object Value { get; }
            public UnitValue MaxUnit { set;  get; }
            public string FullPathIncludee { set; get; }
            public string FullPathIncluder { set; get; }

            public string IncluderName
            {
                get
                {
                    return Includer is CompileValue ? (Includer as CompileValue).Name : (Includer is UnitValue ? (Includer as UnitValue).Name : "-- unknown --");
                }
            }

            public ulong  Accumulated
            {
                get
                {
                    return Value is IncludersInclValue ? (Value as IncludersInclValue).Accumulated : (Value is IncludersUnitValue ? (Value as IncludersUnitValue).Duration : 0);
                }
            }

            public uint Max
            {
                get
                {
                    return Value is IncludersInclValue ? (Value as IncludersInclValue).Max : (Value is IncludersUnitValue ? (Value as IncludersUnitValue).Duration : 0);
                }
            }

            public uint Average
            {
                get
                {
                    return Value is IncludersInclValue ? (Value as IncludersInclValue).Average : (Value is IncludersUnitValue ? (Value as IncludersUnitValue).Duration : 0);
                }
            }

            public uint Count
            {
                get
                {
                    return Value is IncludersInclValue ? (Value as IncludersInclValue).Count : (Value is IncludersUnitValue ? 1u : 0u);
                }
            }
        }

        private ICollectionView dataView;

        private System.Threading.CancellationTokenSource TokenSource = new System.Threading.CancellationTokenSource();
        private List<IncluderProxyValue> OriginalValues = new List<IncluderProxyValue>();
        private HashSet<IncluderProxyValue> FilterSet = new HashSet<IncluderProxyValue>();

        public IncludersTable()
        {
            InitializeComponent();

            compileDataGrid.MouseRightButtonDown += DataGridRow_ContextMenu;
            compileDataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

            CreateFullPathColumn("Full Path Includee", "FullPathIncludee");
            CreateFullPathColumn("Full Path Includer", "FullPathIncluder");

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }
       

        private void CreateFullPathColumn(string header, string binding)
        {
            var textColumn = new DataGridTextColumn();
            textColumn.Header = header;
            textColumn.Binding = new Binding(binding);
            textColumn.IsReadOnly = true;
            textColumn.Width = 600;
            compileDataGrid.Columns.Add(textColumn);
        }

        private static bool FilterText(string value, string[] filterWords)
        {
            bool result = true;
            foreach (string word in filterWords)
            {
                result = result && value.ContainsNoCase(word);
            }
            return result;
        }

        private async System.Threading.Tasks.Task FilterEntriesAsync(string filterTextIncludee, bool targetFullPathIncludee, string filterTextIncluder, bool targetFullPathIncluder, System.Threading.CancellationToken token)
        {
            HashSet<IncluderProxyValue> newSet = new HashSet<IncluderProxyValue>(OriginalValues.Count);

            if (filterTextIncludee.Length > 0 || filterTextIncluder.Length > 0)
            {
                string[] filterIncludeeWords = filterTextIncludee.Split(' ');
                string[] filterIncluderWords = filterTextIncluder.Split(' ');
                foreach (IncluderProxyValue value in OriginalValues)
                {
                    token.ThrowIfCancellationRequested();

                    if (!FilterText(targetFullPathIncludee ? value.FullPathIncludee : value.Includee.Name, filterIncludeeWords) || 
                        !FilterText(targetFullPathIncluder ? value.FullPathIncluder : value.IncluderName, filterIncluderWords))
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

        public async System.Threading.Tasks.Task SearchAsync(string filterTextIncludee, bool targetFullPathIncludee, string filterTextIncluder, bool targetFullPathIncluder)
        {
            var newTokenSource = new System.Threading.CancellationTokenSource();
            var oldTokenSource = System.Threading.Interlocked.Exchange(ref TokenSource, newTokenSource);

            if (!oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
            }

            try
            {
                await ThreadUtils.ForkAsync(() => FilterEntriesAsync(filterTextIncludee, targetFullPathIncludee, filterTextIncluder, targetFullPathIncluder, newTokenSource.Token));
            }
            catch (System.OperationCanceledException)
            {
                //Search got cancelled
            }
        }

        private async System.Threading.Tasks.Task PopulateProxyDataAsync(List<IncluderProxyValue> data)
        {
            foreach (IncluderProxyValue val in data)
            {
                val.FullPathIncludee = CompilerData.Instance.Folders.GetValuePathSafe(val.Includee);

                if ( val.Includer is CompileValue)
                {
                    val.FullPathIncluder = CompilerData.Instance.Folders.GetValuePathSafe(val.Includer as CompileValue);
                }
                else if ( val.Includer is UnitValue)
                {
                    val.FullPathIncluder = CompilerData.Instance.Folders.GetUnitPathSafe(val.Includer as UnitValue);
                }
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (dataView != null)
            {
                dataView.Refresh();
            }
        }

        private void OnDataChanged()
        {
            //create proxy original structures for display
            IncludersDataChunk includersData = CompilerIncluders.Instance.GetIncluderData();

            if ( includersData == null )
            {
                OriginalValues = new List<IncluderProxyValue>();
            }
            else
            {
                OriginalValues = new List<IncluderProxyValue>(includersData.LinkCount);

                for ( int i = 0; i < includersData.Includers.Count; ++i ) 
                {
                    CompileValue includee = CompilerData.Instance.GetValue(CompilerData.CompileCategory.Include, i);
                    IncludersValue includersVal = includersData.Includers[i];

                    if (includersVal.Includes != null)
                    {
                        foreach ( IncludersInclValue inclValue in includersVal.Includes)
                        {
                            CompileValue includer = CompilerData.Instance.GetValue(CompilerData.CompileCategory.Include, (int)inclValue.Index);
                            if ( includer != null && includee != null )
                            {
                                IncluderProxyValue newEntry = new IncluderProxyValue(includer, includee, inclValue);
                                newEntry.MaxUnit = CompilerData.Instance.GetUnitByIndex(inclValue.MaxId);
                                OriginalValues.Add(newEntry);
                            }
                        }
                    }

                    if (includersVal.Units != null)
                    {
                        foreach (IncludersUnitValue inclValue in includersVal.Units)
                        {
                            UnitValue includer = CompilerData.Instance.GetUnitByIndex(inclValue.Index);
                            if (includer != null && includee != null)
                            {
                                IncluderProxyValue newEntry = new IncluderProxyValue(includer, includee, inclValue);
                                newEntry.MaxUnit = includer;
                                OriginalValues.Add(newEntry);
                            }
                        }
                    }
                }
            }

            ThreadUtils.Fork(async delegate { await PopulateProxyDataAsync(OriginalValues); });

            this.dataView = CollectionViewSource.GetDefaultView(OriginalValues);
            dataView.Filter = d => !FilterSet.Contains((IncluderProxyValue)d);
            compileDataGrid.ItemsSource = this.dataView;
        }
        private void RefreshSearch()
        {
            bool includeeTargetFullPath = searchTextTargetIncludee.IsChecked.HasValue && searchTextTargetIncludee.IsChecked.Value;
            bool includerTargetFullPath = searchTextTargetIncluder.IsChecked.HasValue && searchTextTargetIncluder.IsChecked.Value;
            _ = SearchAsync(searchIncludeeTextBox.Text, includeeTargetFullPath, searchIncluderTextBox.Text, includerTargetFullPath);
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            RefreshSearch();
        }

        private void SearchTextTargetChangedEventHandler(object sender, RoutedEventArgs e)
        {
            RefreshSearch();
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ChangedButton != MouseButton.Left) return;

            DataGridRow row = (sender as DataGridRow);
            if (row == null) return;

            IncluderProxyValue value = (row.Item as IncluderProxyValue);
            if (value == null) return;

            Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit,value.Includee);
        }

        private void DataGridRow_ContextMenu(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dataGrid = (DataGrid)sender;
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, Mouse.GetPosition(dataGrid));
            DataGridRow row = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
            if (row == null) return;

            dataGrid.SelectedItem = row.Item;
            IncluderProxyValue value = (row.Item as IncluderProxyValue);
            if (value == null) return;


            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value.Includee)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph", (a, b) => CompilerIncluders.Instance.DisplayIncluders(value.Includee)));

            contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open Includee File", (a, b) => EditorUtils.OpenFile(value.Includee)));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Includee Full Path", (a, b) => Clipboard.SetDataObject(CompilerData.Instance.Folders.GetValuePathSafe(value.Includee))));

            if (value.Includee.Name.Length > 0)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Includee Name", (a, b) => Clipboard.SetDataObject(value.Includee.Name)));
            }

            contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            if (value.Includer is UnitValue)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open Includer File", (a, b) => EditorUtils.OpenFile(value.Includer as UnitValue)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Includer Full Path", (a, b) => Clipboard.SetDataObject(CompilerData.Instance.Folders.GetUnitPathSafe(value.Includer as UnitValue))));
            }
            if (value.Includer is CompileValue)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open Includer File", (a, b) => EditorUtils.OpenFile(value.Includer as CompileValue)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Includer Full Path", (a, b) => Clipboard.SetDataObject(CompilerData.Instance.Folders.GetValuePathSafe(value.Includer as CompileValue))));
            }

            if (value.IncluderName.Length > 0)
            {
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Copy Includer Name", (a, b) => Clipboard.SetDataObject(value.IncluderName)));
            }

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }
    }
}

