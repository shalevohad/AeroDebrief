using Xunit;
using AeroDebrief.UI.ViewModels;
using System;

namespace AeroDebrief.Tests.Controls
{
    /// <summary>
    /// Integration tests for UnifiedGraphControl Phase 6 Step 2: Playhead Visual Line.
    /// Note: WPF control rendering requires STA thread, so we test ViewModel logic.
    /// </summary>
    public class UnifiedGraphControlPhase6Step2Tests
    {
        private UnifiedGraphViewModel CreateViewModel()
        {
            var vm = new UnifiedGraphViewModel();
            
            // Initialize Start/End to reasonable values for testing
            var now = DateTime.Now;
            vm.Start = now;
            vm.End = now.AddMinutes(10);
            
            return vm;
        }

        [Fact]
        public void PlayheadTime_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testTime = DateTime.Now.AddMinutes(5);

            // Act
            vm.PlayheadTime = testTime;

            // Assert
            Assert.Equal(testTime, vm.PlayheadTime);
        }

        [Fact]
        public void PlayheadTimeChanged_EventFires()
        {
            // Arrange
            var vm = CreateViewModel();
            var eventFired = false;
            DateTime? eventTime = null;

            vm.PlayheadTimeChanged += (s, time) =>
            {
                eventFired = true;
                eventTime = time;
            };

            var testTime = DateTime.Now.AddMinutes(5);

            // Act
            vm.PlayheadTime = testTime;

            // Assert
            Assert.True(eventFired);
            Assert.Equal(testTime, eventTime);
        }

        [Fact]
        public void FollowMode_AutoPansWhenPlayheadNearEdge()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(1)); // 1 minute viewport
            
            // Initialize playhead at viewport start
            vm.PlayheadTime = start;
            
            vm.FollowMode = true;
            vm.IsPlaying = true;

            var initialViewportStart = vm.ViewportStart;

            // Act - Move playhead beyond 40% threshold
            // Viewport spans 0:00 to 1:00, center at 0:30
            // Move to 0:55 which is 25 seconds to center (42% of 60 seconds)
            vm.PlayheadTime = start.AddSeconds(55);

            // Assert - Viewport should have panned
            Assert.NotEqual(initialViewportStart, vm.ViewportStart);
            
            // Playhead should now be more centered
            var newCenter = vm.ViewportStart + vm.ViewportDuration / 2;
            var distanceFromCenter = (vm.PlayheadTime - newCenter).Duration();
            Assert.True(distanceFromCenter < vm.ViewportDuration * 0.4); // Within 40% dead zone
        }

        [Fact]
        public void FollowMode_DoesNotPanWhenPlayheadNearCenter()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(1));
            vm.FollowMode = true;
            vm.IsPlaying = true;

            var initialViewportStart = vm.ViewportStart;

            // Act - Move playhead slightly (within 40% dead zone)
            // Viewport center is at 0:30, move to 0:35 (5 seconds = 8.3% of 60s viewport)
            vm.PlayheadTime = start.AddSeconds(35);

            // Assert - Viewport should NOT have panned
            Assert.Equal(initialViewportStart, vm.ViewportStart);
        }

        [Fact]
        public void FollowMode_Disabled_DoesNotPan()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(1));
            vm.FollowMode = false; // Disabled
            vm.IsPlaying = true;

            var initialViewportStart = vm.ViewportStart;

            // Act - Move playhead far from center
            vm.PlayheadTime = start.AddMinutes(5);

            // Assert - Viewport should NOT have panned
            Assert.Equal(initialViewportStart, vm.ViewportStart);
        }

        [Fact]
        public void FollowMode_NotPlaying_DoesNotPan()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(1));
            vm.FollowMode = true;
            vm.IsPlaying = false; // Not playing

            var initialViewportStart = vm.ViewportStart;

            // Act - Move playhead far from center
            vm.PlayheadTime = start.AddMinutes(5);

            // Assert - Viewport should NOT have panned
            Assert.Equal(initialViewportStart, vm.ViewportStart);
        }

        [Fact]
        public void FollowMode_ClampsToBounds()
        {
            // Arrange
            var vm = CreateViewModel();
            // CreateViewModel now sets Start and End properly
            var start = vm.Start;
            var end = vm.End;
            
            vm.SetViewport(start, start.AddMinutes(1));
            
            // Initialize playhead
            vm.PlayheadTime = start;
            
            vm.FollowMode = true;
            vm.IsPlaying = true;

            // Act - Move playhead near the very start
            vm.PlayheadTime = start.AddSeconds(5);

            // Assert - Viewport should clamp to start
            Assert.True(vm.ViewportStart >= start, 
                $"ViewportStart ({vm.ViewportStart:HH:mm:ss}) should be >= Start ({start:HH:mm:ss})");
            Assert.True(vm.ViewportEnd <= end,
                $"ViewportEnd ({vm.ViewportEnd:HH:mm:ss}) should be <= End ({end:HH:mm:ss})");
        }

        [Fact]
        public void IsPlaying_Property_WorksCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.IsPlaying = true;

            // Assert
            Assert.True(vm.IsPlaying);

            // Act
            vm.IsPlaying = false;

            // Assert
            Assert.False(vm.IsPlaying);
        }

        [Fact]
        public void PlaybackRate_Property_WorksCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.PlaybackRate = 2.0;

            // Assert
            Assert.Equal(2.0, vm.PlaybackRate);

            // Act
            vm.PlaybackRate = 0.5;

            // Assert
            Assert.Equal(0.5, vm.PlaybackRate);
        }

        [Fact]
        public void FollowMode_Toggle_WorksCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var initialState = vm.FollowMode;

            // Act
            vm.FollowMode = !vm.FollowMode;

            // Assert
            Assert.NotEqual(initialState, vm.FollowMode);

            // Act
            vm.FollowMode = !vm.FollowMode;

            // Assert
            Assert.Equal(initialState, vm.FollowMode);
        }

        [Fact]
        public void FollowMode_MultipleUpdates_PansCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(1));
            
            // Initialize playhead at start
            vm.PlayheadTime = start;
            
            vm.FollowMode = true;
            vm.IsPlaying = true;

            // Act - Simulate playback progression
            for (int i = 1; i <= 10; i++) // Start from 1 to avoid setting to same value
            {
                vm.PlayheadTime = start.AddSeconds(i * 10); // Every 10 seconds
            }

            // Assert - Final playhead should be visible and reasonably centered
            Assert.True(vm.PlayheadTime >= vm.ViewportStart, 
                $"Playhead {vm.PlayheadTime:HH:mm:ss} should be >= ViewportStart {vm.ViewportStart:HH:mm:ss}");
            Assert.True(vm.PlayheadTime <= vm.ViewportEnd,
                $"Playhead {vm.PlayheadTime:HH:mm:ss} should be <= ViewportEnd {vm.ViewportEnd:HH:mm:ss}");

            // Should be reasonably centered (within 60% to allow for threshold behavior)
            var center = vm.ViewportStart + vm.ViewportDuration / 2;
            var distanceFromCenter = (vm.PlayheadTime - center).Duration();
            Assert.True(distanceFromCenter < vm.ViewportDuration * 0.6, 
                $"Distance from center ({distanceFromCenter.TotalSeconds:F1}s) should be < 60% of viewport ({(vm.ViewportDuration * 0.6).TotalSeconds:F1}s)");
        }
    }
}
