using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Unified recording file loader that handles all formats:
    /// - CVR (Combat Voice Recording) = .cvr = 7z compressed database (PRIMARY)
    /// - ADB (Legacy) = .adb = old binary format (auto-migrates)
    /// - DuckDB = .duckdb = uncompressed database (HIDDEN - works if provided)
    /// </summary>
    public static class RecordingFileLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Open any supported recording format and return a DuckDBStore
        /// Automatically handles:
        /// - CVR decompression
        /// - ADB migration
        /// - DuckDB direct open
        /// </summary>
        public static async Task<(DuckDBStore Store, string? TempPath)> OpenAsync(
            string filePath,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Recording file not found: {filePath}");

            var format = CvrFormat.GetFormatName(filePath);
            Logger.Info($"Opening recording: {filePath}");
            Logger.Info($"Format: {format}");

            string? tempPath = null;
            string duckDbPath;

            try
            {
                if (CvrFormat.IsCvrFile(filePath))
                {
                    // CVR: Decompress to temp location
                    progress?.Report("Decompressing CVR archive...");
                    
                    var decompressProgress = new Progress<int>(p =>
                        progress?.Report($"Decompressing CVR archive... {p}%"));
                    
                    tempPath = await CvrFormat.DecompressFromCvrAsync(filePath, decompressProgress, ct);
                    duckDbPath = tempPath;
                    
                    Logger.Info($"Decompressed to temp: {tempPath}");
                }
                else if (CvrFormat.IsAdbFile(filePath))
                {
                    // ADB: Check for existing .duckdb conversion
                    var existingDuckDb = Path.ChangeExtension(filePath, CvrFormat.DUCKDB_EXTENSION);
                    
                    if (File.Exists(existingDuckDb))
                    {
                        // Check if DuckDB is newer than ADB
                        var adbTime = File.GetLastWriteTimeUtc(filePath);
                        var dbTime = File.GetLastWriteTimeUtc(existingDuckDb);
                        
                        if (dbTime >= adbTime)
                        {
                            Logger.Info("Using existing DuckDB conversion");
                            progress?.Report("Opening converted DuckDB...");
                            duckDbPath = existingDuckDb;
                        }
                        else
                        {
                            Logger.Info("Existing DuckDB is outdated, re-converting...");
                            duckDbPath = await ConvertAdbAsync(filePath, existingDuckDb, progress, ct);
                        }
                    }
                    else
                    {
                        // Convert ADB to DuckDB
                        Logger.Info("Converting ADB to DuckDB...");
                        duckDbPath = await ConvertAdbAsync(filePath, existingDuckDb, progress, ct);
                    }
                }
                else if (CvrFormat.IsDuckDbFile(filePath))
                {
                    // DuckDB: Direct open
                    progress?.Report("Opening DuckDB...");
                    duckDbPath = filePath;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported file format: {Path.GetExtension(filePath)}");
                }

                progress?.Report("Loading database...");

                // Open the DuckDB store
                var store = new DuckDBStore(duckDbPath);
                await store.OpenAsync(ct);

                progress?.Report("Ready");

                Logger.Info($"? Recording opened: {store.TotalPackets:N0} packets");

                return (store, tempPath);
            }
            catch
            {
                // Cleanup temp file on error
                if (tempPath != null)
                {
                    CvrFormat.CleanupTempFile(tempPath);
                }
                throw;
            }
        }

        /// <summary>
        /// Convert ADB to DuckDB
        /// </summary>
        private static async Task<string> ConvertAdbAsync(
            string adbPath,
            string outputDbPath,
            IProgress<string>? progress,
            CancellationToken ct)
        {
            progress?.Report("Converting ADB to DuckDB...");

            var converter = new AdbToDuckDBConverter();
            
            var conversionProgress = new Progress<ConversionProgress>(p =>
                progress?.Report($"Converting: {p.Stage} ({p.Percent}%)"));

            var result = await converter.ConvertAsync(adbPath, outputDbPath, conversionProgress, ct);

            if (!result.Success)
                throw new Exception($"ADB conversion failed: {result.Error}");

            Logger.Info($"ADB converted: {result.TotalPackets:N0} packets in {result.Duration.TotalSeconds:F1}s");

            return outputDbPath;
        }

        /// <summary>
        /// Cleanup temporary files (call when done with the recording)
        /// </summary>
        public static void Cleanup(string? tempPath)
        {
            if (tempPath != null)
            {
                CvrFormat.CleanupTempFile(tempPath);
            }
        }

        /// <summary>
        /// Get supported file filters for open dialog.
        /// Shows CVR and ADB to users.
        /// DuckDB is hidden but still accepted via "All Files" filter.
        /// </summary>
        public static string GetFileFilters()
        {
            return "Combat Voice Recordings|*.cvr;*.adb|" +
                   "CVR Files (*.cvr)|*.cvr|" +
                   "Legacy ADB Files (*.adb)|*.adb|" +
                   "All Files (*.*)|*.*";
        }

        /// <summary>
        /// Get all supported extensions (including hidden .duckdb)
        /// </summary>
        public static string[] GetSupportedExtensions()
        {
            return new[] { ".cvr", ".adb", ".duckdb" }; // .duckdb works but not advertised
        }
    }
}
