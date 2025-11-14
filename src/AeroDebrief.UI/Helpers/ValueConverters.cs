using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace AeroDebrief.UI.Helpers
{
    /// <summary>
    /// Common WPF value converters for use throughout the application
    /// </summary>
    
    /// <summary>
    /// Converts a boolean value to Visibility, inverting the logic
    /// (true -> Collapsed, false -> Visible)
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// Inverts a boolean value
    /// (true -> false, false -> true)
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts a boolean value to Visibility
    /// (true -> Visible, false -> Collapsed)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// Checks if a collection is null or empty
    /// Returns Visibility.Visible if null/empty, Collapsed otherwise
    /// </summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Visible;
            
            if (value is ICollection collection)
            {
                return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (value is IEnumerable enumerable)
            {
                // Check if enumerable has any items
                var enumerator = enumerable.GetEnumerator();
                bool hasItems = enumerator.MoveNext();
                if (enumerator is IDisposable disposable)
                    disposable.Dispose();
                
                return hasItems ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Checks if a collection is null or empty (INVERSE)
    /// Returns Visibility.Collapsed if null/empty, Visible otherwise
    /// </summary>
    public class NullOrEmptyToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;
            
            if (value is ICollection collection)
            {
                return collection.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            
            if (value is IEnumerable enumerable)
            {
                // Check if enumerable has any items
                var enumerator = enumerable.GetEnumerator();
                bool hasItems = enumerator.MoveNext();
                if (enumerator is IDisposable disposable)
                    disposable.Dispose();
                
                return hasItems ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Extracts the filename from a full file path
    /// </summary>
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    return Path.GetFileName(path);
                }
                catch
                {
                    return path;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Extracts the directory path from a full file path
    /// </summary>
    public class FilePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    var directory = Path.GetDirectoryName(path);
                    return string.IsNullOrEmpty(directory) ? path : directory;
                }
                catch
                {
                    return path;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
