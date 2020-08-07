namespace CompileScore.Overview
{
    using Microsoft.VisualStudio.PlatformUI;
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    /// <summary>
    /// Interaction logic for OverviewWindowControl.
    /// </summary>
    public partial class OverviewWindowControl : UserControl
    {
        private ICollectionView includeView;
        private string searchTokens = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="OverviewWindowControl"/> class.
        /// </summary>
        public OverviewWindowControl()
        {
            this.InitializeComponent();

            this.includeView = CollectionViewSource.GetDefaultView(CompilerData.Instance.GetCollection(CompilerData.CompileCategory.Include));
            this.includeView.Filter = d => FilterCompileValue((CompileValue)d, searchTokens);
            includeGrid.ItemsSource = this.includeView;
        }

        private static bool FilterCompileValue(CompileValue value, string tokens)
        {
            //TODO ~ ramonv ~ handle tokens 
            return value.Name.Contains(tokens);
        }

        private void SearchTextChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            this.searchTokens = SearchTextBox.Text.ToLower();
            this.includeView.Refresh();
        }
        
    }
}