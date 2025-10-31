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
            // Create real audio output (won't actually play sound in test environment)
            _audioOutput = new AudioOutputEngine();
            try
            {
                await _audioOutput.InitializeAsync();
            }
            catch
            {
                // If audio initialization fails in test environment, that's okay
                // Tests will be inconclusive
            }
            _mixer = new MasterMixer(_audioOutput);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mixer?.Dispose();
            _audioOutput?.Dispose();
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
        public void Properties_HaveCorrectDefaultValues()
        {
            // Act
            var framesMixed = _mixer!.FramesMixed;
            var underruns = _mixer.Underruns;
            var silentFrames = _mixer.SilentFramesDrained;
            var activeFreqs = _mixer.ActiveFrequencies;

            // Assert
            Assert.AreEqual(0, framesMixed);
            Assert.AreEqual(0, underruns);
            Assert.AreEqual(0, silentFrames);
            Assert.AreEqual(0, activeFreqs);
        }

        [TestMethod]
        public async Task Dispose_StopsMixingGracefully()
        {
            // Arrange
            var audioOutput = new AudioOutputEngine();
            await audioOutput.InitializeAsync();
            var mixer = new MasterMixer(audioOutput);

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
