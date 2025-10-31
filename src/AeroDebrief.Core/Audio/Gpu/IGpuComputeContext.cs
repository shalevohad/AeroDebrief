namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Interface for GPU compute context for waveform generation
    /// </summary>
    public interface IGpuComputeContext : IDisposable
    {
        /// <summary>
        /// Gets whether GPU compute is available
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the device name
        /// </summary>
        string DeviceName { get; }

        /// <summary>
        /// Gets the compute capability description
        /// </summary>
        string ComputeCapability { get; }

        /// <summary>
        /// Gets the maximum number of threads supported
        /// </summary>
        int MaxThreads { get; }

        /// <summary>
        /// Computes waveform data from audio packet data
        /// </summary>
        /// <param name="packetData">Array of packet data with timestamps and amplitudes</param>
        /// <param name="startTimeTicks">Start time in ticks</param>
        /// <param name="timeStepTicks">Time step per waveform point in ticks</param>
        /// <param name="outputPoints">Number of output waveform points</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of waveform amplitude values</returns>
        Task<float[]> ComputeWaveformAsync(
            GpuPacketData[] packetData,
            long startTimeTicks,
            long timeStepTicks,
            int outputPoints,
            CancellationToken cancellationToken = default);
    }
}
