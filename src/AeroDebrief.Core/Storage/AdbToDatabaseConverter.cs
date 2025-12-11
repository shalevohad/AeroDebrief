using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Storage.Sqlite;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Converts legacy ADB files to modern database format (SQLite).
    /// One-time migration tool with progress reporting and validation.
    /// Technology-agnostic using repository pattern.
    /// </summary>
    public class AdbToDatabaseConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IRepositoryFactory _repositoryFactory;

        public AdbToDatabaseConverter()
            : this(new SqliteRepositoryFactory())
        {
        }

        public AdbToDatabaseConverter(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        }

        /// <summary>
        /// Convert an ADB file to database format
        /// </summary>
        /// <param name="adbPath">Path to source ADB file</param>
        /// <param name="outputDbPath">Path to output database (optional, defaults to .db extension)</param>
        /// <param name="compressToCvr">Compress to CVR format after conversion</param>
        /// <param name="computeAmplitudeCache">Pre-compute amplitude cache during conversion (slower but instant waveforms later). Defaults to Constants.COMPUTE_AMPLITUDE_CACHE_ON_CONVERSION</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<ConversionResult> ConvertAsync(
            string adbPath,
            string? outputDbPath = null,
            bool compressToCvr = false,
            bool computeAmplitudeCache = Constants.COMPUTE_AMPLITUDE_CACHE_ON_CONVERSION,
            IProgress<ConversionProgress>? progress = null,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new ConversionResult { SourceFile = adbPath };

            try
            {
                // Validate input
                if (!File.Exists(adbPath))
                    throw new FileNotFoundException($"ADB file not found: {adbPath}");

                if (!Path.GetExtension(adbPath).Equals(".adb", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Input file must have .adb extension");

                outputDbPath ??= Path.ChangeExtension(adbPath, ".db");

                Logger.Info($"Starting ADB ? Database conversion:");
                Logger.Info($"  Source: {adbPath}");
                Logger.Info($"  Output: {outputDbPath}");
                Logger.Info($"  Amplitude cache: {(computeAmplitudeCache ? "Enabled (slower, instant waveforms)" : "Disabled (faster, waveforms computed on-demand)")}");
                if (compressToCvr)
                {
                    Logger.Info($"  CVR compression: Enabled");
                }

                progress?.Report(new ConversionProgress
                {
                    Stage = "Opening source file",
                    Percent = 0,
                    Message = "Reading ADB file header..."
                });

                // Open source ADB file
                using var reader = new IO.RecordingFileReader(adbPath);
                reader.Open();

                var header = reader.Header;
                
                Logger.Info($"ADB file opened:");
                Logger.Info($"  Has header: {header != null}");
                Logger.Info($"  Packets start at: {reader.PacketsStartPosition}");
                
                var metadata = new RecordingMetadata
                {
                    Version = "SQLite-v2",
                    ServerIp = header?.ServerIp ?? "unknown",
                    ServerPort = header?.ServerPort ?? 0,
                    StartTime = header?.RecordingStart ?? DateTime.UtcNow
                };

                progress?.Report(new ConversionProgress
                {
                    Stage = "Creating database",
                    Percent = 5,
                    Message = "Initializing SQLite database..."
                });

                // Create database using repository pattern
                using (var uow = _repositoryFactory.CreateRecording(outputDbPath, metadata))
                {
                    // ? NEW: Set amplitude computation flag in repository
                    if (uow is SqliteUnitOfWork sqliteUow && 
                        sqliteUow.Packets is SqlitePacketRepository sqlitePackets)
                    {
                        sqlitePackets.SetComputeAmplitudeCache(computeAmplitudeCache);
                    }

                    progress?.Report(new ConversionProgress
                    {
                        Stage = "Converting packets",
                        Percent = 10,
                        Message = computeAmplitudeCache 
                            ? "Converting packets with amplitude computation (slower)..." 
                            : "Converting packets (fast mode)..."
                    });

                    // Convert packets in batches (optimized for performance)
                    const int batchSize = 1000;
                    var batch = new List<AudioPacketMetadata>(batchSize);
                    var totalPackets = 0L;
                    var lastProgress = DateTime.UtcNow;
                    var lastGcCheck = DateTime.UtcNow;
                    var conversionStartTime = DateTime.UtcNow;

                    Logger.Info("Starting packet conversion loop...");

                    while (!ct.IsCancellationRequested)
                    {
                        var packet = reader.ReadNextPacket();
                        if (packet == null)
                        {
                            if (totalPackets == 0)
                            {
                                // First packet failed - this is likely a format issue
                                Logger.Warn($"Failed to read first packet. File position after header: {reader.PacketsStartPosition}");
                                Logger.Warn($"File size: {new System.IO.FileInfo(adbPath).Length} bytes");
                                Logger.Warn($"This suggests the ADB file format may have changed or the file is corrupted.");
                            }
                            Logger.Info($"ReadNextPacket returned null after {totalPackets} packets");
                            break;
                        }

                        batch.Add(packet);
                        totalPackets++;
                        
                        if (totalPackets <= 5)
                        {
                            Logger.Debug($"Packet {totalPackets}: Freq={packet.Frequency:F1} MHz, Player={packet.PlayerData?.Name ?? "Unknown"}");
                        }

                        if (batch.Count >= batchSize)
                        {
                            // Use repository pattern - single transaction per batch
                            await uow.Packets.InsertBatchAsync(batch, ct);
                            Logger.Info($"Inserted batch of {batch.Count} packets (total: {totalPackets})");
                            batch.Clear();

                            // MEMORY OPTIMIZATION: Force GC every 10 seconds
                            // This prevents memory from accumulating during long conversions
                            if ((DateTime.UtcNow - lastGcCheck).TotalSeconds >= 10)
                            {
                                GC.Collect(1, GCCollectionMode.Optimized, false);
                                lastGcCheck = DateTime.UtcNow;
                                Logger.Debug($"GC: Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
                            }

                            // Report detailed progress every 500ms
                            if ((DateTime.UtcNow - lastProgress).TotalMilliseconds >= 500)
                            {
                                var elapsed = DateTime.UtcNow - conversionStartTime;
                                var packetsPerSecond = totalPackets / elapsed.TotalSeconds;
                                
                                // Estimate remaining time (rough estimate)
                                // Assume average file has ~100k packets (adjust based on file size if available)
                                var estimatedTotalPackets = Math.Max(totalPackets * 1.2, 10000); // At least 10k
                                var remainingPackets = estimatedTotalPackets - totalPackets;
                                var etaSeconds = remainingPackets / Math.Max(1, packetsPerSecond);
                                
                                var progressPercent = 10 + (int)(70 * totalPackets / Math.Max(1, totalPackets + 1000));
                                
                                progress?.Report(new ConversionProgress
                                {
                                    Stage = "Converting packets",
                                    Percent = Math.Min(80, progressPercent),
                                    PacketsProcessed = totalPackets,
                                    
                                    // ? NEW: Set PacketsPerSecond for progress reporting
                                    PacketsPerSecond = packetsPerSecond,
                                    
                                    // ? NEW: Estimate time remaining (ETA)
                                    EstimatedTimeRemaining = etaSeconds > 0 && etaSeconds < 3600 
                                        ? TimeSpan.FromSeconds(etaSeconds) 
                                        : (TimeSpan?)null,
                                    
                                    Message = $"Processing: {totalPackets:N0} packets ({packetsPerSecond:F0} pkt/sec){(computeAmplitudeCache ? " + amplitude cache" : "")}"
                                });
                                lastProgress = DateTime.UtcNow;
                            }
                        }
                    }

                    Logger.Info($"Finished reading packets. Total: {totalPackets}");

                    // Insert remaining packets
                    if (batch.Count > 0)
                    {
                        await uow.Packets.InsertBatchAsync(batch, ct);
                        Logger.Info($"Inserted final batch of {batch.Count} packets");
                    }

                    progress?.Report(new ConversionProgress
                    {
                        Stage = "Finalizing database",
                        Percent = 85,
                        PacketsProcessed = totalPackets,
                        Message = "Building indexes and statistics..."
                    });

                    // Finalize: rebuild statistics and optimize
                    await uow.Frequencies.RebuildStatsAsync(ct);
                    await uow.Players.RebuildStatsAsync(ct);
                    await uow.Recording.MarkFinalizedAsync(ct);
                    await uow.Packets.FinalizeAsync(ct);

                    sw.Stop();

                    // Get final file sizes
                    var sourceSize = new FileInfo(adbPath).Length;
                    var outputSize = new FileInfo(outputDbPath).Length;
                    var compressionRatio = (1.0 - (double)outputSize / sourceSize) * 100.0;

                    result.Success = true;
                    result.TotalPackets = totalPackets;
                    result.Duration = sw.Elapsed;
                    result.OutputFile = outputDbPath;
                    result.SourceSizeBytes = sourceSize;
                    result.OutputSizeBytes = outputSize;
                    result.CompressionRatio = compressionRatio;

                    Logger.Info($"? Conversion complete:");
                    Logger.Info($"   Packets: {totalPackets:N0}");
                    Logger.Info($"   Duration: {sw.Elapsed.TotalSeconds:F1}s");
                    Logger.Info($"   Speed: {totalPackets / sw.Elapsed.TotalSeconds:N0} packets/sec");
                    Logger.Info($"   Source size: {sourceSize / 1024.0 / 1024.0:F1} MB");
                    Logger.Info($"   Output size: {outputSize / 1024.0 / 1024.0:F1} MB");
                    Logger.Info($"   Compression: {compressionRatio:F1}% smaller");
                    Logger.Info($"   Amplitude cache: {(computeAmplitudeCache ? "Computed" : "Skipped (compute on-demand)")}");
                } // Close the using block here - this disposes UnitOfWork and closes SQLite connection

                // Optional CVR compression (AFTER database is closed)
                if (compressToCvr)
                {
                    progress?.Report(new ConversionProgress
                    {
                        Stage = "Compressing to CVR",
                        Percent = 90,
                        PacketsProcessed = result.TotalPackets,
                        Message = "Compressing database to CVR format..."
                    });

                    // Give SQLite/Windows significant time to release ALL file handles
                    // WAL checkpoint helps, but Windows can still hold handles briefly
                    Logger.Info("Waiting for file handles to be released...");
                    await Task.Delay(500, ct);  // Increased from 100ms to 500ms
                    
                    // Force garbage collection to ensure all database connections are closed
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();  // Second collection to clean up any finalizers that created new objects
                    
                    // Additional delay after GC
                    await Task.Delay(200, ct);

                    Logger.Info("Compressing to CVR format...");
                    
                    // Retry logic in case file is still briefly locked
                    Exception? lastException = null;
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            var cvrPath = await CvrFormat.CompressToCvrAsync(outputDbPath, null, null, ct);
                            
                            // Update result with CVR information
                            result.OutputFile = cvrPath;
                            result.OutputSizeBytes = new FileInfo(cvrPath).Length;
                            
                            // Delete the uncompressed .db file and associated WAL files
                            // This is CRITICAL because the .db file can be very large
                            Logger.Info("Attempting to delete uncompressed database files...");
                            
                            // More aggressive delays and retries since Windows may hold the file
                            await Task.Delay(500, ct);  // Initial longer delay
                            
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            
                            await Task.Delay(300, ct);  // Additional delay after GC
                            
                            bool deletionSuccessful = false;
                            
                            for (int deleteAttempt = 0; deleteAttempt < 10; deleteAttempt++)  // Increased to 10 attempts
                            {
                                try
                                {
                                    // Delete main database file
                                    if (File.Exists(outputDbPath))
                                    {
                                        File.Delete(outputDbPath);
                                        Logger.Info($"? Deleted uncompressed database: {outputDbPath}");
                                        deletionSuccessful = true;
                                    }
                                    
                                    // Also delete WAL and SHM files if they exist
                                    var walPath = outputDbPath + "-wal";
                                    var shmPath = outputDbPath + "-shm";
                                    
                                    if (File.Exists(walPath))
                                    {
                                        File.Delete(walPath);
                                        Logger.Debug($"? Deleted WAL file: {walPath}");
                                    }
                                    
                                    if (File.Exists(shmPath))
                                    {
                                        File.Delete(shmPath);
                                        Logger.Debug($"? Deleted SHM file: {shmPath}");
                                    }
                                    
                                    break;  // Success - exit retry loop
                                }
                                catch (IOException deleteEx) when (deleteAttempt < 9)
                                {
                                    Logger.Warn($"Delete attempt {deleteAttempt + 1}/10 failed (file locked by another process), retrying in {500 * (deleteAttempt + 1)}ms...");
                                    await Task.Delay(500 * (deleteAttempt + 1), ct);  // Exponential backoff
                                    
                                    // Force GC every few attempts
                                    if (deleteAttempt % 3 == 2)
                                    {
                                        GC.Collect();
                                        GC.WaitForPendingFinalizers();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(ex, $"Could not delete uncompressed database files: {outputDbPath}");
                                    break;  // Don't retry on other exceptions
                                }
                            }
                            
                            if (!deletionSuccessful && File.Exists(outputDbPath))
                            {
                                Logger.Warn("? Could not delete uncompressed database file after 10 attempts.");
                                Logger.Warn($"? You can manually delete: {outputDbPath}");
                                Logger.Warn("? This is likely caused by Windows Search Indexer or Antivirus scanning the file.");
                                Logger.Warn("? The compressed .cvr file has been created successfully.");
                            }

                            lastException = null;
                            break;  // Success - compression complete
                        }
                        catch (IOException ex) when (attempt < 4)
                        {
                            lastException = ex;
                            Logger.Warn($"Compression attempt {attempt + 1} failed, retrying in 500ms...");
                            await Task.Delay(500, ct);
                        }
                    }
                    
                    if (lastException != null)
                    {
                        // All retries failed, throw the exception
                        throw lastException;
                    }
                }

                progress?.Report(new ConversionProgress
                {
                    Stage = "Complete",
                    Percent = 100,
                    PacketsProcessed = result.TotalPackets,
                    Message = $"Conversion complete! {result.TotalPackets:N0} packets in {sw.Elapsed.TotalSeconds:F1}s"
                });

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Conversion failed");
                result.Success = false;
                result.Error = ex.Message;
                
                progress?.Report(new ConversionProgress
                {
                    Stage = "Failed",
                    Percent = 0,
                    Message = $"? Error: {ex.Message}"
                });
                
                return result;
            }
        }

        /// <summary>
        /// Batch convert multiple ADB files
        /// </summary>
        public async Task<List<ConversionResult>> ConvertBatchAsync(
            IEnumerable<string> adbFiles,
            IProgress<BatchConversionProgress>? progress = null,
            CancellationToken ct = default)
        {
            var files = adbFiles.ToList();
            var results = new List<ConversionResult>();
            var totalFiles = files.Count;
            var completedFiles = 0;

            Logger.Info($"Starting batch conversion of {totalFiles} files");

            foreach (var adbPath in files)
            {
                if (ct.IsCancellationRequested)
                    break;

                var fileProgress = new Progress<ConversionProgress>(p =>
                {
                    progress?.Report(new BatchConversionProgress
                    {
                        CurrentFile = adbPath,
                        CurrentFilePercent = p.Percent,
                        TotalFiles = totalFiles,
                        CompletedFiles = completedFiles,
                        CurrentStage = p.Stage,
                        CurrentPackets = p.PacketsProcessed
                    });
                });

                var result = await ConvertAsync(adbPath, null, false, progress: fileProgress, ct: ct);
                results.Add(result);

                completedFiles++;

                progress?.Report(new BatchConversionProgress
                {
                    CurrentFile = adbPath,
                    CurrentFilePercent = 100,
                    TotalFiles = totalFiles,
                    CompletedFiles = completedFiles,
                    CurrentStage = result.Success ? "Complete" : "Failed"
                });
            }

            Logger.Info($"Batch conversion complete: {results.Count(r => r.Success)}/{totalFiles} successful");

            return results;
        }
    }

    /// <summary>
    /// Progress information for single file conversion
    /// </summary>
    public class ConversionProgress
    {
        public string Stage { get; set; } = string.Empty;
        public int Percent { get; set; }
        public long PacketsProcessed { get; set; }
        
        /// <summary>
        /// ? NEW: Human-readable progress message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// ? NEW: Current processing speed (packets per second)
        /// </summary>
        public double PacketsPerSecond { get; set; }
        
        /// <summary>
        /// ? NEW: Estimated time remaining (null if unknown)
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Progress information for batch conversion
    /// </summary>
    public class BatchConversionProgress
    {
        public string CurrentFile { get; set; } = string.Empty;
        public int CurrentFilePercent { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public string CurrentStage { get; set; } = string.Empty;
        public long CurrentPackets { get; set; }

        public int OverallPercent => TotalFiles > 0
            ? (CompletedFiles * 100 + CurrentFilePercent) / TotalFiles
            : 0;
    }

    /// <summary>
    /// Result of conversion operation
    /// </summary>
    public class ConversionResult
    {
        public bool Success { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public long TotalPackets { get; set; }
        public TimeSpan Duration { get; set; }
        public long SourceSizeBytes { get; set; }
        public long OutputSizeBytes { get; set; }
        public double CompressionRatio { get; set; }
        public string? Error { get; set; }

        public double PacketsPerSecond => Duration.TotalSeconds > 0
            ? TotalPackets / Duration.TotalSeconds
            : 0;
    }
}
