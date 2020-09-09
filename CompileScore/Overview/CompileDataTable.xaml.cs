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
    /// Interaction logic for CompileDataTable.xaml
    /// </summary>
    public partial class CompileDataTable : UserControl
    {
        private ICollectionView dataView;

        private CompilerData.CompileCategory Category { set; get; }

        public CompileDataTable()
        {
            InitializeComponent();

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
            compilaDataGrid.ItemsSource = this.dataView;
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

            CompileValue value = (row.Item as CompileValue);
            if (value == null) return;

            //TODO ~ ramonv ~ go to CompileTimeline and set it up for the timeline ( missing data for this )
        }

    }
}
