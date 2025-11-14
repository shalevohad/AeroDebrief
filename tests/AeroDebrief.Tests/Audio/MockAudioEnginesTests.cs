using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Tests.Audio;
using AeroDebrief.Tests.TestHelpers;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Helpers;
using System;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Tests demonstrating the usage of MockAudioProcessingEngine and MockAudioOutputEngine
    /// </summary>
    [TestClass]
    public class MockAudioEnginesTests
    {
        [TestMethod]
        public void MockAudioProcessingEngine_Initialize_SetsIsInitialized()
        {
            // Arrange
            var engine = new MockAudioProcessingEngine();

            // Act
            engine.Initialize();

            // Assert
            Assert.IsTrue(engine.IsInitialized, "Engine should be initialized");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_DecodePacketToFloat_ReturnsAudioData()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var packet = CreateTestPacket();

            // Act
            var audioData = engine.DecodePacketToFloat(packet);

            // Assert
            Assert.IsNotNull(audioData, "Should return audio data");
            Assert.IsTrue(audioData.Length > 0, "Should have audio samples");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_ProcessPacket_IncrementsPacketCount()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var packet = CreateTestPacket();

            var initialCount = engine.ProcessedPacketCount;

            // Act
            engine.ProcessPacket(packet);
            engine.ProcessPacket(packet);
            engine.ProcessPacket(packet);

            // Assert
            Assert.AreEqual(initialCount + 3, engine.ProcessedPacketCount, "Should have processed 3 packets");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_SetMasterVolume_AffectsOutput()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var packet = CreateTestPacket();

            // Process with normal volume
            engine.SetMasterVolume(1.0f);
            var normalAudio = engine.ProcessPacket(packet);
            var normalMax = GetMaxAmplitude(normalAudio);

            // Process with reduced volume
            engine.ResetDecoders();
            engine.SetMasterVolume(0.5f);
            var quietAudio = engine.ProcessPacket(packet);
            var quietMax = GetMaxAmplitude(quietAudio);

            // Assert
            Assert.IsTrue(quietMax < normalMax, "Reduced volume should result in quieter audio");
            Assert.IsTrue(quietMax > 0, "Audio should not be silent");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_SetTransmitterVolume_AffectsSpecificTransmitter()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var packet = CreateTestPacket();

            // Act
            engine.SetTransmitterVolume(packet.TransmitterGuid, 0.3f);

            // Assert
            var volume = engine.GetTransmitterVolume(packet.TransmitterGuid);
            Assert.AreEqual(0.3f, volume, 0.01f, "Transmitter volume should be set");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_ResetDecoders_ClearsProcessedCount()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var packet = CreateTestPacket();

            engine.ProcessPacket(packet);
            engine.ProcessPacket(packet);

            // Act
            engine.ResetDecoders();

            // Assert
            Assert.AreEqual(0, engine.ProcessedPacketCount, "Processed count should be reset");
        }

        [TestMethod]
        public void MockAudioProcessingEngine_Dispose_SetsIsDisposed()
        {
            // Arrange
            var engine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();

            // Act
            engine.Dispose();

            // Assert
            Assert.IsTrue(engine.IsDisposed, "Engine should be disposed");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_Initialize_SetsIsInitialized()
        {
            // Arrange
            var engine = new MockAudioOutputEngine();

            // Act
            await engine.InitializeAsync();

            // Assert
            Assert.IsTrue(engine.IsInitialized, "Engine should be initialized");
            Assert.AreEqual(1, engine.InitializeCallCount, "Initialize should be called once");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_WriteAudio_RecordsFrames()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
            var audioData = CreateTestAudioData(1920); // 20ms at 48kHz

            // Act
            await engine.WriteAudioAsync(audioData);
            await engine.WriteAudioAsync(audioData);

            // Assert
            Assert.AreEqual(2, engine.WrittenFrames.Count, "Should have written 2 frames");
            Assert.AreEqual(1920 * 2 * 2, engine.TotalBytesWritten, "Should have written correct total bytes");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_Start_SetsIsRunning()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();

            // Act
            engine.Start();

            // Assert
            Assert.IsTrue(engine.IsRunning, "Engine should be running");
            Assert.AreEqual(1, engine.StartCallCount, "Start should be called once");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_Stop_ClearsIsRunning()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
            engine.Start();

            // Act
            engine.Stop();

            // Assert
            Assert.IsFalse(engine.IsRunning, "Engine should not be running");
            Assert.AreEqual(1, engine.StopCallCount, "Stop should be called once");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_SetMasterVolume_UpdatesCurrentVolume()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();

            // Act
            engine.SetMasterVolume(0.7f);

            // Assert
            Assert.AreEqual(0.7f, engine.CurrentVolume, 0.01f, "Volume should be set");
            Assert.AreEqual(0.7f, engine.GetMasterVolume(), 0.01f, "GetMasterVolume should return set value");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_SetMasterVolume_ClampsToValidRange()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();

            // Act & Assert - Test upper bound
            engine.SetMasterVolume(5.0f);
            Assert.AreEqual(2.0f, engine.CurrentVolume, 0.01f, "Volume should be clamped to 2.0");

            // Act & Assert - Test lower bound
            engine.SetMasterVolume(-1.0f);
            Assert.AreEqual(0.0f, engine.CurrentVolume, 0.01f, "Volume should be clamped to 0.0");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_ClearBuffer_ClearsWrittenFrames()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
            var audioData = CreateTestAudioData(1920);
            
            await engine.WriteAudioAsync(audioData);
            await engine.WriteAudioAsync(audioData);

            // Act
            engine.ClearBuffer();

            // Assert
            Assert.AreEqual(0, engine.WrittenFrames.Count, "Written frames should be cleared");
            Assert.AreEqual(0, engine.TotalBytesWritten, "Total bytes should be reset");
            Assert.AreEqual(1, engine.ClearBufferCallCount, "ClearBuffer should be called once");
        }

        [TestMethod]
        public async Task MockAudioOutputEngine_Dispose_SetsIsDisposed()
        {
            // Arrange
            var engine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();

            // Act
            engine.Dispose();

            // Assert
            Assert.IsTrue(engine.IsDisposed, "Engine should be disposed");
            Assert.AreEqual(0, engine.WrittenFrames.Count, "Written frames should be cleared on dispose");
        }

        [TestMethod]
        public async Task FullPipeline_WithMockEngines_Works()
        {
            // Arrange
            var processingEngine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
            var outputEngine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
            var packet = CreateTestPacket();

            // Act - Process audio packet
            var processedAudio = processingEngine.ProcessPacket(packet);
            
            // Convert float to byte array for output
            var audioBytes = AudioConverter.FloatToPcm16(processedAudio);
            
            // Write to output
            outputEngine.Start();
            await outputEngine.WriteAudioAsync(audioBytes);

            // Assert
            Assert.AreEqual(1, processingEngine.ProcessedPacketCount, "Should have processed 1 packet");
            Assert.AreEqual(1, outputEngine.WrittenFrames.Count, "Should have written 1 frame");
            Assert.IsTrue(outputEngine.IsRunning, "Output should be running");
            Assert.IsTrue(outputEngine.TotalBytesWritten > 0, "Should have written bytes");

            // Cleanup
            outputEngine.Stop();
            processingEngine.Dispose();
            outputEngine.Dispose();
        }

        #region Helper Methods

        /// <summary>
        /// Creates a test audio packet with valid data
        /// </summary>
        private AudioPacketMetadata CreateTestPacket()
        {
            // Create a simple test audio payload (sine wave)
            var sampleCount = 1920; // 20ms at 48kHz
            var audioPayload = new byte[sampleCount * 2]; // 16-bit PCM

            for (int i = 0; i < sampleCount; i++)
            {
                // Generate 440Hz sine wave
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 48000.0) * 8000);
                var bytes = BitConverter.GetBytes(sample);
                audioPayload[i * 2] = bytes[0];
                audioPayload[i * 2 + 1] = bytes[1];
            }

            var playerInfo = new PlayerInfo
            {
                Name = "TestPilot",
                TransmitterGuid = "TEST_GUID_001",
                Coalition = 2,
                Seat = 0,
                AllowRecord = true,
                Position = new Position { Latitude = 45.0, Longitude = -122.0, Altitude = 10000 },
                AircraftInfo = new AircraftInfo { UnitType = "F-16C_50", UnitId = 1 }
            };

            return new AudioPacketMetadata(
                DateTime.UtcNow,
                251_000_000.0,
                0, // AM
                0, // No encryption
                1,
                1,
                "TEST_GUID_001",
                playerInfo,
                Constants.OUTPUT_SAMPLE_RATE,
                1,
                2,
                audioPayload
            );
        }

        /// <summary>
        /// Creates test audio data with a simple tone
        /// </summary>
        private byte[] CreateTestAudioData(int sampleCount)
        {
            var audioData = new byte[sampleCount * 2]; // 16-bit PCM

            for (int i = 0; i < sampleCount; i++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 48000.0) * 8000);
                var bytes = BitConverter.GetBytes(sample);
                audioData[i * 2] = bytes[0];
                audioData[i * 2 + 1] = bytes[1];
            }

            return audioData;
        }

        /// <summary>
        /// Gets the maximum amplitude from float audio data
        /// </summary>
        private float GetMaxAmplitude(float[] audioData)
        {
            float max = 0;
            foreach (var sample in audioData)
            {
                max = Math.Max(max, Math.Abs(sample));
            }
            return max;
        }

        #endregion
    }
}
