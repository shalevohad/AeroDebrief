using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Converters
{
    /// <summary>
    /// Converts PlayerMode to icon emoji
    /// </summary>
    public class ModeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlayerMode mode)
            {
                return mode switch
                {
                    PlayerMode.Recording => "??",
                    PlayerMode.Playback => "??",
                    PlayerMode.Idle => "??",
                    _ => "?"
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts PlayerMode to color brush
    /// </summary>
    public class ModeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlayerMode mode)
            {
                return mode switch
                {
                    PlayerMode.Recording => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    PlayerMode.Playback => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    PlayerMode.Idle => new SolidColorBrush(Color.FromRgb(96, 125, 139)), // Gray
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts PlayerMode to status bar color
    /// </summary>
    public class ModeStatusBarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlayerMode mode)
            {
                return mode switch
                {
                    PlayerMode.Recording => new SolidColorBrush(Color.FromRgb(198, 40, 40)), // Dark Red
                    PlayerMode.Playback => new SolidColorBrush(Color.FromRgb(56, 142, 60)), // Dark Green
                    PlayerMode.Idle => new SolidColorBrush(Color.FromRgb(69, 90, 100)), // Dark Gray
                    _ => new SolidColorBrush(Colors.DarkGray)
                };
            }
            return new SolidColorBrush(Colors.DarkGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts boolean for visibility binding
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts PlaybackState to play/pause icon path data
    /// </summary>
    public class PlayPauseIconConverter : IValueConverter
    {
        // Play icon: triangle
        private const string PlayIcon = "M 0,0 L 0,10 L 8,5 Z";
        
        // Pause icon: two bars
        private const string PauseIcon = "M 0,0 L 3,0 L 3,10 L 0,10 Z M 7,0 L 10,0 L 10,10 L 7,10 Z";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlaybackState state)
            {
                return state == PlaybackState.Playing ? PauseIcon : PlayIcon;
            }
            return PlayIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts PlaybackState to tooltip text
    /// </summary>
    public class PlayPauseTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPaused)
            {
                return isPaused ? "Resume" : "Play";
            }
            return "Play";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts pan value (-1 to 1) to display string
    /// </summary>
    public class PanDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float pan)
            {
                if (Math.Abs(pan) < 0.01f) return "C";
                if (pan < 0) return $"L{Math.Abs(pan) * 100:F0}";
                return $"R{pan * 100:F0}";
            }
            return "C";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts string to visibility (visible if not empty)
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null to Collapsed, non-null to Visible
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts zero count to visible (for empty state display)
    /// </summary>
    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
