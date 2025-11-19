using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.IO;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Packet source contract for providing audio packets for playback.
    /// Abstracts the underlying storage mechanism:
    /// - FilePacketSource: Memory-mapped .adb files
    /// - DuckDBPacketSource: DuckDB database (.cvr, .duckdb)
    /// 
    /// Enables unified playback pipeline regardless of source format.
    /// </summary>
    public interface IPacketSource : IDisposable
    {
        /// <summary>
        /// Gets the total number of packets available
        /// </summary>
        long TotalPackets { get; }

        /// <summary>
        /// Gets the total duration of the recording
        /// </summary>
        TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets the recording start time (timestamp of first packet)
        /// </summary>
        DateTime RecordingStart { get; }

        /// <summary>
        /// Opens the packet source and prepares it for reading
        /// </summary>
        Task OpenAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads packets in chronological order starting from a specific time
        /// </summary>
        IAsyncEnumerable<RadioPacket> ReadRange(
            TimeSpan from,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads packets in batches for improved performance
        /// </summary>
        IAsyncEnumerable<RadioPacket[]> ReadRangeBatched(
            TimeSpan from,
            int batchSize = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets frequency metadata without reading packets
        /// </summary>
        Dictionary<double, FrequencyMetadata> GetFrequencyMetadata();
    }
}
