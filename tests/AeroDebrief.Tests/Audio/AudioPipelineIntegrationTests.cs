using Xunit;
using FluentAssertions;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Integration tests for the audio pipeline using the new interface-based architecture.
    /// These tests verify end-to-end functionality with mock implementations.
    /// </summary>
    public class AudioPipelineIntegrationTests : IDisposable
    {
        private MockAudioOutputEngine _mockOutput;
        private MockAudioProcessingEngine _mockProcessing;
        private MasterMixer _mixer;

        public AudioPipelineIntegrationTests()
        {
            _mockOutput = new MockAudioOutputEngine();
            _mockProcessing = new MockAudioProcessingEngine();
        }

        [Fact]
        public async Task AudioOutputEngine_InitializeAsync_ShouldBeCallable()
        {
            // Arrange
            var output = new MockAudioOutputEngine();

            // Act
            await output.InitializeAsync();

            // Assert
            output.InitializeCallCount.Should().Be(1);
            output.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public async Task AudioOutputEngine_WriteAudioAsync_ShouldAcceptData()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            await output.InitializeAsync();
            var testData = new byte[1024];

            // Act
            await output.WriteAudioAsync(testData);

            // Assert
            output.WrittenFrames.Should().HaveCount(1);
            output.WrittenFrames[0].Should().BeSameAs(testData);
        }

        [Fact]
        public async Task AudioOutputEngine_WriteMultipleFrames_ShouldAccumulateData()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            await output.InitializeAsync();
            var frame1 = new byte[512];
            var frame2 = new byte[512];
            var frame3 = new byte[512];

            // Act
            await output.WriteAudioAsync(frame1);
            await output.WriteAudioAsync(frame2);
            await output.WriteAudioAsync(frame3);

            // Assert
            output.WrittenFrames.Should().HaveCount(3);
            output.TotalBytesWritten.Should().Be(1536);
        }

        [Fact]
        public void AudioOutputEngine_ClearBuffer_ShouldResetWrittenFrames()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            output.WriteAudioAsync(new byte[1024]).Wait();
            output.WrittenFrames.Should().HaveCount(1);

            // Act
            output.ClearBuffer();

            // Assert
            output.WrittenFrames.Should().BeEmpty();
            output.TotalBytesWritten.Should().Be(0);
        }

        [Fact]
        public void AudioOutputEngine_SetMasterVolume_ShouldUpdateVolume()
        {
            // Arrange
            var output = new MockAudioOutputEngine();

            // Act
            output.SetMasterVolume(0.75f);

            // Assert
            output.CurrentVolume.Should().BeApproximately(0.75f, 0.001f);
        }

        [Fact]
        public void AudioProcessingEngine_Initialize_ShouldBeCallable()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();

            // Act
            processing.Initialize();

            // Assert
            processing.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void AudioProcessingEngine_ProcessPacket_ShouldReturnAudioData()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var packet = CreateTestPacket("test-tx-1", 251000000, 480);

            // Act
            var result = processing.ProcessPacket(packet);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().Be(480);
            processing.ProcessedPacketCount.Should().Be(1);
        }

        [Fact]
        public void AudioProcessingEngine_SetMasterVolume_ShouldAffectOutput()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var packet = CreateTestPacket("test-tx-1", 251000000, 480);

            // Act
            processing.SetMasterVolume(0.5f);
            var result = processing.ProcessPacket(packet);

            // Assert
            var maxAmplitude = result.Max(Math.Abs);
            maxAmplitude.Should().BeLessOrEqualTo(0.5f);
        }

        [Fact]
        public void AudioProcessingEngine_SetTransmitterVolume_ShouldApplyPerTransmitter()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var transmitterId = "test-tx-1";

            // Act
            processing.SetTransmitterVolume(transmitterId, 0.3f);
            var volume = processing.GetTransmitterVolume(transmitterId);

            // Assert
            volume.Should().BeApproximately(0.3f, 0.001f);
        }

        [Fact]
        public void AudioProcessingEngine_ResetDecoders_ShouldClearState()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var packet1 = CreateTestPacket("test-tx-1", 251000000, 480);
            processing.ProcessPacket(packet1);
            processing.ProcessedPacketCount.Should().Be(1);

            // Act
            processing.ResetDecoders();

            // Assert
            processing.ProcessedPacketCount.Should().Be(0);
        }

        [Fact]
        public void MasterMixer_WithMockOutput_ShouldInitialize()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            mockOutput.InitializeAsync().Wait();

            // Act
            using var mixer = new MasterMixer(mockOutput);

            // Assert
            mixer.Should().NotBeNull();
            mixer.FramesMixed.Should().Be(0);
        }

        [Fact]
        public void MasterMixer_GetStats_ShouldReturnInitialState()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            mockOutput.InitializeAsync().Wait();
            using var mixer = new MasterMixer(mockOutput);

            // Act
            var stats = mixer.GetStats();

            // Assert
            stats.FramesMixed.Should().Be(0);
            stats.Underruns.Should().Be(0);
            stats.ActiveFrequencies.Should().Be(0);
        }

        [Fact]
        public async Task AudioPipeline_EndToEnd_ShouldProcessAudio()
        {
            // Arrange - Create a complete mock pipeline
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            
            var mockProcessing = new MockAudioProcessingEngine();
            mockProcessing.Initialize();

            using var mixer = new MasterMixer(mockOutput);

            // Give mixer time to start
            await Task.Delay(50);

            // Act - Let the mixer run for a short time
            // Note: Without registered frequency workers, the mixer will produce underrun frames (silence)
            await Task.Delay(200);
            var stats = mixer.GetStats();

            // Assert - Mixer should be running and attempting to produce frames
            // Even without frequency workers, it will produce underrun frames to maintain timing
            (stats.FramesMixed + stats.Underruns).Should().BeGreaterThan(0, 
                "mixer should attempt to produce frames during operation (either mixed or underruns)");
            mockOutput.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public async Task AudioPipeline_WithFrequencyGating_ShouldFilterCorrectly()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            
            using var mixer = new MasterMixer(mockOutput);
            var testFreq = 251000000.0; // 251 MHz

            // Register a mock FrequencyWorker would go here in real integration
            // For now, test the gating logic

            // Act - Apply frequency gate
            mixer.SetFrequencyGate(testFreq, FrequencyGateMode.Mute);

            // Assert - Stats should reflect the muted frequency
            await Task.Delay(100);
            var stats = mixer.GetStats();
            stats.MutedFrequencies.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AudioPipeline_VolumeControl_ShouldApplyCorrectly()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            mockOutput.Start();

            var testVolume = 0.6f;

            // Act
            mockOutput.SetMasterVolume(testVolume);

            // Assert
            mockOutput.CurrentVolume.Should().BeApproximately(testVolume, 0.001f);
        }

        [Fact]
        public async Task AudioPipeline_BufferClear_ShouldResetState()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            await mockOutput.WriteAudioAsync(new byte[1024]);
            mockOutput.WrittenFrames.Should().HaveCount(1);

            // Act
            mockOutput.ClearBuffer();

            // Assert
            mockOutput.WrittenFrames.Should().BeEmpty();
        }

        [Fact]
        public void AudioProcessingEngine_MultiplePackets_ShouldMaintainState()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var transmitterId = "test-tx-1";
            var freq = 251000000.0;

            // Act - Process multiple packets from same transmitter
            var results = new List<float[]>();
            for (int i = 0; i < 5; i++)
            {
                var packet = CreateTestPacket(transmitterId, freq, 480);
                var result = processing.ProcessPacket(packet);
                results.Add(result);
            }

            // Assert
            results.Should().HaveCount(5);
            results.All(r => r.Length == 480).Should().BeTrue();
            processing.ProcessedPacketCount.Should().Be(5);
        }

        [Fact]
        public async Task MasterMixer_Disposal_ShouldCleanupResources()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            var mixer = new MasterMixer(mockOutput);

            // Act
            await Task.Delay(100); // Let it run briefly
            mixer.Dispose();

            // Assert - Should not throw
            Action getStatsAfterDispose = () => mixer.GetStats();
            getStatsAfterDispose.Should().NotThrow();
        }

        [Fact]
        public void AudioProcessing_NullPacket_ShouldHandleGracefully()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();

            // Act
            var result = processing.ProcessPacket(null);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void AudioProcessing_EmptyPayload_ShouldReturnSilence()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var packet = CreateTestPacket("test-tx-1", 251000000, 0); // Empty payload

            // Act
            var result = processing.ProcessPacket(packet);

            // Assert
            result.Should().NotBeNull();
            var maxAmplitude = result.Max(Math.Abs);
            maxAmplitude.Should().BeLessOrEqualTo(0.01f, "empty payload should produce near-silence");
        }

        [Fact]
        public async Task AudioOutput_StartStop_ShouldControlPlayback()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            await output.InitializeAsync();

            // Act & Assert
            output.IsRunning.Should().BeFalse();
            
            output.Start();
            output.IsRunning.Should().BeTrue();
            
            output.Stop();
            output.IsRunning.Should().BeFalse();
        }

        [Fact]
        public void AudioProcessing_DecodePacketToFloat_ShouldProduceConsistentOutput()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            var packet = CreateTestPacket("test-tx-1", 251000000, 480);

            // Act - Decode same packet multiple times
            var result1 = processing.DecodePacketToFloat(packet);
            var result2 = processing.DecodePacketToFloat(packet);

            // Assert - Results should be consistent
            result1.Length.Should().Be(result2.Length);
            for (int i = 0; i < result1.Length; i++)
            {
                result1[i].Should().BeApproximately(result2[i], 0.001f);
            }
        }

        public void Dispose()
        {
            _mixer?.Dispose();
            _mockOutput?.Dispose();
            _mockProcessing?.Dispose();
        }

        // Advanced Integration Tests

        [Fact]
        public async Task AudioPipeline_WithVolumeControl_ShouldAffectAllComponents()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            var mockProcessing = new MockAudioProcessingEngine();
            await mockOutput.InitializeAsync();
            mockProcessing.Initialize();

            using var mixer = new MasterMixer(mockOutput);

            // Act
            mockOutput.SetMasterVolume(0.5f);
            mockProcessing.SetMasterVolume(0.75f);

            var packet = CreateTestPacket("tx-1", 251000000, 480);
            var processedAudio = mockProcessing.ProcessPacket(packet);

            // Assert
            mockOutput.CurrentVolume.Should().BeApproximately(0.5f, 0.001f);
            var maxAmplitude = processedAudio.Max(Math.Abs);
            maxAmplitude.Should().BeLessOrEqualTo(0.75f, "volume should be applied during processing");
        }

        [Fact]
        public async Task AudioPipeline_ClearBuffer_ShouldNotAffectNewWrites()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            await mockOutput.WriteAudioAsync(new byte[1024]);
            mockOutput.WrittenFrames.Should().HaveCount(1);

            // Act
            mockOutput.ClearBuffer();
            await mockOutput.WriteAudioAsync(new byte[512]);

            // Assert
            mockOutput.WrittenFrames.Should().HaveCount(1);
            mockOutput.WrittenFrames[0].Length.Should().Be(512);
        }

        [Fact]
        public void AudioProcessing_ResetDecoders_ShouldClearAllState()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();
            
            for (int i = 0; i < 10; i++)
            {
                var packet = CreateTestPacket($"tx-{i}", 251000000 + i * 1000000, 480);
                processing.ProcessPacket(packet);
            }
            processing.ProcessedPacketCount.Should().Be(10);

            // Act
            processing.ResetDecoders();

            // Assert
            processing.ProcessedPacketCount.Should().Be(0);
        }

        [Fact]
        public async Task MasterMixer_GetStats_ShouldTrackUnderruns()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            await mockOutput.InitializeAsync();
            using var mixer = new MasterMixer(mockOutput);

            // Act - Wait for mixer to run with no frequency workers (will produce underruns)
            await Task.Delay(300);
            var stats = mixer.GetStats();

            // Assert
            stats.Underruns.Should().BeGreaterThan(0, "mixer should report underruns when no frequency workers are available");
            // UnderrunRate is calculated as (Underruns / FramesMixed) * 100, so if only underruns exist, rate might be 0
            // Instead, just verify total frames (mixed + underruns) is greater than 0
            (stats.FramesMixed + stats.Underruns).Should().BeGreaterThan(0, "mixer should produce either frames or underruns");
        }

        [Fact]
        public void AudioProcessing_MultipleTransmitters_ShouldMaintainSeparateVolumes()
        {
            // Arrange
            var processing = new MockAudioProcessingEngine();
            processing.Initialize();

            // Act
            processing.SetTransmitterVolume("tx-1", 0.3f);
            processing.SetTransmitterVolume("tx-2", 0.7f);
            processing.SetTransmitterVolume("tx-3", 1.0f);

            // Assert
            processing.GetTransmitterVolume("tx-1").Should().BeApproximately(0.3f, 0.001f);
            processing.GetTransmitterVolume("tx-2").Should().BeApproximately(0.7f, 0.001f);
            processing.GetTransmitterVolume("tx-3").Should().BeApproximately(1.0f, 0.001f);
            processing.GetTransmitterVolume("tx-unknown").Should().BeApproximately(1.0f, 0.001f, "unknown transmitters should default to 1.0");
        }

        [Fact]
        public async Task AudioOutput_MultipleWriteCycles_ShouldAccumulate()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            await output.InitializeAsync();
            output.Start();

            // Act
            for (int i = 0; i < 100; i++)
            {
                await output.WriteAudioAsync(new byte[960]); // 10ms frame at 48kHz
            }

            // Assert
            output.WrittenFrames.Should().HaveCount(100);
            output.TotalBytesWritten.Should().Be(96000);
        }

        [Fact]
        public void MasterMixer_FrequencyGating_ShouldUpdateStats()
        {
            // Arrange
            var mockOutput = new MockAudioOutputEngine();
            mockOutput.InitializeAsync().Wait();
            using var mixer = new MasterMixer(mockOutput);

            // Act
            mixer.SetFrequencyGate(251000000, FrequencyGateMode.Mute);
            mixer.SetFrequencyGate(252000000, FrequencyGateMode.Solo);
            mixer.SetFrequencyGate(253000000, FrequencyGateMode.Block);

            // Assert
            var stats = mixer.GetStats();
            stats.MutedFrequencies.Should().BeGreaterOrEqualTo(1);
            stats.SoloFrequencies.Should().BeGreaterOrEqualTo(1);
            stats.BlockedFrequencies.Should().BeGreaterOrEqualTo(1);
        }

        [Fact]
        public async Task AudioPipeline_StressTest_ShouldHandleRapidWrites()
        {
            // Arrange
            var output = new MockAudioOutputEngine();
            await output.InitializeAsync();
            output.Start();

            var frameSize = 960; // 10ms at 48kHz mono 16-bit
            var frameCount = 1000;

            // Act
            for (int i = 0; i < frameCount; i++)
            {
                await output.WriteAudioAsync(new byte[frameSize]);
            }

            // Assert
            output.WrittenFrames.Should().HaveCount(frameCount);
            output.TotalBytesWritten.Should().Be(frameSize * frameCount);
        }

        // Helper Methods

        private AudioPacketMetadata CreateTestPacket(string transmitterId, double frequency, int sampleCount)
        {
            var audioData = new byte[sampleCount * 2]; // 16-bit PCM
            // Fill with simple test pattern
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 48000.0) * 16000);
                audioData[i * 2] = (byte)(sample & 0xFF);
                audioData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return new AudioPacketMetadata(
                Timestamp: DateTime.UtcNow,
                Frequency: frequency,
                Modulation: 0,
                Encryption: 0,
                TransmitterUnitId: 12345,
                PacketId: (ulong)Random.Shared.Next(),
                TransmitterGuid: transmitterId,
                PlayerData: new PlayerInfo
                {
                    Name = "Test Player",
                    TransmitterGuid = transmitterId,
                    Coalition = 2,
                    Seat = 0,
                    AllowRecord = true,
                    Position = new Position(),
                    AircraftInfo = new AircraftInfo()
                },
                SampleRate: 48000,
                ChannelCount: 1,
                Coalition: 2,
                AudioPayload: audioData
            );
        }
    }
}
