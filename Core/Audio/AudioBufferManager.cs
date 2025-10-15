using System.Collections.Concurrent;
using NLog;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Audio
{
    /// <summary>
    /// Manages audio buffering for smooth playback by pre-processing packets ahead of playback position
    /// </summary>
    public sealed class AudioBufferManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly AudioProcessingEngine _processingEngine;
        private readonly ConcurrentQueue<ProcessedAudioChunk> _bufferedAudio = new();
        private readonly object _bufferLock = new();
        private readonly SemaphoreSlim _bufferSemaphore;
        
        private Task? _bufferingTask;
        private CancellationTokenSource? _bufferingCts;
        private volatile bool _isBuffering;
        private volatile int _currentPacketIndex;
        private List<AudioPacketMetadata>? _allPackets;
        private DateTime _recordingStart;
        
        // Configuration
        private readonly TimeSpan _bufferAheadTime;
        private readonly int _maxBufferedChunks;
        
        public AudioBufferManager(AudioProcessingEngine processingEngine, TimeSpan bufferAheadTime = default, int maxBufferedChunks = 1000)
        {
            _processingEngine = processingEngine ?? throw new ArgumentNullException(nameof(processingEngine));
            _bufferAheadTime = bufferAheadTime == default ? TimeSpan.FromSeconds(3) : bufferAheadTime; // Default 3 seconds ahead
            _maxBufferedChunks = maxBufferedChunks;
            _bufferSemaphore = new SemaphoreSlim(_maxBufferedChunks, _maxBufferedChunks);
            
            Logger.Info($"AudioBufferManager initialized - Buffer ahead: {_bufferAheadTime.TotalSeconds:F1}s, Max chunks: {_maxBufferedChunks}");
        }
        
        /// <summary>
        /// Starts buffering audio packets from the specified position
        /// </summary>
        public void StartBuffering(List<AudioPacketMetadata> packets, DateTime recordingStart, int startPacketIndex = 0)
        {
            lock (_bufferLock)
            {
                if (_isBuffering)
                {
                    Logger.Debug("Buffering already started, restarting with new parameters");
                    StopBuffering();
                }
                
                _allPackets = packets ?? throw new ArgumentNullException(nameof(packets));
                _recordingStart = recordingStart;
                _currentPacketIndex = Math.Max(0, Math.Min(startPacketIndex, packets.Count - 1));
                
                // Clear existing buffer
                while (_bufferedAudio.TryDequeue(out _)) { }
                
                _isBuffering = true;
                _bufferingCts = new CancellationTokenSource();
                
                _bufferingTask = Task.Run(() => BufferingLoop(_bufferingCts.Token));
                
                Logger.Info($"Started buffering from packet index {_currentPacketIndex}/{packets.Count}");
            }
        }
        
        /// <summary>
        /// Stops the buffering process
        /// </summary>
        public void StopBuffering()
        {
            lock (_bufferLock)
            {
                if (!_isBuffering) return;
                
                _isBuffering = false;
                _bufferingCts?.Cancel();
                
                try
                {
                    _bufferingTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
                {
                    // Expected cancellation
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error stopping buffering task");
                }
                
                _bufferingCts?.Dispose();
                _bufferingCts = null;
                _bufferingTask = null;
                
                Logger.Debug("Buffering stopped");
            }
        }
        
        /// <summary>
        /// Seeks to a new position and clears/rebuilds the buffer
        /// </summary>
        public void SeekTo(int packetIndex)
        {
            lock (_bufferLock)
            {
                if (!_isBuffering || _allPackets == null) return;
                
                var newIndex = Math.Max(0, Math.Min(packetIndex, _allPackets.Count - 1));
                
                Logger.Debug($"Seeking buffer to packet index {newIndex} (was {_currentPacketIndex})");
                
                // Clear existing buffer
                while (_bufferedAudio.TryDequeue(out var chunk))
                {
                    chunk.Dispose();
                }
                
                // Reset semaphore count
                var currentCount = _bufferSemaphore.CurrentCount;
                if (currentCount < _maxBufferedChunks)
                {
                    _bufferSemaphore.Release(_maxBufferedChunks - currentCount);
                }
                
                _currentPacketIndex = newIndex;
                
                Logger.Debug($"Buffer seek completed to index {_currentPacketIndex}");
            }
        }
        
        /// <summary>
        /// Gets the next processed audio chunk if available, otherwise returns null
        /// </summary>
        public ProcessedAudioChunk? GetNextAudioChunk(TimeSpan currentPlaybackPosition)
        {
            if (!_bufferedAudio.TryPeek(out var nextChunk))
            {
                return null; // No buffered audio available
            }
            
            // Check if this chunk should be played now
            var chunkPlayTime = nextChunk.PlaybackTime;
            var timeDifference = (chunkPlayTime - currentPlaybackPosition).TotalMilliseconds;
            
            // Allow some tolerance for timing (±50ms)
            if (timeDifference <= 50)
            {
                if (_bufferedAudio.TryDequeue(out var dequeuedChunk))
                {
                    _bufferSemaphore.Release(); // Allow buffering of another chunk
                    Logger.Trace($"Retrieved buffered audio chunk at {chunkPlayTime} (current: {currentPlaybackPosition})");
                    return dequeuedChunk;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the number of buffered audio chunks
        /// </summary>
        public int BufferedChunkCount => _bufferedAudio.Count;
        
        /// <summary>
        /// Gets the total buffered time duration
        /// </summary>
        public TimeSpan BufferedDuration
        {
            get
            {
                if (_bufferedAudio.IsEmpty) return TimeSpan.Zero;
                
                var chunks = _bufferedAudio.ToArray();
                if (chunks.Length == 0) return TimeSpan.Zero;
                
                var firstChunk = chunks[0];
                var lastChunk = chunks[^1];
                return lastChunk.PlaybackTime - firstChunk.PlaybackTime + TimeSpan.FromMilliseconds(20); // Add estimated chunk duration
            }
        }
        
        private async Task BufferingLoop(CancellationToken cancellationToken)
        {
            Logger.Debug("Buffering loop started");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isBuffering)
                {
                    // Wait for buffer space to become available
                    await _bufferSemaphore.WaitAsync(cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    // Check if we need more buffering
                    if (!ShouldContinueBuffering())
                    {
                        _bufferSemaphore.Release(); // Release the semaphore we just acquired
                        await Task.Delay(50, cancellationToken); // Brief pause before checking again
                        continue;
                    }
                    
                    // Get the next packet to process
                    AudioPacketMetadata? packetToProcess = null;
                    lock (_bufferLock)
                    {
                        if (_allPackets != null && _currentPacketIndex < _allPackets.Count)
                        {
                            packetToProcess = _allPackets[_currentPacketIndex];
                            _currentPacketIndex++;
                        }
                    }
                    
                    if (packetToProcess == null)
                    {
                        _bufferSemaphore.Release();
                        await Task.Delay(100, cancellationToken); // End of packets, wait longer
                        continue;
                    }
                    
                    // Process the packet
                    try
                    {
                        var processedAudio = _processingEngine.ProcessPacket(packetToProcess);
                        
                        if (processedAudio != null && processedAudio.Length > 0)
                        {
                            var pcmData = AudioConverter.FloatToPcm16(processedAudio);
                            if (pcmData.Length > 0)
                            {
                                var playbackTime = packetToProcess.Timestamp - _recordingStart;
                                var chunk = new ProcessedAudioChunk(pcmData, playbackTime, packetToProcess);
                                
                                _bufferedAudio.Enqueue(chunk);
                                
                                Logger.Trace($"?? Buffered audio chunk at {playbackTime} (packet {_currentPacketIndex - 1}, Freq={packetToProcess.Frequency:F1} Hz, Mod={(Modulation)packetToProcess.Modulation})");
                            }
                            else
                            {
                                _bufferSemaphore.Release(); // No audio produced, release semaphore
                            }
                        }
                        else
                        {
                            _bufferSemaphore.Release(); // No audio produced, release semaphore
                            Logger.Trace($"No audio produced for packet at index {_currentPacketIndex - 1}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error processing packet for buffer: {packetToProcess.TransmitterGuid}");
                        _bufferSemaphore.Release(); // Release on error
                        await Task.Delay(10, cancellationToken); // Brief pause on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Buffering loop cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error in buffering loop");
            }
            finally
            {
                Logger.Debug("Buffering loop ended");
            }
        }
        
        private bool ShouldContinueBuffering()
        {
            if (_allPackets == null) return false;
            
            // Don't buffer if we've reached the end of packets
            if (_currentPacketIndex >= _allPackets.Count) return false;
            
            // Don't buffer if we already have enough time buffered ahead
            if (!_bufferedAudio.IsEmpty)
            {
                var bufferedDuration = BufferedDuration;
                if (bufferedDuration >= _bufferAheadTime)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public void Dispose()
        {
            StopBuffering();
            
            // Clean up any remaining buffered chunks
            while (_bufferedAudio.TryDequeue(out var chunk))
            {
                chunk.Dispose();
            }
            
            _bufferSemaphore.Dispose();
            Logger.Debug("AudioBufferManager disposed");
        }
    }
    
    /// <summary>
    /// Represents a processed audio chunk ready for playback
    /// </summary>
    public sealed class ProcessedAudioChunk : IDisposable
    {
        public byte[] AudioData { get; }
        public TimeSpan PlaybackTime { get; }
        public AudioPacketMetadata SourcePacket { get; }
        
        public ProcessedAudioChunk(byte[] audioData, TimeSpan playbackTime, AudioPacketMetadata sourcePacket)
        {
            AudioData = audioData ?? throw new ArgumentNullException(nameof(audioData));
            PlaybackTime = playbackTime;
            SourcePacket = sourcePacket ?? throw new ArgumentNullException(nameof(sourcePacket));
        }
        
        public void Dispose()
        {
            // Audio data is a byte array, no explicit disposal needed
            // This is here for future extensibility if we need to dispose managed resources
        }
    }
}