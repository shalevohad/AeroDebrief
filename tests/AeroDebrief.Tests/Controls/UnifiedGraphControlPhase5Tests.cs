using Xunit;
using AeroDebrief.UI.ViewModels;
using System;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Controls
{
    /// <summary>
    /// Integration tests for UnifiedGraphControl Phase 5: Zoom/Pan gestures.
    /// Note: WPF control creation requires STA thread, so we test ViewModel logic only.
    /// </summary>
    public class UnifiedGraphControlPhase5Tests
    {
        private UnifiedGraphViewModel CreateViewModel()
        {
            return new UnifiedGraphViewModel();
        }

        [Fact]
        public void ViewModel_HasViewportProperties()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm);
            Assert.NotEqual(default(DateTime), vm.ViewportStart);
            Assert.NotEqual(default(DateTime), vm.ViewportEnd);
            Assert.True(vm.ViewportDuration > TimeSpan.Zero);
        }

        [Fact]
        public void ViewModel_CanZoomIn()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);
            var initialDuration = vm.ViewportDuration;

            // Act
            vm.ZoomIn(0.5);

            // Assert
            Assert.True(vm.ViewportDuration < initialDuration);
        }

        [Fact]
        public void ViewModel_CanZoomOut()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start.AddMinutes(2), start.AddMinutes(8));
            var initialDuration = vm.ViewportDuration;

            // Act
            vm.ZoomOut(2.0);

            // Assert
            Assert.True(vm.ViewportDuration > initialDuration);
        }

        [Fact]
        public void ViewModel_CanPan()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(5));
            var initialStart = vm.ViewportStart;

            // Act
            vm.Pan(TimeSpan.FromMinutes(2));

            // Assert
            Assert.True(vm.ViewportStart > initialStart);
        }

        [Fact]
        public void ViewModel_ViewportChangedEvent_Fires()
        {
            // Arrange
            var vm = CreateViewModel();
            var eventFired = false;
            vm.ViewportChanged += (s, e) => eventFired = true;

            // Act
            vm.SetViewport(DateTime.Now, DateTime.Now.AddMinutes(10));

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public async Task ViewModel_ResetViewport_RestoresFullRange()
        {
            // Arrange
            var provider = new Tests.ViewModels.MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            
            // Zoom in
            vm.ZoomIn(0.5);
            Assert.True(vm.ViewportDuration < TimeSpan.FromMinutes(10));

            // Act
            vm.ResetViewport();

            // Assert
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
            Assert.Equal(TimeSpan.FromMinutes(10), vm.ViewportDuration);
        }

        [Fact]
        public void ViewModel_InitializesWithDefaultViewport()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm);
            Assert.NotNull(vm.ViewportStart);
            Assert.NotNull(vm.ViewportEnd);
            Assert.True(vm.ViewportEnd > vm.ViewportStart);
        }

        #region Phase 5 Step 3: Keyboard Navigation Tests

        [Fact]
        public void ViewModel_PanByPercentage_Works()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(5));
            var initialStart = vm.ViewportStart;

            // Act - Pan by 10% of viewport
            var panAmount = vm.ViewportDuration * 0.1;
            vm.Pan(panAmount);

            // Assert
            Assert.True(vm.ViewportStart > initialStart);
            Assert.Equal(TimeSpan.FromMinutes(5), vm.ViewportDuration); // Duration unchanged
        }

        [Fact]
        public void ViewModel_PanBackward_Works()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start.AddMinutes(3), start.AddMinutes(8));
            var initialStart = vm.ViewportStart;

            // Act - Pan backward (negative delta)
            vm.Pan(TimeSpan.FromMinutes(-1));

            // Assert
            Assert.True(vm.ViewportStart < initialStart);
        }

        [Fact]
        public async Task ViewModel_JumpToStart_Works()
        {
            // Arrange
            var provider = new Tests.ViewModels.MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            
            // Zoom in and pan to middle
            vm.SetViewport(start.AddMinutes(4), start.AddMinutes(6));

            // Act - Jump to start
            vm.SetViewport(start, start + vm.ViewportDuration);

            // Assert
            Assert.Equal(start, vm.ViewportStart);
        }

        [Fact]
        public async Task ViewModel_JumpToEnd_Works()
        {
            // Arrange
            var provider = new Tests.ViewModels.MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            
            // Zoom in and pan to middle
            vm.SetViewport(start.AddMinutes(4), start.AddMinutes(6));
            var viewportDuration = vm.ViewportDuration;

            // Act - Jump to end
            vm.SetViewport(end - viewportDuration, end);

            // Assert
            Assert.Equal(end, vm.ViewportEnd);
        }

        #endregion
    }

    /// <summary>
    /// Tests for viewport synchronization between ViewModel and Chart.
    /// </summary>
    public class ViewportSynchronizationTests
    {
        private UnifiedGraphViewModel CreateViewModel()
        {
            return new UnifiedGraphViewModel();
        }

        [Fact]
        public void ViewportChange_UpdatesViewModel()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            vm.SetViewport(start, end);

            // Assert
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
        }

        [Fact]
        public void ZoomIn_TriggersViewportChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var eventCount = 0;
            vm.ViewportChanged += (s, e) => eventCount++;

            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);
            eventCount = 0; // Reset after initial set

            // Act
            vm.ZoomIn(0.5);

            // Assert
            Assert.True(eventCount > 0, "ViewportChanged event should fire on zoom");
        }

        [Fact]
        public void Pan_TriggersViewportChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var eventCount = 0;
            vm.ViewportChanged += (s, e) => eventCount++;

            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(5));
            eventCount = 0; // Reset after initial set

            // Act
            vm.Pan(TimeSpan.FromMinutes(1));

            // Assert
            Assert.True(eventCount > 0, "ViewportChanged event should fire on pan");
        }

        [Fact]
        public void MultipleZoomOperations_MaintainConsistency()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);

            // Act - Zoom in then out
            vm.ZoomIn(0.5);
            var afterZoomIn = vm.ViewportDuration;
            
            vm.ZoomOut(2.0);
            var afterZoomOut = vm.ViewportDuration;

            // Assert
            Assert.True(afterZoomIn < TimeSpan.FromMinutes(10));
            Assert.True(afterZoomOut > afterZoomIn);
        }

        [Fact]
        public async Task PanOperations_StayWithinBounds()
        {
            // Arrange
            var provider = new Tests.ViewModels.MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            
            vm.SetViewport(start, start.AddMinutes(5));

            // Act - Try to pan beyond end
            vm.Pan(TimeSpan.FromMinutes(10));

            // Assert - Should clamp to data range
            Assert.True(vm.ViewportEnd <= end);
            Assert.True(vm.ViewportStart >= start);
        }
    }
}
