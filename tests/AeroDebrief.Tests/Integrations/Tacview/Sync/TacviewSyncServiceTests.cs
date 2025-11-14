using System;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Playback;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.Integrations.Tacview.Sync;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Sync;

public class TacviewSyncServiceTests : IDisposable
{
    private readonly PlaybackController _playbackController;
    private readonly SeekController _seekController;
    private readonly ScrubbingManager _scrubbingManager;
    private readonly TacviewConfiguration _config;
    private readonly TacviewSyncService _syncService;
    private readonly DateTime _recordingStartUtc;

    public TacviewSyncServiceTests()
    {
        _playbackController = new PlaybackController();
        _seekController = new SeekController();
        _scrubbingManager = new ScrubbingManager();
        _config = new TacviewConfiguration();
        _recordingStartUtc = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        
        // Set up playback controller with test data
        _playbackController.SetTotalDuration(TimeSpan.FromSeconds(1000));
        _playbackController.SetRecordingStart(_recordingStartUtc);
        
        _syncService = new TacviewSyncService(
            _playbackController, 
            _seekController,
            _config,
            _scrubbingManager
        );
        
        // Initialize the sync service with recording start time
        _syncService.Initialize(_recordingStartUtc);
    }

    [Fact]
    public async Task HandleTimeUpdate_LargeDrift_PerformsSeek()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(200).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _seekController.HasPendingSeek().Should().BeTrue("should have requested a seek due to large drift");
    }

    [Fact]
    public async Task HandleTimeUpdate_MediumDrift_AdjustsSpeed()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100.3).ToString("o"), // 300ms drift
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.PlaybackSpeed.Should().BeInRange(1.01, 1.03, 
            "should adjust speed to compensate for medium drift");
    }

    [Fact]
    public async Task HandleTimeUpdate_SmallDrift_MaintainsNormalSpeed()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));
        _playbackController.SetPlaybackSpeed(1.0);

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100.05).ToString("o"), // 50ms drift
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.PlaybackSpeed.Should().BeApproximately(1.0, 0.02,
            "should maintain normal speed for small drift");
    }

    [Fact]
    public async Task HandleTimeUpdate_PlaybackStateChangesToPlaying_ResumesPlayback()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));
        // Start with paused state - we need to actually start playback and then pause it
        _playbackController.Start("test", async (ct) => { await Task.Delay(100, ct); });
        await Task.Delay(50); // Let it start
        _playbackController.Pause();

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert - after resume, IsPlaying should be true and IsPaused should be false
        _playbackController.IsPlaying.Should().BeTrue("should resume playback");
        _playbackController.IsPaused.Should().BeFalse("should not be paused after resume");
    }

    [Fact]
    public async Task HandleTimeUpdate_PlaybackStateChangesToPaused_PausesPlayback()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));
        // Start playback
        _playbackController.Start("test", async (ct) => { await Task.Delay(1000, ct); });
        await Task.Delay(50); // Let it start

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "paused",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.IsPaused.Should().BeTrue("should pause playback");
    }

    [Fact]
    public async Task HandleTimeUpdate_SpeedChange_UpdatesPlaybackSpeed()
    {
        // Arrange
        _playbackController.SetPlaybackSpeed(1.0);
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 2.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.PlaybackSpeed.Should().Be(2.0, "should update to requested speed");
    }

    [Fact]
    public async Task HandlePlaybackCommand_PlayCommand_ResumesPlayback()
    {
        // Arrange
        _playbackController.Start("test", async (ct) => { await Task.Delay(100, ct); });
        await Task.Delay(50);
        _playbackController.Pause();
        
        var message = new PlaybackCommandMessage
        {
            Command = "play"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);

        // Assert
        _playbackController.IsPlaying.Should().BeTrue("should resume playback on play command");
    }

    [Fact]
    public async Task HandlePlaybackCommand_PauseCommand_PausesPlayback()
    {
        // Arrange
        _playbackController.Start("test", async (ct) => { await Task.Delay(1000, ct); });
        await Task.Delay(50);
        
        var message = new PlaybackCommandMessage
        {
            Command = "pause"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);

        // Assert
        _playbackController.IsPaused.Should().BeTrue("should pause on pause command");
    }

    [Fact]
    public async Task HandlePlaybackCommand_StopCommand_StopsPlayback()
    {
        // Arrange
        _playbackController.Start("test", async (ct) => { await Task.Delay(1000, ct); });
        await Task.Delay(50);
        
        var message = new PlaybackCommandMessage
        {
            Command = "stop"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);
        await Task.Delay(100); // Give time for stop to complete

        // Assert
        _playbackController.IsPlaying.Should().BeFalse("should stop playback on stop command");
    }

    [Fact]
    public async Task HandleSeek_ValidTime_SeeksToTargetPosition()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(50));
        var targetTime = _recordingStartUtc.AddSeconds(150);
        var message = new SeekMessage
        {
            TargetTimeUtc = targetTime.ToString("o")
        };

        // Act
        await _syncService.HandleSeekAsync(message);

        // Assert
        _seekController.HasPendingSeek().Should().BeTrue("should have pending seek after seek command");
    }

    [Fact]
    public async Task GetSyncHealth_ReturnsSyncStatistics()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };
        await _syncService.HandleTimeUpdateAsync(message);

        // Act
        var health = _syncService.GetSyncHealth();

        // Assert
        health.Should().NotBeNull();
        health.QualityPercent.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task HandleTimeUpdate_NegativeDrift_AdjustsSpeedDown()
    {
        // Arrange
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(99.7).ToString("o"), // 300ms behind
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.PlaybackSpeed.Should().BeInRange(0.97, 0.99,
            "should adjust speed down for negative drift");
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public async Task HandleTimeUpdate_VariousSpeeds_AppliesCorrectSpeed(double speed)
    {
        // Arrange
        _playbackController.SetPlaybackSpeed(1.0);
        _playbackController.UpdatePosition(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = speed
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _playbackController.PlaybackSpeed.Should().Be(speed, $"should set playback speed to {speed}x");
    }

    public void Dispose()
    {
        _playbackController?.Dispose();
        _seekController?.Dispose();
    }
}
