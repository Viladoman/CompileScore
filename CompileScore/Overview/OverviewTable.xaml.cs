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
        private string searchTokens = "";

        public OverviewTable()
        {
            InitializeComponent();

            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        private static bool FilterCompileValue(FullUnitValue value, string tokens)
        {
            //TODO ~ ramonv ~ handle tokens 
            return value.Name.Contains(tokens);
        }

        private void OnDataChanged()
        {
            this.dataView = CollectionViewSource.GetDefaultView(CompilerData.Instance.GetUnits());
            this.dataView.Filter = d => FilterCompileValue((FullUnitValue)d, searchTokens);
            compilaDataGrid.ItemsSource = this.dataView;
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            this.searchTokens = SearchTextBox.Text.ToLower();
            this.dataView.Refresh();
        }
    }
}
