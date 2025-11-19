using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Handles CVR (Combat Voice Recording) format:
    /// - CVR = .cvr file = 7z compressed .duckdb database
    /// - Provides transparent compression/decompression
    /// - Auto-cleanup of temporary files
    /// </summary>
    public static class CvrFormat
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public const string CVR_EXTENSION = ".cvr";
        public const string DUCKDB_EXTENSION = ".duckdb";
        public const string ADB_EXTENSION = ".adb";
        
        private const string TEMP_FOLDER_PREFIX = "AeroDebrief_";

        /// <summary>
        /// Compress a DuckDB file into CVR format
        /// </summary>
        public static async Task<string> CompressToCvrAsync(
            string duckDbPath,
            string? outputCvrPath = null,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(duckDbPath))
                throw new FileNotFoundException($"DuckDB file not found: {duckDbPath}");

            outputCvrPath ??= Path.ChangeExtension(duckDbPath, CVR_EXTENSION);

            Logger.Info($"Compressing DuckDB to CVR:");
            Logger.Info($"  Source: {duckDbPath}");
            Logger.Info($"  Output: {outputCvrPath}");

            try
            {
                progress?.Report(0);

                // Create temp file for 7z archive (SharpCompress doesn't support direct archive creation)
                var tempArchive = Path.GetTempFileName();
                
                try
                {
                    using (var stream = File.OpenWrite(tempArchive))
                    using (var writer = SharpCompress.Writers.WriterFactory.Open(stream, SharpCompress.Common.ArchiveType.SevenZip, 
                        new SharpCompress.Writers.WriterOptions(CompressionType.LZMA)))
                    {
                        var fileInfo = new FileInfo(duckDbPath);
                        writer.Write(Path.GetFileName(duckDbPath), File.OpenRead(duckDbPath), fileInfo.LastWriteTime);
                    }
                    
                    progress?.Report(80);
                    
                    // Move temp to final destination
                    if (File.Exists(outputCvrPath))
                        File.Delete(outputCvrPath);
                    File.Move(tempArchive, outputCvrPath);
                }
                finally
                {
                    if (File.Exists(tempArchive))
                        File.Delete(tempArchive);
                }

                progress?.Report(100);

                var originalSize = new FileInfo(duckDbPath).Length;
                var compressedSize = new FileInfo(outputCvrPath).Length;
                var ratio = (1.0 - (double)compressedSize / originalSize) * 100.0;

                Logger.Info($"? Compression complete:");
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
        /// Decompress a CVR file to a temporary DuckDB file
        /// Returns the path to the temporary .duckdb file
        /// Caller is responsible for cleanup via CleanupTempFile()
        /// </summary>
        public static async Task<string> DecompressFromCvrAsync(
            string cvrPath,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(cvrPath))
                throw new FileNotFoundException($"CVR file not found: {cvrPath}");

            Logger.Info($"Decompressing CVR: {cvrPath}");

            try
            {
                progress?.Report(0);

                // Create temp directory
                var tempDir = Path.Combine(Path.GetTempPath(), $"{TEMP_FOLDER_PREFIX}{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                Logger.Debug($"Temp directory: {tempDir}");

                progress?.Report(20);

                // Extract using 7z
                using (var archive = SevenZipArchive.Open(cvrPath))
                {
                    var entry = archive.Entries.GetEnumerator();
                    if (!entry.MoveNext())
                        throw new InvalidDataException("CVR file is empty");

                    progress?.Report(40);

                    entry.Current.WriteToDirectory(tempDir, new ExtractionOptions
                    {
                        ExtractFullPath = false,
                        Overwrite = true
                    });

                    progress?.Report(80);
                }

                // Find the extracted .duckdb file
                var extractedFiles = Directory.GetFiles(tempDir, "*" + DUCKDB_EXTENSION);
                if (extractedFiles.Length == 0)
                    throw new InvalidDataException("CVR file does not contain a .duckdb file");

                var duckDbPath = extractedFiles[0];

                progress?.Report(100);

                Logger.Info($"? Decompressed to: {duckDbPath}");

                return duckDbPath;
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
        /// Check if a file is in DuckDB format
        /// </summary>
        public static bool IsDuckDbFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(DUCKDB_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the appropriate file format name for display
        /// DuckDB is called "CVR (Uncompressed)" to avoid confusing users.
        /// </summary>
        public static string GetFormatName(string filePath)
        {
            if (IsCvrFile(filePath)) return "CVR (Combat Voice Recording)";
            if (IsAdbFile(filePath)) return "ADB (Legacy Format)";
            if (IsDuckDbFile(filePath)) return "CVR (Uncompressed)";  // User-friendly name
            return "Unknown Format";
        }
    }
}
