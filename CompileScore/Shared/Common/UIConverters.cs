using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CompileScore.Common
{
    static public class UIHelpers
    {
        public static T GetParentOfType<T>(this DependencyObject element) where T : DependencyObject
        {
            Type type = typeof(T);
            if (element == null) return null;
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            if (parent == null && ((FrameworkElement)element).Parent is DependencyObject) parent = ((FrameworkElement)element).Parent;
            if (parent == null) return null;
            else if (parent.GetType() == type || parent.GetType().IsSubclassOf(type)) return parent as T;
            return GetParentOfType<T>(parent);
        }   

        public static System.Windows.Forms.ToolStripMenuItem CreateContextItem(string label, EventHandler onClick)
        {
            var element = new System.Windows.Forms.ToolStripMenuItem(label);
            element.Click += onClick;
            return element;
        }

        public static void ShowDataGridItem(DataGrid dataGrid, object entry)
        {
            var collection = dataGrid.ItemsSource;
            Type collectionType = collection.GetType();
            Type itemType = collectionType.GetGenericArguments().Single();

            if (itemType != entry.GetType()) return;

            //search for item 
            for (int i = 0; i < dataGrid.Items.Count; ++i)
            {
                DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(i);
                if (row.Item == entry)
                {
                    dataGrid.SelectedItem = entry;
                    dataGrid.ScrollIntoView(entry);
                    row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    break;
                }
            }
        }
    }

    public static class Order
    {
        public static CompilerData.CompileCategory[] CategoryDisplay = new CompilerData.CompileCategory[] {
            CompilerData.CompileCategory.ExecuteCompiler,
            CompilerData.CompileCategory.FrontEnd,
            CompilerData.CompileCategory.BackEnd,
            CompilerData.CompileCategory.Include,
            CompilerData.CompileCategory.ParseClass,
            CompilerData.CompileCategory.ParseTemplate,
            CompilerData.CompileCategory.InstanceClass,
            CompilerData.CompileCategory.InstanceFunction,
            CompilerData.CompileCategory.PendingInstantiations,
            CompilerData.CompileCategory.CodeGeneration,
            CompilerData.CompileCategory.OptimizeModule,
            CompilerData.CompileCategory.OptimizeFunction,
            CompilerData.CompileCategory.Other,
        };
    }

    class UIConverters
    {
        static public string ToSentenceCase(string str)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {m.Value[1]}");
        }
        static public string GetTimeStr(ulong uSeconds, bool allowZero = false)
        {
            ulong ms = uSeconds / 1000;
            ulong us = uSeconds - (ms * 1000);
            ulong sec = ms / 1000;
            ms = ms - (sec * 1000);
            ulong min = sec / 60;
            sec = sec - (min * 60);
            ulong hour = min / 60;
            min = min - (hour * 60);
            ulong day = hour / 24;
            hour = hour - (day * 24);

            if (day > 0)  { return day + " d " + hour + " h "; }
            if (hour > 0) { return hour + " h " + min + " m"; }
            if (min > 0)  { return min  + " m " + sec + " s"; }
            if (sec > 0)  { return sec  + "." + ms.ToString().PadLeft(3, '0') + " s"; }
            if (ms > 0)   { return ms + "." + us.ToString().PadLeft(3, '0')+" ms"; }
            if (us > 0)   { return us + " μs"; }
            return allowZero? "-" : "< 1 μs";
        }
        public static string GetHeaderStr(CompilerData.CompileCategory category)
        {
            switch (category)
            {
                case CompilerData.CompileCategory.ExecuteCompiler: return "Duration";
                default: return CompileScore.Common.UIConverters.ToSentenceCase(category.ToString());
            }
        }
    }

    public class CategoryStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                return UIConverters.GetHeaderStr((CompilerData.CompileCategory)value);
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class CategoryColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                return Colors.GetCategoryBackground((CompilerData.CompileCategory)value);
            }
            catch (Exception)
            {
                return Brushes.Red;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class UITimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try 
            {
                return value is uint? UIConverters.GetTimeStr((uint)value) : UIConverters.GetTimeStr((ulong)value);
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class UITimeConverterZero : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                return value is uint ? UIConverters.GetTimeStr((uint)value,true) : UIConverters.GetTimeStr((ulong)value,true);
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class RatioToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                double val = (double)value;
                double percent = val * 100; 
                return val <= 0? "-" : (percent < 0.1? "<0.1" : percent.ToString("F1")) + '%';
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
