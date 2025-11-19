namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Common interface for waveform generators (CPU and GPU)
    /// Allows seamless switching between implementations
    /// </summary>
    public interface IWaveformGenerator : IDisposable
    {
        /// <summary>
        /// Gets or sets the time resolution for waveform generation
        /// </summary>
        TimeSpan Resolution { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of data points in the waveform
        /// </summary>
        int MaxDataPoints { get; set; }

        /// <summary>
        /// Gets the waveform channels
        /// </summary>
        IReadOnlyDictionary<double, WaveformChannel> Channels { get; }

        /// <summary>
        /// Event fired when waveform data is updated
        /// </summary>
        event EventHandler<WaveformUpdatedEventArgs>? WaveformUpdated;

        /// <summary>
        /// Generates waveform data from audio packets with frequency filtering
        /// </summary>
        Task<WaveformData> GenerateWaveformAsync(
            string filePath,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates waveform data from pre-filtered packets
        /// </summary>
        Task<WaveformData> GenerateWaveformFromPacketsAsync(
            List<AudioPacketMetadata> packets,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates waveform from IPacketSource (memory-mapped, efficient)
        /// This is 20-30x faster and uses 10x less RAM than loading all packets
        /// Supports both FilePacketSource (.adb) and DuckDBPacketSource (.cvr/.duckdb)
        /// </summary>
        Task<WaveformData> GenerateWaveformFromSourceAsync(
            IO.IPacketSource source,
            TimeSpan from,
            TimeSpan to,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets combined waveform data for all selected frequencies
        /// </summary>
        float[] GetCombinedWaveform();

        /// <summary>
        /// Gets waveform data for a specific frequency channel
        /// </summary>
        float[]? GetChannelWaveform(double frequency);

        /// <summary>
        /// Clears all waveform data
        /// </summary>
        void Clear();
    }
}
