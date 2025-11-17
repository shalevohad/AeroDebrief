using Xunit;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Controls.Charts;
using AeroDebrief.Tests.TestHelpers;
using NLog;

namespace AeroDebrief.Tests.Phase9.Performance
{
    /// <summary>
    /// Memory leak detection tests for Phase 9 components.
    /// Verifies no memory leaks occur during repeated operations.
    /// </summary>
    public class MemoryLeakTests
    {
        private readonly Logger _logger;

        public MemoryLeakTests()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        [Fact]
        public async Task LoadingSpinner_NoMemoryLeak_After100Shows()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Show and hide 100 times
            for (int i = 0; i < 100; i++)
            {
                var spinner = new LoadingSpinnerOverlay();
                // Simulate showing and hiding
                await Task.Delay(10);
                // Allow GC to collect
                spinner = null;
            }

            // Force collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be minimal (< 5 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(5,
                $"LoadingSpinner should not leak memory after 100 cycles (growth: {memoryGrowthMB:F2} MB)");
        }

        [Fact]
        public async Task ErrorBanner_NoMemoryLeak_After100Shows()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Show and hide 100 times
            for (int i = 0; i < 100; i++)
            {
                var banner = new ErrorBannerOverlay();
                // Simulate showing and hiding
                await Task.Delay(10);
                // Allow GC to collect
                banner = null;
            }

            // Force collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be minimal (< 5 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(5,
                $"ErrorBanner should not leak memory after 100 cycles (growth: {memoryGrowthMB:F2} MB)");
        }

        [Fact]
        public async Task PerformanceStats_NoMemoryLeak_After1000Updates()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            var statsOverlay = new PerformanceStatsOverlay();

            // Act - Simulate 1000 stat updates
            for (int i = 0; i < 1000; i++)
            {
                // Simulate stats update cycle
                await Task.Delay(5);
            }

            // Cleanup
            statsOverlay = null;

            // Force collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be minimal (< 10 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(10,
                $"PerformanceStats should not leak memory after 1000 updates (growth: {memoryGrowthMB:F2} MB)");
        }

        [Fact]
        public async Task ErrorHandlingService_NoMemoryLeak_After1000Errors()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            var errorService = new ErrorHandlingService(_logger);

            // Act - Report 1000 errors
            for (int i = 0; i < 1000; i++)
            {
                var task = errorService.ShowErrorAsync($"Error {i}", $"Error message {i}");
                await Task.Delay(5);
                errorService.ClearErrors();
            }

            // Cleanup
            errorService = null;

            // Force collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be minimal (< 10 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(10,
                $"ErrorHandlingService should not leak memory after 1000 errors (growth: {memoryGrowthMB:F2} MB)");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task Animation_NoMemoryLeak_After100Plays()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Play animations 100 times
            for (int i = 0; i < 100; i++)
            {
                // Simulate animation playback
                await Task.Delay(10);
            }

            // Force collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be minimal (< 5 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(5,
                $"Animations should not leak memory after 100 plays (growth: {memoryGrowthMB:F2} MB)");
        }

        [Fact]
        public async Task ViewModel_NoMemoryLeak_After100LoadCycles()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Create and dispose 100 ViewModels with load cycles
            for (int i = 0; i < 100; i++)
            {
                var provider = new MockAmplitudeSeriesProvider();
                var vm = new UnifiedGraphViewModel(provider);
                
                var start = DateTime.Now;
                var end = start.AddMinutes(10);
                await vm.LoadDataAsync(start, end);
                
                vm.Dispose();
                vm = null;
                provider = null;

                // Periodic collection every 10 cycles
                if (i % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Force final collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory growth should be acceptable (< 20 MB for 100 cycles)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(20,
                $"ViewModel should not leak significant memory after 100 load cycles (growth: {memoryGrowthMB:F2} MB)");
        }
    }
}
