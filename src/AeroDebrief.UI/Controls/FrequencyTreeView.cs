using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Helpers;
using AeroDebrief.Core.Models;
using FontAwesome.WPF;

namespace AeroDebrief.UI.Controls
{
    public class FrequencyTreeView : UserControl
    {
        public static readonly DependencyProperty FrequenciesProperty =
            DependencyProperty.Register(nameof(Frequencies), typeof(ObservableCollection<FrequencyGroupViewModel>), 
                typeof(FrequencyTreeView), new PropertyMetadata(null, OnFrequenciesChanged));

        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register(nameof(StatusMessage), typeof(string), 
                typeof(FrequencyTreeView), new PropertyMetadata(string.Empty));

        public ObservableCollection<FrequencyGroupViewModel>? Frequencies
        {
            get => (ObservableCollection<FrequencyGroupViewModel>?)GetValue(FrequenciesProperty);
            set => SetValue(FrequenciesProperty, value);
        }

        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        public event EventHandler<FrequencySelectionChangedEventArgs>? FrequencySelectionChanged;
        public event EventHandler<StatusMessageChangedEventArgs>? StatusMessageChanged;

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

        private void SetStatus(string message)
        {
            StatusMessage = message;
            StatusMessageChanged?.Invoke(this, new StatusMessageChangedEventArgs(message));
        }

        private static void OnFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrequencyTreeView control)
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Info($"📊 FrequencyTreeView.OnFrequenciesChanged called");
                logger.Info($"   ⏳ Old value: {(e.OldValue as ObservableCollection<FrequencyGroupViewModel>)?.Count ?? 0} groups");
                logger.Info($"   ✅ New value: {(e.NewValue as ObservableCollection<FrequencyGroupViewModel>)?.Count ?? 0} groups");
                
                // Unsubscribe from old collection
                if (e.OldValue is ObservableCollection<FrequencyGroupViewModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnFrequenciesCollectionChanged;
                }
                
                // Subscribe to new collection
                if (e.NewValue is ObservableCollection<FrequencyGroupViewModel> newCollection)
                {
                    newCollection.CollectionChanged += control.OnFrequenciesCollectionChanged;
                    
                    var totalFreqs = newCollection.Sum(g => g.Frequencies.Count);
                    control.SetStatus($"Loaded {newCollection.Count} groups with {totalFreqs} frequencies");
                }
                else
                {
                    control.SetStatus("Ready");
                }
                
                control.UpdateTreeView();
            }
        }

        private void OnFrequenciesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info($"📊 FrequencyTreeView.OnFrequenciesCollectionChanged: Action={e.Action}");
            
            if (e.NewItems != null)
            {
                logger.Info($"   ➕ Added {e.NewItems.Count} items");
                SetStatus($"Added {e.NewItems.Count} frequency groups");
            }
            if (e.OldItems != null)
            {
                logger.Info($"   ➖ Removed {e.OldItems.Count} items");
                SetStatus($"Removed {e.OldItems.Count} frequency groups");
            }
            
            UpdateTreeView();
        }

        public void UpdateTreeView()
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            logger.Info($"🚀 FrequencyTreeView.UpdateTreeView started");
            
            if (_treeView == null)
            {
                logger.Warn("   ❌ TreeView is NULL!");
                SetStatus("Error: TreeView not initialized");
                return;
            }
                
            if (Frequencies == null)
            {
                logger.Warn("   ❌ Frequencies collection is NULL!");
                SetStatus("No frequencies available");
                return;
            }

            // DEFENSIVE: Take a snapshot of the groups and each group's frequencies to avoid
            // concurrent modifications or lazy population changing counts between calls.
            var groupSnapshots = Frequencies
                .Select(g => new
                {
                    Group = g,
                    Frequencies = g.Frequencies?.ToList() ?? new List<FrequencyViewModel>()
                })
                .ToList();

            var totalFrequencies = groupSnapshots.Sum(s => s.Frequencies.Count);
            logger.Info($"   📦 BATCH RENDERING: {groupSnapshots.Count} groups with {totalFrequencies} total frequencies");
            SetStatus($"Rendering {totalFrequencies} frequencies...");
            
            logger.Info($"   🗑️ Clearing tree view (currently has {_treeView.Items.Count} items)");
            _treeView.Items.Clear();

            // OPTIMIZATION: Suspend layout during batch creation
            _treeView.BeginInit();
            
            try
            {
                foreach (var snapshot in groupSnapshots)
                {
                    var group = snapshot.Group;
                    var frequenciesSnapshot = snapshot.Frequencies;

                    var expectedFreqCount = frequenciesSnapshot.Count;
                    logger.Debug($"      🔧 Creating TreeViewItem for group: {group.Name} ({expectedFreqCount} frequencies)");

                    var groupItem = new TreeViewItem
                    {
                        Header = CreateGroupHeader(group),
                        IsExpanded = group.IsExpanded,
                        Style = TryFindResource("ModernTreeViewItem") as Style
                    };

                    // Iterate over the snapshot (safe from concurrent modification)
                    int actualCount = 0;
                    foreach (var frequency in frequenciesSnapshot)
                    {
                        if (frequency == null)
                        {
                            logger.Warn($"      ⚠️ NULL frequency in group {group.Name}");
                            continue;
                        }

                        try
                        {
                            var freqItem = CreateFrequencyTreeViewItem(frequency);
                            groupItem.Items.Add(freqItem);
                            actualCount++;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"      ❌ Failed to create TreeViewItem for frequency {frequency?.DisplayName}");
                        }
                    }

                    _treeView.Items.Add(groupItem);

                    if (actualCount != expectedFreqCount)
                    {
                        logger.Warn($"      ⚠️ FREQUENCY COUNT MISMATCH for {group.Name}: Expected {expectedFreqCount}, Got {actualCount}");
                    }

                    logger.Debug($"      ✅ Added group '{group.Name}' with {groupItem.Items.Count} frequency items");
                }
            }
            finally
            {
                // Resume layout after batch creation
                _treeView.EndInit();
            }
            
            sw.Stop();
            logger.Info($"   ⚡ Tree view now has {_treeView.Items.Count} top-level items (rendered in {sw.ElapsedMilliseconds}ms)");
            SetStatus($"Rendered {totalFrequencies} frequencies in {sw.ElapsedMilliseconds}ms");
        }

        private void OnFrequencyChecked(FrequencyViewModel frequency, bool isChecked)
        {
            frequency.IsSelected = isChecked;
            
            var action = isChecked ? "Showing" : "Hiding";
            SetStatus($"{action} frequency: {frequency.DisplayName}");
            
            FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, isChecked));
        }

        public void SelectAll()
        {
            if (Frequencies == null)
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var totalFreqs = Frequencies.Sum(g => g.Frequencies.Count);
            SetStatus($"Selecting all {totalFreqs} frequencies...");

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
            sw.Stop();
            SetStatus($"Selected all {totalFreqs} frequencies ({sw.ElapsedMilliseconds}ms)");
        }

        public void SelectNone()
        {
            if (Frequencies == null)
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var totalFreqs = Frequencies.Sum(g => g.Frequencies.Count);
            SetStatus($"Hiding all {totalFreqs} frequencies...");

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
            sw.Stop();
            SetStatus($"Hidden all {totalFreqs} frequencies ({sw.ElapsedMilliseconds}ms)");
        }

        /// <summary>
        /// Creates a lightweight TreeViewItem for a frequency with deferred expander content loading
        /// This dramatically speeds up initial rendering (10-50x faster!)
        /// </summary>
        private TreeViewItem CreateFrequencyTreeViewItem(FrequencyViewModel frequency)
        {
            var freqItem = new TreeViewItem
            {
                Tag = frequency,
                Style = TryFindResource("ModernTreeViewItem") as Style
            };

            // Create expander with lightweight header
            var expander = new Expander
            {
                IsExpanded = false,
                Style = TryFindResource("ModernExpander") as Style
            };

            // Lightweight header (no player info loaded yet)
            expander.Header = CreateFrequencyHeaderSimple(frequency);

            // CRITICAL OPTIMIZATION: Defer expensive content loading until expander is opened!
            bool contentLoaded = false;
            expander.Expanded += (s, e) =>
            {
                if (!contentLoaded)
                {
                    SetStatus($"Loading details for {frequency.DisplayName}...");
                    
                    // Load expensive content (player info, mixer controls) only when expanded
                    expander.Content = CreateFrequencyExpandedContent(frequency);
                    contentLoaded = true;
                    
                    SetStatus($"Loaded details for {frequency.DisplayName}");
                }
            };

            freqItem.Header = expander;
            return freqItem;
        }

        /// <summary>
        /// Creates a lightweight frequency header (fast!)
        /// Only checkbox, color indicator, name, and packet count
        /// </summary>
        private FrameworkElement CreateFrequencyHeaderSimple(FrequencyViewModel frequency)
        {
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Bind the checkbox to the frequency's IsSelected property
            var binding = new System.Windows.Data.Binding("IsSelected")
            {
                Source = frequency,
                Mode = System.Windows.Data.BindingMode.TwoWay
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);

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

            return headerPanel;
        }

        /// <summary>
        /// Creates the expensive expanded content (mixer controls + player info)
        /// Only loaded when user expands the frequency (lazy loading!)
        /// </summary>
        private FrameworkElement CreateFrequencyExpandedContent(FrequencyViewModel frequency)
        {
            var contentPanel = new StackPanel { Margin = new Thickness(12, 4, 0, 4) };

            // Add integrated mixer controls
            var mixerPanel = CreateFrequencyMixerPanel(frequency);
            contentPanel.Children.Add(mixerPanel);

            // Add player details if available
            if (frequency.SourceData?.Players.Count > 0)
            {
                var playersHeader = new TextBlock
                {
                    Text = "Players",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Margin = new Thickness(0, 8, 0, 4),
                    Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray
                };
                contentPanel.Children.Add(playersHeader);

                // Calculate total packets for percentage calculation
                var totalPackets = frequency.SourceData.Players.Sum(p => p.PacketCount);

                // Sort by contribution (packet count) - highest first
                foreach (var player in frequency.SourceData.Players.OrderByDescending(p => p.PacketCount))
                {
                    var contribution = totalPackets > 0 ? (player.PacketCount * 100.0 / totalPackets) : 0;
                    var playerInfo = CreatePlayerInfoPanel(player, contribution);
                    contentPanel.Children.Add(playerInfo);
                }
            }

            return contentPanel;
        }

        private FrameworkElement CreateGroupHeader(FrequencyGroupViewModel group)
        {
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Add checkbox for group selection (select/unselect all in group)
            // Replaced visual checkbox with a FontAwesome icon button for consistency
            var groupSelectButton = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Select/Unselect all frequencies in this group",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Helper to set icon based on selection state
            void UpdateGroupIcon()
            {
                if (group.Frequencies.Count == 0)
                {
                    groupSelectButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.Circle, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                }
                else if (group.Frequencies.All(f => f.IsSelected))
                {
                    groupSelectButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.CheckCircle, 14, TryFindResource("AccentBrush") as Brush ?? Brushes.Green);
                }
                else if (group.Frequencies.Any(f => f.IsSelected))
                {
                    groupSelectButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.MinusCircle, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                }
                else
                {
                    groupSelectButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.Circle, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                }
            }

            UpdateGroupIcon();

            groupSelectButton.Click += (s, e) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var allSelectedNow = group.Frequencies.All(f => f.IsSelected);
                
                if (allSelectedNow)
                {
                    SetStatus($"Hiding {group.Frequencies.Count} frequencies in {group.Name}...");
                    
                    // Deselect all
                    foreach (var frequency in group.Frequencies)
                    {
                        if (frequency.IsSelected)
                        {
                            frequency.IsSelected = false;
                            FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, false));
                        }
                    }
                }
                else
                {
                    SetStatus($"Showing {group.Frequencies.Count} frequencies in {group.Name}...");
                    
                    // Select all
                    foreach (var frequency in group.Frequencies)
                    {
                        if (!frequency.IsSelected)
                        {
                            frequency.IsSelected = true;
                            FrequencySelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, true));
                        }
                    }
                }

                // Refresh UI
                UpdateTreeView();
                sw.Stop();
                var action = allSelectedNow ? "Hidden" : "Shown";
                SetStatus($"{action} {group.Frequencies.Count} frequencies ({sw.ElapsedMilliseconds}ms)");
            };

            var textBlock = new TextBlock
            {
                Text = group.Name,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(groupSelectButton);
            stackPanel.Children.Add(textBlock);
            return stackPanel;
        }

        private FrameworkElement CreateFrequencyMixerPanel(FrequencyViewModel frequency)
        {
            // Modern minimalistic collapsible mixer control
            var mixerExpander = new Expander
            {
                IsExpanded = false, // Start collapsed
                Style = TryFindResource("ModernExpander") as Style,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // COMPACT HEADER - Shows key info when collapsed
            var headerPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(15, 100, 100, 100)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Mixer icon
            var mixerIcon = IconHelper.CreateFaIcon(FontAwesomeIcon.Cog, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
            Grid.SetColumn(mixerIcon, 0);
            headerGrid.Children.Add(mixerIcon);

            // Compact volume slider shown when expander is collapsed (compact mode)
            Slider? compactVolumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 2,
                Value = frequency.Volume,
                Width = 120,
                Height = 16,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                // Use a smaller slider style if available, fall back to main slider style
                Style = TryFindResource("ModernMiniSlider") as Style ?? TryFindResource("ModernSlider") as Style
            };

            // We'll keep a reference to the expanded volume slider so we can keep both in sync
            Slider? expandedVolumeSlider = null;

            Grid.SetColumn(compactVolumeSlider, 1);
            headerGrid.Children.Add(compactVolumeSlider);

            // Status indicators (volume + state)
            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var volumeIndicator = new TextBlock
            {
                Text = $"{frequency.Volume * 100:F0}%",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindResource("AccentBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            statusPanel.Children.Add(volumeIndicator);

            // Wire compact slider updates to frequency and header indicator
            compactVolumeSlider.ValueChanged += (s, e) =>
            {
                frequency.Volume = (float)e.NewValue;
                volumeIndicator.Text = $"{frequency.Volume * 100:F0}%";
                // Propagate to expanded slider if present
                if (expandedVolumeSlider != null && Math.Abs(expandedVolumeSlider.Value - e.NewValue) > 0.001)
                {
                    expandedVolumeSlider.Value = e.NewValue;
                }
                OnMixerValueChanged(frequency, "Volume", frequency.Volume);
            };

            // State badges (compact)
            if (frequency.IsMuted)
            {
                var muteBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(211, 47, 47)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                muteBadge.Child = new TextBlock
                {
                    Text = "M",
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                statusPanel.Children.Add(muteBadge);
            }

            if (frequency.IsSolo)
            {
                var soloBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                soloBadge.Child = new TextBlock
                {
                    Text = "S",
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black
                };
                statusPanel.Children.Add(soloBadge);
            }

            Grid.SetColumn(statusPanel, 2);
            headerGrid.Children.Add(statusPanel);

            headerPanel.Child = headerGrid;
            mixerExpander.Header = headerPanel;

            // Ensure compact slider visibility matches initial expanded state
            compactVolumeSlider.Visibility = mixerExpander.IsExpanded ? Visibility.Collapsed : Visibility.Visible;

            // Toggle visibility when expander state changes
            mixerExpander.Expanded += (s, e) => compactVolumeSlider.Visibility = Visibility.Collapsed;
            mixerExpander.Collapsed += (s, e) => compactVolumeSlider.Visibility = Visibility.Visible;

            // EXPANDED CONTENT - Full mixer controls
            var contentBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(10, 128, 128, 128)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 4, 4),
                Padding = new Thickness(6, 6, 6, 6)
            };

            var contentStack = new StackPanel();

            // Volume Control (Compact)
            var volumePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            
            var volumeHeader = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            volumeHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volumeHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volumeHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var volumeLabel = new TextBlock
            {
                Text = "Volume",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(volumeLabel, 0);
            volumeHeader.Children.Add(volumeLabel);

            var volumeValue = new TextBlock
            {
                Text = $"{frequency.Volume * 100:F0}%",
                FontSize = 9,
                Style = TryFindResource("MonospaceTextStyle") as Style,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(volumeValue, 2);
            volumeHeader.Children.Add(volumeValue);

            var volumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 2,
                Value = frequency.Volume,
                Style = TryFindResource("ModernSlider") as Style,
                Height = 18
            };

            // assign to expandedVolumeSlider reference so compact can update it
            expandedVolumeSlider = volumeSlider;

            volumeSlider.ValueChanged += (s, e) =>
            {
                frequency.Volume = (float)e.NewValue;
                volumeValue.Text = $"{frequency.Volume * 100:F0}%";
                volumeIndicator.Text = $"{frequency.Volume * 100:F0}%";
                // Keep compact slider in sync
                if (compactVolumeSlider != null && Math.Abs(compactVolumeSlider.Value - e.NewValue) > 0.001)
                {
                    compactVolumeSlider.Value = e.NewValue;
                }
                OnMixerValueChanged(frequency, "Volume", frequency.Volume);
            };

            volumePanel.Children.Add(volumeHeader);
            volumePanel.Children.Add(volumeSlider);
            contentStack.Children.Add(volumePanel);

            // Pan Control (Compact)
            var panPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            
            var panHeader = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var panLabel = new TextBlock
            {
                Text = "Pan",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(panLabel, 0);
            panHeader.Children.Add(panLabel);

            var panValue = new TextBlock
            {
                Text = FormatPanValue(frequency.Pan),
                FontSize = 9,
                Style = TryFindResource("MonospaceTextStyle") as Style,
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
                Style = TryFindResource("ModernSlider") as Style,
                Height = 18
            };

            panSlider.ValueChanged += (s, e) =>
            {
                frequency.Pan = (float)e.NewValue;
                panValue.Text = FormatPanValue(frequency.Pan);
                OnMixerValueChanged(frequency, "Pan", frequency.Pan);
            };

            panPanel.Children.Add(panHeader);
            panPanel.Children.Add(panSlider);
            contentStack.Children.Add(panPanel);

            // Compact Action Buttons Row
            var buttonsPanel = new UniformGrid
            {
                Rows = 1,
                Columns = 3,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var muteButton = new Button
            {
                Content = IconHelper.CreateFaIcon(frequency.IsMuted ? FontAwesomeIcon.VolumeOff : FontAwesomeIcon.VolumeUp, 14),
                Style = TryFindResource("ModernSecondaryButton") as Style,
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = frequency.IsMuted ? "Unmute" : "Mute",
                Background = frequency.IsMuted ? new SolidColorBrush(Color.FromArgb(40, 211, 47, 47)) : null
            };

            muteButton.Click += (s, e) =>
            {
                frequency.IsMuted = !frequency.IsMuted;
                muteButton.Content = IconHelper.CreateFaIcon(frequency.IsMuted ? FontAwesomeIcon.VolumeOff : FontAwesomeIcon.VolumeUp, 14);
                muteButton.ToolTip = frequency.IsMuted ? "Unmute" : "Mute";
                muteButton.Background = frequency.IsMuted ? new SolidColorBrush(Color.FromArgb(40, 211, 47, 47)) : null;
                OnMixerBooleanChanged(frequency, "Mute", frequency.IsMuted);
            };

            var soloButton = new Button
            {
                Content = IconHelper.CreateFaIcon(frequency.IsSolo ? FontAwesomeIcon.Star : FontAwesomeIcon.StarOutline, 14),
                Style = TryFindResource("ModernSecondaryButton") as Style,
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = frequency.IsSolo ? "Unsolo" : "Solo",
                Background = frequency.IsSolo ? new SolidColorBrush(Color.FromArgb(40, 255, 193, 7)) : null
            };

            soloButton.Click += (s, e) =>
            {
                frequency.IsSolo = !frequency.IsSolo;
                soloButton.Content = IconHelper.CreateFaIcon(frequency.IsSolo ? FontAwesomeIcon.Star : FontAwesomeIcon.StarOutline, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                soloButton.ToolTip = frequency.IsSolo ? "Unsolo" : "Solo";
                soloButton.Background = frequency.IsSolo ? new SolidColorBrush(Color.FromArgb(40, 255, 193, 7)) : null;
                OnMixerBooleanChanged(frequency, "Solo", frequency.IsSolo);
            };

            var resetButton = new Button
            {
                Content = IconHelper.CreateIconTextBlock(IconHelper.Reset, 14),
                Style = TryFindResource("ModernSecondaryButton") as Style,
                Padding = new Thickness(4, 2, 4, 2),
                ToolTip = "Reset to default"
            };

            resetButton.Click += (s, e) =>
            {
                frequency.Volume = 1.0f;
                frequency.Pan = 0.0f;
                frequency.IsMuted = false;
                frequency.IsSolo = false;
                
                volumeSlider.Value = 1.0f;
                panSlider.Value = 0.0f;
                // Use FontAwesome icons for mute/solo after reset
                muteButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.VolumeUp, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                muteButton.Background = null;
                soloButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.StarOutline, 14, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                soloButton.Background = null;
                volumeIndicator.Text = "100%";
                
                OnMixerValueChanged(frequency, "Reset", 0);
            };

            buttonsPanel.Children.Add(muteButton);
            buttonsPanel.Children.Add(soloButton);
            buttonsPanel.Children.Add(resetButton);
            contentStack.Children.Add(buttonsPanel);

            contentBorder.Child = contentStack;
            mixerExpander.Content = contentBorder;

            return mixerExpander;
        }

        private static string FormatPanValue(float pan)
        {
            if (Math.Abs(pan) < 0.01f)
                return "Center";
            
            return pan > 0 ? $"R{pan:F2}" : $"L{Math.Abs(pan):F2}";
        }

        // Helper method to create TextBlock with symbol font for button content
        private static TextBlock CreateIconTextBlock(string icon, double fontSize)
        {
            return new TextBlock
            {
                Text = icon,
                FontFamily = IconHelper.SymbolFont,
                FontSize = fontSize,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        // Event for mixer changes
        public event EventHandler<MixerValueChangedEventArgs>? MixerValueChanged;
        public event EventHandler<MixerBooleanChangedEventArgs>? MixerBooleanChanged;

        private void OnMixerValueChanged(FrequencyViewModel frequency, string property, float value)
        {
            MixerValueChanged?.Invoke(this, new MixerValueChangedEventArgs(frequency, property, value));
        }

        private void OnMixerBooleanChanged(FrequencyViewModel frequency, string property, bool value)
        {
            MixerBooleanChanged?.Invoke(this, new MixerBooleanChangedEventArgs(frequency, property, value));
        }
        
        // NEW: Overload for pilot filter events with player info
        private void OnMixerBooleanChanged(FrequencyViewModel frequency, string property, bool value, PlayerFrequencyInfo player)
        {
            MixerBooleanChanged?.Invoke(this, new MixerBooleanChangedEventArgs(frequency, property, value, player));
        }

        private FrameworkElement CreatePlayerInfoPanel(PlayerFrequencyInfo player, double contributionPercent)
        {
            // Minimalistic single-line design with contribution percentage and pilot filter controls
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 128, 128, 128)), // Subtle background
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(6, 3, 6, 3)
            };

            var mainPanel = new DockPanel();

            // Coalition dot indicator (left-most)
            if (!string.IsNullOrEmpty(player.Coalition) && player.Coalition != "Unknown")
            {
                var coalitionDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = GetCoalitionBrush(player.Coalition),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    ToolTip = $"{player.Coalition} Coalition"
                };
                DockPanel.SetDock(coalitionDot, Dock.Left);
                mainPanel.Children.Add(coalitionDot);
            }

            // Pilot checkbox filter
            var pilotCheckBox = new CheckBox
            {
                IsChecked = player.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 6, 0),
                ToolTip = player.IsSelected ? "Deselect pilot (filter out)" : "Select pilot (include in playback)"
            };

            pilotCheckBox.Checked += (s, e) =>
            {
                player.IsSelected = true;
                pilotCheckBox.ToolTip = "Deselect pilot (filter out)";
                
                SetStatus($"Including pilot: {player.Name}");
                
                // Fire event for ViewModel to handle
                var freqViewModel = Frequencies
                    .SelectMany(g => g.Frequencies)
                    .FirstOrDefault(f => f.SourceData?.Players.Contains(player) == true);
                if (freqViewModel != null)
                {
                    OnMixerBooleanChanged(freqViewModel, "PilotSelected", true, player);
                }
            };

            pilotCheckBox.Unchecked += (s, e) =>
            {
                player.IsSelected = false;
                pilotCheckBox.ToolTip = "Select pilot (include in playback)";
                
                SetStatus($"Filtering out pilot: {player.Name}");
                
                // Fire event for ViewModel to handle
                var freqViewModel = Frequencies
                    .SelectMany(g => g.Frequencies)
                    .FirstOrDefault(f => f.SourceData?.Players.Contains(player) == true);
                if (freqViewModel != null)
                {
                    OnMixerBooleanChanged(freqViewModel, "PilotSelected", false, player);
                }
            };

            DockPanel.SetDock(pilotCheckBox, Dock.Right);
            mainPanel.Children.Add(pilotCheckBox);

            // Contribution percentage badge (right-most)
            var contributionBadge = new Border
            {
                Background = GetContributionBrush(contributionPercent),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = $"{contributionPercent:F1}% of activity on this frequency\n{player.PacketCount} packets transmitted"
            };

            var contributionText = new TextBlock
            {
                Text = $"{contributionPercent:F0}%",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            contributionBadge.Child = contributionText;
            DockPanel.SetDock(contributionBadge, Dock.Right);
            mainPanel.Children.Add(contributionBadge);

            // Main info panel (fills remaining space)
            var infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Player name
            var nameText = new TextBlock
            {
                Text = player.Name,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120
            };
            infoPanel.Children.Add(nameText);

            // Aircraft icon + name
            if (!string.IsNullOrEmpty(player.Aircraft) && player.Aircraft != "Unknown")
            {
                var aircraftIcon = IconHelper.CreateFaIcon(FontAwesomeIcon.Plane, 12, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
                infoPanel.Children.Add(aircraftIcon);

                var aircraftText = new TextBlock
                {
                    Text = player.Aircraft,
                    FontSize = 9,
                    Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 80
                };
                infoPanel.Children.Add(aircraftText);
            }

            // Time range with clock icon
            var duration = player.LastSeen - player.FirstSeen;
            var durationText = duration.TotalMinutes >= 1 
                ? $"{duration.TotalMinutes:F0}m" 
                : $"{duration.TotalSeconds:F0}s";

            var timeIconElement = IconHelper.CreateIconTextBlock("\u23F0", 9, TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray);
            timeIconElement.Margin = new Thickness(0, 0, 2, 0);
            infoPanel.Children.Add(timeIconElement);

            var timeText = new TextBlock
            {
                Text = durationText,
                FontSize = 9,
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Active from {player.FirstSeen:HH:mm:ss} to {player.LastSeen:HH:mm:ss}"
            };
            infoPanel.Children.Add(timeText);

            mainPanel.Children.Add(infoPanel);

            // Set overall tooltip
            var selectionStatus = player.IsSelected ? "SELECTED (audio included)" : "DESELECTED (audio filtered out)";

            border.ToolTip = $"{player.Name}\n" +
                            $"Coalition: {player.Coalition}\n" +
                            $"Aircraft: {player.Aircraft}\n" +
                            $"Contribution: {contributionPercent:F1}% ({player.PacketCount} packets)\n" +
                            $"Active: {player.FirstSeen:HH:mm:ss} - {player.LastSeen:HH:mm:ss} ({duration.TotalMinutes:F1}min)\n" +
                            $"Status: {selectionStatus}";

            border.Child = mainPanel;
            return border;
        }

        private Brush GetContributionBrush(double percent)
        {
            // Color-code contribution percentage for visual hierarchy
            // High contribution (>50%) = Green
            // Medium contribution (20-50) = Blue
            // Low contribution (<20%) = Gray
            
            if (percent >= 50)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            else if (percent >= 20)
                return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
            else
                return new SolidColorBrush(Color.FromRgb(120, 120, 120)); // Gray
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

    public class StatusMessageChangedEventArgs : EventArgs
    {
        public string Message { get; }

        public StatusMessageChangedEventArgs(string message)
        {
            Message = message;
        }
    }

    public class MixerValueChangedEventArgs : EventArgs
    {
        public FrequencyViewModel Frequency { get; }
        public string Property { get; }
        public float Value { get; }

        public MixerValueChangedEventArgs(FrequencyViewModel frequency, string property, float value)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
        }
    }

    public class MixerBooleanChangedEventArgs : EventArgs
    {
        public FrequencyViewModel Frequency { get; }
        public string Property { get; }
        public bool Value { get; }
        
        // NEW: Player information for pilot filters
        public PlayerFrequencyInfo? Player { get; }

        public MixerBooleanChangedEventArgs(FrequencyViewModel frequency, string property, bool value)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
            Player = null;
        }
        
        // NEW: Constructor with player info for pilot filters
        public MixerBooleanChangedEventArgs(FrequencyViewModel frequency, string property, bool value, PlayerFrequencyInfo player)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
            Player = player;
        }
    }
}
