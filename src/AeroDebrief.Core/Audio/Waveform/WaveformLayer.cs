using System;
using Vortice.Direct3D11;

namespace AeroDebrief.Core.Audio.Waveform
{
    /// <summary>
    /// Represents a single frequency waveform layer with independent GPU texture.
    /// Each layer can be shown/hidden without affecting other layers.
    /// </summary>
    public sealed class WaveformLayer : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Unique identifier for this layer
        /// </summary>
        public Guid LayerId { get; } = Guid.NewGuid();

        /// <summary>
        /// Frequency in Hz (e.g., 251000000 for 251.0 MHz)
        /// </summary>
        public double FrequencyHz { get; set; }

        /// <summary>
        /// Display name for UI (e.g., "251.000 MHz - UHF Guard")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Color for waveform rendering (ARGB format)
        /// </summary>
        public uint WaveformColor { get; set; }

        /// <summary>
        /// Whether this layer is currently visible in the composite view
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Last time this layer was updated (for cache invalidation)
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// GPU texture containing waveform data at current resolution
        /// Format: R32_Float (single-channel amplitude values 0.0-1.0)
        /// </summary>
        public ID3D11Texture1D? GpuTexture { get; set; }

        /// <summary>
        /// Shader resource view for GPU texture (for reading in compositor)
        /// </summary>
        public ID3D11ShaderResourceView? GpuTextureSRV { get; set; }

        /// <summary>
        /// CPU-side cache of waveform data for zoom level recalculation
        /// Stored at highest resolution (1 second per pixel)
        /// </summary>
        public float[]? CachedWaveformData { get; set; }

        /// <summary>
        /// Current resolution level of the GPU texture
        /// </summary>
        public WaveformResolution CurrentResolution { get; set; } = WaveformResolution.Base;

        /// <summary>
        /// Visual effect applied to this layer (Phase 3 feature)
        /// </summary>
        public WaveformEffect EffectType { get; set; } = WaveformEffect.None;

        /// <summary>
        /// Effect intensity (0-1 range)
        /// </summary>
        public float EffectIntensity { get; set; } = 1.0f;

        /// <summary>
        /// Total number of data points in the cached waveform
        /// </summary>
        public int DataPointCount => CachedWaveformData?.Length ?? 0;

        /// <summary>
        /// Whether this layer has valid data
        /// </summary>
        public bool HasData => GpuTexture != null && CachedWaveformData != null;

        /// <summary>
        /// Size of GPU texture in bytes (for memory tracking)
        /// </summary>
        public long GpuMemoryBytes
        {
            get
            {
                if (GpuTexture == null) return 0;
                return DataPointCount * sizeof(float); // R32_Float = 4 bytes per pixel
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            GpuTextureSRV?.Dispose();
            GpuTexture?.Dispose();
            CachedWaveformData = null;

            _disposed = true;
        }

        public override string ToString()
        {
            return $"WaveformLayer[{FrequencyHz:F1} Hz, {DisplayName}, Visible={IsVisible}, Points={DataPointCount}]";
        }
    }

    /// <summary>
    /// Visual effects that can be applied to waveform layers (Phase 3)
    /// </summary>
    public enum WaveformEffect
    {
        /// <summary>
        /// No effect - standard rendering
        /// </summary>
        None = 0,

        /// <summary>
        /// Soft glow effect - increases alpha near waveform center
        /// </summary>
        Glow = 1,

        /// <summary>
        /// Highlight effect - brightens waveform color
        /// </summary>
        Highlight = 2,

        /// <summary>
        /// Pulse effect - animated pulsing based on time
        /// </summary>
        Pulse = 3
    }

    /// <summary>
    /// Waveform resolution levels for adaptive LOD
    /// </summary>
    public enum WaveformResolution
    {
        /// <summary>
        /// Base resolution: 5 seconds per pixel (entire file overview)
        /// </summary>
        Base = 0,

        /// <summary>
        /// Medium resolution: 2 seconds per pixel (moderate zoom)
        /// </summary>
        Medium = 1,

        /// <summary>
        /// High resolution: 1 second per pixel (maximum zoom)
        /// </summary>
        High = 2
    }

    /// <summary>
    /// Helper extensions for WaveformResolution
    /// </summary>
    public static class WaveformResolutionExtensions
    {
        /// <summary>
        /// Gets the time span per pixel for this resolution level
        /// </summary>
        public static TimeSpan GetTimePerPixel(this WaveformResolution resolution)
        {
            return resolution switch
            {
                WaveformResolution.Base => TimeSpan.FromSeconds(5),
                WaveformResolution.Medium => TimeSpan.FromSeconds(2),
                WaveformResolution.High => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Calculates required resolution based on visible time range and pixel width
        /// </summary>
        public static WaveformResolution CalculateRequiredResolution(TimeSpan visibleTimeRange, int pixelWidth)
        {
            if (pixelWidth <= 0) return WaveformResolution.Base;

            var secondsPerPixel = visibleTimeRange.TotalSeconds / pixelWidth;

            if (secondsPerPixel <= 1.5) return WaveformResolution.High;   // <1.5s/px -> High (1s/px)
            if (secondsPerPixel <= 3.5) return WaveformResolution.Medium; // <3.5s/px -> Medium (2s/px)
            return WaveformResolution.Base;                                // >=3.5s/px -> Base (5s/px)
        }

        /// <summary>
        /// Gets the number of pixels required for a given duration at this resolution
        /// </summary>
        public static int GetPixelCount(this WaveformResolution resolution, TimeSpan duration)
        {
            var timePerPixel = resolution.GetTimePerPixel();
            return (int)Math.Ceiling(duration.TotalSeconds / timePerPixel.TotalSeconds);
        }
    }
}
