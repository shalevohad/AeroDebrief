using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage;
using NLog;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Packet source adapter for DuckDB storage.
    /// Bridges DuckDBStore (Storage namespace) with FilePlaybackPipeline (IO namespace).
    /// Enables playback from CVR and DuckDB files.
    /// </summary>
    public sealed class DuckDBPacketSource : IPacketSource
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly DuckDBStore _store;
        private Dictionary<double, FrequencyMetadata>? _frequencyMetadata;
        private long _totalPackets;
        private TimeSpan _totalDuration;
        private bool _disposed;

        public long TotalPackets => _totalPackets;
        public TimeSpan TotalDuration => _totalDuration;
        public DateTime RecordingStart => _store.RecordingStart;

        public DuckDBPacketSource(DuckDBStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Opens the DuckDB store and loads metadata
        /// </summary>
        public async Task OpenAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            Logger.Info("Opening DuckDB packet source");
            progress?.Report("Loading recording metadata...");

            // Get recording stats
            var stats = await _store.GetRecordingStatsAsync(cancellationToken);
            _totalPackets = stats.TotalPackets;
            _totalDuration = stats.Duration;

            Logger.Info($"DuckDB packet source opened: {_totalPackets:N0} packets, {_totalDuration.TotalSeconds:F1}s duration");
            progress?.Report($"Loaded: {_totalPackets:N0} packets");

            // Pre-load frequency metadata for UI
            await BuildFrequencyMetadataAsync(progress, cancellationToken);
        }

        /// <summary>
        /// Reads packets in chronological order starting from a specific time
        /// </summary>
        public async IAsyncEnumerable<IO.RadioPacket> ReadRange(
            TimeSpan from,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Logger.Debug($"ReadRange from {from}");

            await foreach (var storagePacket in _store.StreamPacketsAsync(from, null, null, null, null, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                // Convert Storage.RadioPacket to IO.RadioPacket
                yield return ConvertPacket(storagePacket);
            }
        }

        /// <summary>
        /// Reads packets in batches for improved performance
        /// </summary>
        public async IAsyncEnumerable<IO.RadioPacket[]> ReadRangeBatched(
            TimeSpan from,
            int batchSize = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Logger.Debug($"ReadRangeBatched from {from}, batch size={batchSize}");

            var batch = new List<IO.RadioPacket>(batchSize);

            await foreach (var storagePacket in _store.StreamPacketsAsync(from, null, null, null, null, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                batch.Add(ConvertPacket(storagePacket));

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
        /// Builds frequency metadata from DuckDB stats
        /// </summary>
        private async Task BuildFrequencyMetadataAsync(IProgress<string>? progress, CancellationToken cancellationToken)
        {
            progress?.Report("Loading frequency metadata...");

            var frequencies = await _store.GetFrequenciesAsync(cancellationToken);
            var players = await _store.GetPlayersAsync(cancellationToken);

            _frequencyMetadata = new Dictionary<double, FrequencyMetadata>();

            foreach (var freq in frequencies)
            {
                // Find players who used this frequency
                var frequencyPlayers = players
                    .Where(p => p.Frequencies.Contains(freq.Frequency))
                    .Select(p => new PlayerFrequencyInfo
                    {
                        PlayerName = p.PlayerName,
                        TransmitterGuid = p.TransmitterGuid,
                        Coalition = GetCoalitionName(p.Coalition),
                        UnitType = p.UnitType ?? string.Empty,
                        UnitId = 0, // Not stored in DuckDB player stats
                        PacketCount = (int)Math.Min(p.TransmissionCount, int.MaxValue),
                        ContributionPercent = 0.0 // Will be calculated below
                    })
                    .ToList();

                // Calculate contribution percentages
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

            Logger.Info($"Built frequency metadata: {_frequencyMetadata.Count} frequencies, {players.Count} players");
            progress?.Report($"Metadata loaded: {_frequencyMetadata.Count} frequencies");
        }

        /// <summary>
        /// Converts Storage.RadioPacket to IO.RadioPacket
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IO.RadioPacket ConvertPacket(Storage.RadioPacket storagePacket)
        {
            return new IO.RadioPacket
            {
                PacketId = storagePacket.PacketId,
                Timestamp = storagePacket.Timestamp,
                Frequency = storagePacket.Frequency,
                Modulation = storagePacket.Modulation,
                Encryption = 0, // Not stored in DuckDB
                TransmitterUnitId = 0, // Not stored in DuckDB
                TransmitterGuid = storagePacket.TransmitterGuid,
                Coalition = storagePacket.Coalition,
                AudioPayload = storagePacket.AudioPayload,
                SampleRate = storagePacket.SampleRate,
                ChannelCount = 1, // Default mono
                PlayerData = new PlayerInfo
                {
                    Name = storagePacket.PlayerName,
                    TransmitterGuid = storagePacket.TransmitterGuid,
                    Coalition = storagePacket.Coalition,
                    AircraftInfo = new AircraftInfo
                    {
                        UnitType = storagePacket.UnitType ?? string.Empty,
                        UnitId = 0
                    }
                }
            };
        }

        private static string GetCoalitionName(byte coalition)
        {
            return coalition switch
            {
                1 => "Red",
                2 => "Blue",
                0 => "Neutral",
                _ => "Unknown"
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Note: We don't dispose the DuckDBStore here because it's owned by the caller
            // (RecordingFileLoader manages its lifetime)
            _frequencyMetadata?.Clear();
            _frequencyMetadata = null;
            _disposed = true;

            Logger.Debug("DuckDBPacketSource disposed");
        }
    }
}
