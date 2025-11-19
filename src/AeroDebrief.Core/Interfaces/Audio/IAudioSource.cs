using System.Threading;
using System.Threading.Tasks;

namespace AeroDebrief.Core.Interfaces.Audio
{
    /// <summary>
    /// Audio source contract for feeding audio into the processing pipeline.
    /// Enables testability by allowing injection of test audio sources (e.g., synthetic audio).
    /// Used primarily for unit testing UserWorker and FrequencyWorker without real packet sources.
    /// </summary>
    public interface IAudioSource
    {
        /// <summary>
        /// Gets the frequency associated with this audio source in Hz
        /// </summary>
        double Frequency { get; }

        /// <summary>
        /// Gets the sample rate of the audio data in samples per second
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// Reads the next audio packet from the source.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// Audio packet metadata, or null if no more audio is available.
        /// The AudioPayload should contain PCM16 or Opus-encoded audio data.
        /// </returns>
        Task<AudioPacketMetadata?> ReadNextPacketAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the audio source to the beginning (for testing loops)
        /// </summary>
        void Reset();

        /// <summary>
        /// Gets whether more audio data is available
        /// </summary>
        bool HasMoreData { get; }
    }
}
