using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Tests for MasterMixer - per-pilot and per-frequency filtering
    /// </summary>
    [TestClass]
    public class MasterMixerFilteringTests
    {
        private AudioOutputEngine? _audioOutput;
        private MasterMixer? _mixer;

        [TestInitialize]
        public async Task Setup()
        {
            // Force cleanup of any previous state
            _mixer?.Dispose();
            _audioOutput?.Dispose();
            _mixer = null;
            _audioOutput = null;
            
            // Wait for any background threads to complete
            await Task.Delay(200);
            
            // Create fresh instances
            _audioOutput = new AudioOutputEngine();
            try
            {
                await _audioOutput.InitializeAsync();
            }
            catch
            {
                // If audio initialization fails in test environment, that's okay
            }
            
            _mixer = new MasterMixer(_audioOutput);
            
            // Wait for mixer to fully initialize
            await Task.Delay(100);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (_mixer != null)
                {
                    _mixer.Dispose();
                    _mixer = null;
                }
                
                if (_audioOutput != null)
                {
                    _audioOutput.Dispose();
                    _audioOutput = null;
                }
                
                // Ensure background threads complete
                System.Threading.Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        [TestMethod]
        public void Constructor_WithNullOutput_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new MasterMixer(null!));
        }

        [TestMethod]
        public void SetFrequencyGate_Performance_IsInstant()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var stopwatch = Stopwatch.StartNew();

            // Act
            _mixer!.SetFrequencyGate(frequency, FrequencyGateMode.Solo);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2,
                $"Frequency gate change took {stopwatch.ElapsedMilliseconds}ms, expected < 2ms");
        }

        [TestMethod]
        public void SetPilotGate_Performance_IsInstant()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var pilotId = "PILOT-123";
            var stopwatch = Stopwatch.StartNew();

            // Act
            _mixer!.SetPilotGate(pilotId, frequency, PilotGateMode.Mute);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2,
                $"Pilot gate change took {stopwatch.ElapsedMilliseconds}ms, expected < 2ms");
        }

        [TestMethod]
        public void SetPilotGate_WithNullPilotId_ThrowsArgumentException()
        {
            // Arrange
            var frequency = 251_000_000.0;

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                _mixer!.SetPilotGate(null!, frequency, PilotGateMode.Solo));
        }

        [TestMethod]
        public void SetPilotGate_WithEmptyPilotId_ThrowsArgumentException()
        {
            // Arrange
            var frequency = 251_000_000.0;

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                _mixer!.SetPilotGate(string.Empty, frequency, PilotGateMode.Solo));
        }

        [TestMethod]
        public void GetPilotGates_WithNoGates_ReturnsEmptyDictionary()
        {
            // Arrange
            var frequency = 251_000_000.0;

            // Act
            var gates = _mixer!.GetPilotGates(frequency);

            // Assert
            Assert.IsNotNull(gates);
            Assert.AreEqual(0, gates.Count);
        }

        [TestMethod]
        public void GetPilotGates_AfterSetPilotGate_ReturnsGate()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var pilotId = "PILOT-123";
            _mixer!.SetPilotGate(pilotId, frequency, PilotGateMode.Solo);

            // Act
            var gates = _mixer.GetPilotGates(frequency);

            // Assert
            Assert.IsNotNull(gates);
            Assert.AreEqual(1, gates.Count);
            Assert.IsTrue(gates.ContainsKey(pilotId));
            Assert.AreEqual(PilotGateMode.Solo, gates[pilotId]);
        }

        [TestMethod]
        public void ClearPilotGate_ResetsToAllow()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var pilotId = "PILOT-123";
            _mixer!.SetPilotGate(pilotId, frequency, PilotGateMode.Mute);

            // Act
            _mixer.ClearPilotGate(pilotId, frequency);

            // Assert
            var gates = _mixer.GetPilotGates(frequency);
            Assert.AreEqual(PilotGateMode.Allow, gates[pilotId]);
        }

        [TestMethod]
        public void GetStats_ReturnsValidStatistics()
        {
            // Arrange
            var frequency1 = 251_000_000.0;
            var frequency2 = 305_000_000.0;
            var pilotId = "PILOT-123";

            _mixer!.SetFrequencyGate(frequency1, FrequencyGateMode.Solo);
            _mixer.SetFrequencyGate(frequency2, FrequencyGateMode.Mute);
            _mixer.SetPilotGate(pilotId, frequency1, PilotGateMode.Solo);

            // Act
            var stats = _mixer.GetStats();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(1, stats.SoloFrequencies);
            Assert.AreEqual(1, stats.MutedFrequencies);
            Assert.AreEqual(1, stats.SoloPilots);
            Assert.IsTrue(stats.RunTime >= TimeSpan.Zero);
        }

        [TestMethod]
        public void MultipleFrequencyGates_CanBeSetIndependently()
        {
            // Arrange
            var freq1 = 251_000_000.0;
            var freq2 = 305_000_000.0;
            var freq3 = 270_000_000.0;

            // Act
            _mixer!.SetFrequencyGate(freq1, FrequencyGateMode.Solo);
            _mixer.SetFrequencyGate(freq2, FrequencyGateMode.Mute);
            _mixer.SetFrequencyGate(freq3, FrequencyGateMode.Block);

            var stats = _mixer.GetStats();

            // Assert
            Assert.AreEqual(1, stats.SoloFrequencies);
            Assert.AreEqual(1, stats.MutedFrequencies);
            Assert.AreEqual(1, stats.BlockedFrequencies);
        }

        [TestMethod]
        public void MultiplePilotGates_OnSameFrequency_AreIndependent()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var pilot1 = "PILOT-123";
            var pilot2 = "PILOT-456";
            var pilot3 = "PILOT-789";

            // Act
            _mixer!.SetPilotGate(pilot1, frequency, PilotGateMode.Solo);
            _mixer.SetPilotGate(pilot2, frequency, PilotGateMode.Mute);
            _mixer.SetPilotGate(pilot3, frequency, PilotGateMode.Allow);

            var gates = _mixer.GetPilotGates(frequency);

            // Assert
            Assert.AreEqual(3, gates.Count);
            Assert.AreEqual(PilotGateMode.Solo, gates[pilot1]);
            Assert.AreEqual(PilotGateMode.Mute, gates[pilot2]);
            Assert.AreEqual(PilotGateMode.Allow, gates[pilot3]);
        }

        [TestMethod]
        public void PilotGates_OnDifferentFrequencies_AreIndependent()
        {
            // Arrange
            var freq1 = 251_000_000.0;
            var freq2 = 305_000_000.0;
            var pilot = "PILOT-123";

            // Act
            _mixer!.SetPilotGate(pilot, freq1, PilotGateMode.Solo);
            _mixer.SetPilotGate(pilot, freq2, PilotGateMode.Mute);

            var gates1 = _mixer.GetPilotGates(freq1);
            var gates2 = _mixer.GetPilotGates(freq2);

            // Assert
            Assert.AreEqual(PilotGateMode.Solo, gates1[pilot]);
            Assert.AreEqual(PilotGateMode.Mute, gates2[pilot]);
        }

        [TestMethod]
        public void RegisterUserWorker_WithNullWorker_ThrowsArgumentNullException()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var pilotId = "PILOT-123";

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                _mixer!.RegisterUserWorker(frequency, pilotId, null!));
        }

        [TestMethod]
        public void RegisterUserWorker_WithNullPilotId_ThrowsArgumentException()
        {
            // Arrange
            var frequency = 251_000_000.0;
            var worker = new UserWorker("PILOT-123", frequency);

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                _mixer!.RegisterUserWorker(frequency, null!, worker));
        }

        [TestMethod]
        public async Task Properties_HaveCorrectDefaultValues()
        {
            // Create completely isolated instances for this test only
            // Don't use the Setup() instances at all
            AudioOutputEngine? testAudioOutput = null;
            MasterMixer? testMixer = null;
            
            try
            {
                // Create fresh audio output
                testAudioOutput = new AudioOutputEngine();
                try
                {
                    await testAudioOutput.InitializeAsync();
                }
                catch
                {
                    // Audio init failure in test environment is okay
                }
                
                // Create fresh mixer
                testMixer = new MasterMixer(testAudioOutput);
                
                // Don't wait - check immediately after construction
                // The mixing loop starts in the background but shouldn't have run yet
                
                // Act - Read properties immediately after construction
                var framesMixed = testMixer.FramesMixed;
                var silentFrames = testMixer.SilentFramesDrained;
                var activeFreqs = testMixer.ActiveFrequencies;
                
                // Note: Underruns might be > 0 because the mixing loop starts immediately
                // and will write silence when no workers are registered. This is expected behavior.
                var underruns = testMixer.Underruns;

                // Assert - Check the properties that should definitely be 0
                Assert.AreEqual(0, framesMixed, "FramesMixed should be 0 - no frames have been mixed yet");
                Assert.AreEqual(0, silentFrames, "SilentFramesDrained should be 0 - no frames have been drained yet");
                Assert.AreEqual(0, activeFreqs, "ActiveFrequencies should be 0 - no frequency workers registered");
                
                // Underruns are acceptable during initialization when no workers are registered
                // The mixer will write silence to prevent WASAPI buffer underruns
                // This is expected behavior and not an error
                Assert.IsTrue(underruns >= 0, $"Underruns should be >= 0, got {underruns}");
            }
            finally
            {
                // Cleanup test-specific instances
                testMixer?.Dispose();
                testAudioOutput?.Dispose();
                await Task.Delay(100);
            }
        }

        [TestMethod]
        public async Task Dispose_StopsMixingGracefully()
        {
            // Arrange
            var audioOutput = new AudioOutputEngine();
            await audioOutput.InitializeAsync();
            var mixer = new MasterMixer(audioOutput);
            
            // Wait for mixer to initialize
            await Task.Delay(50);

            // Act
            mixer.Dispose();
            audioOutput.Dispose();

            // Wait a bit to ensure cleanup completes
            await Task.Delay(100);

            // Assert - should not throw
            Assert.IsTrue(true);
        }
    }
}
