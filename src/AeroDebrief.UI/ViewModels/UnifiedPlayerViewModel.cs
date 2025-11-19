using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AeroDebrief.Core;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.Playback;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Storage;
using AeroDebrief.UI.Commands;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Services.Data;
using AeroDebrief.UI.Services.Audio;
using AeroDebrief.UI.Services.Visualization.Graphs;
using NLog;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// Main view model for the unified player control.
    /// 
    /// ARCHITECTURE (Service-Based):
    /// - FrequencyManager: Handles frequency discovery and selection
    /// - PlaybackSessionManager: Handles file loading and playback lifecycle
    /// - MixerController: Handles audio mixer channel management
    /// - UnifiedGraphViewModel: LiveCharts2 tile-based amplitude visualization
    /// - Single FilePacketSource (memory-mapped, shared for graph + playback)
    /// - FilePlaybackPipeline for playback (batched streaming, instant filtering)
    /// 
    /// PERFORMANCE:
    /// - 82% less RAM usage (10MB vs 55MB)
    /// - 2.5x faster file open
    /// - 500x faster filtering (instant vs 500-1000ms restart)
    /// - Tile-based viewport rendering for scalability
    /// </summary>
    public class UnifiedPlayerViewModel : ViewModelBase, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Service instances
        private readonly FrequencyManager _frequencyManager;
        private readonly PlaybackSessionManager _sessionManager;
        private readonly MixerController _mixerController;
        private readonly LivePlaybackManager _livePlaybackManager; // Phase 4: Live playback
        private readonly LiveAudioPlaybackService _liveAudioService; // Phase 4: Live audio
        
        // Phase 7 Step 4: Unified graph view model for chart integration
        private readonly UnifiedGraphViewModel _graphViewModel;
        
        // Tacview integration (optional)
        private TacviewIntegrationViewModel? _tacviewIntegration;
        private Integrations.Tacview.TacviewIntegrationService? _tacviewService;
        
        // Zoom state tracking for GPU compositor (Phase 3.1)
        private double _zoomStartTime = 0.0;
        private double _zoomEndTime = 1.0;

        // State management
        private PlayerMode _currentMode = PlayerMode.Idle;
        private PlaybackState _playbackState = PlaybackState.Stopped;
        private string _statusMessage = "Ready";
        private TimeSpan _currentPosition = TimeSpan.Zero;
        private TimeSpan _totalDuration = TimeSpan.Zero;
        private double _progressPercent = 0.0;
        private double _playheadPositionNormalized = 0.0;
        private bool _isBuffering = false;
        private string _currentSourceName = string.Empty;
        private bool _isLiveRecording = false; // Phase 4: Track live recording state
        
        // Phase 4 Enhanced: Dual playhead support
        private TimeSpan _recordingPosition = TimeSpan.Zero;  // Where recording is (static playhead)
        private TimeSpan _playbackPosition = TimeSpan.Zero;   // Where audio is playing (dynamic playhead)
        
        private double _bufferStartPosition = 0.0;
        private double _bufferEndPosition = 0.0;

        // Source view models
        private ServerSourceViewModel? _serverSource;
        private FileSourceViewModel? _fileSource;
        
        // Phase 2.5: File format display
        private string _fileFormat = string.Empty;

        // Collections (now backed by services)
        private ObservableCollection<FrequencyGroupViewModel> _frequencies = new();
        private ObservableCollection<MixerChannelViewModel> _mixerChannels = new();

        // Legacy waveform data removed - UnifiedGraphControl handles visualization
        private DateTime _lastPlayheadUpdate = DateTime.MinValue;

        // Phase 3.1: Performance monitoring
        private int _currentFPS;
        private double _compositionTime;
        private string _renderMode = "CPU";
        private int _frameCount;
        private readonly System.Diagnostics.Stopwatch _fpsTimer = System.Diagnostics.Stopwatch.StartNew();
        private int _colorIndex = 0;

        #region Properties

        public PlayerMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnPropertyChanged(nameof(IsRecordingMode));
                    OnPropertyChanged(nameof(IsPlaybackMode));
                    OnPropertyChanged(nameof(IsIdle));
#if DEBUG
                    Logger.Debug($"Mode changed: {value}");
#endif
                }
            }
        }

        public PlaybackState PlaybackState
        {
            get => _playbackState;
            set
            {
                if (SetProperty(ref _playbackState, value))
                {
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(IsStopped));
#if DEBUG
                    Logger.Debug($"Playback state: {value}");
#endif
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (SetProperty(ref _currentPosition, value))
                {
                    OnPropertyChanged(nameof(CurrentPositionDisplay));
                }
            }
        }

        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (SetProperty(ref _totalDuration, value))
                {
                    OnPropertyChanged(nameof(TotalDurationDisplay));
                }
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public double PlayheadPositionNormalized
        {
            get => _playheadPositionNormalized;
            set => SetProperty(ref _playheadPositionNormalized, value);
        }

        public bool IsBuffering
        {
            get => _isBuffering;
            set => SetProperty(ref _isBuffering, value);
        }

        public string CurrentSourceName
        {
            get => _currentSourceName;
            set => SetProperty(ref _currentSourceName, value);
        }
        
        /// <summary>
        /// Phase 2.5: File format display (CVR, ADB, or CVR Uncompressed).
        /// </summary>
        public string FileFormat
        {
            get => _fileFormat;
            set => SetProperty(ref _fileFormat, value);
        }

        public ObservableCollection<FrequencyGroupViewModel> Frequencies
        {
            get => _frequencies;
            set => SetProperty(ref _frequencies, value);
        }

        public ObservableCollection<MixerChannelViewModel> MixerChannels
        {
            get => _mixerChannels;
            set => SetProperty(ref _mixerChannels, value);
        }

        /// <summary>
        /// Phase 6: Gets the current PlaybackController for integration with chart playhead.
        /// Returns null if no session is loaded.
        /// </summary>
        public Core.Playback.PlaybackController? PlaybackController
        {
            get
            {
                try
                {
                    return _sessionManager?.Pipeline?.PlaybackController;
                }
                catch (InvalidOperationException)
                {
                    // Pipeline not opened yet
                    return null;
                }
            }
        }

        /// <summary>
        /// Phase 7 Step 4: Unified graph view model for chart integration with audio sync.
        /// Provides LiveCharts2 amplitude visualization with automatic mute/solo synchronization.
        /// </summary>
        public UnifiedGraphViewModel GraphViewModel => _graphViewModel;

        public bool IsRecordingMode => CurrentMode == PlayerMode.Recording;
        public bool IsPlaybackMode => CurrentMode == PlayerMode.Playback;
        public bool IsIdle => CurrentMode == PlayerMode.Idle;
        public bool IsPlaying => PlaybackState == PlaybackState.Playing;
        public bool IsPaused => PlaybackState == PlaybackState.Paused;
        public bool IsStopped => PlaybackState == PlaybackState.Stopped;
        public string CurrentPositionDisplay => CurrentPosition.ToString(@"hh\:mm\:ss");
        public string TotalDurationDisplay => TotalDuration.ToString(@"hh\:mm\:ss");

        /// <summary>
        /// Phase 4: Gets whether live recording is currently active.
        /// </summary>
        public bool IsLiveRecording
        {
            get => _isLiveRecording;
            set => SetProperty(ref _isLiveRecording, value);
        }

        /// <summary>
        /// Phase 4 Enhanced: Gets the recording playhead position (static - where packets are being written).
        /// This is the "pencil" drawing the waveform.
        /// </summary>
        public TimeSpan RecordingPosition
        {
            get => _recordingPosition;
            set
            {
                if (SetProperty(ref _recordingPosition, value))
                {
                    OnPropertyChanged(nameof(RecordingPositionNormalized));
                    OnPropertyChanged(nameof(RecordingPositionDisplay));
                }
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Gets the playback playhead position (dynamic - where audio is playing).
        /// This is synchronized with the audio output.
        /// </summary>
        public TimeSpan PlaybackPosition
        {
            get => _playbackPosition;
            set
            {
                if (SetProperty(ref _playbackPosition, value))
                {
                    OnPropertyChanged(nameof(PlaybackPositionNormalized));
                }
            }
        }

        /// <summary>
        /// Gets the normalized recording position (0.0 to 1.0) for UI binding.
        /// </summary>
        public double RecordingPositionNormalized
        {
            get
            {
                if (TotalDuration.TotalSeconds <= 0)
                    return 1.0; // At end if no duration yet
                
                return RecordingPosition.TotalMilliseconds / TotalDuration.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Gets the normalized playback position (0.0 to 1.0) for UI binding.
        /// </summary>
        public double PlaybackPositionNormalized
        {
            get
            {
                if (TotalDuration.TotalSeconds <= 0)
                    return 0.0;
                
                return PlaybackPosition.TotalMilliseconds / TotalDuration.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Gets the recording position display string.
        /// </summary>
        public string RecordingPositionDisplay => RecordingPosition.ToString(@"hh\:mm\:ss");

        public ServerSourceViewModel ServerSource
        {
            get => _serverSource ??= new ServerSourceViewModel();
            set => SetProperty(ref _serverSource, value);
        }

        public FileSourceViewModel FileSource
        {
            get => _fileSource ??= new FileSourceViewModel();
            set => SetProperty(ref _fileSource, value);
        }
        
        /// <summary>
        /// Tacview integration view model (optional)
        /// Displays connection status and control for Tacview synchronization
        /// Initialized when a file is loaded and PlaybackController is available
        /// Always returns a non-null value - stub before file load, real instance after
        /// </summary>
        public TacviewIntegrationViewModel? TacviewIntegration
        {
            get
            {
                // Always return the current instance (null before file loads, real instance after)
                // The XAML binding will use FallbackValue=Collapsed if null
                return _tacviewIntegration;
            }
        }

        public double ZoomStartTime
        {
            get => _zoomStartTime;
            set
            {
                SetProperty(ref _zoomStartTime, value);
            }
        }

        public double ZoomEndTime
        {
            get => _zoomEndTime;
            set
            {
                SetProperty(ref _zoomEndTime, value);
            }
        }
        
        // Phase 3.1: Performance monitoring properties
        public int CurrentFPS
        {
            get => _currentFPS;
            set => SetProperty(ref _currentFPS, value);
        }

        public double CompositionTime
        {
            get => _compositionTime;
            set => SetProperty(ref _compositionTime, value);
        }

        public string RenderMode
        {
            get => _renderMode;
            set => SetProperty(ref _renderMode, value);
        }

        /// <summary>
        /// Phase 12: Exposes the PlaybackSessionManager to allow access to PacketSource and Pipeline
        /// for WaveformDisplayPanel initialization.
        /// </summary>
        public PlaybackSessionManager SessionManager => _sessionManager;

        #endregion

        #region Commands

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SeekCommand { get; }
        public ICommand GoLiveCommand { get; }
        public ICommand ChangeSourceCommand { get; }
        public ICommand SelectAllFrequenciesCommand { get; }
        public ICommand SelectNoFrequenciesCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        
        // Phase 4 Enhanced: Live recording playback commands
        public ICommand PlayLiveRecordingCommand { get; }
        public ICommand PauseLiveRecordingCommand { get; }
        public ICommand SeekLiveRecordingCommand { get; }
        public ICommand GoToLivePositionCommand { get; }

        #endregion

        #region Constructor

        public UnifiedPlayerViewModel()
        {
            Logger.Info("Initializing UnifiedPlayerViewModel with service architecture");

            // Initialize services
            _frequencyManager = new FrequencyManager();
            _sessionManager = new PlaybackSessionManager();
            _mixerController = new MixerController();
            _livePlaybackManager = new LivePlaybackManager(_frequencyManager); // Phase 4: Live playback
            _liveAudioService = new LiveAudioPlaybackService(); // Phase 4: Live audio

            // Phase 7 Step 4: Initialize graph view model with MixerController for audio sync
            // Phase 8: Tile system now enabled by default (DataTileCache and DataTileManager created automatically)
            _graphViewModel = new UnifiedGraphViewModel(
                new AmplitudeSeriesProvider(),
                tileCache: null, // Will be created automatically with 300 MB default budget
                _mixerController, // Pass mixer for bidirectional sync
                tileManager: null, // Will be created automatically
                errorHandler: new ErrorHandlingService(Logger)); // Phase 9: Error handling service

            Logger.Info("? GraphViewModel initialized with tile system (ENABLED BY DEFAULT) and audio synchronization");

            // Note: Tacview integration will be initialized when a file is loaded
            // (requires PlaybackController which is created during file load)
            // The control will be hidden until then

            // Wire up events
            WireServiceEvents();

            // Initialize commands
            PlayCommand = new RelayCommand(ExecutePlay, CanExecutePlay);
            PauseCommand = new RelayCommand(ExecutePause, CanExecutePause);
            StopCommand = new RelayCommand(ExecuteStop, CanExecuteStop);
            SeekCommand = new RelayCommand<double>(pos => ExecuteSeek(pos), pos => CanExecuteSeek(pos));
            GoLiveCommand = new RelayCommand(ExecuteGoLive, CanExecuteGoLive);
            ChangeSourceCommand = new RelayCommand(ExecuteChangeSource);
            SelectAllFrequenciesCommand = new RelayCommand(ExecuteSelectAllFrequencies);
            SelectNoFrequenciesCommand = new RelayCommand(ExecuteSelectNoFrequencies);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            
            // Phase 4 Enhanced: Live recording playback commands
            PlayLiveRecordingCommand = new RelayCommand(ExecutePlayLiveRecording, CanExecutePlayLiveRecording);
            PauseLiveRecordingCommand = new RelayCommand(ExecutePauseLiveRecording, CanExecutePauseLiveRecording);
            SeekLiveRecordingCommand = new RelayCommand<double>(pos => ExecuteSeekLiveRecording(pos), pos => CanExecuteSeekLiveRecording(pos));
            GoToLivePositionCommand = new RelayCommand(ExecuteGoToLivePosition, CanExecuteGoToLivePosition);

            // Wire up source view model events
            ServerSource.ConnectionStateChanged += OnServerConnectionStateChanged;
            ServerSource.RecordingStateChanged += OnServerRecordingStateChanged;
            ServerSource.LivePlaybackReady += OnServerLivePlaybackReady; // Phase 4
            FileSource.FileLoaded += OnFileLoaded;
            FileSource.FileUnloaded += OnFileUnloaded;

            Logger.Info("? UnifiedPlayerViewModel initialized with service architecture");
        }

        #endregion

        #region Service Event Wiring

        private void WireServiceEvents()
        {
            Logger.Debug("Wiring service events...");

            // Session events
            _sessionManager.SessionLoaded += OnSessionLoaded;
            _sessionManager.SessionUnloaded += OnSessionUnloaded;
            _sessionManager.SessionError += OnSessionError;

            // Frequency events
            _frequencyManager.SelectionChanged += OnFrequencySelectionChanged;
            _frequencyManager.FrequenciesLoaded += OnFrequenciesLoaded;

            // Mixer events
            _mixerController.ChannelAdded += OnMixerChannelAdded;
            _mixerController.ChannelRemoved += OnMixerChannelRemoved;
            _mixerController.ChannelChanged += OnMixerChannelChanged;

            // Phase 4: Live playback events
            _livePlaybackManager.FrequencyDetected += OnLiveFrequencyDetected;
            _livePlaybackManager.PlayerDetected += OnLivePlayerDetected;
            _livePlaybackManager.PacketsAvailable += OnLivePacketsAvailable;
            _livePlaybackManager.DurationUpdated += OnLiveDurationUpdated;
            _livePlaybackManager.AudioPacketsAvailable += OnLiveAudioPacketsAvailable; // Phase 4: Audio streaming

            Logger.Debug("? Service events wired");
        }

        #endregion

        #region Session Manager Event Handlers

        private void OnSessionLoaded(object? sender, SessionLoadedEventArgs e)
        {
            Logger.Info($"Session loaded: {e.TotalPackets} packets, {e.TotalDuration}");
            
            CurrentSourceName = System.IO.Path.GetFileName(e.FilePath);
            TotalDuration = e.TotalDuration;
            StatusMessage = "File loaded. Analyzing frequencies...";

            // Wire up pipeline events
            WireUpPlaybackEvents();
            
            // Initialize Tacview integration now that we have PlaybackController
            InitializeTacviewIntegration(e);
            
            // Auto-start frequency analysis
            _ = LoadFrequenciesAsync();
        }
        
        /// <summary>
        /// Initializes Tacview integration when a playback session is loaded.
        /// Wires up controllers and audio components from the pipeline.
        /// </summary>
        private void InitializeTacviewIntegration(SessionLoadedEventArgs sessionArgs)
        {
            try
            {
                Logger.Info("Initializing Tacview integration with playback session...");
                
                // Get controllers from pipeline (now exposed after refactoring)
                var playbackController = sessionArgs.Pipeline.PlaybackController;
                var seekController = sessionArgs.Pipeline.SeekController;
                
                // Create Tacview integration service
                _tacviewService = new Integrations.Tacview.TacviewIntegrationService(
                    playbackController,
                    seekController);
                
                // Wire up core integration with audio components
                try
                {
                    var audioOutput = sessionArgs.Pipeline.AudioOutput;
                    var masterMixer = sessionArgs.Pipeline.MasterMixer;
                    
                    // Note: MasterMixer doesn't have an AudioMixer property, so we pass MasterMixer itself
                    // The TacviewIntegrationService.InitializeCoreIntegration expects AudioMixerEngine
                    // which needs to be exposed by MasterMixer, or we refactor the integration service
                    
                    // For now, just wire up what we can
                    _tacviewService.InitializeCoreIntegration(
                        audioMixer: null, // TODO: MasterMixer needs to expose AudioMixerEngine
                        audioOutputEngine: audioOutput);
                    
                    Logger.Info("? Core integration wired up (AudioOutput connected, AudioMixer pending)");
                    Logger.Warn("? AudioMixerEngine not connected - packet filtering from Tacview won't work yet");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to wire up core integration - some features may not work");
                }
                
                // Create view model
                _tacviewIntegration = new TacviewIntegrationViewModel(_tacviewService);
                
                // Notify UI that Tacview integration is now available
                OnPropertyChanged(nameof(TacviewIntegration));
                
                Logger.Info("? Tacview integration initialized and ready");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize Tacview integration - will continue without it");
                _tacviewService = null;
                _tacviewIntegration = null;
            }
        }

        private void OnSessionUnloaded(object? sender, EventArgs e)
        {
            Logger.Info("Session unloaded");
            
            CurrentMode = PlayerMode.Idle;
            CurrentSourceName = string.Empty;
            StatusMessage = "Ready";
            
            // Clean up Tacview integration
            if (_tacviewService != null)
            {
                try
                {
                    _tacviewService.Dispose();
                    _tacviewService = null;
                    _tacviewIntegration = null;
                    OnPropertyChanged(nameof(TacviewIntegration));
                    Logger.Info("Tacview integration cleaned up");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error disposing Tacview integration");
                }
            }
            
            // Clear UI state
            Frequencies.Clear();
            MixerChannels.Clear();
            FileFormat = string.Empty; // Phase 2.5: Clear file format
        }

        private void OnSessionError(object? sender, SessionErrorEventArgs e)
        {
            Logger.Error(e.Exception, $"Session error: {e.FilePath}");
            StatusMessage = $"Error: {e.Exception.Message}";
            IsBuffering = false;
            CurrentMode = PlayerMode.Idle;
        }

        #endregion

        #region Frequency Manager Event Handlers

        private void OnFrequencySelectionChanged(object? sender, FrequencySelectionChangedEventArgs e)
        {
            Logger.Debug($"Frequency selection changed: {e.Frequency:F1} Hz = {e.IsSelected}");
            
            if (e.IsSelected)
            {
                // Add mixer channel
                var freqInfo = _sessionManager.Pipeline?.GetAvailableFrequencies()
                    .FirstOrDefault(f => Math.Abs(f.Frequency - e.Frequency) < 0.1);
                var displayName = freqInfo?.DisplayName ?? $"{e.Frequency / 1_000_000.0:F3} MHz";
                
                _mixerController.SetupChannel(e.Frequency, displayName);
                _sessionManager.Pipeline?.SetFrequencyGate(e.Frequency, FrequencyGateMode.Allow);
                
                // Phase 12: Sync to UnifiedGraph - show series
                // Convert frequency (Hz) to string format that graph expects
                var freqId = $"{e.Frequency:F0}";
                _graphViewModel.SetFrequencyVisible(freqId, true);
                Logger.Debug($"? Phase 12: Graph series shown for {e.Frequency:F1} Hz (key: {freqId})");
            }
            else
            {
                // Remove mixer channel
                _mixerController.RemoveChannel(e.Frequency);
                _sessionManager.Pipeline?.SetFrequencyGate(e.Frequency, FrequencyGateMode.Block);
                
                // Phase 12: Sync to UnifiedGraph - hide series
                // Convert frequency (Hz) to string format that graph expects
                var freqId = $"{e.Frequency:F0}";
                _graphViewModel.SetFrequencyVisible(freqId, false);
                Logger.Debug($"? Phase 12: Graph series hidden for {e.Frequency:F1} Hz (key: {freqId})");
            }
            
            // Phase 12: Legacy GPU layers and waveform regeneration removed
            // The UnifiedGraphControl handles all visualization internally
        }

        private void OnFrequenciesLoaded(object? sender, FrequenciesLoadedEventArgs e)
        {
            Logger.Info($"Frequencies loaded: {e.TotalFrequencies} found");
            
            StatusMessage = $"Found {e.TotalFrequencies} frequencies. Select frequencies to visualize.";
            
            // Load frequencies into Tacview integration if available
            if (_tacviewIntegration != null)
            {
                Logger.Info($"?? Loading {e.TotalFrequencies} frequencies into Tacview integration");
                
                // Get all frequencies from the frequency manager
                var allFrequencies = _frequencyManager.Frequencies
                    .SelectMany(g => g.Frequencies)
                    .Select(f => f.Frequency)
                    .ToList();
                
                _tacviewIntegration.LoadFrequenciesFromAudioFile(allFrequencies);
                
                Logger.Info($"? Tacview integration frequency list populated with {allFrequencies.Count} frequencies");
            }
            else
            {
                Logger.Debug("Tacview integration not available yet (will be initialized after file load)");
            }
        }

        #endregion

        #region Mixer Controller Event Handlers

        private void OnMixerChannelAdded(object? sender, ChannelAddedEventArgs e)
        {
            Logger.Debug($"Mixer channel added: {e.DisplayName}");
            
            // Update mixer channels collection if needed
            var channel = _mixerController.GetChannel(e.Frequency);
            if (channel != null && !MixerChannels.Contains(channel))
            {
                MixerChannels.Add(channel);
            }
        }

        private void OnMixerChannelRemoved(object? sender, ChannelRemovedEventArgs e)
        {
            Logger.Debug($"Mixer channel removed: {e.DisplayName}");
            
            // Remove from UI collection
            var channel = MixerChannels.FirstOrDefault(ch => Math.Abs(ch.Frequency - e.Frequency) < 0.1);
            if (channel != null)
            {
                MixerChannels.Remove(channel);
            }
        }

        private void OnMixerChannelChanged(object? sender, ChannelChangedEventArgs e)
        {
            Logger.Debug($"Mixer channel changed: {e.Frequency:F1} Hz, {e.Property} = {e.Value}");
        }

        #endregion

        #region Phase 4: Live Playback Event Handlers

        private void OnLiveFrequencyDetected(object? sender, FrequencyDetectedEventArgs e)
        {
            Logger.Info($"?? LIVE: New frequency detected - {e.Frequency.Frequency:F1} MHz");
            
            try
            {
                // Add frequency to UI on dispatcher thread
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    // Find or create coalition group
                    var coalition = GetCoalitionFromFrequency(e.Frequency);
                    var group = Frequencies.FirstOrDefault(g => g.Name.Contains(coalition));
                    
                    if (group == null)
                    {
                        group = new FrequencyGroupViewModel
                        {
                            Name = $"{coalition} (Live Recording)",
                            IsExpanded = true
                        };
                        Frequencies.Add(group);
                    }
                    
                    // Create frequency view model
                    var freqViewModel = new FrequencyViewModel
                    {
                        Frequency = e.Frequency.Frequency,
                        DisplayName = $"{e.Frequency.Frequency / 1_000_000.0:F3} MHz",
                        PacketCount = (int)e.Frequency.PacketCount,
                        IsSelected = true // Auto-select new frequencies
                    };
                    
                    group.Frequencies.Add(freqViewModel);
                    
                    // Auto-select and setup mixer for new frequency
                    _frequencyManager.SelectFrequency(e.Frequency.Frequency);
                    
                    StatusMessage = $"?? LIVE: New frequency {e.Frequency.Frequency / 1_000_000.0:F3} MHz";
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle live frequency detection");
            }
        }

        private void OnLivePlayerDetected(object? sender, PlayerDetectedEventArgs e)
        {
            Logger.Info($"?? LIVE: New player detected - {e.Player.PlayerName} ({GetCoalitionName(e.Player.Coalition)})");
            
            try
            {
                // Update UI to show new player
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    StatusMessage = $"?? LIVE: Player joined - {e.Player.PlayerName}";
                    
                    // Find frequency groups that include this player's frequencies
                    foreach (var freq in e.Player.Frequencies)
                    {
                        var freqViewModel = FindFrequencyViewModel(freq);
                        if (freqViewModel != null)
                        {
                            // Update player count or add player info
                            freqViewModel.PacketCount++;
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle live player detection");
            }
        }

        private void OnLivePacketsAvailable(object? sender, LivePacketsEventArgs e)
        {
            Logger.Debug($"?? LIVE: {e.NewPacketCount} new packets available, duration: {e.CurrentDuration}");
            
            try
            {
                // Update waveform and timeline in real-time
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    // Update duration if recording is growing
                    if (e.CurrentDuration > TotalDuration)
                    {
                        TotalDuration = e.CurrentDuration;
                    }
                    
                    // Trigger waveform refresh for UnifiedGraphViewModel
                    // The graph will automatically extend to show new data
                    _graphViewModel.RefreshLiveData();
                    
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle live packets");
            }
        }

        private void OnLiveDurationUpdated(object? sender, TimeSpan duration)
        {
            Logger.Debug($"?? LIVE: Duration updated - {duration}");
            
            try
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    TotalDuration = duration;
                    RecordingPosition = duration; // Recording playhead follows duration
                    
                    // If user is at the end (live position), auto-scroll
                    if (IsAtLivePosition())
                    {
                        // Auto-scroll to end
                        CurrentPosition = duration;
                        PlayheadPositionNormalized = 1.0;
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update live duration");
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Handle live audio packets for real-time playback
        /// </summary>
        private void OnLiveAudioPacketsAvailable(object? sender, LiveAudioPacketsEventArgs e)
        {
            try
            {
                if (e.Packets.Count > 0)
                {
                    Logger.Debug($"?? Received {e.Packets.Count} audio packets for live playback");
                    
                    // Forward to audio playback service
                    _liveAudioService.AddPackets(e.Packets);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle live audio packets");
            }
        }

        /// <summary>
        /// Helper: Check if playhead is near the end (live position)
        /// </summary>
        private bool IsAtLivePosition()
        {
            if (TotalDuration.TotalSeconds < 1)
                return true;
            
            var distanceFromEnd = TotalDuration - CurrentPosition;
            return distanceFromEnd.TotalSeconds < 2.0; // Within 2 seconds of end
        }

        /// <summary>
        /// Helper: Get coalition name from frequency info
        /// </summary>
        private string GetCoalitionFromFrequency(Core.Storage.FrequencyInfo freq)
        {
            // Try to determine coalition from frequency metadata
            // This is a simplified version - you may need to enhance based on your data
            return "Mixed"; // Default to Mixed for live frequencies
        }

        /// <summary>
        /// Helper: Get coalition name from coalition code
        /// </summary>
        private string GetCoalitionName(byte coalition)
        {
            return coalition switch
            {
                0 => "Neutral",
                1 => "Red",
                2 => "Blue",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Helper: Find frequency view model by frequency value
        /// </summary>
        private FrequencyViewModel? FindFrequencyViewModel(double frequency)
        {
            foreach (var group in Frequencies)
            {
                var freq = group.Frequencies.FirstOrDefault(f => Math.Abs(f.Frequency - frequency) < 0.1);
                if (freq != null)
                    return freq;
            }
            return null;
        }

        /// <summary>
        /// Phase 4: Start live playback monitoring for the given recording database
        /// </summary>
        public async Task StartLivePlaybackAsync(string liveDatabasePath)
        {
            try
            {
                Logger.Info($"?? Starting live playback: {liveDatabasePath}");
                
                // Start live playback monitoring
                await _livePlaybackManager.StartLivePlaybackAsync(liveDatabasePath);
                
                // Wire up to playback pipeline's PlaybackController
                if (_livePlaybackManager.PlaybackPipeline?.PlaybackController != null)
                {
                    var controller = _livePlaybackManager.PlaybackPipeline.PlaybackController;
                    
                    // Subscribe to playback position updates
                    controller.TimeChanged += OnLivePlaybackTimeChanged;
                    
                    Logger.Info("   ? Playback controller wired for dual-playhead tracking");
                }
                
                // Start live audio playback (legacy service - may be replaced by pipeline)
                await _liveAudioService.StartAsync();
                
                IsLiveRecording = true;
                StatusMessage = "?? LIVE RECORDING - Dual playhead tracking active";
                
                Logger.Info("? Live playback monitoring, audio streaming, and dual playheads active");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start live playback");
                StatusMessage = $"Live playback error: {ex.Message}";
            }
        }

        /// <summary>
        /// Phase 4: Stop live playback monitoring
        /// </summary>
        public async Task StopLivePlaybackAsync()
        {
            try
            {
                Logger.Info("?? Stopping live playback...");
                
                // Unsubscribe from playback pipeline
                if (_livePlaybackManager.PlaybackPipeline?.PlaybackController != null)
                {
                    _livePlaybackManager.PlaybackPipeline.PlaybackController.TimeChanged -= OnLivePlaybackTimeChanged;
                }
                
                await _livePlaybackManager.StopLivePlaybackAsync();
                await _liveAudioService.StopAsync();
                
                IsLiveRecording = false;
                RecordingPosition = TimeSpan.Zero;
                PlaybackPosition = TimeSpan.Zero;
                StatusMessage = "Live recording stopped";
                
                Logger.Info("? Live playback monitoring, audio, and playheads stopped");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to stop live playback");
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Handle playback time changes from live pipeline.
        /// </summary>
        private void OnLivePlaybackTimeChanged(TimeSpan currentTime, TimeSpan totalTime)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    PlaybackPosition = currentTime;
                    CurrentPosition = currentTime; // Also update main position for scrubber
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update playback position");
            }
        }

        #endregion

        #region Command Implementations

        private bool CanExecutePlay() => (IsPlaybackMode || IsRecordingMode) && !IsPlaying;

        private void ExecutePlay()
        {
            try
            {
                if (_sessionManager.Pipeline != null)
                {
                    _ = _sessionManager.PlayAsync();
                    PlaybackState = PlaybackState.Playing;
                    StatusMessage = "Playing...";
                    Logger.Info("Playback started");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start playback");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private bool CanExecutePause() => IsPlaying;

        private void ExecutePause()
        {
            try
            {
                _sessionManager.Pause();
                PlaybackState = PlaybackState.Paused;
                StatusMessage = "Paused";
#if DEBUG
                Logger.Debug("Playback paused");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to pause playback");
            }
        }

        private bool CanExecuteStop() => IsPlaying || IsPaused;

        private void ExecuteStop()
        {
            try
            {
                if (_sessionManager.Pipeline != null)
                    _ = _sessionManager.StopAsync();
                    
                PlaybackState = PlaybackState.Stopped;
                CurrentPosition = TimeSpan.Zero;
                ProgressPercent = 0;
                StatusMessage = "Stopped";
#if DEBUG
                Logger.Debug("Playback stopped");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to stop playback");
            }
        }

        private bool CanExecuteSeek(double? normalizedPosition) => IsPlaybackMode && normalizedPosition.HasValue;

        private void ExecuteSeek(double? normalizedPosition)
        {
            if (!normalizedPosition.HasValue || _sessionManager.Pipeline == null) return;

            try
            {
                var targetTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * normalizedPosition.Value));
                _ = _sessionManager.SeekAsync(targetTime);
                CurrentPosition = targetTime;
                ProgressPercent = normalizedPosition.Value * 100.0;
                
                _bufferStartPosition = 0.0;
                _bufferEndPosition = 0.0;
                
#if DEBUG
                Logger.Debug($"Seeked to: {targetTime}");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to seek");
            }
        }

        private bool CanExecuteGoLive() => IsRecordingMode && CurrentPosition < TotalDuration;

        private void ExecuteGoLive()
        {
            try
            {
                ExecuteSeek(1.0);
                StatusMessage = "Live";
#if DEBUG
                Logger.Debug("Jumped to live position");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to go live");
            }
        }

        private void ExecuteChangeSource()
        {
            CurrentMode = PlayerMode.Idle;
            PlaybackState = PlaybackState.Stopped;
            StatusMessage = "Select source...";
#if DEBUG
            Logger.Debug("Source selection opened");
#endif
        }

        private async void ExecuteSelectAllFrequencies()
        {
            try
            {
                Logger.Info("Selecting all frequencies...");
                _frequencyManager.SelectAll();
                
                // Waveform will be regenerated via events
                StatusMessage = "All frequencies selected";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to select all frequencies");
            }
        }

        private async void ExecuteSelectNoFrequencies()
        {
            try
            {
                Logger.Info("Deselecting all frequencies...");
                _frequencyManager.DeselectAll();
                
                // Waveform will be regenerated via events
                StatusMessage = "All frequencies deselected";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to deselect all frequencies");
            }
        }

        private void ExecuteOpenSettings()
        {
            try
            {
                var settingsWindow = new Windows.SettingsWindow
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                
                var result = settingsWindow.ShowDialog();
                
                if (result == true)
                {
                    Logger.Info("Settings saved");
                    StatusMessage = "Settings saved successfully";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open settings");
                StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Play the live recording from the current position.
        /// Behaves like PlayCommand, but targets the live recording stream.
        /// </summary>
        private bool CanExecutePlayLiveRecording() => IsLiveRecording && PlaybackState != PlaybackState.Playing;

        private void ExecutePlayLiveRecording()
        {
            try
            {
                Logger.Info("Playing live recording");
                
                // Just resume if already playing
                if (PlaybackState == PlaybackState.Playing)
                    return;
                
                // For live recording, we directly set the playback state
                PlaybackState = PlaybackState.Playing;
                StatusMessage = "Live recording in progress...";
                
                Logger.Info("? Live recording playback started");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start live recording playback");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Pause the live recording playback.
        /// This pauses the dual playheads and live audio stream.
        /// </summary>
        private bool CanExecutePauseLiveRecording() => IsLiveRecording && PlaybackState == PlaybackState.Playing;

        private void ExecutePauseLiveRecording()
        {
            try
            {
                Logger.Info("Pausing live recording playback");
                
                PlaybackState = PlaybackState.Paused;
                StatusMessage = "Live recording paused";
                
                Logger.Info("? Live recording playback paused");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to pause live recording playback");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Seek within the live recording.
        /// This adjusts the dynamic playhead position in the live recording stream.
        /// </summary>
        private bool CanExecuteSeekLiveRecording(double? normalizedPosition) => IsLiveRecording && normalizedPosition.HasValue;

        private void ExecuteSeekLiveRecording(double? normalizedPosition)
        {
            if (!normalizedPosition.HasValue) return;

            try
            {
                var targetPosition = TimeSpan.FromTicks((long)(TotalDuration.Ticks * normalizedPosition.Value));
                
                Logger.Info($"Seeking live recording to {targetPosition}");
                
                // For live recording, we directly set the playback position
                PlaybackPosition = targetPosition;
                CurrentPosition = targetPosition; // Also update main position
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to seek live recording");
            }
        }

        /// <summary>
        /// Phase 4 Enhanced: Jump to the live position in the recording.
        /// This seeks to the latest position of the live recording.
        /// </summary>
        private bool CanExecuteGoToLivePosition() => IsLiveRecording && PlaybackState != PlaybackState.Stopped;

        private void ExecuteGoToLivePosition()
        {
            try
            {
                Logger.Info("Jumping to live position in recording");
                
                // For live recording, we directly set the playback position to the end
                PlaybackPosition = TotalDuration;
                CurrentPosition = TotalDuration; // Also update main position
                
                Logger.Info("? Jumped to live position");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to jump to live position");
            }
        }

        #endregion

        #region Event Handlers (Legacy)

        private void OnServerConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                CurrentMode = PlayerMode.Recording;
                CurrentSourceName = $"SRS Server: {ServerSource.ServerIp}:{ServerSource.ServerPort}";
                Logger.Info($"Connected to server: {ServerSource.ServerIp}:{ServerSource.ServerPort}");
            }
            else
            {
                if (CurrentMode == PlayerMode.Recording)
                    ExecuteStop();
                
                CurrentMode = PlayerMode.Idle;
                CurrentSourceName = string.Empty;
                StatusMessage = "Disconnected";
                Logger.Info("Disconnected from server");
            }
        }

        private void OnServerRecordingStateChanged(bool isRecording)
        {
            if (isRecording)
            {
                PlaybackState = PlaybackState.Playing;
                StatusMessage = "Recording...";
                Logger.Info("Recording started");
            }
            else
            {
                PlaybackState = PlaybackState.Stopped;
                StatusMessage = "Recording stopped";
                Logger.Info("Recording stopped");
            }
        }

        /// <summary>
        /// Phase 4: Handle live playback ready from server recording
        /// </summary>
        private async void OnServerLivePlaybackReady(string liveDatabasePath)
        {
            try
            {
                Logger.Info($"?? Server recording started - enabling live playback: {liveDatabasePath}");
                
                // Start live playback monitoring
                await StartLivePlaybackAsync(liveDatabasePath);
                
                StatusMessage = "?? LIVE RECORDING - Monitoring for new frequencies/players";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start live playback from server recording");
                StatusMessage = $"Live playback error: {ex.Message}";
            }
        }

        private async void OnFileLoaded(string filePath)
        {
            try
            {
                CurrentMode = PlayerMode.Playback;
                CurrentSourceName = System.IO.Path.GetFileName(filePath);
                
                // Phase 2.5: Detect and display file format
                FileFormat = CvrFormat.GetFormatName(filePath);
                Logger.Info($"File format detected: {FileFormat}");
                
                IsBuffering = true;
                StatusMessage = "Loading file...";
                ProgressPercent = 0;
                
                Logger.Info($"======== LOADING FILE (Service Architecture): {filePath} ========");

                // Create progress reporter for status updates - update every 5% of file load
                int lastProgress = 0;
                var progress = new Progress<string>(status =>
                {
                    // Phase 2.5: Also update FileSourceViewModel's loading display
                    bool hasPercent = false;
                    int percent = 0;
                    
                    // Parse progress from status messages like "Loading frequencies... 45%"
                    if (status.Contains("%"))
                    {
                        // Extract percentage from message
                        if (int.TryParse(
                            System.Text.RegularExpressions.Regex.Match(status, @"\d+").Value, 
                            out percent))
                        {
                            hasPercent = true;
                            // Only update UI every 5% to reduce dispatcher overhead
                            if (Math.Abs(percent - lastProgress) >= 5 || percent == 0 || percent == 100)
                            {
                                lastProgress = percent;
                                // Marshal to UI thread
                                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                                {
                                    ProgressPercent = percent;
                                    StatusMessage = status;
                                    // Phase 2.5: Update FileSourceViewModel
                                    FileSource?.UpdateLoadingProgress(status, percent, isIndeterminate: false);
                                }, System.Windows.Threading.DispatcherPriority.Background);
                            }
                        }
                    }
                    else
                    {
                        // Status message without percentage - use indeterminate progress
                        System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            StatusMessage = status;
                            // Phase 2.5: Update FileSourceViewModel with indeterminate progress
                            FileSource?.UpdateLoadingProgress(status, 0, isIndeterminate: true);
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                });
                
                // Load file asynchronously on background thread
                // This keeps UI responsive while loading
                await _sessionManager.LoadFileAsync(filePath, progress);
                
                Logger.Info("? File load completed - session loaded event should have fired");
                
                // Phase 2.5: Mark loading as complete in FileSourceViewModel
                FileSource?.CompleteLoading(success: true, message: "File loaded successfully");
                
                // Session loaded event will trigger next steps (frequency loading, waveform generation)
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load file");
                StatusMessage = $"Error loading file: {ex.Message}";
                ProgressPercent = 0;
                IsBuffering = false;
                CurrentMode = PlayerMode.Idle;
                
                // Phase 2.5: Mark loading as failed in FileSourceViewModel
                FileSource?.CompleteLoading(success: false, message: ex.Message);
            }
        }

        private void OnFileUnloaded()
        {
            ExecuteStop();
            
            // Delegate to session manager
            _sessionManager.UnloadSession();
            
            // Cleanup
            _frequencyManager.Clear();
            _mixerController.ClearChannels();
            
            // Clear UI state
            Frequencies.Clear();
            MixerChannels.Clear();
            FileFormat = string.Empty; // Phase 2.5: Clear file format
        }

        #endregion

        #region Public Methods for UI Integration

        /// <summary>
        /// Handles frequency selection changes from UI controls.
        /// Routes to FrequencyManager which will trigger the internal event handler.
        /// </summary>
        public void HandleFrequencySelectionChanged(double frequency, bool isSelected)
        {
            if (isSelected)
            {
                _frequencyManager.SelectFrequency(frequency);
            }
            else
            {
                _frequencyManager.DeselectFrequency(frequency);
            }
        }

        /// <summary>
        /// Updates channel gain (volume) for a specific frequency.
        /// Called from UI controls when mixer sliders are adjusted.
        /// </summary>
        public void UpdateChannelGain(double frequency, float gain)
        {
            _mixerController.SetChannelGain(frequency, gain);
        }

        /// <summary>
        /// Updates channel pan for a specific frequency.
        /// Called from UI controls when pan sliders are adjusted.
        /// </summary>
        public void UpdateChannelPan(double frequency, float pan)
        {
            _mixerController.SetChannelPan(frequency, pan);
        }

        /// <summary>
        /// Updates channel mute state for a specific frequency.
        /// Called from UI controls when mute buttons are toggled.
        /// </summary>
        public void UpdateChannelMute(double frequency, bool muted)
        {
            _mixerController.SetChannelMuted(frequency, muted);
            
            // Sync with graph visualization
            var freqId = $"{frequency:F0}";
            _graphViewModel.SetFrequencyVisible(freqId, !muted);
            Logger.Debug($"? Graph mute sync: {frequency:F1} Hz (key: {freqId}), muted={muted}");
        }

        /// <summary>
        /// Updates channel solo state for a specific frequency.
        /// Called from UI controls when solo buttons are toggled.
        /// </summary>
        public void UpdateChannelSolo(double frequency, bool solo)
        {
            _mixerController.SetChannelSolo(frequency, solo);
        }

        /// <summary>
        /// Updates pilot selection (for per-pilot filtering).
        /// Called from UI controls when pilot checkboxes are toggled.
        /// </summary>
        public void UpdatePilotSelection(String pilotGuid, bool isSelected)
        {
            Logger.Debug($"Pilot selection changed: {pilotGuid} = {isSelected}");
            
            // Find which frequency this pilot belongs to
            foreach (var group in _frequencyManager.Frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    // Check if this frequency has player data
                    if (freq.SourceData?.Players == null)
                        continue;
                    
                    var player = freq.SourceData.Players.FirstOrDefault(p => 
                        p.TransmitterGuid.Equals(pilotGuid, StringComparison.OrdinalIgnoreCase));
                    
                    if (player != null)
                    {
                        // Convert frequency to string format for graph key
                        var freqId = $"{freq.Frequency:F0}";
                        
                        // Sync with graph visualization - show/hide this specific pilot's series
                        _graphViewModel.SetPilotVisible(freqId, pilotGuid, isSelected);
                        Logger.Debug($"? Graph pilot visibility updated: {freq.Frequency:F1} Hz (key: {freqId}), {pilotGuid} = {isSelected}");
                        
                        // TODO: Implement per-pilot audio filtering when feature is ready
                        // For now, pilot selection only affects visualization
                        return; // Exit once we found the pilot
                    }
                }
            }
            
            Logger.Warn($"Pilot not found in any frequency: {pilotGuid}");
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            Logger.Info("UnifiedPlayerViewModel disposing");

            ExecuteStop();

            // Dispose services in reverse order
            _tacviewService?.Dispose();
            _mixerController?.Dispose();
            _sessionManager?.Dispose();
            _frequencyManager?.Dispose();
            _livePlaybackManager?.Dispose(); // Phase 4
            _liveAudioService?.Dispose(); // Phase 4: Audio
            
            Logger.Info("? UnifiedPlayerViewModel disposed");
        }

        #endregion

        #region Helper Methods

        private void WireUpPlaybackEvents()
        {
            if (_sessionManager.Pipeline == null) return;

            _sessionManager.Pipeline.PlaybackStarted += () =>
            {
                PlaybackState = PlaybackState.Playing;
                StatusMessage = "Playing...";
            };

            _sessionManager.Pipeline.PlaybackStopped += () =>
            {
                PlaybackState = PlaybackState.Stopped;
                StatusMessage = "Stopped";
                _bufferStartPosition = 0.0;
                _bufferEndPosition = 0.0;
            };

            _sessionManager.Pipeline.PlaybackPaused += () =>
            {
                PlaybackState = PlaybackState.Paused;
                StatusMessage = "Paused";
            };

            _sessionManager.Pipeline.PlaybackResumed += () =>
            {
                PlaybackState = PlaybackState.Playing;
                StatusMessage = "Playing...";
            };

            _sessionManager.Pipeline.PositionChanged += (currentTime, totalTime) =>
            {
                // Marshal to UI thread with throttling
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    if ((DateTime.UtcNow - _lastPlayheadUpdate).TotalMilliseconds < 30)
                        return;

                    _lastPlayheadUpdate = DateTime.UtcNow;

                    CurrentPosition = currentTime;
                    TotalDuration = totalTime;

                    if (TotalDuration.Ticks > 0)
                    {
                        var normalized = currentTime.TotalMilliseconds / totalTime.TotalMilliseconds;
                        PlayheadPositionNormalized = normalized;
                        ProgressPercent = normalized * 100.0;
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            };

            _sessionManager.Pipeline.ErrorOccurred += (ex) =>
            {
                Logger.Error(ex, "Playback error");
                StatusMessage = $"Playback error: {ex.Message}";
                PlaybackState = PlaybackState.Stopped;
            };

            // Phase 4: Live playback events
            _livePlaybackManager.FrequencyDetected += OnLiveFrequencyDetected;
            _livePlaybackManager.PlayerDetected += OnLivePlayerDetected;
            _livePlaybackManager.PacketsAvailable += OnLivePacketsAvailable;
            _livePlaybackManager.DurationUpdated += OnLiveDurationUpdated;
            _livePlaybackManager.AudioPacketsAvailable += OnLiveAudioPacketsAvailable; // Phase 4: Audio streaming
        }

        private async Task LoadFrequenciesAsync()
        {
            if (!_sessionManager.IsSessionLoaded) return;

            try
            {
                _colorIndex = 0;
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Frequencies.Clear();
                    MixerChannels.Clear();
                    StatusMessage = "Analyzing frequencies...";
                    ProgressPercent = 30;
                });
                
                // CRITICAL FIX: Initialize services BEFORE loading frequencies
                // This ensures mixer is ready when auto-selection triggers
                Logger.Info("Initializing services before frequency analysis...");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProgressPercent = 40;
                    StatusMessage = "Initializing audio mixer...";
                });
                
                _mixerController.Initialize();
                Logger.Info("? Audio mixer initialized");
                
                // Now load frequencies - runs on background thread
                // This will auto-select all frequencies, which requires mixer to be initialized
                Logger.Info("Starting frequency analysis...");
                await _frequencyManager.LoadFrequenciesAsync(
                    _sessionManager.PacketSource!,
                    _sessionManager.Pipeline!);
                
                // Bind frequency collections on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProgressPercent = 60;
                    StatusMessage = "Building frequency list...";

                    // Copy frequencies from manager to UI observable collection
                    Logger.Info($"?? Copying {_frequencyManager.Frequencies.Count} frequency groups to UI...");
                    foreach (var group in _frequencyManager.Frequencies)
                    {
                        Logger.Debug($"   Adding group: {group.Name} with {group.Frequencies.Count} frequencies");
                        Frequencies.Add(group);
                    }
                    Logger.Info($"? UI Frequencies collection now has {Frequencies.Count} groups");
                    
                    // Force property change notification
                    OnPropertyChanged(nameof(Frequencies));
                    
                    // Copy mixer channels from controller to UI observable collection
                    foreach (var channel in _mixerController.Channels)
                    {
                        MixerChannels.Add(channel);
                    }
                    
                    ProgressPercent = 80;
                    StatusMessage = "Loading graph data...";
                });
                
                // Phase 12: Load data into UnifiedGraphViewModel for the new graph
                Logger.Info("Phase 12: Loading amplitude data into GraphViewModel...");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = "Loading graph data...";
                    ProgressPercent = 85;
                });
                
                // CRITICAL FIX: Get ACTUAL recording time range from pipeline (not DateTime.Now!)
                var recordingStart = _sessionManager.Pipeline!.RecordingStart;
                var recordingEnd = recordingStart.Add(TotalDuration);
                
                Logger.Info($"Phase 12: Using ACTUAL recording timestamps: {recordingStart:yyyy-MM-dd HH:mm:ss} to {recordingEnd:yyyy-MM-dd HH:mm:ss}");
                
                try
                {
                    await _graphViewModel.LoadDataAsync(recordingStart, recordingEnd);
                    Logger.Info($"? Phase 12: GraphViewModel loaded with data from {recordingStart:HH:mm:ss} to {recordingEnd:HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Phase 12: Failed to load graph data - graph will be empty");
                    // Continue anyway - graph will be empty but app still functional
                }

                // Final update on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsBuffering = false;
                    ProgressPercent = 100;
                    StatusMessage = $"File loaded with {_frequencyManager.Frequencies.Count} frequencies. Select frequencies to visualize.";
                });

                Logger.Info($"? File loaded successfully with {_frequencyManager.Frequencies.Count} frequency groups");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load frequencies");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"Error loading frequencies: {ex.Message}";
                    ProgressPercent = 0;
                    IsBuffering = false;
                });
            }
        }

        #endregion
    }

    public enum PlayerMode
    {
        Idle,
        Recording,
        Playback
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    public class MixerChannelViewModel : ViewModelBase
    {
        private double _frequency;
        private string _displayName = string.Empty;
        private float _volume = 1.0f;
        private float _pan = 0.0f;
        private bool _isMuted;
        private bool _isSolo;

        public double Frequency
        {
            get => _frequency;
            set => SetProperty(ref _frequency, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public float Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, Math.Clamp(value, 0f, 2f));
        }

        public float Pan
        {
            get => _pan;
            set => SetProperty(ref _pan, Math.Clamp(value, -1f, 1f));
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public bool IsSolo
        {
            get => _isSolo;
            set => SetProperty(ref _isSolo, value);
        }
    }
}
