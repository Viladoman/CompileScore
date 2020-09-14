namespace CompileScore.Overview
{
    using EnvDTE;
    using Microsoft.VisualStudio.PlatformUI;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    /// <summary>
    /// Interaction logic for OverviewWindowControl.
    /// </summary>
    public partial class OverviewWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OverviewWindowControl"/> class.
        /// </summary>
        public OverviewWindowControl()
        {
            this.InitializeComponent();
            CompilerData.Instance.ScoreDataChanged += RefreshTabs;

            //Initialize Tabs
            for (CompilerData.CompileCategory category = 0; category < CompilerData.CompileCategory.GahterCount; ++category)
            {
                AddTab(category);
            }
            RefreshTabs();
        }

        private void AddTab(CompilerData.CompileCategory category)
        {
            TabItem tab = new TabItem();
            tab.Header = CompileScore.Common.UIConverters.ToSentenceCase(Enum.GetName(typeof(CompilerData.CompileCategory), (int)category));

            CompileDataTable content = new CompileDataTable();
            content.SetCategory(category);
            tab.Content = content;
            tabControl.Items.Add(tab);
        }

        public void RefreshTabs()
        {
            int baseIndex = tabControl.Items.Count - (int)CompilerData.CompileCategory.GahterCount;
            if (baseIndex >= 0)
            {
                //We assume the last tabs are the one for the categories
                for (CompilerData.CompileCategory category = 0; category < CompilerData.CompileCategory.GahterCount; ++category)
                {
                    int index = baseIndex + (int)category;
                    (tabControl.Items[index] as TabItem).IsEnabled = CompilerData.Instance.GetCollection(category).Count > 0;
                }
            }
        }

    }
}