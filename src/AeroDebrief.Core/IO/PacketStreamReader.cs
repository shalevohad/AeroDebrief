using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using AeroDebrief.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Advanced stream reader for ADB (and legacy RAW) audio packet files.
    /// Supports live tail-following, timestamp-based seeking, and filtering.
    /// Thread-safe with FileShare.ReadWrite for simultaneous read/write access.
    /// </summary>
    public sealed class PacketStreamReader : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private FileStream? _fileStream;
        private BinaryReader? _reader;
        private string? _filePath;
        private FileFormat _detectedFormat;
        private bool _disposed;

        // Index cache for fast timestamp-based seeking
        private readonly SortedDictionary<TimeSpan, long> _timestampIndex = new();
        private readonly object _indexLock = new();
        private bool _indexBuilt = false;

        // Filtering predicates
        private Func<Coalition, bool>? _coalitionFilter;
        private Func<double, bool>? _frequencyFilter;
        private Func<PlayerInfo, bool>? _playerFilter;

        // Metadata cache
        private TimeSpan _totalDuration = TimeSpan.Zero;
        private DateTime? _recordingStart;
        private readonly HashSet<double> _discoveredFrequencies = new();
        private readonly HashSet<string> _discoveredPlayers = new();

        // Tail follow state
        private long _lastKnownPosition = 0;
        private CancellationTokenSource? _tailFollowCts;

        /// <summary>
        /// Event raised when new data becomes available (for live tail-follow)
        /// </summary>
        public event EventHandler<WaveformDataEventArgs>? OnNewDataAvailable;

        /// <summary>
        /// Event raised when new frequencies are discovered
        /// </summary>
        public event EventHandler<FrequencyDiscoveredEventArgs>? OnFrequencyDiscovered;

        /// <summary>
        /// Event raised when new players are discovered
        /// </summary>
        public event EventHandler<PlayerDiscoveredEventArgs>? OnPlayerDiscovered;

        /// <summary>
        /// Opens a recording file for reading with optional live tail-follow support
        /// </summary>
        /// <param name="path">Path to the recording file (ADB or RAW format)</param>
        /// <param name="share">File share mode (default: ReadWrite for live following)</param>
        /// <param name="ct">Cancellation token</param>
        public async Task OpenAsync(string path, FileShare share = FileShare.ReadWrite, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("File path cannot be null or empty", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Recording file not found: {path}");

            _filePath = path;

            Logger.Info($"Opening recording file: {path} with FileShare.{share}");

            try
            {
                // Open with specified share mode (default ReadWrite for live tail-follow)
                _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, share, 4096, useAsync: true);
                _reader = new BinaryReader(_fileStream);

                // Detect file format (ADB vs RAW)
                _detectedFormat = await DetectFileFormatAsync(ct);
                Logger.Info($"Detected file format: {_detectedFormat}");

                // Build initial index for fast seeking
                await BuildIndexAsync(ct);

                Logger.Info($"File opened successfully: {_totalDuration.TotalSeconds:F1}s duration, {_timestampIndex.Count} index entries");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to open recording file: {path}");
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Reads a packet at or near the specified timestamp
        /// </summary>
        public AudioPacketMetadata? ReadByTimestamp(TimeSpan targetTime)
        {
            if (_reader == null || _fileStream == null)
                throw new InvalidOperationException("File not opened");

            lock (_indexLock)
            {
                // Find closest index entry
                var closestEntry = _timestampIndex
                    .Where(kvp => kvp.Key <= targetTime)
                    .OrderByDescending(kvp => kvp.Key)
                    .FirstOrDefault();

                if (closestEntry.Key == default)
                {
                    // No index entry before target time, start from beginning
                    _fileStream.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    // Seek to closest index position
                    _fileStream.Seek(closestEntry.Value, SeekOrigin.Begin);
                }

                // Read forward until we find a packet at or after target time
                while (_fileStream.Position < _fileStream.Length)
                {
                    try
                    {
                        if (AudioPacketMetadata.TryReadMetadata(_reader, out var metadata) && metadata != null)
                        {
                            var packetTime = metadata.Timestamp - (_recordingStart ?? metadata.Timestamp);
                            
                            if (packetTime >= targetTime)
                            {
                                // Apply filters if set
                                if (ShouldIncludePacket(metadata))
                                {
                                    return metadata;
                                }
                            }
                        }
                        else
                        {
                            break; // End of readable data
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Error reading packet at position {_fileStream.Position}");
                        break;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Follows the tail of the file, yielding new packets as they are written
        /// </summary>
        public async IAsyncEnumerable<AudioPacketMetadata> TailFollowAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            if (_reader == null || _fileStream == null)
                throw new InvalidOperationException("File not opened");

            _tailFollowCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _tailFollowCts.Token;

            Logger.Info("Starting tail-follow mode");

            // Seek to last known position or end of file
            _fileStream.Seek(_lastKnownPosition > 0 ? _lastKnownPosition : _fileStream.Length, SeekOrigin.Begin);

            while (!token.IsCancellationRequested)
            {
                var currentPosition = _fileStream.Position;

                // Check if new data is available
                _fileStream.Seek(0, SeekOrigin.End);
                var fileLength = _fileStream.Position;
                _fileStream.Seek(currentPosition, SeekOrigin.Begin);

                if (currentPosition >= fileLength)
                {
                    // No new data, wait a bit
                    await Task.Delay(100, token);
                    continue;
                }

                // Read new packets
                var newPackets = new List<AudioPacketMetadata>();

                while (_fileStream.Position < fileLength && !token.IsCancellationRequested)
                {
                    AudioPacketMetadata? metadata = null;
                    var shouldYield = false;

                    try
                    {
                        if (AudioPacketMetadata.TryReadMetadata(_reader, out metadata) && metadata != null)
                        {
                            _lastKnownPosition = _fileStream.Position;

                            // Discover new frequencies/players
                            DiscoverMetadata(metadata);

                            // Apply filters
                            if (ShouldIncludePacket(metadata))
                            {
                                newPackets.Add(metadata);
                                shouldYield = true;
                            }
                        }
                        else
                        {
                            break; // End of readable data
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Error reading packet during tail-follow at position {_fileStream.Position}");
                        break;
                    }

                    if (shouldYield && metadata != null)
                    {
                        yield return metadata;
                    }
                }

                // Notify listeners of new data
                if (newPackets.Count > 0)
                {
                    OnNewDataAvailable?.Invoke(this, new WaveformDataEventArgs(newPackets));
                }
            }

            Logger.Info("Tail-follow mode stopped");
        }

        /// <summary>
        /// Sets filtering predicates for coalition, frequency, and player
        /// </summary>
        public void SetFilter(
            Func<Coalition, bool>? coalitionFilter = null,
            Func<double, bool>? frequencyFilter = null,
            Func<PlayerInfo, bool>? playerFilter = null)
        {
            _coalitionFilter = coalitionFilter;
            _frequencyFilter = frequencyFilter;
            _playerFilter = playerFilter;

            var filterCount = (coalitionFilter != null ? 1 : 0) + (frequencyFilter != null ? 1 : 0) + (playerFilter != null ? 1 : 0);
            Logger.Debug($"Filters updated: {filterCount} active filter(s)");
        }

        /// <summary>
        /// Gets the total duration of the recording
        /// </summary>
        public TimeSpan GetDuration() => _totalDuration;

        /// <summary>
        /// Gets the number of index entries (for diagnostics)
        /// </summary>
        public int GetIndexSize()
        {
            lock (_indexLock)
            {
                return _timestampIndex.Count;
            }
        }

        /// <summary>
        /// Gets the set of discovered frequencies
        /// </summary>
        public IReadOnlySet<double> GetDiscoveredFrequencies() => _discoveredFrequencies.ToHashSet();

        /// <summary>
        /// Gets the set of discovered player GUIDs
        /// </summary>
        public IReadOnlySet<string> GetDiscoveredPlayers() => _discoveredPlayers.ToHashSet();

        /// <summary>
        /// Gets recording metadata
        /// </summary>
        public RecordingMetadata GetMetadata()
        {
            return new RecordingMetadata
            {
                FilePath = _filePath ?? string.Empty,
                Format = _detectedFormat,
                TotalDuration = _totalDuration,
                RecordingStart = _recordingStart,
                FrequencyCount = _discoveredFrequencies.Count,
                PlayerCount = _discoveredPlayers.Count,
                IndexEntries = _timestampIndex.Count
            };
        }

        private async Task<FileFormat> DetectFileFormatAsync(CancellationToken ct)
        {
            if (_fileStream == null)
                throw new InvalidOperationException("File stream not initialized");

            // Read first few bytes to detect format
            var headerBytes = new byte[16];
            _fileStream.Seek(0, SeekOrigin.Begin);
            await _fileStream.ReadAsync(headerBytes, ct);
            _fileStream.Seek(0, SeekOrigin.Begin);

            // Check for ADB signature (magic bytes)
            // ADB format starts with: "ADB\0" followed by version number
            if (headerBytes.Length >= 4 && 
                headerBytes[0] == 'A' && 
                headerBytes[1] == 'D' && 
                headerBytes[2] == 'B' && 
                headerBytes[3] == 0)
            {
                Logger.Debug("Detected ADB format");
                return FileFormat.ADB;
            }

            // Otherwise assume RAW format (legacy)
            Logger.Debug("Detected RAW format (legacy)");
            return FileFormat.RAW;
        }

        private async Task BuildIndexAsync(CancellationToken ct)
        {
            if (_reader == null || _fileStream == null)
                throw new InvalidOperationException("File not opened");

            Logger.Info("Building timestamp index...");

            _fileStream.Seek(0, SeekOrigin.Begin);
            var indexInterval = TimeSpan.FromSeconds(1); // Index every 1 second
            var lastIndexedTime = TimeSpan.Zero;
            var packetCount = 0;

            while (_fileStream.Position < _fileStream.Length && !ct.IsCancellationRequested)
            {
                var position = _fileStream.Position;

                try
                {
                    if (AudioPacketMetadata.TryReadMetadata(_reader, out var metadata) && metadata != null)
                    {
                        packetCount++;

                        // Record first packet time
                        if (_recordingStart == null)
                        {
                            _recordingStart = metadata.Timestamp;
                        }

                        var packetTime = metadata.Timestamp - _recordingStart.Value;

                        // Add to index at regular intervals
                        if (packetTime - lastIndexedTime >= indexInterval)
                        {
                            lock (_indexLock)
                            {
                                _timestampIndex[packetTime] = position;
                            }
                            lastIndexedTime = packetTime;
                        }

                        // Update total duration
                        _totalDuration = packetTime;

                        // Discover metadata
                        DiscoverMetadata(metadata);

                        // Log progress periodically
                        if (packetCount % 5000 == 0)
                        {
                            Logger.Debug($"Indexed {packetCount} packets, duration: {_totalDuration.TotalSeconds:F1}s");
                        }
                    }
                    else
                    {
                        break; // End of readable data
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error building index at position {position}, stopping");
                    break;
                }
            }

            _indexBuilt = true;
            _lastKnownPosition = _fileStream.Position;

            Logger.Info($"Index built: {_timestampIndex.Count} entries, {packetCount} packets, {_totalDuration.TotalSeconds:F1}s duration");

            // Return to start
            _fileStream.Seek(0, SeekOrigin.Begin);
        }

        private void DiscoverMetadata(AudioPacketMetadata metadata)
        {
            // Track discovered frequencies
            if (!_discoveredFrequencies.Contains(metadata.Frequency))
            {
                _discoveredFrequencies.Add(metadata.Frequency);
                OnFrequencyDiscovered?.Invoke(this, new FrequencyDiscoveredEventArgs(metadata.Frequency));
                Logger.Debug($"Discovered new frequency: {metadata.Frequency / 1_000_000.0:F3} MHz");
            }

            // Track discovered players
            if (!string.IsNullOrEmpty(metadata.TransmitterGuid) && !_discoveredPlayers.Contains(metadata.TransmitterGuid))
            {
                _discoveredPlayers.Add(metadata.TransmitterGuid);
                OnPlayerDiscovered?.Invoke(this, new PlayerDiscoveredEventArgs(metadata.PlayerData));
                Logger.Debug($"Discovered new player: {metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid}");
            }
        }

        private bool ShouldIncludePacket(AudioPacketMetadata metadata)
        {
            // Apply coalition filter
            if (_coalitionFilter != null)
            {
                var coalition = (Coalition)metadata.Coalition;
                if (!_coalitionFilter(coalition))
                    return false;
            }

            // Apply frequency filter
            if (_frequencyFilter != null && !_frequencyFilter(metadata.Frequency))
                return false;

            // Apply player filter
            if (_playerFilter != null && metadata.PlayerData != null && !_playerFilter(metadata.PlayerData))
                return false;

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _tailFollowCts?.Cancel();
            _tailFollowCts?.Dispose();

            _reader?.Dispose();
            _fileStream?.Dispose();

            _timestampIndex.Clear();
            _discoveredFrequencies.Clear();
            _discoveredPlayers.Clear();

            _disposed = true;
            Logger.Debug("PacketStreamReader disposed");
        }
    }

    /// <summary>
    /// File format detection result
    /// </summary>
    public enum FileFormat
    {
        Unknown,
        RAW,  // Legacy format
        ADB   // New compressed format
    }

    /// <summary>
    /// Recording metadata
    /// </summary>
    public class RecordingMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public FileFormat Format { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public DateTime? RecordingStart { get; set; }
        public int FrequencyCount { get; set; }
        public int PlayerCount { get; set; }
        public int IndexEntries { get; set; }
    }

    /// <summary>
    /// Event args for waveform data updates
    /// </summary>
    public class WaveformDataEventArgs : EventArgs
    {
        public IReadOnlyList<AudioPacketMetadata> NewPackets { get; }

        public WaveformDataEventArgs(IReadOnlyList<AudioPacketMetadata> newPackets)
        {
            NewPackets = newPackets;
        }
    }

    /// <summary>
    /// Event args for frequency discovery
    /// </summary>
    public class FrequencyDiscoveredEventArgs : EventArgs
    {
        public double Frequency { get; }

        public FrequencyDiscoveredEventArgs(double frequency)
        {
            Frequency = frequency;
        }
    }

    /// <summary>
    /// Event args for player discovery
    /// </summary>
    public class PlayerDiscoveredEventArgs : EventArgs
    {
        public PlayerInfo? Player { get; }

        public PlayerDiscoveredEventArgs(PlayerInfo? player)
        {
            Player = player;
        }
    }

    /// <summary>
    /// Coalition enumeration for filtering
    /// </summary>
    public enum Coalition
    {
        Spectator = 0,
        Red = 1,
        Blue = 2
    }
}
