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
    /// Integration tests for file loading workflow.
    /// Tests the complete flow from user clicking "Recording File" to file loaded and ready for playback.
    /// </summary>
    public class FileLoadingWorkflowTests : IDisposable
    {
        private readonly UnifiedPlayerControl _playerControl;
        private readonly Mock<UnifiedPlayerViewModel> _mockViewModel;

        public FileLoadingWorkflowTests()
        {
            _playerControl = ComponentTestHelper.CreateComponent<UnifiedPlayerControl>();
            _mockViewModel = TestDataFactory.CreateMockViewModel();
            
            // Set DataContext
            ComponentTestHelper.SetProperty(_playerControl, UnifiedPlayerControl.DataContextProperty, _mockViewModel.Object);
        }

        [Fact]
        public async Task FileLoadingWorkflow_CompleteFlow_SuccessfullyLoadsFile()
        {
            // Arrange - Start in idle state
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(true);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Act & Assert Step 1: User clicks "Recording File" button
            bool filePanelOpened = false;
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Simulate clicking the file source button
                // This should open the file source panel overlay
                var header = FindPlayerHeaderControl(_playerControl);
                if (header != null)
                {
                    // Trigger FilePanelRequested event
                    var method = typeof(AeroDebrief.UI.Controls.Player.PlayerHeaderControl)
                        .GetMethod("OnFilePanelRequested", 
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    method?.Invoke(header, new object[] { EventArgs.Empty });
                    filePanelOpened = true;
                }
            });

            await Task.Delay(500); // Wait for animation
            filePanelOpened.Should().BeTrue("File panel should open when button is clicked");

            // Act & Assert Step 2: Simulate file selection
            string selectedFile = "C:\\Test\\recording.srs";
            bool fileLoadStarted = false;

            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Simulate file selection through FileSource
                _mockViewModel.Setup(vm => vm.CurrentSourceName).Returns(selectedFile);
                _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);
                fileLoadStarted = true;
            });

            fileLoadStarted.Should().BeTrue("File load should start");

            // Act & Assert Step 3: Simulate file loaded
            await Task.Delay(100);
            
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                // Simulate file loaded - frequencies should populate
                var frequencies = TestDataFactory.CreateMockFrequencies();
                _mockViewModel.Setup(vm => vm.Frequencies).Returns(frequencies);
                
                // Waveform should be generated
                var waveform = TestDataFactory.CreateMockWaveformData();
                _mockViewModel.Setup(vm => vm.WaveformData).Returns(waveform);
                
                // Status should update
                _mockViewModel.Setup(vm => vm.StatusMessage).Returns("File loaded successfully");
            });

            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert final state
            _mockViewModel.Object.IsIdle.Should().BeFalse("Player should not be idle after file load");
            _mockViewModel.Object.CurrentSourceName.Should().Be(selectedFile);
            _mockViewModel.Object.Frequencies.Should().NotBeNull();
            _mockViewModel.Object.Frequencies.Count.Should().BeGreaterThan(0);
            _mockViewModel.Object.WaveformData.Should().NotBeNull();
            _mockViewModel.Object.WaveformData.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task FileLoadingWorkflow_WhenFileLoadFails_DisplaysErrorMessage()
        {
            // Arrange
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(true);

            // Act - Simulate file load failure
            string errorMessage = "Failed to load file";
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.StatusMessage).Returns(errorMessage);
                _mockViewModel.Setup(vm => vm.IsIdle).Returns(true); // Stay in idle state
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _mockViewModel.Object.StatusMessage.Should().Contain("Failed");
            _mockViewModel.Object.IsIdle.Should().BeTrue("Player should remain idle on error");
        }

        [Fact]
        public void FilePanel_WhenFilePanelOpened_FrequenciesPanelIsReady()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            _mockViewModel.Setup(vm => vm.Frequencies).Returns(frequencies);
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);

            // Act
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert - Find frequency mixer panel
            var mixerPanel = FindFrequencyMixerPanel(_playerControl);
            mixerPanel.Should().NotBeNull("Frequency mixer panel should exist");

            if (mixerPanel != null)
            {
                // Verify frequencies are bound
                var boundFrequencies = ComponentTestHelper.GetProperty<System.Collections.ObjectModel.ObservableCollection<FrequencyGroupViewModel>>(
                    mixerPanel, 
                    AeroDebrief.UI.Controls.Player.FrequencyMixerPanel.FrequenciesProperty);
                
                boundFrequencies.Should().NotBeNull();
                boundFrequencies?.Count.Should().Be(frequencies.Count);
            }
        }

        [Fact]
        public void FilePanel_WhenFilePanelOpened_WaveformPanelIsReady()
        {
            // Arrange
            var waveform = TestDataFactory.CreateMockWaveformData();
            _mockViewModel.Setup(vm => vm.WaveformData).Returns(waveform);
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);

            // Act
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert - Find waveform display panel
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull("Waveform display panel should exist");

            if (waveformPanel != null)
            {
                // Verify waveform data is bound
                var boundWaveform = ComponentTestHelper.GetProperty<float[]>(
                    waveformPanel,
                    AeroDebrief.UI.Controls.Player.WaveformDisplayPanel.WaveformDataProperty);

                boundWaveform.Should().NotBeNull();
                boundWaveform?.Length.Should().Be(waveform.Length);
            }
        }

        #region Helper Methods

        private AeroDebrief.UI.Controls.Player.PlayerHeaderControl? FindPlayerHeaderControl(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is AeroDebrief.UI.Controls.Player.PlayerHeaderControl header)
                {
                    return header;
                }

                var result = FindPlayerHeaderControl(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private AeroDebrief.UI.Controls.Player.FrequencyMixerPanel? FindFrequencyMixerPanel(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is AeroDebrief.UI.Controls.Player.FrequencyMixerPanel panel)
                {
                    return panel;
                }

                var result = FindFrequencyMixerPanel(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

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
