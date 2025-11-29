using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// High-performance packet router that groups packets by (FrequencyId, SpeakerId) 
    /// and forwards them to appropriate FrequencyWorker instances.
    /// Optimized for parallel routing of millions of packets with minimal contention.
    /// </summary>
    public sealed class PacketRouter : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<double, FrequencyRoutingWorker> _frequencyWorkers;
        private readonly ConcurrentDictionary<(double FrequencyId, string SpeakerId), PacketQueue> _routingTable;
        private readonly PacketRouterStats _stats;
        private bool _disposed;

        /// <summary>
        /// Gets the number of active frequency workers
        /// </summary>
        public int ActiveFrequencyCount => _frequencyWorkers.Count;

        public PacketRouter()
        {
            _frequencyWorkers = new ConcurrentDictionary<double, FrequencyRoutingWorker>();
            _routingTable = new ConcurrentDictionary<(double, string), PacketQueue>();
            _stats = new PacketRouterStats();
        }

        /// <summary>
        /// Pre-allocates frequency workers based on metadata (avoids runtime allocation).
        /// Call this during pipeline initialization for optimal performance.
        /// </summary>
        public void PreAllocateFrequencies(Dictionary<double, FrequencyMetadata> frequencies)
        {
            if (frequencies == null)
                throw new ArgumentNullException(nameof(frequencies));

            foreach (var (freq, metadata) in frequencies)
            {
                _frequencyWorkers.GetOrAdd(freq, f => new FrequencyRoutingWorker(f));
            }

            Logger.Info($"Pre-allocated {frequencies.Count} FrequencyWorkers");
        }

        /// <summary>
        /// Gets all active frequency workers (for MasterMixer registration)
        /// </summary>
        public IEnumerable<FrequencyRoutingWorker> GetActiveFrequencyWorkers()
        {
            return _frequencyWorkers.Values;
        }

        /// <summary>
        /// Routes a single packet asynchronously (ValueTask for zero-allocation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RoutePacketAsync(RadioPacket packet)
        {
            RoutePacket(packet); // Synchronous routing is fast enough
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Routes a single packet to the appropriate frequency worker
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RoutePacket(RadioPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            var key = (packet.Frequency, packet.TransmitterGuid);

            // Fast path: Get or create queue for this frequency-speaker combination
            var queue = _routingTable.GetOrAdd(key, _ => new PacketQueue());
            queue.Enqueue(packet);

            // Get or create frequency worker
            var worker = _frequencyWorkers.GetOrAdd(
                packet.Frequency,
                freq => new FrequencyRoutingWorker(freq));

            // Forward to worker
            worker.EnqueueForUser(packet.TransmitterGuid, packet);

            _stats.IncrementPacketsRouted();
        }

        /// <summary>
        /// Routes multiple packets in batch for better performance
        /// </summary>
        public void RoutePacketBatch(IEnumerable<RadioPacket> packets)
        {
            if (packets == null)
                throw new ArgumentNullException(nameof(packets));

            foreach (var packet in packets)
            {
                RoutePacket(packet);
            }
        }

        /// <summary>
        /// Routes packets in parallel using multiple threads
        /// </summary>
        public async Task RoutePacketsParallelAsync(
            IEnumerable<RadioPacket> packets,
            int degreeOfParallelism = -1,
            CancellationToken cancellationToken = default)
        {
            if (packets == null)
                throw new ArgumentNullException(nameof(packets));

            if (degreeOfParallelism <= 0)
                degreeOfParallelism = Environment.ProcessorCount;

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = degreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(packets, options, packet =>
                {
                    RoutePacket(packet);
                });
            }, cancellationToken);
        }

        /// <summary>
        /// Gets the frequency worker for a specific frequency
        /// </summary>
        public FrequencyRoutingWorker? GetFrequencyWorker(double frequency)
        {
            _frequencyWorkers.TryGetValue(frequency, out var worker);
            return worker;
        }

        /// <summary>
        /// Gets all active frequency workers
        /// </summary>
        public IReadOnlyCollection<FrequencyRoutingWorker> GetAllWorkers()
        {
            return _frequencyWorkers.Values.ToList();
        }

        /// <summary>
        /// Gets routing statistics
        /// </summary>
        public PacketRouterStats GetStats()
        {
            _stats.FrequencyCount = _frequencyWorkers.Count;
            _stats.SpeakerCount = _routingTable.Keys.Select(k => k.SpeakerId).Distinct().Count();
            _stats.RouteCount = _routingTable.Count;
            return _stats;
        }

        /// <summary>
        /// Clears all routing state (for testing/reset)
        /// </summary>
        public void Clear()
        {
            foreach (var worker in _frequencyWorkers.Values)
            {
                worker.Dispose();
            }
            _frequencyWorkers.Clear();
            _routingTable.Clear();
            _stats.Reset();
        }

        public void Dispose()
        {
            if (_disposed) return;

            Clear();
            _disposed = true;

            Logger.Debug("PacketRouter disposed");
        }
    }

    /// <summary>
    /// Worker that processes packets for a specific frequency.
    /// NOW CONNECTED: Routes packets to FrequencyWorker for actual audio processing.
    /// </summary>
    public sealed class FrequencyRoutingWorker : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly double _frequency;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<RadioPacket>> _userQueues;
        private readonly FrequencyWorker _frequencyWorker; // NEW: Actual audio processing worker
        private long _totalPackets;
        private bool _disposed;

        public double Frequency => _frequency;
        public int UserCount => _userQueues.Count;
        public long TotalPackets => Interlocked.Read(ref _totalPackets);
        
        // NEW: Access to the actual FrequencyWorker for audio pipeline
        public FrequencyWorker AudioWorker => _frequencyWorker;

        public FrequencyRoutingWorker(double frequency)
        {
            _frequency = frequency;
            _userQueues = new ConcurrentDictionary<string, ConcurrentQueue<RadioPacket>>();
            
            // NEW: Create the actual FrequencyWorker for audio processing
            _frequencyWorker = new FrequencyWorker(frequency);
            
            Logger.Debug($"FrequencyRoutingWorker created with audio pipeline: {frequency:F0} Hz");
        }

        /// <summary>
        /// Enqueues a packet for a specific user/speaker.
        /// NOW CONNECTED: Forwards packet to FrequencyWorker for audio processing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueForUser(string speakerId, RadioPacket packet)
        {
            // Store in routing queue (for backwards compatibility and statistics)
            var queue = _userQueues.GetOrAdd(speakerId, _ => new ConcurrentQueue<RadioPacket>());
            queue.Enqueue(packet);
            Interlocked.Increment(ref _totalPackets);
            
            // NEW: Forward to actual FrequencyWorker for processing
            _frequencyWorker.EnqueueForUser(speakerId, packet);
        }

        /// <summary>
        /// Gets packets for a specific user
        /// </summary>
        public IEnumerable<RadioPacket> GetPacketsForUser(string speakerId)
        {
            if (_userQueues.TryGetValue(speakerId, out var queue))
            {
                while (queue.TryDequeue(out var packet))
                {
                    yield return packet;
                }
            }
        }

        /// <summary>
        /// Gets all user IDs on this frequency
        /// </summary>
        public IReadOnlyCollection<string> GetUserIds()
        {
            return _userQueues.Keys.ToList();
        }

        /// <summary>
        /// Gets packet count for a specific user
        /// </summary>
        public int GetUserPacketCount(string speakerId)
        {
            return _userQueues.TryGetValue(speakerId, out var queue) ? queue.Count : 0;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _userQueues.Clear();
            
            // NEW: Dispose the audio worker
            _ = _frequencyWorker.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            
            _disposed = true;
            Logger.Debug($"FrequencyRoutingWorker disposed: {_frequency:F0} Hz");
        }
    }

    /// <summary>
    /// Internal packet queue for routing
    /// </summary>
    internal class PacketQueue
    {
        private readonly ConcurrentQueue<RadioPacket> _queue = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(RadioPacket packet)
        {
            _queue.Enqueue(packet);
        }

        public bool TryDequeue(out RadioPacket? packet)
        {
            return _queue.TryDequeue(out packet);
        }

        public int Count => _queue.Count;
    }

    /// <summary>
    /// Statistics for packet routing performance
    /// </summary>
    public class PacketRouterStats
    {
        private long _packetsRouted;

        public long PacketsRouted => Interlocked.Read(ref _packetsRouted);
        public int FrequencyCount { get; set; }
        public int SpeakerCount { get; set; }
        public int RouteCount { get; set; }
        public TimeSpan ElapsedTime { get; set; }

        public double PacketsPerSecond => 
            ElapsedTime.TotalSeconds > 0 ? PacketsRouted / ElapsedTime.TotalSeconds : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IncrementPacketsRouted()
        {
            Interlocked.Increment(ref _packetsRouted);
        }

        internal void Reset()
        {
            Interlocked.Exchange(ref _packetsRouted, 0);
            FrequencyCount = 0;
            SpeakerCount = 0;
            RouteCount = 0;
            ElapsedTime = TimeSpan.Zero;
        }

        public override string ToString()
        {
            return $"Packets: {PacketsRouted:N0}, Frequencies: {FrequencyCount}, " +
                   $"Speakers: {SpeakerCount}, Routes: {RouteCount}, " +
                   $"Rate: {PacketsPerSecond:N0} pkt/s, Time: {ElapsedTime.TotalSeconds:F3}s";
        }
    }
}
