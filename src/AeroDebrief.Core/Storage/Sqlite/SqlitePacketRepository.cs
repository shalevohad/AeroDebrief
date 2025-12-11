using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Dapper;
using Microsoft.Data.Sqlite;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// High-performance SQLite implementation of packet repository using Dapper.
    /// Optimized with:
    /// - Single persistent connection (not per-operation)
    /// - WAL mode for concurrent access
    /// - Batch transactions (not per-packet)
    /// - Prepared statements for bulk inserts
    /// </summary>
    public class SqlitePacketRepository : IPacketRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private readonly string _filePath;
        private DateTime _recordingStart;
        private bool _isLive;
        private bool _disposed;
        private bool _computeAmplitudeCache = true; // ? NEW: Default to true for backward compatibility

        public SqlitePacketRepository(IDbConnection connection, string filePath)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _filePath = filePath;
        }

        public DateTime RecordingStart => _recordingStart;
        public bool IsLiveRecording => _isLive;

        /// <summary>
        /// ? NEW: Set whether to compute amplitude cache during packet insertion.
        /// Default is true. Set to false for faster conversion (amplitude computed on-demand later).
        /// </summary>
        public void SetComputeAmplitudeCache(bool compute)
        {
            _computeAmplitudeCache = compute;
            Logger.Info($"Amplitude cache computation: {(compute ? "Enabled" : "Disabled")}");
        }

        public async Task InitializeAsync(RecordingMetadata metadata, CancellationToken ct = default)
        {
            Logger.Info($"Initializing packet repository: {_filePath}");

            _recordingStart = metadata.StartTime;
            _isLive = true;

            // Schema is created at connection level, just initialize state
            await Task.CompletedTask;
        }

        public async Task OpenAsync(CancellationToken ct = default)
        {
            Logger.Info($"Opening packet repository: {_filePath}");

            // Load recording metadata to get start time
            var info = await _connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT start_time, is_live FROM recording_info WHERE id = 1");

            if (info == null)
            {
                Logger.Warn("recording_info table is empty - inferring start time from first packet");
                
                // CRITICAL FIX: Read actual recording start from first packet timestamp
                // instead of using DateTime.UtcNow which would be completely wrong!
                var firstPacket = await _connection.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT timestamp_utc FROM packets ORDER BY relative_ms ASC LIMIT 1");
                
                if (firstPacket != null && !string.IsNullOrEmpty(firstPacket.timestamp_utc))
                {
                    _recordingStart = DateTime.Parse(firstPacket.timestamp_utc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    Logger.Info($"Inferred recording start from first packet: {_recordingStart:yyyy-MM-dd HH:mm:ss} (Kind={_recordingStart.Kind})");
                }
                else
                {
                    // Absolute fallback if no packets exist
                    Logger.Error("No packets found in database - using current time as last resort");
                    _recordingStart = DateTime.UtcNow;
                }
                
                _isLive = false;
                return;
            }

            _recordingStart = DateTime.Parse(info.start_time, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            _isLive = info.is_live != 0;
            
            Logger.Debug($"Loaded recording metadata: Start={_recordingStart:yyyy-MM-dd HH:mm:ss} (Kind={_recordingStart.Kind}), IsLive={_isLive}");
        }

        /// <summary>
        /// Insert a batch of packets with optimized bulk insert.
        /// Uses a single transaction for the entire batch.
        /// ? Phase 7: Now computes and caches amplitude data during insertion.
        /// </summary>
        public async Task InsertBatchAsync(IEnumerable<AudioPacketMetadata> packets, CancellationToken ct = default)
        {
            var packetList = packets as IList<AudioPacketMetadata> ?? packets.ToList();
            if (!packetList.Any()) return;

            using var transaction = _connection.BeginTransaction();
            
            try
            {
                // Bulk insert with Dapper - single SQL, multiple parameter sets
                await _connection.ExecuteAsync(@"
                    INSERT INTO packets 
                    (timestamp_utc, relative_ms, frequency, modulation, player_name, 
                     transmitter_guid, coalition, unit_type, unit_id, audio_data, 
                     sample_rate, encryption, channel_count)
                    VALUES 
                    (@TimestampUtc, @RelativeMs, @Frequency, @Modulation, @PlayerName, 
                     @TransmitterGuid, @Coalition, @UnitType, @UnitId, @AudioData, 
                     @SampleRate, @Encryption, @ChannelCount)",
                    packetList.Select(p => new
                    {
                        TimestampUtc = p.Timestamp.ToString("O"),
                        RelativeMs = (long)(p.Timestamp - _recordingStart).TotalMilliseconds,
                        Frequency = p.Frequency,
                        Modulation = (int)p.Modulation,
                        PlayerName = p.PlayerData?.Name ?? "Unknown",
                        TransmitterGuid = p.TransmitterGuid ?? string.Empty,
                        Coalition = (int)p.Coalition,
                        UnitType = p.PlayerData?.AircraftInfo?.UnitType,
                        UnitId = p.PlayerData?.AircraftInfo?.UnitId,
                        AudioData = p.AudioPayload ?? Array.Empty<byte>(),
                        SampleRate = p.SampleRate,
                        Encryption = (int)p.Encryption,
                        ChannelCount = (int)p.ChannelCount
                    }),
                    transaction);

                // ? Phase 7: Compute and cache amplitude data (if enabled)
                if (_computeAmplitudeCache)
                {
                    await ComputeAndCacheAmplitudesAsync(packetList, transaction, ct);
                }

                transaction.Commit();

                Logger.Debug($"Inserted {packetList.Count} packets in batch{(_computeAmplitudeCache ? " with amplitude cache" : "")}");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// ? Phase 7: Pre-compute amplitude data during recording.
        /// This is the KEY optimization - decode ONCE during recording, cache forever!
        /// 
        /// CRITICAL: Stores RAW amplitude (before volume/gain/pan adjustments).
        /// Volume controls are applied at query time, not storage time.
        /// 
        /// MEMORY OPTIMIZED: Processes packets one at a time and disposes decoded audio immediately.
        /// </summary>
        private async Task ComputeAndCacheAmplitudesAsync(
            IList<AudioPacketMetadata> packets,
            IDbTransaction transaction,
            CancellationToken ct)
        {
            // OPTIMIZATION: Reuse single AudioProcessingEngine for batch
            using var processingEngine = new Audio.AudioProcessingEngine();
            processingEngine.Initialize();

            // OPTIMIZATION: Reuse timestamp string for entire batch
            var computedAt = DateTime.UtcNow.ToString("O");

            // Get first packet ID from this batch
            var firstPacketId = await _connection.ExecuteScalarAsync<long>(
                "SELECT last_insert_rowid() - @Count + 1",
                new { Count = packets.Count },
                transaction);

            // OPTIMIZATION: Pre-allocate list with exact capacity to avoid resizing
            var amplitudeEntries = new List<AmplitudeCacheEntry>(packets.Count);

            for (int i = 0; i < packets.Count; i++)
            {
                var packet = packets[i];
                var packetId = firstPacketId + i;

                if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                    continue;

                try
                {
                    // Decode audio ONCE (this is the expensive operation we're caching!)
                    var decoded = processingEngine.DecodePacketToFloat(packet);
                    if (decoded == null || decoded.Length == 0)
                        continue;

                    // Calculate max amplitude using Math.Abs for better performance
                    var maxAmplitude = 0f;
                    var sumSquares = 0.0;
                    
                    // OPTIMIZATION: Single loop for both max and RMS calculation
                    foreach (var sample in decoded)
                    {
                        var absSample = Math.Abs(sample);
                        if (absSample > maxAmplitude)
                            maxAmplitude = absSample;
                        sumSquares += sample * sample;
                    }
                    
                    var rmsAmplitude = (float)Math.Sqrt(sumSquares / decoded.Length);

                    // Extract peak envelope (10ms windows at 48kHz = 480 samples per window)
                    var peakEnvelope = ExtractPeakEnvelope(decoded, 480);

                    amplitudeEntries.Add(new AmplitudeCacheEntry
                    {
                        PacketId = packetId,
                        MaxAmplitude = maxAmplitude,
                        RmsAmplitude = rmsAmplitude,
                        PeakEnvelope = peakEnvelope
                    });
                    
                    // CRITICAL: Decoded audio is now eligible for GC after this iteration
                    // Peak envelope is small (~48 floats = 192 bytes vs. decoded ~7680 floats = 30KB)
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to compute amplitude for packet {packetId}");
                    // Continue with other packets - don't let one failure break the batch
                }
            }

            // Batch insert all amplitude data
            if (amplitudeEntries.Any())
            {
                // OPTIMIZATION: Pre-serialize envelopes to avoid repeated allocations in Select()
                var parameters = new List<object>(amplitudeEntries.Count);
                
                foreach (var entry in amplitudeEntries)
                {
                    parameters.Add(new
                    {
                        entry.PacketId,
                        entry.MaxAmplitude,
                        entry.RmsAmplitude,
                        PeakEnvelope = SerializeFloatArray(entry.PeakEnvelope),
                        EnvelopePoints = entry.PeakEnvelope.Length,
                        ComputedAt = computedAt // Reuse same string for entire batch
                    });
                }
                
                await _connection.ExecuteAsync(@"
                    INSERT INTO amplitude_cache 
                    (packet_id, max_amplitude, rms_amplitude, peak_envelope, envelope_points, computed_at)
                    VALUES (@PacketId, @MaxAmplitude, @RmsAmplitude, @PeakEnvelope, @EnvelopePoints, @ComputedAt)",
                    parameters,
                    transaction);

                Logger.Debug($"?? Cached amplitude data for {amplitudeEntries.Count}/{packets.Count} packets");
            }
            
            // AudioProcessingEngine is disposed by 'using' statement
        }

        /// <summary>
        /// Extract peak envelope from decoded samples using sliding window.
        /// Each window represents 10ms of audio for smooth waveform rendering.
        /// </summary>
        private static float[] ExtractPeakEnvelope(float[] samples, int windowSize)
        {
            var envelope = new List<float>();
            for (int i = 0; i < samples.Length; i += windowSize)
            {
                var end = Math.Min(i + windowSize, samples.Length);
                var peak = 0f;
                for (int j = i; j < end; j++)
                    peak = Math.Max(peak, Math.Abs(samples[j]));
                envelope.Add(peak);
            }
            return envelope.ToArray();
        }

        /// <summary>
        /// Serialize float array to BLOB for database storage.
        /// </summary>
        private static byte[] SerializeFloatArray(float[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();
            var bytes = new byte[data.Length * sizeof(float)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Stream packets with optional filtering.
        /// Uses efficient indexed queries with minimal buffering.
        /// Phase 6 Optimization: Uses batched streaming to balance memory and performance.
        /// Benefits: Significantly reduced memory usage, progressive result delivery.
        /// </summary>
        public async IAsyncEnumerable<RadioPacket> StreamAsync(
            TimeSpan fromTime,
            TimeSpan? toTime = null,
            IEnumerable<double>? frequencies = null,
            IEnumerable<string>? players = null,
            int? coalition = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var sql = @"
                SELECT id, timestamp_utc, frequency, modulation, player_name, 
                       transmitter_guid, coalition, unit_type, audio_data, sample_rate
                FROM packets 
                WHERE relative_ms >= @FromMs";

            var parameters = new DynamicParameters();
            parameters.Add("FromMs", (long)fromTime.TotalMilliseconds);

            if (toTime.HasValue)
            {
                sql += " AND relative_ms <= @ToMs";
                parameters.Add("ToMs", (long)toTime.Value.TotalMilliseconds);
            }

            if (frequencies?.Any() == true)
            {
                sql += " AND frequency IN @Frequencies";
                parameters.Add("Frequencies", frequencies.ToArray());
            }

            if (players?.Any() == true)
            {
                sql += " AND player_name IN @Players";
                parameters.Add("Players", players.ToArray());
            }

            if (coalition.HasValue)
            {
                sql += " AND coalition = @Coalition";
                parameters.Add("Coalition", coalition.Value);
            }

            sql += " ORDER BY relative_ms";

            // Phase 6 Optimization: Use batched streaming with LIMIT/OFFSET for memory efficiency
            // This processes results in chunks instead of loading everything into memory
            const int batchSize = 1000;  // Process 1000 packets at a time
            long offset = 0;
            bool hasMore = true;

            while (hasMore && !ct.IsCancellationRequested)
            {
                var batchSql = sql + $" LIMIT {batchSize} OFFSET {offset}";
                
                var batch = await _connection.QueryAsync<dynamic>(batchSql, parameters);
                var batchList = batch.ToList();
                
                hasMore = batchList.Count == batchSize;
                offset += batchList.Count;

                foreach (var row in batchList)
                {
                    if (ct.IsCancellationRequested)
                        yield break;

                    yield return new RadioPacket
                    {
                        PacketId = (ulong)(long)row.id,
                        Timestamp = DateTime.Parse(row.timestamp_utc, null,
                            System.Globalization.DateTimeStyles.RoundtripKind),
                        Frequency = (double)row.frequency,
                        Modulation = (byte)(long)row.modulation,
                        PlayerName = (string)row.player_name,
                        TransmitterGuid = (string)row.transmitter_guid,
                        Coalition = (byte)(long)row.coalition,
                        UnitType = row.unit_type as string,
                        AudioPayload = row.audio_data as byte[] ?? Array.Empty<byte>(),
                        SampleRate = (int)(long)row.sample_rate
                    };
                }

                // Allow async yielding between batches
                if (hasMore && !ct.IsCancellationRequested)
                    await Task.Yield();
            }
        }

        public async Task<RadioPacket?> GetByIdAsync(long packetId, CancellationToken ct = default)
        {
            var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT id, timestamp_utc, frequency, modulation, player_name, 
                       transmitter_guid, coalition, unit_type, audio_data, sample_rate
                FROM packets 
                WHERE id = @Id",
                new { Id = packetId });

            if (row == null) return null;

            return new RadioPacket
            {
                PacketId = (ulong)(long)row.id,
                Timestamp = DateTime.Parse(row.timestamp_utc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                Frequency = (double)row.frequency,
                Modulation = (byte)(long)row.modulation,
                PlayerName = (string)row.player_name,
                TransmitterGuid = (string)row.transmitter_guid,
                Coalition = (byte)(long)row.coalition,
                UnitType = row.unit_type as string,
                AudioPayload = (byte[])row.audio_data,
                SampleRate = (int)(long)row.sample_rate
            };
        }

        public async Task<long> GetCountAsync(CancellationToken ct = default)
        {
            return await _connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM packets");
        }

        public async Task<long> GetCountAsync(TimeSpan fromTime, TimeSpan toTime, CancellationToken ct = default)
        {
            return await _connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM packets WHERE relative_ms >= @From AND relative_ms <= @To",
                new
                {
                    From = (long)fromTime.TotalMilliseconds,
                    To = (long)toTime.TotalMilliseconds
                });
        }

        public async Task FinalizeAsync(CancellationToken ct = default)
        {
            Logger.Info("Finalizing packet repository (optimizing database)...");

            // Optimize database after all inserts
            // Explicitly specify CommandType.Text because SQLite doesn't support stored procedures
            await _connection.ExecuteAsync(
                new CommandDefinition("VACUUM", commandType: CommandType.Text, cancellationToken: ct));
            await _connection.ExecuteAsync(
                new CommandDefinition("ANALYZE", commandType: CommandType.Text, cancellationToken: ct));

            _isLive = false;

            Logger.Info("Packet repository finalized");
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork, don't dispose here
            _disposed = true;
        }
    }
}
