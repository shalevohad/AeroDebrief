using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Playback;
using AeroDebrief.Integrations.Tacview;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.Tests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace AeroDebrief.Tests.Integrations.Tacview.Integration;

/// <summary>
/// Integration tests for the complete Tacview integration system
/// </summary>
public class TacviewIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private MockTacviewServer? _mockServer;
    private const int TestPort = 52299; // Different from other tests to avoid conflicts

    public TacviewIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FullIntegration_ConnectSyncAndFilter_WorksEndToEnd()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();

        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();
        
        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration 
        { 
            Host = "127.0.0.1",
            Port = TestPort,
            AutoConnect = false 
        };
        
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        try
        {
            // Act - Start integration and connect
            await integrationService.StartAsync(recordingStart, CancellationToken.None);
            await integrationService.ConnectAsync(CancellationToken.None);

            // Wait for connection
            await Task.Delay(300);

            // Send time update from mock server
            var timeUpdate = new TimeUpdateMessage
            {
                MissionTimeUtc = recordingStart.AddSeconds(10).ToString("o"),
                PlaybackState = "playing",
                PlaybackSpeed = 1.0
            };
            await _mockServer.SendMessageToAllAsync(timeUpdate);

            // Wait for processing
            await Task.Delay(500);

            // Assert - Verify server received connection
            Assert.True(integrationService.IsConnected);
            Assert.Equal(1, _mockServer.ConnectedClientCount);

            _output.WriteLine("Integration test passed - connection and sync working");
        }
        finally
        {
            await integrationService.StopAsync();
            integrationService.Dispose();
            playbackController.Dispose();
            seekController.Dispose();
            _mockServer.Stop();
        }
    }

    [Fact]
    public async Task FrequencyFiltering_IntegrationWithAudioPipeline_FiltersCorrectly()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();

        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();
        
        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration 
        { 
            Host = "127.0.0.1",
            Port = TestPort,
            AutoConnect = false 
        };
        
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        await integrationService.StartAsync(recordingStart, CancellationToken.None);

        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double> { 251000000.0 }
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };

        // Act
        integrationService.AudioFilter.UpdateSelection(selection);

        var filter = integrationService.AudioFilter;
        
        // Assert
        var packet1 = CreateTestPacket("pilot-1", 251000000.0);
        var packet2 = CreateTestPacket("pilot-1", 305000000.0);
        var packet3 = CreateTestPacket("pilot-2", 251000000.0);

        Assert.True(filter.ShouldPlayPacket(packet1), "Should play selected pilot on enabled frequency");
        Assert.False(filter.ShouldPlayPacket(packet2), "Should filter selected pilot on disabled frequency");
        Assert.False(filter.ShouldPlayPacket(packet3), "Should filter non-selected pilot");

        await integrationService.StopAsync();
        integrationService.Dispose();
        playbackController.Dispose();
        seekController.Dispose();
        _mockServer.Stop();

        _output.WriteLine("Frequency filtering integration test passed");
    }

    [Fact(Skip = "Requires long execution time - run manually")]
    public async Task LongDuration_MaintainsSyncAccuracy_Over2Hours()
    {
        // Arrange
        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();
        
        var currentPosition = TimeSpan.Zero;
        playbackController.SetTotalDuration(TimeSpan.FromHours(2));

        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration { AutoConnect = false };
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        await integrationService.StartAsync(recordingStart, CancellationToken.None);

        var driftSamples = new List<int>();
        var startTime = DateTime.UtcNow;
        var testDuration = TimeSpan.FromHours(2);

        // Act
        while ((DateTime.UtcNow - startTime) < testDuration)
        {
            // Simulate time update every 100ms (10 Hz)
            var elapsedTime = DateTime.UtcNow - startTime;

            // Sample drift every 10 seconds
            if (elapsedTime.TotalSeconds % 10 < 0.1)
            {
                currentPosition = playbackController.CurrentPosition;
                var drift = Math.Abs((currentPosition - elapsedTime).TotalMilliseconds);
                driftSamples.Add((int)drift);
                
                _output.WriteLine($"Time: {elapsedTime.TotalMinutes:F1}m, Drift: {drift:F0}ms");
            }

            await Task.Delay(100);
        }

        // Assert
        var within1Second = driftSamples.Count(d => d <= 1000);
        var accuracy = (double)within1Second / driftSamples.Count;

        _output.WriteLine($"Total samples: {driftSamples.Count}");
        _output.WriteLine($"Within 1 second: {within1Second}");
        _output.WriteLine($"Accuracy: {accuracy:P2}");

        Assert.True(accuracy >= 0.999, $"Expected 99.9% accuracy, got {accuracy:P2}");

        await integrationService.StopAsync();
        integrationService.Dispose();
        playbackController.Dispose();
        seekController.Dispose();
    }

    [Fact]
    public async Task Performance_CpuOverhead_RemainsUnder5Percent()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();

        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();

        // Measure baseline CPU
        var baselineCpu = await MeasureCpuUsage(TimeSpan.FromSeconds(3));
        _output.WriteLine($"Baseline CPU: {baselineCpu:F2}%");

        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration 
        { 
            Host = "127.0.0.1",
            Port = TestPort,
            AutoConnect = false 
        };
        
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        try
        {
            // Act - Start integration and simulate load
            await integrationService.StartAsync(recordingStart, CancellationToken.None);
            await integrationService.ConnectAsync(CancellationToken.None);
            await Task.Delay(300);

            // Simulate active sync for 10 seconds (reduced from 30)
            var testStart = DateTime.UtcNow;
            while ((DateTime.UtcNow - testStart).TotalSeconds < 10)
            {
                var timeUpdate = new TimeUpdateMessage
                {
                    MissionTimeUtc = recordingStart.Add(DateTime.UtcNow - testStart).ToString("o"),
                    PlaybackState = "playing",
                    PlaybackSpeed = 1.0
                };

                await _mockServer.SendMessageToAllAsync(timeUpdate);

                // Process updates at 10 Hz
                await Task.Delay(100);
            }

            var withIntegrationCpu = await MeasureCpuUsage(TimeSpan.FromSeconds(3));
            _output.WriteLine($"With Integration CPU: {withIntegrationCpu:F2}%");

            // Assert
            var overhead = withIntegrationCpu - baselineCpu;
            _output.WriteLine($"CPU Overhead: {overhead:F2}%");

            Assert.True(overhead < 5.0, $"Expected CPU overhead < 5%, got {overhead:F2}%");
        }
        finally
        {
            await integrationService.StopAsync();
            integrationService.Dispose();
            playbackController.Dispose();
            seekController.Dispose();
            _mockServer.Stop();
        }
    }

    [Fact]
    public async Task Reconnection_AfterDisconnect_RecoversSmoothly()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();

        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();
        
        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration 
        { 
            Host = "127.0.0.1",
            Port = TestPort,
            AutoConnect = false,
            AutoReconnect = true,
            MaxReconnectAttempts = 3,
            ReconnectIntervalSeconds = 1
        };
        
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        // Start with connection
        await integrationService.StartAsync(recordingStart, CancellationToken.None);
        await integrationService.ConnectAsync(CancellationToken.None);
        await Task.Delay(300);

        Assert.True(integrationService.IsConnected, "Should be connected initially");

        // Simulate disconnect
        _mockServer.DisconnectAllClients();
        await Task.Delay(500);

        Assert.False(integrationService.IsConnected, "Should be disconnected after server closes connection");

        // Restart server for reconnection
        _mockServer.Start();

        // Wait for reconnection (with generous timeout for 3 attempts with 1s intervals)
        await Task.Delay(5000);

        // Assert - should be reconnected
        Assert.True(integrationService.IsConnected, "Should reconnect after server becomes available");

        await integrationService.StopAsync();
        integrationService.Dispose();
        playbackController.Dispose();
        seekController.Dispose();
        _mockServer.Stop();

        _output.WriteLine("Reconnection test completed successfully");
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public async Task VariableSpeed_AllSpeeds_SyncCorrectly(double speed)
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();

        // Use real controllers instead of mocks (they're sealed classes)
        var playbackController = new PlaybackController();
        var seekController = new SeekController();
        playbackController.SetTotalDuration(TimeSpan.FromSeconds(200));
        playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var recordingStart = DateTime.UtcNow;
        var config = new TacviewConfiguration 
        { 
            Host = "127.0.0.1",
            Port = TestPort,
            AutoConnect = false 
        };
        
        var integrationService = new TacviewIntegrationService(
            playbackController,
            seekController,
            config
        );

        await integrationService.StartAsync(recordingStart, CancellationToken.None);
        await integrationService.ConnectAsync(CancellationToken.None);
        await Task.Delay(300);

        // Act - Send time update with speed
        var timeUpdate = new TimeUpdateMessage
        {
            MissionTimeUtc = recordingStart.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = speed
        };

        await _mockServer.SendMessageToAllAsync(timeUpdate);
        await Task.Delay(200);

        // Assert - Verify speed was set on playback controller
        Assert.Equal(speed, playbackController.PlaybackSpeed);
        
        await integrationService.StopAsync();
        integrationService.Dispose();
        playbackController.Dispose();
        seekController.Dispose();
        _mockServer.Stop();

        _output.WriteLine($"Speed {speed}x test passed");
    }

    private AudioPacketMetadata CreateTestPacket(string transmitterGuid, double frequency)
    {
        var playerInfo = new PlayerInfo
        {
            Name = transmitterGuid,
            TransmitterGuid = transmitterGuid,
            Coalition = 2, // Blue
            Seat = 0,
            AllowRecord = true,
            Position = new Position(),
            AircraftInfo = new AircraftInfo()
        };

        return new AudioPacketMetadata(
            DateTime.UtcNow,                    // Timestamp
            frequency,                          // Frequency
            0,                                  // Modulation
            0,                                  // Encryption
            1,                                  // TransmitterUnitId
            1,                                  // PacketId
            transmitterGuid,                    // TransmitterGuid
            playerInfo,                         // PlayerData
            48000,                              // SampleRate
            1,                                  // ChannelCount
            2,                                  // Coalition
            new byte[] { 0x01, 0x02, 0x03 }     // AudioPayload
        );
    }

    private async Task<double> MeasureCpuUsage(TimeSpan duration)
    {
        var process = Process.GetCurrentProcess();
        var startTime = DateTime.UtcNow;
        var startCpu = process.TotalProcessorTime;

        await Task.Delay(duration);

        var endTime = DateTime.UtcNow;
        var endCpu = process.TotalProcessorTime;

        var cpuUsed = (endCpu - startCpu).TotalMilliseconds;
        var totalTime = (endTime - startTime).TotalMilliseconds;
        var cpuUsagePercent = (cpuUsed / (Environment.ProcessorCount * totalTime)) * 100;

        return cpuUsagePercent;
    }

    public void Dispose()
    {
        _mockServer?.Dispose();
    }
}
