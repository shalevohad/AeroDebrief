using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using AeroDebrief.Core.Interfaces.Storage;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Handles CVR (Combat Voice Recording) format:
    /// - CVR = .cvr file = Compressed database (.db or .duckdb)
    /// - Provides transparent compression/decompression
    /// - Auto-cleanup of temporary files
    /// - Uses pluggable IArchiveCodec for compression
    /// </summary>
    public static class CvrFormat
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public const string CVR_EXTENSION = ".cvr";
        public const string DB_EXTENSION = ".db";
        public const string ADB_EXTENSION = ".adb";
        
        private const string TEMP_FOLDER_PREFIX = "AeroDebrief_";
        
        // Default codec - can be replaced via dependency injection in future
        private static IArchiveCodec _defaultCodec = new Codecs.ZstdArchiveCodec();

        /// <summary>
        /// Allows setting a custom codec (useful for testing or switching compression algorithms)
        /// </summary>
        public static void SetCodec(IArchiveCodec codec)
        {
            _defaultCodec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        /// <summary>
        /// Compress a database file .db into CVR format using the configured codec
        /// </summary>
        public static async Task<string> CompressToCvrAsync(
            string dbPath,
            string? outputCvrPath = null,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(dbPath))
                throw new FileNotFoundException($"Database file not found: {dbPath}");

            outputCvrPath ??= Path.ChangeExtension(dbPath, CVR_EXTENSION);

            Logger.Info($"Compressing database to CVR:");
            Logger.Info($"  Source: {dbPath}");
            Logger.Info($"  Output: {outputCvrPath}");
            Logger.Info($"  Codec: {_defaultCodec.CodecId}");

            try
            {
                progress?.Report(0);

                // Use pluggable codec for compression
                // FileShare.ReadWrite allows us to read the file even if another process has it open
                using (var sourceStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var outputStream = File.Create(outputCvrPath))
                {
                    await _defaultCodec.CompressAsync(sourceStream, outputStream, ct);
                }
                
                progress?.Report(100);

                var originalSize = new FileInfo(dbPath).Length;
                var compressedSize = new FileInfo(outputCvrPath).Length;
                var ratio = (1.0 - (double)compressedSize / originalSize) * 100.0;

                Logger.Info($"✓ Compression complete:");
                Logger.Info($"   Original: {originalSize / 1024.0 / 1024.0:F1} MB");
                Logger.Info($"   Compressed: {compressedSize / 1024.0 / 1024.0:F1} MB");
                Logger.Info($"   Ratio: {ratio:F1}%");

                return outputCvrPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to compress to CVR format");
                throw;
            }
        }

        /// <summary>
        /// Decompress a CVR file to a temporary database file using the configured codec
        /// Returns the path to the temporary .db or .duckdb file
        /// Caller is responsible for cleanup via CleanupTempFile()
        /// </summary>
        public static async Task<string> DecompressFromCvrAsync(
            string cvrPath,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(cvrPath))
                throw new FileNotFoundException($"CVR file not found: {cvrPath}");

            Logger.Info($"Decompressing CVR to temp database:");
            Logger.Info($"  Source: {cvrPath}");
            Logger.Info($"  Codec: {_defaultCodec.CodecId}");

            try
            {
                progress?.Report(0);

                // Create temp directory
                var tempDir = Path.Combine(Path.GetTempPath(), $"{TEMP_FOLDER_PREFIX}{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                Logger.Debug($"Temp directory: {tempDir}");

                progress?.Report(20);

                // Decompress using pluggable codec
                var originalFileName = Path.GetFileNameWithoutExtension(cvrPath);
                var dbPath = Path.Combine(tempDir, originalFileName);
                
                using (var sourceStream = File.OpenRead(cvrPath))
                using (var outputStream = File.Create(dbPath))
                {
                    await _defaultCodec.DecompressAsync(sourceStream, outputStream, ct);
                }

                progress?.Report(100);

                Logger.Info($"✓ Decompressed to: {dbPath}");

                return dbPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to decompress CVR format");
                throw;
            }
        }

        /// <summary>
        /// Cleanup temporary file and its directory
        /// </summary>
        public static void CleanupTempFile(string tempFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(tempFilePath))
                    return;

                var tempDir = Path.GetDirectoryName(tempFilePath);
                
                if (tempDir != null && tempDir.Contains(TEMP_FOLDER_PREFIX))
                {
                    Logger.Debug($"Cleaning up temp directory: {tempDir}");
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to cleanup temp file: {tempFilePath}");
            }
        }

        /// <summary>
        /// Check if a file is in CVR format
        /// </summary>
        public static bool IsCvrFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(CVR_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a file is in legacy ADB format
        /// </summary>
        public static bool IsAdbFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(ADB_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a file is in DB format (internal testing formats)
        /// Includes both .db and .cvr-debug extensions
        /// </summary>
        public static bool IsDbFile(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext.Equals(DB_EXTENSION, StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".cvr-debug", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the appropriate file format name for display
        /// IMPORTANT: Internal database terminology is hidden from users.
        /// All formats are presented as variations of CVR (Combat Voice Recording).
        /// </summary>
        public static string GetFormatName(string filePath)
        {
            if (IsCvrFile(filePath)) return "CVR (Combat Voice Recording)";
            if (IsAdbFile(filePath)) return "Legacy Recording Format";
            if (filePath.EndsWith(".cvr-debug", StringComparison.OrdinalIgnoreCase)) 
                return "CVR (Uncompressed Debug)";  // Development format
            if (IsDbFile(filePath)) return "CVR (Uncompressed)";  // Internal format, user-friendly name
            return "Unknown Format";
        }
    }
}
