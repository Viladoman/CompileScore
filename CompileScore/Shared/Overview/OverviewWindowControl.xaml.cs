using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Overview
{
    public partial class OverviewWindowControl : UserControl
    {
        public OverviewWindowControl()
        {
            CompilerData compilerData = CompilerData.Instance;
            compilerData.Hydrate(CompilerData.HydrateFlag.Main);
            compilerData.Hydrate(CompilerData.HydrateFlag.Globals);

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
            if (category == CompilerData.CompileCategory.Include)
            {
                TabItem tab = new TabItem();
                tab.Header = "Include Global";
                tab.Content = new CompileDataTable(category);
                tabControl.Items.Add(tab);
            }
            else
            {
                TabItem tab = new TabItem();
                tab.Header = CompileScore.Common.UIConverters.ToSentenceCase(Enum.GetName(typeof(CompilerData.CompileCategory), (int)category));
                tab.Content = new CompileDataTable(category);
                tabControl.Items.Add(tab);
            }
        }

        private void AddIncludeSingleTab()
        {
            TabItem tab = new TabItem();
            tab.Header = "Include Single";
            tab.Content = new IncludersTable();
            tabControl.Items.Add(tab);
        }

        private void RefreshTabs()
        {
            bool validData = CompilerData.Instance.GetUnits().Count > 0;
            unitsTab.Visibility = validData ? Visibility.Visible : Visibility.Collapsed;
            includersTab.Visibility = validData && CompilerData.Instance.GetSession().Version >= 11 ? Visibility.Visible : Visibility.Collapsed;

            //We assume the last tabs are the one for the categories
            int baseIndex = tabControl.Items.Count - (int)CompilerData.CompileThresholds.Gather;
            if (baseIndex >= 0)
            {
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
                inspectionTxt.Text = "Inspecting Custom ";
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