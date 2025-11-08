using System.Threading.Channels;
using System.Diagnostics;
using NLog;
using AeroDebrief.Core.Audio;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Processes audio packets for a single user/speaker on a frequency.
    /// Pipeline: JitterBuffer ? Decode ? Effects ? Output (NO filtering here!)
    /// 
    /// Filtering is applied at the MasterMixer level for instant switching.
    /// </summary>
    public sealed class UserWorker : IAsyncDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly string _userId;
        private readonly double _frequency;
        private readonly Channel<RadioPacket> _inputChannel;
        private readonly Channel<DecodedAudioBlock> _outputChannel;
        private readonly AudioProcessingEngine _processingEngine;
        private readonly JitterBuffer _jitterBuffer;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;
        
        private long _packetsProcessed;
        private long _packetsDropped;
        private DateTime _lastActivityTime;
        private bool _disposed;

        public string UserId => _userId;
        public double Frequency => _frequency;
        public long PacketsProcessed => Interlocked.Read(ref _packetsProcessed);
        public long PacketsDropped => Interlocked.Read(ref _packetsDropped);
        public TimeSpan IdleTime => DateTime.UtcNow - _lastActivityTime;
        public bool IsIdle(TimeSpan idleThreshold) => IdleTime > idleThreshold;

        /// <summary>
        /// Creates a UserWorker with bounded input/output channels
        /// </summary>
        public UserWorker(
            string userId, 
            double frequency,
            int inputBufferSize = 100,
            int outputBufferSize = 100)
        {
            _userId = userId ?? throw new ArgumentNullException(nameof(userId));
            _frequency = frequency;
            
            _inputChannel = Channel.CreateBounded<RadioPacket>(new BoundedChannelOptions(inputBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropWrite, // Return false when full instead of dropping oldest
                SingleWriter = false,
                SingleReader = true
            });

            _outputChannel = Channel.CreateBounded<DecodedAudioBlock>(new BoundedChannelOptions(outputBufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            _processingEngine = new AudioProcessingEngine();
            _jitterBuffer = new JitterBuffer(targetDepthMs: 60, maxCapacity: 50);
            _cts = new CancellationTokenSource();
            _lastActivityTime = DateTime.UtcNow;

            // Initialize processing engine
            _processingEngine.Initialize();

            // Start processing pipeline
            _processingTask = Task.Run(() => ProcessingPipelineAsync(_cts.Token));

            Logger.Debug($"UserWorker created: UserId={_userId}, Frequency={_frequency:F0} Hz (filtering at MasterMixer)");
        }

        /// <summary>
        /// Enqueues a packet for processing (non-blocking)
        /// </summary>
        public bool TryEnqueuePacket(RadioPacket packet)
        {
            if (_disposed || _cts.IsCancellationRequested)
                return false;

            _lastActivityTime = DateTime.UtcNow;
            return _inputChannel.Writer.TryWrite(packet);
        }

        /// <summary>
        /// Gets the output channel reader for consuming decoded blocks
        /// </summary>
        public ChannelReader<DecodedAudioBlock> OutputReader => _outputChannel.Reader;

        /// <summary>
        /// Signals completion of input (no more packets will be enqueued)
        /// </summary>
        public void CompleteInput()
        {
            _inputChannel.Writer.Complete();
            Logger.Debug($"UserWorker input completed: UserId={_userId}");
        }

        /// <summary>
        /// Main processing pipeline: JitterBuffer ? Decode ? Effects ? Output
        /// NO FILTERING HERE - MasterMixer handles all filtering for instant switching!
        /// </summary>
        private async Task ProcessingPipelineAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info($"UserWorker pipeline started: UserId={_userId} (filtering at MasterMixer)");

                await foreach (var packet in _inputChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        // Step 1: JitterBuffer - compensate for network jitter
                        var bufferedPackets = _jitterBuffer.AddPacket(packet);
                        
                        foreach (var bufferedPacket in bufferedPackets)
                        {
                            // Step 2: Decode and process
                            var metadata = ConvertToAudioPacketMetadata(bufferedPacket);
                            var processedSamples = _processingEngine.ProcessPacket(metadata);

                            if (processedSamples == null || processedSamples.Length == 0)
                            {
                                Interlocked.Increment(ref _packetsDropped);
                                continue;
                            }

                            // Step 3: Create decoded block (NO FILTERING!)
                            // MasterMixer will apply pilot filtering when pulling frames
                            var block = new DecodedAudioBlock
                            {
                                UserId = _userId,
                                Frequency = _frequency,
                                Timestamp = bufferedPacket.Timestamp,
                                AudioData = processedSamples,
                                SampleRate = Constants.OUTPUT_SAMPLE_RATE,
                                PacketId = bufferedPacket.PacketId,
                                Duration = TimeSpan.FromSeconds(processedSamples.Length / (double)Constants.OUTPUT_SAMPLE_RATE)
                            };

                            // Step 4: Write to output channel
                            await _outputChannel.Writer.WriteAsync(block, cancellationToken);

                            Interlocked.Increment(ref _packetsProcessed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error processing packet for user {_userId}");
                        Interlocked.Increment(ref _packetsDropped);
                    }
                }

                Logger.Info($"UserWorker pipeline completed: UserId={_userId}, Processed={_packetsProcessed}, Dropped={_packetsDropped}");
            }
            catch (OperationCanceledException)
            {
                Logger.Debug($"UserWorker pipeline cancelled: UserId={_userId}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"UserWorker pipeline failed: UserId={_userId}");
            }
            finally
            {
                _outputChannel.Writer.Complete();
            }
        }

        /// <summary>
        /// Converts RadioPacket to AudioPacketMetadata for audio processing
        /// </summary>
        private AudioPacketMetadata ConvertToAudioPacketMetadata(RadioPacket packet)
        {
            // Use the built-in conversion method
            return packet.ToMetadata();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Signal cancellation
                _cts.Cancel();

                // Complete channels
                _inputChannel.Writer.Complete();
                _outputChannel.Writer.Complete();

                // Wait for pipeline to complete (with timeout)
                var completed = await Task.WhenAny(_processingTask, Task.Delay(5000));
                if (completed != _processingTask)
                {
                    Logger.Warn($"UserWorker disposal timeout: UserId={_userId}");
                }

                // Dispose resources
                _processingEngine?.Dispose();
                _cts?.Dispose();

                Logger.Debug($"UserWorker disposed: UserId={_userId}, Processed={_packetsProcessed}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error disposing UserWorker: UserId={_userId}");
            }
        }
    }

    /// <summary>
    /// Manages multiple UserWorkers for a single frequency.
    /// Merges time-aligned audio from all users and emits frequency frames (10ms).
    /// NO FILTERING HERE - MasterMixer handles all filtering!
    /// </summary>
    public sealed class FrequencyWorker : IAsyncDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly double _frequency;
        private readonly Dictionary<string, UserWorker> _userWorkers;
        private readonly Channel<FrequencyAudioFrame> _outputChannel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _mixingTask;
        private readonly TimeSpan _idleTimeout;
        private readonly object _workersLock = new();
        
        private long _framesProduced;
        private DateTime _lastActivityTime;
        private bool _disposed;

        // 10ms frame size at 48kHz
        private const int FrameSizeMs = 10;
        private const int SamplesPerFrame = Constants.OUTPUT_SAMPLE_RATE * FrameSizeMs / 1000;

        public double Frequency => _frequency;
        public int ActiveUserCount 
        { 
            get 
            { 
                lock (_workersLock) 
                    return _userWorkers.Count; 
            } 
        }
        public long FramesProduced => Interlocked.Read(ref _framesProduced);
        public TimeSpan IdleTime => DateTime.UtcNow - _lastActivityTime;
        public bool IsIdle => IdleTime > _idleTimeout && ActiveUserCount == 0;
        
        /// <summary>
        /// Creates a FrequencyWorker with configurable idle timeout
        /// </summary>
        public FrequencyWorker(
            double frequency,
            TimeSpan? idleTimeout = null,
            int outputBufferSize = 200)
        {
            _frequency = frequency;
            _userWorkers = new Dictionary<string, UserWorker>();
            _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
            _lastActivityTime = DateTime.UtcNow;

            _outputChannel = Channel.CreateBounded<FrequencyAudioFrame>(new BoundedChannelOptions(outputBufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            _cts = new CancellationTokenSource();
            _mixingTask = Task.Run(() => MixingPipelineAsync(_cts.Token));

            Logger.Info($"FrequencyWorker created: Frequency={_frequency:F0} Hz (NO filtering - handled by MasterMixer)");
        }

        /// <summary>
        /// Gets or creates a UserWorker for the specified user
        /// </summary>
        public UserWorker GetOrCreateUserWorker(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            lock (_workersLock)
            {
                if (!_userWorkers.TryGetValue(userId, out var worker))
                {
                    worker = new UserWorker(userId, _frequency);
                    _userWorkers[userId] = worker;
                    Logger.Debug($"Created UserWorker: UserId={userId}, Frequency={_frequency:F0} Hz");
                }

                _lastActivityTime = DateTime.UtcNow;
                return worker;
            }
        }

        /// <summary>
        /// Enqueues a packet for a specific user
        /// </summary>
        public bool EnqueueForUser(string userId, RadioPacket packet)
        {
            var worker = GetOrCreateUserWorker(userId);
            _lastActivityTime = DateTime.UtcNow;
            return worker.TryEnqueuePacket(packet);
        }

        /// <summary>
        /// Gets the output channel reader for consuming frequency frames
        /// </summary>
        public ChannelReader<FrequencyAudioFrame> OutputReader => _outputChannel.Reader;

        /// <summary>
        /// Removes idle user workers that haven't been active recently
        /// </summary>
        public async Task<int> RemoveIdleUsersAsync(TimeSpan idleThreshold)
        {
            var removed = 0;
            List<KeyValuePair<string, UserWorker>> idleWorkers;

            lock (_workersLock)
            {
                idleWorkers = _userWorkers
                    .Where(kvp => kvp.Value.IsIdle(idleThreshold))
                    .ToList();
            }

            foreach (var kvp in idleWorkers)
            {
                lock (_workersLock)
                {
                    _userWorkers.Remove(kvp.Key);
                }

                await kvp.Value.DisposeAsync();
                removed++;
                Logger.Debug($"Removed idle UserWorker: UserId={kvp.Key}, IdleTime={kvp.Value.IdleTime}");
            }

            return removed;
        }

        /// <summary>
        /// Main mixing pipeline: Collects audio from all users, time-aligns, and mixes
        /// NO FILTERING HERE - outputs ALL audio for MasterMixer to filter
        /// </summary>
        private async Task MixingPipelineAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info($"FrequencyWorker mixing pipeline started: Frequency={_frequency:F0} Hz");

                var currentTime = DateTime.UtcNow;
                var accumulatedSamples = new List<float>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Collect audio blocks from all active users
                    var userBlocks = new List<(string UserId, DecodedAudioBlock Block)>();

                    lock (_workersLock)
                    {
                        foreach (var kvp in _userWorkers)
                        {
                            while (kvp.Value.OutputReader.TryRead(out var block))
                            {
                                userBlocks.Add((kvp.Key, block));
                            }
                        }
                    }

                    if (userBlocks.Count == 0)
                    {
                        // No activity - check idle timeout
                        if (IdleTime > _idleTimeout)
                        {
                            Logger.Debug($"FrequencyWorker idle timeout: Frequency={_frequency:F0} Hz");
                            break;
                        }

                        // Wait a bit before checking again
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }

                    // Sort blocks by timestamp for time-aligned mixing
                    userBlocks.Sort((a, b) => a.Block.Timestamp.CompareTo(b.Block.Timestamp));

                    // Accumulate samples and emit 10ms frames
                    foreach (var (userId, block) in userBlocks)
                    {
                        accumulatedSamples.AddRange(block.AudioData);

                        // Emit frames when we have enough samples
                        while (accumulatedSamples.Count >= SamplesPerFrame)
                        {
                            var frameSamples = accumulatedSamples.Take(SamplesPerFrame).ToArray();
                            accumulatedSamples.RemoveRange(0, SamplesPerFrame);

                            var frame = new FrequencyAudioFrame
                            {
                                Frequency = _frequency,
                                Timestamp = currentTime,
                                AudioData = frameSamples,
                                SampleRate = Constants.OUTPUT_SAMPLE_RATE,
                                Duration = TimeSpan.FromMilliseconds(FrameSizeMs),
                                ContributingUsers = userBlocks.Select(ub => ub.UserId).Distinct().ToList()
                            };

                            await _outputChannel.Writer.WriteAsync(frame, cancellationToken);
                            Interlocked.Increment(ref _framesProduced);
                        }
                    }

                    currentTime = currentTime.AddMilliseconds(FrameSizeMs);
                }

                Logger.Info($"FrequencyWorker mixing pipeline completed: Frequency={_frequency:F0} Hz, Produced={_framesProduced}");
            }
            catch (OperationCanceledException)
            {
                Logger.Debug($"FrequencyWorker mixing pipeline cancelled: Frequency={_frequency:F0} Hz");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"FrequencyWorker mixing pipeline failed: Frequency={_frequency:F0} Hz");
            }
            finally
            {
                _outputChannel.Writer.Complete();
            }
        }

        /// <summary>
        /// Gets all active UserWorkers for this frequency
        /// Used by MasterMixer to register for per-pilot audio mixing
        /// </summary>
        public IEnumerable<(string PilotId, UserWorker Worker)> GetUserWorkers()
        {
            lock (_workersLock)
            {
                return _userWorkers.Select(kvp => (kvp.Key, kvp.Value)).ToList();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Signal cancellation
                _cts.Cancel();

                // Complete channels
                _outputChannel.Writer.Complete();

                // Wait for mixing task to complete (with timeout)
                var completed = await Task.WhenAny(_mixingTask, Task.Delay(5000));
                if (completed != _mixingTask)
                {
                    Logger.Warn($"FrequencyWorker disposal timeout: Frequency={_frequency:F0} Hz");
                }

                // Dispose user workers
                List<UserWorker> workersToDispose;
                lock (_workersLock)
                {
                    workersToDispose = _userWorkers.Values.ToList();
                    _userWorkers.Clear();
                }

                foreach (var worker in workersToDispose)
                {
                    await worker.DisposeAsync();
                }

                Logger.Debug($"FrequencyWorker disposed: Frequency={_frequency:F0} Hz, Produced={_framesProduced}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error disposing FrequencyWorker: Frequency={_frequency:F0} Hz");
            }
        }
    }

    // NEW: Decoded audio block structure
    public class DecodedAudioBlock
    {
        public string UserId { get; set; } = string.Empty;
        public double Frequency { get; set; }
        public DateTime Timestamp { get; set; }
        public float[] AudioData { get; set; } = Array.Empty<float>();
        public int SampleRate { get; set; }
        public ulong PacketId { get; set; }
        public TimeSpan Duration { get; set; }
    }

    // NEW: Frequency audio frame structure
    public class FrequencyAudioFrame
    {
        public double Frequency { get; set; }
        public DateTime Timestamp { get; set; }
        public float[] AudioData { get; set; } = Array.Empty<float>();
        public int SampleRate { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> ContributingUsers { get; set; } = new();
    }
}
