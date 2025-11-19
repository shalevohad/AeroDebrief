using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AeroDebrief.UI.Helpers;
using AeroDebrief.UI.Events;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Visualization.Graphs;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Models;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Audio;
using AeroDebrief.UI.Interfaces.Visualization;
using AeroDebrief.Core.Interfaces.Audio;
using FontAwesome.WPF;

namespace AeroDebrief.UI.Controls.Visualization
{
    /// <summary>
    /// Phase 12: Migrated waveform display panel using UnifiedGraphControl.
    /// Replaces legacy WaveformWithMiniMap/WaveformViewer/WaveformMiniMap with LiveCharts2-based rendering.
    /// </summary>
    public partial class WaveformDisplayPanel : UserControl
    {
        #region Phase 12: Services and ViewModel

        /// <summary>
        /// ViewModel for UnifiedGraphControl - the heart of Phase 12 migration.
        /// </summary>
        public UnifiedGraphViewModel? UnifiedGraphViewModel { get; private set; }

        /// <summary>
        /// Phase 12: Playhead synchronization service for connecting to PlaybackController.
        /// Exposed publicly to allow parent controls to connect to audio playback.
        /// </summary>
        public IPlayheadSyncService? PlayheadSyncService => _playheadSyncService;

        // Services (lazy initialization)
        private IAmplitudeSeriesProvider? _amplitudeProvider;
        private IDataTileManager? _tileManager;
        private IPlayheadSyncService? _playheadSyncService;
        private IErrorHandlingService? _errorHandlingService;
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        #endregion

        #region Dependency Properties

        // Legacy dependency properties removed - now using direct IPacketSource binding via UnifiedGraphControl
        
        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(
                nameof(PlayheadPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0, OnPlayheadPositionChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(
                nameof(ZoomStartTime),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0, OnZoomPropertyChanged));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(
                nameof(ZoomEndTime),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(1.0, OnZoomPropertyChanged));

        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(
                nameof(TotalDuration),
                typeof(TimeSpan),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(TimeSpan.Zero, OnTotalDurationChanged));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(false));

        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register(
                nameof(LoadingMessage),
                typeof(string),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata("Generating waveform..."));

        public static readonly DependencyProperty LoadingProgressProperty =
            DependencyProperty.Register(
                nameof(LoadingProgress),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty EngineTypeProperty =
            DependencyProperty.Register(
                nameof(EngineType),
                typeof(string),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata("LiveCharts2", OnEngineTypeChanged));

        public static readonly DependencyProperty IsUsingGpuProperty =
            DependencyProperty.Register(
                nameof(IsUsingGpu),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true, OnIsUsingGpuChanged));

        public static readonly DependencyProperty EngineStatusColorProperty =
            DependencyProperty.Register(
                nameof(EngineStatusColor),
                typeof(Brush),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(76, 175, 80))));

        public static readonly DependencyProperty EngineStatusTooltipProperty =
            DependencyProperty.Register(
                nameof(EngineStatusTooltip),
                typeof(string),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata("LiveCharts2-based rendering\nHardware-accelerated, high-performance visualization"));

        public static readonly DependencyProperty ShowEngineStatusProperty =
            DependencyProperty.Register(
                nameof(ShowEngineStatus),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowZoomControlsProperty =
            DependencyProperty.Register(
                nameof(ShowZoomControls),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowMinimapProperty =
            DependencyProperty.Register(
                nameof(ShowMinimap),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        public static readonly DependencyProperty MinimapHeightProperty =
            DependencyProperty.Register(
                nameof(MinimapHeight),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(80.0));

        public static readonly DependencyProperty BufferStartPositionProperty =
            DependencyProperty.Register(
                nameof(BufferStartPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty BufferEndPositionProperty =
            DependencyProperty.Register(
                nameof(BufferEndPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        #endregion

        #region Properties

        // Legacy properties removed - WaveformData and FrequencyWaveforms
        // Visualization now handled by UnifiedGraphControl with direct IPacketSource binding

        public double PlayheadPosition
        {
            get => (double)GetValue(PlayheadPositionProperty);
            set => SetValue(PlayheadPositionProperty, value);
        }

        public double ZoomStartTime
        {
            get => (double)GetValue(ZoomStartTimeProperty);
            set => SetValue(ZoomStartTimeProperty, value);
        }

        public double ZoomEndTime
        {
            get => (double)GetValue(ZoomEndTimeProperty);
            set => SetValue(ZoomEndTimeProperty, value);
        }

        public TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public string LoadingMessage
        {
            get => (string)GetValue(LoadingMessageProperty);
            set => SetValue(LoadingMessageProperty, value);
        }

        public double LoadingProgress
        {
            get => (double)GetValue(LoadingProgressProperty);
            set => SetValue(LoadingProgressProperty, value);
        }

        public string EngineType
        {
            get => (string)GetValue(EngineTypeProperty);
            set => SetValue(EngineTypeProperty, value);
        }

        public bool IsUsingGpu
        {
            get => (bool)GetValue(IsUsingGpuProperty);
            set => SetValue(IsUsingGpuProperty, value);
        }

        public Brush EngineStatusColor
        {
            get => (Brush)GetValue(EngineStatusColorProperty);
            set => SetValue(EngineStatusColorProperty, value);
        }

        public string EngineStatusTooltip
        {
            get => (string)GetValue(EngineStatusTooltipProperty);
            set => SetValue(EngineStatusTooltipProperty, value);
        }

        public bool ShowEngineStatus
        {
            get => (bool)GetValue(ShowEngineStatusProperty);
            set => SetValue(ShowEngineStatusProperty, value);
        }

        public bool ShowZoomControls
        {
            get => (bool)GetValue(ShowZoomControlsProperty);
            set => SetValue(ShowZoomControlsProperty, value);
        }

        public bool ShowMinimap
        {
            get => (bool)GetValue(ShowMinimapProperty);
            set => SetValue(ShowMinimapProperty, value);
        }

        public double MinimapHeight
        {
            get => (double)GetValue(MinimapHeightProperty);
            set => SetValue(MinimapHeightProperty, value);
        }

        public double BufferStartPosition
        {
            get => (double)GetValue(BufferStartPositionProperty);
            set => SetValue(BufferStartPositionProperty, value);
        }

        public double BufferEndPosition
        {
            get => (double)GetValue(BufferEndPositionProperty);
            set => SetValue(BufferEndPositionProperty, value);
        }

        #endregion

        #region Routed Events - Using Centralized PlayerEvents

        public event Events.RoutedEventHandler<Events.SeekRequestedEventArgs> SeekRequested
        {
            add => this.AddSeekRequestedHandler(value);
            remove => this.RemoveSeekRequestedHandler(value);
        }

        public event Events.RoutedEventHandler<Events.ZoomChangedEventArgs> ZoomChanged
        {
            add => this.AddZoomChangedHandler(value);
            remove => this.RemoveZoomChangedHandler(value);
        }

        public event Events.RoutedEventHandler<Events.WaveformSizeChangedEventArgs> WaveformSizeChanged
        {
            add => this.AddWaveformSizeChangedHandler(value);
            remove => this.RemoveWaveformSizeChangedHandler(value);
        }

        #endregion

        #region Constructor

        public WaveformDisplayPanel()
        {
            InitializeComponent();

            _logger.Info("Phase 12: WaveformDisplayPanel initializing with UnifiedGraphControl");

            // Initialize services lazily
            InitializeServices();

            // Update engine icon when loaded
            this.Loaded += WaveformDisplayPanel_Loaded;
            this.Unloaded += WaveformDisplayPanel_Unloaded;
            
            // Subscribe to size changes
            this.SizeChanged += WaveformDisplayPanel_SizeChanged;
        }

        #endregion

        #region Phase 12: Service Initialization

        private void InitializeServices()
        {
            try
            {
                // Create amplitude provider WITHOUT real data source initially
                // It will be connected when a recording is loaded via SetRecordingSource()
                _amplitudeProvider = new AmplitudeSeriesProvider();
                _logger.Debug("AmplitudeSeriesProvider created (synthetic data mode - waiting for recording)");

                // Create tile cache with 300MB budget (required by DataTileManager)
                var tileCache = new DataTileCache(budgetMB: 300.0);
                _logger.Debug("DataTileCache created");

                // Create tile manager for scalable data loading
                _tileManager = new DataTileManager(tileCache);
                _logger.Debug("DataTileManager created");

                // Create playhead sync service
                _playheadSyncService = new PlayheadSyncService();
                _logger.Debug("PlayheadSyncService created");

                // Create error handling service
                _errorHandlingService = new ErrorHandlingService(_logger);
                _logger.Debug("ErrorHandlingService created");

                // Create UnifiedGraphViewModel with all services
                UnifiedGraphViewModel = new UnifiedGraphViewModel(
                    _amplitudeProvider,
                    tileCache: tileCache,
                    mixerController: null, // Will be connected later if needed
                    tileManager: _tileManager,
                    errorHandler: _errorHandlingService
                );

                _logger.Info("Phase 12: UnifiedGraphViewModel initialized successfully");

                // Subscribe to ViewModel events
                SubscribeToViewModelEvents();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to initialize services");
            }
        }

        /// <summary>
        /// Phase 12: Connects the waveform display to a real recording data source.
        /// Call this when a recording is loaded to switch from synthetic to real data.
        /// </summary>
        public void SetRecordingSource(FilePacketSource packetSource, IAudioProcessingEngine audioEngine)
        {
            try
            {
                _logger.Info("Phase 12: Connecting to real recording data source");
                
                // Create new amplitude provider with real data pipeline
                _amplitudeProvider = new AmplitudeSeriesProvider(packetSource, audioEngine);
                
                // Recreate tile cache and manager
                var tileCache = new DataTileCache(budgetMB: 300.0);
                _tileManager = new DataTileManager(tileCache);
                
                // Recreate UnifiedGraphViewModel with new provider
                UnifiedGraphViewModel?.Dispose();
                UnifiedGraphViewModel = new UnifiedGraphViewModel(
                    _amplitudeProvider,
                    tileCache: tileCache,
                    mixerController: null,
                    tileManager: _tileManager,
                    errorHandler: _errorHandlingService
                );
                
                // Re-subscribe to events
                SubscribeToViewModelEvents();
                
                _logger.Info("Phase 12: Recording data source connected successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to set recording source");
            }
        }

        private void SubscribeToViewModelEvents()
        {
            if (UnifiedGraphViewModel == null) return;

            // Subscribe to playhead sync service
            if (_playheadSyncService != null)
            {
                _playheadSyncService.TimeChanged += (s, time) =>
                {
                    // Update ViewModel playhead
                    if (UnifiedGraphViewModel != null)
                    {
                        UnifiedGraphViewModel.PlayheadTime = time;
                    }
                };
            }
            
            _logger.Debug("Phase 12: Subscribed to ViewModel events");
        }

        #endregion

        #region Event Handlers

        private void WaveformDisplayPanel_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEngineIcon();
            UpdateEngineStatusVisuals();
            _logger.Debug("Phase 12: WaveformDisplayPanel loaded");
        }

        private void WaveformDisplayPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup if needed
            if (_playheadSyncService is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _logger.Debug("Phase 12: WaveformDisplayPanel unloaded");
        }

        private void WaveformDisplayPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                this.RaiseWaveformSizeChanged(e.NewSize.Width, e.NewSize.Height);
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        #endregion

        #region Public Methods - Phase 12 Implementation

        /// <summary>
        /// Phase 12: Zooms in using UnifiedGraphViewModel.
        /// </summary>
        public void ZoomIn()
        {
            if (UnifiedGraphViewModel != null)
            {
                UnifiedGraphViewModel.ZoomIn(2.0); // 2x zoom in
                _logger.Debug("Phase 12: Zoom in requested");
            }
        }

        /// <summary>
        /// Phase 12: Zooms out using UnifiedGraphViewModel.
        /// </summary>
        public void ZoomOut()
        {
            if (UnifiedGraphViewModel != null)
            {
                UnifiedGraphViewModel.ZoomOut(0.5); // 2x zoom out
                _logger.Debug("Phase 12: Zoom out requested");
            }
        }

        /// <summary>
        /// Phase 12: Resets zoom using UnifiedGraphViewModel.
        /// </summary>
        public void ResetZoom()
        {
            if (UnifiedGraphViewModel != null)
            {
                UnifiedGraphViewModel.ResetViewport();
                _logger.Debug("Phase 12: Zoom reset requested");
            }
        }

        /// <summary>
        /// Phase 12: Zooms to a specific time region.
        /// </summary>
        public void ZoomToRegion(double startTime, double endTime)
        {
            if (UnifiedGraphViewModel != null && TotalDuration.TotalSeconds > 0)
            {
                // Convert normalized times to DateTime
                var start = DateTime.Now;
                var duration = TotalDuration;
                var viewportStart = start.AddSeconds(startTime * duration.TotalSeconds);
                var viewportEnd = start.AddSeconds(endTime * duration.TotalSeconds);

                UnifiedGraphViewModel.SetViewport(viewportStart, viewportEnd);
                _logger.Debug($"Phase 12: Zoom to region {startTime:F2} - {endTime:F2}");
            }
        }

        #endregion

        #region Property Change Handlers - Phase 12

        private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdatePlayheadPosition();
            }
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateTotalDuration();
            }
        }

        private static void OnZoomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.RaiseZoomChangedEvent();
            }
        }

        private static void OnEngineTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateEngineIcon();
                panel.UpdateEngineStatusVisuals();
            }
        }

        private static void OnIsUsingGpuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateEngineIcon();
                panel.UpdateEngineStatusVisuals();
            }
        }

        #endregion

        #region Phase 12: Data Update Methods

        private async void UpdateWaveformData()
        {
            if (UnifiedGraphViewModel == null) return;

            try
            {
                _logger.Debug("Phase 12: Loading waveform data");
                
                // Determine time range - use TotalDuration if available
                var start = DateTime.Now;
                var end = TotalDuration.TotalSeconds > 0 
                    ? start.Add(TotalDuration) 
                    : start.AddHours(1);

                // Set time range on playhead service
                if (_playheadSyncService != null)
                {
                    _playheadSyncService.SetTimeRange(start, end);
                }

                // Load data using tile-based loading
                await UnifiedGraphViewModel.LoadDataAsync(start, end);
                
                _logger.Info($"Phase 12: Waveform data loaded successfully (Duration: {TotalDuration})");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to load waveform data");
            }
        }

        private async void UpdateFrequencyWaveforms()
        {
            // Legacy method - now handled by UpdateWaveformData
            await Task.Run(() => UpdateWaveformData());
        }

        private void UpdatePlayheadPosition()
        {
            if (_playheadSyncService == null || TotalDuration.TotalSeconds <= 0) return;

            try
            {
                // Convert normalized position (0-1) to DateTime
                var start = _playheadSyncService.StartTime;
                var duration = _playheadSyncService.EndTime - _playheadSyncService.StartTime;
                var currentTime = start.AddSeconds(PlayheadPosition * duration.TotalSeconds);
                
                _playheadSyncService.Seek(currentTime);
                
                // Also update ViewModel
                if (UnifiedGraphViewModel != null)
                {
                    UnifiedGraphViewModel.PlayheadTime = currentTime;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to update playhead position");
            }
        }

        private void UpdateTotalDuration()
        {
            if (UnifiedGraphViewModel == null || TotalDuration.TotalSeconds <= 0) return;

            try
            {
                var start = DateTime.Now;
                var end = start.Add(TotalDuration);
                
                // Set time range on playhead service
                if (_playheadSyncService != null)
                {
                    _playheadSyncService.SetTimeRange(start, end);
                }

                // Set viewport on ViewModel to show full range
                UnifiedGraphViewModel.Start = start;
                UnifiedGraphViewModel.End = end;
                UnifiedGraphViewModel.ViewportStart = start;
                UnifiedGraphViewModel.ViewportEnd = end;
                
                _logger.Debug($"Phase 12: Total duration set to {TotalDuration}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to update total duration");
            }
        }

        #endregion

        #region Private Helper Methods

        private void UpdateEngineIcon()
        {
            if (EngineIconHost == null)
                return;

            try
            {
                // LiveCharts2 icon (chart/graph)
                var faIcon = FontAwesomeIcon.LineChart;
                var brush = new SolidColorBrush(Colors.White);
                var element = IconHelper.CreateFaIcon(faIcon, 14, brush);

                Dispatcher.Invoke(() =>
                {
                    EngineIconHost.Content = element;
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update engine icon");
            }
        }

        private void UpdateEngineStatusVisuals()
        {
            // LiveCharts2 gets a vibrant green to indicate new modern rendering
            var liveChartsBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Material Green

            EngineStatusColor = liveChartsBrush;
            EngineStatusTooltip = "LiveCharts2-based rendering\nHardware-accelerated, high-performance visualization\nPhase 12 Migration Complete ?";
        }

        private void RaiseZoomChangedEvent()
        {
            this.RaiseZoomChanged(ZoomStartTime, ZoomEndTime, 1.0 / (ZoomEndTime - ZoomStartTime));
        }

        #endregion
    }
}
