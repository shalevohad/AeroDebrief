using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Dapper;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// SQLite implementation of recording metadata repository using Dapper.
    /// Manages recording_info table and calculates statistics from packets table.
    /// 
    /// Responsibilities:
    /// - Query and update recording metadata (version, server, start time)
    /// - Calculate live statistics (packet count, duration)
    /// - Track live recording status
    /// - Mark recording as finalized
    /// 
    /// Performance optimizations:
    /// - Single persistent connection (shared with other repositories)
    /// - Efficient aggregation queries
    /// - Minimal round trips
    /// </summary>
    public class SqliteRecordingRepository : IRecordingRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private bool _disposed;

        public SqliteRecordingRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Get recording metadata from recording_info table
        /// </summary>
        public async Task<RecordingMetadata> GetMetadataAsync(CancellationToken ct = default)
        {
            Logger.Debug("Getting recording metadata");

            var row = await _connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM recording_info WHERE id = 1");

            return new RecordingMetadata
            {
                Version = row.version,
                ServerIp = row.server_ip,
                ServerPort = (int)(long)row.server_port,
                StartTime = DateTime.Parse(row.start_time, null,
                    System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }

        /// <summary>
        /// Update recording metadata in recording_info table
        /// </summary>
        public async Task UpdateMetadataAsync(RecordingMetadata metadata, CancellationToken ct = default)
        {
            Logger.Debug("Updating recording metadata");

            await _connection.ExecuteAsync(@"
                UPDATE recording_info 
                SET version = @Version,
                    server_ip = @ServerIp,
                    server_port = @ServerPort,
                    start_time = @StartTime
                WHERE id = 1",
                new
                {
                    metadata.Version,
                    metadata.ServerIp,
                    metadata.ServerPort,
                    StartTime = metadata.StartTime.ToString("O")
                });
        }

        /// <summary>
        /// Get current recording statistics by aggregating packets table.
        /// Efficient query that calculates all stats in single round trip.
        /// </summary>
        public async Task<RecordingStats> GetStatsAsync(CancellationToken ct = default)
        {
            Logger.Debug("Getting recording statistics");

            // Query both recording_info and packets in a single efficient query
            var row = await _connection.QuerySingleAsync<dynamic>(@"
                SELECT 
                    (SELECT COUNT(*) FROM packets) as packet_count,
                    (SELECT MIN(timestamp_utc) FROM packets) as first_packet,
                    (SELECT MAX(timestamp_utc) FROM packets) as last_packet,
                    (SELECT MAX(relative_ms) FROM packets) as duration_ms,
                    (SELECT is_live FROM recording_info WHERE id = 1) as is_live
                ");

            // Handle null values from empty database
            var packetCount = row.packet_count != null ? (long)row.packet_count : 0L;
            var isLiveValue = row.is_live != null ? (long)row.is_live : 0L;
            var isLive = isLiveValue != 0;

            if (packetCount == 0)
            {
                // No packets yet (new or empty recording)
                Logger.Debug("No packets found in database");
                return new RecordingStats
                {
                    TotalPackets = 0,
                    Duration = TimeSpan.Zero,
                    IsLive = isLive,
                    LastUpdate = DateTime.UtcNow
                };
            }

            // Calculate duration from relative_ms (most accurate)
            var durationMs = row.duration_ms != null ? (long)row.duration_ms : 0L;

            Logger.Debug($"Recording stats: Packets={packetCount}, Duration={durationMs}ms, IsLive={isLive}");

            return new RecordingStats
            {
                TotalPackets = packetCount,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                IsLive = isLive,
                LastUpdate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Mark recording as finalized (is_live = 0).
        /// Called when recording is stopped or completed.
        /// </summary>
        public async Task MarkFinalizedAsync(CancellationToken ct = default)
        {
            Logger.Info("Marking recording as finalized");

            await _connection.ExecuteAsync(
                "UPDATE recording_info SET is_live = 0 WHERE id = 1");
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork, don't dispose here
            _disposed = true;
        }
    }
}
