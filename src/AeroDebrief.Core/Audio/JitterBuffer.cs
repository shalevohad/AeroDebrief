using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using NLog;
using AeroDebrief.Core.IO;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// High-performance adaptive jitter buffer with 60ms target depth based on PTS.
    /// Optimized with SRS-inspired techniques: lock-free operations, minimal allocations.
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
        private DateTime _headPacketTimestamp;
        private DateTime _tailPacketTimestamp;
        
        // Use Interlocked for thread-safe counters (SRS pattern)
        private long _packetsAdded;
        private long _packetsEmitted;
        private long _packetsDropped;
        private long _latePackets;
        private int _currentDepth;

        public int CurrentDepth => _currentDepth;
        public TimeSpan CurrentBufferDuration => _tailPacketTimestamp - _headPacketTimestamp;
        public long PacketsAdded => Interlocked.Read(ref _packetsAdded);
        public long PacketsEmitted => Interlocked.Read(ref _packetsEmitted);
        public long PacketsDropped => Interlocked.Read(ref _packetsDropped);
        public long LatePackets => Interlocked.Read(ref _latePackets);

        public JitterBuffer(int targetDepthMs = 60, int maxCapacity = 200, int maxGapMs = 100)
        {
            _targetDepthMs = targetDepthMs;
            _maxCapacity = maxCapacity;
            _maxGapMs = maxGapMs;
            _buffer = new SortedDictionary<ulong, RadioPacket>();
            _lastEmittedPacketId = 0;
            _lastSequentialPacketId = 0;
            _headPacketTimestamp = DateTime.MinValue;
            _tailPacketTimestamp = DateTime.MinValue;
        }

        /// <summary>
        /// Optimized packet addition with minimal lock time and bulk operations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public IEnumerable<RadioPacket> AddPacket(RadioPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            Interlocked.Increment(ref _packetsAdded);

            lock (_lock)
            {
                // Fast path: Check late packet - drop if it was emitted sequentially
                // Packets that were skipped due to gap forcing can still arrive later
                // Only drop packets that are truly before the last sequential emission
                if (packet.PacketId <= _lastSequentialPacketId && _lastSequentialPacketId > 0)
                {
                    Interlocked.Increment(ref _latePackets);
                    Logger.Trace($"Late packet dropped: Id={packet.PacketId}, last sequential={_lastSequentialPacketId}");
                    return Array.Empty<RadioPacket>();
                }

                // Check for duplicate (already in buffer)
                if (_buffer.ContainsKey(packet.PacketId))
                {
                    Logger.Trace($"Duplicate packet dropped: Id={packet.PacketId}");
                    return Array.Empty<RadioPacket>();
                }

                // Add to buffer
                _buffer[packet.PacketId] = packet;

                // Update timestamps - use the earliest packet's timestamp as head
                if (_headPacketTimestamp == DateTime.MinValue || (_buffer.Count > 0 && _buffer.Values.First().Timestamp < _headPacketTimestamp))
                {
                    _headPacketTimestamp = _buffer.Values.First().Timestamp;
                }
                
                // Tail is always the latest timestamp
                if (_tailPacketTimestamp == DateTime.MinValue || packet.Timestamp > _tailPacketTimestamp)
                {
                    _tailPacketTimestamp = packet.Timestamp;
                }

                // Drop oldest if overflow (SRS pattern - maintain capacity) - DO THIS BEFORE EMISSION DECISION
                while (_buffer.Count > _maxCapacity)
                {
                    var oldest = _buffer.First();
                    _buffer.Remove(oldest.Key);
                    Interlocked.Increment(ref _packetsDropped);
                    Logger.Warn($"Buffer overflow - dropped packet: Id={oldest.Key}");
                    
                    // Update head timestamp after dropping
                    if (_buffer.Count > 0)
                    {
                        _headPacketTimestamp = _buffer.Values.First().Timestamp;
                    }
                }

                // Update depth AFTER overflow handling
                _currentDepth = _buffer.Count;

                // Calculate buffer duration using latest PTS minus head PTS
                var bufferDurationMs = (_tailPacketTimestamp - _headPacketTimestamp).TotalMilliseconds;

                // Emit packets ONLY when buffer depth >= target duration
                // Do NOT use capacity-based emission here - that's only for gap handling in EmitPacketsLocked
                if (bufferDurationMs >= _targetDepthMs)
                {
                    return EmitPacketsLocked();
                }

                return Array.Empty<RadioPacket>();
            }
        }

        /// <summary>
        /// Emits packets in order (must be called within lock)
        /// Uses List<T> pre-allocation to reduce allocations
        /// Handles gaps with timeout mechanism
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<RadioPacket> EmitPacketsLocked()
        {
            var emitted = new List<RadioPacket>(Math.Min(_buffer.Count, 32)); // Pre-allocate reasonable size

            while (_buffer.Count > 0)
            {
                var first = _buffer.First();
                var firstKey = first.Key;

                // Check if this is the next sequential packet
                ulong expectedNextId = _lastSequentialPacketId == 0 ? _buffer.Keys.Min() : _lastSequentialPacketId + 1;
                bool isSequential = firstKey == expectedNextId;

                // Force emission if buffer is 80% full (capacity pressure)
                bool shouldForceEmit = _buffer.Count >= (_maxCapacity * 4 / 5);
                
                // Check for sequence gap
                if (!shouldForceEmit && !isSequential)
                {
                    // There's a gap - check if we should skip the missing packet(s)
                    bool hasGap = firstKey > expectedNextId;
                    
                    if (hasGap)
                    {
                        // We have a gap. Check if we should give up waiting for missing packet(s):
                        // 1. Buffer is 80% full (capacity pressure) - force emission to prevent overflow
                        // 2. Buffer duration exceeds 2x target depth (we've waited long enough)
                        // 3. Timestamp gap between first buffered packet and next packet is too large
                        
                        ulong gapSize = firstKey - expectedNextId;
                        
                        if (_buffer.Count >= (_maxCapacity * 4 / 5))
                        {
                            Logger.Warn($"Forcing emission past gap due to capacity: Expected {expectedNextId}, have {firstKey}, buffer depth={_buffer.Count}");
                            shouldForceEmit = true;
                        }
                        else if (_buffer.Count >= 10)
                        {
                            // Force past gap when buffer has accumulated 10+ packets
                            Logger.Warn($"Forcing emission past gap: Expected {expectedNextId}, have {firstKey}, buffer depth={_buffer.Count}");
                            shouldForceEmit = true;
                        }
                        else
                        {
                            // Check timestamp gap between consecutive packets
                            var packetsAfterFirst = _buffer.Where(p => p.Key > firstKey).Take(1).ToList();
                            if (packetsAfterFirst.Any())
                            {
                                var gapDuration = (packetsAfterFirst[0].Value.Timestamp - first.Value.Timestamp).TotalMilliseconds;
                                if (gapDuration > _maxGapMs)
                                {
                                    Logger.Warn($"Gap timeout: Missing packets before {firstKey}, gap={gapDuration:F1}ms");
                                    shouldForceEmit = true;
                                }
                            }
                        }
                    }
                }

                if (isSequential || shouldForceEmit)
                {
                    _buffer.Remove(firstKey);
                    _lastEmittedPacketId = firstKey;
                    
                    // Only update sequential ID if this was actually sequential
                    if (isSequential)
                    {
                        _lastSequentialPacketId = firstKey;
                    }
                    
                    emitted.Add(first.Value);
                    Interlocked.Increment(ref _packetsEmitted);
                    Interlocked.Decrement(ref _currentDepth);

                    // Update head timestamp
                    if (_buffer.Count > 0)
                    {
                        _headPacketTimestamp = _buffer.Values.First().Timestamp;
                    }
                    else
                    {
                        _headPacketTimestamp = DateTime.MinValue;
                        _tailPacketTimestamp = DateTime.MinValue;
                    }
                }
                else
                {
                    break; // Gap in sequence and no timeout yet
                }
            }

            if (emitted.Count > 0)
            {
                var bufferDurationMs = _buffer.Count > 0 ? (_tailPacketTimestamp - _headPacketTimestamp).TotalMilliseconds : 0;
                Logger.Trace($"Emitted {emitted.Count} packets, buffer={_buffer.Count}, duration={bufferDurationMs:F1}ms");
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
                allPackets.AddRange(_buffer.Values.OrderBy(p => p.PacketId));
                
                _buffer.Clear();
                _currentDepth = 0;
                
                foreach (var packet in allPackets)
                {
                    _lastEmittedPacketId = packet.PacketId;
                    Interlocked.Increment(ref _packetsEmitted);
                }

                Logger.Debug($"Flushed {allPackets.Count} packets from buffer");
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
                    BufferDurationMs = (_tailPacketTimestamp - _headPacketTimestamp).TotalMilliseconds,
                    PacketsAdded = PacketsAdded,
                    PacketsEmitted = PacketsEmitted,
                    PacketsDropped = PacketsDropped,
                    LatePackets = LatePackets,
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
                _headPacketTimestamp = DateTime.MinValue;
                _tailPacketTimestamp = DateTime.MinValue;
                
                Interlocked.Exchange(ref _currentDepth, 0);
                Interlocked.Exchange(ref _packetsAdded, 0);
                Interlocked.Exchange(ref _packetsEmitted, 0);
                Interlocked.Exchange(ref _packetsDropped, 0);
                Interlocked.Exchange(ref _latePackets, 0);
                
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
        public int TargetDepthMs { get; init; }
        public double BufferUtilization { get; init; }

        public override string ToString()
        {
            return $"Depth={CurrentDepth}, Duration={BufferDurationMs:F1}ms (target={TargetDepthMs}ms), " +
                   $"Added={PacketsAdded}, Emitted={PacketsEmitted}, Dropped={PacketsDropped}, " +
                   $"Late={LatePackets}, Utilization={BufferUtilization:P1}";
        }
    }
}
