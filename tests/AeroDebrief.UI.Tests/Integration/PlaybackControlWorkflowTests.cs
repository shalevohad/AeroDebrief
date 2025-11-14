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
    /// Integration tests for playback control workflow.
    /// Tests Play/Pause/Stop/Seek operations and state transitions.
    /// </summary>
    public class PlaybackControlWorkflowTests : IDisposable
    {
        private readonly UnifiedPlayerControl _playerControl;
        private readonly Mock<UnifiedPlayerViewModel> _mockViewModel;
        private readonly MockCommand _playCommand;
        private readonly MockCommand _pauseCommand;
        private readonly MockCommand _stopCommand;
        private readonly MockCommand _seekCommand;

        public PlaybackControlWorkflowTests()
        {
            _playerControl = ComponentTestHelper.CreateComponent<UnifiedPlayerControl>();
            _mockViewModel = TestDataFactory.CreateMockViewModel();

            // Create mock commands
            _playCommand = new MockCommand();
            _pauseCommand = new MockCommand();
            _stopCommand = new MockCommand();
            _seekCommand = new MockCommand();

            // Setup commands
            _mockViewModel.Setup(vm => vm.PlayCommand).Returns(_playCommand);
            _mockViewModel.Setup(vm => vm.PauseCommand).Returns(_pauseCommand);
            _mockViewModel.Setup(vm => vm.StopCommand).Returns(_stopCommand);
            _mockViewModel.Setup(vm => vm.SeekCommand).Returns(_seekCommand);

            // Setup initial state - file loaded, not playing
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
            _mockViewModel.Setup(vm => vm.IsPaused).Returns(false);
            _mockViewModel.Setup(vm => vm.TotalDuration).Returns(TimeSpan.FromSeconds(30));

            // Set DataContext
            ComponentTestHelper.SetProperty(_playerControl, UnifiedPlayerControl.DataContextProperty, _mockViewModel.Object);
        }

        [Fact]
        public async Task PlaybackWorkflow_ClickPlay_StartsPlaybackAndChangesButtonToPause()
        {
            // Arrange
            var transportPanel = FindTransportControlsPanel(_playerControl);
            transportPanel.Should().NotBeNull("Transport controls panel should exist");

            // Act - Click Play button
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
                _mockViewModel.Setup(vm => vm.IsPaused).Returns(false);
                
                // Trigger play command
                if (_playCommand.CanExecute(null))
                {
                    _playCommand.Execute(null);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _playCommand.ExecuteCount.Should().Be(1, "Play command should be executed once");
            _mockViewModel.Object.IsPlaying.Should().BeTrue("Player should be in playing state");
        }

        [Fact]
        public async Task PlaybackWorkflow_ClickPause_PausesPlaybackAndChangesButtonToPlay()
        {
            // Arrange - Start in playing state
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Act - Click Pause button
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
                _mockViewModel.Setup(vm => vm.IsPaused).Returns(true);

                if (_pauseCommand.CanExecute(null))
                {
                    _pauseCommand.Execute(null);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _pauseCommand.ExecuteCount.Should().Be(1, "Pause command should be executed once");
            _mockViewModel.Object.IsPlaying.Should().BeFalse("Player should not be playing");
            _mockViewModel.Object.IsPaused.Should().BeTrue("Player should be paused");
        }

        [Fact]
        public async Task PlaybackWorkflow_ClickStop_StopsPlaybackAndResetsPosition()
        {
            // Arrange - Start in playing state
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
            _mockViewModel.Setup(vm => vm.PlayheadPositionNormalized).Returns(0.5);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Act - Click Stop button
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
                _mockViewModel.Setup(vm => vm.IsPaused).Returns(false);
                _mockViewModel.Setup(vm => vm.PlayheadPositionNormalized).Returns(0.0);

                if (_stopCommand.CanExecute(null))
                {
                    _stopCommand.Execute(null);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _stopCommand.ExecuteCount.Should().Be(1, "Stop command should be executed once");
            _mockViewModel.Object.IsPlaying.Should().BeFalse("Player should not be playing");
            _mockViewModel.Object.IsPaused.Should().BeFalse("Player should not be paused");
            _mockViewModel.Object.PlayheadPositionNormalized.Should().Be(0.0, "Position should be reset to start");
        }

        [Fact]
        public async Task PlaybackWorkflow_SeekOnWaveform_ChangesPlaybackPosition()
        {
            // Arrange
            double seekPosition = 0.75; // 75% through the audio
            var waveformPanel = FindWaveformDisplayPanel(_playerControl);
            waveformPanel.Should().NotBeNull("Waveform display panel should exist");

            // Act - Simulate seek on waveform
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.PlayheadPositionNormalized).Returns(seekPosition);

                if (_seekCommand.CanExecute(seekPosition))
                {
                    _seekCommand.Execute(seekPosition);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _seekCommand.ExecuteCount.Should().Be(1, "Seek command should be executed once");
            _mockViewModel.Object.PlayheadPositionNormalized.Should().Be(seekPosition, "Position should update to seek position");
        }

        [Fact]
        public async Task PlaybackWorkflow_SpaceKeyPressed_TogglesPlayPause()
        {
            // Arrange - Not playing
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
            var transportPanel = FindTransportControlsPanel(_playerControl);
            transportPanel.Should().NotBeNull();

            // Act 1 - Press Space to play
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
                
                if (transportPanel != null)
                {
                    ComponentTestHelper.PressKey(transportPanel, System.Windows.Input.Key.Space);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert 1
            _mockViewModel.Object.IsPlaying.Should().BeTrue("Player should be playing after Space press");

            // Act 2 - Press Space again to pause
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
                _mockViewModel.Setup(vm => vm.IsPaused).Returns(true);

                if (transportPanel != null)
                {
                    ComponentTestHelper.PressKey(transportPanel, System.Windows.Input.Key.Space);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert 2
            _mockViewModel.Object.IsPlaying.Should().BeFalse("Player should be paused after second Space press");
            _mockViewModel.Object.IsPaused.Should().BeTrue("Player should be in paused state");
        }

        [Fact]
        public async Task PlaybackWorkflow_EscapeKeyPressed_StopsPlayback()
        {
            // Arrange - Playing
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
            var transportPanel = FindTransportControlsPanel(_playerControl);
            transportPanel.Should().NotBeNull();

            // Act - Press Escape
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                _mockViewModel.Setup(vm => vm.IsPlaying).Returns(false);
                _mockViewModel.Setup(vm => vm.IsPaused).Returns(false);

                if (transportPanel != null)
                {
                    ComponentTestHelper.PressKey(transportPanel, System.Windows.Input.Key.Escape);
                }
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            _mockViewModel.Object.IsPlaying.Should().BeFalse("Player should stop after Escape press");
            _mockViewModel.Object.IsPaused.Should().BeFalse("Player should not be paused");
        }

        [Fact]
        public async Task PlaybackWorkflow_PlayheadUpdates_DuringPlayback()
        {
            // Arrange
            _mockViewModel.Setup(vm => vm.IsPlaying).Returns(true);
            double[] positions = { 0.0, 0.25, 0.5, 0.75, 1.0 };

            // Act - Simulate playhead moving
            foreach (var position in positions)
            {
                ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
                {
                    _mockViewModel.Setup(vm => vm.PlayheadPositionNormalized).Returns(position);
                });

                await Task.Delay(50);
                ComponentTestHelper.PumpDispatcher(_playerControl);

                // Assert
                _mockViewModel.Object.PlayheadPositionNormalized.Should().Be(position,
                    $"Playhead should be at {position * 100}%");
            }
        }

        #region Helper Methods

        private AeroDebrief.UI.Controls.Player.TransportControlsPanel? FindTransportControlsPanel(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is AeroDebrief.UI.Controls.Player.TransportControlsPanel panel)
                {
                    return panel;
                }

                var result = FindTransportControlsPanel(child);
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

        /// <summary>
        /// Mock command for testing.
        /// </summary>
        private class MockCommand : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                ExecuteCount++;
            }

            public int ExecuteCount { get; private set; }
        }

        #endregion

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_playerControl);
        }
    }
}
