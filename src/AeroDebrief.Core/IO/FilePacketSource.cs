using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// High-performance packet source using memory-mapped files and PTS-sorted secondary index.
    /// Supports efficient seeking and range queries for large recording files.
    /// </summary>
    public sealed class FilePacketSource : IPacketSource
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly string _filePath;
        private readonly string _indexPath;
        private FileStream? _fileStream;
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private PacketIndex? _index;
        private bool _disposed;

        public long TotalPackets => _index?.Entries.Count ?? 0;
        public TimeSpan TotalDuration => _index?.TotalDuration ?? TimeSpan.Zero;
        
        /// <summary>
        /// Gets the recording start time (timestamp of first packet)
        /// </summary>
        public DateTime RecordingStart => _index?.Entries.FirstOrDefault().Timestamp ?? DateTime.UtcNow;

        public FilePacketSource(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));
            
            _indexPath = Path.ChangeExtension(filePath, ".pkidx");
        }

        /// <summary>
        /// Opens the file and builds/loads the packet index
        /// </summary>
        public async Task OpenAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException($"Recording file not found: {_filePath}");
            
            Logger.Info($"Opening file packet source: {_filePath}");
            progress?.Report("Opening file...");

            // Open file stream for memory mapping
            _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            // Create memory-mapped file
            _mmf = MemoryMappedFile.CreateFromFile(
                _fileStream,
                mapName: null,
                capacity: 0,
                access: MemoryMappedFileAccess.Read,
                inheritability: HandleInheritability.None,
                leaveOpen: false);
            
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            progress?.Report("File opened successfully");

            // Load or build index
            bool needsRebuild = false;
            
            if (File.Exists(_indexPath))
            {
                if (await IsIndexValidAsync(cancellationToken))
                {
                    try
                    {
                        Logger.Info("Loading existing packet index...");
                        progress?.Report("Loading packet index...");
                        _index = await PacketIndex.LoadAsync(_indexPath, cancellationToken);
                        Logger.Info($"✅ Loaded index with {_index.Entries.Count} entries");
                        progress?.Report($"Index loaded: {_index.Entries.Count:N0} packets");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to load index - will rebuild");
                        needsRebuild = true;
                    }
                }
                else
                {
                    Logger.Info("Index file is outdated - will rebuild");
                    progress?.Report("Index outdated, rebuilding...");
                    needsRebuild = true;
                }
            }
            else
            {
                Logger.Info("No index file found - will build");
                progress?.Report("Building packet index...");
                needsRebuild = true;
            }
            
            if (needsRebuild || _index == null)
            {
                Logger.Info("Building packet index...");
                progress?.Report("Analyzing file and building index...");
                _index = await BuildIndexAsync(progress, cancellationToken);
                progress?.Report("Saving index...");
                await _index.SaveAsync(_indexPath, cancellationToken);
                Logger.Info($"✅ Built and saved index with {_index.Entries.Count} entries");
                progress?.Report($"Index built: {_index.Entries.Count:N0} packets");
            }
        }

        /// <summary>
        /// Reads packets in PTS (presentation timestamp) order starting from a specific time
        /// </summary>
        public async IAsyncEnumerable<RadioPacket> ReadRange(
            TimeSpan from,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_index == null || _accessor == null)
                throw new InvalidOperationException("FilePacketSource not opened. Call OpenAsync first.");

            // Binary search for start position
            int startIndex = _index.FindIndexByTime(from);
            
            Logger.Debug($"ReadRange from {from}: starting at packet index {startIndex}/{_index.Entries.Count}");

            for (int i = startIndex; i < _index.Entries.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                var entry = _index.Entries[i];
                var packet = ReadPacketAt(entry.FileOffset);
                
                if (packet != null)
                    yield return packet;

                // Yield control periodically
                if (i % 100 == 0)
                    await Task.Yield();
            }
        }

        /// <summary>
        /// Reads packets in batches for improved performance (reduces async overhead).
        /// Recommended for streaming playback.
        /// </summary>
        public async IAsyncEnumerable<RadioPacket[]> ReadRangeBatched(
            TimeSpan from,
            int batchSize = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_index == null || _accessor == null)
                throw new InvalidOperationException("FilePacketSource not opened. Call OpenAsync first.");

            if (batchSize <= 0)
                throw new ArgumentException("Batch size must be positive", nameof(batchSize));

            // Binary search for start position
            int startIndex = _index.FindIndexByTime(from);
            
            Logger.Debug($"ReadRangeBatched from {from}: starting at packet index {startIndex}/{_index.Entries.Count}, batch size={batchSize}");

            var batch = new List<RadioPacket>(batchSize);

            for (int i = startIndex; i < _index.Entries.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var entry = _index.Entries[i];
                var packet = ReadPacketAt(entry.FileOffset);
                
                if (packet != null)
                    batch.Add(packet);

                // Yield batch when full
                if (batch.Count >= batchSize)
                {
                    yield return batch.ToArray();
                    batch.Clear();
                    
                    // Yield control after each batch
                    await Task.Yield();
                }
            }

            // Yield remaining packets
            if (batch.Count > 0)
                yield return batch.ToArray();
        }

        /// <summary>
        /// Gets frequency metadata from the index without reading packets.
        /// Extremely fast - used for UI population and pre-allocation.
        /// NOW includes player information aggregated from index!
        /// </summary>
        public Dictionary<double, FrequencyMetadata> GetFrequencyMetadata()
        {
            if (_index == null)
                throw new InvalidOperationException("FilePacketSource not opened. Call OpenAsync first.");

            Logger.Debug("Extracting frequency metadata with player info from index...");

            // Group by frequency from index (no packet reads - instant!)
            var freqGroups = _index.Entries
                .GroupBy(e => e.Frequency)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var entries = g.ToList();
                        var totalPackets = entries.Count;
                        
                        // Aggregate player information
                        var playerGroups = entries
                            .GroupBy(e => e.TransmitterGuid)
                            .Select(playerGroup =>
                            {
                                var firstEntry = playerGroup.First();
                                var packetCount = playerGroup.Count();
                                
                                return new PlayerFrequencyInfo
                                {
                                    PlayerName = firstEntry.PlayerName,
                                    TransmitterGuid = firstEntry.TransmitterGuid,
                                    Coalition = GetCoalitionName(firstEntry.Coalition),
                                    UnitType = firstEntry.UnitType,
                                    UnitId = firstEntry.UnitId,
                                    PacketCount = packetCount,
                                    ContributionPercent = (double)packetCount / totalPackets * 100.0
                                };
                            })
                            .OrderByDescending(p => p.PacketCount)
                            .ToList();
                        
                        // Get most common modulation
                        var mostCommonModulation = entries
                            .GroupBy(e => e.Modulation)
                            .OrderByDescending(group => group.Count())
                            .First()
                            .Key;
                        
                        return new FrequencyMetadata
                        {
                            Frequency = g.Key,
                            PacketCount = totalPackets,
                            FirstSeen = entries.Min(e => e.Timestamp),
                            LastSeen = entries.Max(e => e.Timestamp),
                            Players = playerGroups,
                            Modulation = mostCommonModulation
                        };
                    });

            Logger.Info($"Extracted metadata for {freqGroups.Count} frequencies with {freqGroups.Sum(f => f.Value.Players.Count)} players from index (0ms - no packet reads!)");

            return freqGroups;
        }
        
        private static string GetCoalitionName(int coalition)
        {
            return coalition switch
            {
                1 => "Red",
                2 => "Blue",
                0 => "Neutral",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Reads a specific packet by its index
        /// </summary>
        public RadioPacket? ReadPacketByIndex(int index)
        {
            if (_index == null || _accessor == null)
                throw new InvalidOperationException("FilePacketSource not opened. Call OpenAsync first.");

            if (index < 0 || index >= _index.Entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var entry = _index.Entries[index];
            return ReadPacketAt(entry.FileOffset);
        }

        private RadioPacket? ReadPacketAt(long offset)
        {
            try
            {
                // Create a stream at the specific offset
                _fileStream!.Seek(offset, SeekOrigin.Begin);
                using var reader = new BinaryReader(_fileStream, System.Text.Encoding.UTF8, leaveOpen: true);

                // Use AudioPacketMetadata.TryReadMetadata for full packet reading
                if (AudioPacketMetadata.TryReadMetadata(reader, out var metadata) && metadata != null)
                {
                    return RadioPacket.FromMetadata(metadata);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to read packet at offset {offset}");
                return null;
            }
        }

        private async Task<PacketIndex> BuildIndexAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var index = new PacketIndex();
            var entries = new List<PacketIndexEntry>();

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            // Skip header
            SkipHeader(reader);

            DateTime? firstTimestamp = null;
            DateTime? lastTimestamp = null;
            
            // Error recovery tracking
            int validPackets = 0;
            int corruptedPackets = 0;
            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 100; // Stop if too many consecutive failures
            long lastValidOffset = fs.Position;
            long lastProgressReport = DateTime.UtcNow.Ticks;

            Logger.Info($"Building packet index for file: {Path.GetFileName(_filePath)} ({fs.Length:N0} bytes)");
            progress?.Report($"Indexing: 0% (0 packets)");

            while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
            {
                var offset = fs.Position;

                // Progress reporting every 0.5 seconds for smoother UI updates
                if (DateTime.UtcNow.Ticks - lastProgressReport > TimeSpan.FromMilliseconds(500).Ticks)
                {
                    var progressPercent = (double)fs.Position / fs.Length * 100.0;
                    var message = $"Indexing: {progressPercent:F1}% ({validPackets:N0} packets)";
                    
                    if (corruptedPackets > 0)
                    {
                        message += $" [{corruptedPackets} corrupted]";
                    }
                    
                    progress?.Report(message);
                    Logger.Info($"Index building: {progressPercent:F1}% ({validPackets:N0} valid, {corruptedPackets:N0} corrupted)");
                    lastProgressReport = DateTime.UtcNow.Ticks;
                }

                if (AudioPacketMetadata.TryReadMetadata(reader, out var metadata) && metadata != null)
                {
                    // Valid packet found
                    firstTimestamp ??= metadata.Timestamp;
                    lastTimestamp = metadata.Timestamp;

                    entries.Add(new PacketIndexEntry
                    {
                        FileOffset = offset,
                        Timestamp = metadata.Timestamp,
                        Frequency = metadata.Frequency,
                        PacketId = metadata.PacketId,
                        
                        // Player metadata
                        Coalition = metadata.PlayerData?.Coalition ?? metadata.Coalition,
                        UnitId = metadata.PlayerData?.AircraftInfo?.UnitId ?? metadata.TransmitterUnitId,
                        PlayerName = metadata.PlayerData?.Name ?? metadata.TransmitterGuid,
                        TransmitterGuid = metadata.TransmitterGuid,
                        UnitType = metadata.PlayerData?.AircraftInfo?.UnitType ?? string.Empty,
                        Modulation = metadata.Modulation
                    });

                    validPackets++;
                    consecutiveFailures = 0;
                    lastValidOffset = fs.Position;

                    if (validPackets % 1000 == 0)
                        await Task.Yield(); // Prevent blocking
                }
                else
                {
                    // Corrupted packet - attempt recovery
                    corruptedPackets++;
                    consecutiveFailures++;

                    if (consecutiveFailures >= maxConsecutiveFailures)
                    {
                        Logger.Warn($"Stopping index build: {maxConsecutiveFailures} consecutive failures at position {offset:N0}");
                        progress?.Report($"Warning: Stopped at {offset:N0} due to corruption");
                        break;
                    }

                    // IMPROVED ERROR RECOVERY: Try to find next valid packet by looking for reasonable timestamp/frequency
                    const int maxSearchBytes = 2048; // Search up to 2KB ahead
                    long startSearchPos = offset;
                    long endSearchPos = Math.Min(startSearchPos + maxSearchBytes, fs.Length);
                    bool foundValidPacket = false;
                    
                    // Try to find a valid packet header by checking for reasonable timestamp + frequency values
                    for (long searchPos = startSearchPos + 1; searchPos < endSearchPos - AudioPacketMetadata.FixedHeaderLength; searchPos++)
                    {
                        try
                        {
                            fs.Seek(searchPos, SeekOrigin.Begin);
                            
                            // Attempt to read timestamp and frequency
                            long ticks = reader.ReadInt64();
                            double freq = reader.ReadDouble();

                            // Check if the read values are within reasonable ranges
                            if (ticks >= Constants.MinValidTimestamp.Ticks && ticks <= Constants.MaxValidTimestamp.Ticks &&
                                freq >= Constants.MinValidFrequencyHz && freq <= Constants.MaxValidFrequencyHz && 
                                !double.IsNaN(freq) && !double.IsInfinity(freq))
                            {
                                // Found what looks like a valid packet header!
                                fs.Seek(searchPos, SeekOrigin.Begin);
                                foundValidPacket = true;
                                Logger.Debug($"Found valid packet signature at {searchPos:N0} (skipped {searchPos - offset} bytes from {offset:N0})");
                                break;
                            }
                        }
                        catch
                        {
                            // Continue searching
                        }
                    }
                    
                    if (!foundValidPacket)
                    {
                        // Couldn't find valid packet, skip forward by reasonable amount
                        long recoverySkipBytes = 512; // Larger skip if we can't find anything
                        long newPosition = Math.Min(offset + recoverySkipBytes, fs.Length);
                        
                        if (newPosition >= fs.Length)
                            break; // EOF reached
                        
                        fs.Seek(newPosition, SeekOrigin.Begin);
                    }
                }
            }

            // Final progress update
            progress?.Report($"Sorting {validPackets:N0} packets...");

            // Sort by PTS (Presentation Time Stamp)
            entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            index.Entries = entries;
            index.TotalDuration = lastTimestamp.HasValue && firstTimestamp.HasValue
                ? lastTimestamp.Value - firstTimestamp.Value
                : TimeSpan.Zero;

            var corruptionRate = validPackets > 0 ? (double)corruptedPackets / (validPackets + corruptedPackets) * 100.0 : 0;
            Logger.Info($"✅ Index built: {validPackets:N0} valid packets, {corruptedPackets:N0} corrupted ({corruptionRate:F2}% corruption rate)");
            progress?.Report($"Index complete: {validPackets:N0} packets ({index.TotalDuration})");
            
            if (corruptionRate > 10.0)
            {
                Logger.Warn($"⚠️ High corruption rate detected ({corruptionRate:F2}%) - file may be damaged");
            }
            else if (corruptionRate > 0)
            {
                Logger.Info($"ℹ️ Minor corruption detected ({corruptionRate:F2}%) - successfully recovered valid packets");
            }

            return index;
        }

        private void SkipHeader(BinaryReader reader)
        {
            try
            {
                var magic = reader.ReadString();
                if (magic == Constants.RECORDING_FILE_MAGIC)
                {
                    reader.ReadString(); // IP
                    reader.ReadInt32();  // Port
                    reader.ReadInt64();  // StartTicks
                }
            }
            catch
            {
                reader.BaseStream.Position = 0;
            }
        }

        private async Task<bool> IsIndexValidAsync(CancellationToken cancellationToken)
        {
            try
            {
                var indexWriteTime = File.GetLastWriteTimeUtc(_indexPath);
                var fileWriteTime = File.GetLastWriteTimeUtc(_filePath);
                
                return indexWriteTime >= fileWriteTime;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _accessor?.Dispose();
            _mmf?.Dispose();
            _fileStream?.Dispose();

            _disposed = true;
            Logger.Debug("FilePacketSource disposed");
        }
    }

    /// <summary>
    /// Frequency metadata extracted from index (no packet reads required)
    /// Now includes aggregated player information per frequency
    /// </summary>
    public class FrequencyMetadata
    {
        public double Frequency { get; set; }
        public int PacketCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public TimeSpan Duration => LastSeen - FirstSeen;
        
        // NEW: Player information aggregated from index
        public List<PlayerFrequencyInfo> Players { get; set; } = new();
        public byte Modulation { get; set; }  // Most common modulation for this frequency
    }
    
    /// <summary>
    /// Player information for a specific frequency (extracted from index)
    /// </summary>
    public class PlayerFrequencyInfo
    {
        public string PlayerName { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public string? Coalition { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public uint UnitId { get; set; }
        public int PacketCount { get; set; }
        public double ContributionPercent { get; set; }
    }

    /// <summary>
    /// PTS-sorted packet index for efficient seeking
    /// </summary>
    internal class PacketIndex
    {
        public List<PacketIndexEntry> Entries { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }

        /// <summary>
        /// Binary search to find packet index at or after specified time
        /// </summary>
        public int FindIndexByTime(TimeSpan from)
        {
            if (Entries.Count == 0)
                return 0;

            var firstTimestamp = Entries[0].Timestamp;
            var targetTime = firstTimestamp.Add(from);

            int left = 0, right = Entries.Count - 1;
            int result = Entries.Count;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (Entries[mid].Timestamp >= targetTime)
                {
                    result = mid;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return Math.Min(result, Entries.Count);
        }

        public async Task SaveAsync(string path, CancellationToken cancellationToken)
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new BinaryWriter(fs);

            // Write index header with version
            writer.Write("PKIDX"); // Magic identifier
            writer.Write(2); // Version 2 (with player metadata)
            writer.Write(Entries.Count);
            writer.Write(TotalDuration.Ticks);

            // Write each entry with player metadata
            foreach (var entry in Entries)
            {
                writer.Write(entry.FileOffset);
                writer.Write(entry.Timestamp.Ticks);
                writer.Write(entry.Frequency);
                writer.Write(entry.PacketId);
                
                // Player metadata (version 2)
                writer.Write(entry.Coalition);
                writer.Write(entry.UnitId);
                writer.Write(entry.PlayerName ?? string.Empty);
                writer.Write(entry.TransmitterGuid ?? string.Empty);
                writer.Write(entry.UnitType ?? string.Empty);
                writer.Write(entry.Modulation);
            }
        }

        public static async Task<PacketIndex> LoadAsync(string path, CancellationToken cancellationToken)
        {
            var logger = LogManager.GetCurrentClassLogger();
            
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                var index = new PacketIndex();
                
                // Try to read version header
                string magic;
                int version = 1; // Default to version 1 for old files
                
                try
                {
                    magic = reader.ReadString();
                    if (magic == "PKIDX")
                    {
                        version = reader.ReadInt32();
                        logger.Debug($"Loading index version {version}");
                    }
                    else
                    {
                        // Old format without magic - rewind and read as count
                        fs.Position = 0;
                        logger.Debug("Loading old index format (version 1)");
                    }
                }
                catch
                {
                    // Old format - rewind
                    fs.Position = 0;
                    logger.Debug("Loading old index format (version 1 - no header)");
                }
                
                int count = reader.ReadInt32();
                long durationTicks = reader.ReadInt64();
                index.TotalDuration = TimeSpan.FromTicks(durationTicks);

                index.Entries = new List<PacketIndexEntry>(count);

                if (version >= 2)
                {
                    // Version 2: with player metadata
                    for (int i = 0; i < count; i++)
                    {
                        index.Entries.Add(new PacketIndexEntry
                        {
                            FileOffset = reader.ReadInt64(),
                            Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                            Frequency = reader.ReadDouble(),
                            PacketId = reader.ReadUInt64(),
                            
                            // Player metadata
                            Coalition = reader.ReadInt32(),
                            UnitId = reader.ReadUInt32(),
                            PlayerName = reader.ReadString(),
                            TransmitterGuid = reader.ReadString(),
                            UnitType = reader.ReadString(),
                            Modulation = reader.ReadByte()
                        });

                        if (i % 1000 == 0)
                            await Task.Yield();
                    }
                }
                else
                {
                    // Version 1: without player metadata - throw to trigger rebuild
                    logger.Info("Old index format detected - will rebuild with player metadata");
                    throw new InvalidDataException("Old index format - needs rebuild");
                }

                return index;
            }
            catch (Exception ex)
            {
                logger = LogManager.GetCurrentClassLogger();
                logger.Warn(ex, $"Failed to load index from {path} - will rebuild");
                throw; // Re-throw to trigger rebuild in OpenAsync
            }
        }
    }

    /// <summary>
    /// Index entry for a single packet - now includes player metadata
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PacketIndexEntry
    {
        public long FileOffset;
        public DateTime Timestamp;
        public double Frequency;
        public ulong PacketId;
        
        // NEW: Player metadata for instant UI population
        public int Coalition;           // Red/Blue/Neutral (1/2/0)
        public uint UnitId;             // Aircraft unit ID
        public string PlayerName;       // Pilot name
        public string TransmitterGuid;  // Unique client GUID
        public string UnitType;         // Aircraft type (e.g., "F-16C_50")
        public byte Modulation;         // AM/FM modulation type

        public PacketIndexEntry()
        {
            FileOffset = 0;
            Timestamp = DateTime.MinValue;
            Frequency = 0;
            PacketId = 0;
            Coalition = 0;
            UnitId = 0;
            PlayerName = string.Empty;
            TransmitterGuid = string.Empty;
            UnitType = string.Empty;
            Modulation = 0;
        }
    }
}
