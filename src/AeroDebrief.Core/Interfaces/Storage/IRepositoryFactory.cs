using System;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Factory contract for creating repository instances.
    /// Enables dependency injection and storage technology abstraction.
    /// 
    /// Allows switching between storage implementations:
    /// - SqliteRepositoryFactory (current)
    /// - Future: PostgreSQL, MongoDB, cloud storage, etc.
    /// 
    /// Used by:
    /// - RecordingFileLoader for opening recordings
    /// - AudioPacketRecorder for creating new recordings
    /// - Test fixtures for mock implementations
    /// </summary>
    public interface IRepositoryFactory
    {
        /// <summary>
        /// Create a new recording with all repositories.
        /// Initializes schema and prepares for write operations.
        /// </summary>
        IUnitOfWork CreateRecording(string filePath, RecordingMetadata metadata);

        /// <summary>
        /// Open an existing recording with all repositories.
        /// Connects to existing database for read/write operations.
        /// </summary>
        IUnitOfWork OpenRecording(string filePath);
    }

    /// <summary>
    /// Unit of Work contract - groups all repositories for a single recording.
    /// Ensures transactional consistency and manages connection lifecycle.
    /// 
    /// Design pattern benefits:
    /// - Single connection for all operations (performance)
    /// - Transaction support for atomicity
    /// - Clear lifecycle management (open/close)
    /// - Coordinates multiple repositories
    /// 
    /// Used by:
    /// - DatabasePacketSource for streaming packets
    /// - AudioPacketRecorder for saving packets
    /// - UI services for reading recording data
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Packet repository - audio packet storage and retrieval
        /// </summary>
        IPacketRepository Packets { get; }

        /// <summary>
        /// Frequency repository - frequency statistics and analysis
        /// </summary>
        IFrequencyRepository Frequencies { get; }

        /// <summary>
        /// Player repository - player statistics and analysis
        /// </summary>
        IPlayerRepository Players { get; }

        /// <summary>
        /// Recording metadata repository - overall recording information
        /// </summary>
        IRecordingRepository Recording { get; }

        /// <summary>
        /// Amplitude repository - pre-computed amplitude cache for waveform rendering (Phase 7)
        /// </summary>
        IAmplitudeRepository Amplitudes { get; }

        /// <summary>
        /// Initialize the unit of work (open connection, configure, create schema).
        /// Call after creation but before using repositories.
        /// </summary>
        System.Threading.Tasks.Task InitializeAsync(
            RecordingMetadata? metadata = null, 
            System.Threading.CancellationToken ct = default);

        /// <summary>
        /// Begin a transaction for multiple operations.
        /// All repository operations will be part of this transaction.
        /// </summary>
        void BeginTransaction();

        /// <summary>
        /// Commit all pending changes in the current transaction.
        /// </summary>
        System.Threading.Tasks.Task CommitAsync(System.Threading.CancellationToken ct = default);

        /// <summary>
        /// Rollback all pending changes in the current transaction.
        /// </summary>
        System.Threading.Tasks.Task RollbackAsync(System.Threading.CancellationToken ct = default);
    }
}
