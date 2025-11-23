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
    /// SQLite implementation of player repository using Dapper.
    /// Manages player statistics and aggregations.
    /// Phase 6 Optimization: Added query result caching for read-only statistics.
    /// </summary>
    public class SqlitePlayerRepository : IPlayerRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private bool _disposed;

        // Phase 6: Cache for read-only queries (statistics tables don't change after recording finalization)
        private List<PlayerStats>? _cachedAllPlayers;
        private DateTime _cacheTime = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(30);  // 30-second cache

        public SqlitePlayerRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<List<PlayerStats>> GetAllAsync(CancellationToken ct = default)
        {
            // Phase 6: Check cache first
            if (_cachedAllPlayers != null && 
                DateTime.UtcNow - _cacheTime < _cacheLifetime)
            {
                Logger.Debug("Returning cached player list (Phase 6 optimization)");
                return _cachedAllPlayers;
            }

            // Cache miss or expired - query database
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM player_stats ORDER BY player_name");

            _cachedAllPlayers = rows.Select(MapToPlayerStats).ToList();
            _cacheTime = DateTime.UtcNow;

            Logger.Debug($"Player list cached: {_cachedAllPlayers.Count} players");
            return _cachedAllPlayers;
        }

        public async Task<PlayerStats?> GetByNameAsync(string playerName, CancellationToken ct = default)
        {
            var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM player_stats WHERE player_name = @Name",
                new { Name = playerName });

            return row == null ? null : MapToPlayerStats(row);
        }

        public async Task<List<PlayerStats>> GetMostActiveAsync(int limit = 10, CancellationToken ct = default)
        {
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM player_stats ORDER BY transmission_count DESC LIMIT @Limit",
                new { Limit = limit });

            return rows.Select(MapToPlayerStats).ToList();
        }

        public async Task<List<PlayerStats>> GetByCoalitionAsync(int coalition, CancellationToken ct = default)
        {
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM player_stats WHERE coalition = @Coalition ORDER BY transmission_count DESC",
                new { Coalition = coalition });

            return rows.Select(MapToPlayerStats).ToList();
        }

        public async Task<List<PlayerStats>> GetByFrequencyAsync(double frequency, CancellationToken ct = default)
        {
            // Query players who have transmitted on this frequency
            // Note: frequencies column is JSON array
            var rows = await _connection.QueryAsync<dynamic>(@"
                SELECT DISTINCT ps.*
                FROM player_stats ps
                INNER JOIN packets p ON ps.player_name = p.player_name AND ps.transmitter_guid = p.transmitter_guid
                WHERE p.frequency = @Frequency",
                new { Frequency = frequency });

            return rows.Select(MapToPlayerStats).ToList();
        }

        public async Task<List<PlayerStats>> GetByAircraftTypeAsync(string unitType, CancellationToken ct = default)
        {
            var rows = await _connection.QueryAsync<dynamic>(
                "SELECT * FROM player_stats WHERE unit_type = @UnitType ORDER BY transmission_count DESC",
                new { UnitType = unitType });

            return rows.Select(MapToPlayerStats).ToList();
        }

        public async Task RebuildStatsAsync(CancellationToken ct = default)
        {
            Logger.Debug("Rebuilding player statistics...");

            using var transaction = _connection.BeginTransaction();

            try
            {
                // First, delete existing stats
                await _connection.ExecuteAsync("DELETE FROM player_stats", null, transaction);
                
                // Then rebuild player stats table with JSON frequency array
                // GROUP BY only the PRIMARY KEY fields (player_name, transmitter_guid)
                // Use MAX() for coalition and unit_type to get the most recent values
                await _connection.ExecuteAsync(@"
                    INSERT INTO player_stats
                    SELECT 
                        player_name,
                        transmitter_guid,
                        MAX(coalition) as coalition,
                        MAX(unit_type) as unit_type,
                        COUNT(*) as transmission_count,
                        MIN(timestamp_utc) as first_seen,
                        MAX(timestamp_utc) as last_seen,
                        json_group_array(DISTINCT frequency) as frequencies
                    FROM packets
                    GROUP BY player_name, transmitter_guid",
                    null, transaction);

                transaction.Commit();
                Logger.Debug("Player statistics rebuilt");
                
                // Phase 6: Invalidate cache after rebuild
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
                "SELECT COUNT(*) FROM player_stats");
        }

        private static PlayerStats MapToPlayerStats(dynamic row)
        {
            // Parse JSON frequency array
            var frequenciesJson = (string)row.frequencies;
            var frequencies = JsonSerializer.Deserialize<double[]>(frequenciesJson) ?? Array.Empty<double>();

            return new PlayerStats
            {
                PlayerName = (string)row.player_name,
                TransmitterGuid = (string)row.transmitter_guid,
                Coalition = (byte)(long)row.coalition,
                UnitType = row.unit_type as string,
                TransmissionCount = (long)row.transmission_count,
                FirstSeen = DateTime.Parse(row.first_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                LastSeen = DateTime.Parse(row.last_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                Frequencies = frequencies.ToList()
            };
        }

        /// <summary>
        /// Invalidates the cache. Call this if player_stats table is rebuilt.
        /// </summary>
        public void InvalidateCache()
        {
            _cachedAllPlayers = null;
            _cacheTime = DateTime.MinValue;
            Logger.Debug("Player cache invalidated");
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork
            _disposed = true;
        }
    }
}
