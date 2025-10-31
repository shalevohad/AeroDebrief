using System;
using System.Windows;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.UI.Tests.Components
{
    /// <summary>
    /// Unit tests for PlayerHeaderControl component.
    /// Tests dependency properties, event raising, and UI behavior.
    /// </summary>
    public class PlayerHeaderControlTests : IDisposable
    {
        private readonly PlayerHeaderControl _control;

        public PlayerHeaderControlTests()
        {
            // Create control on STA thread
            _control = ComponentTestHelper.CreateComponent<PlayerHeaderControl>();
        }

        [Fact]
        public void StatusMessage_WhenSet_UpdatesUI()
        {
            // Arrange
            const string testMessage = "Test Status Message";

            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.StatusMessageProperty, testMessage);

            // Assert
            var result = ComponentTestHelper.GetProperty<string>(_control, PlayerHeaderControl.StatusMessageProperty);
            result.Should().Be(testMessage);
        }

        [Fact]
        public void SourceName_WhenSet_UpdatesUI()
        {
            // Arrange
            const string testSourceName = "Test Recording.srs";

            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.SourceNameProperty, testSourceName);

            // Assert
            var result = ComponentTestHelper.GetProperty<string>(_control, PlayerHeaderControl.SourceNameProperty);
            result.Should().Be(testSourceName);
        }

        [Fact]
        public void CurrentMode_WhenSet_UpdatesBadge()
        {
            // Arrange
            const string testMode = "Recording";

            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.CurrentModeProperty, testMode);

            // Assert
            var result = ComponentTestHelper.GetProperty<string>(_control, PlayerHeaderControl.CurrentModeProperty);
            result.Should().Be(testMode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsIdle_WhenSet_TogglesVisibilityCorrectly(bool isIdle)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.IsIdleProperty, isIdle);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, PlayerHeaderControl.IsIdleProperty);
            result.Should().Be(isIdle);
        }

        [Fact]
        public void ServerSourceButton_WhenClicked_RaisesSourceTypeSelectedEvent()
        {
            // Arrange
            SourceTypeEventArgs? capturedArgs = null;
            _control.SourceTypeSelected += (sender, args) => capturedArgs = args;

            // Set control to idle mode so buttons are visible
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.IsIdleProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Find and click the server button
                var serverButton = FindButton(_control, "ServerSourceButton");
                serverButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.SourceType.Should().Be(SourceType.Server);
        }

        [Fact]
        public void FileSourceButton_WhenClicked_RaisesSourceTypeSelectedEvent()
        {
            // Arrange
            SourceTypeEventArgs? capturedArgs = null;
            _control.SourceTypeSelected += (sender, args) => capturedArgs = args;

            // Set control to idle mode so buttons are visible
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.IsIdleProperty, true);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Find and click the file button
                var fileButton = FindButton(_control, "FileSourceButton");
                fileButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.SourceType.Should().Be(SourceType.File);
        }

        [Fact]
        public void OpenFilePanelButton_WhenClicked_RaisesFilePanelRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _control.FilePanelRequested += (sender, args) => eventRaised = true;

            // Set control to active mode so button is visible
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.IsIdleProperty, false);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Find and click the open file panel button
                var button = FindButton(_control, "OpenFilePanelButton");
                button?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void ChangeSourceCommand_WhenBound_CanExecute()
        {
            // Arrange
            var mockCommand = new MockCommand();

            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.ChangeSourceCommandProperty, mockCommand);

            // Assert
            var result = ComponentTestHelper.GetProperty<MockCommand>(_control, PlayerHeaderControl.ChangeSourceCommandProperty);
            result.Should().NotBeNull();
            result.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void OpenSettingsCommand_WhenBound_CanExecute()
        {
            // Arrange
            var mockCommand = new MockCommand();

            // Act
            ComponentTestHelper.SetProperty(_control, PlayerHeaderControl.OpenSettingsCommandProperty, mockCommand);

            // Assert
            var result = ComponentTestHelper.GetProperty<MockCommand>(_control, PlayerHeaderControl.OpenSettingsCommandProperty);
            result.Should().NotBeNull();
            result.CanExecute(null).Should().BeTrue();
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
            ComponentTestHelper.CleanupComponent(_control);
        }
    }
}
