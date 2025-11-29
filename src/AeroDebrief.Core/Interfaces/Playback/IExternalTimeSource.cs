using System;

namespace AeroDebrief.Core.Interfaces.Playback
{
    /// <summary>
    /// External time source contract for synchronizing playback with external applications.
    /// Primary use case: Tacview integration for synchronized mission replay.
    /// 
    /// Allows external applications to control:
    /// - Current playback time
    /// - Play/pause state
    /// - Playback speed
    /// 
    /// AeroDebrief audio playback follows the external time source.
    /// </summary>
    public interface IExternalTimeSource
    {
        /// <summary>
        /// Current time from external source
        /// </summary>
        TimeSpan CurrentTime { get; }
        
        /// <summary>
        /// Whether external source is playing
        /// </summary>
        bool IsPlaying { get; }
        
        /// <summary>
        /// Playback speed from external source (1.0 = normal)
        /// </summary>
        double PlaybackSpeed { get; }
        
        /// <summary>
        /// Fired when external time changes significantly
        /// </summary>
        event EventHandler<TimeSpan>? TimeChanged;
        
        /// <summary>
        /// Fired when external playback state changes
        /// </summary>
        event EventHandler<bool>? PlaybackStateChanged;
        
        /// <summary>
        /// Fired when external playback speed changes
        /// </summary>
        event EventHandler<double>? PlaybackSpeedChanged;
    }
}
