using System;
using System.Windows;
using System.Windows.Controls;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.Events;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Unified player control with LiveCharts2 integration (Phase 1).
    /// Features modern, smooth design matching existing UI style.
    /// </summary>
    public partial class UnifiedPlayerControl : UserControl
    {
        #region Fields

        private bool _fileLoadedEventSubscribed = false;
        private bool _serverConnectionEventSubscribed = false;
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        #endregion

        #region Constructor

        public UnifiedPlayerControl()
        {
            InitializeComponent();
            
            // Subscribe to lifecycle events
            this.DataContextChanged += UnifiedPlayerControl_DataContextChanged;
            this.Loaded += UnifiedPlayerControl_Loaded;
            
            // Initialize LiveCharts visibility based on feature flag
            InitializeLiveChartsVisibility();
        }

        #endregion

        #region Properties

        public UnifiedPlayerViewModel? ViewModel
        {
            get => DataContext as UnifiedPlayerViewModel;
            set => DataContext = value;
        }

        #endregion

        #region Lifecycle Event Handlers

        private void UnifiedPlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize FileSourcePanel content
            if (ViewModel?.FileSource != null && FileOverlay != null)
            {
                var fileSourcePanel = new FileSourcePanel
                {
                    DataContext = ViewModel.FileSource
                };
                FileOverlay.PanelContent = fileSourcePanel;
            }

            // Initialize ServerSourcePanel content
            if (ViewModel?.ServerSource != null && ServerOverlay != null)
            {
                var serverSourcePanel = new ServerSourcePanel
                {
                    DataContext = ViewModel.ServerSource
                };
                ServerOverlay.PanelContent = serverSourcePanel;
            }
        }

        private void UnifiedPlayerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is UnifiedPlayerViewModel oldViewModel)
            {
                if (oldViewModel.FileSource != null)
                {
                    oldViewModel.FileSource.FileLoaded -= OnFileLoaded;
                    _fileLoadedEventSubscribed = false;
                }
                
                if (oldViewModel.ServerSource != null)
                {
                    oldViewModel.ServerSource.ConnectionStateChanged -= OnServerConnected;
                    _serverConnectionEventSubscribed = false;
                }
            }

            // Subscribe to new ViewModel
            var newViewModel = e.NewValue as UnifiedPlayerViewModel;
            
            if (newViewModel?.FileSource != null && !_fileLoadedEventSubscribed)
            {
                newViewModel.FileSource.FileLoaded += OnFileLoaded;
                _fileLoadedEventSubscribed = true;
                
                if (FileOverlay != null)
                {
                    var fileSourcePanel = new FileSourcePanel
                    {
                        DataContext = newViewModel.FileSource
                    };
                    FileOverlay.PanelContent = fileSourcePanel;
                }
            }
            
            if (newViewModel?.ServerSource != null && !_serverConnectionEventSubscribed)
            {
                newViewModel.ServerSource.ConnectionStateChanged += OnServerConnected;
                _serverConnectionEventSubscribed = true;
                
                if (ServerOverlay != null)
                {
                    var serverSourcePanel = new ServerSourcePanel
                    {
                        DataContext = newViewModel.ServerSource
                    };
                    ServerOverlay.PanelContent = serverSourcePanel;
                }
            }
        }

        private void OnFileLoaded(string filePath)
        {
            Dispatcher.BeginInvoke(() =>
            {
                FileOverlay?.Close();
                
                // Phase 6: Connect playhead sync to PlaybackController
                // In production, this connection should always succeed when file loads
                // Simulation mode is only for unit tests
                try
                {
                    var playbackController = ViewModel?.PlaybackController;
                    
                    if (playbackController != null && UnifiedGraph != null)
                    {
                        UnifiedGraph.ConnectPlayheadToPlayback(playbackController);
                        _logger.Info("Phase 6: Connected UnifiedGraph playhead to PlaybackController");
                    }
                    else
                    {
                        _logger.Error($"Phase 6: Failed to connect playhead - PlaybackController: {playbackController != null}, UnifiedGraph: {UnifiedGraph != null}");
                        // This is an error in production - playhead won't sync with audio
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Phase 6: Error connecting playhead to PlaybackController - playhead will not sync with audio");
                }
            });
        }
        
        private void OnServerConnected(bool isConnected)
        {
            if (isConnected)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ServerOverlay?.Close();
                });
            }
        }

        #endregion

        #region LiveCharts Integration (Phase 1)

        private void InitializeLiveChartsVisibility()
        {
            try
            {
                var useLiveCharts = Properties.Settings.Default.UseLiveChartsRenderer;
                
                if (UnifiedGraphContainer != null)
                {
                    UnifiedGraphContainer.Visibility = useLiveCharts ? Visibility.Visible : Visibility.Collapsed;
                }
                
                _logger.Info($"LiveCharts unified graph: {(useLiveCharts ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize LiveCharts visibility");
            }
        }

        #endregion

        #region PlayerHeaderControl Event Handlers

        private void OnSourceTypeSelected(object sender, Events.SourceTypeSelectedEventArgs e)
        {
            if (ViewModel == null) return;

            switch (e.SourceType)
            {
                case Player.SourceType.Server:
                    if (ViewModel.ServerSource?.ConnectCommand?.CanExecute(null) == true)
                    {
                        ViewModel.ServerSource.ConnectCommand.Execute(null);
                    }
                    break;

                case Player.SourceType.File:
                    FileOverlay?.Open();
                    break;
            }
        }

        private void OnFilePanelRequested(object sender, RoutedEventArgs e)
        {
            FileOverlay?.Open();
        }

        private void OnServerPanelRequested(object sender, RoutedEventArgs e)
        {
            ServerOverlay?.Open();
        }

        #endregion

        #region TransportControlsPanel Event Handlers

        private void OnPlayRequested(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.PlayCommand?.CanExecute(null) == true)
            {
                ViewModel.PlayCommand.Execute(null);
            }
        }

        private void OnPauseRequested(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.PauseCommand?.CanExecute(null) == true)
            {
                ViewModel.PauseCommand.Execute(null);
            }
        }

        private void OnStopRequested(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.StopCommand?.CanExecute(null) == true)
            {
                ViewModel.StopCommand.Execute(null);
            }
        }

        #endregion

        #region WaveformDisplayPanel Event Handlers

        private void OnSeekRequested(object sender, Events.SeekRequestedEventArgs e)
        {
            if (ViewModel?.SeekCommand?.CanExecute(e.NormalizedPosition) == true)
            {
                ViewModel.SeekCommand.Execute(e.NormalizedPosition);
            }
        }

        private void OnZoomChanged(object sender, Events.ZoomChangedEventArgs e)
        {
            // Handled via TwoWay binding
        }

        private async void OnWaveformSizeChanged(object sender, Events.WaveformSizeChangedEventArgs e)
        {
            if (ViewModel == null || e.NewWidth <= 0 || e.NewHeight <= 0)
                return;

            try
            {
                var waveformWidth = (int)e.NewWidth;
                var waveformHeight = (int)e.NewHeight;
                await ViewModel.UpdateWaveformAsync(waveformWidth, waveformHeight);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update waveform on size change");
            }
        }

        #endregion

        #region FrequencyMixerPanel Event Handlers

        private void OnFrequencySelectionChanged(object sender, Events.FrequencySelectionChangedEventArgs e)
        {
            ViewModel?.OnFrequencySelectionChanged(e.Frequency, e.IsSelected);
        }

        private void OnMixerValueChanged(object sender, Events.MixerValueChangedEventArgs e)
        {
            if (ViewModel == null) return;

            var frequency = e.Frequency;

            switch (e.Property)
            {
                case "Volume":
                    ViewModel.UpdateChannelGain(frequency.Frequency, e.Value);
                    break;
                case "Pan":
                    ViewModel.UpdateChannelPan(frequency.Frequency, e.Value);
                    break;
                case "Reset":
                    ViewModel.UpdateChannelGain(frequency.Frequency, 1.0f);
                    ViewModel.UpdateChannelPan(frequency.Frequency, 0.0f);
                    ViewModel.UpdateChannelMute(frequency.Frequency, false);
                    ViewModel.UpdateChannelSolo(frequency.Frequency, false);
                    break;
            }
        }

        private void OnMixerBooleanChanged(object sender, Events.MixerBooleanChangedEventArgs e)
        {
            if (ViewModel == null) return;

            var frequency = e.Frequency;

            switch (e.Property)
            {
                case "Mute":
                    ViewModel.UpdateChannelMute(frequency.Frequency, e.Value);
                    break;

                case "Solo":
                    ViewModel.UpdateChannelSolo(frequency.Frequency, e.Value);
                    HandleSoloLogic(frequency, e.Value);
                    break;

                case "PilotSelected":
                    if (e.Player != null)
                    {
                        ViewModel.UpdatePilotSelection(e.Player.TransmitterGuid, e.Value);
                    }
                    break;
            }
        }

        private void HandleSoloLogic(FrequencyViewModel frequency, bool isSoloed)
        {
            if (ViewModel == null) return;

            if (isSoloed)
            {
                foreach (var group in ViewModel.Frequencies)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        if (Math.Abs(freq.Frequency - frequency.Frequency) > 0.1)
                        {
                            freq.IsMuted = true;
                            ViewModel.UpdateChannelMute(freq.Frequency, true);
                        }
                    }
                }
            }
            else
            {
                bool anySoloed = false;
                foreach (var group in ViewModel.Frequencies)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        if (freq.IsSolo)
                        {
                            anySoloed = true;
                            break;
                        }
                    }
                    if (anySoloed) break;
                }

                if (!anySoloed)
                {
                    foreach (var group in ViewModel.Frequencies)
                    {
                        foreach (var freq in group.Frequencies)
                        {
                            freq.IsMuted = false;
                            ViewModel.UpdateChannelMute(freq.Frequency, false);
                        }
                    }
                }
            }
        }

        #endregion

        #region FileSourcePanelOverlay Event Handlers

        private void OnFileSourcePanelClosed(object sender, EventArgs e)
        {
            // Panel closed
        }

        private void OnServerSourcePanelClosed(object sender, EventArgs e)
        {
            // Panel closed
        }

        #endregion
    }
}
