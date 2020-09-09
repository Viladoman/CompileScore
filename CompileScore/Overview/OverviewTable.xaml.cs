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

        private static CompilerData.CompileCategory[] columnDisplay = new CompilerData.CompileCategory[] { 
            CompilerData.CompileCategory.ExecuteCompiler,
            CompilerData.CompileCategory.FrontEnd,
            CompilerData.CompileCategory.BackEnd,
            CompilerData.CompileCategory.Include,
            CompilerData.CompileCategory.ParseClass,
            CompilerData.CompileCategory.ParseTemplate,
            CompilerData.CompileCategory.InstanceClass,
            CompilerData.CompileCategory.InstanceFunction,
            CompilerData.CompileCategory.PendingInstantiations,
            CompilerData.CompileCategory.CodeGeneration,
            CompilerData.CompileCategory.RunPass,
            CompilerData.CompileCategory.OptimizeModule,
            CompilerData.CompileCategory.OptimizeFunction,
            CompilerData.CompileCategory.Other,
        };

        public OverviewTable()
        {
            InitializeComponent();

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;

            foreach (CompilerData.CompileCategory category in columnDisplay)
            {
                CreateColumn(category);
            }
        }

        private string GetHeaderStr(CompilerData.CompileCategory category)
        {
            switch (category)
            {
                case CompilerData.CompileCategory.ExecuteCompiler: return "Duration";
                case CompilerData.CompileCategory.FrontEnd:        return "Frontend";
                case CompilerData.CompileCategory.BackEnd:         return "Backend";
                default: return CompileScore.Common.UIConverters.ToSentenceCase(category.ToString());
            }
        }

        private void CreateColumn(CompilerData.CompileCategory category)
        {
            string header = GetHeaderStr(category);
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

        private static bool FilterCompileValue(FullUnitValue value, string filterText)
        {
            return value.Name.Contains(filterText);
        }

        private void UpdateFilterFunction()
        {
            string filterText = searchTextBox.Text.ToLower();
            this.dataView.Filter = d => FilterCompileValue((FullUnitValue)d, filterText);
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
            DataGridRow row = (sender as DataGridRow);
            if (row == null) return;

            FullUnitValue value = (row.Item as FullUnitValue);
            if (value == null) return;

            //TODO ~ ramonv ~ go to CompileTimeline and set it up for the timeline ( missing data for this )
        }
    }
}
