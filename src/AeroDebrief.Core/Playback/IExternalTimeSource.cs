using System;

namespace AeroDebrief.Core.Playback
{
    /// <summary>
    /// Interface for external time sources (e.g., Tacview) that can control playback timing
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
