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
    /// Implementation of database lifecycle management that stores databases in hidden working directories.
    /// Ensures that only .cvr files are exposed to users, with all database files hidden.
    /// </summary>
    public class RecordingDbLifecycle : IRecordingDbLifecycle
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string SessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AeroDebrief",
            "Sessions");

        private readonly IArchiveCodec _codec;
        private bool _disposed;

        public string SessionId { get; }
        public string WorkingDirectory { get; }
        public string DatabasePath { get; }
        public bool IsInitialized { get; private set; }

        public RecordingDbLifecycle(string? sessionId = null, IArchiveCodec? codec = null)
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString("N");
            WorkingDirectory = Path.Combine(SessionsRoot, SessionId);
            DatabasePath = Path.Combine(WorkingDirectory, "recording.db");
            _codec = codec ?? new Codecs.ZstdArchiveCodec();

            Logger.Debug($"RecordingDbLifecycle created: SessionId={SessionId}");
            Logger.Debug($"Working directory: {WorkingDirectory}");
        }

        public async Task CreateAsync(CancellationToken ct = default)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Database already initialized");

            try
            {
                Logger.Info($"Creating new recording database in hidden directory");
                Logger.Info($"  Session ID: {SessionId}");
                Logger.Info($"  Working Dir: {WorkingDirectory}");

                // Create hidden working directory
                Directory.CreateDirectory(WorkingDirectory);

                // Mark directory as hidden on Windows
                if (OperatingSystem.IsWindows())
                {
                    var dirInfo = new DirectoryInfo(WorkingDirectory);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                }

                Logger.Info($"? Created hidden working directory");
                
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create recording database");
                throw;
            }
        }

        public async Task ExtractFromCvrAsync(string cvrFilePath, CancellationToken ct = default)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Database already initialized");

            if (!File.Exists(cvrFilePath))
                throw new FileNotFoundException($"CVR file not found: {cvrFilePath}");

            try
            {
                Logger.Info($"Extracting CVR to hidden directory");
                Logger.Info($"  Source CVR: {cvrFilePath}");
                Logger.Info($"  Session ID: {SessionId}");
                Logger.Info($"  Working Dir: {WorkingDirectory}");

                // Create hidden working directory
                Directory.CreateDirectory(WorkingDirectory);

                // Mark directory as hidden on Windows
                if (OperatingSystem.IsWindows())
                {
                    var dirInfo = new DirectoryInfo(WorkingDirectory);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                }

                // Extract CVR archive to database file
                var progress = new Progress<int>(p => Logger.Debug($"Extraction progress: {p}%"));
                
                using (var sourceStream = File.OpenRead(cvrFilePath))
                using (var outputStream = File.Create(DatabasePath))
                {
                    await _codec.DecompressAsync(sourceStream, outputStream, ct);
                }

                Logger.Info($"? Extracted CVR to hidden database: {DatabasePath}");
                
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to extract CVR");
                await CleanupAsync();
                throw;
            }
        }

        public async Task CompressToCvrAsync(string cvrOutputPath, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Database not initialized");

            if (!File.Exists(DatabasePath))
                throw new FileNotFoundException($"Database file not found: {DatabasePath}");

            try
            {
                Logger.Info($"Compressing database to CVR");
                Logger.Info($"  Source DB: {DatabasePath}");
                Logger.Info($"  Output CVR: {cvrOutputPath}");

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(cvrOutputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Compress database to CVR
                using (var sourceStream = new FileStream(DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var outputStream = File.Create(cvrOutputPath))
                {
                    await _codec.CompressAsync(sourceStream, outputStream, ct);
                }

                var originalSize = new FileInfo(DatabasePath).Length;
                var compressedSize = new FileInfo(cvrOutputPath).Length;
                var ratio = (1.0 - (double)compressedSize / originalSize) * 100.0;

                Logger.Info($"? Compression complete:");
                Logger.Info($"   Original: {originalSize / 1024.0 / 1024.0:F1} MB");
                Logger.Info($"   Compressed: {compressedSize / 1024.0 / 1024.0:F1} MB");
                Logger.Info($"   Ratio: {ratio:F1}%");

                progress?.Report(100);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to compress to CVR");
                throw;
            }
        }

        public async Task CleanupAsync()
        {
            if (_disposed)
                return;

            try
            {
                if (Directory.Exists(WorkingDirectory))
                {
                    Logger.Info($"Cleaning up session directory: {WorkingDirectory}");
                    
                    // Wait a moment for any file handles to be released
                    await Task.Delay(100);
                    
                    // Retry cleanup with exponential backoff
                    int maxRetries = 3;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            Directory.Delete(WorkingDirectory, recursive: true);
                            Logger.Info($"? Session directory cleaned up");
                            break;
                        }
                        catch (IOException) when (i < maxRetries - 1)
                        {
                            Logger.Debug($"Cleanup retry {i + 1}/{maxRetries}");
                            await Task.Delay(200 * (i + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to cleanup session directory: {WorkingDirectory}");
                // Don't throw - cleanup is best-effort
            }

            IsInitialized = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Synchronous cleanup on dispose
            try
            {
                CleanupAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during disposal cleanup");
            }
        }
    }
}
