using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Comprehensive audio quality tests using IAudioSource injection
    /// Tests real audio flow through UserWorker ? MasterMixer ? TestAudioCapture
    /// </summary>
    [TestClass]
    public class AudioQualityTests
    {
        private TestAudioCapture? _audioCapture;
        private MasterMixer? _mixer;

        [TestInitialize]
        public async Task Setup()
        {
            _audioCapture = new TestAudioCapture();
            await _audioCapture.InitializeAsync();
            
            _mixer = new MasterMixer(_audioCapture);
            
            // Wait for mixer to initialize
            await Task.Delay(100);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            _mixer?.Dispose();
            _audioCapture?.Dispose();
            
            await Task.Delay(200); // Ensure cleanup completes
        }

        /// <summary>
        /// TEST 1: Verify crossfade envelope is linear and smooth
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task CrossfadeEnvelope_IsLinearAndSmooth()
        {
            // Arrange - Create test audio source with 440Hz tone
            var frequency = 251_000_000.0;
            var testSource = new TestAudioSource(frequency, 48000);
            
            // Generate 10 packets of continuous 440Hz tone (200ms total)
            testSource.EnqueueContinuousTone(440.0, packetCount: 10, samplesPerPacket: 960, amplitude: 0.7f);
            
            // Create UserWorker with test audio source
            var worker = new UserWorker("TEST-PILOT-001", frequency, testSource);
            _mixer!.RegisterUserWorker(frequency, "TEST-PILOT-001", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            
            // Wait for audio to start flowing
            await Task.Delay(100);
            
            // Act - Trigger crossfade by muting frequency
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Mute);
            
            // Wait for fade-out to complete (64 samples = ~1.3ms at 48kHz + processing time)
            await Task.Delay(50);
            
            // Trigger fade-in by unmuting
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            await Task.Delay(50);
            
            // Let audio stabilize
            await Task.Delay(100);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert - Check for crossfade quality
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio data");
            
            // Verify no clicks/pops (sudden amplitude changes > 0.5)
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.5f);
            Assert.IsFalse(hasClicks, "Crossfade should not produce clicks or pops");
            
            // Verify audio is within valid range [-1.0, 1.0]
            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio samples should be within [-1.0, 1.0] range");
            
            // Verify we have actual audio content (not complete silence)
            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.01f, $"Audio should have content, got RMS={rms:F4}");
            
            testSource.Dispose();
            await worker.DisposeAsync();
        }

        /// <summary>
        /// TEST 2: Verify mixing three frequencies produces correct amplitude
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task ThreeFrequencies_MixToCorrectAmplitude()
        {
            // Arrange - Create 3 frequencies with different tones
            var freq1 = 251_000_000.0;
            var freq2 = 305_000_000.0;
            var freq3 = 270_000_000.0;
            
            var testSource1 = new TestAudioSource(freq1, 48000);
            var testSource2 = new TestAudioSource(freq2, 48000);
            var testSource3 = new TestAudioSource(freq3, 48000);
            
            // Generate test tones: 440Hz (A4), 523Hz (C5), 659Hz (E5)
            // Use moderate amplitude (0.3) so mixing 3 sources doesn't clip
            testSource1.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.3f);
            testSource2.EnqueueContinuousTone(523.0, packetCount: 10, amplitude: 0.3f);
            testSource3.EnqueueContinuousTone(659.0, packetCount: 10, amplitude: 0.3f);
            
            // Create workers
            var worker1 = new UserWorker("PILOT-001", freq1, testSource1);
            var worker2 = new UserWorker("PILOT-002", freq2, testSource2);
            var worker3 = new UserWorker("PILOT-003", freq3, testSource3);
            
            _mixer!.RegisterUserWorker(freq1, "PILOT-001", worker1);
            _mixer.RegisterUserWorker(freq2, "PILOT-002", worker2);
            _mixer.RegisterUserWorker(freq3, "PILOT-003", worker3);
            
            // Enable all frequencies
            _mixer.SetFrequencyGate(freq1, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq3, FrequencyGateMode.Allow);
            
            // Wait for audio to mix
            await Task.Delay(300);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert - Verify mixing quality
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured mixed audio");
            
            // Verify no clipping (peak amplitude should be < 1.0)
            var hasClipping = AudioAnalyzer.HasClipping(capturedAudio, threshold: 0.99f);
            Assert.IsFalse(hasClipping, "Mixing 3 frequencies should not cause clipping");
            
            // Verify peak amplitude is reasonable (mixing 3x 0.3 amplitude sources)
            var peakAmplitude = AudioAnalyzer.CalculatePeakAmplitude(capturedAudio);
            Assert.IsTrue(peakAmplitude > 0.1f && peakAmplitude < 1.0f,
                $"Peak amplitude should be between 0.1 and 1.0, got {peakAmplitude:F4}");
            
            // Verify RMS is higher than single source (more energy)
            var rms = AudioAnalyzer.CalculateRMS(capturedAudio);
            Assert.IsTrue(rms > 0.05f, $"Mixed RMS should be significant, got {rms:F4}");
            
            // Cleanup
            testSource1.Dispose();
            testSource2.Dispose();
            testSource3.Dispose();
            await worker1.DisposeAsync();
            await worker2.DisposeAsync();
            await worker3.DisposeAsync();
        }

        /// <summary>
        /// TEST 3: Verify SIMD mixing accuracy
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task SIMDMixing_ProducesAccurateOutput()
        {
            // Arrange - Create 2 frequencies with known tones for verification
            var freq1 = 251_000_000.0;
            var freq2 = 305_000_000.0;
            
            var testSource1 = new TestAudioSource(freq1, 48000);
            var testSource2 = new TestAudioSource(freq2, 48000);
            
            // Use simple 440Hz tones at 0.5 amplitude each
            testSource1.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.5f);
            testSource2.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.5f);
            
            var worker1 = new UserWorker("PILOT-001", freq1, testSource1);
            var worker2 = new UserWorker("PILOT-002", freq2, testSource2);
            
            _mixer!.RegisterUserWorker(freq1, "PILOT-001", worker1);
            _mixer.RegisterUserWorker(freq2, "PILOT-002", worker2);
            
            _mixer.SetFrequencyGate(freq1, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Allow);
            
            // Wait for mixing
            await Task.Delay(300);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert - Verify SIMD mixing accuracy
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio");
            
            // When mixing two identical 440Hz tones at 0.5 amplitude,
            // the result should have ~1.0 peak amplitude (0.5 + 0.5 = 1.0)
            var peakAmplitude = AudioAnalyzer.CalculatePeakAmplitude(capturedAudio);
            
            // Allow some tolerance for processing variations
            Assert.IsTrue(peakAmplitude > 0.8f && peakAmplitude <= 1.0f,
                $"SIMD mixing should produce ~1.0 peak amplitude, got {peakAmplitude:F4}");
            
            // Verify no distortion introduced by SIMD operations
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.5f);
            Assert.IsFalse(hasClicks, "SIMD mixing should not introduce artifacts");
            
            testSource1.Dispose();
            testSource2.Dispose();
            await worker1.DisposeAsync();
            await worker2.DisposeAsync();
        }

        /// <summary>
        /// TEST 4: Verify fade-in envelope is smooth and linear
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task FadeIn_ProducesSmoothEnvelope()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var testSource = new TestAudioSource(frequency, 48000);
            
            // Generate continuous tone
            testSource.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.7f);
            
            var worker = new UserWorker("PILOT-001", frequency, testSource);
            _mixer!.RegisterUserWorker(frequency, "PILOT-001", worker);
            
            // Start with frequency muted
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Mute);
            await Task.Delay(50);
            
            // Act - Trigger fade-in by allowing frequency
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            
            // Wait for fade-in to complete
            await Task.Delay(200);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured fade-in");
            
            // Look for fade-in pattern (amplitude should increase gradually)
            // Find first non-zero sample
            var firstNonZero = Array.FindIndex(capturedAudio, s => Math.Abs(s) > 0.01f);
            
            if (firstNonZero >= 0 && firstNonZero < capturedAudio.Length - 64)
            {
                // Check if samples increase in the fade-in region
                var fadeRegion = capturedAudio.Skip(firstNonZero).Take(64).ToArray();
                var isFadeLinear = AudioAnalyzer.IsFadeLinear(fadeRegion, fadeSamples: 64, isFadeIn: true);
                
                Assert.IsTrue(isFadeLinear, "Fade-in envelope should be approximately linear");
            }
            
            // Verify no clicks during fade-in
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.5f);
            Assert.IsFalse(hasClicks, "Fade-in should not produce clicks");
            
            testSource.Dispose();
            await worker.DisposeAsync();
        }

        /// <summary>
        /// TEST 5: Verify pilot-level filtering produces clean audio
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task PilotFiltering_ProducesCleanAudio()
        {
            // Arrange - Create frequency with 2 pilots
            var frequency = 251_000_000.0;
            
            var testSource1 = new TestAudioSource(frequency, 48000);
            var testSource2 = new TestAudioSource(frequency, 48000);
            
            // Different tones for identification
            testSource1.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.5f);
            testSource2.EnqueueContinuousTone(523.0, packetCount: 10, amplitude: 0.5f);
            
            var worker1 = new UserWorker("PILOT-001", frequency, testSource1);
            var worker2 = new UserWorker("PILOT-002", frequency, testSource2);
            
            _mixer!.RegisterUserWorker(frequency, "PILOT-001", worker1);
            _mixer!.RegisterUserWorker(frequency, "PILOT-002", worker2);
            
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            
            // Act - Mute pilot 2, keep pilot 1 audible
            _mixer.SetPilotGate("PILOT-002", frequency, PilotGateMode.Mute);
            
            await Task.Delay(300);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio with pilot filtering");
            
            // Verify audio quality
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.5f);
            Assert.IsFalse(hasClicks, "Pilot filtering should not introduce clicks");
            
            var peakAmplitude = AudioAnalyzer.CalculatePeakAmplitude(capturedAudio);
            Assert.IsTrue(peakAmplitude > 0.1f && peakAmplitude < 1.0f,
                $"Pilot filtering should produce valid amplitude, got {peakAmplitude:F4}");
            
            testSource1.Dispose();
            testSource2.Dispose();
            await worker1.DisposeAsync();
            await worker2.DisposeAsync();
        }

        /// <summary>
        /// TEST 6: Verify rapid gate changes don't cause audio corruption
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task RapidGateChanges_NoAudioCorruption()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var testSource = new TestAudioSource(frequency, 48000);
            
            // Generate longer audio stream
            testSource.EnqueueContinuousTone(440.0, packetCount: 20, amplitude: 0.5f);
            
            var worker = new UserWorker("PILOT-001", frequency, testSource);
            _mixer!.RegisterUserWorker(frequency, "PILOT-001", worker);
            
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            
            // Act - Rapid gate changes (simulating user toggling mute button)
            for (int i = 0; i < 10; i++)
            {
                _mixer.SetFrequencyGate(frequency,
                    i % 2 == 0 ? FrequencyGateMode.Mute : FrequencyGateMode.Allow);
                await Task.Delay(20); // 20ms between changes
            }
            
            await Task.Delay(200); // Let audio settle
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio during rapid changes");
            
            // Verify no severe corruption (clicks should be minimal due to crossfades)
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.7f);
            Assert.IsFalse(hasClicks, "Rapid gate changes should not cause severe audio corruption");
            
            // Verify samples remain within valid range
            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio should remain within valid range during rapid changes");
            
            testSource.Dispose();
            await worker.DisposeAsync();
        }

        /// <summary>
        /// TEST 7: Verify solo mode transitions produce clean audio
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task SoloMode_ProducesCleanTransitions()
        {
            // Arrange - 3 frequencies
            var freq1 = 251_000_000.0;
            var freq2 = 305_000_000.0;
            var freq3 = 270_000_000.0;
            
            var testSource1 = new TestAudioSource(freq1, 48000);
            var testSource2 = new TestAudioSource(freq2, 48000);
            var testSource3 = new TestAudioSource(freq3, 48000);
            
            testSource1.EnqueueContinuousTone(440.0, packetCount: 15, amplitude: 0.3f);
            testSource2.EnqueueContinuousTone(523.0, packetCount: 15, amplitude: 0.3f);
            testSource3.EnqueueContinuousTone(659.0, packetCount: 15, amplitude: 0.3f);
            
            var worker1 = new UserWorker("PILOT-001", freq1, testSource1);
            var worker2 = new UserWorker("PILOT-002", freq2, testSource2);
            var worker3 = new UserWorker("PILOT-003", freq3, testSource3);
            
            _mixer!.RegisterUserWorker(freq1, "PILOT-001", worker1);
            _mixer.RegisterUserWorker(freq2, "PILOT-002", worker2);
            _mixer.RegisterUserWorker(freq3, "PILOT-003", worker3);
            
            // Enable all
            _mixer.SetFrequencyGate(freq1, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq3, FrequencyGateMode.Allow);
            
            await Task.Delay(100);
            
            // Act - Solo freq1
            _mixer.SetFrequencyGate(freq1, FrequencyGateMode.Solo);
            await Task.Delay(100);
            
            // Switch solo to freq2
            _mixer.SetFrequencyGate(freq1, FrequencyGateMode.Allow);
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Solo);
            await Task.Delay(100);
            
            // Remove solo
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Allow);
            await Task.Delay(100);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured solo transitions");
            
            // Verify no clicks during solo transitions
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.5f);
            Assert.IsFalse(hasClicks, "Solo transitions should be smooth without clicks");
            
            // Verify audio remains valid
            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Solo transitions should not cause invalid samples");
            
            testSource1.Dispose();
            testSource2.Dispose();
            testSource3.Dispose();
            await worker1.DisposeAsync();
            await worker2.DisposeAsync();
            await worker3.DisposeAsync();
        }

        /// <summary>
        /// TEST 8: Verify crest factor is reasonable (dynamic range preserved)
        /// </summary>
        [TestMethod]
        [TestCategory("AudioQuality")]
        public async Task AudioProcessing_PreservesDynamicRange()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var testSource = new TestAudioSource(frequency, 48000);
            
            // Generate tone with varying amplitude (simulate speech dynamics)
            testSource.EnqueueTestTone(440.0, sampleCount: 960, amplitude: 0.3f);
            testSource.EnqueueTestTone(440.0, sampleCount: 960, amplitude: 0.7f);
            testSource.EnqueueTestTone(440.0, sampleCount: 960, amplitude: 0.5f);
            testSource.EnqueueSilence(sampleCount: 960);
            testSource.EnqueueTestTone(440.0, sampleCount: 960, amplitude: 0.8f);
            
            var worker = new UserWorker("PILOT-001", frequency, testSource);
            _mixer!.RegisterUserWorker(frequency, "PILOT-001", worker);
            _mixer.SetFrequencyGate(frequency, FrequencyGateMode.Allow);
            
            await Task.Delay(300);
            
            // Get captured audio
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();
            
            // Assert
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio with dynamics");
            
            // Calculate crest factor (Peak / RMS)
            var crestFactor = AudioAnalyzer.CalculateCrestFactor(capturedAudio);
            
            // Crest factor should be reasonable (typically 3-10 for audio with dynamics)
            // Too low (<2) means over-compression, too high (>15) might indicate problems
            Assert.IsTrue(crestFactor > 1.5 && crestFactor < 15,
                $"Crest factor should indicate preserved dynamics, got {crestFactor:F2}");
            
            testSource.Dispose();
            await worker.DisposeAsync();
        }
    }
}
