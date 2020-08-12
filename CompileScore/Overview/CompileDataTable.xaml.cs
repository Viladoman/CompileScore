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
        private string searchTokens = "";

        public CompileDataTable()
        {
            InitializeComponent();

            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        public CompilerData.CompileCategory Category { set; get; }

        private static bool FilterCompileValue(CompileValue value, string tokens)
        {
            //TODO ~ ramonv ~ handle tokens 
            return value.Name.Contains(tokens);
        }

        private void OnDataChanged()
        {
            this.dataView = CollectionViewSource.GetDefaultView(CompilerData.Instance.GetCollection(Category));
            this.dataView.Filter = d => FilterCompileValue((CompileValue)d, searchTokens);
            compilaDataGrid.ItemsSource = this.dataView;
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            this.searchTokens = SearchTextBox.Text.ToLower();
            this.dataView.Refresh();
        }

       
    }
}
