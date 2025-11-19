using Xunit;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Visualization.Graphs;
using AeroDebrief.UI.Interfaces.Visualization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.ViewModels
{
    /// <summary>
    /// Tests for UnifiedGraphViewModel Phase 5: Viewport management for minimap and zoom/pan.
    /// </summary>
    public class UnifiedGraphViewModelPhase5Tests
    {
        private UnifiedGraphViewModel CreateViewModel()
        {
            var provider = new MockAmplitudeSeriesProvider();
            return new UnifiedGraphViewModel(provider);
        }

        [Fact]
        public async Task LoadDataAsync_InitializesViewportToFullRange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            await vm.LoadDataAsync(start, end);

            // Assert
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
            Assert.Equal(TimeSpan.FromMinutes(10), vm.ViewportDuration);
        }

        [Fact]
        public void SetViewport_UpdatesProperties()
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
            Assert.Equal(TimeSpan.FromMinutes(10), vm.ViewportDuration);
        }

        [Fact]
        public void SetViewport_RaisesViewportChangedEvent()
        {
            // Arrange
            var vm = CreateViewModel();
            var eventRaised = false;
            vm.ViewportChanged += (s, e) => eventRaised = true;

            // Act
            vm.SetViewport(DateTime.Now, DateTime.Now.AddMinutes(10));

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task SetViewport_ClampsToDataRange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);

            // Act - Try to set viewport outside data range
            vm.SetViewport(start.AddMinutes(-5), end.AddMinutes(5));

            // Assert - Should clamp to data range
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
        }

        [Fact]
        public void ZoomIn_ReducesViewportSize()
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
            Assert.Equal(TimeSpan.FromMinutes(5), vm.ViewportDuration);
        }

        [Fact]
        public void ZoomIn_KeepsCenterPoint()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);
            var initialCenter = start + TimeSpan.FromMinutes(5);

            // Act
            vm.ZoomIn(0.5);

            // Assert
            var newCenter = vm.ViewportStart + (vm.ViewportEnd - vm.ViewportStart) / 2;
            Assert.Equal(initialCenter, newCenter);
        }

        [Fact]
        public void ZoomOut_IncreasesViewportSize()
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
        public async Task ZoomOut_ClampsToDataRange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            vm.SetViewport(start.AddMinutes(4), start.AddMinutes(6));

            // Act - Zoom out beyond data range
            vm.ZoomOut(10.0);

            // Assert - Should clamp to data range
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
        }

        [Fact]
        public void Pan_MovesViewport()
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
            Assert.Equal(initialStart.AddMinutes(2), vm.ViewportStart);
        }

        [Fact]
        public void Pan_MaintainsViewportDuration()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, start.AddMinutes(5));
            var initialDuration = vm.ViewportDuration;

            // Act
            vm.Pan(TimeSpan.FromMinutes(2));

            // Assert
            Assert.Equal(initialDuration, vm.ViewportDuration);
        }

        [Fact]
        public async Task Pan_ClampsToDataRange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            vm.SetViewport(start, start.AddMinutes(5));

            // Act - Try to pan beyond end
            vm.Pan(TimeSpan.FromMinutes(10));

            // Assert - Should clamp to data range
            Assert.Equal(start.AddMinutes(5), vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
        }

        [Fact]
        public async Task ResetViewport_ShowsFullRange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            await vm.LoadDataAsync(start, end);
            vm.SetViewport(start.AddMinutes(2), start.AddMinutes(8));

            // Act
            vm.ResetViewport();

            // Assert
            Assert.Equal(start, vm.ViewportStart);
            Assert.Equal(end, vm.ViewportEnd);
        }

        [Fact]
        public void ZoomIn_WithInvalidFactor_DoesNotChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);
            var initialDuration = vm.ViewportDuration;

            // Act
            vm.ZoomIn(1.5); // Invalid (>= 1.0)

            // Assert
            Assert.Equal(initialDuration, vm.ViewportDuration);
        }

        [Fact]
        public void ZoomOut_WithInvalidFactor_DoesNotChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);
            vm.SetViewport(start, end);
            var initialDuration = vm.ViewportDuration;

            // Act
            vm.ZoomOut(0.5); // Invalid (<= 1.0)

            // Assert
            Assert.Equal(initialDuration, vm.ViewportDuration);
        }

        [Fact]
        public void ViewportDuration_CalculatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            vm.SetViewport(start, end);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), vm.ViewportDuration);
        }

        [Fact]
        public void ViewportChanged_RaisedOnStartChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var eventCount = 0;
            vm.ViewportChanged += (s, e) => eventCount++;
            vm.SetViewport(start, start.AddMinutes(10));
            eventCount = 0; // Reset

            // Act
            vm.ViewportStart = start.AddMinutes(1);

            // Assert
            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void ViewportChanged_RaisedOnEndChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var start = DateTime.Now;
            var eventCount = 0;
            vm.ViewportChanged += (s, e) => eventCount++;
            vm.SetViewport(start, start.AddMinutes(10));
            eventCount = 0; // Reset

            // Act
            vm.ViewportEnd = start.AddMinutes(11);

            // Assert
            Assert.Equal(1, eventCount);
        }
    }

    /// <summary>
    /// Mock amplitude series provider for testing.
    /// </summary>
    internal class MockAmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        public async IAsyncEnumerable<(string key, IEnumerable<LiveChartsCore.Defaults.ObservablePoint> points)> GetSeriesAsync(
            DateTime start,
            DateTime end,
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Return empty series for testing
            await Task.CompletedTask;
            yield break;
        }
    }
}
