using NAudio.Wave;
using NAudio.CoreAudioApi;
using NLog;
using AeroDebrief.Core.Helpers;

namespace AeroDebrief.Core.Audio
{
    /// <summary>Handles audio output using WASAPI with spatial audio support</summary>
    public sealed class AudioOutputEngine : IAudioOutputEngine
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private WasapiOut? _wasapiOut;
        private BufferedWaveProvider? _waveProvider;
        private bool _disposed;
        private long _totalBytesWritten = 0;
        private readonly object _wasapiLock = new();
        private volatile bool _isSeekInProgress = false;
        
        // NEW: Spatial audio provider support
        private ISpatialAudioProvider? _spatialAudioProvider;

        public async Task InitializeAsync()
        {
            try
            {
                Logger.Info("Initializing WASAPI audio output engine...");
                await InitializeWasapiAsync();
                Logger.Info("WASAPI audio engine initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "WASAPI initialization failed - no audio output available");
                throw new InvalidOperationException(
                    "Audio initialization failed - check audio drivers and device", 
                    ex);
            }
        }

        private async Task InitializeWasapiAsync()
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);
            var waveFormat = new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 16, 1);
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                ReadFully = true,
                BufferLength = waveFormat.AverageBytesPerSecond * 10,
                DiscardOnBufferOverflow = true
            };

            _wasapiOut.Init(_waveProvider);
            _wasapiOut.Volume = 1.0f;

            Logger.Info($"WASAPI initialized: {device.FriendlyName} ({waveFormat.SampleRate}Hz, {waveFormat.BitsPerSample}-bit, {waveFormat.Channels}ch)");
#if DEBUG
            Logger.Debug($"WASAPI buffer: {_waveProvider.BufferLength} bytes ({_waveProvider.BufferLength / waveFormat.AverageBytesPerSecond}s)");
#endif

            await Task.CompletedTask;
        }

        public void Start() 
        {
            try
            {
                _wasapiOut?.Play();
                Logger.Info("Audio playback started (WASAPI)");
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
                _wasapiOut?.Stop();
#if DEBUG
                Logger.Debug("Audio playback stopped");
#endif
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error stopping audio playback");
            }
        }
        
        public void SetMasterVolume(float volume)
        {
            var clampedVolume = Math.Clamp(volume, 0.0f, 1.0f);
            
            if (_wasapiOut != null)
            {
                lock (_wasapiLock)
                {
                    _wasapiOut.Volume = clampedVolume;
                }
            }
            
#if DEBUG
            Logger.Debug($"Master volume set to {clampedVolume:F2}");
#endif
        }

        public void ClearBuffer() 
        {
            try
            {
                _isSeekInProgress = true;
                
                if (_waveProvider != null)
                {
                    lock (_wasapiLock)
                    {
                        var wasPlaying = _wasapiOut?.PlaybackState == PlaybackState.Playing;
                        
                        try
                        {
                            _wasapiOut?.Stop();
                            _waveProvider.ClearBuffer();
                            _ = Task.Delay(100);
                            
                            if (wasPlaying)
                                _wasapiOut?.Play();
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Error restarting WASAPI during buffer clear");
#if DEBUG
                            Logger.Debug(ex, "WASAPI restart details");
#endif
                            if (wasPlaying)
                            {
                                try { _wasapiOut?.Play(); } catch { }
                            }
                        }
                    }
                }
                
#if DEBUG
                Logger.Debug("Audio buffers cleared for seek operation");
#endif
            }
            catch (Exception ex)
            {
                Logger.Warn("Error clearing audio buffer");
#if DEBUG
                Logger.Debug(ex, "Buffer clear details");
#endif
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(150);
                    _isSeekInProgress = false;
                });
            }
        }

        public async Task WriteAudioAsync(byte[] audioData)
        {
            await WriteAudioAsync(audioData, isSilence: false, chunkEndTime: default, positionUpdater: null);
        }

        /// <summary>
        /// Writes audio data to the output device with proper timing for both audio and silence
        /// </summary>
        /// <param name="audioData">Audio data to write</param>
        /// <param name="isSilence">Whether this is a silence chunk (affects timing)</param>
        /// <param name="chunkEndTime">End time of this chunk for position tracking during silence</param>
        /// <param name="positionUpdater">Optional callback to update position during silence playback</param>
        public async Task WriteAudioAsync(byte[] audioData, bool isSilence, TimeSpan chunkEndTime = default, Action<TimeSpan>? positionUpdater = null)
        {
            if (audioData == null || audioData.Length == 0 || _isSeekInProgress)
                return;

            try
            {
                _totalBytesWritten += audioData.Length;

                // For silence chunks, enforce duration with incremental position updates
                if (isSilence)
                {
                    var durationMs = (audioData.Length / 2.0) / Constants.OUTPUT_SAMPLE_RATE * 1000.0;
                    Logger.Debug($"Playing silence chunk: {audioData.Length} bytes (~{durationMs:F1}ms)");
                    
                    // Update position incrementally during silence
                    if (positionUpdater != null && chunkEndTime != default)
                    {
                        var startTime = chunkEndTime - TimeSpan.FromMilliseconds(durationMs);
                        var updateIntervalMs = 20;
                        var totalSteps = (int)(durationMs / updateIntervalMs);
                        
                        for (int step = 0; step < totalSteps && !_isSeekInProgress; step++)
                        {
                            await Task.Delay(updateIntervalMs);
                            
                            var progress = (step + 1) / (double)totalSteps;
                            var currentPos = startTime + TimeSpan.FromMilliseconds(durationMs * progress);
                            positionUpdater(currentPos);
                        }
                        
                        if (!_isSeekInProgress)
                        {
                            positionUpdater(chunkEndTime);
                        }
                    }
                    else
                    {
                        await Task.Delay((int)durationMs);
                    }
                    
                    return;
                }

#if DEBUG
                var pcmSamples = AudioHelpers.IsOpusEncodedByteArray(audioData) 
                    ? AudioHelpers.DecodeAudioToPcm(audioData)
                    : AudioHelpers.ConvertBytesToPcm16(audioData);
                
                var maxAmplitude = 0;
                foreach (var sample in pcmSamples)
                {
                    var absSample = sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
                    maxAmplitude = Math.Max(maxAmplitude, absSample);
                }

                Logger.Debug($"[WASAPI] Writing {audioData.Length} bytes, amplitude: {maxAmplitude}/32767");
#endif

                if (_waveProvider != null && !_isSeekInProgress)
                {
                    // Wait if buffer is too full
                    while (!_isSeekInProgress && _waveProvider != null)
                    {
                        double bufferUsage;
                        lock (_wasapiLock)
                        {
                            if (_waveProvider == null) break;
                            bufferUsage = (_waveProvider.BufferedBytes / (double)_waveProvider.BufferLength) * 100.0;
                        }
                        
                        if (bufferUsage < 70.0)
                            break;
                        
                        await Task.Delay(5);
                    }
                    
                    // Write audio data
                    lock (_wasapiLock)
                    {
                        if (_waveProvider == null || _isSeekInProgress)
                            return;

                        var availableBytes = _waveProvider.BufferLength - _waveProvider.BufferedBytes;
                        var currentBufferUsage = (_waveProvider.BufferedBytes / (double)_waveProvider.BufferLength) * 100.0;
                        
                        if (availableBytes < audioData.Length)
                        {
                            if (currentBufferUsage > 90)
                            {
                                Logger.Warn($"Audio buffer critically full ({currentBufferUsage:F1}%), clearing to prevent overflow");
                                _waveProvider.ClearBuffer();
                            }
                            else
                            {
#if DEBUG
                                Logger.Debug($"Audio buffer full, skipping write ({currentBufferUsage:F1}%)");
#endif
                                return;
                            }
                        }

                        if (audioData.Length <= 0 || audioData.Length > _waveProvider.BufferLength)
                        {
                            Logger.Error($"Invalid audio data length: {audioData.Length}");
                            return;
                        }

                        try
                        {
                            _waveProvider.AddSamples(audioData, 0, audioData.Length);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to add samples to audio buffer");
#if DEBUG
                            Logger.Debug(ex, $"Buffer state: {_waveProvider.BufferedBytes}/{_waveProvider.BufferLength}");
#endif
                            try { _waveProvider.ClearBuffer(); } catch { }
                            return;
                        }
                        
#if DEBUG
                        var bufferTimeMs = (_waveProvider.BufferedBytes / (double)_waveProvider.WaveFormat.AverageBytesPerSecond) * 1000.0;
                        if (currentBufferUsage > 85)
                        {
                            Logger.Debug($"Buffer filling: {currentBufferUsage:F1}% ({bufferTimeMs:F0}ms)");
                        }
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write audio data");
#if DEBUG
                Logger.Debug(ex, "Audio write details");
#endif
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sets an external spatial audio provider for pan control
        /// </summary>
        /// <param name="provider">Spatial audio provider, or null to disable</param>
        public void SetSpatialAudioProvider(ISpatialAudioProvider? provider)
        {
            lock (_wasapiLock)
            {
                _spatialAudioProvider = provider;
                Logger.Info($"Spatial audio provider {(provider != null ? "enabled" : "disabled")}");
            }
        }
        
        /// <summary>
        /// Adjusts buffer size based on playback speed for optimal performance
        /// </summary>
        /// <param name="speed">Current playback speed (1.0 = normal)</param>
        public void AdjustBufferForSpeed(double speed)
        {
            lock (_wasapiLock)
            {
                if (_waveProvider == null || _wasapiOut == null)
                    return;
                
                int targetBufferSeconds;
                
                // Use constants for threshold checks and buffer sizes
                if (speed > Constants.FAST_PLAYBACK_THRESHOLD)
                    targetBufferSeconds = Constants.FAST_PLAYBACK_BUFFER_SECONDS;
                else if (speed < Constants.SLOW_PLAYBACK_THRESHOLD)
                    targetBufferSeconds = Constants.SLOW_PLAYBACK_BUFFER_SECONDS;
                else
                    targetBufferSeconds = Constants.NORMAL_PLAYBACK_BUFFER_SECONDS;
                
                var newBufferLength = _waveProvider.WaveFormat.AverageBytesPerSecond * targetBufferSeconds;
                
                if (_waveProvider.BufferLength != newBufferLength)
                {
                    try
                    {
                        // Save current state
                        var wasPlaying = _wasapiOut.PlaybackState == PlaybackState.Playing;
                        var currentVolume = _wasapiOut.Volume;
                        
                        // Stop playback
                        _wasapiOut.Stop();
                        
                        // Recreate wave provider with new buffer size
                        var waveFormat = _waveProvider.WaveFormat;
                        _waveProvider = new BufferedWaveProvider(waveFormat)
                        {
                            ReadFully = true,
                            BufferLength = newBufferLength,
                            DiscardOnBufferOverflow = true
                        };
                        
                        // Reinitialize WASAPI
                        _wasapiOut.Init(_waveProvider);
                        _wasapiOut.Volume = currentVolume;
                        
                        // Resume if was playing
                        if (wasPlaying)
                            _wasapiOut.Play();
                        
                        Logger.Info($"Audio buffer adjusted for {speed:F2}x speed: {targetBufferSeconds}s ({newBufferLength} bytes)");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to adjust audio buffer size");
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies spatial audio (pan) to stereo audio data
        /// </summary>
        /// <param name="packet">Audio packet metadata</param>
        /// <param name="audioData">Audio data (16-bit PCM samples)</param>
        private void ApplySpatialAudio(AudioPacketMetadata packet, short[] audioData)
        {
            if (_spatialAudioProvider == null || audioData.Length == 0)
                return;
            
            try
            {
                var pan = _spatialAudioProvider.GetPanForPilot(packet.TransmitterGuid);
                
                // Clamp pan to valid range
                pan = Math.Clamp(pan, -1.0, 1.0);
                
                // Skip if centered (no pan adjustment needed)
                if (Math.Abs(pan) < 0.01)
                    return;
                
                // Calculate L/R gains using constant power pan law
                // This maintains perceived loudness while panning
                var panAngle = pan * Math.PI / 4.0; // -45° to +45°
                var leftGain = (float)Math.Cos(panAngle);
                var rightGain = (float)Math.Sin(panAngle);
                
                // Note: SRS audio is mono, so we duplicate to stereo for panning
                // For proper stereo output, we'd need to convert mono to stereo
                // For now, we apply differential gains to simulate panning
                
                // Apply pan to audio samples (assuming mono input)
                for (int i = 0; i < audioData.Length; i++)
                {
                    // For mono output, we can only reduce volume on one "virtual" channel
                    // In a true stereo system, we'd split the signal:
                    // audioDataStereo[i*2] = audioData[i] * leftGain;     // Left channel
                    // audioDataStereo[i*2+1] = audioData[i] * rightGain;  // Right channel
                    
                    // For mono, apply average of both gains
                    var averageGain = (leftGain + rightGain) / 2.0f;
                    audioData[i] = (short)Math.Clamp(audioData[i] * averageGain, short.MinValue, short.MaxValue);
                }
                
#if DEBUG
                Logger.Debug($"Applied spatial audio: pan={pan:F2}, leftGain={leftGain:F2}, rightGain={rightGain:F2}");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply spatial audio");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Logger.Info($"Disposing audio output engine (wrote {_totalBytesWritten} bytes total)");

            try
            {
                _wasapiOut?.Stop();
                _wasapiOut?.Dispose();
                _waveProvider = null;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error during audio engine disposal");
#if DEBUG
                Logger.Debug(ex, "Disposal details");
#endif
            }

            _disposed = true;
        }
    }
}