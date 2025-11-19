using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Converts legacy ADB files to DuckDB format.
    /// One-time migration tool with progress reporting and validation.
    /// </summary>
    public class AdbToDuckDBConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Convert an ADB file to DuckDB format
        /// </summary>
        public async Task<ConversionResult> ConvertAsync(
            string adbPath,
            string? outputDbPath = null,
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

                outputDbPath ??= Path.ChangeExtension(adbPath, ".duckdb");

                Logger.Info($"Starting ADB ? DuckDB conversion:");
                Logger.Info($"  Source: {adbPath}");
                Logger.Info($"  Output: {outputDbPath}");

                progress?.Report(new ConversionProgress
                {
                    Stage = "Opening source file",
                    Percent = 0
                });

                // Open source ADB file
                using var reader = new IO.RecordingFileReader(adbPath);
                reader.Open();

                var header = reader.Header;
                var metadata = new RecordingMetadata
                {
                    Version = "DuckDB-v1",
                    ServerIp = header?.ServerIp ?? "unknown",
                    ServerPort = header?.ServerPort ?? 0,
                    StartTime = header?.RecordingStart ?? DateTime.UtcNow
                };

                progress?.Report(new ConversionProgress
                {
                    Stage = "Creating DuckDB database",
                    Percent = 5
                });

                // Create DuckDB database
                using var store = new DuckDBStore(outputDbPath);
                await store.CreateAsync(metadata, ct);

                progress?.Report(new ConversionProgress
                {
                    Stage = "Converting packets",
                    Percent = 10
                });

                // Convert packets in batches
                const int batchSize = 1000;
                var batch = new List<AudioPacketMetadata>(batchSize);
                var totalPackets = 0L;
                var lastProgress = DateTime.UtcNow;

                while (!ct.IsCancellationRequested)
                {
                    var packet = reader.ReadNextPacket();
                    if (packet == null) break;

                    batch.Add(packet);
                    totalPackets++;

                    if (batch.Count >= batchSize)
                    {
                        await store.InsertPacketsAsync(batch, ct);
                        batch.Clear();

                        // Report progress every 500ms
                        if ((DateTime.UtcNow - lastProgress).TotalMilliseconds >= 500)
                        {
                            var progressPercent = 10 + (int)(70 * totalPackets / Math.Max(1, totalPackets + 1000));
                            progress?.Report(new ConversionProgress
                            {
                                Stage = "Converting packets",
                                Percent = Math.Min(80, progressPercent),
                                PacketsProcessed = totalPackets
                            });
                            lastProgress = DateTime.UtcNow;
                        }
                    }
                }

                // Insert remaining packets
                if (batch.Count > 0)
                {
                    await store.InsertPacketsAsync(batch, ct);
                }

                progress?.Report(new ConversionProgress
                {
                    Stage = "Finalizing database",
                    Percent = 85,
                    PacketsProcessed = totalPackets
                });

                // Finalize database (optimize and build stats)
                await store.FinalizeAsync(ct);

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

                progress?.Report(new ConversionProgress
                {
                    Stage = "Complete",
                    Percent = 100,
                    PacketsProcessed = totalPackets
                });

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Conversion failed");
                result.Success = false;
                result.Error = ex.Message;
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

                var result = await ConvertAsync(adbPath, null, fileProgress, ct);
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
