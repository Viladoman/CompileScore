using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace CompileScore.Overview
{
    /// <summary>
    /// Interaction logic for OverviewTable.xaml
    /// </summary>
    public partial class OverviewTable : UserControl
    {
        private ICollectionView dataView;

        public OverviewTable()
        {
            InitializeComponent();

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;

            foreach (CompilerData.CompileCategory category in Common.Order.CategoryDisplay)
            {
                CreateColumn(category);
            }
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

            DataGridRow row = (sender as DataGridRow);
            if (row == null) return;

            UnitValue value = (row.Item as UnitValue);
            if (value == null) return;

            Timeline.CompilerTimeline.Instance.DisplayTimeline(value);
        }
    }
}
