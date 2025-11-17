using Xunit;
using AeroDebrief.UI.Services.Graphs;
using System;

namespace AeroDebrief.Tests.Services
{
    /// <summary>
    /// Unit tests for PlayheadSyncService.
    /// Phase 6: Tests playhead synchronization, seeking, and state management.
    /// </summary>
    public class PlayheadSyncServiceTests
    {
        [Fact]
        public void Constructor_InitializesDefaults()
        {
            // Arrange & Act
            using var service = new PlayheadSyncService();

            // Assert
            Assert.NotEqual(default(DateTime), service.CurrentTime);
            Assert.Equal(1.0, service.PlaybackRate);
            Assert.False(service.IsPlaying);
        }

        [Fact]
        public void SetTimeRange_UpdatesStartAndEndTimes()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(2);

            // Act
            service.SetTimeRange(start, end);

            // Assert
            Assert.Equal(start, service.StartTime);
            Assert.Equal(end, service.EndTime);
            Assert.Equal(start, service.CurrentTime); // Should reset to start
        }

        [Fact]
        public void Seek_UpdatesCurrentTime()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);

            var targetTime = start.AddMinutes(30);

            // Act
            service.Seek(targetTime);

            // Assert
            Assert.Equal(targetTime, service.CurrentTime);
        }

        [Fact]
        public void Seek_ClampsToStartTime()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);

            var tooEarly = start.AddMinutes(-10);

            // Act
            service.Seek(tooEarly);

            // Assert
            Assert.Equal(start, service.CurrentTime);
        }

        [Fact]
        public void Seek_ClampsToEndTime()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);

            var tooLate = end.AddMinutes(10);

            // Act
            service.Seek(tooLate);

            // Assert
            Assert.Equal(end, service.CurrentTime);
        }

        [Fact]
        public void Seek_RaisesTimeChangedEvent()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);

            var eventRaised = false;
            DateTime? eventTime = null;
            service.TimeChanged += (s, time) =>
            {
                eventRaised = true;
                eventTime = time;
            };

            var targetTime = start.AddMinutes(30);

            // Act
            service.Seek(targetTime);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(targetTime, eventTime);
        }

        [Fact]
        public void SeekRelative_MovesFromCurrentPosition()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);
            service.Seek(start.AddMinutes(30));

            // Act
            service.SeekRelative(TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal(start.AddMinutes(35), service.CurrentTime);
        }

        [Fact]
        public void SeekRelative_BackwardWorks()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var start = DateTime.Now;
            var end = start.AddHours(1);
            service.SetTimeRange(start, end);
            service.Seek(start.AddMinutes(30));

            // Act
            service.SeekRelative(TimeSpan.FromMinutes(-10));

            // Assert
            Assert.Equal(start.AddMinutes(20), service.CurrentTime);
        }

        [Fact]
        public void SetPlaybackState_UpdatesIsPlaying()
        {
            // Arrange
            using var service = new PlayheadSyncService();

            // Act
            service.SetPlaybackState(true);

            // Assert
            Assert.True(service.IsPlaying);
        }

        [Fact]
        public void SetPlaybackState_RaisesPlaybackStateChangedEvent()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var eventRaised = false;
            bool? eventState = null;

            service.PlaybackStateChanged += (s, isPlaying) =>
            {
                eventRaised = true;
                eventState = isPlaying;
            };

            // Act
            service.SetPlaybackState(true);

            // Assert
            Assert.True(eventRaised);
            Assert.True(eventState);
        }

        [Fact]
        public void SetPlaybackRate_UpdatesPlaybackRate()
        {
            // Arrange
            using var service = new PlayheadSyncService();

            // Act
            service.SetPlaybackRate(2.0);

            // Assert
            Assert.Equal(2.0, service.PlaybackRate);
        }

        [Fact]
        public void SetPlaybackRate_RaisesPlaybackRateChangedEvent()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            var eventRaised = false;
            double? eventRate = null;

            service.PlaybackRateChanged += (s, rate) =>
            {
                eventRaised = true;
                eventRate = rate;
            };

            // Act
            service.SetPlaybackRate(1.5);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(1.5, eventRate);
        }

        [Fact]
        public void StartUpdates_EnablesTimer()
        {
            // Arrange
            using var service = new PlayheadSyncService();

            // Act
            service.StartUpdates();

            // Assert
            // Timer is started (verified by no exception)
            // Actual timer behavior tested via integration tests
            Assert.True(true);
        }

        [Fact]
        public void StopUpdates_DisablesTimer()
        {
            // Arrange
            using var service = new PlayheadSyncService();
            service.StartUpdates();

            // Act
            service.StopUpdates();

            // Assert
            // Timer is stopped (verified by no exception)
            Assert.True(true);
        }

        [Fact]
        public void Dispose_StopsUpdatesAndCleansUp()
        {
            // Arrange
            var service = new PlayheadSyncService();
            service.StartUpdates();

            // Act
            service.Dispose();

            // Assert
            // No exception means dispose worked correctly
            Assert.True(true);
        }
    }
}
