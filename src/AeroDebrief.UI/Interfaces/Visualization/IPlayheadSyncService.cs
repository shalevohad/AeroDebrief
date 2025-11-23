using System;

namespace AeroDebrief.UI.Interfaces.Visualization
{
    /// <summary>
    /// Playhead synchronization contract for coordinating chart visualization with audio playback.
    /// Phase 6: Enables visual feedback of playback position and user seeking.
    /// 
    /// Responsibilities:
    /// - Synchronize chart playhead with audio playback engine
    /// - Handle user seek gestures from chart
    /// - Manage playback rate display
    /// - Coordinate play/pause state
    /// 
    /// Event-driven design allows:
    /// - Chart to update playhead in real-time (30-60 Hz)
    /// - User clicks on chart to seek audio
    /// - Playback controls to affect chart display
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
        /// Chart updates playhead position in response.
        /// </summary>
        event EventHandler<DateTime>? TimeChanged;

        /// <summary>
        /// Raised when playback state changes (play/pause/stop).
        /// Chart can show play indicator or pause state.
        /// </summary>
        event EventHandler<bool>? PlaybackStateChanged;

        /// <summary>
        /// Raised when playback rate changes (speed adjustment).
        /// Chart can display current speed (0.5x, 2x, etc.).
        /// </summary>
        event EventHandler<double>? PlaybackRateChanged;

        /// <summary>
        /// Seek to specific time in recording.
        /// Called when user clicks on chart to jump to position.
        /// Clamps to valid range [StartTime, EndTime].
        /// </summary>
        void Seek(DateTime time);
        
        /// <summary>
        /// Set recording time range.
        /// Called when recording is loaded to initialize time boundaries.
        /// </summary>
        void SetTimeRange(DateTime start, DateTime end);
    }
}
