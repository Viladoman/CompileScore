using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace CompileScore.Overview
{
    /// <summary>
    /// Interaction logic for OverviewTotalsTable.xaml
    /// </summary>
    public partial class OverviewTotalsTable : UserControl
    {
        public OverviewTotalsTable()
        {
            InitializeComponent();

            RefreshWidths();
            listview.SizeChanged += RefreshWidths;
            listview.Loaded += RefreshWidths;

            OnDataChanged();
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
        }

        public void OnDataChanged()
        {
            CompileSession session = CompilerData.Instance.GetSession();
            UnitTotal compilerTotal = CompilerData.Instance.GetTotal(CompilerData.CompileCategory.ExecuteCompiler);
            float parallelizationRatio = compilerTotal != null && session.FullDuration > 0 ? (float)compilerTotal.Total / (float)session.FullDuration : 0;
            parallelizationRatio = Math.Max(parallelizationRatio, 1);

            buildTimeText.Text = Common.UIConverters.GetTimeStr(session.FullDuration,true);
            threadsText.Text = "Parallel Ratio: " + parallelizationRatio.ToString("n2") + "x";

            ObservableCollection<UnitTotal> totals = new ObservableCollection<UnitTotal>();
            foreach (CompilerData.CompileCategory category in Common.Order.CategoryDisplay)
            {
                UnitTotal total = CompilerData.Instance.GetTotal(category);
                if (total != null && total.Total > 0)
                {
                    totals.Add(total);
                }
            }
            listview.ItemsSource = totals;

            Visibility vis = totals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            sessionGrid.Visibility = vis;
            headerA.Visibility = vis;
            headerB.Visibility = vis;

            RefreshWidths();
        }
        
        private void RefreshWidths(object sender = null, object args = null)
        {
            GridView gView = listview.View as GridView;

            const double buffer = 10;

            var workingWidth = listview.ActualWidth - SystemParameters.VerticalScrollBarWidth;
            var usedWidth = buffer + gView.Columns[0].ActualWidth + gView.Columns[1].ActualWidth + gView.Columns[3].ActualWidth;

            gView.Columns[2].Width = Math.Max(0, workingWidth - usedWidth);
        }
    }
}
