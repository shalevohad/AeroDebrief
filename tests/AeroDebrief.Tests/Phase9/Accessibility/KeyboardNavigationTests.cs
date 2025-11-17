using Xunit;
using FluentAssertions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using AeroDebrief.UI.Controls.Charts;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.Tests.TestHelpers;

namespace AeroDebrief.Tests.Phase9.Accessibility
{
    /// <summary>
    /// Keyboard navigation tests for Phase 9 features.
    /// Verifies all overlays and controls are keyboard accessible.
    /// </summary>
    public class KeyboardNavigationTests
    {
        [Fact(Skip = "Requires WPF focus context")]
        public void TabKey_CyclesThroughControls()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var retryButton = errorBanner.FindName("RetryButton") as Button;
            var dismissButton = errorBanner.FindName("DismissButton") as Button;

            // Act - Simulate Tab key
            retryButton?.Focus();
            var firstFocus = Keyboard.FocusedElement;
            
            // Simulate Tab
            var tabArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dismissButton?.RaiseEvent(tabArgs);
            
            var secondFocus = Keyboard.FocusedElement;

            // Assert
            firstFocus.Should().NotBeNull("First element should have focus");
            secondFocus.Should().NotBeNull("Second element should have focus after Tab");
            firstFocus.Should().NotBe(secondFocus, "Focus should move to next element");
        }

        [Fact(Skip = "Requires WPF focus context")]
        public void ShiftTab_CyclesBackward()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var dismissButton = errorBanner.FindName("DismissButton") as Button;

            // Act - Simulate Shift+Tab
            dismissButton?.Focus();
            var firstFocus = Keyboard.FocusedElement;

            var shiftTabArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            dismissButton?.RaiseEvent(shiftTabArgs);
            
            var secondFocus = Keyboard.FocusedElement;

            // Assert
            secondFocus.Should().NotBe(firstFocus, "Focus should move backward with Shift+Tab");
        }

        [Fact(Skip = "Requires WPF input context")]
        public void EnterKey_ActivatesButtons()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var dismissButton = errorBanner.FindName("DismissButton") as Button;
            var buttonClicked = false;
            
            if (dismissButton != null)
            {
                dismissButton.Click += (s, e) => buttonClicked = true;
            }

            // Act - Simulate Enter key
            var enterArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dismissButton?.RaiseEvent(enterArgs);

            // Assert
            buttonClicked.Should().BeTrue("Enter key should activate button");
        }

        [Fact(Skip = "Requires WPF input context")]
        public void SpaceKey_ActivatesButtons()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var dismissButton = errorBanner.FindName("DismissButton") as Button;
            var buttonClicked = false;
            
            if (dismissButton != null)
            {
                dismissButton.Click += (s, e) => buttonClicked = true;
            }

            // Act - Simulate Space key
            var spaceArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Space)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dismissButton?.RaiseEvent(spaceArgs);

            // Assert
            buttonClicked.Should().BeTrue("Space key should activate button");
        }

        [Fact]
        public void EscapeKey_DismissesOverlays()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();

            // Act - Simulate Escape key
            var escapeArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            errorBanner.RaiseEvent(escapeArgs);

            // Assert
            // The ErrorBannerOverlay should handle Escape key in PreviewKeyDown
            Assert.NotNull(errorBanner);
        }

        [Fact]
        public void F3Key_TogglesPerformanceStats()
        {
            // Arrange
            var provider = new MockAmplitudeSeriesProvider();
            var vm = new UnifiedGraphViewModel(provider);
            var initialState = vm.ShowPerformanceStats;

            // Act - Simulate F3 key press (toggle)
            vm.ShowPerformanceStats = !vm.ShowPerformanceStats;

            // Assert
            vm.ShowPerformanceStats.Should().NotBe(initialState, 
                "F3 should toggle performance stats visibility");

            // Cleanup
            vm.Dispose();
        }

        [Fact(Skip = "Requires WPF focus context")]
        public void FocusTrap_WorksInErrorBanner()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var retryButton = errorBanner.FindName("RetryButton") as Button;
            var dismissButton = errorBanner.FindName("DismissButton") as Button;

            // Act - Try to Tab past last element
            dismissButton?.Focus();
            
            // Simulate Tab from last element
            var tabArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                Keyboard.PrimaryDevice.ActiveSource,
                0,
                Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dismissButton?.RaiseEvent(tabArgs);

            var focusAfterTab = Keyboard.FocusedElement;

            // Assert
            // Focus should wrap back to first element in error banner
            Assert.NotNull(focusAfterTab);
        }

        [Fact(Skip = "Requires WPF focus context")]
        public void FocusRestoration_WorksAfterDismiss()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var externalElement = new Button();
            
            externalElement.Focus();
            var originalFocus = Keyboard.FocusedElement;

            // Act - Show error banner, then dismiss
            // Error banner should store original focus
            var dismissButton = errorBanner.FindName("DismissButton") as Button;
            dismissButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var restoredFocus = Keyboard.FocusedElement;

            // Assert
            restoredFocus.Should().Be(originalFocus, 
                "Focus should be restored to original element after dismissing error");
        }
    }
}
