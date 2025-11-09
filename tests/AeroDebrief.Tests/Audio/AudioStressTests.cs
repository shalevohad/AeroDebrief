using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Stress tests for MasterMixer under extreme load conditions
    /// Tests system behavior with 10+ simultaneous frequencies and prolonged operation
    /// </summary>
    [TestClass]
    public class AudioStressTests
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
        /// TEST 1: Extreme load - 10 simultaneous frequencies
        /// Verifies mixer can handle high frequency count without performance degradation
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("ExtremeLoad")]
        public async Task TenSimultaneousFrequencies_MaintainsStability()
        {
            // Arrange - Create 10 different frequencies with unique tones
            var frequencies = new List<double>
            {
                251_000_000.0, 305_000_000.0, 270_000_000.0, 243_000_000.0, 282_000_000.0,
                225_000_000.0, 340_000_000.0, 360_000_000.0, 380_000_000.0, 395_000_000.0
            };

            var testSources = new List<TestAudioSource>();
            var workers = new List<UserWorker>();

            // Generate different tone frequencies for each channel (musical scale)
            var toneFrequencies = new double[] { 
                261.63, 293.66, 329.63, 349.23, 392.00, 
                440.00, 493.88, 523.25, 587.33, 659.25 
            };

            // Create test sources and workers for all 10 frequencies
            for (int i = 0; i < frequencies.Count; i++)
            {
                var testSource = new TestAudioSource(frequencies[i], 48000);
                testSource.EnqueueContinuousTone(toneFrequencies[i], packetCount: 15, amplitude: 0.15f); // Lower amplitude to prevent clipping
                testSources.Add(testSource);

                var worker = new UserWorker($"PILOT-{i:D3}", frequencies[i], testSource);
                workers.Add(worker);

                _mixer!.RegisterUserWorker(frequencies[i], $"PILOT-{i:D3}", worker);
                _mixer.SetFrequencyGate(frequencies[i], FrequencyGateMode.Allow);
            }

            var stopwatch = Stopwatch.StartNew();

            // Act - Let all 10 frequencies mix for 500ms
            await Task.Delay(500);

            stopwatch.Stop();

            // Get mixer stats
            var stats = _mixer!.GetStats();
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();

            // Assert - Verify system stability under extreme load
            Assert.AreEqual(10, stats.ActiveFrequencies, 
                "Should have 10 active frequencies registered");

            Assert.IsTrue(stats.FramesMixed > 0, 
                "Mixer should have produced frames with 10 frequencies");

            // Verify reasonable underrun rate even under extreme load (< 5%)
            var underrunRate = stats.Underruns / (double)Math.Max(1, stats.FramesMixed);
            Assert.IsTrue(underrunRate < 0.05, 
                $"Underrun rate should be < 5% even with 10 frequencies, got {underrunRate:P1}");

            // Verify audio quality maintained
            Assert.IsTrue(capturedAudio.Length > 0, "Should have captured audio");

            var hasClipping = AudioAnalyzer.HasClipping(capturedAudio, threshold: 0.99f);
            Assert.IsFalse(hasClipping, 
                "Mixing 10 frequencies should not cause clipping (proper amplitude reduction)");

            var peakAmplitude = AudioAnalyzer.CalculatePeakAmplitude(capturedAudio);
            Assert.IsTrue(peakAmplitude > 0.1f && peakAmplitude <= 1.0f,
                $"Peak amplitude should be reasonable with 10 frequencies, got {peakAmplitude:F4}");

            // Verify mixing performance (should maintain real-time even under load)
            var avgFrameRate = stats.AverageFrameRate;
            Assert.IsTrue(avgFrameRate > 50, 
                $"Should maintain >50 FPS even with 10 frequencies, got {avgFrameRate:F1} FPS");

            // Cleanup
            foreach (var source in testSources) source.Dispose();
            foreach (var worker in workers) await worker.DisposeAsync();

            Console.WriteLine($"? 10 Frequency Test: {stats.FramesMixed} frames in {stopwatch.ElapsedMilliseconds}ms, " +
                            $"FPS={avgFrameRate:F1}, Underruns={stats.Underruns}, Peak={peakAmplitude:F4}");
        }

        /// <summary>
        /// TEST 2: Extreme load - 15 simultaneous frequencies (stress limit)
        /// Verifies graceful handling at system capacity
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("ExtremeLoad")]
        public async Task FifteenSimultaneousFrequencies_GracefulHandling()
        {
            // Arrange - Push system to limits with 15 frequencies
            var frequencies = new List<double>();
            for (int i = 0; i < 15; i++)
            {
                frequencies.Add(220_000_000.0 + (i * 10_000_000.0)); // 220-360 MHz range
            }

            var testSources = new List<TestAudioSource>();
            var workers = new List<UserWorker>();

            // Use very low amplitude to test clipping prevention with many sources
            for (int i = 0; i < frequencies.Count; i++)
            {
                var testSource = new TestAudioSource(frequencies[i], 48000);
                testSource.EnqueueContinuousTone(440.0 + (i * 10), packetCount: 10, amplitude: 0.1f);
                testSources.Add(testSource);

                var worker = new UserWorker($"PILOT-{i:D3}", frequencies[i], testSource);
                workers.Add(worker);

                _mixer!.RegisterUserWorker(frequencies[i], $"PILOT-{i:D3}", worker);
                _mixer.SetFrequencyGate(frequencies[i], FrequencyGateMode.Allow);
            }

            // Act
            await Task.Delay(400);

            var stats = _mixer!.GetStats();
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();

            // Assert - System should handle gracefully, even if performance degrades
            Assert.AreEqual(15, stats.ActiveFrequencies, "Should have 15 active frequencies");
            Assert.IsTrue(stats.FramesMixed > 0, "Should still produce frames even at capacity");

            // Audio quality might degrade but should not crash
            var hasClipping = AudioAnalyzer.HasClipping(capturedAudio, threshold: 0.99f);
            Assert.IsFalse(hasClipping, "Should prevent clipping even with 15 frequencies");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio should remain within valid range");

            // Cleanup
            foreach (var source in testSources) source.Dispose();
            foreach (var worker in workers) await worker.DisposeAsync();

            Console.WriteLine($"? 15 Frequency Stress Test: {stats.FramesMixed} frames, " +
                            $"FPS={stats.AverageFrameRate:F1}, Underruns={stats.Underruns}");
        }

        /// <summary>
        /// TEST 3: Prolonged operation - 30 seconds of continuous mixing
        /// Verifies no memory leaks or performance degradation over time
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("LongDuration")]
        [Timeout(35000)] // 35 second timeout (30s test + 5s margin)
        public async Task ProlongedOperation_NoMemoryLeaks()
        {
            // Arrange - Setup moderate load (5 frequencies) for extended duration
            var frequencies = new double[] { 
                251_000_000.0, 305_000_000.0, 270_000_000.0, 
                243_000_000.0, 282_000_000.0 
            };

            var testSources = new List<TestAudioSource>();
            var workers = new List<UserWorker>();

            for (int i = 0; i < frequencies.Length; i++)
            {
                var testSource = new TestAudioSource(frequencies[i], 48000);
                // Generate enough packets for 30 seconds (48000 samples/sec, 960 samples/packet = 50 packets/sec)
                testSource.EnqueueContinuousTone(440.0 + (i * 50), packetCount: 1500, amplitude: 0.3f);
                testSources.Add(testSource);

                var worker = new UserWorker($"PILOT-{i:D3}", frequencies[i], testSource);
                workers.Add(worker);

                _mixer!.RegisterUserWorker(frequencies[i], $"PILOT-{i:D3}", worker);
                _mixer.SetFrequencyGate(frequencies[i], FrequencyGateMode.Allow);
            }

            // Get initial memory usage
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);
            var initialStats = _mixer!.GetStats();

            var stopwatch = Stopwatch.StartNew();

            // Act - Run for 30 seconds
            await Task.Delay(30000);

            stopwatch.Stop();

            // Get final stats
            var finalStats = _mixer.GetStats();
            var finalMemory = GC.GetTotalMemory(false);

            // Force GC to see if memory is released
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryAfterGC = GC.GetTotalMemory(false);

            // Assert - Verify stability over time
            Assert.IsTrue(finalStats.FramesMixed > initialStats.FramesMixed + 1000,
                "Should have mixed many frames over 30 seconds");

            // Check for consistent frame rate (no degradation)
            var avgFrameRate = finalStats.AverageFrameRate;
            Assert.IsTrue(avgFrameRate > 50,
                $"Frame rate should remain consistent, got {avgFrameRate:F1} FPS");

            // Check for reasonable underrun rate throughout
            var underrunRate = finalStats.Underruns / (double)Math.Max(1, finalStats.FramesMixed);
            Assert.IsTrue(underrunRate < 0.02,
                $"Underrun rate should remain low over time, got {underrunRate:P2}");

            // Memory should not grow excessively (< 50MB growth allowed)
            var memoryGrowth = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            var memoryAfterGCGrowth = (memoryAfterGC - initialMemory) / (1024.0 * 1024.0);
            
            Assert.IsTrue(memoryAfterGCGrowth < 50,
                $"Memory growth after GC should be < 50MB, got {memoryAfterGCGrowth:F2} MB");

            // Cleanup
            foreach (var source in testSources) source.Dispose();
            foreach (var worker in workers) await worker.DisposeAsync();

            Console.WriteLine($"? Prolonged Operation Test (30s):");
            Console.WriteLine($"   Frames Mixed: {finalStats.FramesMixed:N0}");
            Console.WriteLine($"   Avg FPS: {avgFrameRate:F1}");
            Console.WriteLine($"   Underruns: {finalStats.Underruns} ({underrunRate:P2})");
            Console.WriteLine($"   Memory Growth: {memoryGrowth:F2} MB (After GC: {memoryAfterGCGrowth:F2} MB)");
            Console.WriteLine($"   Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
        }

        /// <summary>
        /// TEST 4: Dynamic load - Frequencies added/removed during operation
        /// Verifies stability during dynamic topology changes
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("DynamicLoad")]
        public async Task DynamicFrequencyChanges_MaintainsStability()
        {
            // Arrange
            var frequencyPool = new List<double>
            {
                251_000_000.0, 305_000_000.0, 270_000_000.0, 243_000_000.0, 
                282_000_000.0, 225_000_000.0, 340_000_000.0, 360_000_000.0
            };

            var activeWorkers = new Dictionary<double, (TestAudioSource Source, UserWorker Worker)>();

            // Act - Dynamically add and remove frequencies
            for (int cycle = 0; cycle < 10; cycle++)
            {
                // Add 2-4 random frequencies
                var toAdd = new Random().Next(2, 5);
                for (int i = 0; i < toAdd && activeWorkers.Count < 8; i++)
                {
                    var freq = frequencyPool[new Random().Next(frequencyPool.Count)];
                    if (!activeWorkers.ContainsKey(freq))
                    {
                        var source = new TestAudioSource(freq, 48000);
                        source.EnqueueContinuousTone(440.0, packetCount: 10, amplitude: 0.3f);
                        
                        var worker = new UserWorker($"PILOT-{freq}", freq, source);
                        activeWorkers[freq] = (source, worker);

                        _mixer!.RegisterUserWorker(freq, $"PILOT-{freq}", worker);
                        _mixer.SetFrequencyGate(freq, FrequencyGateMode.Allow);
                    }
                }

                await Task.Delay(100);

                // Remove 1-2 random frequencies
                var toRemove = Math.Min(new Random().Next(1, 3), activeWorkers.Count);
                var keysToRemove = activeWorkers.Keys.Take(toRemove).ToList();
                foreach (var freq in keysToRemove)
                {
                    var (source, worker) = activeWorkers[freq];
                    _mixer!.SetFrequencyGate(freq, FrequencyGateMode.Block);
                    await worker.DisposeAsync();
                    source.Dispose();
                    activeWorkers.Remove(freq);
                }

                await Task.Delay(100);
            }

            // Final stabilization
            await Task.Delay(200);

            // Get stats
            var stats = _mixer!.GetStats();
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();

            // Assert
            Assert.IsTrue(stats.FramesMixed > 0, "Should have mixed frames during dynamic changes");
            
            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.7f);
            Assert.IsFalse(hasClicks, "Should not produce major clicks during dynamic changes");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio should remain valid during dynamic topology");

            // Cleanup remaining workers
            foreach (var (source, worker) in activeWorkers.Values)
            {
                await worker.DisposeAsync();
                source.Dispose();
            }

            Console.WriteLine($"? Dynamic Load Test: {stats.FramesMixed} frames, " +
                            $"ActiveFreqs={activeWorkers.Count}, FPS={stats.AverageFrameRate:F1}");
        }

        /// <summary>
        /// TEST 5: Rapid pilot changes - Simulate busy radio environment
        /// Verifies per-pilot filtering under high churn
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("HighChurn")]
        public async Task RapidPilotMuteUnmute_MaintainsQuality()
        {
            // Arrange - Create 5 pilots on same frequency
            var frequency = 251_000_000.0;
            var pilots = new List<string> { "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO" };
            var workers = new Dictionary<string, (TestAudioSource, UserWorker)>();

            foreach (var pilot in pilots)
            {
                var source = new TestAudioSource(frequency, 48000);
                source.EnqueueContinuousTone(440.0, packetCount: 20, amplitude: 0.3f);
                
                var worker = new UserWorker(pilot, frequency, source);
                workers[pilot] = (source, worker);

                _mixer!.RegisterUserWorker(frequency, pilot, worker);
            }

            _mixer!.SetFrequencyGate(frequency, FrequencyGateMode.Allow);

            // Act - Rapidly mute/unmute pilots (simulate radio chatter)
            var random = new Random();
            for (int i = 0; i < 50; i++)
            {
                var pilot = pilots[random.Next(pilots.Count)];
                var mode = random.Next(3) switch
                {
                    0 => PilotGateMode.Allow,
                    1 => PilotGateMode.Mute,
                    _ => PilotGateMode.Solo
                };

                _mixer.SetPilotGate(pilot, frequency, mode);
                await Task.Delay(20);
            }

            await Task.Delay(200);

            // Get results
            var stats = _mixer.GetStats();
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();

            // Assert
            Assert.IsTrue(stats.FramesMixed > 0, "Should have mixed frames during rapid pilot changes");

            var hasClicks = AudioAnalyzer.HasClicksOrPops(capturedAudio, threshold: 0.7f);
            Assert.IsFalse(hasClicks, "Should not produce major clicks during rapid pilot changes");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio should remain valid during rapid gating");

            // Cleanup
            foreach (var (source, worker) in workers.Values)
            {
                await worker.DisposeAsync();
                source.Dispose();
            }

            Console.WriteLine($"? Rapid Pilot Changes Test: {stats.FramesMixed} frames, " +
                            $"FPS={stats.AverageFrameRate:F1}");
        }

        /// <summary>
        /// TEST 6: Combined stress - Multiple frequencies + rapid changes + prolonged operation
        /// Ultimate stress test combining all load factors
        /// </summary>
        [TestMethod]
        [TestCategory("StressTest")]
        [TestCategory("UltimateStress")]
        [Timeout(12000)] // 12 second timeout
        public async Task CombinedStressTest_SystemStability()
        {
            // Arrange - 8 frequencies, each with 2 pilots
            var frequencies = new double[] { 
                251_000_000.0, 305_000_000.0, 270_000_000.0, 243_000_000.0,
                282_000_000.0, 225_000_000.0, 340_000_000.0, 360_000_000.0
            };

            var allWorkers = new List<(TestAudioSource, UserWorker, double, string)>();

            for (int f = 0; f < frequencies.Length; f++)
            {
                for (int p = 0; p < 2; p++)
                {
                    var freq = frequencies[f];
                    var pilot = $"F{f}-P{p}";
                    
                    var source = new TestAudioSource(freq, 48000);
                    source.EnqueueContinuousTone(440.0 + (f * 10) + (p * 5), packetCount: 50, amplitude: 0.15f);
                    
                    var worker = new UserWorker(pilot, freq, source);
                    allWorkers.Add((source, worker, freq, pilot));

                    _mixer!.RegisterUserWorker(freq, pilot, worker);
                    _mixer.SetFrequencyGate(freq, FrequencyGateMode.Allow);
                }
            }

            var random = new Random();
            var stopwatch = Stopwatch.StartNew();

            // Act - Run for 10 seconds with random gate changes
            for (int i = 0; i < 100; i++) // 100 changes over 10 seconds
            {
                // Random frequency gate change
                var freq = frequencies[random.Next(frequencies.Length)];
                var freqMode = (FrequencyGateMode)random.Next(3);
                _mixer!.SetFrequencyGate(freq, freqMode);

                // Random pilot gate change
                var (_, _, pilotFreq, pilot) = allWorkers[random.Next(allWorkers.Count)];
                var pilotMode = (PilotGateMode)random.Next(3);
                _mixer.SetPilotGate(pilot, pilotFreq, pilotMode);

                await Task.Delay(100);
            }

            stopwatch.Stop();

            // Get final stats
            var stats = _mixer!.GetStats();
            var capturedAudio = _audioCapture!.GetCapturedAudioAsFloat();

            // Assert - System should survive combined stress
            Assert.IsTrue(stats.FramesMixed > 500,
                "Should have mixed many frames during combined stress");

            Assert.IsTrue(stats.AverageFrameRate > 40,
                $"Should maintain reasonable frame rate, got {stats.AverageFrameRate:F1} FPS");

            var underrunRate = stats.Underruns / (double)Math.Max(1, stats.FramesMixed);
            Assert.IsTrue(underrunRate < 0.1,
                $"Underrun rate should be acceptable under stress, got {underrunRate:P1}");

            var isValidRange = AudioAnalyzer.IsWithinValidRange(capturedAudio);
            Assert.IsTrue(isValidRange, "Audio should remain valid under combined stress");

            // Cleanup
            foreach (var (source, worker, _, _) in allWorkers)
            {
                await worker.DisposeAsync();
                source.Dispose();
            }

            Console.WriteLine($"? Combined Stress Test ({stopwatch.Elapsed.TotalSeconds:F1}s):");
            Console.WriteLine($"   Frequencies: 8 x 2 pilots = 16 workers");
            Console.WriteLine($"   Frames: {stats.FramesMixed:N0}");
            Console.WriteLine($"   FPS: {stats.AverageFrameRate:F1}");
            Console.WriteLine($"   Underruns: {stats.Underruns} ({underrunRate:P2})");
            Console.WriteLine($"   Gate Changes: 100 frequency + 100 pilot = 200 total");
        }
    }
}
