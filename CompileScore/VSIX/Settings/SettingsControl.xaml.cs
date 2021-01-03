using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace CompileScore
{
    public partial class SettingsControl : UserControl
    {
        private class UIConditionalField
        {
            public UIConditionalField(MethodInfo filter, UIElement element)
            {
                Filter = filter;
                Element = element;
            }

            public MethodInfo Filter { set; get; }
            public UIElement Element { set; get; }
        }
        
        public SolutionSettings Options { set; get; }
        private SettingsWindow Win { set; get; }
        private List<UIConditionalField> ConditionalFields { set; get; }

        public SettingsControl(SettingsWindow window, SolutionSettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Win = window;
            InitializeComponent();

            CreateGrid();
            Options = settings;
            RefreshConditionalFields(Options);
        }

        private void ObjectToUI(StackPanel panel, Type type, string prefix, List<UIConditionalField> conditionalFields)
        {
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                var customAttributes = (UIDescription[])property.GetCustomAttributes(typeof(UIDescription), true);
                UIDescription description = (customAttributes.Length > 0 && customAttributes[0] != null) ? customAttributes[0] : null;
                if (description == null) { continue; }

                string labelStr = description.Label == null ? property.Name : description.Label;

                string thisFullName = prefix + '.' + property.Name;

                bool isComplexObject = !property.PropertyType.IsEnum && !property.PropertyType.IsPrimitive && property.PropertyType != typeof(string);

                UIElement newElement;

                if (isComplexObject)
                {
                    var expander = new Expander();
                    var stackpanel = new StackPanel();
                    var headerLabel = new TextBlock();
                    headerLabel.Text = labelStr;
                    headerLabel.FontSize = 15;
                    headerLabel.Height = 25;
                    headerLabel.Background = this.Background;
                    headerLabel.Foreground = this.Foreground;

                    var header = new Grid();
                    header.Background = this.Background;
                    header.Children.Add(headerLabel);

                    expander.Content = stackpanel;
                    expander.Header = header;
                    expander.IsExpanded = true;

                    ObjectToUI(stackpanel, property.PropertyType, thisFullName, conditionalFields);
                    newElement = expander;
                }
                else
                {
                    var elementGrid = new Grid();
                    elementGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(170) });
                    elementGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                    var label = new Label();
                    label.Content = labelStr;
                    label.ToolTip = description.Tooltip;

                    Grid.SetColumn(label, 0);
                    elementGrid.Children.Add(label);

                    if (property.PropertyType == typeof(string))
                    {
                        var inputControl = new TextBox();
                        inputControl.VerticalAlignment = VerticalAlignment.Center;
                        inputControl.SetBinding(TextBox.TextProperty, new Binding(thisFullName));
                        inputControl.Margin = new Thickness(5);
                        Grid.SetColumn(inputControl, 1);
                        elementGrid.Children.Add(inputControl);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        var inputControl = new CheckBox();
                        inputControl.VerticalAlignment = VerticalAlignment.Center;
                        inputControl.SetBinding(CheckBox.IsCheckedProperty, new Binding(thisFullName));
                        inputControl.Margin = new Thickness(5);
                        Grid.SetColumn(inputControl, 1);
                        elementGrid.Children.Add(inputControl);
                    }
                    else if (property.PropertyType == typeof(uint) || property.PropertyType == typeof(int))
                    {
                        var inputControl = new TextBox();
                        inputControl.SetBinding(TextBox.TextProperty, new Binding(thisFullName));
                        inputControl.PreviewTextInput += NumberValidationTextBox;
                        inputControl.Margin = new Thickness(5);
                        Grid.SetColumn(inputControl, 1);
                        elementGrid.Children.Add(inputControl);
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        ComboBox inputControl = new ComboBox();
                        inputControl.ItemsSource = Enum.GetValues(property.PropertyType);
                        inputControl.SetBinding(ComboBox.SelectedValueProperty, new Binding(thisFullName));
                        inputControl.Margin = new Thickness(5);
                        inputControl.SelectionChanged += OnFieldChanged;
                        Grid.SetColumn(inputControl, 1);
                        elementGrid.Children.Add(inputControl);
                    }

                    newElement = elementGrid;
                }

                panel.Children.Add(newElement);

                if (!String.IsNullOrEmpty(description.FilterMethod))
                {
                    Type filtersType = typeof(UISettingsFilters);
                    MethodInfo filterMethod = filtersType.GetMethod(description.FilterMethod);

                    if (filterMethod != null)
                    {
                        conditionalFields.Add(new UIConditionalField(filterMethod,newElement));
                    }
                }
            }
        }

        private void OnFieldChanged(object sender, object e)
        {
            RefreshConditionalFields(Options);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void CreateGrid()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var ConditionalFields = new List<UIConditionalField>();
            ObjectToUI(optionStack, typeof(SolutionSettings), "Options", ConditionalFields);
        }

        private void RefreshConditionalFields(Object reference)
        {
            object[] arguments = { reference };
            foreach (UIConditionalField field in ConditionalFields)
            {
                field.Element.Visibility = (bool)field.Filter.Invoke(null, arguments) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyChanges()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var manager = SettingsManager.Instance;
            manager.Settings = Options;
            manager.Save();
        }

        public void ButtonSave_OnClick(object sender, object e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ApplyChanges();
            Win.Close();
        }

        private void ButtonDocumentation_OnClick(object sender, object e)
        {
            Documentation.OpenLink(Documentation.Link.GeneralConfiguration);
        }
    }
}
