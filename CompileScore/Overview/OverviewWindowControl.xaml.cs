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

            foreach (CompilerData.CompileCategory category in Enum.GetValues(typeof(CompilerData.CompileCategory)))
            {
                AddTab(category);
            }
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
    }
}