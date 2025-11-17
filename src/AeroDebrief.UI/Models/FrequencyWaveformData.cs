using System;
using System.Windows.Media;

namespace AeroDebrief.UI.Models
{
    /// <summary>
    /// Data model for frequency-specific waveform visualization.
    /// Used by legacy components during Phase 12 transition.
    /// </summary>
    public class FrequencyWaveformData
    {
        /// <summary>
        /// Frequency in Hz
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Waveform amplitude data
        /// </summary>
        public float[] WaveformData { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Display color for this frequency
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Display name (e.g., "251.0 MHz - Red")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// GPU layer ID (if using GPU rendering)
        /// </summary>
        public Guid LayerId { get; set; }

        /// <summary>
        /// Whether this frequency is currently visible
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// GPU composite texture (legacy, for compatibility)
        /// </summary>
        public object? GpuCompositeTexture { get; set; }

        /// <summary>
        /// Whether this is a GPU-composited waveform (legacy, for compatibility)
        /// </summary>
        public bool IsGpuComposite { get; set; }
    }

    /// <summary>
    /// Event arguments for minimap click events.
    /// </summary>
    public class MiniMapClickEventArgs : EventArgs
    {
        /// <summary>
        /// Normalized position (0.0 to 1.0) where the click occurred
        /// </summary>
        public double NormalizedPosition { get; }

        /// <summary>
        /// Start of selected range (for range selection)
        /// </summary>
        public double StartPosition { get; }

        /// <summary>
        /// End of selected range (for range selection)
        /// </summary>
        public double EndPosition { get; }

        public MiniMapClickEventArgs(double normalizedPosition)
        {
            NormalizedPosition = normalizedPosition;
            StartPosition = normalizedPosition;
            EndPosition = normalizedPosition;
        }

        public MiniMapClickEventArgs(double startPosition, double endPosition)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
            NormalizedPosition = (startPosition + endPosition) / 2.0;
        }
    }

    /// <summary>
    /// Event arguments for minimap drag events.
    /// </summary>
    public class MiniMapDragEventArgs : EventArgs
    {
        /// <summary>
        /// Normalized start position of the drag
        /// </summary>
        public double StartPosition { get; }

        /// <summary>
        /// Normalized end position of the drag
        /// </summary>
        public double EndPosition { get; }

        public MiniMapDragEventArgs(double startPosition, double endPosition)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
    }
}
