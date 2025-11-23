using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Repository contract for audio packet storage and retrieval.
    /// Abstracts underlying storage technology (SQLite, DuckDB, etc.).
    /// 
    /// Responsibilities:
    /// - Batch insert operations for recording
    /// - Stream packets with filtering (time, frequency, player, coalition)
    /// - Packet count queries
    /// - Schema initialization and finalization
    /// 
    /// Used by:
    /// - AudioPacketRecorder for saving packets during recording
    /// - IPacketSource implementations for playback
    /// - DatabasePacketSource for filtered streaming
    /// </summary>
    public interface IPacketRepository : IDisposable
    {
        /// <summary>
        /// Initialize the repository (create schema, configure, etc.)
        /// </summary>
        Task InitializeAsync(RecordingMetadata metadata, CancellationToken ct = default);

        /// <summary>
        /// Open an existing repository
        /// </summary>
        Task OpenAsync(CancellationToken ct = default);

        /// <summary>
        /// Insert a batch of packets (optimized bulk operation)
        /// </summary>
        Task InsertBatchAsync(IEnumerable<AudioPacketMetadata> packets, CancellationToken ct = default);

        /// <summary>
        /// Stream packets with optional filtering
        /// </summary>
        IAsyncEnumerable<RadioPacket> StreamAsync(
            TimeSpan fromTime,
            TimeSpan? toTime = null,
            IEnumerable<double>? frequencies = null,
            IEnumerable<string>? players = null,
            int? coalition = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get a single packet by ID
        /// </summary>
        Task<RadioPacket?> GetByIdAsync(long packetId, CancellationToken ct = default);

        /// <summary>
        /// Get total packet count
        /// </summary>
        Task<long> GetCountAsync(CancellationToken ct = default);

        /// <summary>
        /// Get packet count for a specific time range
        /// </summary>
        Task<long> GetCountAsync(TimeSpan fromTime, TimeSpan toTime, CancellationToken ct = default);

        /// <summary>
        /// Finalize the repository (optimize, build indexes, etc.)
        /// </summary>
        Task FinalizeAsync(CancellationToken ct = default);

        /// <summary>
        /// Get recording start time
        /// </summary>
        DateTime RecordingStart { get; }

        /// <summary>
        /// Check if repository is for live recording
        /// </summary>
        bool IsLiveRecording { get; }
    }
}
