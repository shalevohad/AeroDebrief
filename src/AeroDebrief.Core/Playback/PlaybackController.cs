using NLog;

namespace AeroDebrief.Core.Playback
{
    /// <summary>Handles playback control and timing</summary>
    public sealed class PlaybackController : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public event Action? PlaybackStarted;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused;
        public event Action? PlaybackResumed;
        public event Action<Exception>? PlaybackError;
        public event Action<double>? ProgressChanged;
        public event Action<TimeSpan, TimeSpan>? TimeChanged;
        public event Action<AudioPacketMetadata>? PacketStarted;
        
        /// <summary>
        /// Event fired when playback speed changes (actual speed after clamping)
        /// </summary>
        public event Action<double>? PlaybackSpeedChanged;
        
        /// <summary>
        /// Event fired when speed clamping state changes (for UI warnings)
        /// </summary>
        public event Action<bool, double, double>? SpeedClampedChanged; // (isClamped, requestedSpeed, actualSpeed)
        
        /// <summary>
        /// Event fired when audio should be muted due to extreme playback speed (< 0.25x or > 4.0x)
        /// </summary>
        public event Action<bool>? ShouldMuteForExtremeSpeed;

        private CancellationTokenSource? _cts;
        private Task? _playbackTask;
        private bool _isPlaybackActive;
        private bool _isPaused;
        private bool _isStopping;
        private TaskCompletionSource<bool>? _pauseTask;
        private readonly object _lock = new object();
        
        private double _playbackSpeed = 1.0; // NEW: Playback speed control (1.0 = normal, 0.5 = half speed, 2.0 = double speed)
        private double _requestedPlaybackSpeed = 1.0; // NEW: Track what Tacview actually requested
        
        // NEW: External time source support
        private IExternalTimeSource? _externalTimeSource;
        private bool _isExternalSyncEnabled;
        
        public TimeSpan TotalDuration { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public DateTime RecordingStart { get; private set; }
        public bool IsPlaying => _isPlaybackActive && !_isPaused && !_isStopping;
        public bool IsPaused => _isPaused;
        
        public double PlaybackSpeed // Current playback speed (clamped)
        {
            get => _playbackSpeed;
            private set
            {
                if (Math.Abs(_playbackSpeed - value) > 0.001)
                {
                    _playbackSpeed = value;
                    Logger.Info($"Playback speed changed to {_playbackSpeed:F2}x");
                    try
                    {
                        PlaybackSpeedChanged?.Invoke(_playbackSpeed);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error invoking PlaybackSpeedChanged event");
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the originally requested playback speed (before clamping)
        /// </summary>
        public double RequestedPlaybackSpeed => _requestedPlaybackSpeed;
        
        /// <summary>
        /// Gets whether the current playback speed is clamped (limited to supported range)
        /// </summary>
        public bool IsSpeedClamped => Math.Abs(_requestedPlaybackSpeed - _playbackSpeed) > 0.001;
        
        /// <summary>
        /// Gets the reason for speed clamping, or null if not clamped
        /// </summary>
        public string? SpeedClampReason
        {
            get
            {
                if (!IsSpeedClamped)
                    return null;
                
                if (_requestedPlaybackSpeed < Constants.MIN_PLAYBACK_SPEED)
                    return $"Requested speed {_requestedPlaybackSpeed:F2}x is too slow. " +
                           $"Minimum supported speed is {Constants.MIN_PLAYBACK_SPEED}x.";
                
                if (_requestedPlaybackSpeed > Constants.MAX_PLAYBACK_SPEED)
                    return $"Requested speed {_requestedPlaybackSpeed:F2}x is too fast. " +
                           $"Maximum supported speed is {Constants.MAX_PLAYBACK_SPEED}x.";
                
                return null;
            }
        }
        
        /// <summary>
        /// Gets whether external sync is currently active
        /// </summary>
        public bool IsExternalSyncEnabled => _isExternalSyncEnabled && _externalTimeSource != null;

        /// <summary>
        /// Sets an external time source for synchronization (e.g., Tacview)
        /// </summary>
        /// <param name="source">External time source, or null to disable</param>
        public void SetExternalTimeSource(IExternalTimeSource? source)
        {
            lock (_lock)
            {
                // Unsubscribe from old source
                if (_externalTimeSource != null)
                {
                    _externalTimeSource.TimeChanged -= OnExternalTimeChanged;
                    _externalTimeSource.PlaybackStateChanged -= OnExternalPlaybackStateChanged;
                    _externalTimeSource.PlaybackSpeedChanged -= OnExternalPlaybackSpeedChanged;
                    Logger.Info("External time source disconnected");
                }
                
                _externalTimeSource = source;
                _isExternalSyncEnabled = source != null;
                
                // Subscribe to new source
                if (_externalTimeSource != null)
                {
                    _externalTimeSource.TimeChanged += OnExternalTimeChanged;
                    _externalTimeSource.PlaybackStateChanged += OnExternalPlaybackStateChanged;
                    _externalTimeSource.PlaybackSpeedChanged += OnExternalPlaybackSpeedChanged;
                    Logger.Info("External time source connected");
                }
            }
        }
        
        /// <summary>
        /// Handles time changes from external source
        /// </summary>
        private void OnExternalTimeChanged(object? sender, TimeSpan targetTime)
        {
            if (!_isExternalSyncEnabled)
                return;
            
            // Calculate drift
            var drift = Math.Abs((targetTime - CurrentPosition).TotalMilliseconds);
            
            Logger.Debug($"External time change: target={targetTime}, current={CurrentPosition}, drift={drift:F0}ms");
            
            // NOTE: Actual sync logic is handled by TacviewSyncService
            // This event is primarily for logging and monitoring
        }
        
        /// <summary>
        /// Handles playback state changes from external source
        /// </summary>
        private void OnExternalPlaybackStateChanged(object? sender, bool isPlaying)
        {
            if (!_isExternalSyncEnabled)
                return;
            
            Logger.Debug($"External playback state change: {(isPlaying ? "playing" : "paused")}");
            
            // NOTE: Actual playback control is handled by TacviewSyncService
            // This event is primarily for logging and monitoring
        }
        
        /// <summary>
        /// Handles playback speed changes from external source
        /// </summary>
        private void OnExternalPlaybackSpeedChanged(object? sender, double speed)
        {
            if (!_isExternalSyncEnabled)
                return;
            
            Logger.Debug($"External playback speed change: {speed:F2}x");
            SetPlaybackSpeed(speed);
        }

        /// <summary>
        /// Sets the playback speed (will be clamped to 0.25x - 4.0x range)
        /// </summary>
        public void SetPlaybackSpeed(double speed)
        {
            // Store the originally requested speed
            _requestedPlaybackSpeed = speed;
            
            // Clamp to reasonable range using constants
            var clampedSpeed = Math.Clamp(speed, Constants.MIN_PLAYBACK_SPEED, Constants.MAX_PLAYBACK_SPEED);
            
            bool isClamped = Math.Abs(speed - clampedSpeed) > 0.001;
            
            lock (_lock)
            {
                PlaybackSpeed = clampedSpeed;
            }
            
            // Fire clamping event for UI warnings
            try
            {
                SpeedClampedChanged?.Invoke(isClamped, speed, clampedSpeed);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error invoking SpeedClampedChanged event");
            }
            
            if (isClamped)
            {
                Logger.Warn($"Playback speed {speed:F2}x clamped to {clampedSpeed:F2}x " +
                           $"(supported range: {Constants.MIN_PLAYBACK_SPEED}x-{Constants.MAX_PLAYBACK_SPEED}x)");
            }
            else
            {
                Logger.Info($"Playback speed set to {clampedSpeed:F2}x");
            }
        }

        /// <summary>
        /// Gets the current time scaling factor for audio timing calculations
        /// </summary>
        public double GetTimeScale()
        {
            return _playbackSpeed;
        }

        public void Start(string filePath, Func<CancellationToken, Task> playbackFunc)
        {
            lock (_lock)
            {
                // Stop any existing playback first
                if (_isPlaybackActive)
                {
                    _ = StopAsync();
                }

                Logger.Info($"Starting playback for: {filePath}");
                
                _cts = new CancellationTokenSource();
                _isPlaybackActive = true;
                _isPaused = false;
                _isStopping = false;
                _pauseTask = null;
                
                _playbackTask = Task.Run(async () =>
                {
                    try
                    {
                        Logger.Debug("Invoking PlaybackStarted event");
                        PlaybackStarted?.Invoke();
                        await playbackFunc(_cts.Token);
                        
                        Logger.Debug("Playback completed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info("Playback was cancelled");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Playback error occurred");
                        try
                        {
                            PlaybackError?.Invoke(ex);
                        }
                        catch (Exception eventEx)
                        {
                            Logger.Error(eventEx, "Error while invoking PlaybackError event");
                        }
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _isPlaybackActive = false;
                            _isPaused = false;
                            _isStopping = false;
                            Logger.Debug("Invoking PlaybackStopped event");
                        }
                        
                        try
                        {
                            PlaybackStopped?.Invoke();
                        }
                        catch (Exception eventEx)
                        {
                            Logger.Error(eventEx, "Error while invoking PlaybackStopped event");
                        }
                    }
                });
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (!_isPlaybackActive || _isPaused || _isStopping)
                {
                    Logger.Debug("Pause called but playback is not active, already paused, or stopping");
                    return;
                }

                Logger.Info("Pausing playback");
                _isPaused = true;
                _pauseTask = new TaskCompletionSource<bool>();
                
                try
                {
                    PlaybackPaused?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while invoking PlaybackPaused event");
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (!_isPlaybackActive || !_isPaused || _isStopping)
                {
                    Logger.Debug("Resume called but playback is not paused or is stopping");
                    return;
                }

                Logger.Info("Resuming playback");
                _isPaused = false;
                _pauseTask?.SetResult(true);
                _pauseTask = null;
                
                try
                {
                    PlaybackResumed?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while invoking PlaybackResumed event");
                }
            }
        }

        public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool>? currentPauseTask;
            
            lock (_lock)
            {
                if (!_isPaused || _isStopping)
                    return;
                
                currentPauseTask = _pauseTask;
            }

            if (currentPauseTask != null)
            {
                Logger.Debug("Waiting for resume signal");
                
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                try
                {
                    await currentPauseTask.Task.WaitAsync(combinedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    lock (_lock)
                    {
                        if (_isPaused && _pauseTask == currentPauseTask)
                        {
                            _pauseTask?.SetCanceled();
                            _pauseTask = null;
                        }
                    }
                    throw;
                }
            }
        }

        public void Stop()
        {
            _ = StopAsync();
        }

        public async Task StopAsync()
        {
            bool shouldStop = false;
            Task? taskToWait = null;
            
            lock (_lock)
            {
                if (!_isPlaybackActive || _isStopping)
                {
                    Logger.Debug("Stop called but playback is not active or already stopping");
                    return;
                }

                Logger.Info("Stopping playback");
                _isStopping = true;
                shouldStop = true;
                taskToWait = _playbackTask;
                
                if (_isPaused)
                {
                    _isPaused = false;
                    _pauseTask?.SetResult(true);
                    _pauseTask = null;
                }
                
                try
                {
                    _cts?.Cancel();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error cancelling playback token");
                }
            }
            
            if (shouldStop && taskToWait != null && !taskToWait.IsCompleted)
            {
                try
                {
                    Logger.Debug("Waiting for playback task to complete");
                    
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await taskToWait.WaitAsync(timeoutCts.Token);
                    
                    Logger.Debug("Playback task completed successfully");
                }
                catch (OperationCanceledException)
                {
                    Logger.Warn("Playback task did not complete within timeout");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while waiting for playback task to complete");
                }
            }
            
            CleanupResources();
        }

        private void CleanupResources()
        {
            lock (_lock)
            {
                try
                {
                    _cts?.Dispose();
                    _cts = null;
                    
                    _playbackTask = null;
                    
                    _pauseTask?.SetCanceled();
                    _pauseTask = null;
                    
                    _isPaused = false;
                    _isPlaybackActive = false;
                    _isStopping = false;
                    
                    Logger.Debug("Playback resources cleaned up");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error cleaning up playback resources");
                }
            }
        }

        public void SetTotalDuration(TimeSpan duration) => TotalDuration = duration;
        public void SetRecordingStart(DateTime start) => RecordingStart = start;
        public void UpdatePosition(TimeSpan position) => CurrentPosition = position;
        
        public void SetPosition(TimeSpan position)
        {
            CurrentPosition = position;
            try
            {
                var progress = TotalDuration.Ticks > 0 ? (double)CurrentPosition.Ticks / TotalDuration.Ticks * 100.0 : 0.0;
                var clampedProgress = Math.Clamp(progress, 0.0, 100.0);
                
                ProgressChanged?.Invoke(clampedProgress);
                TimeChanged?.Invoke(CurrentPosition, TotalDuration);
                
                Logger.Debug($"Position set to: {position} (progress: {clampedProgress:F1}%)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while invoking position change events during seek");
            }
        }
        
        public void NotifyPacketStarted(AudioPacketMetadata packet) => PacketStarted?.Invoke(packet);

        private double _lastLoggedProgress = -1;

        public void UpdateProgress()
        {
            if (TotalDuration.Ticks > 0)
            {
                var progress = (double)CurrentPosition.Ticks / TotalDuration.Ticks * 100.0;
                var clampedProgress = Math.Clamp(progress, 0.0, 100.0);
                
                try
                {
                    ProgressChanged?.Invoke(clampedProgress);
                    TimeChanged?.Invoke(CurrentPosition, TotalDuration);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while invoking progress change events");
                }
                
                if ((int)clampedProgress % 10 == 0 && clampedProgress != _lastLoggedProgress)
                {
                    Logger.Debug($"Playback progress: {clampedProgress:F1}% ({CurrentPosition}/{TotalDuration})");
                    _lastLoggedProgress = clampedProgress;
                }
            }
        }

        public void Dispose()
        {
            _ = StopAsync();
        }
    }
}