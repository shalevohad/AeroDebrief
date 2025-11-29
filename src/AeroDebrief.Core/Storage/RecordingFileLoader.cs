using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Sqlite;
using Dapper;
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
                    // ADB: Check cache in temp directory, then convert to temporary directory
                    var cachedPath = FindCachedTempFile(filePath);
                    
                    if (cachedPath != null)
                    {
                        Logger.Info($"Using cached converted ADB: {cachedPath}");
                        
                        // Check if cached DB has precomputed amplitude data
                        await ValidateCachedAmplitudeData(cachedPath);
                        
                        progress?.Report("Using cached ADB conversion...");
                        tempPath = cachedPath;
                        dbPath = cachedPath;
                    }
                    else
                    {
                        // Clean up any outdated cache for this file
                        CleanupOutdatedCache(filePath);
                        
                        progress?.Report("Converting ADB to database (temp)...");

                        // Create a temp directory with the same prefix used by CvrFormat cleanup so it can be removed later
                        var tempDir = Path.Combine(Path.GetTempPath(), $"AeroDebrief_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);

                        // Use filename without extension to avoid extremely long paths
                        var tempDbPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(filePath) + ".db");

                        Logger.Info("Converting ADB to temporary database...");
                        Logger.Debug($"Temp DB path: {tempDbPath}");

                        dbPath = await ConvertAdbAsync(filePath, tempDbPath, progress, ct);

                        // Mark cache with source file timestamp
                        MarkCachedFile(tempDbPath, filePath);

                        // Remember temp path for cleanup
                        tempPath = tempDbPath;
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
        /// Find a cached temp file for the given source file.
        /// Returns path to cached .db file if found and still valid, otherwise null.
        /// Cache validity is based on matching source filename and last write time.
        /// </summary>
        private static string? FindCachedTempFile(string sourceFilePath)
        {
            try
            {
                var sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
                var sourceLastWriteTime = File.GetLastWriteTimeUtc(sourceFilePath);
                var tempRoot = Path.GetTempPath();

                Logger.Debug($"Looking for cached file: sourceFile={Path.GetFileName(sourceFilePath)}, timestamp={sourceLastWriteTime:O}");
                Logger.Debug($"Source file name (without ext): {sourceFileName}");

                // Search all AeroDebrief temp directories
                var tempDirs = Directory.GetDirectories(tempRoot, "AeroDebrief_*");
                
                Logger.Debug($"Found {tempDirs.Length} temp directories to search");

                foreach (var tempDir in tempDirs)
                {
                    Logger.Debug($"Searching in: {tempDir}");
                    
                    // Look for .db file with matching name
                    var dbPath = Path.Combine(tempDir, sourceFileName + ".db");
                    
                    Logger.Debug($"Looking for DB at: {dbPath}");
                    Logger.Debug($"DB exists: {File.Exists(dbPath)}");
                    
                    if (File.Exists(dbPath))
                    {
                        Logger.Debug($"Found DB file: {dbPath}");
                        
                        // Check if cache marker exists and matches source file timestamp
                        var markerPath = dbPath + ".cache";
                        
                        Logger.Debug($"Looking for marker at: {markerPath}");
                        Logger.Debug($"Marker exists: {File.Exists(markerPath)}");
                        
                        if (File.Exists(markerPath))
                        {
                            var markerContent = File.ReadAllText(markerPath);
                            var parts = markerContent.Split('|');
                            
                            Logger.Debug($"Cache marker content: {markerContent}");
                            Logger.Debug($"Parts count: {parts.Length}");
                            if (parts.Length >= 1) Logger.Debug($"Part[0] (filename): '{parts[0]}'");
                            if (parts.Length >= 2) Logger.Debug($"Part[1] (timestamp): '{parts[1]}'");
                            Logger.Debug($"Expected filename: '{Path.GetFileName(sourceFilePath)}'");
                            Logger.Debug($"Expected timestamp: '{sourceLastWriteTime:O}'");
                            
                            if (parts.Length == 2 &&
                                parts[0] == Path.GetFileName(sourceFilePath) &&
                                DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedTimestamp) &&
                                cachedTimestamp.ToUniversalTime() == sourceLastWriteTime)
                            {
                                Logger.Info($"? Found valid cached file: {dbPath}");
                                return dbPath;
                            }
                            else
                            {
                                // Log why it's invalid
                                if (parts.Length != 2)
                                    Logger.Debug($"  Invalid: marker has {parts.Length} parts (expected 2)");
                                else if (parts[0] != Path.GetFileName(sourceFilePath))
                                    Logger.Debug($"  Invalid: filename mismatch ('{parts[0]}' != '{Path.GetFileName(sourceFilePath)}')");
                                else if (!DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out cachedTimestamp))
                                    Logger.Debug($"  Invalid: could not parse timestamp '{parts[1]}'");
                                else if (cachedTimestamp.ToUniversalTime() != sourceLastWriteTime)
                                {
                                    Logger.Debug($"  Invalid: timestamp mismatch");
                                    Logger.Debug($"    Cached:   {cachedTimestamp:O} ({cachedTimestamp.Kind})");
                                    Logger.Debug($"    Expected: {sourceLastWriteTime:O} ({sourceLastWriteTime.Kind})");
                                    Logger.Debug($"    Difference: {(cachedTimestamp - sourceLastWriteTime).TotalMilliseconds} ms");
                                }
                                
                                Logger.Debug($"Found outdated cached file: {dbPath}");
                            }
                        }
                        else
                        {
                            Logger.Debug($"No cache marker found at: {markerPath}");
                        }
                    }
                }
                
                Logger.Debug("No valid cached file found");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to search for cached temp file");
                return null;
            }
        }

        /// <summary>
        /// Clean up outdated cache files for the given source file.
        /// Removes temp directories containing cached files with matching name but outdated timestamps.
        /// </summary>
        private static void CleanupOutdatedCache(string sourceFilePath)
        {
            try
            {
                var sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
                var sourceLastWriteTime = File.GetLastWriteTimeUtc(sourceFilePath);
                var tempRoot = Path.GetTempPath();

                var tempDirs = Directory.GetDirectories(tempRoot, "AeroDebrief_*");

                foreach (var tempDir in tempDirs)
                {
                    var dbPath = Path.Combine(tempDir, sourceFileName + ".db");
                    var markerPath = dbPath + ".cache";

                    if (File.Exists(dbPath) && File.Exists(markerPath))
                    {
                        var markerContent = File.ReadAllText(markerPath);
                        var parts = markerContent.Split('|');

                        // If this is our file but timestamp is outdated, delete it
                        if (parts.Length == 2 && parts[0] == Path.GetFileName(sourceFilePath))
                        {
                            if (DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedTimestamp) &&
                                cachedTimestamp.ToUniversalTime() != sourceLastWriteTime)
                            {
                                try
                                {
                                    Logger.Info($"Cleaning up outdated cache: {tempDir}");
                                    Directory.Delete(tempDir, true);
                                    Logger.Debug($"Deleted outdated cache directory: {tempDir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn(ex, $"Failed to delete outdated cache directory: {tempDir}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to cleanup outdated cache");
            }
        }

        /// <summary>
        /// Mark a cached file with source file information for validation.
        /// Creates a .cache marker file containing source filename and timestamp.
        /// </summary>
        private static void MarkCachedFile(string cachedDbPath, string sourceFilePath)
        {
            try
            {
                var markerPath = cachedDbPath + ".cache";
                var sourceFileName = Path.GetFileName(sourceFilePath);
                var sourceTimestamp = File.GetLastWriteTimeUtc(sourceFilePath);

                // Format: "filename|timestamp"
                var markerContent = $"{sourceFileName}|{sourceTimestamp:O}";
                File.WriteAllText(markerPath, markerContent);

                Logger.Debug($"Created cache marker: {markerPath}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to create cache marker file");
            }
        }

        /// <summary>
        /// Clear all cached temp files (ADB conversions and CVR decompressions).
        /// Called from settings window when user wants to clear cache.
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                var tempRoot = Path.GetTempPath();
                var tempDirs = Directory.GetDirectories(tempRoot, "AeroDebrief_*");

                Logger.Info($"Clearing cache: found {tempDirs.Length} temp directories");

                foreach (var tempDir in tempDirs)
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        Logger.Debug($"Deleted cache directory: {tempDir}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Failed to delete cache directory: {tempDir}");
                    }
                }

                Logger.Info($"Cache cleared: deleted {tempDirs.Length} directories");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clear cache");
                throw;
            }
        }

        /// <summary>
        /// Validate that the cached database has precomputed amplitude data.
        /// Logs statistics about amplitude data availability.
        /// </summary>
        private static async Task ValidateCachedAmplitudeData(string cachedDbPath)
        {
            try
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={cachedDbPath}");
                await connection.OpenAsync();

                // Check total packet count
                var totalPackets = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM packets");

                // Check packets with amplitude data
                var packetsWithAmplitude = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM packets WHERE amplitude_data IS NOT NULL");

                // Check packets with non-empty audio data
                var packetsWithAudio = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM packets WHERE LENGTH(audio_data) > 0");

                var amplitudePercentage = totalPackets > 0 ? (packetsWithAmplitude * 100.0) / totalPackets : 0;

                Logger.Info($"Cached DB amplitude data status:");
                Logger.Info($"  Total packets: {totalPackets:N0}");
                Logger.Info($"  Packets with audio: {packetsWithAudio:N0}");
                Logger.Info($"  Packets with precomputed amplitude: {packetsWithAmplitude:N0} ({amplitudePercentage:F1}%)");

                if (packetsWithAmplitude == 0 && packetsWithAudio > 0)
                {
                    Logger.Warn("??  Cached database has no precomputed amplitude data - will use on-demand computation");
                    Logger.Warn("   Consider clearing cache to regenerate with amplitude precomputation");
                }
                else if (amplitudePercentage < 50)
                {
                    Logger.Warn($"??  Only {amplitudePercentage:F1}% of packets have precomputed amplitude data");
                }
                else
                {
                    Logger.Info($"? Cached database has good amplitude precomputation coverage ({amplitudePercentage:F1}%)");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to validate cached amplitude data - proceeding anyway");
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
