using System;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Playback;
using AeroDebrief.Core.Interfaces.Storage;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Phase 4 Enhanced: Live recording playback pipeline.
    /// Integrates with existing FilePlaybackPipeline infrastructure for consistent audio processing.
    /// 
    /// DUAL PLAYHEAD ARCHITECTURE:
    /// 1. Recording Playhead (Static) - Shows current recording position (latest packet)
    /// 2. Playback Playhead (Dynamic) - Shows audio output position (synced to PlaybackController)
    /// 
    /// INTEGRATION:
    /// - Uses IUnitOfWork as packet source (live recording database)
    /// - Wraps existing FilePlaybackPipeline for audio processing
    /// - Reuses AudioMixerEngine, MasterMixer, AudioOutputEngine
    /// - Provides consistent audio quality and effects
    /// </summary>
    public sealed class LiveRecordingPlaybackPipeline : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly IUnitOfWork _liveStore;
        private readonly string _liveDbPath;
        
        // Pipeline components (reuse existing infrastructure)
        private FilePacketSource? _packetSource;
        private FilePlaybackPipeline? _pipeline;
        
        // Playback state
        private bool _isInitialized;
        private bool _disposed;
        private TimeSpan _recordingPosition; // Where recording is currently at
        private TimeSpan _playbackPosition;  // Where audio playback is at
        
        /// <summary>
        /// Gets the current recording position (static playhead - the "pencil").
        /// This is where new packets are being written.
        /// </summary>
        public TimeSpan RecordingPosition
        {
            get => _recordingPosition;
            private set
            {
                if (_recordingPosition != value)
                {
                    _recordingPosition = value;
                    RecordingPositionChanged?.Invoke(this, value);
                }
            }
        }
        
        /// <summary>
        /// Gets the current playback position (dynamic playhead - audio output).
        /// This is where audio is currently playing from.
        /// </summary>
        public TimeSpan PlaybackPosition
        {
            get => _playbackPosition;
            private set
            {
                if (_playbackPosition != value)
                {
                    _playbackPosition = value;
                    PlaybackPositionChanged?.Invoke(this, value);
                }
            }
        }
        
        /// <summary>
        /// Gets the recording start time.
        /// </summary>
        public DateTime RecordingStart { get; private set; }
        
        /// <summary>
        /// Gets the PlaybackController for UI integration.
        /// </summary>
        public PlaybackController? PlaybackController => _pipeline?.PlaybackController;
        
        /// <summary>
        /// Gets whether the pipeline is initialized and ready for playback.
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Raised when recording position updates (new packets written).
        /// </summary>
        public event EventHandler<TimeSpan>? RecordingPositionChanged;
        
        /// <summary>
        /// Raised when playback position updates (audio output).
        /// </summary>
        public event EventHandler<TimeSpan>? PlaybackPositionChanged;
        
        public LiveRecordingPlaybackPipeline(IUnitOfWork liveStore, string liveDbPath)
        {
            _liveStore = liveStore ?? throw new ArgumentNullException(nameof(liveStore));
            _liveDbPath = liveDbPath ?? throw new ArgumentNullException(nameof(liveDbPath));
            
            Logger.Info($"LiveRecordingPlaybackPipeline created for: {liveDbPath}");
        }
        
        /// <summary>
        /// Initializes the pipeline for live playback.
        /// Opens the live database as a FilePacketSource and creates a FilePlaybackPipeline.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized)
            {
                Logger.Warn("Pipeline already initialized");
                return;
            }
            
            try
            {
                Logger.Info("?? Initializing live recording playback pipeline...");
                
                // Phase 5: Get recording metadata using repository pattern
                var metadata = await _liveStore.Recording.GetMetadataAsync(ct);
                RecordingStart = metadata.StartTime;
                
                // Open the live database as a FilePacketSource
                // This allows us to reuse all existing playback infrastructure
                Logger.Info("   Opening live database as FilePacketSource...");
                _packetSource = new FilePacketSource(_liveDbPath);
                await _packetSource.OpenAsync(null, ct);
                
                Logger.Info($"   ? FilePacketSource opened: {_packetSource.TotalPackets} packets, {_packetSource.TotalDuration}");
                
                // Create FilePlaybackPipeline using the live packet source
                Logger.Info("   Creating FilePlaybackPipeline...");
                _pipeline = new FilePlaybackPipeline(_packetSource);
                await _pipeline.OpenAsync();
                
                Logger.Info("   ? FilePlaybackPipeline initialized");
                
                // Wire up position tracking
                if (_pipeline.PlaybackController != null)
                {
                    _pipeline.PlaybackController.TimeChanged += OnPlaybackTimeChanged;
                    Logger.Info("   ? Playback position tracking enabled");
                }
                
                // Set initial recording position to end of current data
                RecordingPosition = _packetSource.TotalDuration;
                PlaybackPosition = TimeSpan.Zero;
                
                _isInitialized = true;
                
                Logger.Info("? Live recording playback pipeline ready");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize live recording playback pipeline");
                throw;
            }
        }
        
        /// <summary>
        /// Updates the recording position when new packets arrive.
        /// Called by LivePlaybackManager when duration updates.
        /// </summary>
        public void UpdateRecordingPosition(TimeSpan newPosition)
        {
            if (!_isInitialized)
                return;
            
            RecordingPosition = newPosition;
            
            // Also update the packet source's view of total duration
            // This allows the playback to access new packets
            if (_packetSource != null)
            {
                // TODO: FilePacketSource needs a method to refresh metadata
                // For now, just update our local tracking
                Logger.Debug($"?? Recording position updated: {newPosition}");
            }
        }
        
        /// <summary>
        /// Starts audio playback from the current position.
        /// </summary>
        public async Task PlayAsync()
        {
            if (!_isInitialized || _pipeline == null)
                throw new InvalidOperationException("Pipeline not initialized");
            
            Logger.Info("?? Starting live recording playback...");
            await _pipeline.PlayAsync();
        }
        
        /// <summary>
        /// Pauses audio playback.
        /// </summary>
        public void Pause()
        {
            if (!_isInitialized || _pipeline == null)
                return;
            
            Logger.Info("?? Pausing live recording playback...");
            _pipeline.Pause();
        }
        
        /// <summary>
        /// Stops audio playback.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isInitialized || _pipeline == null)
                return;
            
            Logger.Info("?? Stopping live recording playback...");
            await _pipeline.StopAsync();
        }
        
        /// <summary>
        /// Seeks to a specific position in the recording.
        /// Useful for scrubbing through already-recorded audio during live recording.
        /// </summary>
        public async Task SeekAsync(TimeSpan position)
        {
            if (!_isInitialized || _pipeline == null)
                throw new InvalidOperationException("Pipeline not initialized");
            
            // Clamp to valid range (can't seek beyond recording position)
            var clampedPosition = TimeSpan.FromMilliseconds(
                Math.Max(0, Math.Min(position.TotalMilliseconds, RecordingPosition.TotalMilliseconds)));
            
            Logger.Info($"? Seeking to: {clampedPosition} (clamped to recording position)");
            await _pipeline.SeekAsync(clampedPosition);
        }
        
        /// <summary>
        /// "Go Live" - seek to the current recording position.
        /// </summary>
        public async Task GoLiveAsync()
        {
            if (!_isInitialized)
                return;
            
            Logger.Info("?? Going live - seeking to recording position");
            await SeekAsync(RecordingPosition);
        }
        
        /// <summary>
        /// Sets frequency gate for audio filtering.
        /// </summary>
        public void SetFrequencyGate(double frequency, FrequencyGateMode mode)
        {
            if (!_isInitialized || _pipeline == null)
                return;
            
            _pipeline.SetFrequencyGate(frequency, mode);
        }
        
        /// <summary>
        /// Gets available frequencies from the current recording.
        /// </summary>
        public List<Core.Playback.FrequencyInfo> GetAvailableFrequencies()
        {
            if (!_isInitialized || _pipeline == null)
                return new List<Core.Playback.FrequencyInfo>();
            
            return _pipeline.GetAvailableFrequencies();
        }
        
        /// <summary>
        /// Handles playback time changes from PlaybackController.
        /// </summary>
        private void OnPlaybackTimeChanged(TimeSpan currentTime, TimeSpan totalTime)
        {
            PlaybackPosition = currentTime;
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            Logger.Info("Disposing live recording playback pipeline...");
            
            if (_pipeline?.PlaybackController != null)
            {
                _pipeline.PlaybackController.TimeChanged -= OnPlaybackTimeChanged;
            }
            
            _pipeline?.Dispose();
            _packetSource?.Dispose();
            
            _disposed = true;
            
            Logger.Info("? Live recording playback pipeline disposed");
        }
    }
}
