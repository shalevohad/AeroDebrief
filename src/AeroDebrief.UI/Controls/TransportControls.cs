using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AeroDebrief.UI.Controls
{
    public class TransportControls : UserControl
    {
        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(TransportControls),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(TransportControls),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty CurrentTimeProperty =
            DependencyProperty.Register(nameof(CurrentTime), typeof(TimeSpan), typeof(TransportControls),
                new PropertyMetadata(TimeSpan.Zero, OnTimeChanged));

        public static readonly DependencyProperty TotalTimeProperty =
            DependencyProperty.Register(nameof(TotalTime), typeof(TimeSpan), typeof(TransportControls),
                new PropertyMetadata(TimeSpan.Zero, OnTimeChanged));

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(TransportControls),
                new PropertyMetadata(0.0, OnProgressChanged));

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        public TimeSpan CurrentTime
        {
            get => (TimeSpan)GetValue(CurrentTimeProperty);
            set => SetValue(CurrentTimeProperty, value);
        }

        public TimeSpan TotalTime
        {
            get => (TimeSpan)GetValue(TotalTimeProperty);
            set => SetValue(TotalTimeProperty, value);
        }

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public event EventHandler? PlayRequested;
        public event EventHandler? PauseRequested;
        public event EventHandler? StopRequested;
        public event EventHandler<double>? SeekRequested;

        private Button? _playPauseButton;
        private Button? _stopButton;
        private Slider? _progressSlider;
        private TextBlock? _currentTimeText;
        private TextBlock? _totalTimeText;
        private bool _isUserSeeking;

        // Icon references (created per-button)
        private UIElement? _playIconForPlayButton;
        private UIElement? _pauseIconForPlayButton;
        private UIElement? _stopIconForStopButton;

        public TransportControls()
        {
            InitializeComponent();
            UpdateButtonStates();
        }

        private void InitializeComponent()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Play/Pause Button
            _playPauseButton = new Button
            {
                Style = (Style)FindResource("ModernButton"),
                Width = 50,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _playPauseButton.Click += OnPlayPauseClick;
            Grid.SetColumn(_playPauseButton, 0);
            grid.Children.Add(_playPauseButton);

            // Stop Button
            _stopButton = new Button
            {
                Style = (Style)FindResource("ModernSecondaryButton"),
                Width = 50,
                Height = 34,
                Margin = new Thickness(0, 0, 16, 0)
            };
            _stopButton.Click += OnStopClick;
            Grid.SetColumn(_stopButton, 1);
            grid.Children.Add(_stopButton);

            // Current Time
            _currentTimeText = new TextBlock
            {
                Style = (Style)FindResource("MonospaceTextStyle"),
                Text = "00:00:00",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(_currentTimeText, 2);
            grid.Children.Add(_currentTimeText);

            // Progress Slider
            _progressSlider = new Slider
            {
                Style = (Style)FindResource("ModernSlider"),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _progressSlider.PreviewMouseDown += OnProgressSliderMouseDown;
            _progressSlider.PreviewMouseUp += OnProgressSliderMouseUp;
            _progressSlider.ValueChanged += OnProgressSliderValueChanged;
            Grid.SetColumn(_progressSlider, 3);
            grid.Children.Add(_progressSlider);

            // Total Time
            _totalTimeText = new TextBlock
            {
                Style = (Style)FindResource("MonospaceTextStyle"),
                Text = "00:00:00",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_totalTimeText, 4);
            grid.Children.Add(_totalTimeText);

            Content = grid;

            // Handle property changes
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Create icons bound to the buttons' Foreground so they follow theme colors
            if (_playPauseButton != null)
            {
                _playIconForPlayButton = CreatePlayIcon(_playPauseButton);
                _pauseIconForPlayButton = CreatePauseIcon(_playPauseButton);
                _playPauseButton.Content = IsPlaying ? _pauseIconForPlayButton : _playIconForPlayButton;
            }

            if (_stopButton != null)
            {
                _stopIconForStopButton = CreateStopIcon(_stopButton);
                _stopButton.Content = _stopIconForStopButton;
            }

            UpdateButtonStates();
            UpdateTimeDisplay();
            UpdateProgressSlider();
        }

        private void OnPlayPauseClick(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
            {
                PauseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                PlayRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnProgressSliderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isUserSeeking = true;
        }

        private void OnProgressSliderMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isUserSeeking = false;
            
            if (_progressSlider != null)
            {
                SeekRequested?.Invoke(this, _progressSlider.Value / 100.0);
            }
        }

        private void OnProgressSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUserSeeking && _progressSlider != null)
            {
                // Update time display during seeking
                var seekTime = TimeSpan.FromTicks((long)(TotalTime.Ticks * (_progressSlider.Value / 100.0)));
                if (_currentTimeText != null)
                {
                    _currentTimeText.Text = FormatTime(seekTime);
                }
            }
        }

        private void UpdateButtonStates()
        {
            if (_playPauseButton == null || _stopButton == null)
                return;

            // Update play/pause icon
            _playPauseButton.Content = IsPlaying ? _pauseIconForPlayButton : _playIconForPlayButton;

            // Ensure stop button shows stop icon and enable state
            _stopButton.Content = _stopIconForStopButton;

            if (IsPlaying)
            {
                _playPauseButton.IsEnabled = true;
                _stopButton.IsEnabled = true;
            }
            else
            {
                _playPauseButton.IsEnabled = true;
                _stopButton.IsEnabled = false;
            }
        }

        private void UpdateTimeDisplay()
        {
            if (_currentTimeText == null || _totalTimeText == null)
                return;

            if (!_isUserSeeking)
            {
                _currentTimeText.Text = FormatTime(CurrentTime);
            }
            
            _totalTimeText.Text = FormatTime(TotalTime);
        }

        private void UpdateProgressSlider()
        {
            if (_progressSlider == null || _isUserSeeking)
                return;

            _progressSlider.Value = Math.Clamp(Progress, 0, 100);
        }

        private static string FormatTime(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        private UIElement CreatePlayIcon(Button target)
        {
            var path = new Path
            {
                Data = Geometry.Parse("M 4 2 L 4 30 L 28 16 Z"),
                Stretch = Stretch.Uniform,
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Bind fill to button Foreground
            path.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = target });

            var viewbox = new Viewbox { Child = path, Width = 20, Height = 20 };
            return viewbox;
        }

        private UIElement CreatePauseIcon(Button target)
        {
            var grid = new Grid { Width = 20, Height = 20 };
            var rect1 = new Rectangle { Width = 6, Height = 20, RadiusX = 1, RadiusY = 1 };
            var rect2 = new Rectangle { Width = 6, Height = 20, RadiusX = 1, RadiusY = 1, Margin = new Thickness(10,0,0,0) };
            // Bind fills
            rect1.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = target });
            rect2.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = target });
            grid.Children.Add(rect1);
            grid.Children.Add(rect2);
            var viewbox = new Viewbox { Child = grid, Width = 20, Height = 20 };
            return viewbox;
        }

        private UIElement CreateStopIcon(Button target)
        {
            var rect = new Rectangle
            {
                Width = 16,
                Height = 16,
                RadiusX = 1,
                RadiusY = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rect.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = target });
            var viewbox = new Viewbox { Child = rect, Width = 20, Height = 20 };
            return viewbox;
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControls control)
            {
                control.UpdateButtonStates();
            }
        }

        private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControls control)
            {
                control.UpdateTimeDisplay();
            }
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControls control)
            {
                control.UpdateProgressSlider();
            }
        }
    }
}