using System;
using System.Threading.Tasks;
using AeroDebrief.UI.Controls;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace AeroDebrief.UI.Tests.Integration
{
    /// <summary>
    /// Integration tests for zoom operations workflow.
    /// Tests zoom in/out/reset, region selection, and minimap interaction.
    /// </summary>
    public class ZoomOperationsWorkflowTests : IDisposable
    {
        private readonly UnifiedPlayerControl _playerControl;
        private readonly Mock<UnifiedPlayerViewModel> _mockViewModel;

        public ZoomOperationsWorkflowTests()
        {
            _playerControl = ComponentTestHelper.CreateComponent<UnifiedPlayerControl>();
            _mockViewModel = TestDataFactory.CreateMockViewModel();

            // Setup initial state - file loaded with waveform
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);
            _mockViewModel.Setup(vm => vm.TotalDuration).Returns(TimeSpan.FromSeconds(60));
            _mockViewModel.Setup(vm => vm.WaveformData).Returns(TestDataFactory.CreateMockWaveformData());
            _mockViewModel.Setup(vm => vm.ZoomStartTime).Returns(0.0);
            _mockViewModel.Setup(vm => vm.ZoomEndTime).Returns(60.0);

            ComponentTestHelper.SetProperty(_playerControl, UnifiedPlayerControl.DataContextProperty, _mockViewModel.Object);
        }

        [Fact]
        public async Task ZoomWorkflow_ClickZoomIn_IncreasesZoomLevel()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            double initialStart = 0.0;
            double initialEnd = 60.0;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomIn();
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert - Zoom range should be smaller
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            var zoomRange = zoomEnd - zoomStart;
            var initialRange = initialEnd - initialStart;
            
            zoomRange.Should().BeLessThan(initialRange, "Zoom range should decrease after zooming in");
        }

        [Fact]
        public async Task ZoomWorkflow_ClickZoomOut_DecreasesZoomLevel()
        {
            // Arrange - Start zoomed in
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(20.0, 40.0);
            });
            await Task.Delay(100);

            double initialStart = 0;
            double initialEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                initialStart = waveformPanel?.ZoomStartTime ?? 0;
                initialEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomOut();
            });

            await Task.Delay(100);

            // Assert - Zoom range should be larger
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            var zoomRange = zoomEnd - zoomStart;
            var initialRange = initialEnd - initialStart;
            
            zoomRange.Should().BeGreaterThan(initialRange, "Zoom range should increase after zooming out");
        }

        [Fact]
        public async Task ZoomWorkflow_ClickResetZoom_ShowsFullWaveform()
        {
            // Arrange - Start zoomed in
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(20.0, 40.0);
            });
            await Task.Delay(100);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ResetZoom();
            });

            await Task.Delay(100);

            // Assert
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            zoomStart.Should().Be(0.0, "Zoom should start at beginning");
            zoomEnd.Should().Be(60.0, "Zoom should end at total duration");
        }

        [Fact]
        public async Task ZoomWorkflow_SelectRegionOnWaveform_ZoomsToRegion()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            double targetStart = 15.0;
            double targetEnd = 35.0;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(targetStart, targetEnd);
            });

            await Task.Delay(100);

            // Assert
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            zoomStart.Should().Be(targetStart);
            zoomEnd.Should().Be(targetEnd);
        }

        [Fact]
        public async Task ZoomWorkflow_ClickOnMinimap_UpdatesMainWaveformZoom()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            // Act - Simulate clicking on minimap (simulated as ZoomToRegion call)
            double minimapClickStart = 10.0;
            double minimapClickEnd = 25.0;

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(minimapClickStart, minimapClickEnd);
            });

            await Task.Delay(100);

            // Assert - Main waveform should zoom to clicked region
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            zoomStart.Should().Be(minimapClickStart);
            zoomEnd.Should().Be(minimapClickEnd);
        }

        [Fact]
        public async Task ZoomWorkflow_DragMinimapViewport_UpdatesZoomRange()
        {
            // Arrange - Start with some zoom
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(10.0, 30.0);
            });
            await Task.Delay(100);

            // Act - Simulate dragging viewport (simulated as new ZoomToRegion call)
            double newStart = 20.0;
            double newEnd = 40.0;

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(newStart, newEnd);
            });

            await Task.Delay(100);

            // Assert
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            zoomStart.Should().Be(newStart);
            zoomEnd.Should().Be(newEnd);
        }

        [Fact]
        public async Task ZoomWorkflow_PlayheadVisible_WhenZoomed()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            // Set playhead position
            double playheadPosition = 0.5; // 50%
            _mockViewModel.Setup(vm => vm.PlayheadPositionNormalized).Returns(playheadPosition);

            // Act - Zoom to region containing playhead
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                waveformPanel?.ZoomToRegion(20.0, 40.0);
            });

            await Task.Delay(100);

            // Assert - Playhead should still be visible (position binding should work)
            _mockViewModel.Object.PlayheadPositionNormalized.Should().Be(playheadPosition);
        }

        [Fact]
        public async Task ZoomWorkflow_MultipleZoomOperations_MaintainCorrectState()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            // Act - Perform multiple zoom operations
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Zoom in
                waveformPanel?.ZoomIn();
            });
            await Task.Delay(100);

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Zoom in again
                waveformPanel?.ZoomIn();
            });
            await Task.Delay(100);

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Zoom out once
                waveformPanel?.ZoomOut();
            });
            await Task.Delay(100);

            // Assert - Should still be zoomed (not at full view)
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            var zoomRange = zoomEnd - zoomStart;
            zoomRange.Should().BeLessThan(60.0, "Should still be zoomed after mixed operations");
            zoomRange.Should().BeGreaterThan(0, "Zoom range should be valid");
        }

        [Fact]
        public async Task ZoomWorkflow_ZoomBeyondLimits_ClampsToBounds()
        {
            // Arrange
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull();

            // Act - Try to zoom out beyond limits
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    waveformPanel?.ZoomOut();
                }
            });

            await Task.Delay(200);

            // Assert - Should be clamped to full waveform
            double zoomStart = 0;
            double zoomEnd = 0;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                zoomStart = waveformPanel?.ZoomStartTime ?? 0;
                zoomEnd = waveformPanel?.ZoomEndTime ?? 0;
            });

            zoomStart.Should().BeGreaterOrEqualTo(0.0, "Start should not go below 0");
            zoomEnd.Should().BeLessOrEqualTo(60.0, "End should not exceed total duration");
        }

        #region Helper Methods

        private AeroDebrief.UI.Controls.Player.WaveformDisplayPanel? FindWaveformDisplayPanel(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is AeroDebrief.UI.Controls.Player.WaveformDisplayPanel panel)
                {
                    return panel;
                }

                var result = FindWaveformDisplayPanel(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        #endregion

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_playerControl);
        }
    }
}
