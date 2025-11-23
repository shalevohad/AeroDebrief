using System;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Repository contract for recording metadata and overall statistics.
    /// Abstracts underlying storage technology.
    /// 
    /// Responsibilities:
    /// - Store and retrieve recording metadata (version, server, start time)
    /// - Track recording statistics (packet count, duration, live status)
    /// - Mark recording finalization
    /// 
    /// Used by:
    /// - AudioPacketRecorder for metadata creation/updates
    /// - RecordingFileLoader for metadata retrieval
    /// - UI for displaying recording information
    /// </summary>
    public interface IRecordingRepository : IDisposable
    {
        /// <summary>
        /// Get recording metadata
        /// </summary>
        Task<RecordingMetadata> GetMetadataAsync(CancellationToken ct = default);

        /// <summary>
        /// Update recording metadata
        /// </summary>
        Task UpdateMetadataAsync(RecordingMetadata metadata, CancellationToken ct = default);

        /// <summary>
        /// Get current recording statistics
        /// </summary>
        Task<RecordingStats> GetStatsAsync(CancellationToken ct = default);

        /// <summary>
        /// Mark recording as finalized (no longer live)
        /// </summary>
        Task MarkFinalizedAsync(CancellationToken ct = default);
    }
}
