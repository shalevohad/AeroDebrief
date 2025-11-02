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
        public event Action<double>? PlaybackSpeedChanged; // NEW: Event for playback speed changes

        private CancellationTokenSource? _cts;
        private Task? _playbackTask;
        private bool _isPlaybackActive;
        private bool _isPaused;
        private bool _isStopping;
        private TaskCompletionSource<bool>? _pauseTask;
        private readonly object _lock = new object();
        
        private double _playbackSpeed = 1.0; // NEW: Playback speed control (1.0 = normal, 0.5 = half speed, 2.0 = double speed)
        
        public TimeSpan TotalDuration { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public DateTime RecordingStart { get; private set; }
        public bool IsPlaying => _isPlaybackActive && !_isPaused && !_isStopping;
        public bool IsPaused => _isPaused;
        
        public double PlaybackSpeed // NEW: Current playback speed
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
        /// Sets the playback speed (0.25x to 4.0x supported)
        /// </summary>
        public void SetPlaybackSpeed(double speed)
        {
            // Clamp to reasonable range: 0.25x (quarter speed) to 4.0x (quad speed)
            var clampedSpeed = Math.Clamp(speed, 0.25, 4.0);
            
            lock (_lock)
            {
                PlaybackSpeed = clampedSpeed;
            }
            
            Logger.Info($"Playback speed set to {clampedSpeed:F2}x");
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