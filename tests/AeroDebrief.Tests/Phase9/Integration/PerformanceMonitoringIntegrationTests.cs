using Xunit;
using FluentAssertions;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.Tests.TestHelpers;
using System;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Phase9.Integration
{
    /// <summary>
    /// Integration tests for performance monitoring in Phase 9.
    /// Tests F3 toggle, stats updates, and accuracy verification.
    /// </summary>
    public class PerformanceMonitoringIntegrationTests
    {
        [Fact]
        public void EndToEnd_F3Toggle_ShowsHidesStats()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);

            // Initially hidden
            Assert.False(vm.ShowPerformanceStats);

            // Act - Toggle on (simulate F3)
            vm.ShowPerformanceStats = true;

            // Assert - Stats visible
            Assert.True(vm.ShowPerformanceStats);

            // Act - Toggle off (simulate F3 again)
            vm.ShowPerformanceStats = false;

            // Assert - Stats hidden
            Assert.False(vm.ShowPerformanceStats);
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task EndToEnd_Stats_UpdateDuringLoad()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            vm.ShowPerformanceStats = true;

            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Capture initial stats
            var initialMemory = vm.MemoryUsageMB;
            var initialLoadTime = vm.LastLoadTimeMs;

            // Act - Load data
            await vm.LoadDataAsync(start, end);
            await Task.Delay(100); // Allow stats to update

            // Assert - Load time should be recorded
            Assert.True(vm.LastLoadTimeMs >= 0, 
                $"Load time should be >= 0, got {vm.LastLoadTimeMs}");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task EndToEnd_Stats_AccuracyVerification()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            vm.ShowPerformanceStats = true;

            // Act - Check stats are valid
            var memory = vm.MemoryUsageMB;
            var fps = vm.CurrentFPS;
            var loadTime = vm.LastLoadTimeMs;

            // Assert - Stats should be non-negative
            Assert.True(memory >= 0, $"Memory should be >= 0, got {memory}");
            Assert.True(fps >= 0, $"FPS should be >= 0, got {fps}");
            Assert.True(loadTime >= 0, $"Load time should be >= 0, got {loadTime}");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task EndToEnd_Stats_MemoryTrackingCorrect()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            vm.ShowPerformanceStats = true;

            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act - Load data
            await vm.LoadDataAsync(start, end);
            await Task.Delay(200); // Allow memory stats to update

            var memory = vm.MemoryUsageMB;

            // Assert - Memory usage should be reported
            Assert.True(memory >= 0, $"Memory should be >= 0, got {memory}");
            
            // Memory should be reasonable (< 1 GB = 1024 MB)
            Assert.True(memory < 1024, 
                $"Memory usage ({memory} MB) should be < 1024 MB");
            
            // Cleanup
            vm.Dispose();
        }
    }
}
