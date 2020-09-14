using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public delegate void TimelineSearchSelectionEventHandler(object sender, string name);

    /// <summary>
    /// Interaction logic for TimelineSearch.xaml
    /// </summary>
    public partial class TimelineSearch : UserControl
    {
        public event TimelineSearchSelectionEventHandler OnSelection;

        private List<string> AutoSuggestionList = new List<string>();

        private TimelineNode NodeData { set; get; }

        public TimelineSearch()
        {
            InitializeComponent();
        }

        public void SetData(List<string> optionsList)
        {
            AutoSuggestionList = optionsList;
            RefreshSuggestions();
        }

        public void SetPlaceholderText(string text)
        {
            placeholderText.Text = text;
        }

        public void SetCurrentText(string text)
        {
            autoTextBox.Text = text;
            CloseAutoSuggestionBox();
            Keyboard.ClearFocus(); //TODO ~ ramonv ~ this does not work ( find a different solution ) 
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
            RefreshSuggestions();
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
                OnSelection.Invoke(this, autoTextBox.Text);
                autoList.SelectedIndex = -1;
            }
        }

        private void RefreshSuggestions()
        {
            if (string.IsNullOrEmpty(autoTextBox.Text))
            {
                // No text close the popup
                CloseAutoSuggestionBox();
            }
            else
            {
                // Input text show suggestions
                OpenAutoSuggestionBox();

                string filter = autoTextBox.Text.ToLower();
                autoList.ItemsSource = AutoSuggestionList.Where(p => p.ToLower().Contains(filter)).ToList();
            }
        }

    }
}
