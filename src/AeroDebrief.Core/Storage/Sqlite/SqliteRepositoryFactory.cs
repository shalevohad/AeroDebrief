using System;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// Factory for creating SQLite-based repository instances.
    /// Handles connection lifecycle and repository initialization.
    /// </summary>
    public class SqliteRepositoryFactory : IRepositoryFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new recording with SQLite storage
        /// </summary>
        public IUnitOfWork CreateRecording(string filePath, RecordingMetadata metadata)
        {
            Logger.Info($"Creating new SQLite recording: {filePath}");

            var unitOfWork = new SqliteUnitOfWork(filePath, createNew: true);
            
            // Initialize synchronously using GetAwaiter().GetResult() with ConfigureAwait(false)
            // This avoids deadlocks by not capturing the synchronization context
            unitOfWork.InitializeAsync(metadata).ConfigureAwait(false).GetAwaiter().GetResult();

            return unitOfWork;
        }

        /// <summary>
        /// Open an existing recording with SQLite storage
        /// </summary>
        public IUnitOfWork OpenRecording(string filePath)
        {
            Logger.Info($"Opening existing SQLite recording: {filePath}");

            var unitOfWork = new SqliteUnitOfWork(filePath, createNew: false);
            
            // Initialize synchronously using GetAwaiter().GetResult() with ConfigureAwait(false)
            // This avoids deadlocks by not capturing the synchronization context
            unitOfWork.InitializeAsync(metadata: null).ConfigureAwait(false).GetAwaiter().GetResult();

            return unitOfWork;
        }
    }
}
