using Xunit;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Services.Visualization.Graphs;
using AeroDebrief.Tests.TestHelpers;
using NLog;

namespace AeroDebrief.Tests.Phase9.Regression
{
    /// <summary>
    /// End-to-end regression tests for complete user workflows.
    /// Ensures the entire application flow works correctly after Phase 9.
    /// </summary>
    public class EndToEndRegressionTests
    {
        private readonly Logger _logger;

        public EndToEndRegressionTests()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        [Fact]
        public async Task E2E_Load_Display_Zoom_Play()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(1);

            // Act & Assert - Complete user workflow
            
            // Step 1: Load file
            await vm.LoadDataAsync(start, end);
            vm.Series.Should().NotBeNull("Step 1: Data should load");
            vm.IsLoadingTiles.Should().BeFalse("Step 1: Loading should complete");

            // Step 2: Display chart
            vm.ViewportStart.Should().Be(start, "Step 2: Viewport should show data");
            vm.ViewportEnd.Should().Be(end, "Step 2: Viewport should show data");

            // Step 3: Zoom in
            var initialDuration = vm.ViewportDuration;
            vm.ZoomIn(0.5);
            vm.ViewportDuration.Should().BeLessThan(initialDuration, 
                "Step 3: Zoom should reduce viewport");

            // Step 4: Play
            vm.PlayheadTime = start.AddMinutes(10);
            vm.IsPlaying = true;
            vm.PlayheadTime.Should().Be(start.AddMinutes(10), 
                "Step 4: Playhead should be positioned");
            vm.IsPlaying.Should().BeTrue("Step 4: Playback should be active");

            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task E2E_Load_Error_Retry_Success()
        {
            // Arrange
            var errorService = new ErrorHandlingService(_logger);
            var failingProvider = new FailingMockAmplitudeSeriesProvider();
            var successProvider = new MockAmplitudeSeriesProvider();
            
            var start = DateTime.Now;
            var end = start.AddMinutes(30);

            // Act & Assert - Error recovery workflow

            // Step 1: Try to load (fails)
            UnifiedGraphViewModel vm = null;
            Exception caughtException = null;
            try
            {
                vm = new UnifiedGraphViewModel(failingProvider, errorHandler: errorService);
                await vm.LoadDataAsync(start, end);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            caughtException.Should().NotBeNull("Step 1: Load should fail");

            // Step 2: Error is displayed
            // (In real app, error would be shown via errorService)
            await Task.Delay(50);

            // Step 3: User retries with correct provider
            vm?.Dispose();
            vm = new UnifiedGraphViewModel(successProvider, errorHandler: errorService);
            await vm.LoadDataAsync(start, end);

            // Step 4: Success
            vm.IsLoadingTiles.Should().BeFalse("Step 4: Retry should succeed");

            // Cleanup
            if (errorService.IsVisible)
            {
                errorService.ClearErrors();
            }
            vm?.Dispose();
        }

        [Fact]
        public async Task E2E_Load_Cancel_Success()
        {
            // Arrange
            var provider = new SlowMockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(1);

            // Act & Assert - Cancellation workflow

            // Step 1: Start loading
            var loadTask = vm.LoadDataAsync(start, end);
            await Task.Delay(100); // Let it start

            // Step 2: User clicks cancel
            if (vm.IsLoadingTiles && vm.CancelLoadingCommand.CanExecute(null))
            {
                vm.CancelLoadingCommand.Execute(null);
            }

            // Wait a moment for cancellation
            await Task.Delay(300);

            // Step 3: Loading stops
            vm.IsLoadingTiles.Should().BeFalse("Step 3: Loading should be cancelled");

            // Step 4: User can retry
            var fastProvider = new MockAmplitudeSeriesProvider();
            vm.Dispose();
            vm = new UnifiedGraphViewModel(fastProvider);
            await vm.LoadDataAsync(start, end);

            vm.IsLoadingTiles.Should().BeFalse("Step 4: Retry should succeed");

            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task E2E_LongSession_NoIssues()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var errorService = new ErrorHandlingService(_logger);
            
            var start = DateTime.Now;
            var end = start.AddHours(2);

            // Act - Simulate long session with multiple operations
            try
            {
                // Load data
                await vm.LoadDataAsync(start, end);

                // Perform multiple zoom/pan operations
                for (int i = 0; i < 20; i++)
                {
                    vm.ZoomIn(0.8);
                    await Task.Delay(50);
                    vm.Pan(TimeSpan.FromMinutes(5));
                    await Task.Delay(50);
                }

                // Reset and zoom out
                vm.ResetViewport();
                for (int i = 0; i < 10; i++)
                {
                    vm.ZoomOut(1.2);
                    await Task.Delay(50);
                }

                // Playback simulation
                vm.IsPlaying = true;
                for (int i = 0; i < 30; i++)
                {
                    vm.PlayheadTime = start.AddMinutes(i * 2);
                    await Task.Delay(50);
                }
                vm.IsPlaying = false;

                // Toggle performance stats multiple times
                for (int i = 0; i < 5; i++)
                {
                    vm.ShowPerformanceStats = !vm.ShowPerformanceStats;
                    await Task.Delay(100);
                }

                // Show and dismiss errors
                for (int i = 0; i < 5; i++)
                {
                    var errorTask = errorService.ShowErrorAsync("Test Error", "Test message");
                    await Task.Delay(100);
                    errorService.ClearErrors();
                    await Task.Delay(100);
                }

                // Assert - No exceptions during long session
                vm.Should().NotBeNull("Long session should complete without crashes");
                
                // Memory should be reasonable
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                memoryMB.Should().BeLessThan(1024, 
                    $"Memory should remain reasonable after long session (actual: {memoryMB:F2} MB)");
            }
            finally
            {
                // Cleanup
                if (errorService.IsVisible)
                {
                    errorService.ClearErrors();
                }
                vm.Dispose();
            }
        }
    }
}
