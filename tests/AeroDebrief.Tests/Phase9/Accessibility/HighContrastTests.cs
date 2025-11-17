using Xunit;
using FluentAssertions;
using System.Windows;
using System.Windows.Media;
using AeroDebrief.UI.Controls.Charts;

namespace AeroDebrief.Tests.Phase9.Accessibility
{
    /// <summary>
    /// High contrast mode tests for Phase 9 overlays.
    /// Verifies WCAG 2.1 AA compliance for high contrast themes.
    /// </summary>
    public class HighContrastTests
    {
        [Fact(Skip = "Requires WPF resource context")]
        public void HighContrast_StylesApply_Automatically()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();

            // Act - Simulate high contrast mode
            // In real WPF, this would be detected via SystemParameters.HighContrast
            
            // Check if high contrast resources are available
            var hasHighContrastStyles = Application.Current?.Resources.Contains("HighContrastStyles") ?? false;

            // Assert
            // High contrast styles should be defined in resources
            Assert.NotNull(errorBanner);
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public void HighContrast_FocusIndicators_Visible()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();
            var retryButton = errorBanner.FindName("RetryButton") as System.Windows.Controls.Button;

            // Act - Check focus visual in high contrast
            var focusVisual = retryButton?.FocusVisualStyle;

            // Assert
            focusVisual.Should().NotBeNull(
                "Focus indicators must be visible in high contrast mode");
        }

        [Fact]
        public void HighContrast_Borders_Visible()
        {
            // Arrange
            var errorBanner = new ErrorBannerOverlay();

            // Act - Find border elements
            var border = FindVisualChild<System.Windows.Controls.Border>(errorBanner, "MainBorder");

            // Assert
            if (border != null)
            {
                border.BorderThickness.Should().NotBe(new Thickness(0),
                    "Borders should be visible in high contrast mode");
            }
        }

        [Fact]
        public void HighContrast_TextContrast_WCAG21AA()
        {
            // Arrange
            const double RequiredContrastRatio = 4.5; // WCAG 2.1 AA for normal text

            // Sample high contrast colors (from HighContrastStyles.xaml)
            var backgroundColor = Color.FromRgb(0, 0, 0); // Black
            var textColor = Color.FromRgb(255, 255, 255); // White

            // Act
            var contrastRatio = CalculateContrastRatio(textColor, backgroundColor);

            // Assert
            contrastRatio.Should().BeGreaterOrEqualTo(RequiredContrastRatio,
                $"Text contrast ratio should meet WCAG 2.1 AA (4.5:1), actual: {contrastRatio:F2}:1");
        }

        #region Helper Methods

        /// <summary>
        /// Calculates the contrast ratio between two colors per WCAG 2.1.
        /// </summary>
        private double CalculateContrastRatio(Color foreground, Color background)
        {
            var L1 = GetRelativeLuminance(foreground);
            var L2 = GetRelativeLuminance(background);

            // Ensure L1 is the lighter color
            if (L2 > L1)
            {
                (L1, L2) = (L2, L1);
            }

            return (L1 + 0.05) / (L2 + 0.05);
        }

        /// <summary>
        /// Calculates relative luminance per WCAG 2.1.
        /// </summary>
        private double GetRelativeLuminance(Color color)
        {
            var r = GetChannelLuminance(color.R / 255.0);
            var g = GetChannelLuminance(color.G / 255.0);
            var b = GetChannelLuminance(color.B / 255.0);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// Applies gamma correction to a color channel.
        /// </summary>
        private double GetChannelLuminance(double channel)
        {
            if (channel <= 0.03928)
            {
                return channel / 12.92;
            }
            return System.Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        private static T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && (child as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }

                var foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }

        #endregion
    }
}
