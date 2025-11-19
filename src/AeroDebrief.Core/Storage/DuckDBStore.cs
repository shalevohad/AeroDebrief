using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// DuckDB storage for AeroDebrief recordings.
    /// Supports concurrent reads and writes for live recording + playback.
    /// Thread-safe with WAL (Write-Ahead Logging) for durability.
    /// </summary>
    public sealed class DuckDBStore : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly string _dbPath;
        private DuckDBConnection? _connection;
        private DateTime _recordingStart;
        private bool _disposed;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        
        // Live recording state
        private bool _isLiveRecording;
        private long _lastPacketCount;
        private DateTime _lastStatsUpdate = DateTime.UtcNow;
        
        public DuckDBStore(string dbPath)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        }

        /// <summary>
        /// Create a new recording database for live recording
        /// </summary>
        public async Task CreateAsync(RecordingMetadata metadata, CancellationToken ct = default)
        {
            Logger.Info($"Creating new DuckDB recording: {_dbPath}");
            
            await _connectionLock.WaitAsync(ct);
            try
            {
                // Create connection with WAL mode for concurrent access
                _connection = new DuckDBConnection($"Data Source={_dbPath}");
                await _connection.OpenAsync(ct);
                
                // Configure for optimal performance
                await ConfigureConnectionAsync(ct);
                
                // Load and execute schema
                var schemaPath = Path.Combine(
                    Path.GetDirectoryName(typeof(DuckDBStore).Assembly.Location)!,
                    "Storage", "Schema.sql");
                
                if (!File.Exists(schemaPath))
                {
                    Logger.Warn($"Schema file not found: {schemaPath}, using embedded schema");
                    schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage", "Schema.sql");
                }
                
                var schema = await File.ReadAllTextAsync(schemaPath, ct);
                await ExecuteNonQueryAsync(schema, ct);
                
                // Insert recording metadata (single row)
                _recordingStart = metadata.StartTime;
                _isLiveRecording = true;
                
                await ExecuteNonQueryAsync(@"
                    INSERT INTO recording_info 
                    (id, version, server_ip, server_port, start_time, is_live)
                    VALUES (1, ?, ?, ?, ?, ?)",
                    ct,
                    metadata.Version,
                    metadata.ServerIp,
                    metadata.ServerPort,
                    metadata.StartTime,
                    true);
                
                Logger.Info($"? Recording created: Start={_recordingStart:o}");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Open an existing database (read-only or for continued recording)
        /// </summary>
        public async Task OpenAsync(CancellationToken ct = default)
        {
            Logger.Info($"Opening DuckDB recording: {_dbPath}");
            
            await _connectionLock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_dbPath))
                    throw new FileNotFoundException($"Database file not found: {_dbPath}");
                
                _connection = new DuckDBConnection($"Data Source={_dbPath}");
                await _connection.OpenAsync(ct);
                
                // Load recording metadata (single row)
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT start_time, is_live, packet_count FROM recording_info WHERE id = 1";
                
                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    _recordingStart = reader.GetDateTime(0);
                    _isLiveRecording = reader.GetBoolean(1);
                    _lastPacketCount = reader.GetInt64(2);
                    
                    Logger.Info($"? Recording opened: Live={_isLiveRecording}, Packets={_lastPacketCount:N0}");
                }
                else
                {
                    throw new InvalidDataException("Recording metadata not found in database");
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Insert a batch of packets (optimized for live recording)
        /// This method is thread-safe and non-blocking for readers
        /// </summary>
        public async Task InsertPacketsAsync(IEnumerable<AudioPacketMetadata> packets, CancellationToken ct = default)
        {
            var packetList = packets as IList<AudioPacketMetadata> ?? packets.ToList();
            if (!packetList.Any())
                return;

            await _connectionLock.WaitAsync(ct);
            try
            {
                using var transaction = _connection!.BeginTransaction();
                using var cmd = _connection.CreateCommand();
                
                cmd.CommandText = @"
                    INSERT INTO packets 
                    (id, timestamp_utc, relative_ms, frequency, modulation, player_name, 
                     transmitter_guid, coalition, unit_type, unit_id, audio_data, 
                     sample_rate, encryption, channel_count)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                foreach (var packet in packetList)
                {
                    var relativeMs = (long)(packet.Timestamp - _recordingStart).TotalMilliseconds;
                    
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(new DuckDBParameter(packet.PacketId));
                    cmd.Parameters.Add(new DuckDBParameter(packet.Timestamp));
                    cmd.Parameters.Add(new DuckDBParameter(relativeMs));
                    cmd.Parameters.Add(new DuckDBParameter(packet.Frequency));
                    cmd.Parameters.Add(new DuckDBParameter(packet.Modulation));
                    cmd.Parameters.Add(new DuckDBParameter(packet.PlayerData?.Name ?? "Unknown"));
                    cmd.Parameters.Add(new DuckDBParameter(packet.TransmitterGuid ?? string.Empty));
                    cmd.Parameters.Add(new DuckDBParameter(packet.Coalition));
                    cmd.Parameters.Add(new DuckDBParameter(packet.PlayerData?.AircraftInfo?.UnitType ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new DuckDBParameter(packet.PlayerData?.AircraftInfo?.UnitId ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new DuckDBParameter(packet.AudioPayload ?? Array.Empty<byte>()));
                    cmd.Parameters.Add(new DuckDBParameter(packet.SampleRate));
                    cmd.Parameters.Add(new DuckDBParameter(packet.Encryption));
                    cmd.Parameters.Add(new DuckDBParameter(packet.ChannelCount));
                    
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                
                await transaction.CommitAsync(ct);
                
                _lastPacketCount += packetList.Count;
                
                // Update stats periodically (every 5 seconds)
                if ((DateTime.UtcNow - _lastStatsUpdate).TotalSeconds >= 5)
                {
                    await UpdateLiveStatsAsync(ct);
                    _lastStatsUpdate = DateTime.UtcNow;
                }
                
                Logger.Debug($"Inserted {packetList.Count} packets (total: {_lastPacketCount:N0})");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Stream packets with optional filtering (main playback method)
        /// Non-blocking for concurrent live recording
        /// </summary>
        public async IAsyncEnumerable<RadioPacket> StreamPacketsAsync(
            TimeSpan fromTime,
            TimeSpan? toTime = null,
            IEnumerable<double>? frequencies = null,
            IEnumerable<string>? players = null,
            int? coalition = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT id, timestamp_utc, frequency, modulation, player_name, ");
            sql.Append("transmitter_guid, coalition, unit_type, audio_data, sample_rate ");
            sql.Append("FROM packets WHERE relative_ms >= ? ");
            
            var parameters = new List<object> { (long)fromTime.TotalMilliseconds };
            
            if (toTime.HasValue)
            {
                sql.Append("AND relative_ms <= ? ");
                parameters.Add((long)toTime.Value.TotalMilliseconds);
            }
            
            if (frequencies?.Any() == true)
            {
                var freqList = frequencies.ToList();
                sql.Append($"AND frequency IN ({string.Join(",", Enumerable.Range(0, freqList.Count).Select(_ => "?"))}) ");
                parameters.AddRange(freqList.Cast<object>());
            }
            
            if (players?.Any() == true)
            {
                var playerList = players.ToList();
                sql.Append($"AND player_name IN ({string.Join(",", Enumerable.Range(0, playerList.Count).Select(_ => "?"))}) ");
                parameters.AddRange(playerList.Cast<object>());
            }
            
            if (coalition.HasValue)
            {
                sql.Append("AND coalition = ? ");
                parameters.Add(coalition.Value);
            }
            
            sql.Append("ORDER BY relative_ms");

            // No lock needed for read operations - DuckDB handles concurrent access
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql.ToString();
            
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(new DuckDBParameter(param));
            }

            using var reader = await cmd.ExecuteReaderAsync(ct);
            
            while (await reader.ReadAsync(ct))
            {
                yield return new RadioPacket
                {
                    PacketId = reader.GetFieldValue<ulong>(0),
                    Timestamp = reader.GetDateTime(1),
                    Frequency = reader.GetDouble(2),
                    Modulation = reader.GetByte(3),
                    PlayerName = reader.GetString(4),
                    TransmitterGuid = reader.GetString(5),
                    Coalition = reader.GetByte(6),
                    UnitType = reader.IsDBNull(7) ? null : reader.GetString(7),
                    AudioPayload = (byte[])reader.GetValue(8),
                    SampleRate = reader.GetInt32(9)
                };
            }
        }

        /// <summary>
        /// Get frequency metadata (instant - from pre-computed stats)
        /// Safe to call during live recording
        /// </summary>
        public async Task<List<FrequencyInfo>> GetFrequenciesAsync(CancellationToken ct = default)
        {
            var result = new List<FrequencyInfo>();
            
            // If live recording and stats are stale, update them first
            if (_isLiveRecording)
            {
                await UpdateLiveStatsAsync(ct);
            }
            
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT * FROM frequency_stats ORDER BY frequency";
            
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new FrequencyInfo
                {
                    Frequency = reader.GetDouble(0),
                    Modulation = reader.GetByte(1),
                    PacketCount = reader.GetInt64(2),
                    FirstSeen = reader.GetDateTime(3),
                    LastSeen = reader.GetDateTime(4),
                    PlayerCount = reader.GetInt32(5)
                });
            }
            
            return result;
        }

        /// <summary>
        /// Get player metadata (instant - from pre-computed stats)
        /// Safe to call during live recording
        /// </summary>
        public async Task<List<PlayerInfo>> GetPlayersAsync(CancellationToken ct = default)
        {
            var result = new List<PlayerInfo>();
            
            // If live recording and stats are stale, update them first
            if (_isLiveRecording)
            {
                await UpdateLiveStatsAsync(ct);
            }
            
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT * FROM player_stats ORDER BY transmission_count DESC";
            
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new PlayerInfo
                {
                    PlayerName = reader.GetString(0),
                    TransmitterGuid = reader.GetString(1),
                    Coalition = reader.GetByte(2),
                    UnitType = reader.IsDBNull(3) ? null : reader.GetString(3),
                    TransmissionCount = reader.GetInt64(4),
                    FirstSeen = reader.GetDateTime(5),
                    LastSeen = reader.GetDateTime(6),
                    Frequencies = reader.GetFieldValue<double[]>(7).ToList()
                });
            }
            
            return result;
        }

        /// <summary>
        /// Update live statistics (called periodically during recording)
        /// </summary>
        private async Task UpdateLiveStatsAsync(CancellationToken ct = default)
        {
            await _connectionLock.WaitAsync(ct);
            try
            {
                // Rebuild frequency stats
                await ExecuteNonQueryAsync(@"
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
                    GROUP BY frequency, modulation", ct);
                
                // Rebuild player stats
                await ExecuteNonQueryAsync(@"
                    DELETE FROM player_stats;
                    INSERT INTO player_stats
                    SELECT 
                        player_name,
                        transmitter_guid,
                        coalition,
                        unit_type,
                        COUNT(*) as transmission_count,
                        MIN(timestamp_utc) as first_seen,
                        MAX(timestamp_utc) as last_seen,
                        array_agg(DISTINCT frequency ORDER BY frequency) as frequencies
                    FROM packets
                    GROUP BY player_name, transmitter_guid, coalition, unit_type", ct);
                
                Logger.Debug("Updated live stats");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Finalize recording (call when recording stops)
        /// Rebuilds stats, optimizes database, marks as complete
        /// </summary>
        public async Task FinalizeAsync(CancellationToken ct = default)
        {
            Logger.Info("Finalizing recording...");
            
            await _connectionLock.WaitAsync(ct);
            try
            {
                // Final stats update
                await UpdateLiveStatsAsync(ct);
                
                // Update recording info (single row)
                await ExecuteNonQueryAsync(@"
                    UPDATE recording_info 
                    SET 
                        end_time = (SELECT MAX(timestamp_utc) FROM packets),
                        packet_count = (SELECT COUNT(*) FROM packets),
                        duration_ms = (SELECT MAX(relative_ms) FROM packets),
                        is_live = FALSE,
                        last_updated = CURRENT_TIMESTAMP
                    WHERE id = 1", ct);
                
                // Optimize database
                await ExecuteNonQueryAsync("CHECKPOINT", ct);
                await ExecuteNonQueryAsync("VACUUM", ct);
                await ExecuteNonQueryAsync("ANALYZE", ct);
                
                _isLiveRecording = false;
                
                Logger.Info($"? Recording finalized: {_lastPacketCount:N0} packets");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Configure DuckDB connection for optimal performance
        /// </summary>
        private async Task ConfigureConnectionAsync(CancellationToken ct = default)
        {
            // Enable parallel processing
            await ExecuteNonQueryAsync("PRAGMA threads=4", ct);
            
            // Set memory limit
            await ExecuteNonQueryAsync("PRAGMA memory_limit='2GB'", ct);
            
            // Enable WAL for concurrent access
            await ExecuteNonQueryAsync("PRAGMA wal_autocheckpoint=1000", ct);
            
            // Enable object cache
            await ExecuteNonQueryAsync("PRAGMA enable_object_cache=true", ct);
            
            Logger.Debug("DuckDB connection configured");
        }

        private async Task ExecuteNonQueryAsync(string sql, CancellationToken ct, params object[] parameters)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(new DuckDBParameter(param));
            }
            
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// Get recording metadata (single row)
        /// </summary>
        public async Task<RecordingMetadata> GetMetadataAsync(CancellationToken ct = default)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT version, server_ip, server_port, start_time FROM recording_info WHERE id = 1";
            
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new RecordingMetadata
                {
                    Version = reader.GetString(0),
                    ServerIp = reader.GetString(1),
                    ServerPort = reader.GetInt32(2),
                    StartTime = reader.GetDateTime(3)
                };
            }
            
            throw new InvalidDataException("Recording metadata not found");
        }

        /// <summary>
        /// Get current recording statistics (for live monitoring)
        /// </summary>
        public async Task<RecordingStats> GetRecordingStatsAsync(CancellationToken ct = default)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    packet_count,
                    duration_ms,
                    is_live
                FROM recording_info 
                WHERE id = 1";
            
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var durationMs = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                
                return new RecordingStats
                {
                    TotalPackets = reader.GetInt64(0),
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    IsLive = reader.GetBoolean(2)
                };
            }
            
            return new RecordingStats();
        }

        /// <summary>
        /// Get unique frequencies (for live discovery)
        /// </summary>
        public async Task<List<FrequencyInfo>> GetUniqueFrequenciesAsync(CancellationToken ct = default)
        {
            return await GetFrequenciesAsync(ct);
        }

        /// <summary>
        /// Get unique players (for live discovery)
        /// </summary>
        public async Task<List<PlayerInfo>> GetUniquePlayersAsync(CancellationToken ct = default)
        {
            return await GetPlayersAsync(ct);
        }

        // Properties
        public DateTime RecordingStart => _recordingStart;
        public long TotalPackets => _lastPacketCount;
        public bool IsLiveRecording => _isLiveRecording;

        public void Dispose()
        {
            if (_disposed) return;
            
            _connectionLock.Wait();
            try
            {
                _connection?.Dispose();
                _connectionLock.Dispose();
                _disposed = true;
                
                Logger.Debug($"DuckDBStore disposed: {_dbPath}");
            }
            finally
            {
                // Lock already disposed
            }
        }
    }

    // Supporting types
    public class RecordingMetadata
    {
        public required string Version { get; set; }
        public required string ServerIp { get; set; }
        public required int ServerPort { get; set; }
        public required DateTime StartTime { get; set; }
    }

    public class RadioPacket
    {
        public ulong PacketId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Frequency { get; set; }
        public byte Modulation { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public byte Coalition { get; set; }
        public string? UnitType { get; set; }
        public byte[] AudioPayload { get; set; } = Array.Empty<byte>();
        public int SampleRate { get; set; }
    }

    public class FrequencyInfo
    {
        public double Frequency { get; set; }
        public byte Modulation { get; set; }
        public long PacketCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int PlayerCount { get; set; }
    }

    public class PlayerInfo
    {
        public string PlayerName { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public byte Coalition { get; set; }
        public string? UnitType { get; set; }
        public long TransmissionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public List<double> Frequencies { get; set; } = new();
    }

    public class RecordingStats
    {
        public long TotalPackets { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsLive { get; set; }
    }
}
