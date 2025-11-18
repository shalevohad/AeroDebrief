using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using AeroDebrief.UI.Services.Graphs;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Charts;
using AeroDebrief.UI.Models;
using AeroDebrief.UI.Commands;
using SkiaSharp;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// ViewModel for LiveCharts2 amplitude visualization.
    /// Phase 4: Full frequency/pilot visibility integration with density-aware rendering.
    /// Phase 5: Viewport management for minimap and zoom/pan support.
    /// Phase 6: Playhead synchronization with audio playback.
    /// Phase 7: Visibility toggle with audio mixer synchronization.
    /// Phase 8: Tile-based data loading for scalability.
    /// Phase 9: Loading indicators and error handling.
    /// Phase 12: Support switching data sources (e.g., from synthetic to real recording data).
    /// </summary>
    public class UnifiedGraphViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<ISeries> Series { get; } = new();

        private DateTime _start = DateTime.Now;
        private DateTime _end;
        private int _totalPoints = 0;
        private int _visibleSeriesCount = 0;
        private double _zoomLevel = 1.0;
        
        // Phase 5: Viewport management for zoom/pan
        private DateTime _viewportStart = DateTime.Now;
        private DateTime _viewportEnd = DateTime.Now.AddHours(1);
        
        private IAmplitudeSeriesProvider _amplitudeProvider;
        private IDataTileCache? _tileCache;
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        // Phase 7: Audio mixer integration for sync
        private readonly MixerController? _mixerController;
        private bool _audioSyncEnabled = true;

        // Phase 8: Tile-based data loading
        private IDataTileManager? _tileManager;
        private bool _isTileBasedLoadingEnabled = false;
        private bool _isLoadingTiles = false;
        private CancellationTokenSource? _currentLoadCancellation;

        // Phase 9: Loading status and cancellation
        private string _loadingStatusText = "Loading tiles...";
        
        // Phase 9 Step 2: Error handling service
        private readonly IErrorHandlingService? _errorHandler;

        // Phase 9 Step 3: Performance monitoring
        private bool _showPerformanceStats = false;
        private double _memoryUsageMB = 0.0;
        private double _cacheHitRate = 0.0;
        private int _loadedTileCount = 0;
        private double _currentFPS = 0.0;
        private double _lastLoadTimeMs = 0.0;
        private int _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.UtcNow;
        private System.Windows.Threading.DispatcherTimer? _performanceUpdateTimer;

        // Series management for visibility toggles
        private readonly Dictionary<string, ISeries> _allSeries = new();
        private readonly Dictionary<string, bool> _seriesVisibility = new();
        private readonly Dictionary<string, HashSet<string>> _frequencyPilots = new(); // FreqId -> PilotIds

        // Density management for high-pilot frequencies
        private readonly Dictionary<string, bool> _frequencyExpanded = new();
        private const int DefaultMaxPilotsVisible = 8;
        private int _maxPilotsPerFrequency = DefaultMaxPilotsVisible;

        // Phase 6: Playhead synchronization
        private DateTime _playheadTime;
        private bool _isPlaying;
        private double _playbackRate = 1.0;
        private bool _followMode = true;

        // Phase 7: Event loop prevention for bidirectional sync
        private bool _isSyncingVisibility = false;
        
        // Phase 8: Prevent infinite loop during initial data load
        private bool _isLoadingData = false;
        
        // Phase 5: Track whether data was loaded via LoadDataAsync (for viewport clamping logic)
        private bool _isDataLoadedExplicitly = false;

        public UnifiedGraphViewModel() : this(new AmplitudeSeriesProvider(), null, null, null, null)
        {
        }

        public UnifiedGraphViewModel(
            IAmplitudeSeriesProvider amplitudeProvider, 
            IDataTileCache? tileCache = null, 
            MixerController? mixerController = null,
            IDataTileManager? tileManager = null,
            IErrorHandlingService? errorHandler = null)
        {
            _amplitudeProvider = amplitudeProvider ?? throw new ArgumentNullException(nameof(amplitudeProvider));
            _tileCache = tileCache;
            _mixerController = mixerController;
            _tileManager = tileManager;
            _errorHandler = errorHandler;
            
            // Phase 8: DISABLE tile-based loading temporarily - it causes infinite loops
            // The tile system needs pre-generated tiles, not on-demand generation from amplitude provider
            // TODO: Implement proper tile pre-generation or database storage
            _isTileBasedLoadingEnabled = false; // Forced to false until tile generation is fixed
            
            _logger.Info($"UnifiedGraphViewModel initialized (Phase 8: TileBasedLoading={_isTileBasedLoadingEnabled} [DISABLED - see TODO], Phase 9: ErrorHandling={_errorHandler != null})");

            // Phase 7: Subscribe to mixer events if available
            if (_mixerController != null)
            {
                _mixerController.ChannelChanged += OnMixerChannelChanged;
                _logger.Debug("Phase 7: Subscribed to MixerController events");
            }
        }

        public int TotalPoints => _totalPoints;
        
        public int VisibleSeriesCount
        {
            get => _visibleSeriesCount;
            private set
            {
                if (_visibleSeriesCount != value)
                {
                    _visibleSeriesCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (Math.Abs(_zoomLevel - value) > 0.01)
                {
                    _zoomLevel = value;
                    OnPropertyChanged();
                    // Phase 4: Adjust marker density based on zoom
                    UpdateMarkerDensity();
                }
            }
        }

        public DateTime Start
        {
            get => _start;
            set { _start = value; OnPropertyChanged(); }
        }

        public DateTime End
        {
            get => _end;
            set { _end = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Phase 5: Start of visible viewport in main chart.
        /// Used for minimap synchronization and zoom/pan operations.
        /// Phase 8: Triggers tile loading when viewport changes.
        /// </summary>
        public DateTime ViewportStart
        {
            get => _viewportStart;
            set
            {
                if (_viewportStart != value)
                {
                    _viewportStart = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ViewportDuration));
                    ViewportChanged?.Invoke(this, EventArgs.Empty);
                    
                    // Phase 8: Load tiles for new viewport (but not during initial data load)
                    if (!_isLoadingData)
                    {
                        _ = LoadTilesForCurrentViewportAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Phase 5: End of visible viewport in main chart.
        /// Used for minimap synchronization and zoom/pan operations.
        /// Phase 8: Triggers tile loading when viewport changes.
        /// </summary>
        public DateTime ViewportEnd
        {
            get => _viewportEnd;
            set
            {
                if (_viewportEnd != value)
                {
                    _viewportEnd = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ViewportDuration));
                    ViewportChanged?.Invoke(this, EventArgs.Empty);
                    
                    // Phase 8: Load tiles for new viewport (but not during initial data load)
                    if (!_isLoadingData)
                    {
                        _ = LoadTilesForCurrentViewportAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Phase 5: Duration of current viewport.
        /// </summary>
        public TimeSpan ViewportDuration
        {
            get => ViewportEnd - ViewportStart;
            set
            {
                // Update ViewportEnd to reflect the new duration while keeping ViewportStart fixed
                var newEnd = ViewportStart + value;
                if (newEnd != ViewportEnd)
                {
                    ViewportEnd = newEnd;
                    // ViewportEnd setter will handle property change notifications
                }
            }
        }

        /// <summary>
        /// Phase 5: Raised when viewport changes (zoom or pan).
        /// </summary>
        public event EventHandler? ViewportChanged;
        
        /// <summary>
        /// Phase 8: Indicates whether tiles are currently being loaded.
        /// Phase 9: Used to show/hide loading spinner overlay.
        /// </summary>
        public bool IsLoadingTiles
        {
            get => _isLoadingTiles;
            private set
            {
                if (_isLoadingTiles != value)
                {
                    _isLoadingTiles = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 1: Status text for loading indicator (e.g., "Loading tiles...", "Processing data...").
        /// </summary>
        public string LoadingStatusText
        {
            get => _loadingStatusText;
            private set
            {
                if (_loadingStatusText != value)
                {
                    _loadingStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 1: Command to cancel the current loading operation.
        /// </summary>
        public ICommand CancelLoadingCommand => new RelayCommand(
            execute: () => CancelLoading(),
            canExecute: () => IsLoadingTiles);

        /// <summary>
        /// Phase 6: Current playhead position synchronized with audio playback.
        /// </summary>
        public DateTime PlayheadTime
        {
            get => _playheadTime;
            set
            {
                if (_playheadTime != value)
                {
                    _playheadTime = value;
                    OnPropertyChanged();
                    PlayheadTimeChanged?.Invoke(this, value);

                    // Auto-pan in follow mode
                    if (FollowMode && IsPlaying)
                    {
                        UpdateViewportForPlayhead();
                    }
                }
            }
        }

        /// <summary>
        /// Phase 6: Whether audio playback is currently active.
        /// </summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 6: Current playback rate (0.5x, 1x, 2x, etc.).
        /// </summary>
        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                if (Math.Abs(_playbackRate - value) > 0.01)
                {
                    _playbackRate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 6: Whether follow mode is active (auto-pan to keep playhead centered).
        /// </summary>
        public bool FollowMode
        {
            get => _followMode;
            set
            {
                if (_followMode != value)
                {
                    _followMode = value;
                    OnPropertyChanged();
                    _logger.Debug($"Follow mode: {value}");
                }
            }
        }

        /// <summary>
        /// Phase 6: Raised when playhead time changes.
        /// </summary>
        public event EventHandler<DateTime>? PlayheadTimeChanged;

        /// <summary>
        /// Gets or sets the maximum number of pilots to show per frequency before collapsing.
        /// </summary>
        public int MaxPilotsPerFrequency
        {
            get => _maxPilotsPerFrequency;
            set
            {
                if (_maxPilotsPerFrequency != value && value > 0)
                {
                    _maxPilotsPerFrequency = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 7: Gets or sets whether audio mixer synchronization is enabled.
        /// When enabled, visibility changes in the graph will sync to the audio mixer (mute/unmute).
        /// </summary>
        public bool AudioSyncEnabled
        {
            get => _audioSyncEnabled;
            set
            {
                if (_audioSyncEnabled != value)
                {
                    _audioSyncEnabled = value;
                    OnPropertyChanged();
                    _logger.Debug($"Audio sync: {value}");
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Gets or sets whether performance stats overlay is visible.
        /// Toggle with F3 key.
        /// </summary>
        public bool ShowPerformanceStats
        {
            get => _showPerformanceStats;
            set
            {
                if (_showPerformanceStats != value)
                {
                    _showPerformanceStats = value;
                    OnPropertyChanged();
                    _logger.Debug($"Performance stats: {value}");
                    
                    // Start/stop performance monitoring timer
                    if (value && _performanceUpdateTimer == null)
                    {
                        StartPerformanceMonitoring();
                    }
                    else if (!value && _performanceUpdateTimer != null)
                    {
                        StopPerformanceMonitoring();
                    }
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Current memory usage in MB.
        /// </summary>
        public double MemoryUsageMB
        {
            get => _memoryUsageMB;
            private set
            {
                if (Math.Abs(_memoryUsageMB - value) > 0.1)
                {
                    _memoryUsageMB = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Cache hit rate (0.0 to 1.0).
        /// </summary>
        public double CacheHitRate
        {
            get => _cacheHitRate;
            private set
            {
                if (Math.Abs(_cacheHitRate - value) > 0.01)
                {
                    _cacheHitRate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Number of loaded tiles in cache.
        /// </summary>
        public int LoadedTileCount
        {
            get => _loadedTileCount;
            private set
            {
                if (_loadedTileCount != value)
                {
                    _loadedTileCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Current frames per second.
        /// </summary>
        public double CurrentFPS
        {
            get => _currentFPS;
            private set
            {
                if (Math.Abs(_currentFPS - value) > 0.5)
                {
                    _currentFPS = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Last tile load time in milliseconds.
        /// </summary>
        public double LastLoadTimeMs
        {
            get => _lastLoadTimeMs;
            private set
            {
                if (Math.Abs(_lastLoadTimeMs - value) > 1.0)
                {
                    _lastLoadTimeMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Load amplitude data for the specified time range.
        /// Phase 4: Creates series with colors and markers, applies density management.
        /// Phase 8: Tile-based loading for improved performance with large data ranges.
        /// Phase 9 Step 2: Error handling with user-friendly messages and recovery options.
        /// </summary>
        public async Task LoadDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"Loading amplitude data from {start:HH:mm:ss} to {end:HH:mm:ss}");
                
                // Set flag to prevent infinite loop from viewport changes
                _isLoadingData = true;
                
                Start = start;
                End = end;
                _isDataLoadedExplicitly = true; // Mark that data was explicitly loaded
                Series.Clear();
                _allSeries.Clear();
                _seriesVisibility.Clear();
                _frequencyPilots.Clear();
                _frequencyExpanded.Clear();
                _totalPoints = 0;

                // CRITICAL: Set recording start time in tile manager for tile generation
                if (_tileManager != null)
                {
                    _tileManager.SetRecordingStart(start);
                    _logger.Debug($"Tile manager recording start set to {start:yyyy-MM-dd HH:mm:ss}");
                }

                // Phase 8: Tile-based loading
                if (_isTileBasedLoadingEnabled)
                {
                    await LoadTileBasedDataAsync(start, end, cancellationToken);
                }
                else
                {
                    await LoadRawDataAsync(start, end, cancellationToken);
                }

                // Phase 5: Initialize viewport to show full range
                // This won't trigger LoadTilesForCurrentViewportAsync because _isLoadingData is true
                ViewportStart = start;
                ViewportEnd = end;
                
                // Phase 4: Apply marker density based on current zoom level
                UpdateMarkerDensity();
                
                _logger.Debug($"Viewport initialized: {start:HH:mm:ss} to {end:HH:mm:ss}");
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Data load cancelled by user");
                _errorHandler?.ShowWarning("Data loading was cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load amplitude data");
                
                // Phase 9 Step 2: Show user-friendly error with retry option
                if (_errorHandler != null)
                {
                    await _errorHandler.ShowErrorAsync(
                        "Data Load Error",
                        "Failed to load chart data. This may be due to a database connection issue or corrupted data.",
                        ex,
                        ErrorSeverity.Error,
                        new ErrorAction("Retry", async () => await LoadDataAsync(start, end, cancellationToken))
                    );
                }
                
                throw;
            }
            finally
            {
                // Clear flag to allow viewport changes to trigger tile loading
                _isLoadingData = false;
            }
        }

        /// <summary>
        /// Phase 8: Load data using tile-based manager for improved performance.
        /// Only loads tiles for the current viewport with preload buffer.
        /// </summary>
        private async Task LoadTileBasedDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            try
            {
                if (_tileManager == null)
                {
                    _logger.Warn("TileManager not available, falling back to raw data load");
                    await LoadRawDataAsync(start, end, cancellationToken);
                    return;
                }

                _logger.Info($"Tile-based data load: {start:HH:mm:ss} to {end:HH:mm:ss}");

                // For initial load, get all visible frequencies from the data
                var visibleFrequencies = await GetVisibleFrequenciesAsync(start, end, cancellationToken);
                
                _logger.Debug($"Found {visibleFrequencies.Count} frequencies in recording");

                // Initialize frequency/pilot tracking
                foreach (var freq in visibleFrequencies)
                {
                    var freqId = GetFrequencyKey(freq);
                    if (!_frequencyPilots.ContainsKey(freqId))
                    {
                        _frequencyPilots[freqId] = new HashSet<string>();
                    }
                }

                // Load tiles for the viewport
                await LoadTilesForViewportInternalAsync(start, end, visibleFrequencies, cancellationToken);

                _logger.Info($"Tile-based load complete: {_allSeries.Count} series, {TotalPoints} points");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during tile-based data load");
                // Phase 9 Step 2: Show error with retry option
                if (_errorHandler != null)
                {
                    await _errorHandler.ShowErrorAsync(
                        "Data Load Error",
                        "Failed to load tile-based chart data. This may be due to a database issue.",
                        ex,
                        ErrorSeverity.Error,
                        new ErrorAction("Retry", async () => await LoadTileBasedDataAsync(start, end, cancellationToken))
                    );
                }
                throw;
            }
        }

        /// <summary>
        /// Phase 8: Load tiles for the current viewport with preload buffer.
        /// Called automatically when viewport changes (pan/zoom).
        /// Phase 9 Step 2: Enhanced error handling with graceful degradation.
        /// </summary>
        private async Task LoadTilesForCurrentViewportAsync()
        {
            if (!_isTileBasedLoadingEnabled || _tileManager == null)
                return;

            // Cancel any pending load
            _currentLoadCancellation?.Cancel();
            _currentLoadCancellation = new CancellationTokenSource();
            var ct = _currentLoadCancellation.Token;

            try
            {
                // Get visible frequencies from current series visibility state
                var visibleFrequencies = _frequencyPilots.Keys
                    .Where(freqId => _seriesVisibility.Any(kvp => kvp.Key.StartsWith(freqId + "-") && kvp.Value))
                    .Select(freqId => ParseFrequencyFromKey(freqId))
                    .ToList();

                if (visibleFrequencies.Count == 0)
                {
                    _logger.Debug("No visible frequencies, skipping tile load");
                    return;
                }

                await LoadTilesForViewportInternalAsync(ViewportStart, ViewportEnd, visibleFrequencies, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Tile load cancelled (viewport changed)");
                // Don't show error for user-initiated cancellations
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading tiles for viewport");
                
                // Phase 9 Step 2: Show warning for viewport load errors (non-critical, allows graceful degradation)
                _errorHandler?.ShowWarning(
                    $"Some chart data could not be loaded. You may see gaps in the display."
                );
            }
        }

        /// <summary>
        /// Phase 9 Step 1: Cancel the current tile loading operation.
        /// Called when user clicks the cancel button in the loading spinner overlay.
        /// </summary>
        private void CancelLoading()
        {
            _logger.Info("UserCancelled tile loading");
            _currentLoadCancellation?.Cancel();
            LoadingStatusText = "Cancelling...";
        }

        /// <summary>
        /// Phase 8: Internal method to load tiles for a specific viewport and frequencies.
        /// Phase 9: Updates loading status text with progress.
        /// Phase 9 Step 2: Enhanced error handling with detailed error messages.
        /// Phase 9 Step 3: Tracks load time for performance monitoring.
        /// </summary>
        private async Task LoadTilesForViewportInternalAsync(
            DateTime viewportStart,
            DateTime viewportEnd,
            List<double> visibleFrequencies,
            CancellationToken cancellationToken)
        {
            if (_tileManager == null)
                return;

            IsLoadingTiles = true;
            LoadingStatusText = "Loading tiles...";
            
            // CRITICAL: Set _isLoadingData to prevent viewport changes during tile processing
            var wasLoadingData = _isLoadingData;
            _isLoadingData = true;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Calculate zoom level from viewport duration
                var fullDuration = (End - Start).TotalSeconds;
                var viewportDuration = (viewportEnd - viewportStart).TotalSeconds;
                var zoomLevel = fullDuration > 0 ? fullDuration / viewportDuration : 1.0;

                _logger.Debug($"Loading tiles: viewport={viewportDuration:F1}s, zoom={zoomLevel:F2}");
                LoadingStatusText = $"Loading tiles for {visibleFrequencies.Count} frequencies...";

                // Load tiles from manager (includes preload buffer)
                var tiles = await _tileManager.LoadTilesForViewportAsync(
                    viewportStart,
                    viewportEnd,
                    zoomLevel,
                    visibleFrequencies,
                    cancellationToken);

                var tileList = tiles.ToList();
                _logger.Info($"Loaded {tileList.Count} tiles");
                LoadingStatusText = $"Processing {tileList.Count} tiles...";

                // Process tiles and update series (protected by _isLoadingData flag)
                ProcessTiles(tileList);

                LoadingStatusText = "Optimizing memory...";
                // Unload tiles outside viewport to free memory
                _tileManager.UnloadTilesOutsideViewport(viewportStart, viewportEnd);

                // Log memory usage
                var memoryMB = _tileManager.GetMemoryUsage() / (1024.0 * 1024.0);
                var stats = _tileManager.GetStats();
                _logger.Debug($"Tile cache: {stats.LoadedTileCount} tiles, {memoryMB:F1} MB, hit rate={stats.CacheHitRate:P0}");
                
                // Phase 9 Step 3: Record load time
                stopwatch.Stop();
                LastLoadTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                _logger.Debug($"Tile load completed in {LastLoadTimeMs:F0} ms");
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Tile loading cancelled by user");
                LoadingStatusText = "Loading cancelled";
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during tile loading");
                LoadingStatusText = "Error loading tiles";
                
                // Phase 9 Step 2: Show specific error based on exception type
                if (_errorHandler != null)
                {
                    var errorMessage = ex is OutOfMemoryException
                        ? "Not enough memory to load chart data. Try zooming in or closing other applications."
                        : "Failed to load chart tiles. The data may be corrupted or the database connection was lost.";

                    await _errorHandler.ShowErrorAsync(
                        "Tile Load Error",
                        errorMessage,
                        ex,
                        ErrorSeverity.Error,
                        new ErrorAction("Retry", async () => 
                            await LoadTilesForViewportInternalAsync(viewportStart, viewportEnd, visibleFrequencies, CancellationToken.None))
                    );
                }
                
                throw;
            }
            finally
            {
                IsLoadingTiles = false;
                stopwatch.Stop();
                
                // Restore previous _isLoadingData state (only clear if we set it)
                if (!wasLoadingData)
                {
                    _isLoadingData = false;
                }
            }
        }

        /// <summary>
        /// Phase 8: Process loaded tiles and update series data.
        /// </summary>
        private void ProcessTiles(List<SeriesTile> tiles)
        {
            _logger.Debug($"ProcessTiles: Processing {tiles.Count} tiles");
            
            // CRITICAL: Batch all series additions to prevent multiple property change notifications
            var seriesToAdd = new List<ISeries>();
            
            foreach (var tile in tiles)
            {
                var frequencyId = GetFrequencyKey(tile.Frequency);
                var pilotId = tile.PilotId;
                var key = string.IsNullOrEmpty(pilotId) 
                    ? frequencyId 
                    : $"{frequencyId}-{pilotId}";

                _logger.Debug($"Processing tile: Freq={tile.Frequency} Hz, FreqId={frequencyId}, PilotId={pilotId ?? "(null)"}, Key={key}");

                // Track pilot for this frequency
                if (!string.IsNullOrEmpty(pilotId))
                {
                    if (!_frequencyPilots.ContainsKey(frequencyId))
                        _frequencyPilots[frequencyId] = new HashSet<string>();
                    
                    _frequencyPilots[frequencyId].Add(pilotId);
                    _logger.Debug($"Added pilot '{pilotId}' to frequency {frequencyId}. Total pilots: {_frequencyPilots[frequencyId].Count}");
                }

                // Create or get series
                ISeries? series;
                if (!_allSeries.TryGetValue(key, out series))
                {
                    // Convert tile points to ObservablePoint
                    var points = tile.Points.Select(p => 
                        new ObservablePoint((p.Time - Start).TotalSeconds, p.Amplitude));
                    
                    series = CreateLineSeries(frequencyId, pilotId ?? "FREQ", points);
                    _allSeries[key] = series;
                    
                    // Set initial visibility
                    var shouldCollapse = _frequencyPilots.TryGetValue(frequencyId, out var pilots) 
                        && pilots.Count > MaxPilotsPerFrequency;
                    var initialVisibility = !shouldCollapse;
                    _seriesVisibility[key] = initialVisibility;
                    
                    _logger.Debug($"Created new series: Key={key}, SeriesName={series.Name}, Visible={initialVisibility}, Points={tile.Points.Count}");
                    
                    // Phase 7: Set IsVisible property
                    series.IsVisible = initialVisibility;
                    
                    // Add to batch list instead of adding directly
                    seriesToAdd.Add(series);
                }
                else
                {
                    // Update existing series with tile data
                    if (series is LineSeries<ObservablePoint> lineSeries)
                    {
                        var existingPoints = lineSeries.Values as ObservablePoint[] ?? Array.Empty<ObservablePoint>();
                        var newPoints = tile.Points.Select(p => 
                            new ObservablePoint((p.Time - Start).TotalSeconds, p.Amplitude));
                        
                        // Merge points (remove duplicates by time)
                        var allPoints = existingPoints
                            .Concat(newPoints)
                            .GroupBy(p => p.X)
                            .Select(g => g.First())
                            .OrderBy(p => p.X)
                            .ToArray();
                        
                        lineSeries.Values = allPoints;
                        _logger.Debug($"Updated existing series: Key={key}, Points={allPoints.Length} (was {existingPoints.Length}, added {newPoints.Count()})");
                    }
                }
            }

            // Add all new series in one batch to minimize property change notifications
            if (seriesToAdd.Count > 0)
            {
                _logger.Debug($"Adding {seriesToAdd.Count} new series to collection");
                foreach (var series in seriesToAdd)
                {
                    Series.Add(series);
                }
            }

            // Update visible count
            VisibleSeriesCount = Series.Count(s => s.IsVisible);
            
            _logger.Debug($"ProcessTiles complete: {_allSeries.Count} series, {_frequencyPilots.Count} frequencies");
            foreach (var (freqId, pilots) in _frequencyPilots)
            {
                _logger.Debug($"  Frequency {freqId}: {pilots.Count} pilots = [{string.Join(", ", pilots)}]");
            }
        }

        /// <summary>
        /// Phase 8: Get visible frequencies from recording metadata.
        /// </summary>
        private async Task<List<double>> GetVisibleFrequenciesAsync(
            DateTime start,
            DateTime end,
            CancellationToken cancellationToken)
        {
            var frequencies = new HashSet<double>();

            await foreach (var (key, _) in _amplitudeProvider.GetSeriesAsync(start, end, cancellationToken))
            {
                var parsed = ParseSeriesKey(key);
                if (parsed.frequencyId != null && double.TryParse(parsed.frequencyId, out var freq))
                {
                    frequencies.Add(freq);
                }
            }

            return frequencies.ToList();
        }

        /// <summary>
        /// Phase 8: Parse frequency from frequency ID key.
        /// </summary>
        private double ParseFrequencyFromKey(string frequencyId)
        {
            if (double.TryParse(frequencyId, out var freq))
                return freq;
            
            return 0;
        }

        /// <summary>
        /// Load raw data without tile-based system (fallback or for small datasets).
        /// </summary>
        private async Task LoadRawDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            _logger.Info("Loading raw data (non-tiled)");
            
            await foreach (var (key, points) in _amplitudeProvider.GetSeriesAsync(start, end, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var parsed = ParseSeriesKey(key);
                if (parsed.frequencyId == null)
                {
                    _logger.Warn($"Invalid series key format: {key}");
                    continue;
                }

                var pointsList = points.ToList();
                _totalPoints += pointsList.Count;

                // Track pilots per frequency
                if (!_frequencyPilots.ContainsKey(parsed.frequencyId))
                {
                    _frequencyPilots[parsed.frequencyId] = new HashSet<string>();
                }

                if (parsed.pilotId != null)
                {
                    _frequencyPilots[parsed.frequencyId].Add(parsed.pilotId);
                }

                // Create series
                var series = CreateLineSeries(parsed.frequencyId, parsed.pilotId ?? "FREQ", pointsList);
                _allSeries[key] = series;

                // Determine initial visibility
                var shouldCollapse = _frequencyPilots[parsed.frequencyId].Count > MaxPilotsPerFrequency;
                var initialVisibility = !shouldCollapse;
                _seriesVisibility[key] = initialVisibility;
                
                // Phase 7: Add series to collection with IsVisible set
                series.IsVisible = initialVisibility;
                Series.Add(series);
            }

            _logger.Info($"Loaded {_allSeries.Count} series, {_totalPoints:N0} points");
            
            // Update visible count based on IsVisible property
            VisibleSeriesCount = Series.Count(s => s.IsVisible);
        }

        /// <summary>
        /// Phase 4: Creates a LineSeries with appropriate colors and markers.
        /// </summary>
        private LineSeries<ObservablePoint> CreateLineSeries(
            string frequencyId, 
            string pilotId,
            IEnumerable<ObservablePoint> points)
        {
            var color = ChartColors.GetColorForFrequency(frequencyId);
            // Note: Custom markers via SKPath not directly supported in LiveCharts2
            // Using default circle geometry with size variation for different pilots

            return new LineSeries<ObservablePoint>
            {
                Name = $"{frequencyId}-{pilotId}",
                Values = points.ToArray(),
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(color),
                GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 1 },
                LineSmoothness = 0
            };
        }

        /// <summary>
        /// Parse series key into frequency and pilot IDs.
        /// Format from AmplitudeSeriesProvider: "F251.0-P1" (MHz with F prefix, pilot index with P prefix)
        /// Also supports legacy formats: "251000000-PILOT123" or "251000000" (frequency only)
        /// </summary>
        public (string? frequencyId, string? pilotId) ParseSeriesKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return (null, null);
            
            // Check for new format: "F251.0-P1"
            if (key.StartsWith("F") && key.Contains("-P"))
            {
                var parts = key.Split('-');
                if (parts.Length >= 2)
                {
                    // Extract frequency: "F251.0" -> "251.0"
                    var freqStr = parts[0].Substring(1); // Remove "F" prefix
                    
                    // Extract pilot: "P1" -> "P1" (keep the P prefix for consistency)
                    var pilotStr = parts.Length > 1 ? parts[1] : null;
                    
                    // Convert MHz to Hz for internal storage
                    if (double.TryParse(freqStr, out var freqMHz))
                    {
                        var freqHz = freqMHz * 1_000_000.0;
                        return ($"{freqHz:F0}", pilotStr);
                    }
                }
            }
            
            // Legacy format: "251000000-PILOT123" or "251000000"
            var legacyParts = key.Split('-');
            if (legacyParts.Length == 0)
                return (null, null);
            
            if (legacyParts.Length == 1)
                return (legacyParts[0], null);
            
            return (legacyParts[0], legacyParts[1]);
        }

        /// <summary>
        /// Get frequency key from frequency value in Hz.
        /// </summary>
        private string GetFrequencyKey(double frequency)
        {
            return $"{frequency:F0}";
        }

        /// <summary>
        /// Rebuild the visible series collection based on current visibility state.
        /// Phase 7: Uses IsVisible property instead of removing series from collection.
        /// This allows LiveCharts2 to animate transitions and maintains series references.
        /// </summary>
        private void RebuildVisibleSeries()
        {
            // Phase 7: Set IsVisible on existing series instead of clearing/rebuilding
            // This is better for LiveCharts2 animation and state management
            
            int visibleCount = 0;
            bool anyChanges = false;
            
            foreach (var series in Series)
            {
                var previousVisibility = series.IsVisible;
                
                // Try to find matching key in _seriesVisibility
                // Support multiple key formats: "251-SHARK-1-1", "pilot:251.0:SHARK-1-1", etc.
                var seriesKey = _seriesVisibility.Keys.FirstOrDefault(k => 
                    series.Name.Contains(k) || k.Contains(series.Name) || 
                    AreKeysMatching(series.Name, k));
                
                if (seriesKey != null && _seriesVisibility.TryGetValue(seriesKey, out var shouldBeVisible))
                {
                    // Found explicit visibility state
                    series.IsVisible = shouldBeVisible;
                    if (shouldBeVisible)
                        visibleCount++;
                    
                    if (previousVisibility != shouldBeVisible)
                        anyChanges = true;
                }
                else if (_seriesVisibility.TryGetValue(series.Name, out var visible))
                {
                    // Found exact match by series name
                    series.IsVisible = visible;
                    if (visible)
                        visibleCount++;
                    
                    if (previousVisibility != visible)
                        anyChanges = true;
                }
                else
                {
                    // No explicit state found - check if ANY key in _seriesVisibility matches this series
                    // If _seriesVisibility is populated but no match found, series should be hidden
                    // If _seriesVisibility is empty (initial load), series should be visible by default
                    
                    if (_seriesVisibility.Count > 0)
                    {
                        // Visibility state exists but no match - this series should be hidden
                        series.IsVisible = false;
                        _logger.Debug($"No visibility state found for series '{series.Name}', hiding it");
                        
                        if (previousVisibility != false)
                            anyChanges = true;
                    }
                    else
                    {
                        // No visibility state at all - keep initial visibility
                        if (series.IsVisible)
                            visibleCount++;
                    }
                }
            }
            
            VisibleSeriesCount = visibleCount;
            _logger.Debug($"Updated series visibility: {VisibleSeriesCount} visible / {Series.Count} total (changes: {anyChanges})");
            
            if (anyChanges)
            {
                // CRITICAL FIX: Force LiveCharts2 to recognize visibility changes
                // Multiple strategies to ensure chart redraws:
                
                // 1. Notify that Series collection changed
                OnPropertyChanged(nameof(Series));
                
                // 2. Create a new array reference to force collection change detection
                var seriesArray = Series.ToArray();
                Series.Clear();
                foreach (var s in seriesArray)
                {
                    Series.Add(s);
                }
                
                _logger.Debug("Forced Series collection refresh to update chart");
            }
        }
        
        /// <summary>
        /// Helper method to check if two keys match despite different formats.
        /// Handles formats like "251-SHARK-1-1" vs "pilot:251.0:SHARK-1-1"
        /// </summary>
        private bool AreKeysMatching(string key1, string key2)
        {
            // Extract frequency and pilot parts manually to avoid over-splitting
            // Formats: "251-SHARK-1-1", "pilot:251.0:SHARK-1-1", "freq:251.0-SHARK-1-1"
            
            var (freq1, pilot1) = ExtractKeyComponents(key1);
            var (freq2, pilot2) = ExtractKeyComponents(key2);
            
            // Keys match if both frequency and pilot match
            bool freqMatch = false;
            if (freq1 != null && freq2 != null)
            {
                if (double.TryParse(freq1, out var f1) && double.TryParse(freq2, out var f2))
                {
                    freqMatch = Math.Abs(f1 - f2) < 0.1;
                }
                else
                {
                    freqMatch = freq1.Equals(freq2, StringComparison.OrdinalIgnoreCase);
                }
            }
            
            bool pilotMatch = pilot1 != null && pilot2 != null && 
                            pilot1.Equals(pilot2, StringComparison.OrdinalIgnoreCase);
            
            return freqMatch && pilotMatch;
        }
        
        /// <summary>
        /// Extract frequency and pilot components from a key.
        /// Handles formats: "251-SHARK-1-1", "pilot:251.0:SHARK-1-1", "freq:251.0-SHARK-1-1"
        /// </summary>
        private (string? frequency, string? pilot) ExtractKeyComponents(string key)
        {
            // Remove prefixes
            key = key.Replace("pilot:", "").Replace("freq:", "");
            
            // Split on first separator to get frequency and pilot
            var separators = new[] { ':', '-' };
            var firstSepIndex = key.IndexOfAny(separators);
            
            if (firstSepIndex < 0)
                return (key, null); // Just frequency, no pilot
            
            var frequency = key.Substring(0, firstSepIndex);
            var pilot = key.Substring(firstSepIndex + 1);
            
            return (frequency, pilot);
        }

        /// <summary>
        /// Phase 4: Toggle visibility of entire frequency (all pilots).
        /// </summary>
        public void SetFrequencyVisible(string frequencyId, bool visible)
        {
            _logger.Debug($"SetFrequencyVisible called: frequencyId={frequencyId}, visible={visible}");
            _logger.Debug($"Available keys in _frequencyPilots: {string.Join(", ", _frequencyPilots.Keys)}");
            
            // Phase 7: Search for matching frequency key (supports different formats)
            // First try exact match
            var actualFreqKey = _frequencyPilots.Keys.FirstOrDefault(k => 
                k.Equals(frequencyId, StringComparison.OrdinalIgnoreCase));
            
            // If no exact match, try numeric comparison
            // Handle format: "251000000" (Hz) vs "F251.0" (MHz with prefix)
            if (actualFreqKey == null && double.TryParse(frequencyId, out var targetFreqHz))
            {
                var targetFreqMHz = targetFreqHz / 1_000_000.0;
                _logger.Debug($"Trying numeric match: target={targetFreqHz} Hz ({targetFreqMHz} MHz)");
                
                actualFreqKey = _frequencyPilots.Keys.FirstOrDefault(k =>
                {
                    // Try to extract numeric value from key (handles "F251.0", "251.0", "251000000", etc.)
                    var keyNumeric = k.Replace("F", "").Replace("f", "").Trim();
                    if (double.TryParse(keyNumeric, out var keyValue))
                    {
                        // Check if key is in MHz (< 1000) or Hz (> 1000)
                        var keyMHz = keyValue < 1000 ? keyValue : keyValue / 1_000_000.0;
                        var match = Math.Abs(keyMHz - targetFreqMHz) < 0.01; // 0.01 MHz tolerance
                        if (match)
                        {
                            _logger.Debug($"Matched: key={k} ({keyMHz} MHz) ? target ({targetFreqMHz} MHz)");
                        }
                        return match;
                    }
                    return false;
                });
            }
            
            // If still no match, try partial string matching (fallback)
            if (actualFreqKey == null)
            {
                actualFreqKey = _frequencyPilots.Keys.FirstOrDefault(k => 
                    k.Contains(frequencyId, StringComparison.OrdinalIgnoreCase));
            }
            
            if (actualFreqKey == null)
            {
                _logger.Warn($"Unknown frequency: {frequencyId}, available keys: {string.Join(", ", _frequencyPilots.Keys)}");
                return;
            }
            
            _logger.Debug($"Found matching key: {actualFreqKey}");

            // Phase 7: Prevent event loops during bidirectional sync
            if (_isSyncingVisibility)
                return;

            _isSyncingVisibility = true;
            try
            {
                // Update visibility for all pilots in this frequency
                var pilots = _frequencyPilots[actualFreqKey];
                _logger.Debug($"Updating visibility for {pilots.Count} pilots on frequency {actualFreqKey}");
                
                foreach (var pilotId in pilots)
                {
                    // Search for matching series key in multiple places
                    var matchingKey = FindSeriesKey(actualFreqKey, pilotId);
                    
                    // Ensure key exists in _seriesVisibility
                    if (!_seriesVisibility.ContainsKey(matchingKey))
                    {
                        _seriesVisibility[matchingKey] = true; // Assume visible initially
                    }
                    
                    _seriesVisibility[matchingKey] = visible;
                    _logger.Debug($"Set visibility: {matchingKey} = {visible}");
                    
                    // CRITICAL FIX: Also update IsVisible on the actual series object
                    var series = Series.FirstOrDefault(s => 
                        s.Name == matchingKey || 
                        s.Name.Contains(actualFreqKey) && s.Name.Contains(pilotId));
                    
                    if (series != null)
                    {
                        series.IsVisible = visible;
                        _logger.Debug($"Updated series.IsVisible: {series.Name} = {visible}");
                    }
                }

                RebuildVisibleSeries();

                // Phase 7: Sync to audio mixer
                SyncVisibilityToMixer(actualFreqKey, visible);
            }
            finally
            {
                _isSyncingVisibility = false;
            }
        }

        /// <summary>
        /// Helper method to find the actual series key in various dictionaries and collections.
        /// Supports multiple key formats.
        /// </summary>
        private string FindSeriesKey(string frequencyKey, string pilotId)
        {
            // First, check if there's a key in _seriesVisibility
            var existingKey = _seriesVisibility.Keys.FirstOrDefault(k =>
                k.Contains(frequencyKey) && k.Contains(pilotId));
            if (existingKey != null)
                return existingKey;
            
            // Check _allSeries
            existingKey = _allSeries.Keys.FirstOrDefault(k =>
                k.Contains(frequencyKey) && k.Contains(pilotId));
            if (existingKey != null)
                return existingKey;
            
            // Check Series collection
            var series = Series.FirstOrDefault(s =>
                s.Name.Contains(frequencyKey) && s.Name.Contains(pilotId));
            if (series != null)
                return series.Name;
            
            // Default format
            return $"{frequencyKey}-{pilotId}";
        }

        /// <summary>
        /// Phase 4: Toggle visibility of individual pilot within a frequency.
        /// </summary>
        public void SetPilotVisible(string frequencyId, string pilotId, bool visible)
        {
            // Phase 7: Search for matching series key (supports different formats)
            var matchingKey = FindSeriesKey(frequencyId, pilotId);
            
            // Ensure key exists in _seriesVisibility
            if (!_seriesVisibility.ContainsKey(matchingKey))
            {
                _seriesVisibility[matchingKey] = true; // Assume visible initially
                _logger.Debug($"Creating visibility entry for key: {matchingKey}");
            }

            // Phase 7: Prevent event loops
            if (_isSyncingVisibility)
                return;

            _isSyncingVisibility = true;
            try
            {
                _seriesVisibility[matchingKey] = visible;
                
                // CRITICAL FIX: Also update IsVisible on the actual series object
                var series = Series.FirstOrDefault(s => 
                    s.Name == matchingKey || 
                    s.Name.Contains(frequencyId) && s.Name.Contains(pilotId));
                
                if (series != null)
                {
                    series.IsVisible = visible;
                    _logger.Debug($"Updated series.IsVisible: {series.Name} = {visible}");
                }
                
                RebuildVisibleSeries();

                // Phase 7: Sync to audio mixer
                SyncPilotVisibilityToMixer(frequencyId, pilotId, visible);
                
                _logger.Debug($"Set pilot visibility: {frequencyId}/{pilotId} = {visible}");
            }
            finally
            {
                _isSyncingVisibility = false;
            }
        }

        /// <summary>
        /// Phase 4: Expand/collapse frequency to show all/limited pilots.
        /// </summary>
        public void ExpandFrequency(string frequencyId, bool expanded)
        {
            if (!_frequencyPilots.ContainsKey(frequencyId))
                return;

            _frequencyExpanded[frequencyId] = expanded;

            var pilots = _frequencyPilots[frequencyId].ToList();
            var visibleCount = expanded ? pilots.Count : Math.Min(pilots.Count, MaxPilotsPerFrequency);

            // Show top N pilots or all if expanded
            for (int i = 0; i < pilots.Count; i++)
            {
                var key = $"{frequencyId}-{pilots[i]}";
                if (_seriesVisibility.ContainsKey(key))
                {
                    _seriesVisibility[key] = i < visibleCount;
                }
            }

            RebuildVisibleSeries();
        }

        /// <summary>
        /// Phase 4: Update marker density based on zoom level.
        /// </summary>
        private void UpdateMarkerDensity()
        {
            // Adjust marker visibility based on zoom
            var showMarkers = ZoomLevel > 2.0; // Show markers when zoomed in

            foreach (var series in Series)
            {
                if (series is LineSeries<ObservablePoint> lineSeries)
                {
                    lineSeries.GeometrySize = showMarkers ? 6 : 0;
                }
            }
        }

        /// <summary>
        /// Phase 5: Set viewport to specific range (for zoom/pan).
        /// Clamps to data range to prevent out-of-bounds errors.
        /// If no data was explicitly loaded, initializes/expands data range to accommodate viewport.
        /// </summary>
        public void SetViewport(DateTime start, DateTime end)
        {
            if (start >= end)
            {
                _logger.Warn($"Invalid viewport range: start={start}, end={end}");
                return;
            }

            // If data range is not initialized (no LoadDataAsync called), expand it to accommodate viewport
            bool needsInitialization = Start == default || End == default || Start >= End;
            
            if (needsInitialization)
            {
                // Initialize with a larger range than viewport to allow panning
                // Use 3x viewport duration on each side to provide room for pan operations
                var viewportDuration = end - start;
                var buffer = TimeSpan.FromTicks(viewportDuration.Ticks * 3);
                
                Start = start - buffer;
                End = end + buffer;
                _logger.Debug($"Data range initialized with buffer: {Start:HH:mm:ss} to {End:HH:mm:ss} (viewport: {start:HH:mm:ss} to {end:HH:mm:ss})");
            }
            else if (!_isDataLoadedExplicitly)
            {
                // Data range exists but data wasn't explicitly loaded - allow expansion
                // This handles cases where SetViewport is called multiple times to build up range
                if (start < Start)
                {
                    Start = start;
                    _logger.Debug($"Data range start expanded to {start:HH:mm:ss}");
                }
                if (end > End)
                {
                    End = end;
                    _logger.Debug($"Data range end expanded to {end:HH:mm:ss}");
                }
            }
            // else: data was explicitly loaded, so clamp viewport to loaded range (don't expand)
            
            // Clamp to data range
            if (start < Start) start = Start;
            if (end > End) end = End;
            
            // Ensure we still have a valid range after clamping
            if (start >= end)
            {
                _logger.Warn($"Viewport collapsed after clamping: start={start}, end={end}, data range={Start} to {End}");
                return;
            }

            ViewportStart = start;
            ViewportEnd = end;
            
            _logger.Debug($"Viewport set: {start:HH:mm:ss} to {end:HH:mm:ss} (duration: {(end - start).TotalMinutes:F1} min)");
        }

        /// <summary>
        /// Phase 5: Zoom in by reducing viewport duration (0 < factor < 1).
        /// </summary>
        public void ZoomIn(double factor)
        {
            if (factor <= 0 || factor >= 1.0)
            {
                _logger.Warn($"Invalid zoom factor: {factor} (must be 0 < factor < 1)");
                return;
            }

            // Check if we have valid data range
            if (Start >= End)
            {
                _logger.Warn("Cannot zoom: no valid data range loaded");
                return;
            }

            var center = ViewportStart + ViewportDuration / 2;
            var newDuration = TimeSpan.FromTicks((long)(ViewportDuration.Ticks * factor));
            
            // Ensure minimum viewport duration (1 second)
            if (newDuration < TimeSpan.FromSeconds(1))
            {
                newDuration = TimeSpan.FromSeconds(1);
            }
            
            var newStart = center - newDuration / 2;
            var newEnd = center + newDuration / 2;

            // Clamp to data range
            if (newStart < Start) newStart = Start;
            if (newEnd > End) newEnd = End;
            
            // If clamping pushed us to one side, adjust to maintain center if possible
            if (newEnd > End)
            {
                newEnd = End;
                newStart = End - newDuration;
                if (newStart < Start) newStart = Start;
            }
            if (newStart < Start)
            {
                newStart = Start;
                newEnd = Start + newDuration;
                if (newEnd > End) newEnd = End;
            }

            SetViewport(newStart, newEnd);
        }

        /// <summary>
        /// Phase 5: Zoom out by increasing viewport duration (factor > 1).
        /// </summary>
        public void ZoomOut(double factor)
        {
            if (factor <= 1.0)
            {
                _logger.Warn($"Invalid zoom factor: {factor} (must be > 1)");
                return;
            }

            // Check if we have valid data range
            if (Start >= End)
            {
                _logger.Warn("Cannot zoom: no valid data range loaded");
                return;
            }

            var center = ViewportStart + ViewportDuration / 2;
            var newDuration = TimeSpan.FromTicks((long)(ViewportDuration.Ticks * factor));
            
            // Don't zoom out beyond full range
            if (newDuration >= (End - Start))
            {
                ResetViewport();
                return;
            }

            var newStart = center - newDuration / 2;
            var newEnd = center + newDuration / 2;

            // Clamp to data range
            if (newStart < Start) newStart = Start;
            if (newEnd > End) newEnd = End;
            
            // If clamping pushed us to one side, adjust to maintain center if possible
            if (newEnd > End)
            {
                newEnd = End;
                newStart = End - newDuration;
                if (newStart < Start) newStart = Start;
            }
            if (newStart < Start)
            {
                newStart = Start;
                newEnd = Start + newDuration;
                if (newEnd > End) newEnd = End;
            }

            SetViewport(newStart, newEnd);
        }

        /// <summary>
        /// Phase 5: Pan viewport by specified offset.
        /// </summary>
        public void Pan(TimeSpan offset)
        {
            // Check if we have valid data range
            if (Start >= End)
            {
                _logger.Warn("Cannot pan: no valid data range loaded");
                return;
            }

            var newStart = ViewportStart + offset;
            var newEnd = ViewportEnd + offset;
            
            var viewportDuration = ViewportDuration;

            // Clamp to data range
            if (newStart < Start)
            {
                newStart = Start;
                newEnd = Start + viewportDuration;
                // Ensure newEnd doesn't exceed End
                if (newEnd > End) newEnd = End;
            }
            if (newEnd > End)
            {
                newEnd = End;
                newStart = End - viewportDuration;
                // Ensure newStart doesn't go below Start
                if (newStart < Start) newStart = Start;
            }

            SetViewport(newStart, newEnd);
        }

        /// <summary>
        /// Phase 5: Reset viewport to show full data range.
        /// </summary>
        public void ResetViewport()
        {
            SetViewport(Start, End);
        }

        /// <summary>
        /// Phase 6: Update viewport to keep playhead centered (follow mode).
        /// Called automatically when playhead moves and follow mode is active.
        /// </summary>
        private void UpdateViewportForPlayhead()
        {
            try
            {
                // Calculate center of current viewport
                var viewportCenter = ViewportStart + ViewportDuration / 2;
                var distanceFromCenter = (PlayheadTime - viewportCenter).Duration();

                // Pan if playhead is more than 40% away from center
                // This prevents jitter while allowing the playhead to move within a reasonable range
                if (distanceFromCenter > ViewportDuration * 0.4)
                {
                    // Calculate new viewport start to center the playhead
                    var halfDuration = ViewportDuration / 2;
                    DateTime newViewportStart;
                    DateTime newViewportEnd;

                    try
                    {
                        newViewportStart = PlayheadTime - halfDuration;
                        newViewportEnd = PlayheadTime + halfDuration;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // DateTime arithmetic overflow - clamp to data range
                        newViewportStart = Start;
                        newViewportEnd = Start + ViewportDuration;
                        _logger.Warn("Follow mode: DateTime overflow, clamping to start");
                    }

                    // Ensure we don't go beyond data range
                    if (newViewportStart < Start)
                    {
                        newViewportStart = Start;
                        try
                        {
                            newViewportEnd = newViewportStart + ViewportDuration;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            newViewportEnd = End;
                        }
                    }

                    if (newViewportEnd > End)
                    {
                        newViewportEnd = End;
                        try
                        {
                            newViewportStart = newViewportEnd - ViewportDuration;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            newViewportStart = Start;
                        }
                    }

                    SetViewport(newViewportStart, newViewportEnd);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating viewport for playhead");
            }
        }

        /// <summary>
        /// Phase 7: Set visibility for frequency using frequency value (in Hz).
        /// Searches for matching frequency key in dictionaries to support different key formats.
        /// </summary>
        public void SetFrequencyVisibility(double frequency, bool visible)
        {
            // Try to find matching frequency key in dictionary
            // Tests may use different key formats (e.g., "freq:251.0" vs "251")
            var freqId = _frequencyPilots.Keys.FirstOrDefault(k => 
                k.Contains(frequency.ToString("F1")) || k.Contains(frequency.ToString("F0")));
            
            if (freqId == null)
            {
                // Fallback to default format
                freqId = $"{frequency:F0}";
            }
            
            SetFrequencyVisible(freqId, visible);
        }

        /// <summary>
        /// Phase 7: Set visibility for specific pilot using frequency value (in Hz).
        /// Searches for matching pilot key in dictionaries to support different key formats.
        /// </summary>
        public void SetPilotVisibility(double frequency, string pilotId, bool visible)
        {
            // Find matching keys that contain both the frequency and pilot ID
            var matchingKey = _seriesVisibility.Keys.FirstOrDefault(k =>
                (k.Contains(frequency.ToString("F1")) || k.Contains(frequency.ToString("F0"))) &&
                k.Contains(pilotId));
            
            if (matchingKey != null)
            {
                // Use the actual key format from the dictionary
                var parts = matchingKey.Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var freqKey = parts.Length > 1 ? parts[1] : matchingKey;
                
                // For tests with "pilot:251.0:SHARK-1-1" format
                if (matchingKey.StartsWith("pilot:"))
                {
                    SetPilotVisible(matchingKey, pilotId, visible);
                    return;
                }
            }
            
            // Fallback to default format
            var freqId = $"{frequency:F0}";
            SetPilotVisible(freqId, pilotId, visible);
        }

        /// <summary>
        /// Phase 7: Get visibility state for frequency using frequency value (in Hz).
        /// </summary>
        public bool GetFrequencyVisibility(double frequency)
        {
            // Try to find matching frequency key
            var freqId = _frequencyPilots.Keys.FirstOrDefault(k => 
                k.Contains(frequency.ToString("F1")) || k.Contains(frequency.ToString("F0")));
            
            if (freqId == null)
            {
                freqId = $"{frequency:F0}";
            }
            
            if (!_frequencyPilots.ContainsKey(freqId))
                return false;

            // Return true if any pilot in this frequency is visible
            // If no explicit visibility state exists, default to true (visible)
            return _frequencyPilots[freqId].Any(pilotId =>
            {
                // Search for matching series key using frequency number (not full key with prefix)
                var freqNumber = frequency.ToString("F1");
                var seriesKey = _seriesVisibility.Keys.FirstOrDefault(k =>
                    k.Contains(freqNumber) && k.Contains(pilotId));
                
                if (seriesKey != null)
                {
                    return _seriesVisibility.TryGetValue(seriesKey, out var visible) && visible;
                }
                
                // Check if series exists in collection and is visible
                var series = Series.FirstOrDefault(s => s.Name != null &&
                    s.Name.Contains(freqNumber) && 
                    s.Name.Contains(pilotId));
                
                if (series != null)
                {
                    return series.IsVisible;
                }
                
                // Default to visible if no explicit state exists
                return true;
            });
        }

        /// <summary>
        /// Phase 7: Get visibility state for specific pilot using frequency value (in Hz).
        /// </summary>
        public bool GetPilotVisibility(double frequency, string pilotId)
        {
            // Search for matching series key
            var matchingKey = _seriesVisibility.Keys.FirstOrDefault(k =>
                (k.Contains(frequency.ToString("F1")) || k.Contains(frequency.ToString("F0"))) &&
                k.Contains(pilotId));
            
            if (matchingKey != null)
            {
                return _seriesVisibility.TryGetValue(matchingKey, out var visible) && visible;
            }
            
            // Check if series exists in collection and use its IsVisible property
            var series = Series.FirstOrDefault(s => s.Name != null &&
                (s.Name.Contains(frequency.ToString("F1")) || s.Name.Contains(frequency.ToString("F0"))) &&
                s.Name.Contains(pilotId));
            
            if (series != null)
            {
                return series.IsVisible;
            }
            
            // Default to visible if no explicit state exists
            return true;
        }
        
        /// <summary>
        /// Phase 7: Sync chart visibility changes to audio mixer.
        /// </summary>
        private void SyncVisibilityToMixer(string frequencyId, bool visible)
        {
            if (_mixerController == null || !_audioSyncEnabled)
                return;

            try
            {
                // Extract numeric frequency from key (supports multiple formats)
                double freq = ExtractFrequencyFromKey(frequencyId);
                if (freq > 0)
                {
                    // Use SetChannelMuted (inverted logic: visible=true means muted=false)
                    _mixerController.SetChannelMuted(freq, !visible);
                    _logger.Debug($"Synced frequency {freq:F0} Hz visibility to mixer: {visible}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to sync frequency {frequencyId} to mixer");
            }
        }

        /// <summary>
        /// Extract numeric frequency value from a key in any format.
        /// Handles: "251", "251.0", "freq:251.0", "251000000", etc.
        /// </summary>
        private double ExtractFrequencyFromKey(string key)
        {
            // Remove common prefixes
            key = key.Replace("freq:", "").Replace("pilot:", "");
            
            // Split by separators and find the numeric part
            var parts = key.Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                if (double.TryParse(part, out var freq) && freq > 0)
                {
                    return freq;
                }
            }
            
            return 0;
        }

        /// <summary>
        /// Phase 7: Sync pilot visibility to mixer.
        /// Note: Current MixerController doesn't support per-pilot muting, only per-frequency.
        /// This is a placeholder for future enhancement.
        /// </summary>
        private void SyncPilotVisibilityToMixer(string frequencyId, string pilotId, bool visible)
        {
            if (_mixerController == null || !_audioSyncEnabled)
                return;

            try
            {
                // TODO: Phase 7 Enhancement - Add per-pilot muting support to MixerController
                // For now, we only have per-frequency control
                _logger.Debug($"Pilot visibility sync not yet supported: {pilotId} on {frequencyId} Hz = {visible}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to sync pilot {pilotId} to mixer");
            }
        }

        /// <summary>
        /// Phase 7: Handle mixer channel changes (bidirectional sync).
        /// </summary>
        private void OnMixerChannelChanged(object? sender, EventArgs e)
        {
            _logger.Debug($"OnMixerChannelChanged called: _isSyncingVisibility={_isSyncingVisibility}, _audioSyncEnabled={_audioSyncEnabled}");
            
            if (_isSyncingVisibility || !_audioSyncEnabled)
            {
                _logger.Debug("Skipping sync due to flag check");
                return;
            }

            if (e is not ChannelChangedEventArgs args)
            {
                _logger.Debug("Event args is not ChannelChangedEventArgs");
                return;
            }

            _logger.Debug($"Mixer channel changed: Freq={args.Frequency}, Property={args.Property}, Value={args.Value}");

            // Only sync mute property changes
            if (args.Property != ChannelProperty.Muted)
            {
                _logger.Debug($"Skipping non-mute property: {args.Property}");
                return;
            }

            if (args.Value is not bool isMuted)
            {
                _logger.Debug("Value is not bool");
                return;
            }

            _logger.Debug($"Syncing mixer mute to chart: Freq={args.Frequency}, Muted={isMuted}, WillSetVisible={!isMuted}");

            // Sync from mixer to chart: hide frequency when muted, show when unmuted
            // Note: We don't set _isSyncingVisibility here because we're the SOURCE of the change
            // The SetFrequencyVisibility method will set the flag to prevent looping back to mixer
            SetFrequencyVisibility(args.Frequency, !isMuted); // Visible when NOT muted
            _logger.Debug($"Synced mixer mute to chart: Freq={args.Frequency}, Visible={!isMuted}");
        }

        /// <summary>
        /// Phase 9 Step 3: Start performance monitoring timer.
        /// Updates stats at 1 Hz (once per second) to avoid UI overhead.
        /// </summary>
        private void StartPerformanceMonitoring()
        {
            if (_performanceUpdateTimer != null)
                return; // Already started

            _performanceUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.0) // Update once per second
            };

            _performanceUpdateTimer.Tick += OnPerformanceUpdateTick;
            _performanceUpdateTimer.Start();

            _logger.Debug("Performance monitoring started (1 Hz)");

            // Immediate update
            UpdatePerformanceStats();
        }

        /// <summary>
        /// Phase 9 Step 3: Stop performance monitoring timer.
        /// </summary>
        private void StopPerformanceMonitoring()
        {
            if (_performanceUpdateTimer != null)
            {
                _performanceUpdateTimer.Stop();
                _performanceUpdateTimer.Tick -= OnPerformanceUpdateTick;
                _performanceUpdateTimer = null;
                _logger.Debug("Performance monitoring stopped");
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Timer tick handler for performance updates.
        /// </summary>
        private void OnPerformanceUpdateTick(object? sender, EventArgs e)
        {
            UpdatePerformanceStats();
            UpdateFPS();
        }

        /// <summary>
        /// Phase 9 Step 3: Update performance statistics from tile manager.
        /// </summary>
        private void UpdatePerformanceStats()
        {
            if (!ShowPerformanceStats || _tileManager == null)
                return;

            try
            {
                var stats = _tileManager.GetStats();
                
                MemoryUsageMB = stats.TotalMemoryMB;
                CacheHitRate = stats.CacheHitRate;
                LoadedTileCount = stats.LoadedTileCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating performance stats");
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Update FPS counter.
        /// Calculates frames per second based on frame count since last update.
        /// </summary>
        private void UpdateFPS()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                CurrentFPS = _frameCount / elapsed;
                _frameCount = 0;
                _lastFpsUpdate = now;
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Increment frame counter.
        /// Call this from render loop or viewport update to track FPS.
        /// </summary>
        public void IncrementFrameCount()
        {
            _frameCount++;
        }

        /// <summary>
        /// Phase 12: Reconnects the ViewModel to a new data source (e.g., when switching from synthetic to real recording data).
        /// This allows switching from test data to actual recording data after a file is loaded.
        /// </summary>
        public void SetDataSource(IAmplitudeSeriesProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _logger.Info("Phase 12: Reconnecting to new data source (RAW DATA MODE - tiles disabled)");

            try
            {
                // Replace the amplitude provider
                _amplitudeProvider = provider;

                // CRITICAL: Do NOT create tile manager - it causes infinite loops
                // The tile system needs pre-generated tiles from a database, not on-demand generation
                // For now, use raw data loading which is fast enough for 25-minute recordings
                _tileCache = null;
                _tileManager = null;
                
                // Force tile-based loading to remain disabled
                _isTileBasedLoadingEnabled = false;

                _logger.Info("Phase 12: Data source reconnected successfully (RAW DATA MODE)");
                _logger.Info("Phase 12: Will use LoadRawDataAsync() - fast enough for typical recordings");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Phase 12: Failed to reconnect data source");
                throw;
            }
        }

        /// <summary>
        /// Dispose pattern for cleanup.
        /// Unsubscribes from mixer events and cancels any pending operations.
        /// Phase 9 Step 3: Also stops performance monitoring timer.
        /// </summary>
        public void Dispose()
        {
            _logger.Debug("UnifiedGraphViewModel disposing");

            // Phase 9 Step 3: Stop performance monitoring
            StopPerformanceMonitoring();

            // Cancel any pending load operations
            _currentLoadCancellation?.Cancel();
            _currentLoadCancellation?.Dispose();

            // Unsubscribe from mixer events
            if (_mixerController != null)
            {
                _mixerController.ChannelChanged -= OnMixerChannelChanged;
            }

            _logger.Debug("UnifiedGraphViewModel disposed");
        }
    }
}
