using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Sqlite;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Unified recording file loader that handles all formats:
    /// - CVR (Combat Voice Recording) = .cvr = 7z compressed database (PRIMARY)
    /// - ADB (Legacy) = .adb = old binary format (auto-migrates)
    /// - DB = .db = SQLite database (direct open)
    /// 
    /// Uses Repository Pattern with IUnitOfWork for technology-agnostic access.
    /// </summary>
    public static class RecordingFileLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly IRepositoryFactory Factory = new SqliteRepositoryFactory();

        /// <summary>
        /// Open any supported recording format and return a IUnitOfWork
        /// Automatically handles:
        /// - CVR decompression
        /// - ADB migration
        /// - DB direct open
        /// </summary>
        public static async Task<(IUnitOfWork UnitOfWork, string? TempPath)> OpenAsync(
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
            string dbPath;

            try
            {
                if (CvrFormat.IsCvrFile(filePath))
                {
                    // CVR: Decompress to temp location
                    progress?.Report("Decompressing CVR archive...");
                    
                    var decompressProgress = new Progress<int>(p =>
                        progress?.Report($"Decompressing CVR archive... {p}%"));
                    
                    tempPath = await CvrFormat.DecompressFromCvrAsync(filePath, decompressProgress, ct);
                    dbPath = tempPath;
                    
                    Logger.Info($"Decompressed to temp: {tempPath}");
                }
                else if (CvrFormat.IsAdbFile(filePath))
                {
                    // ADB: Check for existing .db conversion
                    var existingDb = Path.ChangeExtension(filePath, ".db");
                    
                    if (File.Exists(existingDb))
                    {
                        // Check if DB is newer than ADB
                        var adbTime = File.GetLastWriteTimeUtc(filePath);
                        var dbTime = File.GetLastWriteTimeUtc(existingDb);
                        
                        if (dbTime >= adbTime)
                        {
                            Logger.Info("Using existing DB conversion");
                            progress?.Report("Opening converted database...");
                            dbPath = existingDb;
                        }
                        else
                        {
                            Logger.Info("Existing DB is outdated, re-converting...");
                            dbPath = await ConvertAdbAsync(filePath, existingDb, progress, ct);
                        }
                    }
                    else
                    {
                        // Convert ADB to DB
                        Logger.Info("Converting ADB to database...");
                        dbPath = await ConvertAdbAsync(filePath, existingDb, progress, ct);
                    }
                }
                else if (filePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || 
                         filePath.EndsWith(".duckdb", StringComparison.OrdinalIgnoreCase) ||
                         filePath.EndsWith(".cvr-debug", StringComparison.OrdinalIgnoreCase))
                {
                    // DB or CVR-Debug: Direct open (internal formats)
                    progress?.Report("Opening database...");
                    dbPath = filePath;
                    
                    if (filePath.EndsWith(".cvr-debug", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info("Opening CVR-Debug file (uncompressed testing format)");
                    }
                }
                else
                {
                    throw new NotSupportedException($"Unsupported file format: {Path.GetExtension(filePath)}");
                }

                progress?.Report("Loading database...");

                // Open using Repository Pattern
                var uow = Factory.OpenRecording(dbPath);
                await uow.InitializeAsync(metadata: null, ct);

                // Get packet count for logging
                var count = await uow.Packets.GetCountAsync(ct);

                progress?.Report("Ready");

                Logger.Info($"? Recording opened: {count:N0} packets");

                return (uow, tempPath);
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
        /// Convert ADB to database format
        /// </summary>
        private static async Task<string> ConvertAdbAsync(
            string adbPath,
            string outputDbPath,
            IProgress<string>? progress,
            CancellationToken ct)
        {
            progress?.Report("Converting ADB to database...");
            var converter = new AdbToDatabaseConverter();
            
            var conversionProgress = new Progress<ConversionProgress>(p =>
                progress?.Report($"Converting: {p.Stage} ({p.Percent}%)"));

            var result = await converter.ConvertAsync(adbPath, outputDbPath, false, conversionProgress, ct);

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
        /// Get file filter string for open file dialogs
        /// IMPORTANT: Only CVR and legacy ADB formats are exposed to users.
        /// Internal database files (.db) are never shown to users.
        /// </summary>
        public static string GetFileFilters()
        {
            return "All Recording Files|*.cvr;*.adb|" +
                   "Combat Voice Recording (*.cvr)|*.cvr|" +
                   "Legacy Recording (*.adb)|*.adb|" +
                   "All Files|*.*";
        }

        /// <summary>
        /// Get array of supported file extensions for internal use
        /// Note: .db files can be opened but are not shown in user dialogs
        /// .cvr-debug is for internal development testing only
        /// </summary>
        public static string[] GetSupportedExtensions()
        {
            return new[] { ".cvr", ".adb", ".db", ".cvr-debug" };
        }
    }
}
