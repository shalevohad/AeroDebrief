using AeroDebrief.Core.Interfaces.Playback;
using System.Globalization;
using AeroDebrief.Core;
using AeroDebrief.Core.Playback;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using NLog;

namespace AeroDebrief.Integrations.Tacview.Sync;

/// <summary>
/// Synchronizes AeroDebrief playback with Tacview timeline
/// Handles time updates, drift correction, and playback state changes
/// Implements IExternalTimeSource for integration with PlaybackController
/// </summary>
public class TacviewSyncService : IExternalTimeSource
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly PlaybackController _playbackController;
    private readonly SeekController _seekController;
    private readonly TacviewConfiguration _config;
    private readonly ScrubbingManager _scrubbingManager;
    
    private DateTime _recordingStartUtc;
    private bool _isInitialized;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private int _syncUpdateCount;
    
    // IExternalTimeSource implementation fields
    private TimeSpan _currentTime;
    private bool _isPlaying;
    private double _playbackSpeed = 1.0;
    
    /// <summary>
    /// Current sync quality (0-100%)
    /// </summary>
    public double SyncQuality { get; private set; } = 100.0;
    
    /// <summary>
    /// Current drift in milliseconds
    /// </summary>
    public int CurrentDriftMs { get; private set; }
    
    /// <summary>
    /// Target playback position from Tacview
    /// </summary>
    public TimeSpan TargetPosition { get; private set; }
    
    /// <summary>
    /// True if currently synchronized with Tacview
    /// </summary>
    public bool IsSynchronized { get; private set; }
    
    // IExternalTimeSource interface implementation
    public TimeSpan CurrentTime => TargetPosition;
    public bool IsPlaying => _isPlaying;
    public double PlaybackSpeed => _playbackSpeed;
    
    public event EventHandler<TimeSpan>? TimeChanged;
    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<double>? PlaybackSpeedChanged;
    
    /// <summary>
    /// Fired when sync quality changes significantly (>5%)
    /// </summary>
    public event EventHandler<double>? SyncQualityChanged;
    
    /// <summary>
    /// Fired when drift exceeds acceptable threshold
    /// </summary>
    public event EventHandler<int>? DriftExceeded;
    
    public TacviewSyncService(
        PlaybackController playbackController,
        SeekController seekController,
        TacviewConfiguration config,
        ScrubbingManager scrubbingManager)
    {
        _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
        _seekController = seekController ?? throw new ArgumentNullException(nameof(seekController));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _scrubbingManager = scrubbingManager ?? throw new ArgumentNullException(nameof(scrubbingManager));
    }
    
    /// <summary>
    /// Initializes sync service with recording start time
    /// </summary>
    public void Initialize(DateTime recordingStartUtc)
    {
        _recordingStartUtc = recordingStartUtc;
        _isInitialized = true;
        IsSynchronized = false;
        
        Logger.Info($"Tacview sync initialized. Recording start: {_recordingStartUtc:o}");
    }
    
    /// <summary>
    /// Handles time update message from Tacview
    /// </summary>
    public async Task HandleTimeUpdateAsync(TimeUpdateMessage message, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            Logger.Warn("Sync service not initialized");
            return;
        }
        
        try
        {
            // Parse Tacview time
            if (!DateTime.TryParse(message.MissionTimeUtc, null, DateTimeStyles.RoundtripKind, out var tacviewTime))
            {
                Logger.Error($"Failed to parse mission time: {message.MissionTimeUtc}");
                return;
            }
            
            // Convert to offset from recording start
            TargetPosition = tacviewTime - _recordingStartUtc;
            
            // Fire TimeChanged event for IExternalTimeSource
            TimeChanged?.Invoke(this, TargetPosition);
            
            // Get current playback position
            var currentPosition = _playbackController.CurrentPosition;
            
            // Calculate drift
            var drift = (TargetPosition - currentPosition).TotalMilliseconds;
            CurrentDriftMs = (int)Math.Abs(drift);
            
            // Update playback speed if changed
            if (Math.Abs(message.PlaybackSpeed - _playbackSpeed) > 0.01)
            {
                _playbackSpeed = message.PlaybackSpeed;
                _playbackController.SetPlaybackSpeed(message.PlaybackSpeed);
                PlaybackSpeedChanged?.Invoke(this, _playbackSpeed);
                Logger.Debug($"Playback speed updated: {_playbackSpeed:F2}x");
            }
            
            // Process scrubbing and get audio mute state
            var isScrubbing = _scrubbingManager.ProcessSeek(CurrentDriftMs, null); // AudioEngine passed when integrated
            
            // Apply drift correction based on magnitude
            if (!_config.EnableSyncDriftCorrection)
            {
                // Drift correction disabled
                await UpdatePlaybackStateAsync(message.PlaybackState, cancellationToken);
            }
            else if (CurrentDriftMs > 1000)
            {
                // Large drift (>1s): immediate seek
                Logger.Info($"Large drift detected: {CurrentDriftMs}ms, seeking to {TargetPosition.TotalSeconds:F1}s");
                _seekController.SeekTo(TargetPosition, _playbackController.TotalDuration);
                IsSynchronized = true;
            }
            else if (CurrentDriftMs > _config.MaxAcceptableDriftMs)
            {
                // Medium drift: speed adjustment
                var speedAdjust = drift > 0 ? 1.02 : 0.98;
                _playbackController.SetPlaybackSpeed(message.PlaybackSpeed * speedAdjust);
                
                Logger.Debug($"Medium drift: {CurrentDriftMs}ms, adjusting speed to {_playbackController.PlaybackSpeed:F2}x");
                
                DriftExceeded?.Invoke(this, CurrentDriftMs);
                IsSynchronized = false;
            }
            else if (CurrentDriftMs > 100)
            {
                // Small drift: minor speed adjustment
                var speedAdjust = drift > 0 ? 1.01 : 0.99;
                _playbackController.SetPlaybackSpeed(message.PlaybackSpeed * speedAdjust);
                
                IsSynchronized = true;
            }
            else
            {
                // Negligible drift: restore normal speed
                if (Math.Abs(_playbackController.PlaybackSpeed - message.PlaybackSpeed) > 0.01)
                {
                    _playbackController.SetPlaybackSpeed(message.PlaybackSpeed);
                }
                
                IsSynchronized = true;
            }
            
            // Update playback state
            await UpdatePlaybackStateAsync(message.PlaybackState, cancellationToken);
            
            // Update sync quality
            UpdateSyncQuality();
            
            // Track sync statistics
            _syncUpdateCount++;
            _lastSyncTime = DateTime.UtcNow;
            
            if (_config.EnableDebugLogging && _syncUpdateCount % 100 == 0)
            {
                Logger.Debug($"Sync stats: {_syncUpdateCount} updates, drift: {CurrentDriftMs}ms, quality: {SyncQuality:F1}%, speed: {_playbackController.PlaybackSpeed:F2}x");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling time update");
            IsSynchronized = false;
        }
    }
    
    /// <summary>
    /// Handles playback command from Tacview (play/pause/stop)
    /// </summary>
    public async Task HandlePlaybackCommandAsync(PlaybackCommandMessage message, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            Logger.Warn("Sync service not initialized");
            return;
        }
        
        try
        {
            Logger.Info($"Playback command received: {message.Command}");
            
            switch (message.Command?.ToLowerInvariant())
            {
                case "play":
                    if (!_playbackController.IsPlaying)
                    {
                        _playbackController.Resume();
                    }
                    break;
                
                case "pause":
                    if (_playbackController.IsPlaying)
                    {
                        _playbackController.Pause();
                    }
                    break;
                
                case "stop":
                    _playbackController.Stop();
                    break;
                
                default:
                    Logger.Warn($"Unknown playback command: {message.Command}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling playback command");
        }
    }
    
    /// <summary>
    /// Handles seek message from Tacview
    /// </summary>
    public async Task HandleSeekAsync(SeekMessage message, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            Logger.Warn("Sync service not initialized");
            return;
        }
        
        try
        {
            if (!DateTime.TryParse(message.TargetTimeUtc, null, DateTimeStyles.RoundtripKind, out var targetTime))
            {
                Logger.Error($"Failed to parse target time: {message.TargetTimeUtc}");
                return;
            }
            
            var targetOffset = targetTime - _recordingStartUtc;
            
            Logger.Info($"Seek requested to: {targetOffset.TotalSeconds:F1}s");
            
            _seekController.SeekTo(targetOffset, _playbackController.TotalDuration);
            
            // Clear scrubbing state after seek completes
            _scrubbingManager.ProcessSeek(0, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling seek");
        }
    }
    
    /// <summary>
    /// Updates playback state based on Tacview state
    /// </summary>
    private async Task UpdatePlaybackStateAsync(string playbackState, CancellationToken cancellationToken)
    {
        var shouldPlay = playbackState?.ToLowerInvariant() == "playing";
        
        // Fire PlaybackStateChanged event if state changed
        if (shouldPlay != _isPlaying)
        {
            _isPlaying = shouldPlay;
            PlaybackStateChanged?.Invoke(this, _isPlaying);
        }
        
        if (shouldPlay && !_playbackController.IsPlaying)
        {
            _playbackController.Resume();
        }
        else if (!shouldPlay && _playbackController.IsPlaying)
        {
            _playbackController.Pause();
        }
    }
    
    /// <summary>
    /// Updates sync quality metric based on current drift
    /// </summary>
    private void UpdateSyncQuality()
    {
        // Calculate quality: 100% at 0ms drift, 0% at 2000ms drift
        var previousQuality = SyncQuality;
        
        if (CurrentDriftMs == 0)
        {
            SyncQuality = 100.0;
        }
        else if (CurrentDriftMs >= 2000)
        {
            SyncQuality = 0.0;
        }
        else
        {
            // Linear interpolation
            SyncQuality = 100.0 * (1.0 - (CurrentDriftMs / 2000.0));
        }
        
        // Fire event if quality changed significantly (>5%)
        if (Math.Abs(SyncQuality - previousQuality) > 5.0)
        {
            SyncQualityChanged?.Invoke(this, SyncQuality);
        }
    }
    
    /// <summary>
    /// Gets sync health statistics
    /// </summary>
    public SyncQuality GetSyncHealth()
    {
        return new SyncQuality
        {
            QualityPercent = SyncQuality,
            DriftMs = CurrentDriftMs,
            IsSynchronized = IsSynchronized,
            UpdateCount = _syncUpdateCount,
            LastSyncTime = _lastSyncTime,
            TargetPosition = TargetPosition,
            CurrentPosition = _playbackController.CurrentPosition
        };
    }
    
    /// <summary>
    /// Resets sync state
    /// </summary>
    public void Reset()
    {
        IsSynchronized = false;
        SyncQuality = 100.0;
        CurrentDriftMs = 0;
        TargetPosition = TimeSpan.Zero;
        _syncUpdateCount = 0;
        _lastSyncTime = DateTime.MinValue;
        
        Logger.Info("Sync service reset");
    }
}
