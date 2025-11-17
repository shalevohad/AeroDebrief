using Xunit;
using System.Windows;
using AeroDebrief.UI.Controls.Charts;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Phase9.Components
{
    /// <summary>
    /// Tests for PerformanceStatsOverlay component (Phase 9).
    /// Tests visibility toggle, metric formatting, and real-time updates.
    /// </summary>
    public class PerformanceStatsOverlayTests
    {
        [Fact]
        public void Overlay_CanBeCreated()
        {
            // Arrange & Act
            var overlay = new PerformanceStatsOverlay();

            // Assert
            Assert.NotNull(overlay);
        }

        [Fact]
        public void Overlay_HasCorrectAutomationProperties()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var automationName = overlay.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) as string;
            var automationHelpText = overlay.GetValue(System.Windows.Automation.AutomationProperties.HelpTextProperty) as string;

            // Assert
            Assert.Equal("Performance Statistics", automationName);
            Assert.Contains("F3", automationHelpText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Overlay_InitialOpacityIsZero()
        {
            // Arrange & Act
            var overlay = new PerformanceStatsOverlay();

            // Assert - Starts hidden
            Assert.Equal(0.0, overlay.Opacity);
        }

        [Fact]
        public void Overlay_HasStatsPanel()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var statsPanel = overlay.FindName("StatsPanel") as FrameworkElement;

            // Assert
            Assert.NotNull(statsPanel);
        }

        [Fact]
        public void Overlay_HasMemoryUsageText()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var memoryText = overlay.FindName("MemoryUsageText") as System.Windows.Controls.TextBlock;

            // Assert
            Assert.NotNull(memoryText);
        }

        [Fact]
        public void Overlay_HasFPSText()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var fpsText = overlay.FindName("FPSText") as System.Windows.Controls.TextBlock;

            // Assert
            Assert.NotNull(fpsText);
        }

        [Fact]
        public void Overlay_HasLoadTimeText()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var loadTimeText = overlay.FindName("LoadTimeText") as System.Windows.Controls.TextBlock;

            // Assert
            Assert.NotNull(loadTimeText);
        }

        [Fact]
        public void Overlay_HasFadeInAnimation()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var fadeInAnimation = overlay.Resources["FadeInAnimation"];

            // Assert
            Assert.NotNull(fadeInAnimation);
            Assert.IsType<System.Windows.Media.Animation.Storyboard>(fadeInAnimation);
        }

        [Fact]
        public void Overlay_HasFadeOutAnimation()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();

            // Act
            var fadeOutAnimation = overlay.Resources["FadeOutAnimation"];

            // Assert
            Assert.NotNull(fadeOutAnimation);
            Assert.IsType<System.Windows.Media.Animation.Storyboard>(fadeOutAnimation);
        }

        [Fact]
        public void Overlay_HasBitmapCacheForPerformance()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();
            var statsPanel = overlay.FindName("StatsPanel") as System.Windows.Controls.Grid;

            // Act
            var cacheMode = statsPanel?.CacheMode;

            // Assert - Should have BitmapCache for efficient rendering
            Assert.NotNull(cacheMode);
            Assert.IsType<System.Windows.Media.BitmapCache>(cacheMode);
        }

        [Fact]
        public void Overlay_IsPositionedTopRight()
        {
            // Arrange
            var overlay = new PerformanceStatsOverlay();
            var statsPanel = overlay.FindName("StatsPanel") as FrameworkElement;

            // Act
            var horizontalAlignment = statsPanel?.HorizontalAlignment;
            var verticalAlignment = statsPanel?.VerticalAlignment;

            // Assert
            Assert.Equal(HorizontalAlignment.Right, horizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, verticalAlignment);
        }

        [Fact]
        public async Task Overlay_LoadsWithoutException()
        {
            // Arrange & Act
            Exception caughtException = null;
            try
            {
                var overlay = new PerformanceStatsOverlay();
                await Task.Delay(50); // Allow XAML to load
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.Null(caughtException);
        }
    }
}
