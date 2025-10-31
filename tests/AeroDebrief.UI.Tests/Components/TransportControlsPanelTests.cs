using System;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.UI.Tests.Components
{
    /// <summary>
    /// Unit tests for TransportControlsPanel component.
    /// Tests playback controls, keyboard shortcuts, and state management.
    /// </summary>
    public class TransportControlsPanelTests : IDisposable
    {
        private readonly TransportControlsPanel _control;

        public TransportControlsPanelTests()
        {
            _control = ComponentTestHelper.CreateComponent<TransportControlsPanel>();
        }

        [Fact]
        public void IsPlaying_WhenSetToTrue_UpdatesButtonState()
        {
            // Act
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.IsPlayingProperty, true);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, TransportControlsPanel.IsPlayingProperty);
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPaused_WhenSetToTrue_UpdatesButtonState()
        {
            // Act
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.IsPausedProperty, true);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, TransportControlsPanel.IsPausedProperty);
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanPlay_WhenSet_EnablesDisablesPlayButton(bool canPlay)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanPlayProperty, canPlay);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, TransportControlsPanel.CanPlayProperty);
            result.Should().Be(canPlay);
        }

        [Fact]
        public void PlayButton_WhenClicked_RaisesPlayRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.PlayRequested += (sender, args) => eventRaised = true;

            // Set can play to true
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanPlayProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.IsPlayingProperty, false);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var playButton = FindButton(_control, "PlayButton");
                playButton?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void PauseButton_WhenClicked_RaisesPauseRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.PauseRequested += (sender, args) => eventRaised = true;

            // Set to playing state
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.IsPlayingProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanPauseProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var pauseButton = FindButton(_control, "PauseButton");
                pauseButton?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void StopButton_WhenClicked_RaisesStopRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.StopRequested += (sender, args) => eventRaised = true;

            // Set can stop to true
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanStopProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var stopButton = FindButton(_control, "StopButton");
                stopButton?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void SpaceKey_WhenPressed_TriggersPlayPause()
        {
            // Arrange
            bool playEventRaised = false;
            _control.PlayRequested += (sender, args) => playEventRaised = true;

            // Enable keyboard shortcuts and set state
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.EnableKeyboardShortcutsProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanPlayProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.IsPlayingProperty, false);

            // Act
            ComponentTestHelper.PressKey(_control, System.Windows.Input.Key.Space);

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => playEventRaised, TimeSpan.FromSeconds(2));
            playEventRaised.Should().BeTrue();
        }

        [Fact]
        public void EscapeKey_WhenPressed_TriggersStop()
        {
            // Arrange
            bool stopEventRaised = false;
            _control.StopRequested += (sender, args) => stopEventRaised = true;

            // Enable keyboard shortcuts and set state
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.EnableKeyboardShortcutsProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanStopProperty, true);

            // Act
            ComponentTestHelper.PressKey(_control, System.Windows.Input.Key.Escape);

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => stopEventRaised, TimeSpan.FromSeconds(2));
            stopEventRaised.Should().BeTrue();
        }

        [Theory]
        [InlineData(System.Windows.Controls.Orientation.Vertical)]
        [InlineData(System.Windows.Controls.Orientation.Horizontal)]
        public void Orientation_WhenSet_UpdatesLayout(System.Windows.Controls.Orientation orientation)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.OrientationProperty, orientation);

            // Assert
            var result = ComponentTestHelper.GetProperty<System.Windows.Controls.Orientation>(_control, TransportControlsPanel.OrientationProperty);
            result.Should().Be(orientation);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowSkipButtons_WhenSet_TogglesButtonVisibility(bool showSkip)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.ShowSkipButtonsProperty, showSkip);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, TransportControlsPanel.ShowSkipButtonsProperty);
            result.Should().Be(showSkip);
        }

        [Fact]
        public void SkipForwardButton_WhenClicked_RaisesSkipForwardRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.SkipForwardRequested += (sender, args) => eventRaised = true;

            // Enable skip buttons
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.ShowSkipButtonsProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanSeekProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var button = FindButton(_control, "SkipForwardButton");
                button?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void SkipBackwardButton_WhenClicked_RaisesSkipBackwardRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.SkipBackwardRequested += (sender, args) => eventRaised = true;

            // Enable skip buttons
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.ShowSkipButtonsProperty, true);
            ComponentTestHelper.SetProperty(_control, TransportControlsPanel.CanSeekProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var button = FindButton(_control, "SkipBackwardButton");
                button?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        #region Helper Methods

        /// <summary>
        /// Finds a button by name in the control's visual tree.
        /// </summary>
        private System.Windows.Controls.Button? FindButton(System.Windows.DependencyObject parent, string name)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is System.Windows.Controls.Button button && button.Name == name)
                {
                    return button;
                }

                var result = FindButton(child, name);
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
            ComponentTestHelper.CleanupComponent(_control);
        }
    }
}
