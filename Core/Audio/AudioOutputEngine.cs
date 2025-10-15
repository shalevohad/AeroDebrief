using NAudio.Wave;
using NAudio.CoreAudioApi;
using NLog;
using ShalevOhad.DCS.SRS.Recorder.Core.Helpers;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Audio
{
    /// <summary>Handles audio output with automatic fallback to simpler methods</summary>
    public sealed class AudioOutputEngine : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private WasapiOut? _wasapiOut;
        private BufferedWaveProvider? _waveProvider;
        private SimpleAudioOutputEngine? _fallbackEngine;
        private bool _useSimpleFallback = false;
        private bool _disposed;
        private long _totalBytesWritten = 0;
        private DateTime _lastAudioWrite = DateTime.MinValue;
        private readonly Queue<byte[]> _audioQueue = new();
        private readonly object _queueLock = new();
        private Task? _playbackTask;
        private CancellationTokenSource? _playbackCts;
        private volatile bool _isSeekInProgress = false; // Add seek state tracking

        public async Task InitializeAsync()
        {
            try
            {
                Logger.Info("Initializing AudioOutputEngine with WASAPI...");
                await InitializeWasapiAsync();
            }
            catch (Exception wasapiEx)
            {
                Logger.Warn(wasapiEx, "WASAPI initialization failed, falling back to SimpleAudioOutputEngine...");
                
                try
                {
                    await InitializeSimpleFallbackAsync();
                    _useSimpleFallback = true;
                    Logger.Info("? Successfully initialized with SimpleAudioOutputEngine fallback");
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error(fallbackEx, "Both WASAPI and SimpleAudioOutputEngine initialization failed");
                    throw new InvalidOperationException(
                        $"Audio initialization failed. WASAPI: {wasapiEx.Message}, Fallback: {fallbackEx.Message}", 
                        wasapiEx);
                }
            }
        }

        private async Task InitializeWasapiAsync()
        {
            // Select the default multimedia render device explicitly and log details
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);

            // Use PCM 16-bit mono @ 48kHz; the audio engine will upmix/resample as needed
            var waveFormat = new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 16, 1);
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                // Important for streaming: fill reads with silence instead of returning 0,
                // otherwise WASAPI may stop or output nothing when buffer underruns.
                ReadFully = true, // CHANGED: Fill reads completely to prevent gaps
                BufferLength = waveFormat.AverageBytesPerSecond * 5, // INCREASED: More buffering
                DiscardOnBufferOverflow = false // Changed: don't discard audio data
            };

            Logger.Info($"???  WASAPI Buffer: {_waveProvider.BufferLength} bytes ({_waveProvider.BufferLength / waveFormat.AverageBytesPerSecond}s)");

            _wasapiOut.Init(_waveProvider);

            // Set default volume to full volume initially for debugging
            _wasapiOut.Volume = 1.0f;
            Logger.Info($"?? WASAPI Volume: {_wasapiOut.Volume:F2}");

            Logger.Info(
                $"WASAPI initialized on device: '{device.FriendlyName}'. " +
                $"Provider: {waveFormat.SampleRate}Hz, {waveFormat.BitsPerSample}-bit, {waveFormat.Channels}ch | " +
                $"Device MixFormat: {device.AudioClient.MixFormat.SampleRate}Hz, " +
                $"{device.AudioClient.MixFormat.BitsPerSample}-bit, {device.AudioClient.MixFormat.Channels}ch | " +
                $"Buffer Length: {_waveProvider.BufferLength} bytes, Volume: {_wasapiOut.Volume}");

            await Task.CompletedTask; // For consistency with async pattern
        }

        private async Task InitializeSimpleFallbackAsync()
        {
            _fallbackEngine = new SimpleAudioOutputEngine();
            await _fallbackEngine.InitializeAsync();
            Logger.Info("SimpleAudioOutputEngine initialized as fallback");
        }

        public void Start() 
        {
            try
            {
                if (_useSimpleFallback)
                {
                    _fallbackEngine?.Start();
                    StartFallbackPlaybackTask();
                    Logger.Info("SimpleAudioOutputEngine fallback started");
                }
                else
                {
                    _wasapiOut?.Play();
                    Logger.Info("WASAPI playback started");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start audio playback");
                throw;
            }
        }
        
        public void Stop() 
        {
            try
            {
                if (_useSimpleFallback)
                {
                    _ = StopFallbackPlaybackTaskAsync(); // Fire and forget to avoid blocking
                    _fallbackEngine?.Stop();
                    Logger.Info("SimpleAudioOutputEngine fallback stopped");
                }
                else
                {
                    _wasapiOut?.Stop();
                    Logger.Info("WASAPI playback stopped");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error stopping audio playback");
            }
        }
        
        public void SetMasterVolume(float volume)
        {
            var clampedVolume = Math.Clamp(volume, 0.0f, 1.0f);
            
            if (_useSimpleFallback)
            {
                _fallbackEngine?.SetMasterVolume(clampedVolume);
                Logger.Info($"SimpleAudioOutputEngine volume set to: {clampedVolume:F2}");
            }
            else if (_wasapiOut != null)
            {
                _wasapiOut.Volume = clampedVolume;
                Logger.Info($"WASAPI volume set to: {clampedVolume:F2}");
            }
        }

        public void ClearBuffer() 
        {
            try
            {
                _isSeekInProgress = true; // Signal that we're seeking
                
                if (_useSimpleFallback)
                {
                    // For fallback engine: clear the queue and stop/restart playback to flush buffers
                    lock (_queueLock)
                    {
                        _audioQueue.Clear();
                        Logger.Debug("SimpleAudioOutputEngine queue cleared");
                    }
                    
                    // Restart the fallback engine to ensure all buffers are flushed
                    try
                    {
                        _fallbackEngine?.Stop();
                        Task.Delay(50).Wait(); // Small delay to let system flush
                        _fallbackEngine?.Start();
                        Logger.Debug("SimpleAudioOutputEngine restarted to flush buffers");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error restarting SimpleAudioOutputEngine during buffer clear");
                    }
                }
                else if (_waveProvider != null)
                {
                    // For WASAPI: clear the buffer and restart playback to flush system buffers
                    var wasPlaying = _wasapiOut?.PlaybackState == PlaybackState.Playing;
                    
                    try
                    {
                        _wasapiOut?.Stop();
                        _waveProvider.ClearBuffer();
                        Logger.Debug("WASAPI buffer cleared");
                        
                        // Add a small delay to allow system to flush its internal buffers
                        Task.Delay(100).Wait();
                        
                        if (wasPlaying)
                        {
                            _wasapiOut?.Play();
                            Logger.Debug("WASAPI playback restarted after buffer clear");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error restarting WASAPI during buffer clear");
                        // Try to restart anyway
                        if (wasPlaying)
                        {
                            try { _wasapiOut?.Play(); } catch { }
                        }
                    }
                }
                
                Logger.Info("Audio buffers cleared and output restarted for seek operation");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error clearing audio buffer");
            }
            finally
            {
                // Add a delay before allowing new audio to prevent overlap
                _ = Task.Run(async () =>
                {
                    await Task.Delay(150); // Allow time for system to fully flush
                    _isSeekInProgress = false;
                });
            }
        }

        public async Task WriteAudioAsync(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Logger.Trace("Skipping audio write: empty data");
                return;
            }

            // Skip audio writes during seek operations to prevent old audio from playing
            if (_isSeekInProgress)
            {
                Logger.Trace("Skipping audio write: seek in progress");
                return;
            }

            try
            {
                // Log audio data statistics for debugging
                _totalBytesWritten += audioData.Length;
                _lastAudioWrite = DateTime.UtcNow;
                
                // Calculate audio statistics for logging using safe AudioHelpers
                // Note: audioData should already be decoded PCM from AudioProcessingEngine.ProcessPacket()
                // but we check format to be safe
                short[] pcmSamples;
                if (AudioHelpers.IsOpusEncodedByteArray(audioData))
                {
                    // Unexpected: should have been decoded before reaching here
                    Logger.Warn($"Received Opus-encoded audio data ({audioData.Length} bytes) - decoding now, but this indicates upstream issue");
                    pcmSamples = AudioHelpers.DecodeAudioToPcm(audioData);
                }
                else
                {
                    // Expected path: already decoded PCM bytes
                    pcmSamples = AudioHelpers.ConvertBytesToPcm16(audioData);
                }
                
                var maxAmplitude = 0;
                var nonZeroSamples = 0;
                
                foreach (var sample in pcmSamples)
                {
                    // Use safe Math.Abs that handles Int16.MinValue correctly
                    var absSample = sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
                    maxAmplitude = Math.Max(maxAmplitude, absSample);
                    if (absSample > 100) // Consider samples above background noise level
                        nonZeroSamples++;
                }

                // Critical audio output log
                var engineType = _useSimpleFallback ? "SimpleEngine" : "WASAPI";
                var amplitudePercent = (maxAmplitude / 32767.0) * 100.0;
                Logger.Info($"?? OUTPUT: {engineType} - {audioData.Length} bytes, " +
                           $"amplitude: {maxAmplitude}/32767 ({amplitudePercent:F1}%), " +
                           $"active: {nonZeroSamples}/{pcmSamples.Length}");

                // Critical warning if no audio
                if (maxAmplitude == 0)
                {
                    Logger.Error($"? SILENT OUTPUT: All {pcmSamples.Length} samples are zero!");
                }
                else if (maxAmplitude < 500)
                {
                    Logger.Warn($"??  QUIET OUTPUT: Max amplitude only {maxAmplitude}/32767 ({amplitudePercent:F1}%) - may be inaudible");
                }

                // --- DEBUG: dump PCM bytes to WAV for inspection (post-conversion, pre-output)
                DebugAudioDumper.DumpPcmBytesAsWav(audioData, Constants.OUTPUT_SAMPLE_RATE, "output_preplay");

                if (_useSimpleFallback)
                {
                    // Queue audio for the fallback engine (only if not seeking)
                    if (!_isSeekInProgress)
                    {
                        lock (_queueLock)
                        {
                            _audioQueue.Enqueue((byte[])audioData.Clone());
                        }
                        Logger.Debug($"Queued {audioData.Length} bytes for SimpleAudioOutputEngine, max amplitude: {maxAmplitude}, active samples: {nonZeroSamples}/{audioData.Length / 2}");
                    }
                }
                else
                {
                    // Use WASAPI (only if not seeking)
                    if (_waveProvider != null && !_isSeekInProgress)
                    {
                        var availableBytes = _waveProvider.BufferLength - _waveProvider.BufferedBytes;
                        
                        if (availableBytes < audioData.Length)
                        {
                            Logger.Warn($"WASAPI buffer overflow: need {audioData.Length} bytes, only {availableBytes} available. Buffer status: {_waveProvider.BufferedBytes}/{_waveProvider.BufferLength}");
                            
                            // Clear some old data to make room if buffer is too full
                            if (_waveProvider.BufferedBytes > _waveProvider.BufferLength * 0.8)
                            {
                                _waveProvider.ClearBuffer();
                                Logger.Info("Cleared WASAPI buffer due to overflow");
                            }
                        }

                        _waveProvider.AddSamples(audioData, 0, audioData.Length);
                        
                        // Detailed WASAPI write logging
                        var bufferUsagePercent = (_waveProvider.BufferedBytes / (double)_waveProvider.BufferLength) * 100.0;
                        var bufferTimeMs = (_waveProvider.BufferedBytes / (double)_waveProvider.WaveFormat.AverageBytesPerSecond) * 1000.0;
                        
                        Logger.Info($"?? WASAPI WRITE SUCCESS: " +
                                   $"wrote {audioData.Length} bytes, " +
                                   $"amplitude {maxAmplitude}/32767, " +
                                   $"buffer: {_waveProvider.BufferedBytes}/{_waveProvider.BufferLength} ({bufferUsagePercent:F1}%, {bufferTimeMs:F0}ms), " +
                                   $"playback: {_wasapiOut?.PlaybackState}");
                        
                        // Warn if buffer is getting too full
                        if (bufferUsagePercent > 80)
                        {
                            Logger.Warn($"??  WASAPI buffer filling up: {bufferUsagePercent:F1}%");
                        }
                    }
                }

                // Log detailed statistics periodically
                if (_totalBytesWritten % (48000 * 2) == 0) // Every ~1 second of audio
                {
                    Logger.Info($"Audio output stats ({(_useSimpleFallback ? "SimpleEngine" : "WASAPI")}): {_totalBytesWritten} total bytes, " +
                               $"max amplitude: {maxAmplitude}/32767, active samples: {nonZeroSamples}/{audioData.Length / 2}");
                }

                // Warn if audio seems too quiet
                if (maxAmplitude > 0 && maxAmplitude < 1000)
                {
                    Logger.Warn($"Audio amplitude seems low: max {maxAmplitude}/32767. Audio may be too quiet to hear.");
                }

                // Warn if no audio activity
                if (nonZeroSamples == 0)
                {
                    Logger.Warn("No significant audio activity detected in this packet (all samples near zero)");
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to write audio data: {audioData?.Length} bytes");
            }

            await Task.CompletedTask; // For async consistency
        }

        private void StartFallbackPlaybackTask()
        {
            _playbackCts = new CancellationTokenSource();
            _playbackTask = Task.Run(async () =>
            {
                Logger.Debug("Started SimpleAudioOutputEngine playback task");
                
                while (!_playbackCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        byte[]? audioData = null;
                        
                        lock (_queueLock)
                        {
                            if (_audioQueue.Count > 0)
                            {
                                audioData = _audioQueue.Dequeue();
                            }
                        }
                        
                        if (audioData != null && !_isSeekInProgress)
                        {
                            await _fallbackEngine!.PlayAudioAsync(audioData);
                        }
                        else
                        {
                            // No audio data, wait a bit
                            await Task.Delay(10, _playbackCts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error in SimpleAudioOutputEngine playback task");
                        await Task.Delay(100, _playbackCts.Token);
                    }
                }
                
                Logger.Debug("SimpleAudioOutputEngine playback task ended");
            }, _playbackCts.Token);
        }

        private async Task StopFallbackPlaybackTaskAsync()
        {
            try
            {
                _playbackCts?.Cancel();
                
                if (_playbackTask != null && !_playbackTask.IsCompleted)
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _playbackTask.WaitAsync(timeoutCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("SimpleAudioOutputEngine playback task cancellation completed");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error stopping SimpleAudioOutputEngine playback task");
            }
            finally
            {
                _playbackCts?.Dispose();
                _playbackCts = null;
                _playbackTask = null;
            }
        }

        private void StopFallbackPlaybackTask()
        {
            // Synchronous version - just fire and forget the async version
            _ = StopFallbackPlaybackTaskAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;

            Logger.Info($"Disposing AudioOutputEngine ({(_useSimpleFallback ? "SimpleEngine" : "WASAPI")}). Total bytes written: {_totalBytesWritten}, last write: {_lastAudioWrite}");

            try
            {
                if (_useSimpleFallback)
                {
                    _ = StopFallbackPlaybackTaskAsync(); // Use async version to prevent blocking
                    _fallbackEngine?.Dispose();
                }
                else
                {
                    _wasapiOut?.Stop();
                    _wasapiOut?.Dispose();
                }
                
                _waveProvider = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during AudioOutputEngine disposal");
            }

            _disposed = true;
        }
    }
}