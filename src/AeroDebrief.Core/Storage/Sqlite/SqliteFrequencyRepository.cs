using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Dapper;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// SQLite implementation of frequency repository using Dapper.
    /// Manages frequency statistics and aggregations.
    /// Phase 6 Optimization: Added query result caching for read-only statistics.
    /// </summary>
    public class SqliteFrequencyRepository : IFrequencyRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private bool _disposed;

        // Phase 6: Cache for read-only queries (statistics tables don't change after recording finalization)
        private List<FrequencyInfo>? _cachedAllFrequencies;
        private DateTime _cacheTime = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(30);  // 30-second cache

        public SqliteFrequencyRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<List<FrequencyInfo>> GetAllAsync(CancellationToken ct = default)
        {
            // Phase 6: Check cache first
            if (_cachedAllFrequencies != null && 
                DateTime.UtcNow - _cacheTime < _cacheLifetime)
            {
                Logger.Debug("Returning cached frequency list (Phase 6 optimization)");
                return _cachedAllFrequencies;
            }

            // Cache miss or expired - query database
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM frequency_stats ORDER BY frequency");

            _cachedAllFrequencies = rows.Select(MapToFrequencyInfo).ToList();
            _cacheTime = DateTime.UtcNow;

            Logger.Debug($"Frequency list cached: {_cachedAllFrequencies.Count} frequencies");
            return _cachedAllFrequencies;
        }

        public async Task<FrequencyInfo?> GetByFrequencyAsync(double frequency, byte modulation, CancellationToken ct = default)
        {
            var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM frequency_stats WHERE frequency = @Freq AND modulation = @Mod",
                new { Freq = frequency, Mod = (int)modulation });

            return row == null ? null : MapToFrequencyInfo(row);
        }

        public async Task<List<FrequencyInfo>> GetMostActiveAsync(int limit = 10, CancellationToken ct = default)
        {
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM frequency_stats ORDER BY packet_count DESC LIMIT @Limit",
                new { Limit = limit });

            return rows.Select(MapToFrequencyInfo).ToList();
        }

        public async Task<List<FrequencyInfo>> GetByPlayerAsync(string playerName, CancellationToken ct = default)
        {
            // Join with packets to find frequencies used by player
            var rows = await _connection.QueryAsync<dynamic>(@"
                SELECT DISTINCT f.*
                FROM frequency_stats f
                INNER JOIN packets p ON f.frequency = p.frequency AND f.modulation = p.modulation
                WHERE p.player_name = @PlayerName
                ORDER BY f.frequency",
                new { PlayerName = playerName });

            return rows.Select(MapToFrequencyInfo).ToList();
        }

        public async Task<List<FrequencyInfo>> GetByCoalitionAsync(int coalition, CancellationToken ct = default)
        {
            var rows = await _connection.QueryAsync<dynamic>(@"
                SELECT DISTINCT f.*
                FROM frequency_stats f
                INNER JOIN packets p ON f.frequency = p.frequency AND f.modulation = p.modulation
                WHERE p.coalition = @Coalition
                ORDER BY f.frequency",
                new { Coalition = coalition });

            return rows.Select(MapToFrequencyInfo).ToList();
        }

        public async Task RebuildStatsAsync(CancellationToken ct = default)
        {
            Logger.Debug("Rebuilding frequency statistics...");

            using var transaction = _connection.BeginTransaction();

            try
            {
                // Rebuild frequency stats table
                await _connection.ExecuteAsync(@"
                    DELETE FROM frequency_stats;
                    INSERT INTO frequency_stats
                    SELECT 
                        frequency,
                        modulation,
                        COUNT(*) as packet_count,
                        MIN(timestamp_utc) as first_seen,
                        MAX(timestamp_utc) as last_seen,
                        COUNT(DISTINCT player_name) as player_count,
                        MAX(relative_ms) - MIN(relative_ms) as total_duration_ms
                    FROM packets
                    GROUP BY frequency, modulation",
                    transaction: transaction);

                transaction.Commit();

                Logger.Debug("Frequency statistics rebuilt");

                // Invalidate cache after rebuild
                InvalidateCache();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<int> GetCountAsync(CancellationToken ct = default)
        {
            return await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM frequency_stats");
        }

        private static FrequencyInfo MapToFrequencyInfo(dynamic row)
        {
            return new FrequencyInfo
            {
                Frequency = (double)row.frequency,
                Modulation = (byte)(long)row.modulation,
                PacketCount = (long)row.packet_count,
                FirstSeen = DateTime.Parse(row.first_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                LastSeen = DateTime.Parse(row.last_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                PlayerCount = (int)(long)row.player_count
            };
        }

        /// <summary>
        /// Invalidates the cache. Call this if frequency_stats table is rebuilt.
        /// </summary>
        public void InvalidateCache()
        {
            _cachedAllFrequencies = null;
            _cacheTime = DateTime.MinValue;
            Logger.Debug("Frequency cache invalidated");
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork
            _disposed = true;
        }
    }
}
