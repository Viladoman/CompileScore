namespace CompileScore.Overview
{
    using EnvDTE;
    using Microsoft.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.Shell;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;

    public partial class OverviewWindowControl : UserControl
    {
        public OverviewWindowControl()
        {
            this.InitializeComponent();

            //Initialize Tabs
            for (CompilerData.CompileCategory category = 0; (int)category < (int)CompilerData.CompileThresholds.Gather; ++category)
            {
                AddTab(category);
            }

            CompilerData.Instance.ScoreDataChanged += OnScoreDataChanged;
            OnScoreDataChanged();
        }

        private void OnScoreDataChanged()
        {
            RefreshInspection();
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

        private void RefreshTabs()
        {
            int baseIndex = tabControl.Items.Count - (int)CompilerData.CompileThresholds.Gather;
            if (baseIndex >= 0)
            {
                //We assume the last tabs are the one for the categories
                for (CompilerData.CompileCategory category = 0; (int)category < (int)CompilerData.CompileThresholds.Gather; ++category)
                {
                    int index = baseIndex + (int)category;
                    (tabControl.Items[index] as TabItem).Visibility = CompilerData.Instance.GetCollection(category).Count > 0? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void RefreshInspection()
        {
            //Refresh inspection text 
            var scorePath = CompilerData.Instance.GetScoreFullPath();
            string details = File.Exists(scorePath) ? " (" + File.GetLastWriteTime(scorePath) + ")" : " (Not found)";

            if (CompilerData.Instance.Source == CompilerData.DataSource.Forced && EditorContext.Environment == EditorContext.ExecutionEnvironment.VisualStudio)
            {
                inspectionTxt.Text = "Generated ";
                defaultButton.Visibility = Visibility.Visible;
            }
            else
            {
                inspectionTxt.Text = "Inspecting ";
                defaultButton.Visibility = Visibility.Collapsed;
            }

            inspectionTxt.Text += scorePath.Length > 0? scorePath + details : "-- empty --";
        }

        private void OnClick_BackToDefault(object sender, object args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            defaultButton.Visibility = Visibility.Collapsed;
            CompilerData.Instance.LoadDefaultSource();
        }

    }
}