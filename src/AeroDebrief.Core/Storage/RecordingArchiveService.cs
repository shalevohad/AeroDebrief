using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Public-facing service that enforces the CVR-only architecture.
    /// This is the ONLY entry point users should use for working with recordings.
    /// 
    /// Core Principles:
    /// - Users see ONLY .cvr files
    /// - Database files are hidden in %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\
    /// - All database operations are abstracted behind IUnitOfWork
    /// - Archive operations are abstracted behind IArchiveCodec
    /// </summary>
    public class RecordingArchiveService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IRepositoryFactory _repositoryFactory;
        private IRecordingDbLifecycle? _dbLifecycle;
        private IUnitOfWork? _unitOfWork;
        private bool _disposed;

        public RecordingArchiveService(IRepositoryFactory? repositoryFactory = null)
        {
            _repositoryFactory = repositoryFactory ?? new Sqlite.SqliteRepositoryFactory();
        }

        /// <summary>
        /// The currently active session ID (null if no recording is open)
        /// </summary>
        public string? CurrentSessionId => _dbLifecycle?.SessionId;

        /// <summary>
        /// Whether a recording is currently open
        /// </summary>
        public bool IsOpen => _unitOfWork != null && _dbLifecycle?.IsInitialized == true;

        /// <summary>
        /// Gets the current unit of work (for testing purposes)
        /// </summary>
        internal IUnitOfWork? CurrentUnitOfWork => _unitOfWork;

        /// <summary>
        /// Creates a new recording session.
        /// The database is stored in a hidden directory: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\
        /// Users never see the database file.
        /// </summary>
        public async Task<IUnitOfWork> CreateNewRecordingAsync(
            RecordingMetadata metadata,
            CancellationToken ct = default)
        {
            if (IsOpen)
                throw new InvalidOperationException("A recording is already open. Close it first.");

            try
            {
                Logger.Info("Creating new recording session");

                // Create database lifecycle manager with new session ID
                _dbLifecycle = new RecordingDbLifecycle();
                await _dbLifecycle.CreateAsync(ct);

                Logger.Info($"? Session created: {_dbLifecycle.SessionId}");
                Logger.Info($"  Database path (hidden): {_dbLifecycle.DatabasePath}");
                Logger.Info($"  ??  This path should NEVER be shown to users!");

                // Create and initialize the recording
                _unitOfWork = _repositoryFactory.CreateRecording(_dbLifecycle.DatabasePath, metadata);
                await _unitOfWork.InitializeAsync(metadata, ct);

                Logger.Info("? Recording database initialized");

                return _unitOfWork;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create new recording");
                await CloseAsync();
                throw;
            }
        }

        /// <summary>
        /// Opens an existing .cvr recording file.
        /// The CVR is extracted to a hidden directory: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\
        /// Users never see the extracted database file.
        /// </summary>
        /// <param name="cvrFilePath">Path to the user-visible .cvr file</param>
        public async Task<IUnitOfWork> OpenCvrAsync(
            string cvrFilePath,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            if (IsOpen)
                throw new InvalidOperationException("A recording is already open. Close it first.");

            if (!File.Exists(cvrFilePath))
                throw new FileNotFoundException($"CVR file not found: {cvrFilePath}");

            if (!CvrFormat.IsCvrFile(cvrFilePath))
                throw new ArgumentException($"Not a CVR file: {cvrFilePath}");

            try
            {
                Logger.Info($"Opening CVR file: {cvrFilePath}");
                progress?.Report("Extracting CVR archive...");

                // Create database lifecycle manager with new session ID
                _dbLifecycle = new RecordingDbLifecycle();
                await _dbLifecycle.ExtractFromCvrAsync(cvrFilePath, ct);

                Logger.Info($"? CVR extracted to session: {_dbLifecycle.SessionId}");
                Logger.Info($"  Database path (hidden): {_dbLifecycle.DatabasePath}");
                Logger.Info($"  ??  This path should NEVER be shown to users!");

                progress?.Report("Opening database...");

                // Open the extracted database
                _unitOfWork = _repositoryFactory.OpenRecording(_dbLifecycle.DatabasePath);
                await _unitOfWork.InitializeAsync(metadata: null, ct);

                // Get packet count for logging
                var count = await _unitOfWork.Packets.GetCountAsync(ct);
                Logger.Info($"? Recording opened: {count:N0} packets");

                progress?.Report("Ready");

                return _unitOfWork;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open CVR file");
                await CloseAsync();
                throw;
            }
        }

        /// <summary>
        /// Saves the current recording as a .cvr file.
        /// The database is compressed from the hidden directory to a user-specified .cvr file.
        /// The hidden database remains until CloseAsync() is called.
        /// </summary>
        /// <param name="cvrOutputPath">Where to save the .cvr file (user-visible location)</param>
        public async Task SaveAsCvrAsync(
            string cvrOutputPath,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (!IsOpen)
                throw new InvalidOperationException("No recording is open");

            if (!cvrOutputPath.EndsWith(".cvr", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must have .cvr extension");

            try
            {
                Logger.Info($"Saving recording as CVR: {cvrOutputPath}");

                // Finalize the database
                progress?.Report(10);
                await _unitOfWork!.Frequencies.RebuildStatsAsync(ct);
                
                progress?.Report(20);
                await _unitOfWork.Players.RebuildStatsAsync(ct);
                
                progress?.Report(30);
                await _unitOfWork.Recording.MarkFinalizedAsync(ct);
                await _unitOfWork.Packets.FinalizeAsync(ct);

                Logger.Info("? Database finalized");

                // Dispose unit of work to flush all writes and close connections
                // This is essential before compression to ensure database is fully written
                _unitOfWork?.Dispose();
                _unitOfWork = null;

                // Small delay to ensure all file handles are released
                await Task.Delay(100, ct);

                // Compress to CVR
                var compressionProgress = new Progress<int>(p => 
                    progress?.Report(30 + (int)(p * 0.7))); // 30-100%

                await _dbLifecycle!.CompressToCvrAsync(cvrOutputPath, compressionProgress, ct);

                Logger.Info($"? Recording saved: {cvrOutputPath}");
                Logger.Info($"  Users see: {cvrOutputPath}");
                Logger.Info($"  Users do NOT see: {_dbLifecycle.DatabasePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save CVR file");
                throw;
            }
        }

        /// <summary>
        /// Closes the current recording and cleans up the hidden database directory.
        /// This removes all traces of the internal database from the file system.
        /// </summary>
        public async Task CloseAsync()
        {
            if (!IsOpen && _dbLifecycle == null)
                return;

            try
            {
                Logger.Info("Closing recording session");

                // Dispose unit of work
                _unitOfWork?.Dispose();
                _unitOfWork = null;

                // Cleanup database lifecycle (removes hidden directory)
                if (_dbLifecycle != null)
                {
                    await _dbLifecycle.CleanupAsync();
                    _dbLifecycle.Dispose();
                    _dbLifecycle = null;
                }

                Logger.Info("? Recording session closed and cleaned up");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error closing recording session");
                throw;
            }
        }

        /// <summary>
        /// Gets the file filter string for Open File dialogs.
        /// ONLY shows .cvr and legacy .adb formats to users.
        /// </summary>
        public static string GetFileDialogFilter()
        {
            return RecordingFileLoader.GetFileFilters();
        }

        /// <summary>
        /// Validates that a file path is a user-acceptable format (.cvr or .adb).
        /// Returns false for internal formats (.db, .cvr-debug) that should not be shown to users.
        /// </summary>
        public static bool IsUserVisibleFormat(string filePath)
        {
            return CvrFormat.IsCvrFile(filePath) || CvrFormat.IsAdbFile(filePath);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                CloseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during disposal");
            }
        }
    }
}
