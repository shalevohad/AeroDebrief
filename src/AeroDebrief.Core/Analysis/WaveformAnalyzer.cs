using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using AeroDebrief.Core.Helpers;

namespace AeroDebrief.Core.Analysis
{
    /// <summary>
    /// Multi-threaded waveform analyzer with tiered peak caching for high-performance rendering.
    /// Produces render-ready peak data with minimal UI-thread overhead.
    /// </summary>
    public sealed class WaveformAnalyzer : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _cacheLock = new();
        private readonly Dictionary<PeakTier, WaveformPeakCache> _tierCaches = new();
        private bool _disposed;
        private bool _cacheBuilt = false;

        // Configuration
        private readonly int _maxWorkerThreads = Environment.ProcessorCount;

        public WaveformAnalyzer()
        {
            // Initialize tier caches
            _tierCaches[PeakTier.Tier1Px] = new WaveformPeakCache(1);    // 1 pixel resolution
            _tierCaches[PeakTier.Tier4Px] = new WaveformPeakCache(4);    // 4 pixel resolution
            _tierCaches[PeakTier.Tier8Px] = new WaveformPeakCache(8);    // 8 pixel resolution
            _tierCaches[PeakTier.Tier16Px] = new WaveformPeakCache(16);  // 16 pixel resolution

            Logger.Debug($"WaveformAnalyzer initialized with {_maxWorkerThreads} worker threads");
        }

        /// <summary>
        /// Builds complete waveform cache from a recording file
        /// </summary>
        public async Task BuildCacheAsync(string filePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Recording file not found: {filePath}");

            Logger.Info($"Building waveform cache from: {filePath}");

            try
            {
                var startTime = DateTime.UtcNow;

                // Read all packets from file
                var packets = await ReadPacketsAsync(filePath, ct);

                if (packets.Count == 0)
                {
                    Logger.Warn("No packets found in file");
                    return;
                }

                // Build caches in parallel for all tiers
                var tasks = _tierCaches.Values.Select(cache => 
                    Task.Run(() => BuildTierCache(cache, packets, ct), ct)
                ).ToArray();

                await Task.WhenAll(tasks);

                var elapsed = DateTime.UtcNow - startTime;
                _cacheBuilt = true;

                Logger.Info($"Waveform cache built in {elapsed.TotalMilliseconds:F0}ms for {packets.Count} packets");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to build waveform cache");
                throw;
            }
        }

        /// <summary>
        /// Updates cache with new packets from tail-follow
        /// </summary>
        public void UpdateTail(IEnumerable<AudioPacketMetadata> newPackets)
        {
            if (newPackets == null || !newPackets.Any())
                return;

            var packetList = newPackets.ToList();
            Logger.Debug($"Updating waveform cache with {packetList.Count} new packets");

            try
            {
                // Update all tier caches
                lock (_cacheLock)
                {
                    foreach (var cache in _tierCaches.Values)
                    {
                        UpdateTierCache(cache, packetList);
                    }
                }

                Logger.Debug("Waveform cache updated successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update waveform cache");
            }
        }

        /// <summary>
        /// Gets peak data for a specific time range and resolution tier
        /// </summary>
        public WaveformPeakData GetPeaks(TimeRange range, PeakTier tier)
        {
            if (!_cacheBuilt)
            {
                Logger.Warn("Cache not built, returning empty peak data");
                return WaveformPeakData.Empty;
            }

            lock (_cacheLock)
            {
                if (!_tierCaches.TryGetValue(tier, out var cache))
                {
                    Logger.Warn($"Tier {tier} not found, using Tier1Px");
                    cache = _tierCaches[PeakTier.Tier1Px];
                }

                return cache.GetPeaks(range);
            }
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                foreach (var cache in _tierCaches.Values)
                {
                    cache.Clear();
                }
                _cacheBuilt = false;
            }

            Logger.Debug("Waveform cache cleared");
        }

        /// <summary>
        /// Gets cache statistics for diagnostics
        /// </summary>
        public WaveformCacheStats GetCacheStats()
        {
            lock (_cacheLock)
            {
                return new WaveformCacheStats
                {
                    IsBuilt = _cacheBuilt,
                    TierStats = _tierCaches.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new TierCacheStats
                        {
                            Resolution = kvp.Value.Resolution,
                            EntryCount = kvp.Value.EntryCount,
                            MemoryBytes = kvp.Value.EstimatedMemoryBytes
                        })
                };
            }
        }

        private async Task<List<AudioPacketMetadata>> ReadPacketsAsync(string filePath, CancellationToken ct)
        {
            var packets = new List<AudioPacketMetadata>();

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            while (fs.Position < fs.Length && !ct.IsCancellationRequested)
            {
                try
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                    {
                        packets.Add(metadata);

                        if (packets.Count % 5000 == 0)
                        {
                            Logger.Debug($"Read {packets.Count} packets from file");
                        }
                    }
                    else
                    {
                        break; // End of readable data
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error reading packet at position {fs.Position}");
                    break;
                }
            }

            Logger.Debug($"Read total of {packets.Count} packets from file");
            return packets.OrderBy(p => p.Timestamp).ToList();
        }

        private void BuildTierCache(WaveformPeakCache cache, List<AudioPacketMetadata> packets, CancellationToken ct)
        {
            if (packets.Count == 0)
                return;

            var startTime = packets[0].Timestamp;
            var endTime = packets[^1].Timestamp;
            var totalDuration = endTime - startTime;

            if (totalDuration.TotalSeconds <= 0)
                return;

            // Calculate bucket size based on tier resolution
            var bucketDuration = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds / (2000.0 / cache.Resolution));
            var bucketCount = (int)Math.Ceiling(totalDuration.TotalMilliseconds / bucketDuration.TotalMilliseconds);

            Logger.Debug($"Building tier cache: {cache.Resolution}px resolution, {bucketCount} buckets");

            // Process packets into buckets
            var buckets = new ConcurrentDictionary<int, List<AudioPacketMetadata>>();

            // Parallelize packet bucketing
            Parallel.ForEach(packets, new ParallelOptions { MaxDegreeOfParallelism = _maxWorkerThreads, CancellationToken = ct }, packet =>
            {
                var relativeTime = packet.Timestamp - startTime;
                var bucketIndex = (int)(relativeTime.TotalMilliseconds / bucketDuration.TotalMilliseconds);

                buckets.GetOrAdd(bucketIndex, _ => new List<AudioPacketMetadata>()).Add(packet);
            });

            // Calculate peaks for each bucket
            var peaks = new float[bucketCount];
            var rms = new float[bucketCount];

            Parallel.For(0, bucketCount, new ParallelOptions { MaxDegreeOfParallelism = _maxWorkerThreads, CancellationToken = ct }, bucketIndex =>
            {
                if (buckets.TryGetValue(bucketIndex, out var bucketPackets))
                {
                    var (peakValue, rmsValue) = CalculateBucketPeaks(bucketPackets);
                    peaks[bucketIndex] = peakValue;
                    rms[bucketIndex] = rmsValue;
                }
            });

            // Store in cache
            cache.SetPeaks(new TimeRange(TimeSpan.Zero, totalDuration), peaks, rms);

            Logger.Debug($"Tier cache built: {cache.Resolution}px, {peaks.Length} peak values");
        }

        private void UpdateTierCache(WaveformPeakCache cache, List<AudioPacketMetadata> newPackets)
        {
            // For tail updates, we recalculate only the affected buckets
            // This is a simplified approach - a production implementation would
            // intelligently append to existing cache

            if (newPackets.Count == 0)
                return;

            Logger.Trace($"Updating tier cache: {cache.Resolution}px with {newPackets.Count} packets");

            // For simplicity, calculate peaks for the new packet range
            var (peak, rmsValue) = CalculateBucketPeaks(newPackets);

            // Append to cache (simplified - production would handle time ranges properly)
            cache.AppendPeak(peak, rmsValue);
        }

        private (float peak, float rms) CalculateBucketPeaks(List<AudioPacketMetadata> packets)
        {
            if (packets.Count == 0)
                return (0f, 0f);

            var maxPeak = 0f;
            var sumSquares = 0.0;
            var sampleCount = 0;

            foreach (var packet in packets)
            {
                if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                    continue;

                // Decode audio and calculate amplitude
                var amplitude = Audio.FilteredWaveformGenerator.CalculatePacketAmplitude(packet.AudioPayload);
                
                maxPeak = Math.Max(maxPeak, amplitude);
                sumSquares += amplitude * amplitude;
                sampleCount++;
            }

            var rms = sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0f;

            return (maxPeak, rms);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ClearCache();
            _disposed = true;

            Logger.Debug("WaveformAnalyzer disposed");
        }
    }

    /// <summary>
    /// Peak data tier for different zoom levels
    /// </summary>
    public enum PeakTier
    {
        Tier1Px = 0,   // 1 pixel per sample (highest resolution)
        Tier4Px = 1,   // 4 pixels per sample
        Tier8Px = 2,   // 8 pixels per sample
        Tier16Px = 3   // 16 pixels per sample (lowest resolution)
    }

    /// <summary>
    /// Time range for peak data queries
    /// </summary>
    public record TimeRange(TimeSpan Start, TimeSpan End)
    {
        public TimeSpan Duration => End - Start;
    }

    /// <summary>
    /// Peak data result for rendering
    /// </summary>
    public class WaveformPeakData
    {
        public float[] Peaks { get; set; } = Array.Empty<float>();
        public float[] RMS { get; set; } = Array.Empty<float>();
        public TimeRange Range { get; set; } = new TimeRange(TimeSpan.Zero, TimeSpan.Zero);

        public static WaveformPeakData Empty => new WaveformPeakData();
    }

    /// <summary>
    /// Cache for a specific peak tier
    /// </summary>
    internal class WaveformPeakCache
    {
        private float[] _peaks = Array.Empty<float>();
        private float[] _rms = Array.Empty<float>();
        private TimeRange _range = new TimeRange(TimeSpan.Zero, TimeSpan.Zero);

        public int Resolution { get; }
        public int EntryCount => _peaks.Length;
        public long EstimatedMemoryBytes => (_peaks.Length + _rms.Length) * sizeof(float);

        public WaveformPeakCache(int resolution)
        {
            Resolution = resolution;
        }

        public void SetPeaks(TimeRange range, float[] peaks, float[] rms)
        {
            _range = range;
            _peaks = peaks;
            _rms = rms;
        }

        public void AppendPeak(float peak, float rms)
        {
            var newPeaks = new float[_peaks.Length + 1];
            var newRms = new float[_rms.Length + 1];

            Array.Copy(_peaks, newPeaks, _peaks.Length);
            Array.Copy(_rms, newRms, _rms.Length);

            newPeaks[_peaks.Length] = peak;
            newRms[_rms.Length] = rms;

            _peaks = newPeaks;
            _rms = newRms;
        }

        public WaveformPeakData GetPeaks(TimeRange range)
        {
            // Calculate indices for the requested range
            if (_range.Duration.TotalSeconds <= 0 || _peaks.Length == 0)
                return WaveformPeakData.Empty;

            var startRatio = (range.Start - _range.Start).TotalSeconds / _range.Duration.TotalSeconds;
            var endRatio = (range.End - _range.Start).TotalSeconds / _range.Duration.TotalSeconds;

            var startIndex = Math.Clamp((int)(startRatio * _peaks.Length), 0, _peaks.Length - 1);
            var endIndex = Math.Clamp((int)(endRatio * _peaks.Length), startIndex + 1, _peaks.Length);

            var length = endIndex - startIndex;
            var peakSlice = new float[length];
            var rmsSlice = new float[length];

            Array.Copy(_peaks, startIndex, peakSlice, 0, length);
            Array.Copy(_rms, startIndex, rmsSlice, 0, length);

            return new WaveformPeakData
            {
                Peaks = peakSlice,
                RMS = rmsSlice,
                Range = range
            };
        }

        public void Clear()
        {
            _peaks = Array.Empty<float>();
            _rms = Array.Empty<float>();
            _range = new TimeRange(TimeSpan.Zero, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Cache statistics for diagnostics
    /// </summary>
    public class WaveformCacheStats
    {
        public bool IsBuilt { get; set; }
        public Dictionary<PeakTier, TierCacheStats> TierStats { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a specific tier cache
    /// </summary>
    public class TierCacheStats
    {
        public int Resolution { get; set; }
        public int EntryCount { get; set; }
        public long MemoryBytes { get; set; }
    }
}
