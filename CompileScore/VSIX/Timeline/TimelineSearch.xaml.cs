using System;
using System.Collections.Generic;
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

namespace CompileScore.Timeline
{
    /// <summary>
    /// Interaction logic for TimelineSearch.xaml
    /// </summary>
    public partial class TimelineSearch : UserControl
    {
        private TimelineNode NodeData { set; get; }

        public TimelineSearch()
        {
            InitializeComponent();
        }

        public void SetData(TimelineNode root)
        {
            NodeData = root;
        }

        private void OpenAutoSuggestionBox()
        {
            autoListPopup.Visibility = Visibility.Visible;
            autoListPopup.IsOpen = true;
            autoList.Visibility = Visibility.Visible;
        }

        private void CloseAutoSuggestionBox()
        {
            autoListPopup.Visibility = Visibility.Collapsed;
            autoListPopup.IsOpen = false;
            autoList.Visibility = Visibility.Collapsed;
        }

        private void AutoTextBox_TextChanged(object sender, TextChangedEventArgs e)
        { 
            if (string.IsNullOrEmpty(this.autoTextBox.Text))
            {
                // No text close the popup
                CloseAutoSuggestionBox();
            }
            else
            {
                // Input text show suggestions
                OpenAutoSuggestionBox();

                //TODO ~ ramonv ~ this should check the Root node instead, to avoid the list 
                // Settings.  
                //autoList.ItemsSource = AutoSuggestionList.Where(p => p.ToLower().Contains(autoTextBox.Text.ToLower())).ToList();
            }
        }

        private void AutoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (autoList.SelectedIndex <= -1)
            {
                CloseAutoSuggestionBox();
            }
            else
            {
                CloseAutoSuggestionBox();
                autoTextBox.Text = autoList.SelectedItem.ToString();
                autoList.SelectedIndex = -1;

                //TODO ~ Ramonv ~ Notify selection and focus on the other side 
            }
        }

    }
}
