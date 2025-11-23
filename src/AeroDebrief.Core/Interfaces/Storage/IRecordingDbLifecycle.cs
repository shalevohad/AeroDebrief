using System;
using System.Threading;
using System.Threading.Tasks;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Manages the lifecycle of recording database files in hidden working directories.
    /// Ensures that database files are never exposed to users - only .cvr archives are visible.
    /// </summary>
    public interface IRecordingDbLifecycle : IDisposable
    {
        /// <summary>
        /// The session ID for this recording (used in temp directory naming)
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Path to the hidden working directory where the database is stored.
        /// Example: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\
        /// This path should NEVER be exposed to users.
        /// </summary>
        string WorkingDirectory { get; }

        /// <summary>
        /// Path to the internal database file within the working directory.
        /// Example: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\recording.db
        /// This path should NEVER be exposed to users.
        /// </summary>
        string DatabasePath { get; }

        /// <summary>
        /// Whether this database is ready for access
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Creates a new database in a hidden working directory for recording.
        /// The database is stored in: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\recording.db
        /// </summary>
        Task CreateAsync(CancellationToken ct = default);

        /// <summary>
        /// Extracts a .cvr archive to a hidden working directory for reading.
        /// The extracted database is stored in: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\recording.db
        /// </summary>
        /// <param name="cvrFilePath">Path to the user-visible .cvr file</param>
        Task ExtractFromCvrAsync(string cvrFilePath, CancellationToken ct = default);

        /// <summary>
        /// Compresses the database to a .cvr archive file that users can save.
        /// The internal database remains in the hidden working directory until cleanup.
        /// </summary>
        /// <param name="cvrOutputPath">Path where the user wants to save the .cvr file</param>
        Task CompressToCvrAsync(string cvrOutputPath, IProgress<int>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Cleans up the hidden working directory and all its contents.
        /// Should be called when the session is closed or the application exits.
        /// </summary>
        Task CleanupAsync();
    }
}
