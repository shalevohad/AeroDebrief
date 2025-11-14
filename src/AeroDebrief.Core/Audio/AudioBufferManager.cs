using System.Collections.Concurrent;
using NLog;
using AeroDebrief.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Advanced audio buffer manager with intelligent buffering, seek support, and buffer region tracking
    /// </summary>
    public sealed class AudioBufferManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly AudioProcessingEngine _processingEngine;
        private readonly ConcurrentQueue<ProcessedAudioChunk> _processedChunks = new();
        private readonly object _lock = new();
        
        // Buffering state
        private Task? _processingTask;
        private CancellationTokenSource? _cts;
        private volatile bool _isProcessing;
        
        // Packet data
        private List<AudioPacketMetadata>? _packets;
        private DateTime _recordingStart;
        private volatile int _currentBufferIndex; // Current packet being buffered
        private TimeSpan _totalDuration;
        
        // Buffer regions tracking (for UI display)
        private readonly List<BufferedRegion> _bufferedRegions = new();
        
        // Progress tracking
        private volatile int _totalPackets;
        public event Action<double>? ProcessingProgressChanged; // DEPRECATED - kept for compatibility
        public event Action<List<BufferedRegion>>? BufferedRegionsChanged; // NEW - reports buffered regions for UI
        
        // Constants
        private const int MIN_BUFFER_SECONDS = 10; // Minimum 10 seconds of buffer before playback can start
        private const int CHUNK_BATCH_SIZE = 50; // Process 50 packets at a time (~1 second of audio)
        
        public AudioBufferManager(AudioProcessingEngine processingEngine)
        {
            _processingEngine = processingEngine ?? throw new ArgumentNullException(nameof(processingEngine));
            Logger.Info("AudioBufferManager initialized with intelligent buffering");
        }
        
        /// <summary>
        /// Starts background buffering from a specific playback position (typically current playback time)
        /// This allows the buffer to "follow" the playback, buffering ahead continuously
        /// </summary>
        public void StartBufferingFrom(List<AudioPacketMetadata> packets, DateTime recordingStart, TimeSpan startPosition)
        {
            lock (_lock)
            {
                if (_isProcessing)
                {
                    Logger.Info("Buffering already active, stopping and restarting");
                    StopProcessing();
                }
                
                _packets = packets ?? throw new ArgumentNullException(nameof(packets));
                _totalPackets = packets.Count;
                
                // Use first packet's timestamp as recording start for consistent timeline
                if (packets.Count > 0)
                {
                    _recordingStart = packets[0].Timestamp;
                    _totalDuration = packets[^1].Timestamp - packets[0].Timestamp;
                    Logger.Info($"Recording timeline: {_recordingStart} to {packets[^1].Timestamp} (duration: {_totalDuration})");
                }
                else
                {
                    _recordingStart = recordingStart;
                    _totalDuration = TimeSpan.Zero;
                    Logger.Warn("No packets to buffer");
                    return;
                }
                
                // Find starting packet index based on startPosition
                _currentBufferIndex = FindPacketIndexForPosition(startPosition);
                Logger.Info($"Starting buffering from position {startPosition} (packet index {_currentBufferIndex})");
                
                // Clear existing queue and regions
                ClearBufferQueue();
                lock (_bufferedRegions)
                {
                    _bufferedRegions.Clear();
                }
                
                _isProcessing = true;
                _cts = new CancellationTokenSource();
                
                _processingTask = Task.Run(() => ContinuousBufferingAsync(_cts.Token), _cts.Token);
                
                Logger.Info($"Buffering started from packet {_currentBufferIndex}/{_totalPackets}");
            }
        }
        
        /// <summary>
        /// Legacy method - starts processing from beginning
        /// </summary>
        public void StartProcessing(List<AudioPacketMetadata> packets, DateTime recordingStart)
        {
            StartBufferingFrom(packets, recordingStart, TimeSpan.Zero);
        }
        
        /// <summary>
        /// Stops the buffering task
        /// </summary>
        public void StopProcessing()
        {
            lock (_lock)
            {
                if (!_isProcessing) return;
                
                Logger.Info("Stopping buffering...");
                _isProcessing = false;
                _cts?.Cancel();
                
                try
                {
                    _processingTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex) when (ex is OperationCanceledException or AggregateException)
                {
                    // Expected during cancellation
                }
                
                _cts?.Dispose();
                _cts = null;
                _processingTask = null;
                
                Logger.Info("Buffering stopped");
            }
        }
        
        /// <summary>
        /// Gets the next audio chunk for the specified playback position.
        /// Returns null if no chunk is available for that position.
        /// </summary>
        public ProcessedAudioChunk? GetNextChunk(TimeSpan playbackPosition)
        {
            if (!_processedChunks.TryPeek(out var chunk))
            {
                return null; // Queue is empty
            }
            
            // Check if this chunk should play now (within 500ms window for tolerance - increased from 200ms to handle gaps)
            var timeDiff = (chunk.PlaybackTime - playbackPosition).TotalMilliseconds;
            
            // CRITICAL FIX: Increased tolerance window from 200ms to 500ms
            // This prevents playback getting stuck when there are gaps between transmissions
            // SRS transmissions can have natural gaps of 200-500ms which should jump forward, not fill with silence
            if (timeDiff <= 500 && timeDiff >= -100)
            {
                // Chunk is ready to play (or close enough - jump forward if needed)
                if (_processedChunks.TryDequeue(out var dequeuedChunk))
                {
                    if (timeDiff > 50)
                    {
                        Logger.Debug($"Jumping forward {timeDiff:F0}ms from {playbackPosition} to chunk at {dequeuedChunk.PlaybackTime}");
                    }
                    else
                    {
                        Logger.Trace($"Retrieved chunk at {dequeuedChunk.PlaybackTime} for playback at {playbackPosition}");
                    }
                    return dequeuedChunk;
                }
            }
            else if (timeDiff < -100)
            {
                // Chunk is too late - skip it
                if (_processedChunks.TryDequeue(out var lateChunk))
                {
                    Logger.Debug($"Skipping late chunk at {lateChunk.PlaybackTime} (late by {-timeDiff:F0}ms, current position: {playbackPosition})");
                    lateChunk.Dispose();
                    return GetNextChunk(playbackPosition); // Try next chunk recursively
                }
            }
            
            // Chunk is too early (>500ms) - wait or fill with silence
            return null;
        }
        
        /// <summary>
        /// Seeks to a specific position and restarts buffering from there
        /// </summary>
        public void SeekTo(TimeSpan targetPosition)
        {
            lock (_lock)
            {
                if (_packets == null)
                {
                    Logger.Warn("Cannot seek - no packets loaded");
                    return;
                }
                
                var targetIndex = FindPacketIndexForPosition(targetPosition);
                Logger.Info($"Seeking from packet {_currentBufferIndex} to {targetIndex} (position {targetPosition})");
                
                // Clear current buffer queue
                ClearBufferQueue();
                
                // Update buffer index to new position
                _currentBufferIndex = targetIndex;
                
                // Cancel current buffering task and restart from new position
                if (_isProcessing && _cts != null && !_cts.IsCancellationRequested)
                {
                    Logger.Debug("Restarting buffering task after seek");
                    // The buffering loop will pick up the new _currentBufferIndex automatically
                }
                
                Logger.Info($"Seek complete - buffering will resume from packet {_currentBufferIndex}");
            }
        }
        
        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public void SeekTo(int packetIndex)
        {
            lock (_lock)
            {
                if (_packets == null) return;
                
                packetIndex = Math.Clamp(packetIndex, 0, _packets.Count - 1);
                
                var targetPosition = _packets[packetIndex].Timestamp - _recordingStart;
                SeekTo(targetPosition);
            }
        }
        
        /// <summary>
        /// Checks if minimum buffer threshold is met for playback to start
        /// </summary>
        public bool HasMinimumBuffer(TimeSpan currentPosition)
        {
            if (!_processedChunks.TryPeek(out var firstChunk))
                return false; // No buffer at all
            
            // Calculate how much is buffered ahead of current position
            var bufferedDuration = GetBufferedDurationFrom(currentPosition);
            var hasMinimum = bufferedDuration.TotalSeconds >= MIN_BUFFER_SECONDS;
            
            if (!hasMinimum)
            {
                Logger.Trace($"Buffer: {bufferedDuration.TotalSeconds:F1}s / {MIN_BUFFER_SECONDS}s minimum");
            }
            
            return hasMinimum;
        }
        
        /// <summary>
        /// Gets the duration of audio buffered from a specific position
        /// </summary>
        public TimeSpan GetBufferedDurationFrom(TimeSpan position)
        {
            var chunks = _processedChunks.ToArray();
            if (chunks.Length == 0)
                return TimeSpan.Zero;
            
            var relevantChunks = chunks.Where(c => c.PlaybackTime >= position).ToList();
            if (relevantChunks.Count == 0)
                return TimeSpan.Zero;
            
            var lastChunk = relevantChunks[^1];
            return (lastChunk.PlaybackTime + lastChunk.Duration) - position;
        }
        
        /// <summary>
        /// Gets list of buffered regions for UI display (greenish overlay on waveform)
        /// </summary>
        public List<BufferedRegion> GetBufferedRegions()
        {
            lock (_bufferedRegions)
            {
                return new List<BufferedRegion>(_bufferedRegions);
            }
        }
        
        /// <summary>
        /// Gets the number of processed chunks waiting in the queue
        /// </summary>
        public int QueuedChunkCount => _processedChunks.Count;
        
        /// <summary>
        /// Diagnoses queue state when playback is stuck
        /// </summary>
        public (TimeSpan? NextChunkTime, string Status) DiagnoseQueue(TimeSpan currentPosition)
        {
            if (!_processedChunks.TryPeek(out var nextChunk))
            {
                var bufferStatus = _currentBufferIndex < _totalPackets 
                    ? $"Still buffering (packet {_currentBufferIndex}/{_totalPackets})" 
                    : "Buffering complete";
                return (null, $"Queue is EMPTY - {bufferStatus}");
            }
            
            var chunks = _processedChunks.ToArray().Take(5).ToList();
            var nextTime = nextChunk.PlaybackTime;
            var timeDiff = (nextTime - currentPosition).TotalMilliseconds;
            
            var status = $"Next: {nextTime:mm\\:ss\\.ff} (diff: {timeDiff:F0}ms from {currentPosition:mm\\:ss\\.ff})";
            
            if (chunks.Count > 1)
            {
                var times = string.Join(", ", chunks.Select(c => c.PlaybackTime.ToString(@"mm\:ss\.ff")));
                status += $" | Queue: [{times}...]";
            }
            
            status += $" | Buffer idx: {_currentBufferIndex}/{_totalPackets}";
            
            return (nextTime, status);
        }
        
        /// <summary>
        /// Gets whether buffering has processed all packets
        /// </summary>
        public bool IsProcessingComplete
        {
            get
            {
                lock (_lock)
                {
                    return _packets != null && _currentBufferIndex >= _packets.Count;
                }
            }
        }
        
        /// <summary>
        /// Gets buffering progress as percentage (0-100)
        /// NOTE: This is BUFFERING progress, NOT playback progress!
        /// </summary>
        public double ProcessingProgress
        {
            get
            {
                if (_totalPackets == 0) return 0;
                return (_currentBufferIndex / (double)_totalPackets) * 100.0;
            }
        }
        
        /// <summary>
        /// Main buffering loop - continuously processes packets ahead of playback
        /// </summary>
        private async Task ContinuousBufferingAsync(CancellationToken cancellationToken)
        {
            Logger.Info("Continuous buffering task started");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check if we've processed all packets
                    if (_currentBufferIndex >= _totalPackets)
                    {
                        Logger.Info("All packets buffered - buffering task complete");
                        break;
                    }
                    
                    // Process a batch of packets
                    var batchStart = _currentBufferIndex;
                    var batchEnd = Math.Min(_currentBufferIndex + CHUNK_BATCH_SIZE, _totalPackets);
                    var batchCount = batchEnd - batchStart;
                    
                    if (batchCount > 0)
                    {
                        await ProcessPacketBatchAsync(batchStart, batchEnd, cancellationToken);
                    }
                    
                    // Small delay to prevent CPU hogging
                    await Task.Delay(10, cancellationToken);
                }
                
                Logger.Info($"Buffering complete: {_currentBufferIndex}/{_totalPackets} packets processed");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Buffering cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Buffering task crashed");
            }
        }
        
        /// <summary>
        /// Processes a batch of packets efficiently
        /// </summary>
        private async Task ProcessPacketBatchAsync(int startIndex, int endIndex, CancellationToken cancellationToken)
        {
            if (_packets == null) return;
            
            var processedCount = 0;
            var regionStart = TimeSpan.Zero;
            var regionEnd = TimeSpan.Zero;
            
            for (int i = startIndex; i < endIndex && !cancellationToken.IsCancellationRequested; i++)
            {
                var packet = _packets[i];
                
                try
                {
                    // Skip empty packets
                    if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                    {
                        continue;
                    }
                    
                    // Process packet
                    var processedAudio = _processingEngine.ProcessPacket(packet);
                    
                    if (processedAudio != null && processedAudio.Length > 0)
                    {
                        // DIAGNOSTIC: Check audio before conversion
                        var maxAmplitude = processedAudio.Max(Math.Abs);
                        var nonZeroCount = processedAudio.Count(s => Math.Abs(s) > 0.001f);
                        Logger.Debug($"?? Buffer: Processed packet - max amplitude={maxAmplitude:F4}, non-zero={nonZeroCount}/{processedAudio.Length}");
                        
                        if (maxAmplitude == 0)
                        {
                            Logger.Error($"?? CRITICAL: ProcessPacket returned SILENT audio! Packet index={i}, frequency={packet.Frequency}Hz");
                            continue; // Skip silent audio
                        }
                        
                        var pcmData = AudioConverter.FloatToPcm16(processedAudio);
                        if (pcmData.Length > 0)
                        {
                            var playbackTime = packet.Timestamp - _recordingStart;
                            var samples = pcmData.Length / 2;
                            var duration = TimeSpan.FromSeconds(samples / (double)Constants.OUTPUT_SAMPLE_RATE);
                            
                            var chunk = new ProcessedAudioChunk(pcmData, playbackTime, packet, duration);
                            _processedChunks.Enqueue(chunk);
                            
                            processedCount++;
                            
                            // Track buffered region
                            if (processedCount == 1)
                                regionStart = playbackTime;
                            regionEnd = playbackTime + duration;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error processing packet {i}");
                    // Continue processing other packets
                }
            }
            
            // Update buffer index
            lock (_lock)
            {
                _currentBufferIndex = endIndex;
            }
            
            // Update buffered regions if we processed anything
            if (processedCount > 0)
            {
                UpdateBufferedRegions(regionStart, regionEnd);
            }
            
            // Log progress periodically
            if (endIndex % 500 == 0 || endIndex == _totalPackets)
            {
                var progress = (endIndex / (double)_totalPackets) * 100.0;
                Logger.Debug($"Buffering progress: {endIndex}/{_totalPackets} ({progress:F1}%) - Queue: {_processedChunks.Count} chunks");
                
                // Fire legacy progress event
                try
                {
                    ProcessingProgressChanged?.Invoke(progress);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error invoking progress event");
                }
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Updates the list of buffered regions and fires UI update event
        /// </summary>
        private void UpdateBufferedRegions(TimeSpan start, TimeSpan end)
        {
            lock (_bufferedRegions)
            {
                // Try to merge with existing regions
                var merged = false;
                for (int i = 0; i < _bufferedRegions.Count; i++)
                {
                    var region = _bufferedRegions[i];
                    
                    // Check if regions overlap or are adjacent (within 1 second)
                    if (Math.Abs((start - region.End).TotalSeconds) < 1.0 ||
                        Math.Abs((end - region.Start).TotalSeconds) < 1.0 ||
                        (start >= region.Start && start <= region.End) ||
                        (end >= region.Start && end <= region.End))
                    {
                        // Merge regions
                        _bufferedRegions[i] = new BufferedRegion
                        {
                            Start = TimeSpan.FromTicks(Math.Min(start.Ticks, region.Start.Ticks)),
                            End = TimeSpan.FromTicks(Math.Max(end.Ticks, region.End.Ticks))
                        };
                        merged = true;
                        break;
                    }
                }
                
                if (!merged)
                {
                    // Add as new region
                    _bufferedRegions.Add(new BufferedRegion { Start = start, End = end });
                }
                
                // Sort regions by start time
                _bufferedRegions.Sort((a, b) => a.Start.CompareTo(b.Start));
                
                // Notify UI of updated regions
                try
                {
                    BufferedRegionsChanged?.Invoke(new List<BufferedRegion>(_bufferedRegions));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error invoking buffered regions event");
                }
            }
        }
        
        /// <summary>
        /// Finds the packet index closest to a specific playback position
        /// </summary>
        private int FindPacketIndexForPosition(TimeSpan position)
        {
            if (_packets == null || _packets.Count == 0)
                return 0;
            
            var targetTime = _recordingStart + position;
            
            // Binary search for efficiency
            int left = 0;
            int right = _packets.Count - 1;
            int bestIndex = 0;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                
                if (_packets[mid].Timestamp <= targetTime)
                {
                    bestIndex = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            
            return Math.Clamp(bestIndex, 0, _packets.Count - 1);
        }
        
        /// <summary>
        /// Clears all chunks from the buffer queue
        /// </summary>
        private void ClearBufferQueue()
        {
            var count = 0;
            while (_processedChunks.TryDequeue(out var chunk))
            {
                chunk?.Dispose();
                count++;
            }
            
            if (count > 0)
            {
                Logger.Debug($"Cleared {count} chunks from buffer queue");
            }
        }
        
        public void Dispose()
        {
            StopProcessing();
            ClearBufferQueue();
            
            lock (_bufferedRegions)
            {
                _bufferedRegions.Clear();
            }
            
            Logger.Info("AudioBufferManager disposed");
        }
    }
    
    /// <summary>
    /// Represents a region of audio that has been buffered
    /// </summary>
    public class BufferedRegion
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        
        public TimeSpan Duration => End - Start;
        
        /// <summary>
        /// Gets normalized start position (0-1) relative to total duration
        /// </summary>
        public double GetNormalizedStart(TimeSpan totalDuration) =>
            totalDuration.TotalSeconds > 0 ? Start.TotalSeconds / totalDuration.TotalSeconds : 0;
        
        /// <summary>
        /// Gets normalized end position (0-1) relative to total duration
        /// </summary>
        public double GetNormalizedEnd(TimeSpan totalDuration) =>
            totalDuration.TotalSeconds > 0 ? End.TotalSeconds / totalDuration.TotalSeconds : 0;
        
        public override string ToString() => $"{Start:mm\\:ss\\.ff} - {End:mm\\:ss\\.ff} ({Duration.TotalSeconds:F1}s)";
    }
    
    /// <summary>
    /// Represents a processed audio chunk ready for playback
    /// </summary>
    public sealed class ProcessedAudioChunk : IDisposable
    {
        public byte[] AudioData { get; }
        public TimeSpan PlaybackTime { get; }
        public AudioPacketMetadata SourcePacket { get; }
        public TimeSpan Duration { get; }
        
        public ProcessedAudioChunk(byte[] audioData, TimeSpan playbackTime, AudioPacketMetadata sourcePacket, TimeSpan duration)
        {
            AudioData = audioData ?? throw new ArgumentNullException(nameof(audioData));
            PlaybackTime = playbackTime;
            SourcePacket = sourcePacket ?? throw new ArgumentNullException(nameof(sourcePacket));
            Duration = duration;
        }
        
        public void Dispose()
        {
            // Audio data is a byte array, no explicit disposal needed
        }
    }
}