﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        public void SetText(string text)
        {
            autoTextBox.Text = text;
            CloseAutoSuggestionBox();
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
                //Those are used for searching only so clear the text
                OnSelection.Invoke(this, autoList.SelectedItem.ToString());
                autoTextBox.Text = null; //autoList.SelectedItem.ToString();
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
