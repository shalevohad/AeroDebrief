using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Models;
using Dapper;
using NLog;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Packet source adapter for database storage using Repository Pattern.
    /// Bridges IUnitOfWork (Storage namespace) with FilePlaybackPipeline (IO namespace).
    /// Enables playback from CVR and database files.
    /// Technology-agnostic: works with any IUnitOfWork implementation (SQLite, etc.)
    /// </summary>
    public sealed class DatabasePacketSource : IPacketSource
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IUnitOfWork _unitOfWork;
        private Dictionary<double, FrequencyMetadata>? _frequencyMetadata;
        private long _totalPackets;
        private TimeSpan _totalDuration;
        private DateTime _recordingStart;
        private bool _disposed;

        public long TotalPackets => _totalPackets;
        public TimeSpan TotalDuration => _totalDuration;
        public DateTime RecordingStart => _recordingStart;

        public DatabasePacketSource(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Opens the database and loads metadata
        /// </summary>
        public async Task OpenAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            Logger.Info("Opening database packet source");
            progress?.Report("Loading recording metadata...");

            // Get recording stats using Repository Pattern
            var stats = await _unitOfWork.Recording.GetStatsAsync(cancellationToken);
            _totalPackets = stats.TotalPackets;
            _totalDuration = stats.Duration;
            _recordingStart = _unitOfWork.Packets.RecordingStart;

            Logger.Info($"Database packet source opened: {_totalPackets:N0} packets, {_totalDuration.TotalSeconds:F1}s duration");
            progress?.Report($"Loaded: {_totalPackets:N0} packets");

            // Pre-load frequency metadata for UI
            await BuildFrequencyMetadataAsync(progress, cancellationToken);
        }

        /// <summary>
        /// Reads packets in chronological order starting from a specific time
        /// </summary>
        public async IAsyncEnumerable<RadioPacket> ReadRange(
            TimeSpan from,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Logger.Debug($"ReadRange from {from}");

            // Use Repository Pattern: Packets.StreamAsync
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(from, null, null, null, null, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                yield return packet;
            }
        }

        /// <summary>
        /// Reads packets in batches for improved performance
        /// </summary>
        public async IAsyncEnumerable<RadioPacket[]> ReadRangeBatched(
            TimeSpan from,
            int batchSize = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Logger.Debug($"ReadRangeBatched from {from}, batch size={batchSize}");

            var batch = new List<RadioPacket>(batchSize);

            await foreach (var packet in _unitOfWork.Packets.StreamAsync(from, null, null, null, null, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                batch.Add(packet);

                // Yield batch when full
                if (batch.Count >= batchSize)
                {
                    yield return batch.ToArray();
                    batch.Clear();
                }
            }

            // Yield remaining packets
            if (batch.Count > 0)
                yield return batch.ToArray();
        }

        /// <summary>
        /// Gets frequency metadata without reading all packets
        /// </summary>
        public Dictionary<double, FrequencyMetadata> GetFrequencyMetadata()
        {
            return _frequencyMetadata ?? new Dictionary<double, FrequencyMetadata>();
        }

        /// <summary>
        /// Builds frequency metadata from database statistics using Repository Pattern.
        /// Phase 6 Optimization: Uses single JOIN query instead of loading all players
        /// and doing O(F * P) nested loops. This reduces queries by 50% and improves
        /// performance from O(F * P) to O(N) where N is total result rows.
        /// </summary>
        private async Task BuildFrequencyMetadataAsync(IProgress<string>? progress, CancellationToken cancellationToken)
        {
            progress?.Report("Loading frequency metadata...");

            // Phase 6 Optimization: Use a single JOIN query to get all data at once
            // This is much more efficient than loading frequencies and players separately
            // then doing nested loops to match them up
            
            // Get the underlying connection for direct SQL query
            var connection = GetDapperConnection();
            
            if (connection != null)
            {
                // Single efficient query with JOINs - let database do the work
                var query = @"
                    SELECT 
                        f.frequency,
                        f.modulation,
                        f.packet_count as freq_packet_count,
                        f.first_seen,
                        f.last_seen,
                        p.player_name,
                        p.transmitter_guid,
                        p.coalition,
                        p.unit_type,
                        COUNT(pkt.id) as player_packet_count
                    FROM frequency_stats f
                    LEFT JOIN packets pkt ON pkt.frequency = f.frequency AND pkt.modulation = f.modulation
                    LEFT JOIN player_stats p ON p.player_name = pkt.player_name
                    GROUP BY f.frequency, f.modulation, p.player_name, p.transmitter_guid, p.coalition, p.unit_type, f.packet_count, f.first_seen, f.last_seen
                    ORDER BY f.frequency, player_packet_count DESC";
                
                var rows = await connection.QueryAsync<dynamic>(query);
                
                // Group results by frequency in O(N) time
                _frequencyMetadata = rows
                    .GroupBy(r => (double)r.frequency)
                    .ToDictionary(
                        g => g.Key,
                        g => BuildFrequencyMetadataFromGroup(g));
                
                Logger.Info($"Built frequency metadata (optimized): {_frequencyMetadata.Count} frequencies");
                progress?.Report($"Metadata loaded: {_frequencyMetadata.Count} frequencies");
            }
            else
            {
                // Fallback to original method if we can't get connection
                // (This maintains backward compatibility)
                Logger.Warn("Could not get Dapper connection, using fallback method");
                await BuildFrequencyMetadataFallbackAsync(progress, cancellationToken);
            }
        }

        /// <summary>
        /// Builds FrequencyMetadata from a group of database rows (one frequency with all its players).
        /// </summary>
        private FrequencyMetadata BuildFrequencyMetadataFromGroup(IGrouping<double, dynamic> group)
        {
            var first = group.First();
            
            // Build player list from group
            var players = group
                .Where(r => r.player_name != null)  // Filter out nulls from LEFT JOIN
                .Select(r => new PlayerFrequencyInfo
                {
                    PlayerName = (string)r.player_name,
                    TransmitterGuid = (string)r.transmitter_guid,
                    Coalition = GetCoalitionName((byte)(long)r.coalition),
                    UnitType = r.unit_type as string ?? string.Empty,
                    UnitId = 0,
                    PacketCount = (int)(long)r.player_packet_count,
                    ContributionPercent = 0.0  // Calculated below
                })
                .ToList();
            
            // Calculate contribution percentages
            var totalPackets = players.Sum(p => p.PacketCount);
            foreach (var player in players)
            {
                player.ContributionPercent = totalPackets > 0
                    ? (double)player.PacketCount / totalPackets * 100.0
                    : 0.0;
            }
            
            return new FrequencyMetadata
            {
                Frequency = (double)first.frequency,
                PacketCount = (int)(long)first.freq_packet_count,
                FirstSeen = DateTime.Parse(first.first_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                LastSeen = DateTime.Parse(first.last_seen, null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                Players = players,
                Modulation = (byte)(long)first.modulation
            };
        }

        /// <summary>
        /// Fallback method using original approach (2 queries + nested loops).
        /// Used if optimized query fails or connection unavailable.
        /// </summary>
        private async Task BuildFrequencyMetadataFallbackAsync(IProgress<string>? progress, CancellationToken cancellationToken)
        {
            // Original implementation from before Phase 6 optimization
            var frequencies = await _unitOfWork.Frequencies.GetAllAsync(cancellationToken);
            var players = await _unitOfWork.Players.GetAllAsync(cancellationToken);

            _frequencyMetadata = new Dictionary<double, FrequencyMetadata>();

            foreach (var freq in frequencies)
            {
                var frequencyPlayers = players
                    .Where(p => p.Frequencies.Contains(freq.Frequency))
                    .Select(p => new PlayerFrequencyInfo
                    {
                        PlayerName = p.PlayerName,
                        TransmitterGuid = p.TransmitterGuid,
                        Coalition = p.CoalitionName,
                        UnitType = p.UnitType ?? string.Empty,
                        UnitId = 0,
                        PacketCount = (int)Math.Min(p.TransmissionCount, int.MaxValue),
                        ContributionPercent = 0.0
                    })
                    .ToList();

                var totalPackets = frequencyPlayers.Sum(p => p.PacketCount);
                foreach (var player in frequencyPlayers)
                {
                    player.ContributionPercent = totalPackets > 0
                        ? (double)player.PacketCount / totalPackets * 100.0
                        : 0.0;
                }

                _frequencyMetadata[freq.Frequency] = new FrequencyMetadata
                {
                    Frequency = freq.Frequency,
                    PacketCount = (int)Math.Min(freq.PacketCount, int.MaxValue),
                    FirstSeen = freq.FirstSeen,
                    LastSeen = freq.LastSeen,
                    Players = frequencyPlayers,
                    Modulation = freq.Modulation
                };
            }

            Logger.Info($"Built frequency metadata (fallback): {_frequencyMetadata.Count} frequencies, {players.Count} players");
            progress?.Report($"Metadata loaded: {_frequencyMetadata.Count} frequencies");
        }

        /// <summary>
        /// Gets the underlying IDbConnection from UnitOfWork for direct SQL queries.
        /// </summary>
        private IDbConnection? GetDapperConnection()
        {
            // Access the connection through reflection
            var uowType = _unitOfWork.GetType();
            var connectionField = uowType.GetField("_connection", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (connectionField != null)
            {
                return connectionField.GetValue(_unitOfWork) as IDbConnection;
            }
            
            return null;
        }

        private static string GetCoalitionName(byte coalition)
        {
            return coalition switch
            {
                0 => "Spectator",
                1 => "Red",
                2 => "Blue",
                _ => "Unknown"
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _unitOfWork?.Dispose();
            _disposed = true;

            Logger.Debug("DatabasePacketSource disposed");
        }
    }
}
