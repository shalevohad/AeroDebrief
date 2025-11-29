using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NLog;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// High-performance adaptive jitter buffer optimized for voice packet playback.
    /// Handles packet reordering and silence insertion for gaps (SRS-inspired).
    /// </summary>
    public sealed class JitterBuffer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SortedDictionary<ulong, RadioPacket> _buffer;
        private readonly int _targetDepthMs;
        private readonly int _maxCapacity;
        private readonly int _maxGapMs; // Maximum time to wait for missing packet
        private readonly object _lock = new();
        
        private ulong _lastEmittedPacketId;
        private ulong _lastSequentialPacketId; // Track the last packet emitted sequentially (without gaps)
        
        // OPTIMIZED: Track timestamps incrementally without full scans
        private DateTime _earliestBufferedTimestamp;
        private DateTime _latestBufferedTimestamp;
        
        // Use Interlocked for thread-safe counters (SRS pattern)
        private long _packetsAdded;
        private long _packetsEmitted;
        private long _packetsDropped;
        private long _latePackets;
        private long _silencePacketsInserted; // NEW: Track silence insertions
        private int _currentDepth;

        public int CurrentDepth => _currentDepth;
        public TimeSpan CurrentBufferDuration => _latestBufferedTimestamp - _earliestBufferedTimestamp;
        public long PacketsAdded => Interlocked.Read(ref _packetsAdded);
        public long PacketsEmitted => Interlocked.Read(ref _packetsEmitted);
        public long PacketsDropped => Interlocked.Read(ref _packetsDropped);
        public long LatePackets => Interlocked.Read(ref _latePackets);
        public long SilencePacketsInserted => Interlocked.Read(ref _silencePacketsInserted);

        public JitterBuffer(int targetDepthMs = 60, int maxCapacity = 200, int maxGapMs = 100)
        {
            _targetDepthMs = targetDepthMs;
            _maxCapacity = maxCapacity;
            _maxGapMs = maxGapMs;
            _buffer = new SortedDictionary<ulong, RadioPacket>();
            _lastEmittedPacketId = 0;
            _lastSequentialPacketId = 0;
            _earliestBufferedTimestamp = DateTime.MinValue;
            _latestBufferedTimestamp = DateTime.MinValue;
        }

        /// <summary>
        /// OPTIMIZED: Fast packet addition with incremental timestamp tracking
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public IEnumerable<RadioPacket> AddPacket(RadioPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            Interlocked.Increment(ref _packetsAdded);

            lock (_lock)
            {
                // Fast path: Check late packet
                if (packet.PacketId <= _lastSequentialPacketId && _lastSequentialPacketId > 0)
                {
                    Interlocked.Increment(ref _latePackets);
                    return Array.Empty<RadioPacket>();
                }

                // Check for duplicate
                if (_buffer.ContainsKey(packet.PacketId))
                {
                    return Array.Empty<RadioPacket>();
                }

                // Add to buffer
                _buffer[packet.PacketId] = packet;
                _currentDepth = _buffer.Count;

                // OPTIMIZED: Incremental timestamp tracking (no full scan)
                if (_earliestBufferedTimestamp == DateTime.MinValue || packet.Timestamp < _earliestBufferedTimestamp)
                {
                    _earliestBufferedTimestamp = packet.Timestamp;
                }
                if (_latestBufferedTimestamp == DateTime.MinValue || packet.Timestamp > _latestBufferedTimestamp)
                {
                    _latestBufferedTimestamp = packet.Timestamp;
                }

                // Handle overflow - drop oldest BY TIMESTAMP (not PacketId) to better handle shuffled data
                if (_buffer.Count > _maxCapacity)
                {
                    // Find packet with oldest timestamp (likely to be emitted soon anyway)
                    var oldestByTime = _buffer.Values.OrderBy(p => p.Timestamp).First();
                    _buffer.Remove(oldestByTime.PacketId);
                    Interlocked.Increment(ref _packetsDropped);
                    Logger.Warn($"Buffer overflow - dropped packet: Id={oldestByTime.PacketId} (oldest by timestamp)");
                    
                    // If we dropped the earliest timestamp, recalculate it
                    if (oldestByTime.Timestamp == _earliestBufferedTimestamp && _buffer.Count > 0)
                    {
                        _earliestBufferedTimestamp = _buffer.Values.Min(p => p.Timestamp);
                    }
                    
                    _currentDepth = _buffer.Count;
                }

                // OPTIMIZED: Fast-path emission check
                var bufferDurationMs = (_latestBufferedTimestamp - _earliestBufferedTimestamp).TotalMilliseconds;
                
                // Emit when duration >= target OR buffer has 10+ packets (prevents deadlock on shuffled data)
                if (bufferDurationMs >= _targetDepthMs || _buffer.Count >= 10)
                {
                    return EmitPacketsLocked();
                }

                return Array.Empty<RadioPacket>();
            }
        }

        /// <summary>
        /// SRS-INSPIRED: Creates a silence packet to fill gaps in voice transmission
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RadioPacket CreateSilencePacket(ulong packetId, RadioPacket templatePacket)
        {
            // Calculate timestamp based on packet spacing (typically 40ms for Opus frames)
            var timestamp = templatePacket.Timestamp.AddMilliseconds(-40); // One packet duration back
            
            return new RadioPacket
            {
                PacketId = packetId,
                Timestamp = timestamp,
                Frequency = templatePacket.Frequency,
                Modulation = templatePacket.Modulation,
                Encryption = templatePacket.Encryption,
                TransmitterUnitId = templatePacket.TransmitterUnitId,
                TransmitterGuid = templatePacket.TransmitterGuid,
                Coalition = templatePacket.Coalition,
                SampleRate = templatePacket.SampleRate,
                ChannelCount = templatePacket.ChannelCount,
                AudioPayload = new byte[0], // Empty payload = silence
                PlayerData = templatePacket.PlayerData
            };
        }

        /// <summary>
        /// OPTIMIZED: Streamlined emission with silence insertion for gaps (SRS pattern)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<RadioPacket> EmitPacketsLocked()
        {
            var emitted = new List<RadioPacket>(Math.Min(_buffer.Count, 32));

            while (_buffer.Count > 0)
            {
                var first = _buffer.First();
                var firstKey = first.Key;

                // Determine expected next packet
                ulong expectedNextId = _lastSequentialPacketId == 0 
                    ? _buffer.Keys.Min() 
                    : _lastSequentialPacketId + 1;
                
                bool isSequential = firstKey == expectedNextId;
                bool shouldForceEmit = false;

                // Check if we should force emission past a gap
                if (!isSequential && firstKey > expectedNextId)
                {
                    ulong gapSize = firstKey - expectedNextId;
                    
                    // SRS-INSPIRED: Insert silence for small gaps (max 4 packets like SRS)
                    // This maintains audio timing while handling missing voice packets
                    if (gapSize <= 4)
                    {
                        // Insert silence packets to fill the gap
                        for (ulong i = expectedNextId; i < firstKey && i < expectedNextId + 4; i++)
                        {
                            var silencePacket = CreateSilencePacket(i, first.Value);
                            emitted.Add(silencePacket);
                            Interlocked.Increment(ref _packetsEmitted);
                            Interlocked.Increment(ref _silencePacketsInserted);
                            _lastSequentialPacketId = i;
                        }
                        
                        Logger.Debug($"Inserted {Math.Min(gapSize, 4)} silence packets for gap: {expectedNextId} to {firstKey - 1}");
                        
                        // Now the current packet becomes sequential
                        isSequential = true;
                    }
                    else
                    {
                        // Large gap - force emit conditions:
                        // 1. Buffer >= 80% capacity (prevent overflow)
                        // 2. Buffer >= 10 packets (prevent blocking on shuffled data)
                        if (_buffer.Count >= (_maxCapacity * 4 / 5) || _buffer.Count >= 10)
                        {
                            shouldForceEmit = true;
                            Logger.Warn($"Forcing emission past large gap ({gapSize} packets): {expectedNextId} to {firstKey}");
                        }
                        else
                        {
                            // Check timestamp gap (prevent waiting forever for missing packets)
                            var nextPacket = _buffer.Values.FirstOrDefault(p => p.PacketId > firstKey);
                            if (nextPacket != null)
                            {
                                var gapDuration = (nextPacket.Timestamp - first.Value.Timestamp).TotalMilliseconds;
                                if (gapDuration > _maxGapMs)
                                {
                                    shouldForceEmit = true;
                                    Logger.Warn($"Forcing emission due to time gap: {gapDuration:F1}ms");
                                }
                            }
                        }
                    }
                }

                if (isSequential || shouldForceEmit)
                {
                    // Emit packet
                    _buffer.Remove(firstKey);
                    _lastEmittedPacketId = firstKey;
                    
                    if (isSequential)
                    {
                        _lastSequentialPacketId = firstKey;
                    }
                    
                    emitted.Add(first.Value);
                    Interlocked.Increment(ref _packetsEmitted);
                    _currentDepth = _buffer.Count;

                    // OPTIMIZED: Update timestamps incrementally
                    if (_buffer.Count == 0)
                    {
                        _earliestBufferedTimestamp = DateTime.MinValue;
                        _latestBufferedTimestamp = DateTime.MinValue;
                    }
                    else if (first.Value.Timestamp == _earliestBufferedTimestamp)
                    {
                        // Only recalculate if we emitted the earliest packet
                        _earliestBufferedTimestamp = _buffer.Values.Min(p => p.Timestamp);
                    }
                }
                else
                {
                    // Gap in sequence, stop emitting
                    break;
                }
            }

            return emitted;
        }

        /// <summary>
        /// Forces emission of all buffered packets (optimized bulk operation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public IEnumerable<RadioPacket> Flush()
        {
            lock (_lock)
            {
                var allPackets = new List<RadioPacket>(_buffer.Count);
                
                // SRS-INSPIRED: Fill gaps with silence during flush
                if (_buffer.Count > 0)
                {
                    var orderedPackets = _buffer.Values.OrderBy(p => p.PacketId).ToList();
                    ulong lastId = 0;
                    
                    foreach (var packet in orderedPackets)
                    {
                        // Fill small gaps with silence
                        if (lastId > 0 && packet.PacketId > lastId + 1)
                        {
                            ulong gapSize = packet.PacketId - (lastId + 1);
                            if (gapSize <= 4) // Max 4 silence packets like SRS
                            {
                                for (ulong i = lastId + 1; i < packet.PacketId && i < lastId + 5; i++)
                                {
                                    var silencePacket = CreateSilencePacket(i, packet);
                                    allPackets.Add(silencePacket);
                                    Interlocked.Increment(ref _packetsEmitted);
                                    Interlocked.Increment(ref _silencePacketsInserted);
                                }
                            }
                        }
                        
                        allPackets.Add(packet);
                        _lastEmittedPacketId = packet.PacketId;
                        Interlocked.Increment(ref _packetsEmitted);
                        lastId = packet.PacketId;
                    }
                }
                
                _buffer.Clear();
                _currentDepth = 0;
                _earliestBufferedTimestamp = DateTime.MinValue;
                _latestBufferedTimestamp = DateTime.MinValue;

                Logger.Debug($"Flushed {allPackets.Count} packets from buffer ({_silencePacketsInserted} silence packets inserted)");
                return allPackets;
            }
        }

        /// <summary>
        /// Gets buffer statistics (minimal allocation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JitterBufferStats GetStats()
        {
            lock (_lock)
            {
                return new JitterBufferStats
                {
                    CurrentDepth = _currentDepth,
                    BufferDurationMs = (_latestBufferedTimestamp - _earliestBufferedTimestamp).TotalMilliseconds,
                    PacketsAdded = PacketsAdded,
                    PacketsEmitted = PacketsEmitted,
                    PacketsDropped = PacketsDropped,
                    LatePackets = LatePackets,
                    SilencePacketsInserted = SilencePacketsInserted,
                    TargetDepthMs = _targetDepthMs,
                    BufferUtilization = (double)_currentDepth / _maxCapacity
                };
            }
        }

        /// <summary>
        /// Resets the buffer state (bulk operation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Reset()
        {
            lock (_lock)
            {
                _buffer.Clear();
                _lastEmittedPacketId = 0;
                _lastSequentialPacketId = 0;
                _earliestBufferedTimestamp = DateTime.MinValue;
                _latestBufferedTimestamp = DateTime.MinValue;
                
                Interlocked.Exchange(ref _currentDepth, 0);
                Interlocked.Exchange(ref _packetsAdded, 0);
                Interlocked.Exchange(ref _packetsEmitted, 0);
                Interlocked.Exchange(ref _packetsDropped, 0);
                Interlocked.Exchange(ref _latePackets, 0);
                Interlocked.Exchange(ref _silencePacketsInserted, 0);
                
                Logger.Debug("JitterBuffer reset");
            }
        }
    }

    /// <summary>
    /// Statistics struct for jitter buffer (minimal heap allocation)
    /// </summary>
    public readonly struct JitterBufferStats
    {
        public int CurrentDepth { get; init; }
        public double BufferDurationMs { get; init; }
        public long PacketsAdded { get; init; }
        public long PacketsEmitted { get; init; }
        public long PacketsDropped { get; init; }
        public long LatePackets { get; init; }
        public long SilencePacketsInserted { get; init; } // NEW
        public int TargetDepthMs { get; init; }
        public double BufferUtilization { get; init; }

        public override string ToString()
        {
            return $"Depth={CurrentDepth}, Duration={BufferDurationMs:F1}ms (target={TargetDepthMs}ms), " +
                   $"Added={PacketsAdded}, Emitted={PacketsEmitted}, Dropped={PacketsDropped}, " +
                   $"Late={LatePackets}, Silence={SilencePacketsInserted}, Utilization={BufferUtilization:P1}";
        }
    }
}
