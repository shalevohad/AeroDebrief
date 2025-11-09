using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core; // For AudioPacketMetadata

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Test audio source that generates synthetic audio data for testing
    /// Provides various audio signal types for quality analysis
    /// Implements IAudioSource for direct injection into UserWorker pipeline
    /// </summary>
    public sealed class TestAudioSource : IAudioSource, IDisposable
    {
        private readonly double _frequency;
        private readonly int _sampleRate;
        private readonly Queue<AudioPacketMetadata> _packetQueue;
        private bool _isRunning;
        private int _currentPacketId;

        public TestAudioSource(double frequency, int sampleRate = 48000)
        {
            _frequency = frequency;
            _sampleRate = sampleRate;
            _packetQueue = new Queue<AudioPacketMetadata>();
        }

        #region IAudioSource Implementation

        public double Frequency => _frequency;
        public int SampleRate => _sampleRate;
        public bool HasMoreData => _packetQueue.Count > 0 || _isRunning;

        public Task<AudioPacketMetadata?> ReadNextPacketAsync(CancellationToken cancellationToken = default)
        {
            if (_packetQueue.Count > 0)
            {
                return Task.FromResult<AudioPacketMetadata?>(_packetQueue.Dequeue());
            }

            return Task.FromResult<AudioPacketMetadata?>(null);
        }

        public void Reset()
        {
            _packetQueue.Clear();
            _currentPacketId = 0;
        }

        #endregion

        #region Audio Generation Methods

        /// <summary>
        /// Enqueues a test tone packet for processing
        /// </summary>
        public void EnqueueTestTone(double toneFrequency, int sampleCount, float amplitude = 0.5f)
        {
            var audioData = GenerateTestTone(toneFrequency, sampleCount, amplitude);
            var pcmBytes = ConvertFloatToPCM16(audioData);
            
            var packet = new AudioPacketMetadata(
                Timestamp: DateTime.UtcNow.AddMilliseconds(_currentPacketId * 20),
                Frequency: _frequency,
                Modulation: 2, // AM
                Encryption: 0,
                TransmitterUnitId: 0,
                PacketId: (ulong)_currentPacketId++,
                TransmitterGuid: "TEST-SOURCE",
                PlayerData: new PlayerInfo { Name = "TEST-SOURCE", TransmitterGuid = "TEST-SOURCE" },
                SampleRate: _sampleRate,
                ChannelCount: 1,
                Coalition: 0,
                AudioPayload: pcmBytes
            );
            
            _packetQueue.Enqueue(packet);
        }

        /// <summary>
        /// Enqueues multiple test tone packets to simulate continuous audio stream
        /// </summary>
        public void EnqueueContinuousTone(double toneFrequency, int packetCount, int samplesPerPacket = 960, float amplitude = 0.5f)
        {
            for (int i = 0; i < packetCount; i++)
            {
                EnqueueTestTone(toneFrequency, samplesPerPacket, amplitude);
            }
        }

        /// <summary>
        /// Enqueues a test tone with fade-in
        /// </summary>
        public void EnqueueTestToneWithFadeIn(double toneFrequency, int sampleCount, int fadeSamples, float amplitude = 0.5f)
        {
            var audioData = GenerateWithFadeIn(toneFrequency, sampleCount, fadeSamples, amplitude);
            var pcmBytes = ConvertFloatToPCM16(audioData);
            
            var packet = new AudioPacketMetadata(
                Timestamp: DateTime.UtcNow.AddMilliseconds(_currentPacketId * 20),
                Frequency: _frequency,
                Modulation: 2,
                Encryption: 0,
                TransmitterUnitId: 0,
                PacketId: (ulong)_currentPacketId++,
                TransmitterGuid: "TEST-SOURCE",
                PlayerData: new PlayerInfo { Name = "TEST-SOURCE", TransmitterGuid = "TEST-SOURCE" },
                SampleRate: _sampleRate,
                ChannelCount: 1,
                Coalition: 0,
                AudioPayload: pcmBytes
            );
            
            _packetQueue.Enqueue(packet);
        }

        /// <summary>
        /// Enqueues silence packet
        /// </summary>
        public void EnqueueSilence(int sampleCount)
        {
            var audioData = GenerateSilence(sampleCount);
            var pcmBytes = ConvertFloatToPCM16(audioData);
            
            var packet = new AudioPacketMetadata(
                Timestamp: DateTime.UtcNow.AddMilliseconds(_currentPacketId * 20),
                Frequency: _frequency,
                Modulation: 2,
                Encryption: 0,
                TransmitterUnitId: 0,
                PacketId: (ulong)_currentPacketId++,
                TransmitterGuid: "TEST-SOURCE",
                PlayerData: new PlayerInfo { Name = "TEST-SOURCE", TransmitterGuid = "TEST-SOURCE" },
                SampleRate: _sampleRate,
                ChannelCount: 1,
                Coalition: 0,
                AudioPayload: pcmBytes
            );
            
            _packetQueue.Enqueue(packet);
        }

        /// <summary>
        /// Generates a test tone at specified frequency
        /// </summary>
        /// <param name="toneFrequency">Frequency of the test tone in Hz (e.g., 440 for A4)</param>
        /// <param name="sampleCount">Number of samples to generate</param>
        /// <param name="amplitude">Amplitude (0.0 to 1.0)</param>
        /// <returns>Audio samples</returns>
        public float[] GenerateTestTone(double toneFrequency, int sampleCount, float amplitude = 0.5f)
        {
            var samples = new float[sampleCount];
            amplitude = Math.Clamp(amplitude, 0f, 1f);

            for (int i = 0; i < sampleCount; i++)
            {
                var time = i / (double)_sampleRate;
                samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * toneFrequency * time);
            }

            return samples;
        }

        /// <summary>
        /// Generates a test tone as 16-bit PCM bytes
        /// </summary>
        public byte[] GenerateTestToneBytes(double toneFrequency, int sampleCount, float amplitude = 0.5f)
        {
            var floatSamples = GenerateTestTone(toneFrequency, sampleCount, amplitude);
            return ConvertFloatToPCM16(floatSamples);
        }

        /// <summary>
        /// Generates multi-tone audio (multiple frequencies mixed together)
        /// </summary>
        public float[] GenerateMultiTone(double[] frequencies, int sampleCount, float[] amplitudes = null!)
        {
            if (frequencies == null || frequencies.Length == 0)
                return new float[sampleCount];

            amplitudes ??= Enumerable.Repeat(0.5f / frequencies.Length, frequencies.Length).ToArray();

            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                var time = i / (double)_sampleRate;
                float sum = 0f;

                for (int f = 0; f < frequencies.Length; f++)
                {
                    sum += amplitudes[f] * (float)Math.Sin(2 * Math.PI * frequencies[f] * time);
                }

                samples[i] = Math.Clamp(sum, -1f, 1f);
            }

            return samples;
        }

        /// <summary>
        /// Generates silence
        /// </summary>
        public float[] GenerateSilence(int sampleCount)
        {
            return new float[sampleCount];
        }

        /// <summary>
        /// Generates white noise
        /// </summary>
        public float[] GenerateWhiteNoise(int sampleCount, float amplitude = 0.1f)
        {
            var random = new Random();
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = amplitude * (float)(random.NextDouble() * 2.0 - 1.0);
            }

            return samples;
        }

        /// <summary>
        /// Generates a test tone with fade-in
        /// </summary>
        public float[] GenerateWithFadeIn(double toneFrequency, int sampleCount, int fadeSamples, float amplitude = 0.5f)
        {
            var samples = GenerateTestTone(toneFrequency, sampleCount, amplitude);

            // Apply fade-in
            fadeSamples = Math.Min(fadeSamples, sampleCount);
            for (int i = 0; i < fadeSamples; i++)
            {
                var fadeGain = (float)i / fadeSamples;
                samples[i] *= fadeGain;
            }

            return samples;
        }

        /// <summary>
        /// Generates a test tone with fade-out
        /// </summary>
        public float[] GenerateWithFadeOut(double toneFrequency, int sampleCount, int fadeSamples, float amplitude = 0.5f)
        {
            var samples = GenerateTestTone(toneFrequency, sampleCount, amplitude);

            // Apply fade-out
            fadeSamples = Math.Min(fadeSamples, sampleCount);
            var fadeStart = sampleCount - fadeSamples;

            for (int i = 0; i < fadeSamples; i++)
            {
                var fadeGain = 1.0f - ((float)i / fadeSamples);
                samples[fadeStart + i] *= fadeGain;
            }

            return samples;
        }

        /// <summary>
        /// Converts float samples to 16-bit PCM bytes
        /// </summary>
        private byte[] ConvertFloatToPCM16(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = Math.Clamp(samples[i], -1f, 1f);
                short pcmSample = (short)(sample * 32767);
                bytes[i * 2] = (byte)(pcmSample & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            return bytes;
        }

        #endregion

        public void Stop()
        {
            _isRunning = false;
        }

        public void Dispose()
        {
            Stop();
            _packetQueue.Clear();
        }
    }
}
