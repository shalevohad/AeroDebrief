using Xunit;
using FluentAssertions;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services;
using AeroDebrief.Tests.TestHelpers;
using NLog;

namespace AeroDebrief.Tests.Phase9.Performance
{
    /// <summary>
    /// Performance gate tests for Phase 9 features.
    /// Ensures all performance requirements are met.
    /// </summary>
    public class PerformanceGatesTests
    {
        private readonly Logger _logger;

        public PerformanceGatesTests()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        [Fact]
        public async Task Gate_LoadTime_LessThan10Seconds()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(1); // 1 hour of data
            var sw = Stopwatch.StartNew();

            // Act
            await vm.LoadDataAsync(start, end);
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(10000,
                $"Load time should be less than 10 seconds (actual: {sw.ElapsedMilliseconds}ms)");

            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Gate_MemoryUsage_LessThan1GB()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(2); // 2 hours of data

            // Act
            await vm.LoadDataAsync(start, end);
            
            // Force garbage collection to get accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBytes = GC.GetTotalMemory(false);
            var memoryMB = memoryBytes / (1024.0 * 1024.0);

            // Assert
            memoryMB.Should().BeLessThan(1024,
                $"Memory usage should be less than 1 GB (actual: {memoryMB:F2} MB)");

            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Gate_IdleCPU_LessThan2Percent()
        {
            // Arrange
            var process = Process.GetCurrentProcess();
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            // Load data first
            await vm.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(30));
            
            // Let system settle
            await Task.Delay(1000);

            var startCpuTime = process.TotalProcessorTime;
            var sw = Stopwatch.StartNew();

            // Act - Idle period (no operations)
            await Task.Delay(2000);
            
            sw.Stop();
            var endCpuTime = process.TotalProcessorTime;

            // Assert
            var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalMsPassed = sw.ElapsedMilliseconds;
            var cpuUsagePercent = (cpuUsedMs / totalMsPassed) * 100.0 / Environment.ProcessorCount;

            cpuUsagePercent.Should().BeLessThan(2.0,
                $"Idle CPU usage should be less than 2% (actual: {cpuUsagePercent:F2}%)");

            // Cleanup
            vm.Dispose();
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task Gate_AnimationFPS_GreaterThan58()
        {
            // Arrange
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act - Simulate 1 second of animation
            System.Windows.Media.CompositionTarget.Rendering += (s, e) => frameCount++;
            await Task.Delay(1000);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(58,
                $"Animation FPS should be at least 58 (actual: {actualFPS:F1})");
        }

        [Fact]
        public async Task Gate_ErrorHandling_LessThan10ms()
        {
            // Arrange
            var errorService = new ErrorHandlingService(_logger);
            var sw = Stopwatch.StartNew();

            // Act - Report error and measure time
            var task = errorService.ShowErrorAsync("Test Error", "Test message");
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(10,
                $"Error handling should take less than 10ms (actual: {sw.ElapsedMilliseconds}ms)");

            // Cleanup
            await Task.Delay(100);
            errorService.ClearErrors();
        }

        [Fact]
        public void Gate_PerformanceStatsUpdate_LessThan5ms()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            vm.ShowPerformanceStats = true;

            var sw = Stopwatch.StartNew();

            // Act - Simulate performance stats update
            var memory = vm.MemoryUsageMB;
            var fps = vm.CurrentFPS;
            var loadTime = vm.LastLoadTimeMs;
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(5,
                $"Performance stats update should take less than 5ms (actual: {sw.ElapsedMilliseconds}ms)");

            // Cleanup
            vm.Dispose();
        }

        [Fact(Skip = "Requires WPF dispatcher context")]
        public async Task Gate_OverlayShow_LessThan50ms()
        {
            // Arrange
            var errorService = new ErrorHandlingService(_logger);
            var sw = Stopwatch.StartNew();

            // Act
            var task = errorService.ShowErrorAsync("Test", "Message");
            await Task.Delay(100); // Allow dispatcher to process
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(50,
                $"Overlay show should take less than 50ms (actual: {sw.ElapsedMilliseconds}ms)");

            // Cleanup
            errorService.ClearErrors();
        }

        [Fact(Skip = "Requires WPF dispatcher context")]
        public async Task Gate_OverlayHide_LessThan50ms()
        {
            // Arrange
            var errorService = new ErrorHandlingService(_logger);
            await errorService.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            var sw = Stopwatch.StartNew();

            // Act
            errorService.ClearErrors();
            await Task.Delay(100); // Allow dispatcher to process
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(50,
                $"Overlay hide should take less than 50ms (actual: {sw.ElapsedMilliseconds}ms)");
        }
    }
}
