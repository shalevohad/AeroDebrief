using System;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Playback;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.Integrations.Tacview.Sync;
using Moq;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Sync;

public class TacviewSyncServiceTests
{
    private readonly Mock<PlaybackController> _mockPlaybackController;
    private readonly Mock<SeekController> _mockSeekController;
    private readonly Mock<ScrubbingManager> _mockScrubbingManager;
    private readonly TacviewConfiguration _config;
    private readonly TacviewSyncService _syncService;
    private readonly DateTime _recordingStartUtc;

    public TacviewSyncServiceTests()
    {
        _mockPlaybackController = new Mock<PlaybackController>();
        _mockSeekController = new Mock<SeekController>();
        _mockScrubbingManager = new Mock<ScrubbingManager>();
        _config = new TacviewConfiguration();
        _recordingStartUtc = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        
        _syncService = new TacviewSyncService(
            _mockPlaybackController.Object, 
            _mockSeekController.Object,
            _config,
            _mockScrubbingManager.Object
        );
        
        // Initialize the sync service with recording start time
        _syncService.Initialize(_recordingStartUtc);
    }

    [Fact]
    public async Task HandleTimeUpdate_LargeDrift_PerformsSeek()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));
        _mockPlaybackController.Setup(p => p.TotalDuration)
            .Returns(TimeSpan.FromSeconds(1000));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(200).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockSeekController.Verify(
            p => p.SeekTo(It.IsInRange(
                TimeSpan.FromSeconds(199), 
                TimeSpan.FromSeconds(201), 
                Moq.Range.Inclusive),
                It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleTimeUpdate_MediumDrift_AdjustsSpeed()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100.3).ToString("o"), // 300ms drift
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(
            p => p.SetPlaybackSpeed(It.IsInRange(1.01, 1.03, Moq.Range.Inclusive)),
            Times.Once);
    }

    [Fact]
    public async Task HandleTimeUpdate_SmallDrift_MaintainsNormalSpeed()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));
        _mockPlaybackController.Setup(p => p.PlaybackSpeed)
            .Returns(1.0);

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100.05).ToString("o"), // 50ms drift
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(
            p => p.SetPlaybackSpeed(1.0),
            Times.AtMostOnce); // May not be called if already at correct speed
    }

    [Fact]
    public async Task HandleTimeUpdate_PlaybackStateChangesToPlaying_StartsPlayback()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.IsPlaying).Returns(false);
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.Resume(), Times.Once);
    }

    [Fact]
    public async Task HandleTimeUpdate_PlaybackStateChangesToPaused_PausesPlayback()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.IsPlaying).Returns(true);
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "paused",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.Pause(), Times.Once);
    }

    [Fact]
    public async Task HandleTimeUpdate_SpeedChange_UpdatesPlaybackSpeed()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.PlaybackSpeed).Returns(1.0);
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 2.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.SetPlaybackSpeed(2.0), Times.Once);
    }

    [Fact]
    public async Task HandlePlaybackCommand_PlayCommand_StartsPlayback()
    {
        // Arrange
        var message = new PlaybackCommandMessage
        {
            Command = "play"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.Resume(), Times.Once);
    }

    [Fact]
    public async Task HandlePlaybackCommand_PauseCommand_PausesPlayback()
    {
        // Arrange
        var message = new PlaybackCommandMessage
        {
            Command = "pause"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.Pause(), Times.Once);
    }

    [Fact]
    public async Task HandlePlaybackCommand_StopCommand_StopsPlayback()
    {
        // Arrange
        var message = new PlaybackCommandMessage
        {
            Command = "stop"
        };

        // Act
        await _syncService.HandlePlaybackCommandAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.Stop(), Times.Once);
    }

    [Fact]
    public async Task HandleSeek_ValidTime_SeeksToTargetPosition()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.TotalDuration)
            .Returns(TimeSpan.FromSeconds(1000));
        var targetTime = _recordingStartUtc.AddSeconds(150);
        var message = new SeekMessage
        {
            TargetTimeUtc = targetTime.ToString("o")
        };

        // Act
        await _syncService.HandleSeekAsync(message);

        // Assert
        _mockSeekController.Verify(
            p => p.SeekTo(TimeSpan.FromSeconds(150), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSyncHealth_ReturnsSyncStatistics()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

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
        Assert.NotNull(health);
        Assert.True(health.QualityPercent >= 0 && health.QualityPercent <= 100);
    }

    [Fact]
    public async Task HandleTimeUpdate_NegativeDrift_AdjustsSpeedDown()
    {
        // Arrange
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(99.7).ToString("o"), // 300ms behind
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(
            p => p.SetPlaybackSpeed(It.IsInRange(0.97, 0.99, Moq.Range.Inclusive)),
            Times.Once);
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
        _mockPlaybackController.Setup(p => p.PlaybackSpeed).Returns(1.0);
        _mockPlaybackController.Setup(p => p.CurrentPosition)
            .Returns(TimeSpan.FromSeconds(100));

        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = _recordingStartUtc.AddSeconds(100).ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = speed
        };

        // Act
        await _syncService.HandleTimeUpdateAsync(message);

        // Assert
        _mockPlaybackController.Verify(p => p.SetPlaybackSpeed(speed), Times.Once);
    }
}
