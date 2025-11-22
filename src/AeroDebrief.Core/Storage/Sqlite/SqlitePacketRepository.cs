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

        public SqlitePacketRepository(IDbConnection connection, string filePath)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _filePath = filePath;
        }

        public DateTime RecordingStart => _recordingStart;
        public bool IsLiveRecording => _isLive;

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
                Logger.Warn("recording_info table is empty - database may be incomplete. Using defaults.");
                _recordingStart = DateTime.UtcNow;
                _isLive = false;
                return;
            }

            _recordingStart = DateTime.Parse(info.start_time, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            _isLive = info.is_live != 0;
            
            Logger.Debug($"Loaded recording metadata: Start={_recordingStart}, IsLive={_isLive}");
        }

        /// <summary>
        /// Insert a batch of packets with optimized bulk insert.
        /// Uses a single transaction for the entire batch.
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

                transaction.Commit();

                Logger.Debug($"Inserted {packetList.Count} packets in batch");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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
