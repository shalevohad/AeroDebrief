using System;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.UI.Tests.Components
{
    /// <summary>
    /// Unit tests for WaveformDisplayPanel component.
    /// Tests zoom functionality, GPU/CPU badge display, seek operations, and size change handling.
    /// </summary>
    public class WaveformDisplayPanelTests : IDisposable
    {
        private readonly WaveformDisplayPanel _control;

        public WaveformDisplayPanelTests()
        {
            _control = ComponentTestHelper.CreateComponent<WaveformDisplayPanel>();
        }

        [Fact]
        public void WaveformData_WhenSet_UpdatesDisplay()
        {
            // Arrange
            var testWaveform = TestDataFactory.CreateMockWaveformData(1000, true);

            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.WaveformDataProperty, testWaveform);

            // Assert
            var result = ComponentTestHelper.GetProperty<float[]>(_control, WaveformDisplayPanel.WaveformDataProperty);
            result.Should().NotBeNull();
            result.Length.Should().Be(testWaveform.Length);
        }

        [Fact]
        public void FrequencyWaveforms_WhenSet_UpdatesLayers()
        {
            // Arrange
            var testWaveforms = TestDataFactory.CreateMockFrequencyWaveforms();

            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.FrequencyWaveformsProperty, testWaveforms);

            // Assert
            var result = ComponentTestHelper.GetProperty<Dictionary<double, AeroDebrief.UI.Controls.FrequencyWaveformData>>(
                _control, WaveformDisplayPanel.FrequencyWaveformsProperty);
            result.Should().NotBeNull();
            result.Count.Should().Be(testWaveforms.Count);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.25)]
        [InlineData(0.5)]
        [InlineData(0.75)]
        [InlineData(1.0)]
        public void PlayheadPosition_WhenSet_UpdatesPlayheadLocation(double position)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.PlayheadPositionProperty, position);

            // Assert
            var result = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.PlayheadPositionProperty);
            result.Should().Be(position);
        }

        [Fact]
        public void ZoomIn_WhenCalled_IncreasesZoomLevel()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomStartTimeProperty, 0.0);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomEndTimeProperty, 100.0);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.ZoomIn());

            // Assert
            var zoomStart = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomStartTimeProperty);
            var zoomEnd = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomEndTimeProperty);
            var zoomRange = zoomEnd - zoomStart;
            
            zoomRange.Should().BeLessThan(100.0, "Zoom range should decrease when zooming in");
        }

        [Fact]
        public void ZoomOut_WhenCalled_DecreasesZoomLevel()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomStartTimeProperty, 25.0);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomEndTimeProperty, 75.0);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.TotalDurationProperty, TimeSpan.FromSeconds(100));

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.ZoomOut());

            // Assert
            var zoomStart = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomStartTimeProperty);
            var zoomEnd = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomEndTimeProperty);
            var zoomRange = zoomEnd - zoomStart;
            
            zoomRange.Should().BeGreaterThan(50.0, "Zoom range should increase when zooming out");
        }

        [Fact]
        public void ResetZoom_WhenCalled_ShowsFullWaveform()
        {
            // Arrange
            var totalDuration = TimeSpan.FromSeconds(100);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.TotalDurationProperty, totalDuration);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomStartTimeProperty, 25.0);
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ZoomEndTimeProperty, 75.0);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.ResetZoom());

            // Assert
            var zoomStart = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomStartTimeProperty);
            var zoomEnd = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomEndTimeProperty);
            
            zoomStart.Should().Be(0.0, "Zoom should start at beginning");
            zoomEnd.Should().Be(totalDuration.TotalSeconds, "Zoom should end at total duration");
        }

        [Fact]
        public void ZoomToRegion_WhenCalled_UpdatesZoomRange()
        {
            // Arrange
            double startTime = 10.0;
            double endTime = 30.0;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.ZoomToRegion(startTime, endTime));

            // Assert
            var zoomStart = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomStartTimeProperty);
            var zoomEnd = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.ZoomEndTimeProperty);
            
            zoomStart.Should().Be(startTime);
            zoomEnd.Should().Be(endTime);
        }

        [Fact]
        public void SeekRequested_WhenWaveformClicked_RaisesEvent()
        {
            // Arrange
            double capturedPosition = -1;
            _control.SeekRequested += (sender, position) => capturedPosition = position;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Simulate waveform click by raising the event manually
                var method = typeof(WaveformDisplayPanel).GetMethod("OnSeekRequested",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_control, new object[] { 0.5 });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedPosition >= 0, TimeSpan.FromSeconds(2));
            capturedPosition.Should().Be(0.5);
        }

        [Fact]
        public void ZoomChanged_WhenZoomOperationPerformed_RaisesEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.ZoomChanged += (sender, args) => eventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.ZoomIn());

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Theory]
        [InlineData("GPU")]
        [InlineData("CPU")]
        public void EngineType_WhenSet_UpdatesBadge(string engineType)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.EngineTypeProperty, engineType);

            // Assert
            var result = ComponentTestHelper.GetProperty<string>(_control, WaveformDisplayPanel.EngineTypeProperty);
            result.Should().Be(engineType);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsUsingGpu_WhenSet_UpdatesStatusBadge(bool isUsingGpu)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.IsUsingGpuProperty, isUsingGpu);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, WaveformDisplayPanel.IsUsingGpuProperty);
            result.Should().Be(isUsingGpu);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowEngineStatus_WhenSet_TogglesBadgeVisibility(bool showStatus)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ShowEngineStatusProperty, showStatus);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, WaveformDisplayPanel.ShowEngineStatusProperty);
            result.Should().Be(showStatus);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowZoomControls_WhenSet_TogglesControlsVisibility(bool showControls)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ShowZoomControlsProperty, showControls);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, WaveformDisplayPanel.ShowZoomControlsProperty);
            result.Should().Be(showControls);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowMinimap_WhenSet_TogglesMinimapVisibility(bool showMinimap)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.ShowMinimapProperty, showMinimap);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, WaveformDisplayPanel.ShowMinimapProperty);
            result.Should().Be(showMinimap);
        }

        [Theory]
        [InlineData(50)]
        [InlineData(80)]
        [InlineData(100)]
        public void MinimapHeight_WhenSet_UpdatesMinimapSize(double height)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.MinimapHeightProperty, height);

            // Assert
            var result = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.MinimapHeightProperty);
            result.Should().Be(height);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsLoading_WhenSet_UpdatesLoadingIndicator(bool isLoading)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.IsLoadingProperty, isLoading);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, WaveformDisplayPanel.IsLoadingProperty);
            result.Should().Be(isLoading);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(1.0)]
        public void LoadingProgress_WhenSet_UpdatesProgressBar(double progress)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, WaveformDisplayPanel.LoadingProgressProperty, progress);

            // Assert
            var result = ComponentTestHelper.GetProperty<double>(_control, WaveformDisplayPanel.LoadingProgressProperty);
            result.Should().Be(progress);
        }

        [Fact]
        public void WaveformSizeChanged_WhenSizeChanges_RaisesEvent()
        {
            // Arrange
            System.Windows.SizeChangedEventArgs? capturedArgs = null;
            _control.WaveformSizeChanged += (sender, args) => capturedArgs = args;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                _control.Width = 800;
                _control.Height = 400;
                _control.UpdateLayout();
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
        }

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_control);
        }
    }
}
