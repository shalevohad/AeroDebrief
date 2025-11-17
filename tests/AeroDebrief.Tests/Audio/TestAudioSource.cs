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
        private Task? _continuousGenerationTask;
        private CancellationTokenSource? _generationCts;

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

        #region Continuous Real-Time Generation

        /// <summary>
        /// Starts continuous real-time packet generation for stress testing.
        /// Generates packets at 50 Hz (20ms intervals) to simulate real radio transmission.
        /// 
        /// NEW: Pre-buffers initial packets before returning to eliminate startup underruns.
        /// </summary>
        /// <param name="toneFrequency">Frequency of the test tone in Hz (e.g., 440 Hz = A4)</param>
        /// <param name="amplitude">Amplitude of the signal (0.0 to 1.0, recommend 0.3 for stress tests)</param>
        /// <param name="samplesPerPacket">Number of samples per packet (default: 960 = 20ms @ 48kHz)</param>
        /// <param name="preBufferPackets">Number of packets to pre-generate before starting (default: 10 = 200ms buffer)</param>
        public void StartContinuousGeneration(double toneFrequency, float amplitude = 0.3f, int samplesPerPacket = 960, int preBufferPackets = 10)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Continuous generation is already running. Call StopContinuousGenerationAsync first.");
            }

            _isRunning = true;
            _generationCts = new CancellationTokenSource();

            // CRITICAL: Pre-buffer packets BEFORE starting background generation
            // This ensures UserWorkers have immediate data availability
            Console.WriteLine($"[TestAudioSource] Pre-buffering {preBufferPackets} packets ({preBufferPackets * 20}ms) before starting generation...");
            
            for (int i = 0; i < preBufferPackets; i++)
            {
                // Generate tone packet
                var audioData = GenerateTestTone(toneFrequency, samplesPerPacket, amplitude);
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
            
            Console.WriteLine($"[TestAudioSource] Pre-buffer complete: {_packetQueue.Count} packets ready");

            // Start background generation task
            _continuousGenerationTask = Task.Run(async () =>
            {
                var interval = TimeSpan.FromMilliseconds(20); // 50 packets/sec = 20ms interval
                var nextPacketTime = DateTime.UtcNow;

                try
                {
                    while (!_generationCts.Token.IsCancellationRequested)
                    {
                        // Generate tone packet
                        var audioData = GenerateTestTone(toneFrequency, samplesPerPacket, amplitude);
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
                        
                        lock (_packetQueue)
                        {
                            _packetQueue.Enqueue(packet);
                            
                            // Prevent queue from growing too large (keep max 50 packets = 1 second)
                            while (_packetQueue.Count > 50)
                            {
                                _packetQueue.Dequeue();
                            }
                        }

                        // Maintain timing
                        nextPacketTime = nextPacketTime.Add(interval);
                        var delay = nextPacketTime - DateTime.UtcNow;
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, _generationCts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
            }, _generationCts.Token);
        }

        /// <summary>
        /// Stops continuous generation
        /// </summary>
        public async Task StopContinuousGenerationAsync()
        {
            _isRunning = false;
            _generationCts?.Cancel();

            if (_continuousGenerationTask != null)
            {
                try
                {
                    await _continuousGenerationTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _generationCts?.Dispose();
            _generationCts = null;
            _continuousGenerationTask = null;
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

        /// <summary>
        /// Generates realistic voice-like audio with push-to-talk patterns.
        /// Simulates radio transmissions with talk bursts (2-6s) and silence gaps (1-4s).
        /// Uses multi-tone synthesis to create voice-like formants.
        /// </summary>
        /// <param name="durationSeconds">Total duration in seconds</param>
        /// <param name="pilotOffset">Offset for voice variation (different pilots have different pitch)</param>
        /// <param name="seed">Random seed for reproducible patterns</param>
        /// <returns>Audio samples with realistic talk patterns</returns>
        public float[] GenerateTalkingPattern(double durationSeconds, int pilotOffset = 0, int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            int totalSamples = (int)(durationSeconds * _sampleRate);
            var audioBuffer = new float[totalSamples];

            // Push-to-talk state machine
            bool isTalking = false;
            int remainingTalkSamples = 0;
            int remainingSilenceSamples = (int)(2.0 * _sampleRate); // Start with 2s silence

            // Voice parameters (varies per pilot)
            double fundamentalFreq = 120.0 + (pilotOffset * 15.0); // 120-180 Hz pitch
            double phaseAccumulator = 0.0;
            double prosodyPhase = 0.0;

            for (int sampleIdx = 0; sampleIdx < totalSamples; sampleIdx++)
            {
                // State machine: talking or silent
                if (!isTalking && remainingSilenceSamples <= 0)
                {
                    // Start talking (2-6 second bursts)
                    isTalking = true;
                    remainingTalkSamples = random.Next(
                        (int)(2.0 * _sampleRate),
                        (int)(6.0 * _sampleRate)
                    );
                    remainingSilenceSamples = 0;
                    prosodyPhase = 0.0;
                }
                else if (isTalking && remainingTalkSamples <= 0)
                {
                    // Stop talking (1-4 second silence)
                    isTalking = false;
                    remainingSilenceSamples = random.Next(
                        (int)(1.0 * _sampleRate),
                        (int)(4.0 * _sampleRate)
                    );
                    remainingTalkSamples = 0;
                }

                float sample;

                if (isTalking)
                {
                    // Generate voice-like audio with formants
                    sample = GenerateVoiceSample(
                        ref phaseAccumulator,
                        ref prosodyPhase,
                        fundamentalFreq,
                        remainingTalkSamples,
                        sampleIdx,
                        pilotOffset,
                        random
                    );
                    remainingTalkSamples--;
                }
                else
                {
                    // Silence with low noise floor
                    sample = (float)((random.NextDouble() - 0.5) * 0.001); // -60 dB noise
                    remainingSilenceSamples--;
                }

                audioBuffer[sampleIdx] = sample;
            }

            return audioBuffer;
        }

        /// <summary>
        /// Generate a single voice sample with realistic formant structure.
        /// Uses additive synthesis with multiple frequency components.
        /// </summary>
        private float GenerateVoiceSample(
            ref double phaseAccumulator,
            ref double prosodyPhase,
            double fundamentalFreq,
            int remainingSamples,
            int globalSampleIdx,
            int pilotOffset,
            Random random)
        {
            // Prosody (pitch variation for natural speech)
            prosodyPhase += 0.5 / _sampleRate; // ~0.5 Hz prosody rate
            double pitchModulation = 1.0 + (Math.Sin(prosodyPhase) * 0.15); // ±15% pitch variation
            double currentFreq = fundamentalFreq * pitchModulation;

            // Phase increment
            double phaseIncrement = 2.0 * Math.PI * currentFreq / _sampleRate;
            phaseAccumulator += phaseIncrement;
            if (phaseAccumulator > 2.0 * Math.PI)
                phaseAccumulator -= 2.0 * Math.PI;

            // Fundamental (sawtooth for rich harmonics)
            double fundamental = ((phaseAccumulator / Math.PI) - 1.0) * 0.3;

            // Formants (vowel sounds)
            double vowelPhase = (globalSampleIdx + pilotOffset * 1000) / (double)_sampleRate;
            
            // F1: 300-900 Hz (vowel height)
            double f1Freq = 500 + Math.Sin(vowelPhase * 2.0) * 200;
            double formant1 = Math.Sin(phaseAccumulator * (f1Freq / currentFreq)) * 0.25;

            // F2: 800-2500 Hz (vowel frontness)
            double f2Freq = 1500 + Math.Cos(vowelPhase * 3.0) * 500;
            double formant2 = Math.Sin(phaseAccumulator * (f2Freq / currentFreq)) * 0.15;

            // F3: 2000-3500 Hz (voice quality)
            double f3Freq = 2800 + Math.Sin(vowelPhase * 5.0) * 400;
            double formant3 = Math.Sin(phaseAccumulator * (f3Freq / currentFreq)) * 0.08;

            // Consonant noise (15% of the time)
            double consonantNoise = 0.0;
            if (random.NextDouble() < 0.15)
            {
                consonantNoise = (random.NextDouble() - 0.5) * 0.12;
            }

            // Combine components
            double voiceSample = fundamental + formant1 + formant2 + formant3 + consonantNoise;

            // Envelope (smooth attack/release)
            double envelope = CalculateVoiceEnvelope(remainingSamples);

            // Breathiness
            double breath = (random.NextDouble() - 0.5) * 0.03;

            // Final sample with soft clipping
            float finalSample = (float)((voiceSample + breath) * envelope * 0.35);
            finalSample = (float)Math.Tanh(finalSample * 1.5) * 0.7f; // Soft clip

            return finalSample;
        }

        /// <summary>
        /// Calculate voice envelope with attack and release.
        /// </summary>
        private double CalculateVoiceEnvelope(int remainingSamples)
        {
            double releaseTime = 0.2; // 200ms release
            int releaseSamples = (int)(releaseTime * _sampleRate);

            if (remainingSamples > releaseSamples)
            {
                return 1.0; // Sustain phase
            }
            else
            {
                // Release phase
                double releaseEnvelope = (double)remainingSamples / releaseSamples;
                return Math.Max(0.0, Math.Min(1.0, releaseEnvelope));
            }
        }

        /// <summary>
        /// Enqueues packets with realistic push-to-talk talking patterns.
        /// Generates voice-like audio that matches amplitude visualization patterns.
        /// </summary>
        /// <param name="durationSeconds">Duration of the transmission sequence</param>
        /// <param name="pilotOffset">Offset for voice variation (0 for first pilot, 1 for second, etc.)</param>
        /// <param name="seed">Random seed for reproducible patterns (optional)</param>
        public void EnqueueTalkingPattern(double durationSeconds, int pilotOffset = 0, int? seed = null)
        {
            // Generate complete talking pattern
            var audioSamples = GenerateTalkingPattern(durationSeconds, pilotOffset, seed);
            
            // Break into 20ms packets (960 samples @ 48kHz)
            int samplesPerPacket = 960;
            int totalPackets = (int)Math.Ceiling((double)audioSamples.Length / samplesPerPacket);

            for (int packetIdx = 0; packetIdx < totalPackets; packetIdx++)
            {
                int startIdx = packetIdx * samplesPerPacket;
                int remainingSamples = audioSamples.Length - startIdx;
                int currentPacketSize = Math.Min(samplesPerPacket, remainingSamples);

                // Extract packet samples
                var packetSamples = new float[currentPacketSize];
                Array.Copy(audioSamples, startIdx, packetSamples, 0, currentPacketSize);

                // Convert to PCM and enqueue
                var pcmBytes = ConvertFloatToPCM16(packetSamples);
                
                var packet = new AudioPacketMetadata(
                    Timestamp: DateTime.UtcNow.AddMilliseconds(_currentPacketId * 20),
                    Frequency: _frequency,
                    Modulation: 2,
                    Encryption: 0,
                    TransmitterUnitId: 0,
                    PacketId: (ulong)_currentPacketId++,
                    TransmitterGuid: $"TEST-PILOT-{pilotOffset}",
                    PlayerData: new PlayerInfo 
                    { 
                        Name = $"Test Pilot {pilotOffset + 1}", 
                        TransmitterGuid = $"TEST-PILOT-{pilotOffset}" 
                    },
                    SampleRate: _sampleRate,
                    ChannelCount: 1,
                    Coalition: 0,
                    AudioPayload: pcmBytes
                );
                
                _packetQueue.Enqueue(packet);
            }
        }

        #endregion

        public void Stop()
        {
            _isRunning = false;
        }

        public async void Dispose()
        {
            await StopContinuousGenerationAsync();
            _packetQueue.Clear();
        }
    }
}
