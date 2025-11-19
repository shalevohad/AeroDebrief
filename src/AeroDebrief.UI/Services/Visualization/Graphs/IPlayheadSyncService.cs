using System;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Synchronizes chart playhead with audio playback engine.
    /// Phase 6: Enables visual playback position and seek integration.
    /// </summary>
    public interface IPlayheadSyncService
    {
        /// <summary>
        /// Current playback position synchronized with audio engine.
        /// </summary>
        DateTime CurrentTime { get; }

        /// <summary>
        /// Current playback rate (0.5x, 1x, 2x, etc.).
        /// Default is 1.0 (normal speed).
        /// </summary>
        double PlaybackRate { get; }

        /// <summary>
        /// Whether playback is currently active.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Recording start time (earliest packet timestamp).
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Recording end time (latest packet timestamp).
        /// </summary>
        DateTime EndTime { get; }

        /// <summary>
        /// Raised when playback time updates (30-60 Hz during playback).
        /// Provides the current playback position.
        /// </summary>
        event EventHandler<DateTime>? TimeChanged;

        /// <summary>
        /// Raised when playback state changes (play/pause/stop).
        /// Provides true if playing, false if paused/stopped.
        /// </summary>
        event EventHandler<bool>? PlaybackStateChanged;

        /// <summary>
        /// Raised when playback rate changes.
        /// Provides the new playback rate (0.5x, 1x, 2x, etc.).
        /// </summary>
        event EventHandler<double>? PlaybackRateChanged;

        /// <summary>
        /// Seek to specific time in recording.
        /// Clamps to valid range [StartTime, EndTime].
        /// </summary>
        /// <param name="time">Target time to seek to</param>
        void Seek(DateTime time);

        /// <summary>
        /// Seek by offset from current position.
        /// Positive offset moves forward, negative moves backward.
        /// </summary>
        /// <param name="offset">Time offset to apply</param>
        void SeekRelative(TimeSpan offset);

        /// <summary>
        /// Connect to audio playback engine.
        /// Subscribes to playback events and begins synchronization.
        /// </summary>
        /// <param name="sessionManager">Playback session manager instance</param>
        void Connect(Core.Playback.PlaybackController sessionManager);

        /// <summary>
        /// Disconnect from audio playback engine.
        /// Unsubscribes from events and stops synchronization.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Set recording time range.
        /// Called when recording is loaded to establish valid time bounds.
        /// </summary>
        /// <param name="start">Recording start time (earliest packet)</param>
        /// <param name="end">Recording end time (latest packet)</param>
        void SetTimeRange(DateTime start, DateTime end);

        /// <summary>
        /// Start or resume update timer.
        /// Called automatically when playback starts.
        /// </summary>
        void StartUpdates();

        /// <summary>
        /// Stop update timer.
        /// Called automatically when playback stops.
        /// </summary>
        void StopUpdates();
    }
}
