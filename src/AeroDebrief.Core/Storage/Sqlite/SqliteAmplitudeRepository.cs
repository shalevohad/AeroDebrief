using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NLog;
using AeroDebrief.Core.Interfaces.Storage;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// Repository for pre-computed amplitude data.
    /// Phase 7: Provides instant waveform data without re-decoding audio.
    /// 
    /// CRITICAL DESIGN: Stores RAW amplitude (before volume/gain/pan adjustments).
    /// Volume controls are applied at QUERY time, not storage time.
    /// This ensures cached data remains valid regardless of mixer settings.
    /// </summary>
    public class SqliteAmplitudeRepository : IAmplitudeRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private bool _disposed;

        public SqliteAmplitudeRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Insert pre-computed amplitude data for a single packet.
        /// Used for on-demand computation when cache doesn't exist.
        /// </summary>
        public async Task InsertAsync(
            long packetId,
            float maxAmplitude,
            float rmsAmplitude,
            float[] peakEnvelope,
            CancellationToken ct = default)
        {
            // Serialize peak envelope to BLOB
            var envelopeBytes = SerializeFloatArray(peakEnvelope);

            await _connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO amplitude_cache 
                (packet_id, max_amplitude, rms_amplitude, peak_envelope, envelope_points, computed_at)
                VALUES (@PacketId, @MaxAmplitude, @RmsAmplitude, @PeakEnvelope, @EnvelopePoints, @ComputedAt)",
                new
                {
                    PacketId = packetId,
                    MaxAmplitude = maxAmplitude,
                    RmsAmplitude = rmsAmplitude,
                    PeakEnvelope = envelopeBytes,
                    EnvelopePoints = peakEnvelope.Length,
                    ComputedAt = DateTime.UtcNow.ToString("O")
                });
        }

        /// <summary>
        /// Batch insert amplitude data (for efficiency during recording).
        /// This is the PRIMARY method used during recording - inserts happen in batches.
        /// OPTIMIZED: Pre-serializes envelopes to reduce memory allocations.
        /// </summary>
        public async Task InsertBatchAsync(
            IEnumerable<AmplitudeCacheEntry> entries,
            CancellationToken ct = default)
        {
            var entryList = entries.ToList();
            if (!entryList.Any()) return;

            // OPTIMIZATION: Reuse timestamp string for entire batch
            var computedAt = DateTime.UtcNow.ToString("O");

            // OPTIMIZATION: Pre-serialize envelopes to avoid repeated allocations
            var parameters = new List<object>(entryList.Count);
            foreach (var entry in entryList)
            {
                parameters.Add(new
                {
                    entry.PacketId,
                    entry.MaxAmplitude,
                    entry.RmsAmplitude,
                    PeakEnvelope = SerializeFloatArray(entry.PeakEnvelope),
                    EnvelopePoints = entry.PeakEnvelope.Length,
                    ComputedAt = computedAt // Reuse same string
                });
            }

            await _connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO amplitude_cache 
                (packet_id, max_amplitude, rms_amplitude, peak_envelope, envelope_points, computed_at)
                VALUES (@PacketId, @MaxAmplitude, @RmsAmplitude, @PeakEnvelope, @EnvelopePoints, @ComputedAt)",
                parameters);

            Logger.Debug($"?? Cached amplitude data for {entryList.Count} packets");
        }

        /// <summary>
        /// Query RAW amplitude data for a time range (no volume adjustment).
        /// This is the base query - volume/gain is applied by QueryRangeWithGainAsync.
        /// </summary>
        public async Task<List<AmplitudeData>> QueryRangeAsync(
            double frequency,
            long fromMs,
            long toMs,
            CancellationToken ct = default)
        {
            var results = await _connection.QueryAsync<dynamic>(@"
                SELECT 
                    p.id,
                    p.relative_ms,
                    p.frequency,
                    p.transmitter_guid,
                    a.max_amplitude,
                    a.rms_amplitude,
                    a.peak_envelope,
                    a.envelope_points
                FROM packets p
                INNER JOIN amplitude_cache a ON p.id = a.packet_id
                WHERE p.frequency = @Frequency
                  AND p.relative_ms >= @FromMs
                  AND p.relative_ms <= @ToMs
                ORDER BY p.relative_ms",
                new { Frequency = frequency, FromMs = fromMs, ToMs = toMs });

            return results.Select(row => new AmplitudeData
            {
                PacketId = (long)row.id,
                RelativeMs = (long)row.relative_ms,
                Frequency = (double)row.frequency,
                TransmitterGuid = (string)row.transmitter_guid,
                MaxAmplitude = (float)(double)row.max_amplitude,
                RmsAmplitude = (float)(double)row.rms_amplitude,
                PeakEnvelope = DeserializeFloatArray((byte[])row.peak_envelope),
                EnvelopePoints = (int)(long)row.envelope_points
            }).ToList();
        }

        /// <summary>
        /// Query amplitude data with GAIN adjustment applied at query time.
        /// CRITICAL: This applies mixer settings AFTER retrieving cached raw amplitude.
        /// Volume adjustments are cheap (single multiplication) vs. re-decoding (1-5ms per packet).
        /// </summary>
        public async Task<List<AmplitudeData>> QueryRangeWithGainAsync(
            double frequency,
            long fromMs,
            long toMs,
            float gain = 1.0f,
            CancellationToken ct = default)
        {
            // Get RAW cached amplitude (unchanged from database)
            var rawResults = await QueryRangeAsync(frequency, fromMs, toMs, ct);

            // Skip gain adjustment if it's exactly 1.0 (no change)
            if (Math.Abs(gain - 1.0f) < 0.001f)
            {
                return rawResults;
            }

            // Apply gain adjustment to each result
            foreach (var result in rawResults)
            {
                // Apply gain to max amplitude
                result.MaxAmplitude *= gain;
                result.RmsAmplitude *= gain;
                
                // Apply gain to peak envelope
                for (int i = 0; i < result.PeakEnvelope.Length; i++)
                {
                    result.PeakEnvelope[i] *= gain;
                }
            }

            Logger.Trace($"Applied gain {gain:F2} to {rawResults.Count} amplitude results");
            return rawResults;
        }

        /// <summary>
        /// Check if amplitude cache exists for a packet.
        /// Used to determine if on-demand computation is needed.
        /// </summary>
        public async Task<bool> ExistsAsync(long packetId, CancellationToken ct = default)
        {
            var count = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM amplitude_cache WHERE packet_id = @PacketId",
                new { PacketId = packetId });
            return count > 0;
        }

        /// <summary>
        /// Get cache statistics for monitoring and diagnostics.
        /// </summary>
        public async Task<AmplitudeCacheStats> GetStatsAsync(CancellationToken ct = default)
        {
            var stats = await _connection.QuerySingleAsync<dynamic>(@"
                SELECT 
                    COUNT(*) as CachedPackets,
                    (SELECT COUNT(*) FROM packets) as TotalPackets,
                    MIN(computed_at) as FirstComputed,
                    MAX(computed_at) as LastComputed
                FROM amplitude_cache");

            return new AmplitudeCacheStats
            {
                CachedPackets = (long)stats.CachedPackets,
                TotalPackets = (long)stats.TotalPackets,
                CachePercentage = stats.TotalPackets > 0 
                    ? ((double)stats.CachedPackets / stats.TotalPackets * 100) 
                    : 0,
                FirstComputed = stats.FirstComputed != null 
                    ? DateTime.Parse(stats.FirstComputed) 
                    : (DateTime?)null,
                LastComputed = stats.LastComputed != null 
                    ? DateTime.Parse(stats.LastComputed) 
                    : (DateTime?)null
            };
        }

        /// <summary>
        /// Delete amplitude cache for a specific packet.
        /// Used when packet is updated or deleted.
        /// </summary>
        public async Task DeleteAsync(long packetId, CancellationToken ct = default)
        {
            await _connection.ExecuteAsync(
                "DELETE FROM amplitude_cache WHERE packet_id = @PacketId",
                new { PacketId = packetId });
        }

        /// <summary>
        /// Clear all amplitude cache data.
        /// Used for cache invalidation or cleanup.
        /// </summary>
        public async Task ClearAllAsync(CancellationToken ct = default)
        {
            await _connection.ExecuteAsync("DELETE FROM amplitude_cache");
            Logger.Info("Cleared all amplitude cache data");
        }

        private static byte[] SerializeFloatArray(float[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            var bytes = new byte[data.Length * sizeof(float)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] DeserializeFloatArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<float>();

            var floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork, don't dispose here
            _disposed = true;
        }
    }

    /// <summary>
    /// Entry for batch insertion during recording.
    /// </summary>
    public class AmplitudeCacheEntry
    {
        public long PacketId { get; set; }
        public float MaxAmplitude { get; set; }
        public float RmsAmplitude { get; set; }
        public float[] PeakEnvelope { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Amplitude data result from query (with optional gain applied).
    /// </summary>
    public class AmplitudeData
    {
        public long PacketId { get; set; }
        public long RelativeMs { get; set; }
        public double Frequency { get; set; }
        public string TransmitterGuid { get; set; } = string.Empty;
        public float MaxAmplitude { get; set; }
        public float RmsAmplitude { get; set; }
        public float[] PeakEnvelope { get; set; } = Array.Empty<float>();
        public int EnvelopePoints { get; set; }

        /// <summary>
        /// Get timestamp from relative milliseconds (requires recording start time).
        /// </summary>
        public DateTime GetTimestamp(DateTime recordingStart)
        {
            return recordingStart.AddMilliseconds(RelativeMs);
        }
    }

    /// <summary>
    /// Amplitude cache statistics for monitoring.
    /// </summary>
    public class AmplitudeCacheStats
    {
        public long CachedPackets { get; set; }
        public long TotalPackets { get; set; }
        public double CachePercentage { get; set; }
        public DateTime? FirstComputed { get; set; }
        public DateTime? LastComputed { get; set; }

        public override string ToString()
        {
            return $"Amplitude Cache: {CachedPackets:N0}/{TotalPackets:N0} packets ({CachePercentage:F1}% cached)";
        }
    }
}
