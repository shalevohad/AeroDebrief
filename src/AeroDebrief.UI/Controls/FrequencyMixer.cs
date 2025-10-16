using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Controls
{
    public class FrequencyMixer : UserControl
    {
        public static readonly DependencyProperty ActiveFrequenciesProperty =
            DependencyProperty.Register(nameof(ActiveFrequencies), typeof(ObservableCollection<ActiveFrequencyViewModel>), 
                typeof(FrequencyMixer), new PropertyMetadata(null, OnActiveFrequenciesChanged));

        public ObservableCollection<ActiveFrequencyViewModel>? ActiveFrequencies
        {
            get => (ObservableCollection<ActiveFrequencyViewModel>?)GetValue(ActiveFrequenciesProperty);
            set => SetValue(ActiveFrequenciesProperty, value);
        }

        public event EventHandler<FrequencyGainChangedEventArgs>? GainChanged;
        public event EventHandler<FrequencyPanChangedEventArgs>? PanChanged;

        private ScrollViewer? _scrollViewer;
        private StackPanel? _stackPanel;

        public FrequencyMixer()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _stackPanel = new StackPanel { Orientation = Orientation.Vertical };
            
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _stackPanel
            };

            Content = _scrollViewer;
        }

        private static void OnActiveFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrequencyMixer mixer)
            {
                mixer.UpdateMixerControls();
            }
        }

        private void UpdateMixerControls()
        {
            if (_stackPanel == null || ActiveFrequencies == null)
                return;

            _stackPanel.Children.Clear();

            if (ActiveFrequencies.Count == 0)
            {
                var placeholder = new Border
                {
                    Style = (Style)FindResource("ModernCard"),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var placeholderContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var iconText = new TextBlock
                {
                    Text = "???",
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var titleText = new TextBlock
                {
                    Text = "No Active Frequencies",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var instructionText = new TextBlock
                {
                    Text = "Select frequencies from the tree above to control their mixing.\nEach selected frequency will appear here with individual gain and pan controls.",
                    FontSize = 12,
                    Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                };

                placeholderContent.Children.Add(iconText);
                placeholderContent.Children.Add(titleText);
                placeholderContent.Children.Add(instructionText);
                
                placeholder.Child = placeholderContent;
                _stackPanel.Children.Add(placeholder);
                return;
            }

            foreach (var frequency in ActiveFrequencies)
            {
                var control = CreateFrequencyControl(frequency);
                _stackPanel.Children.Add(control);
            }
        }

        private FrameworkElement CreateFrequencyControl(ActiveFrequencyViewModel frequency)
        {
            var border = new Border
            {
                Style = (Style)FindResource("ModernCard"),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with frequency name and activity indicator
            var headerPanel = new Grid();
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = frequency.DisplayName,
                Style = (Style)FindResource("SubHeaderTextStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            headerPanel.Children.Add(nameText);

            // Activity indicator (could be bound to real-time activity)
            var activityIndicator = new Border
            {
                Width = 12,
                Height = 12,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green when active
                CornerRadius = new CornerRadius(6), // Make it circular
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(activityIndicator, 1);
            headerPanel.Children.Add(activityIndicator);

            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Gain control
            var gainPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 8, 0, 8) };
            
            var gainHeader = new Grid();
            gainHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gainHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gainHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var gainLabel = new TextBlock
            {
                Text = "Gain",
                Style = (Style)FindResource("BodyTextStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(gainLabel, 0);
            gainHeader.Children.Add(gainLabel);

            var gainValue = new TextBlock
            {
                Text = $"{frequency.Gain:F2}",
                Style = (Style)FindResource("MonospaceTextStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(gainValue, 2);
            gainHeader.Children.Add(gainValue);

            var gainSlider = new Slider
            {
                Minimum = 0,
                Maximum = 2,
                Value = frequency.Gain,
                Style = (Style)FindResource("ModernSlider"),
                Margin = new Thickness(0, 4, 0, 0)
            };

            gainSlider.ValueChanged += (s, e) =>
            {
                frequency.Gain = (float)e.NewValue;
                gainValue.Text = $"{frequency.Gain:F2}";
                GainChanged?.Invoke(this, new FrequencyGainChangedEventArgs(frequency.Frequency, frequency.Gain));
            };

            gainPanel.Children.Add(gainHeader);
            gainPanel.Children.Add(gainSlider);
            Grid.SetRow(gainPanel, 1);
            grid.Children.Add(gainPanel);

            // Pan control
            var panPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 8) };
            
            var panHeader = new Grid();
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var panLabel = new TextBlock
            {
                Text = "Pan",
                Style = (Style)FindResource("BodyTextStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(panLabel, 0);
            panHeader.Children.Add(panLabel);

            var panValue = new TextBlock
            {
                Text = FormatPanValue(frequency.Pan),
                Style = (Style)FindResource("MonospaceTextStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(panValue, 2);
            panHeader.Children.Add(panValue);

            var panSlider = new Slider
            {
                Minimum = -1,
                Maximum = 1,
                Value = frequency.Pan,
                Style = (Style)FindResource("ModernSlider"),
                Margin = new Thickness(0, 4, 0, 0)
            };

            panSlider.ValueChanged += (s, e) =>
            {
                frequency.Pan = (float)e.NewValue;
                panValue.Text = FormatPanValue(frequency.Pan);
                PanChanged?.Invoke(this, new FrequencyPanChangedEventArgs(frequency.Frequency, frequency.Pan));
            };

            panPanel.Children.Add(panHeader);
            panPanel.Children.Add(panSlider);
            Grid.SetRow(panPanel, 2);
            grid.Children.Add(panPanel);

            // Quick controls panel
            var quickControlsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            var muteButton = new Button
            {
                Content = "Mute",
                Style = (Style)FindResource("ModernSecondaryButton"),
                Width = 60,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var soloButton = new Button
            {
                Content = "Solo",
                Style = (Style)FindResource("ModernSecondaryButton"),
                Width = 60,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var resetButton = new Button
            {
                Content = "Reset",
                Style = (Style)FindResource("ModernSecondaryButton"),
                Width = 60,
                Height = 28
            };

            // Wire up quick control events
            muteButton.Click += (s, e) =>
            {
                frequency.Gain = frequency.Gain > 0 ? 0 : 1.0f;
                GainChanged?.Invoke(this, new FrequencyGainChangedEventArgs(frequency.Frequency, frequency.Gain));
            };

            resetButton.Click += (s, e) =>
            {
                frequency.Gain = 1.0f;
                frequency.Pan = 0.0f;
                GainChanged?.Invoke(this, new FrequencyGainChangedEventArgs(frequency.Frequency, frequency.Gain));
                PanChanged?.Invoke(this, new FrequencyPanChangedEventArgs(frequency.Frequency, frequency.Pan));
                
                // Force UI update by recreating controls
                UpdateMixerControls();
            };

            quickControlsPanel.Children.Add(muteButton);
            quickControlsPanel.Children.Add(soloButton);
            quickControlsPanel.Children.Add(resetButton);

            Grid.SetRow(quickControlsPanel, 3);
            grid.Children.Add(quickControlsPanel);

            border.Child = grid;
            return border;
        }

        private static string FormatPanValue(float pan)
        {
            if (Math.Abs(pan) < 0.01f)
                return "Center";
            
            return pan > 0 ? $"R{pan:F2}" : $"L{Math.Abs(pan):F2}";
        }
    }

    public class FrequencyGainChangedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public float Gain { get; }

        public FrequencyGainChangedEventArgs(double frequency, float gain)
        {
            Frequency = frequency;
            Gain = gain;
        }
    }

    public class FrequencyPanChangedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public float Pan { get; }

        public FrequencyPanChangedEventArgs(double frequency, float pan)
        {
            Frequency = frequency;
            Pan = pan;
        }
    }
}