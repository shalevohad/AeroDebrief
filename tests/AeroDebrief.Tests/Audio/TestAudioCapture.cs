using AeroDebrief.Core.Interfaces.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.Core; // For AudioPacketMetadata

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Test implementation of IAudioOutputEngine that captures audio for analysis
    /// instead of playing it through speakers
    /// </summary>
    public sealed class TestAudioCapture : IAudioOutputEngine
    {
        private readonly List<byte[]> _capturedChunks = new();
        private readonly Channel<byte[]> _audioChannel;
        private bool _isCapturing;
        private float _masterVolume = 1.0f;

        public IReadOnlyList<byte[]> CapturedChunks => _capturedChunks;
        public int TotalSamplesCaptured { get; private set; }
        
        /// <summary>
        /// Gets the number of frames captured
        /// </summary>
        public int FrameCount => _capturedChunks.Count;

        /// <summary>
        /// Gets the total number of bytes captured
        /// </summary>
        public long TotalBytes
        {
            get
            {
                lock (_capturedChunks)
                {
                    return _capturedChunks.Sum(f => (long)f.Length);
                }
            }
        }

        public TestAudioCapture()
        {
            _audioChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public Task InitializeAsync()
        {
            _isCapturing = true;
            return Task.CompletedTask;
        }

        public void Start()
        {
            _isCapturing = true;
        }

        public void Stop()
        {
            _isCapturing = false;
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0.0f, 1.0f);
        }

        public float GetMasterVolume()
        {
            return _masterVolume;
        }

        public void ClearBuffer()
        {
            lock (_capturedChunks)
            {
                _capturedChunks.Clear();
                TotalSamplesCaptured = 0;
            }
        }

        public void SetSpatialAudioProvider(ISpatialAudioProvider? provider)
        {
            // Test implementation - spatial audio not needed for stress tests
        }

        public void AdjustBufferForSpeed(double speed)
        {
            // Test implementation - buffer adjustment not needed for stress tests
        }

        public Task WriteAudioAsync(byte[] audioData)
        {
            return WriteAudioAsync(audioData, false, TimeSpan.Zero, null, null);
        }

        public Task WriteAudioAsync(byte[] audioData, bool isSilence, TimeSpan chunkEndTime = default, 
            Action<TimeSpan>? positionUpdater = null, AudioPacketMetadata? packet = null)
        {
            if (!_isCapturing || audioData == null || audioData.Length == 0)
                return Task.CompletedTask;

            // Copy the audio data to avoid reference issues
            var copy = new byte[audioData.Length];
            Buffer.BlockCopy(audioData, 0, copy, 0, audioData.Length);

            lock (_capturedChunks)
            {
                _capturedChunks.Add(copy);
                TotalSamplesCaptured += copy.Length / 2; // 16-bit samples
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets all captured audio as a single float array for analysis
        /// </summary>
        public float[] GetCapturedAudioAsFloat()
        {
            lock (_capturedChunks)
            {
                var totalSamples = TotalSamplesCaptured;
                var result = new float[totalSamples];
                var offset = 0;

                foreach (var chunk in _capturedChunks)
                {
                    for (int i = 0; i < chunk.Length; i += 2)
                    {
                        if (offset >= totalSamples)
                            break;

                        // Convert 16-bit PCM to float (-1.0 to 1.0)
                        short sample = (short)(chunk[i] | (chunk[i + 1] << 8));
                        result[offset++] = sample / 32768.0f;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Gets captured audio as 16-bit PCM samples
        /// </summary>
        public short[] GetCapturedAudioAsPCM()
        {
            lock (_capturedChunks)
            {
                var totalSamples = TotalSamplesCaptured;
                var result = new short[totalSamples];
                var offset = 0;

                foreach (var chunk in _capturedChunks)
                {
                    for (int i = 0; i < chunk.Length; i += 2)
                    {
                        if (offset >= totalSamples)
                            break;

                        result[offset++] = (short)(chunk[i] | (chunk[i + 1] << 8));
                    }
                }

                return result;
            }
        }

        public void Dispose()
        {
            _isCapturing = false;
            ClearBuffer();
        }
    }
}
