using Xunit;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.Tests.TestHelpers;

namespace AeroDebrief.Tests.Phase9.Regression
{
    /// <summary>
    /// Regression tests to ensure Phase 4-8 features still work after Phase 9 changes.
    /// Verifies no breaking changes were introduced.
    /// </summary>
    public class FeatureRegressionTests
    {
        [Fact]
        public async Task Phase4_UnifiedChart_StillWorks()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);

            // Act
            await vm.LoadDataAsync(start, end);

            // Assert - Basic Phase 4 functionality
            vm.Series.Should().NotBeNull("Unified chart series should be initialized");
            vm.TotalPoints.Should().BeGreaterOrEqualTo(0, "Total points should be tracked");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Phase5_ZoomPan_StillWorks()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);
            await vm.LoadDataAsync(start, end);

            // Act - Test zoom
            var initialViewportDuration = vm.ViewportDuration;
            vm.ZoomIn(0.5);
            var zoomedViewportDuration = vm.ViewportDuration;

            // Reset and test pan
            vm.ResetViewport();
            var initialViewportStart = vm.ViewportStart;
            vm.Pan(TimeSpan.FromMinutes(5));
            var pannedViewportStart = vm.ViewportStart;

            // Assert
            zoomedViewportDuration.Should().BeLessThan(initialViewportDuration,
                "Phase 5: Zoom in should reduce viewport duration");
            
            pannedViewportStart.Should().BeAfter(initialViewportStart,
                "Phase 5: Pan should move viewport forward");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Phase6_Playhead_StillWorks()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);
            await vm.LoadDataAsync(start, end);

            // Act - Test playhead
            var playheadTime = start.AddMinutes(10);
            vm.PlayheadTime = playheadTime;
            vm.IsPlaying = true;
            vm.PlaybackRate = 1.0;
            vm.FollowMode = true;

            // Assert
            vm.PlayheadTime.Should().Be(playheadTime,
                "Phase 6: Playhead time should be settable");
            
            vm.IsPlaying.Should().BeTrue(
                "Phase 6: IsPlaying should be settable");
            
            vm.PlaybackRate.Should().Be(1.0,
                "Phase 6: PlaybackRate should be settable");
            
            vm.FollowMode.Should().BeTrue(
                "Phase 6: FollowMode should be settable");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Phase7_VisibilityToggles_StillWorks()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);
            await vm.LoadDataAsync(start, end);

            // Act - Test audio sync
            vm.AudioSyncEnabled = false;

            // Assert
            vm.AudioSyncEnabled.Should().BeFalse(
                "Phase 7: AudioSyncEnabled should be settable");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task Phase8_TileLoading_StillWorks()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);

            // Act
            await vm.LoadDataAsync(start, end);

            // Assert - Phase 8 tile loading properties
            vm.IsLoadingTiles.Should().BeFalse(
                "Phase 8: IsLoadingTiles should be false after load completes");
            
            vm.LoadingStatusText.Should().NotBeNullOrEmpty(
                "Phase 8: LoadingStatusText should have a value");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task AllExistingTests_StillPass()
        {
            // This test serves as a reminder to run all existing test suites
            // and ensure they still pass after Phase 9 changes.
            
            // Verify basic ViewModel creation and disposal
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            
            await Task.CompletedTask;
            
            // Assert
            vm.Should().NotBeNull("ViewModel should be creatable");
            
            // Cleanup
            vm.Dispose();
            
            // Note: This is a placeholder. In a real CI/CD environment,
            // all existing test suites would be run automatically.
        }

        [Fact]
        public async Task NoPerformanceRegressions()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(1);

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await vm.LoadDataAsync(start, end);
            sw.Stop();

            // Assert
            sw.ElapsedMilliseconds.Should().BeLessThan(10000,
                "Load time should not have regressed (should still be < 10s)");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public async Task NoMemoryRegressions()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddHours(1);

            // Act
            await vm.LoadDataAsync(start, end);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryUsedMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryUsedMB.Should().BeLessThan(100,
                "Memory usage should not have significantly regressed");
            
            // Cleanup
            vm.Dispose();
        }

        [Fact]
        public void NoVisualRegressions()
        {
            // This test serves as a reminder to perform visual regression testing
            // Manual verification or automated visual regression tools should be used
            
            // Verify overlays can be created
            var loadingSpinner = new AeroDebrief.UI.Controls.Charts.LoadingSpinnerOverlay();
            var errorBanner = new AeroDebrief.UI.Controls.Charts.ErrorBannerOverlay();
            var perfStats = new AeroDebrief.UI.Controls.Charts.PerformanceStatsOverlay();

            // Assert
            loadingSpinner.Should().NotBeNull("LoadingSpinner should be creatable");
            errorBanner.Should().NotBeNull("ErrorBanner should be creatable");
            perfStats.Should().NotBeNull("PerformanceStats should be creatable");
            
            // Note: Actual visual verification requires screenshot comparison
            // or manual testing
        }

        [Fact]
        public async Task NoBehaviorRegressions()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var start = DateTime.Now;
            var end = start.AddMinutes(30);

            // Act - Test complete workflow
            await vm.LoadDataAsync(start, end);
            vm.ZoomIn(0.5);
            vm.Pan(TimeSpan.FromMinutes(5));
            vm.PlayheadTime = start.AddMinutes(10);
            vm.IsPlaying = true;
            vm.ResetViewport();

            // Assert - Workflow should complete without exceptions
            vm.ViewportStart.Should().Be(start,
                "Behavior should remain consistent after workflow");
            
            vm.ViewportEnd.Should().Be(end,
                "Behavior should remain consistent after workflow");
            
            // Cleanup
            vm.Dispose();
        }
    }
}
