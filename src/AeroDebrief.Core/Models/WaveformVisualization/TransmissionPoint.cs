using System;

namespace AeroDebrief.Core.Models.WaveformVisualization
{
    /// <summary>
    /// Represents a single transmission point in the waveform visualization.
    /// Optimized for LiveCharts2 performance with minimal allocations.
    /// </summary>
    public sealed class TransmissionPoint
    {
        /// <summary>
        /// Gets or sets the time offset from the recording start (in seconds).
        /// Used as X-axis value for charting.
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the RMS (Root Mean Square) amplitude value.
        /// Used as Y-axis value for charting. Range: 0.0 to 1.0 (normalized).
        /// </summary>
        public double RmsValue { get; set; }

        /// <summary>
        /// Gets or sets the absolute timestamp of this transmission.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the frequency this transmission occurred on (in Hz).
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Gets or sets the pilot identifier (GUID or name).
        /// </summary>
        public string PilotId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the pilot display name.
        /// </summary>
        public string PilotName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this transmission is part of a collision
        /// (multiple pilots transmitting simultaneously on the same frequency).
        /// </summary>
        public bool IsCollision { get; set; }

        /// <summary>
        /// Gets or sets the coalition (1 = Red, 2 = Blue, 0 = Neutral).
        /// </summary>
        public int Coalition { get; set; }
    }
}
