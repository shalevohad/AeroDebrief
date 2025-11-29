using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.Core; // For AudioPacketMetadata
using AeroDebrief.Core.Interfaces.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Test audio source with simulated network jitter
    /// Implements variable packet timing to test JitterBuffer and pipeline robustness
    /// </summary>
    public sealed class JitteredAudioSource : IAudioSource, IDisposable
    {
        private readonly double _frequency;
        private readonly int _sampleRate;
        private readonly Queue<AudioPacketMetadata> _packetQueue;
        private readonly Random _random;
        private readonly JitterProfile _profile;
        private int _currentPacketId;
        private DateTime _lastPacketTime;

        public enum JitterProfile
        {
            None,           // No jitter (baseline)
            Mild,           // ±5ms variation
            Moderate,       // ±20ms variation
            Severe,         // ±50ms variation
            PacketLoss,     // 10% packet loss
            BurstLoss,      // Occasional burst losses (3-5 packets)
            Reordering      // Packets arrive out of order
        }

        public JitteredAudioSource(double frequency, JitterProfile profile, int sampleRate = 48000)
        {
            _frequency = frequency;
            _sampleRate = sampleRate;
            _profile = profile;
            _packetQueue = new Queue<AudioPacketMetadata>();
            _random = new Random();
            _lastPacketTime = DateTime.UtcNow;
        }

        #region IAudioSource Implementation

        public double Frequency => _frequency;
        public int SampleRate => _sampleRate;
        public bool HasMoreData => _packetQueue.Count > 0;

        public async Task<AudioPacketMetadata?> ReadNextPacketAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            if (_packetQueue.Count == 0)
                return null;

            var delay = CalculateJitterDelay();
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var packet = _packetQueue.Dequeue();
            _lastPacketTime = DateTime.UtcNow;
            
            return packet;
        }

        public void Reset()
        {
            _packetQueue.Clear();
            _currentPacketId = 0;
            _lastPacketTime = DateTime.UtcNow;
        }

        #endregion

        private int CalculateJitterDelay()
        {
            return _profile switch
            {
                JitterProfile.None => 0,
                JitterProfile.Mild => _random.Next(-5, 6),
                JitterProfile.Moderate => _random.Next(-20, 21),
                JitterProfile.Severe => _random.Next(-50, 51),
                JitterProfile.PacketLoss => _random.NextDouble() < 0.1 ? 1000 : 0,
                JitterProfile.BurstLoss => _random.NextDouble() < 0.05 ? _random.Next(60, 101) : 0,
                JitterProfile.Reordering => _random.Next(-10, 30),
                _ => 0
            };
        }

        public void EnqueueTestTone(double toneFrequency, int sampleCount, float amplitude = 0.5f)
        {
            var audioData = GenerateTestTone(toneFrequency, sampleCount, amplitude);
            var pcmBytes = ConvertFloatToPCM16(audioData);

            if (_profile == JitterProfile.Reordering && _random.NextDouble() < 0.2)
            {
                _currentPacketId += 2;
            }

            var packet = new AudioPacketMetadata(
                Timestamp: DateTime.UtcNow.AddMilliseconds(_currentPacketId * 20),
                Frequency: _frequency,
                Modulation: 2,
                Encryption: 0,
                TransmitterUnitId: 0,
                PacketId: (ulong)_currentPacketId++,
                TransmitterGuid: "JITTER-TEST",
                PlayerData: new PlayerInfo { Name = "JITTER-TEST", TransmitterGuid = "JITTER-TEST" },
                SampleRate: _sampleRate,
                ChannelCount: 1,
                Coalition: 0,
                AudioPayload: pcmBytes
            );

            _packetQueue.Enqueue(packet);
        }

        public void EnqueueContinuousTone(double toneFrequency, int packetCount, int samplesPerPacket = 960, float amplitude = 0.5f)
        {
            for (int i = 0; i < packetCount; i++)
            {
                EnqueueTestTone(toneFrequency, samplesPerPacket, amplitude);
            }
        }

        private float[] GenerateTestTone(double toneFrequency, int sampleCount, float amplitude)
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

        public void Dispose()
        {
            _packetQueue.Clear();
        }
    }

    /// <summary>
    /// Network jitter simulation tests for audio pipeline
    /// Tests system behavior under various network conditions
    /// </summary>
    [TestClass]
    public class AudioJitterTests
    {
        private TestAudioCapture? _audioCapture;
        private MasterMixer? _mixer;

        [TestInitialize]
        public async Task Setup()
        {
            _audioCapture = new TestAudioCapture();
            await _audioCapture.InitializeAsync();

            _mixer = new MasterMixer(_audioCapture);

            await Task.Delay(100);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _mixer?.Dispose();
            _audioCapture?.Dispose();

            await Task.Delay(200);
        }

        [TestMethod]
        [TestCategory("JitterTest")]
        public async Task NoJitter_Baseline()
        {
            var frequency = 251_000_000.0;
            var jitteredSource = new JitteredAudioSource(frequency, JitteredAudioSource.JitterProfile.None);
            jitteredSource.EnqueueContinuousTone(440.0, packetCount: 20, amplitude: 0.5f);

            var worker = new UserWorker("BASELINE", frequency, jitteredSource);
            _mixer!.RegisterUserWorker(frequency, "BASELINE", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            await Task.Delay(500);

            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            var stats = _mixer.GetStats();

            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio");
            Assert.IsTrue(stats.FramesMixed > 0, "Should have mixed frames");

            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.1f, $"Baseline should have good signal, got RMS={rms:F4}");

            jitteredSource.Dispose();
            await worker.DisposeAsync();

            Console.WriteLine($"? No Jitter Baseline: RMS={rms:F4}, Frames={stats.FramesMixed}");
        }

        [TestMethod]
        [TestCategory("JitterTest")]
        public async Task MildJitter_GoodQuality()
        {
            var frequency = 251_000_000.0;
            var jitteredSource = new JitteredAudioSource(frequency, JitteredAudioSource.JitterProfile.Mild);
            jitteredSource.EnqueueContinuousTone(440.0, packetCount: 20, amplitude: 0.5f);

            var worker = new UserWorker("MILD-JITTER", frequency, jitteredSource);
            _mixer!.RegisterUserWorker(frequency, "MILD-JITTER", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            await Task.Delay(500);

            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            var stats = _mixer.GetStats();

            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio with mild jitter");

            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.08f, $"Mild jitter should maintain quality, got RMS={rms:F4}");

            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.6f);
            Assert.IsFalse(hasClicks, "Mild jitter should not cause clicks");

            jitteredSource.Dispose();
            await worker.DisposeAsync();

            Console.WriteLine($"? Mild Jitter (±5ms): RMS={rms:F4}, Frames={stats.FramesMixed}");
        }

        [TestMethod]
        [TestCategory("JitterTest")]
        public async Task ModerateJitter_AcceptableQuality()
        {
            var frequency = 251_000_000.0;
            var jitteredSource = new JitteredAudioSource(frequency, JitteredAudioSource.JitterProfile.Moderate);
            jitteredSource.EnqueueContinuousTone(440.0, packetCount: 25, amplitude: 0.5f);

            var worker = new UserWorker("MODERATE-JITTER", frequency, jitteredSource);
            _mixer!.RegisterUserWorker(frequency, "MODERATE-JITTER", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            await Task.Delay(600);

            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            var stats = _mixer.GetStats();

            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio with moderate jitter");

            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.05f, $"Moderate jitter should maintain usable quality, got RMS={rms:F4}");

            jitteredSource.Dispose();
            await worker.DisposeAsync();

            Console.WriteLine($"? Moderate Jitter (±20ms): RMS={rms:F4}, Frames={stats.FramesMixed}");
        }

        [TestMethod]
        [TestCategory("JitterTest")]
        public async Task SevereJitter_MaintainsStability()
        {
            var frequency = 251_000_000.0;
            var jitteredSource = new JitteredAudioSource(frequency, JitteredAudioSource.JitterProfile.Severe);
            jitteredSource.EnqueueContinuousTone(440.0, packetCount: 30, amplitude: 0.5f);

            var worker = new UserWorker("SEVERE-JITTER", frequency, jitteredSource);
            _mixer!.RegisterUserWorker(frequency, "SEVERE-JITTER", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            await Task.Delay(800);

            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            var stats = _mixer.GetStats();

            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio even with severe jitter");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Severe jitter should not cause invalid samples");

            Assert.IsTrue(stats.FramesMixed > 0, "Should continue mixing despite severe jitter");

            jitteredSource.Dispose();
            await worker.DisposeAsync();

            Console.WriteLine($"? Severe Jitter (±50ms): Frames={stats.FramesMixed}, Underruns={stats.Underruns}");
        }

        [TestMethod]
        [TestCategory("JitterTest")]
        public async Task PacketLoss_GracefulDegradation()
        {
            var frequency = 251_000_000.0;
            var jitteredSource = new JitteredAudioSource(frequency, JitteredAudioSource.JitterProfile.PacketLoss);
            jitteredSource.EnqueueContinuousTone(440.0, packetCount: 30, amplitude: 0.5f);

            var worker = new UserWorker("PACKET-LOSS", frequency, jitteredSource);
            _mixer!.RegisterUserWorker(frequency, "PACKET-LOSS", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            await Task.Delay(800);

            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            var stats = _mixer.GetStats();

            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio despite packet loss");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Packet loss should not cause invalid samples");

            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.02f, $"Should maintain some signal despite loss, got RMS={rms:F4}");

            jitteredSource.Dispose();
            await worker.DisposeAsync();

            Console.WriteLine($"? Packet Loss (10%): RMS={rms:F4}, Frames={stats.FramesMixed}");
        }
    }
}
