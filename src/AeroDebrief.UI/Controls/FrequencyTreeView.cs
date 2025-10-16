using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.Core.Models;

namespace AeroDebrief.UI.Controls
{
    public class FrequencyTreeView : UserControl
    {
        public static readonly DependencyProperty FrequenciesProperty =
            DependencyProperty.Register(nameof(Frequencies), typeof(ObservableCollection<FrequencyGroupViewModel>), 
                typeof(FrequencyTreeView), new PropertyMetadata(null, OnFrequenciesChanged));

        public ObservableCollection<FrequencyGroupViewModel>? Frequencies
        {
            get => (ObservableCollection<FrequencyGroupViewModel>?)GetValue(FrequenciesProperty);
            set => SetValue(FrequenciesProperty, value);
        }

        public event EventHandler<FrequencySelectionChangedEventArgs>? FrequencySelectionChanged;

        private TreeView? _treeView;

        public FrequencyTreeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _treeView = new TreeView
            {
                Style = TryFindResource("ModernTreeView") as Style
            };

            Content = _treeView;
        }

        private static void OnFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrequencyTreeView control)
            {
                control.UpdateTreeView();
            }
        }

        public void UpdateTreeView()
        {
            if (_treeView == null || Frequencies == null)
                return;

            _treeView.Items.Clear();

            foreach (var group in Frequencies)
            {
                var groupItem = new TreeViewItem
                {
                    Header = CreateGroupHeader(group),
                    IsExpanded = group.IsExpanded,
                    Style = TryFindResource("ModernTreeViewItem") as Style
                };

                foreach (var frequency in group.Frequencies)
                {
                    var freqItem = new TreeViewItem
                    {
                        Header = CreateFrequencyHeader(frequency),
                        Tag = frequency,
                        Style = TryFindResource("ModernTreeViewItem") as Style
                    };

                    groupItem.Items.Add(freqItem);
                }

                _treeView.Items.Add(groupItem);
            }
        }

        private FrameworkElement CreateGroupHeader(FrequencyGroupViewModel group)
        {
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var textBlock = new TextBlock
            {
                Text = group.Name,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(textBlock);
            return stackPanel;
        }

        private FrameworkElement CreateFrequencyHeader(FrequencyViewModel frequency)
        {
            var expander = new Expander
            {
                IsExpanded = false,
                Style = TryFindResource("ModernExpander") as Style
            };

            // Header with checkbox and frequency info
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var checkBox = new CheckBox
            {
                IsChecked = frequency.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            checkBox.Checked += (s, e) => OnFrequencyChecked(frequency, true);
            checkBox.Unchecked += (s, e) => OnFrequencyChecked(frequency, false);

            // Color indicator for waveform
            var colorIndicator = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(frequency.WaveformColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            var freqText = new TextBlock
            {
                Text = frequency.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var countText = new TextBlock
            {
                Text = $"({frequency.PacketCount} packets)",
                FontSize = 10,
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(checkBox);
            headerPanel.Children.Add(colorIndicator);
            headerPanel.Children.Add(freqText);
            headerPanel.Children.Add(countText);

            expander.Header = headerPanel;

            // Content with player details
            if (frequency.SourceData?.Players.Count > 0)
            {
                var playersPanel = new StackPanel { Margin = new Thickness(20, 5, 0, 5) };

                foreach (var player in frequency.SourceData.Players.OrderByDescending(p => p.PacketCount))
                {
                    var playerInfo = CreatePlayerInfoPanel(player);
                    playersPanel.Children.Add(playerInfo);
                }

                expander.Content = playersPanel;
            }

            return expander;
        }

        private FrameworkElement CreatePlayerInfoPanel(AeroDebrief.Core.Models.PlayerFrequencyInfo player)
        {
            var border = new Border
            {
                Style = TryFindResource("ModernCard") as Style,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Player name and aircraft
            var nameText = new TextBlock
            {
                Text = player.Name,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(nameText, 0);
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            // Packet count
            var packetText = new TextBlock
            {
                Text = $"{player.PacketCount} packets",
                FontSize = 11,
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(packetText, 0);
            Grid.SetColumn(packetText, 1);
            grid.Children.Add(packetText);

            // Coalition, aircraft, and time range
            var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            
            if (!string.IsNullOrEmpty(player.Coalition) && player.Coalition != "Unknown")
            {
                var coalitionBadge = new Border
                {
                    Background = GetCoalitionBrush(player.Coalition),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 4, 0)
                };

                var coalitionText = new TextBlock
                {
                    Text = player.Coalition,
                    FontSize = 9,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold
                };

                coalitionBadge.Child = coalitionText;
                detailsPanel.Children.Add(coalitionBadge);
            }

            if (!string.IsNullOrEmpty(player.Aircraft) && player.Aircraft != "Unknown")
            {
                var aircraftText = new TextBlock
                {
                    Text = player.Aircraft,
                    FontSize = 10,
                    Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                detailsPanel.Children.Add(aircraftText);
            }

            // Time range
            var duration = player.LastSeen - player.FirstSeen;
            var timeText = new TextBlock
            {
                Text = $"{player.FirstSeen:HH:mm:ss} - {player.LastSeen:HH:mm:ss} ({duration.TotalMinutes:F1}m)",
                FontSize = 9,
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };
            detailsPanel.Children.Add(timeText);

            Grid.SetRow(detailsPanel, 1);
            Grid.SetColumn(detailsPanel, 0);
            Grid.SetColumnSpan(detailsPanel, 2);
            grid.Children.Add(detailsPanel);

            border.Child = grid;
            return border;
        }

        private Brush GetCoalitionBrush(string coalition)
        {
            return coalition.ToLowerInvariant() switch
            {
                "red" => new SolidColorBrush(Color.FromRgb(211, 47, 47)),
                "blue" => new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                "neutral" => new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                _ => new SolidColorBrush(Color.FromRgb(117, 117, 117))
            };
        }

        private void OnFrequencyChecked(FrequencyViewModel frequency, bool isChecked)
        {
            frequency.IsSelected = isChecked;
            FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, isChecked));
        }

        public void SelectAll()
        {
            if (Frequencies == null)
                return;

            foreach (var group in Frequencies)
            {
                foreach (var frequency in group.Frequencies)
                {
                    if (!frequency.IsSelected)
                    {
                        frequency.IsSelected = true;
                        FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, true));
                    }
                }
            }

            UpdateTreeView();
        }

        public void SelectNone()
        {
            if (Frequencies == null)
                return;

            foreach (var group in Frequencies)
            {
                foreach (var frequency in group.Frequencies)
                {
                    if (frequency.IsSelected)
                    {
                        frequency.IsSelected = false;
                        FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, false));
                    }
                }
            }

            UpdateTreeView();
        }
    }

    public class FrequencySelectionChangedEventArgs : EventArgs
    {
        public FrequencyViewModel Frequency { get; }
        public bool IsSelected { get; }

        public FrequencySelectionChangedEventArgs(FrequencyViewModel frequency, bool isSelected)
        {
            Frequency = frequency;
            IsSelected = isSelected;
        }
    }
}