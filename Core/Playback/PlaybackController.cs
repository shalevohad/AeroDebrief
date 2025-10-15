using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Playback
{
    /// <summary>Handles playback control and timing</summary>
    public sealed class PlaybackController : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public event Action? PlaybackStarted;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused; // Added pause event
        public event Action? PlaybackResumed; // Added resume event
        public event Action<Exception>? PlaybackError;
        public event Action<double>? ProgressChanged;
        public event Action<TimeSpan, TimeSpan>? TimeChanged;
        public event Action<AudioPacketMetadata>? PacketStarted;

        private CancellationTokenSource? _cts;
        private Task? _playbackTask;
        private bool _isPlaybackActive;
        private bool _isPaused; // Added pause state
        private bool _isStopping; // Added to prevent multiple stop calls
        private TaskCompletionSource<bool>? _pauseTask; // Added for pause/resume signaling
        private readonly object _lock = new object();
        
        public TimeSpan TotalDuration { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public DateTime RecordingStart { get; private set; }
        public bool IsPlaying => _isPlaybackActive && !_isPaused && !_isStopping; // Updated to consider stopping state
        public bool IsPaused => _isPaused; // Added pause property

        public void Start(string filePath, Func<CancellationToken, Task> playbackFunc)
        {
            lock (_lock)
            {
                // Stop any existing playback first
                if (_isPlaybackActive)
                {
                    _ = StopAsync(); // Fire and forget the async stop
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
                
                // Create a combined cancellation token that respects both the original cancellation token
                // and the pause/resume mechanism
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                try
                {
                    await currentPauseTask.Task.WaitAsync(combinedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // If cancelled, ensure we're not stuck in paused state
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

        // Synchronous Stop method for backward compatibility (calls async version)
        public void Stop()
        {
            // Don't wait on UI thread - just fire and forget
            _ = StopAsync();
        }

        // Async version of Stop for proper resource cleanup
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
                
                // If paused, resume first to allow clean shutdown
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
            
            // Wait for the task to complete outside the lock to avoid deadlocks
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
                    Logger.Warn("Playback task did not complete within timeout - this may indicate a problem");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while waiting for playback task to complete");
                }
            }
            
            // Final cleanup
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
                    
                    // Don't dispose the task as it might still be running
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
        
        /// <summary>
        /// Immediately update position and notify listeners (used during seek operations)
        /// </summary>
        public void SetPosition(TimeSpan position)
        {
            CurrentPosition = position;
            // Immediately notify of the position change
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
                
                // Log progress occasionally
                if ((int)clampedProgress % 10 == 0 && clampedProgress != _lastLoggedProgress)
                {
                    Logger.Debug($"Playback progress: {clampedProgress:F1}% ({CurrentPosition}/{TotalDuration})");
                    _lastLoggedProgress = clampedProgress;
                }
            }
        }

        public void Dispose()
        {
            // Use async version but don't wait for it in Dispose to avoid blocking
            _ = StopAsync();
        }
    }
}