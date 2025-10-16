using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.UI{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly AudioSession _audioSession;
        private readonly FrequencyAnalyzer _frequencyAnalyzer;
        private readonly Mixer _mixer;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly DispatcherTimer _spectrumUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _audioSession = new AudioSession();
            _frequencyAnalyzer = new FrequencyAnalyzer();
            _mixer = new Mixer();

            // Setup UI update timer for real-time updates during playback
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS
            };
            _uiUpdateTimer.Tick += UpdatePlaybackStatus;

            // Setup spectrum update timer for real-time FFT
            _spectrumUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 10 FPS for spectrum
            };
            _spectrumUpdateTimer.Tick += UpdateSpectrum;

            WireUpAudioSessionEvents();
            
            // Autoload last opened file if it exists
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Try to load the last opened file
            var lastFile = Properties.Settings.Default.LastOpenedFile;
            
            if (!string.IsNullOrEmpty(lastFile) && System.IO.File.Exists(lastFile))
            {
                // Display the file being opened
                var fileName = System.IO.Path.GetFileName(lastFile);
                var fileDirectory = System.IO.Path.GetDirectoryName(lastFile);
                
                _viewModel.StatusText = $"Auto-loading: {fileName}";
                System.Diagnostics.Debug.WriteLine($"[Autoload] Opening file: {lastFile}");
                
                try
                {
                    var success = await _audioSession.LoadFileAsync(lastFile);
                    
                    if (success)
                    {
                        // Set the current filename for display
                        _viewModel.CurrentFileName = fileName;
                        
                        _viewModel.StatusText = $"Loaded: {fileName}";
                        _viewModel.IsFileLoaded = true;
                        _viewModel.TotalTime = _audioSession.TotalDuration;
                        
                        System.Diagnostics.Debug.WriteLine($"[Autoload] Successfully loaded: {lastFile}");
                        
                        // Load available frequencies
                        LoadAvailableFrequencies();
                        
                        // Update status to show completion
                        var freqCount = _viewModel.AvailableFrequencies.Sum(g => g.Frequencies.Count);
                        _viewModel.StatusText = $"Loaded: {fileName} - {freqCount} frequencies found";
                    }
                else
                {
                    // Failed to load, clear the setting
                    System.Diagnostics.Debug.WriteLine($"[Autoload] Failed to load file: {lastFile}");
                    Properties.Settings.Default.LastOpenedFile = string.Empty;
                    Properties.Settings.Default.Save();
                    _viewModel.CurrentFileName = string.Empty;
                    _viewModel.StatusText = "Ready to load recording file";
                }
            }
            catch (Exception ex)
            {
                // Failed to load, clear the setting and show error
                System.Diagnostics.Debug.WriteLine($"[Autoload] Exception while loading file '{lastFile}': {ex.Message}");
                Properties.Settings.Default.LastOpenedFile = string.Empty;
                Properties.Settings.Default.Save();
                _viewModel.CurrentFileName = string.Empty;
                _viewModel.StatusText = "Ready to load recording file";
            }
        }
        else if (!string.IsNullOrEmpty(lastFile))
        {
            // File path exists in settings but file doesn't exist
            System.Diagnostics.Debug.WriteLine($"[Autoload] File no longer exists: {lastFile}");
            Properties.Settings.Default.LastOpenedFile = string.Empty;
            Properties.Settings.Default.Save();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[Autoload] No last opened file found in settings");
        }
    }

        private void WireUpAudioSessionEvents()
        {
            _audioSession.PlaybackStarted += OnPlaybackStarted;
            _audioSession.PlaybackStopped += OnPlaybackStopped;
            _audioSession.PlaybackPaused += OnPlaybackPaused;
            _audioSession.PlaybackResumed += OnPlaybackResumed;
            _audioSession.PlaybackError += OnPlaybackError;
            _audioSession.OnPlaybackProgress += OnPlaybackProgress;
            _audioSession.OnEndReached += OnEndReached;
        }

        #region Event Handlers

        private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select SRS Recording File",
                Filter = "SRS Recording Files (*.raw)|*.raw|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            // Set initial directory to the last opened file's directory if available
            var lastFile = Properties.Settings.Default.LastOpenedFile;
            if (!string.IsNullOrEmpty(lastFile) && System.IO.File.Exists(lastFile))
            {
                openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(lastFile);
            }

            if (openFileDialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                _viewModel.StatusText = $"Loading: {fileName}...";
                System.Diagnostics.Debug.WriteLine($"[Manual Load] Opening file: {openFileDialog.FileName}");
                
                try
                {
                    var success = await _audioSession.LoadFileAsync(openFileDialog.FileName);
                    
                    if (success)
                    {
                        // Save the successfully loaded file path
                        Properties.Settings.Default.LastOpenedFile = openFileDialog.FileName;
                        Properties.Settings.Default.Save();
                        
                        System.Diagnostics.Debug.WriteLine($"[Manual Load] Successfully loaded and saved: {openFileDialog.FileName}");
                        
                        // Set the current filename for display
                        _viewModel.CurrentFileName = fileName;
                        
                        _viewModel.StatusText = $"Loaded: {fileName}";
                        _viewModel.IsFileLoaded = true;
                        _viewModel.TotalTime = _audioSession.TotalDuration;
                        
                        // Load available frequencies FIRST (before waveform)
                        // This ensures the frequency tree is populated immediately
                        // NOTE: LoadAvailableFrequencies() will trigger waveform regeneration
                        // after frequencies are selected, so we don't call GetWaveformData() here
                        LoadAvailableFrequencies();
                        
                        // Update status to show completion
                        var freqCount = _viewModel.AvailableFrequencies.Sum(g => g.Frequencies.Count);
                        _viewModel.StatusText = $"Loaded: {fileName} - {freqCount} frequencies found";
                        System.Diagnostics.Debug.WriteLine($"[Manual Load] Found {freqCount} frequencies in file");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Manual Load] LoadFileAsync returned false for: {openFileDialog.FileName}");
                        _viewModel.CurrentFileName = string.Empty;
                        _viewModel.StatusText = "Failed to load file";
                        _viewModel.IsFileLoaded = false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Manual Load] Exception: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Manual Load] Stack trace: {ex.StackTrace}");
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    _viewModel.CurrentFileName = string.Empty;
                    _viewModel.StatusText = "Error loading file";
                    _viewModel.IsFileLoaded = false;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Manual Load] User cancelled file selection");
            }
        }

        private void WaveformViewer_SeekRequested(object sender, double normalizedPosition)
        {
            _audioSession.SeekTo(normalizedPosition);
            _viewModel.PlayheadPosition = normalizedPosition;
        }

        private void WaveformViewer_ZoomRegionSelected(object sender, ZoomRegionSelectedEventArgs e)
        {
            // Apply zoom to the selected region
            _viewModel.ZoomStartTime = Math.Clamp(e.StartTime, 0.0, 1.0);
            _viewModel.ZoomEndTime = Math.Clamp(e.EndTime, 0.0, 1.0);
            
            // Calculate zoom level
            var range = _viewModel.ZoomEndTime - _viewModel.ZoomStartTime;
            _viewModel.ZoomLevel = range > 0 ? 1.0 / range : 1.0;
            
            // Update time range display
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void WaveformZoomIn_Click(object sender, RoutedEventArgs e)
        {
            // Zoom in at center of current view
            var center = (_viewModel.ZoomStartTime + _viewModel.ZoomEndTime) / 2.0;
            var currentRange = _viewModel.ZoomEndTime - _viewModel.ZoomStartTime;
            var newRange = currentRange / 2.0; // Zoom in by 2x
            
            _viewModel.ZoomStartTime = Math.Clamp(center - newRange / 2.0, 0.0, 1.0);
            _viewModel.ZoomEndTime = Math.Clamp(center + newRange / 2.0, 0.0, 1.0);
            
            // Ensure valid range
            if (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime < 0.01)
            {
                _viewModel.ZoomEndTime = Math.Min(_viewModel.ZoomStartTime + 0.01, 1.0);
            }
            
            _viewModel.ZoomLevel = 1.0 / (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime);
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void WaveformZoomOut_Click(object sender, RoutedEventArgs e)
        {
            // Zoom out from center of current view
            var center = (_viewModel.ZoomStartTime + _viewModel.ZoomEndTime) / 2.0;
            var currentRange = _viewModel.ZoomEndTime - _viewModel.ZoomStartTime;
            var newRange = Math.Min(currentRange * 2.0, 1.0); // Zoom out by 2x, max is full view
            
            _viewModel.ZoomStartTime = Math.Clamp(center - newRange / 2.0, 0.0, 1.0);
            _viewModel.ZoomEndTime = Math.Clamp(center + newRange / 2.0, 0.0, 1.0);
            
            // If we're at or beyond full view, reset to full
            if (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime >= 0.99)
            {
                _viewModel.ZoomStartTime = 0.0;
                _viewModel.ZoomEndTime = 1.0;
            }
            
            _viewModel.ZoomLevel = 1.0 / (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime);
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void WaveformZoomReset_Click(object sender, RoutedEventArgs e)
        {
            // Reset to full view
            _viewModel.ZoomStartTime = 0.0;
            _viewModel.ZoomEndTime = 1.0;
            _viewModel.ZoomLevel = 1.0;
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void WaveformMiniMap_Clicked(object sender, Controls.MiniMapClickEventArgs e)
        {
            // Center viewport on click position
            _viewModel.ZoomStartTime = Math.Clamp(e.StartTime, 0.0, 1.0);
            _viewModel.ZoomEndTime = Math.Clamp(e.EndTime, 0.0, 1.0);
            _viewModel.ZoomLevel = 1.0 / (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime);
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void WaveformMiniMap_Dragged(object sender, Controls.MiniMapDragEventArgs e)
        {
            // Pan viewport by dragging
            _viewModel.ZoomStartTime = Math.Clamp(e.StartTime, 0.0, 1.0);
            _viewModel.ZoomEndTime = Math.Clamp(e.EndTime, 0.0, 1.0);
            _viewModel.ZoomLevel = 1.0 / (_viewModel.ZoomEndTime - _viewModel.ZoomStartTime);
            OnPropertyChanged(nameof(_viewModel.TimeRangeDisplay));
        }

        private void OnPropertyChanged(string propertyName)
        {
            // Trigger property change notification
            _viewModel.GetType().GetProperty(propertyName)?.GetValue(_viewModel);
        }

        private System.Threading.Timer? _waveformRegenerationTimer;
        private bool _waveformRegenerationPending = false;

        private async void FrequencyTreeView_SelectionChanged(object sender, FrequencySelectionChangedEventArgs e)
        {
            // Update frequency filter based on selection
            _audioSession.SetChannelActive(e.Frequency.Frequency, e.IsSelected);
            
            if (e.IsSelected)
            {
                // Add to active frequencies for mixer
                if (!_viewModel.ActiveFrequencies.Any(af => Math.Abs(af.Frequency - e.Frequency.Frequency) < 0.1))
                {
                    var activeFreq = new ActiveFrequencyViewModel
                    {
                        Frequency = e.Frequency.Frequency,
                        DisplayName = e.Frequency.DisplayName,
                        Gain = 1.0f,
                        Pan = 0.0f
                    };
                    
                    _viewModel.ActiveFrequencies.Add(activeFreq);
                }
            }
            else
            {
                // Remove from active frequencies
                var toRemove = _viewModel.ActiveFrequencies
                    .FirstOrDefault(af => Math.Abs(af.Frequency - e.Frequency.Frequency) < 0.1);
                
                if (toRemove != null)
                {
                    _viewModel.ActiveFrequencies.Remove(toRemove);
                }
            }
            
            // PERFORMANCE FIX: Debounce waveform regeneration to avoid regenerating on every checkbox click
            // This dramatically improves UI responsiveness when selecting/deselecting multiple frequencies
            _waveformRegenerationPending = true;
            
            // Cancel existing timer and create a new one
            _waveformRegenerationTimer?.Dispose();
            _waveformRegenerationTimer = new System.Threading.Timer(async _ =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (_waveformRegenerationPending)
                    {
                        _waveformRegenerationPending = false;
                        await RegenerateWaveformAsync();
                    }
                });
            }, null, 300, System.Threading.Timeout.Infinite); // 300ms debounce delay
        }

        private void FrequencyMixer_GainChanged(object sender, FrequencyGainChangedEventArgs e)
        {
            _audioSession.SetChannelGain(e.Frequency, e.Gain);
            _mixer.SetChannelGain(e.Frequency, e.Gain);
        }

        private void FrequencyMixer_PanChanged(object sender, FrequencyPanChangedEventArgs e)
        {
            _audioSession.SetChannelPan(e.Frequency, e.Pan);
            _mixer.SetChannelPan(e.Frequency, e.Pan);
        }

        private async void SelectAllFrequencies_Click(object sender, RoutedEventArgs e)
        {
            // Cancel any pending debounced regeneration
            _waveformRegenerationTimer?.Dispose();
            _waveformRegenerationPending = false;
            
            FrequencyTreeView?.SelectAll();
            await RegenerateWaveformAsync();
        }

        private async void SelectNoFrequencies_Click(object sender, RoutedEventArgs e)
        {
            // Cancel any pending debounced regeneration
            _waveformRegenerationTimer?.Dispose();
            _waveformRegenerationPending = false;
            
            FrequencyTreeView?.SelectNone();
            _viewModel.ActiveFrequencies.Clear();
            await RegenerateWaveformAsync();
        }

        private void TransportControls_PlayRequested(object sender, EventArgs e)
        {
            if (_viewModel.IsPaused)
            {
                _audioSession.Play();
            }
            else
            {
                _audioSession.Play();
            }
        }

        private void TransportControls_PauseRequested(object sender, EventArgs e)
        {
            _audioSession.Pause();
        }

        private void TransportControls_StopRequested(object sender, EventArgs e)
        {
            _audioSession.Stop();
        }

        private void TransportControls_SeekRequested(object sender, double normalizedPosition)
        {
            _audioSession.SeekTo(normalizedPosition);
            _viewModel.PlayheadPosition = normalizedPosition;
        }

        #endregion

        #region Audio Session Event Handlers

        private void OnPlaybackStarted()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.IsPlaying = true;
                _viewModel.IsPaused = false;
                _viewModel.StatusText = "Playing...";
                _uiUpdateTimer.Start();
                _spectrumUpdateTimer.Start();
            });
        }

        private void OnPlaybackStopped()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.IsPlaying = false;
                _viewModel.IsPaused = false;
                // Keep the filename visible when stopped
                var fileName = _viewModel.CurrentFileName;
                _viewModel.StatusText = !string.IsNullOrEmpty(fileName) 
                    ? $"Stopped - {fileName}" 
                    : "Stopped";
                _viewModel.Progress = 0;
                _viewModel.PlayheadPosition = 0;
                _viewModel.CurrentTime = TimeSpan.Zero;
                _uiUpdateTimer.Stop();
                _spectrumUpdateTimer.Stop();
            });
        }

        private void OnPlaybackPaused()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.IsPlaying = false;
                _viewModel.IsPaused = true;
                _viewModel.StatusText = "Paused";
                _uiUpdateTimer.Stop();
                _spectrumUpdateTimer.Stop();
            });
        }

        private void OnPlaybackResumed()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.IsPlaying = true;
                _viewModel.IsPaused = false;
                _viewModel.StatusText = "Playing...";
                _uiUpdateTimer.Start();
                _spectrumUpdateTimer.Start();
            });
        }

        private void OnPlaybackError(Exception ex)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.StatusText = $"Playback error: {ex.Message}";
                _viewModel.IsPlaying = false;
                _viewModel.IsPaused = false;
                _uiUpdateTimer.Stop();
                _spectrumUpdateTimer.Stop();
                
                MessageBox.Show($"Playback error: {ex.Message}", "Playback Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void OnPlaybackProgress(double progress)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.Progress = progress;
                _viewModel.PlayheadPosition = progress / 100.0;
            });
        }

        private void OnEndReached()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _viewModel.StatusText = "Playback completed";
            });
        }

        #endregion

        #region Timer Updates

        private void UpdatePlaybackStatus(object? sender, EventArgs e)
        {
            if (!_viewModel.IsPlaying)
                return;

            _viewModel.CurrentTime = _audioSession.CurrentPosition;
            
            // Update progress if not being manually controlled
            if (_audioSession.TotalDuration.Ticks > 0)
            {
                var progress = (_audioSession.CurrentPosition.Ticks / (double)_audioSession.TotalDuration.Ticks) * 100.0;
                _viewModel.Progress = Math.Clamp(progress, 0, 100);
                _viewModel.PlayheadPosition = progress / 100.0;
            }
        }

        private void UpdateSpectrum(object? sender, EventArgs e)
        {
            if (!_viewModel.IsPlaying)
                return;

            // Get real-time spectrum data from the frequency analyzer
            _viewModel.SpectrumData = _frequencyAnalyzer.GetSpectrumSnapshot();
        }

        #endregion

        #region Helper Methods

        private void LoadAvailableFrequencies()
        {
            _viewModel.AvailableFrequencies.Clear();

            var frequencies = _audioSession.GetAvailableFrequencies();
            
            if (!frequencies.Any())
            {
                _viewModel.StatusText += " (No frequencies detected in file)";
                return;
            }
            
            // Get color palette for frequency visualization
            var colorPalette = GetFrequencyColorPalette();
            var colorIndex = 0;
            
            // Group frequencies by coalition based on player data
            var coalitionGroups = new Dictionary<string, List<Services.FrequencyInfo>>();
            
            foreach (var freq in frequencies)
            {
                // Determine primary coalition for this frequency based on players
                string primaryCoalition = "Unknown";
                if (freq.Players.Any())
                {
                    // Group players by coalition and pick the most active one
                    var coalitionCounts = freq.Players
                        .GroupBy(p => p.Coalition ?? "Unknown")
                        .Select(g => new { Coalition = g.Key, Count = g.Sum(p => p.PacketCount) })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    
                    if (coalitionCounts.Any())
                    {
                        primaryCoalition = coalitionCounts.First().Coalition;
                    }
                }
                
                if (!coalitionGroups.ContainsKey(primaryCoalition))
                {
                    coalitionGroups[primaryCoalition] = new List<Services.FrequencyInfo>();
                }
                coalitionGroups[primaryCoalition].Add(freq);
            }
            
            // Create coalition groups in order: Red, Blue, Neutral/Spectator, Unknown
            var orderedCoalitions = new[] { "Red", "Blue", "Neutral", "Spectator", "Unknown" };
            
            foreach (var coalitionName in orderedCoalitions)
            {
                if (!coalitionGroups.ContainsKey(coalitionName))
                    continue;
                    
                var coalitionFreqs = coalitionGroups[coalitionName];
                var coalitionGroup = new FrequencyGroupViewModel 
                { 
                    Name = $"{coalitionName} - {coalitionFreqs.Count} frequencies" 
                };
                
                foreach (var freq in coalitionFreqs.OrderBy(f => f.Frequency))
                {
                    // Create FrequencyModulationInfo for detailed player data
                    var sourceData = new Core.Models.FrequencyModulationInfo(
                        freq.Frequency, 
                        (Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player.Modulation)Enum.Parse(
                            typeof(Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player.Modulation), 
                            freq.Modulation))
                    {
                        Players = freq.Players
                    };
                    
                    // Assign color from palette (cycling if more frequencies than colors)
                    var color = colorPalette[colorIndex % colorPalette.Count];
                    colorIndex++;
                    
                    var frequencyViewModel = new FrequencyViewModel
                    {
                        Frequency = freq.Frequency,
                        Modulation = freq.Modulation,
                        DisplayName = freq.DisplayName,
                        PacketCount = freq.PacketCount,
                        IsSelected = true, // FIXED: Select all frequencies by default when loading file
                        SourceData = sourceData,
                        WaveformColor = color
                    };
                    
                    coalitionGroup.Frequencies.Add(frequencyViewModel);
                    
                    // Register the color with the audio session for consistent waveform visualization
                    _audioSession.SetFrequencyColor(freq.Frequency, color);
                }
                
                _viewModel.AvailableFrequencies.Add(coalitionGroup);
            }
            
                // CRITICAL FIX: After loading frequencies with IsSelected = true, trigger the UI update
                // to ensure checkboxes are visually checked and active frequencies are populated
                // Use Dispatcher to ensure UI thread handles the update after the TreeView is bound
                Dispatcher.BeginInvoke(async () =>
                {
                    // Manually populate ActiveFrequencies since the checkboxes won't fire events on initial load
                    foreach (var group in _viewModel.AvailableFrequencies)
                    {
                        foreach (var freq in group.Frequencies.Where(f => f.IsSelected))
                        {
                            var activeFreq = new ActiveFrequencyViewModel
                            {
                                Frequency = freq.Frequency,
                                DisplayName = freq.DisplayName,
                                Gain = 1.0f,
                                Pan = 0.0f
                            };
                            
                            _viewModel.ActiveFrequencies.Add(activeFreq);
                            
                            // Also notify the audio session
                            _audioSession.SetChannelActive(freq.Frequency, true);
                        }
                    }
                    
                    // Force the FrequencyTreeView control to rebuild its UI by calling UpdateTreeView()
                    // This ensures the checkboxes reflect the IsSelected=true state
                    FrequencyTreeView?.UpdateTreeView();
                    
                    // Show initial loading message before waveform generation
                    var totalSelected = _viewModel.AvailableFrequencies.Sum(g => g.Frequencies.Count(f => f.IsSelected));
                    _viewModel.WaveformLoadingMessage = $"Generating initial waveform for {totalSelected} {(totalSelected == 1 ? "frequency" : "frequencies")}...";
                    
                    // CRITICAL FIX: Regenerate waveform with selected frequencies
                    // This ensures the waveform shows only the selected frequencies
                    await RegenerateWaveformAsync();
                    
                    // Log for debugging
                    System.Diagnostics.Debug.WriteLine($"Frequency loading complete: {totalSelected} frequencies selected and waveform regenerated");
                    
                }, System.Windows.Threading.DispatcherPriority.DataBind);
        }

        /// <summary>
        /// Regenerates the waveform based on current frequency selection
        /// </summary>
        private async Task RegenerateWaveformAsync()
        {
            if (!_viewModel.IsFileLoaded || string.IsNullOrEmpty(_audioSession.CurrentFilePath))
                return;
            
            try
            {
                // LOADING STATE: Show loading message
                _viewModel.IsWaveformLoading = true;
                
                // Update status and loading message based on selected frequency count
                var selectedCount = _viewModel.AvailableFrequencies
                    .SelectMany(g => g.Frequencies)
                    .Count(f => f.IsSelected);
                
                if (selectedCount == 0)
                {
                    _viewModel.WaveformLoadingMessage = "Clearing waveform...";
                    _viewModel.StatusText = "Clearing waveform...";
                }
                else
                {
                    _viewModel.WaveformLoadingMessage = $"Generating waveform for {selectedCount} {(selectedCount == 1 ? "frequency" : "frequencies")}...";
                    _viewModel.StatusText = $"Generating waveform for {selectedCount} {(selectedCount == 1 ? "frequency" : "frequencies")}...";
                }
                
                // Force UI update to show loading state
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                
                // Regenerate waveform with current frequency selection
                await _audioSession.RegenerateWaveformAsync(_audioSession.CurrentFilePath);
                
                // Update the waveform data in the view model
                // Use multi-frequency colored waveform for ANY number of selected frequencies (including 1)
                // This ensures that single frequencies also display with their assigned color
                if (selectedCount > 0)
                {
                    _viewModel.FrequencyWaveforms = _audioSession.GetFrequencyWaveformData();
                    _viewModel.WaveformData = null; // Clear single waveform when using frequency-colored waveform
                }
                else
                {
                    // Only when NO frequencies are selected, show a blank waveform
                    _viewModel.WaveformData = _audioSession.GetWaveformData();
                    _viewModel.FrequencyWaveforms = null; // Clear multi-frequency when showing blank
                }
                
                // LOADING STATE: Hide loading message
                _viewModel.IsWaveformLoading = false;
                
                // Update status
                _viewModel.StatusText = selectedCount > 0
                    ? $"Waveform updated - {selectedCount} {(selectedCount == 1 ? "frequency" : "frequencies")} selected"
                    : $"Loaded: {System.IO.Path.GetFileName(_audioSession.CurrentFilePath)}";
            }
            catch (Exception ex)
            {
                _viewModel.IsWaveformLoading = false;
                _viewModel.StatusText = $"Error updating waveform: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the color palette used for frequency visualization
        /// This should match the palette in CoreApiService to ensure consistency
        /// </summary>
        private List<System.Windows.Media.Color> GetFrequencyColorPalette()
        {
            return new List<System.Windows.Media.Color>
            {
                System.Windows.Media.Color.FromRgb(231, 76, 60),   // Red
                System.Windows.Media.Color.FromRgb(52, 152, 219),  // Blue
                System.Windows.Media.Color.FromRgb(46, 204, 113),  // Green
                System.Windows.Media.Color.FromRgb(155, 89, 182),  // Purple
                System.Windows.Media.Color.FromRgb(241, 196, 15),  // Yellow
                System.Windows.Media.Color.FromRgb(230, 126, 34),  // Orange
                System.Windows.Media.Color.FromRgb(26, 188, 156),  // Turquoise
                System.Windows.Media.Color.FromRgb(236, 240, 241), // Light Gray
                System.Windows.Media.Color.FromRgb(149, 165, 166), // Gray
                System.Windows.Media.Color.FromRgb(192, 57, 43),   // Dark Red
                System.Windows.Media.Color.FromRgb(41, 128, 185),  // Dark Blue
                System.Windows.Media.Color.FromRgb(39, 174, 96),   // Dark Green
            };
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _uiUpdateTimer?.Stop();
            _spectrumUpdateTimer?.Stop();
            _waveformRegenerationTimer?.Dispose();
            _audioSession?.Dispose();
            base.OnClosed(e);
        }
    }
}