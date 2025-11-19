using System;
using System.Windows.Threading;
using AeroDebrief.Core.Playback;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Synchronizes chart playhead with audio playback engine.
    /// Phase 6: Updates at 30-60 Hz during playback, handles seeks and rate changes.
    /// 
    /// PRODUCTION MODE: Must be connected to PlaybackController via Connect() for real audio sync.
    /// TEST MODE: Can use SetPlaybackState()/SetPlaybackRate() for simulation without PlaybackController.
    /// 
    /// Timer updates only occur when PlaybackController is connected and IsPlaying is true.
    /// Without a connection, the playhead will not move during playback.
    /// </summary>
    public class PlayheadSyncService : IPlayheadSyncService, IDisposable
    {
        private PlaybackController? _playbackController;
        private DispatcherTimer? _updateTimer;
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private DateTime _currentTime;
        private double _playbackRate = 1.0;
        private bool _isPlaying;
        private DateTime _startTime;
        private DateTime _endTime;
        private bool _disposed;

        public DateTime CurrentTime => _currentTime;
        public double PlaybackRate => _playbackRate;
        public bool IsPlaying => _isPlaying;
        public DateTime StartTime => _startTime;
        public DateTime EndTime => _endTime;

        public event EventHandler<DateTime>? TimeChanged;
        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<double>? PlaybackRateChanged;

        public PlayheadSyncService()
        {
            _logger.Info("PlayheadSyncService initialized");
            
            // Create timer for periodic updates (30 Hz = ~33ms)
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _updateTimer.Tick += OnUpdateTick;
            
            // Initialize times to sensible defaults
            _currentTime = DateTime.Now;
            _startTime = DateTime.Now;
            _endTime = DateTime.Now.AddHours(1);
        }

        public void Connect(PlaybackController playbackController)
        {
            if (_playbackController != null)
            {
                Disconnect();
            }

            _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));

            // Phase 6 Step 3B: Subscribe to actual PlaybackController events
            _playbackController.PlaybackStarted += OnPlaybackStarted;
            _playbackController.PlaybackStopped += OnPlaybackStopped;
            _playbackController.PlaybackPaused += OnPlaybackPaused;
            _playbackController.PlaybackResumed += OnPlaybackResumed;
            _playbackController.PlaybackSpeedChanged += OnPlaybackSpeedChanged;
            _playbackController.TimeChanged += OnTimeChanged;

            _logger.Info("Connected to PlaybackController with real events");
        }

        public void Disconnect()
        {
            if (_playbackController != null)
            {
                // Phase 6 Step 3B: Unsubscribe from actual events
                _playbackController.PlaybackStarted -= OnPlaybackStarted;
                _playbackController.PlaybackStopped -= OnPlaybackStopped;
                _playbackController.PlaybackPaused -= OnPlaybackPaused;
                _playbackController.PlaybackResumed -= OnPlaybackResumed;
                _playbackController.PlaybackSpeedChanged -= OnPlaybackSpeedChanged;
                _playbackController.TimeChanged -= OnTimeChanged;

                _playbackController = null;
                _logger.Info("Disconnected from PlaybackController");
            }

            StopUpdates();
        }

        public void StartUpdates()
        {
            if (_updateTimer != null && !_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
                _logger.Debug("Playhead updates started (30 Hz)");
            }
        }

        public void StopUpdates()
        {
            if (_updateTimer != null && _updateTimer.IsEnabled)
            {
                _updateTimer.Stop();
                _logger.Debug("Playhead updates stopped");
            }
        }

        public void Seek(DateTime time)
        {
            // Clamp to valid range
            if (time < _startTime)
                time = _startTime;
            if (time > _endTime)
                time = _endTime;

            _currentTime = time;

            // Phase 6 Step 3B: Command PlaybackController to seek if connected
            if (_playbackController != null)
            {
                try
                {
                    // Convert DateTime to TimeSpan offset from recording start
                    var offset = time - _playbackController.RecordingStart;
                    
                    // TODO: PlaybackController needs a Seek method
                    // _playbackController.Seek(offset);
                    
                    _logger.Debug($"Seek commanded to PlaybackController: {time:HH:mm:ss.fff} (offset: {offset})");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error commanding PlaybackController to seek");
                }
            }

            // Raise event for chart update
            TimeChanged?.Invoke(this, _currentTime);

            _logger.Debug($"Seek requested: {time:HH:mm:ss.fff}");
        }

        public void SeekRelative(TimeSpan offset)
        {
            var newTime = _currentTime + offset;
            Seek(newTime);
        }

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            if (!_isPlaying || _playbackController == null)
                return;

            try
            {
                // Phase 6 Step 3B: Get actual time from PlaybackController
                var currentPosition = _playbackController.CurrentPosition;
                var recordingStart = _playbackController.RecordingStart;
                var newTime = recordingStart + currentPosition;

                // Clamp to valid range
                if (newTime > _endTime)
                {
                    newTime = _endTime;
                    // Don't stop here - PlaybackController will handle it
                }

                if (newTime != _currentTime)
                {
                    _currentTime = newTime;
                    TimeChanged?.Invoke(this, _currentTime);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating playhead time from PlaybackController");
            }
        }

        private void UpdatePlaybackState(bool isPlaying)
        {
            if (_isPlaying != isPlaying)
            {
                _isPlaying = isPlaying;
                PlaybackStateChanged?.Invoke(this, _isPlaying);

                if (_isPlaying)
                {
                    StartUpdates();
                    _logger.Debug("Playback started");
                }
                else
                {
                    StopUpdates();
                    _logger.Debug("Playback stopped");
                }
            }
        }

        private void UpdatePlaybackRate(double rate)
        {
            if (Math.Abs(_playbackRate - rate) > 0.01)
            {
                _playbackRate = rate;
                PlaybackRateChanged?.Invoke(this, _playbackRate);
                _logger.Debug($"Playback rate changed: {rate}x");
            }
        }

        /// <summary>
        /// Set recording time range.
        /// Called when recording is loaded.
        /// </summary>
        public void SetTimeRange(DateTime start, DateTime end)
        {
            _startTime = start;
            _endTime = end;
            _currentTime = start;

            _logger.Info($"Time range set: {start:HH:mm:ss} to {end:HH:mm:ss} (duration: {end - start})");
        }

        /// <summary>
        /// Manually trigger playback state change.
        /// TEST MODE ONLY: For unit tests without PlaybackController.
        /// In production, state changes come from PlaybackController events.
        /// </summary>
        public void SetPlaybackState(bool isPlaying)
        {
            UpdatePlaybackState(isPlaying);
        }

        /// <summary>
        /// Manually trigger playback rate change.
        /// TEST MODE ONLY: For unit tests without PlaybackController.
        /// In production, rate changes come from PlaybackController events.
        /// </summary>
        public void SetPlaybackRate(double rate)
        {
            UpdatePlaybackRate(rate);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Disconnect();

            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Tick -= OnUpdateTick;
                _updateTimer = null;
            }

            _disposed = true;
            _logger.Info("PlayheadSyncService disposed");
        }

        // Phase 6 Step 3B: Handle real PlaybackController events
        private void OnPlaybackStarted()
        {
            UpdatePlaybackState(true);
            _logger.Debug("PlaybackController started");
        }

        private void OnPlaybackStopped()
        {
            UpdatePlaybackState(false);
            _logger.Debug("PlaybackController stopped");
        }

        private void OnPlaybackPaused()
        {
            UpdatePlaybackState(false);
            _logger.Debug("PlaybackController paused");
        }

        private void OnPlaybackResumed()
        {
            UpdatePlaybackState(true);
            _logger.Debug("PlaybackController resumed");
        }

        private void OnPlaybackSpeedChanged(double speed)
        {
            UpdatePlaybackRate(speed);
            _logger.Debug($"PlaybackController speed changed: {speed}x");
        }

        private void OnTimeChanged(TimeSpan currentTime, TimeSpan totalDuration)
        {
            try
            {
                // Convert TimeSpan to DateTime using recording start
                var newTime = _playbackController?.RecordingStart + currentTime ?? _startTime;

                // Clamp to valid range
                if (newTime < _startTime)
                    newTime = _startTime;
                if (newTime > _endTime)
                    newTime = _endTime;

                if (newTime != _currentTime)
                {
                    _currentTime = newTime;
                    TimeChanged?.Invoke(this, _currentTime);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling TimeChanged from PlaybackController");
            }
        }
    }
}
