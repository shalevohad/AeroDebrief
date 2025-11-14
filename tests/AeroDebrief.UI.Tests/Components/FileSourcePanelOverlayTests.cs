using System;
using System.Threading.Tasks;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.UI.Tests.Components
{
    /// <summary>
    /// Unit tests for FileSourcePanelOverlay component.
    /// Tests open/close animations, keyboard shortcuts, content hosting, and event raising.
    /// </summary>
    public class FileSourcePanelOverlayTests : IDisposable
    {
        private readonly FileSourcePanelOverlay _control;

        public FileSourcePanelOverlayTests()
        {
            _control = ComponentTestHelper.CreateComponent<FileSourcePanelOverlay>();
        }

        [Fact]
        public async Task IsOpen_WhenSetToTrue_ShowsOverlay()
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.IsOpenProperty, true);
            await Task.Delay(100);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsOpen_WhenSetToFalse_HidesOverlay()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.IsOpenProperty, true);
            await Task.Delay(100);

            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.IsOpenProperty, false);
            await Task.Delay(100);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Open_WhenCalled_TriggersSlideInAnimation()
        {
            // Arrange
            bool openedEventRaised = false;
            _control.PanelOpened += (sender, args) => openedEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400); // Wait for animation

            // Assert
            var isOpen = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            isOpen.Should().BeTrue();
            openedEventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task Close_WhenCalled_TriggersSlideOutAnimation()
        {
            // Arrange
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            bool closedEventRaised = false;
            _control.PanelClosed += (sender, args) => closedEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Close());
            await Task.Delay(300); // Wait for animation

            // Assert
            var isOpen = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            isOpen.Should().BeFalse();
            closedEventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task Toggle_WhenClosed_OpensPanel()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.IsOpenProperty, false);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Toggle());
            await Task.Delay(100);

            // Assert
            var isOpen = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            isOpen.Should().BeTrue();
        }

        [Fact]
        public async Task Toggle_WhenOpen_ClosesPanel()
        {
            // Arrange
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Toggle());
            await Task.Delay(100);

            // Assert
            var isOpen = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            isOpen.Should().BeFalse();
        }

        [Fact]
        public async Task EscapeKey_WhenPressed_ClosesPanel()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.EnableKeyboardShortcutsProperty, true);
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            // Act
            ComponentTestHelper.PressKey(_control, System.Windows.Input.Key.Escape);
            await Task.Delay(300);

            // Assert
            var isOpen = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.IsOpenProperty);
            isOpen.Should().BeFalse();
        }

        [Fact]
        public async Task ClickOutside_WhenEnabled_ClosesPanel()
        {
            // Arrange
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.CloseOnClickOutsideProperty, true);
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            bool closedEventRaised = false;
            _control.PanelClosed += (sender, args) => closedEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Simulate clicking the overlay (not the panel container)
                var method = typeof(FileSourcePanelOverlay).GetMethod("OverlayRoot_MouseDown",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                var mouseEventArgs = new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = System.Windows.UIElement.MouseDownEvent,
                    Source = _control // Simulate clicking on overlay itself
                };
                
                method?.Invoke(_control, new object[] { _control, mouseEventArgs });
            });
            
            await Task.Delay(300);

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => closedEventRaised, TimeSpan.FromSeconds(2));
            closedEventRaised.Should().BeTrue();
        }

        [Fact]
        public void PanelContent_WhenSet_DisplaysContent()
        {
            // Arrange
            var testContent = new System.Windows.Controls.TextBlock { Text = "Test Content" };

            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.PanelContentProperty, testContent);

            // Assert
            var result = ComponentTestHelper.GetProperty<object>(_control, FileSourcePanelOverlay.PanelContentProperty);
            result.Should().Be(testContent);
        }

        [Theory]
        [InlineData("Test Title")]
        [InlineData("Select Recording File")]
        [InlineData("Settings Panel")]
        public void PanelTitle_WhenSet_UpdatesHeaderText(string title)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.PanelTitleProperty, title);

            // Assert
            var result = ComponentTestHelper.GetProperty<string>(_control, FileSourcePanelOverlay.PanelTitleProperty);
            result.Should().Be(title);
        }

        [Theory]
        [InlineData(300)]
        [InlineData(400)]
        [InlineData(500)]
        public void PanelWidth_WhenSet_UpdatesPanelSize(double width)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.PanelWidthProperty, width);

            // Assert
            var result = ComponentTestHelper.GetProperty<double>(_control, FileSourcePanelOverlay.PanelWidthProperty);
            result.Should().Be(width);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EnableKeyboardShortcuts_WhenSet_TogglesShortcuts(bool enabled)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.EnableKeyboardShortcutsProperty, enabled);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.EnableKeyboardShortcutsProperty);
            result.Should().Be(enabled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CloseOnClickOutside_WhenSet_TogglesClickBehavior(bool enabled)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.CloseOnClickOutsideProperty, enabled);

            // Assert
            var result = ComponentTestHelper.GetProperty<bool>(_control, FileSourcePanelOverlay.CloseOnClickOutsideProperty);
            result.Should().Be(enabled);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(300)]
        [InlineData(500)]
        public void AnimationDuration_WhenSet_UpdatesAnimationSpeed(int duration)
        {
            // Act
            ComponentTestHelper.SetProperty(_control, FileSourcePanelOverlay.AnimationDurationProperty, duration);

            // Assert
            var result = ComponentTestHelper.GetProperty<int>(_control, FileSourcePanelOverlay.AnimationDurationProperty);
            result.Should().Be(duration);
        }

        [Fact]
        public async Task PanelOpening_WhenOpening_RaisesEvent()
        {
            // Arrange
            bool openingEventRaised = false;
            _control.PanelOpening += (sender, args) => openingEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(50); // Wait for event

            // Assert
            openingEventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task PanelOpened_AfterAnimation_RaisesEvent()
        {
            // Arrange
            bool openedEventRaised = false;
            _control.PanelOpened += (sender, args) => openedEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400); // Wait for animation to complete

            // Assert
            openedEventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task PanelClosing_WhenClosing_RaisesEvent()
        {
            // Arrange
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            bool closingEventRaised = false;
            _control.PanelClosing += (sender, args) => closingEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Close());
            await Task.Delay(50); // Wait for event

            // Assert
            closingEventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task PanelClosed_AfterAnimation_RaisesEvent()
        {
            // Arrange
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Open());
            await Task.Delay(400);

            bool closedEventRaised = false;
            _control.PanelClosed += (sender, args) => closedEventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () => _control.Close());
            await Task.Delay(300); // Wait for animation to complete

            // Assert
            closedEventRaised.Should().BeTrue();
        }

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_control);
        }
    }
}
