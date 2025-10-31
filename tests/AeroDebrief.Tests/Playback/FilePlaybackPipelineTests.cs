using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.Playback;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Audio;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace AeroDebrief.Tests.Playback
{
    /// <summary>
    /// Tests for FilePlaybackPipeline - complete playback pipeline with filtering
    /// </summary>
    [TestClass]
    public class FilePlaybackPipelineTests
    {
        private string? _testFilePath;

        [TestInitialize]
        public void Setup()
        {
            _testFilePath = GetTestRecordingFile();
        }

        [TestMethod]
        public void Constructor_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new FilePlaybackPipeline(null!));
        }

        [TestMethod]
        public async Task OpenAsync_WithUnopenedSource_ThrowsInvalidOperationException()
        {
            // Arrange
            var source = new FilePacketSource("test.srs");
            var pipeline = new FilePlaybackPipeline(source);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => pipeline.OpenAsync());
        }

        [TestMethod]
        public async Task OpenAsync_WithValidSource_InitializesPipeline()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);

            // Act
            await pipeline.OpenAsync();

            // Assert
            Assert.IsFalse(pipeline.IsPlaying);
            Assert.IsFalse(pipeline.IsPaused);
            Assert.AreEqual(TimeSpan.Zero, pipeline.CurrentPosition);
            Assert.IsTrue(pipeline.TotalDuration > TimeSpan.Zero);

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task GetAvailableFrequencies_ReturnsFrequencies()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();

            // Act
            var frequencies = pipeline.GetAvailableFrequencies();

            // Assert
            Assert.IsNotNull(frequencies);
            Assert.IsTrue(frequencies.Count > 0, "Should have frequencies");

            foreach (var freq in frequencies)
            {
                Assert.IsTrue(freq.Frequency > 0, "Frequency should be positive");
                Assert.IsFalse(string.IsNullOrEmpty(freq.DisplayName), "Should have display name");
            }

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task SetFrequencyGate_Performance_IsInstant()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();

            var frequencies = pipeline.GetAvailableFrequencies();
            if (frequencies.Count == 0)
            {
                Assert.Inconclusive("No frequencies available for testing");
                pipeline.Dispose();
                source.Dispose();
                return;
            }

            var testFreq = frequencies.First().Frequency;
            var stopwatch = Stopwatch.StartNew();

            // Act
            pipeline.SetFrequencyGate(testFreq, FrequencyGateMode.Solo);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2,
                $"Frequency gate change took {stopwatch.ElapsedMilliseconds}ms, expected < 2ms");

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task SetPilotGate_Performance_IsInstant()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            pipeline.EnablePerPilotMixing = true;
            await pipeline.OpenAsync();

            var frequencies = pipeline.GetAvailableFrequencies();
            if (frequencies.Count == 0)
            {
                Assert.Inconclusive("No frequencies available for testing");
                pipeline.Dispose();
                source.Dispose();
                return;
            }

            var testFreq = frequencies.First().Frequency;
            var testPilot = "TEST-PILOT-123";
            var stopwatch = Stopwatch.StartNew();

            // Act
            pipeline.SetPilotGate(testPilot, testFreq, PilotGateMode.Mute);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2,
                $"Pilot gate change took {stopwatch.ElapsedMilliseconds}ms, expected < 2ms");

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task PlayAsync_StartPlayback_SetsIsPlaying()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();

            // Act
            await pipeline.PlayAsync();
            await Task.Delay(100); // Give it a moment to start

            // Assert
            Assert.IsTrue(pipeline.IsPlaying, "Should be playing");
            Assert.IsFalse(pipeline.IsPaused, "Should not be paused");

            // Cleanup
            await pipeline.StopAsync();
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task Pause_DuringPlayback_SetsPausedState()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();
            await pipeline.PlayAsync();
            await Task.Delay(100);

            // Act
            pipeline.Pause();

            // Assert
            Assert.IsTrue(pipeline.IsPlaying, "IsPlaying should still be true");
            Assert.IsTrue(pipeline.IsPaused, "Should be paused");

            // Cleanup
            await pipeline.StopAsync();
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task Resume_AfterPause_ClearsPassedState()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();
            await pipeline.PlayAsync();
            await Task.Delay(100);
            pipeline.Pause();

            // Act
            pipeline.Resume();

            // Assert
            Assert.IsTrue(pipeline.IsPlaying, "Should be playing");
            Assert.IsFalse(pipeline.IsPaused, "Should not be paused");

            // Cleanup
            await pipeline.StopAsync();
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task StopAsync_DuringPlayback_StopsPlayback()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();
            await pipeline.PlayAsync();
            await Task.Delay(100);

            // Act
            await pipeline.StopAsync();

            // Assert
            Assert.IsFalse(pipeline.IsPlaying, "Should not be playing");
            Assert.IsFalse(pipeline.IsPaused, "Should not be paused");
            Assert.AreEqual(TimeSpan.Zero, pipeline.CurrentPosition, "Position should reset");

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task SeekAsync_ToValidPosition_UpdatesPosition()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            await pipeline.OpenAsync();

            var seekPosition = TimeSpan.FromSeconds(5);
            if (pipeline.TotalDuration < seekPosition)
            {
                seekPosition = pipeline.TotalDuration / 2;
            }

            // Act
            await pipeline.SeekAsync(seekPosition);

            // Assert
            // Position might not be exact due to seek granularity
            Assert.IsTrue(Math.Abs((pipeline.CurrentPosition - seekPosition).TotalSeconds) < 1,
                "Position should be close to seek target");

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task FullPipeline_PlaybackWithFiltering_Works()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);
            pipeline.EnablePerPilotMixing = true;
            await pipeline.OpenAsync();

            var frequencies = pipeline.GetAvailableFrequencies();
            if (frequencies.Count == 0)
            {
                Assert.Inconclusive("No frequencies available for testing");
                pipeline.Dispose();
                source.Dispose();
                return;
            }

            // Act - Start playback
            await pipeline.PlayAsync();
            await Task.Delay(500);

            // Apply frequency filter
            var testFreq = frequencies.First().Frequency;
            pipeline.SetFrequencyGate(testFreq, FrequencyGateMode.Solo);
            await Task.Delay(200);

            // Apply pilot filter
            pipeline.SetPilotGate("TEST-PILOT", testFreq, PilotGateMode.Mute);
            await Task.Delay(200);

            // Verify state
            Assert.IsTrue(pipeline.IsPlaying, "Should still be playing");
            Assert.IsTrue(pipeline.CurrentPosition > TimeSpan.Zero, "Position should advance");

            // Stop
            await pipeline.StopAsync();

            // Assert final state
            Assert.IsFalse(pipeline.IsPlaying);

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        [TestMethod]
        public async Task Properties_AfterOpen_HaveCorrectValues()
        {
            if (_testFilePath == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(_testFilePath);
            await source.OpenAsync();
            var pipeline = new FilePlaybackPipeline(source);

            // Act
            await pipeline.OpenAsync();

            // Assert
            Assert.AreEqual(source.RecordingStart, pipeline.RecordingStart);
            Assert.AreEqual(source.TotalDuration, pipeline.TotalDuration);
            Assert.IsFalse(pipeline.IsPlaying);
            Assert.IsFalse(pipeline.IsPaused);

            // Cleanup
            pipeline.Dispose();
            source.Dispose();
        }

        /// <summary>
        /// Helper to find a test recording file
        /// </summary>
        private string? GetTestRecordingFile()
        {
            var testPaths = new[]
            {
                @"..\..\..\..\TestData\sample.srs",
                @"TestData\sample.srs",
                @"C:\Temp\test.srs"
            };

            foreach (var path in testPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
