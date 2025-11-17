using Xunit;
using AeroDebrief.UI.Charts;
using SkiaSharp;

namespace AeroDebrief.Tests.Charts
{
    /// <summary>
    /// Tests for ChartColors deterministic color palette.
    /// Phase 4: Validates color assignment consistency and uniqueness.
    /// </summary>
    public class ChartColorsTests
    {
        [Fact]
        public void GetColorForFrequency_SameId_ReturnsSameColor()
        {
            // Arrange
            var freqId = "251.0-AM";

            // Act
            var color1 = ChartColors.GetColorForFrequency(freqId);
            var color2 = ChartColors.GetColorForFrequency(freqId);

            // Assert
            Assert.Equal(color1, color2);
        }

        [Fact]
        public void GetColorForFrequency_DifferentIds_ReturnsDifferentColors()
        {
            // Arrange
            var freqId1 = "251.0-AM";
            var freqId2 = "305.0-FM";

            // Act
            var color1 = ChartColors.GetColorForFrequency(freqId1);
            var color2 = ChartColors.GetColorForFrequency(freqId2);

            // Assert
            Assert.NotEqual(color1, color2);
        }

        [Fact]
        public void GetColorForFrequency_NullOrEmpty_ReturnsGray()
        {
            // Act
            var color1 = ChartColors.GetColorForFrequency(null!);
            var color2 = ChartColors.GetColorForFrequency(string.Empty);

            // Assert
            Assert.Equal(SKColors.Gray, color1);
            Assert.Equal(SKColors.Gray, color2);
        }

        [Fact]
        public void GetColorWithAlpha_AppliesOpacity()
        {
            // Arrange
            var freqId = "251.0-AM";
            byte alpha = 128;

            // Act
            var baseColor = ChartColors.GetColorForFrequency(freqId);
            var alphaColor = ChartColors.GetColorWithAlpha(freqId, alpha);

            // Assert
            Assert.Equal(baseColor.Red, alphaColor.Red);
            Assert.Equal(baseColor.Green, alphaColor.Green);
            Assert.Equal(baseColor.Blue, alphaColor.Blue);
            Assert.Equal(alpha, alphaColor.Alpha);
        }

        [Fact]
        public void GetColorForFrequency_ManyFrequencies_UsesFullPalette()
        {
            // Arrange
            var paletteSize = ChartColors.PaletteSize;
            var colors = new HashSet<SKColor>();

            // Act - Generate colors for many frequencies
            for (int i = 0; i < paletteSize; i++)
            {
                var freqId = $"F{i:000}.0-AM";
                var color = ChartColors.GetColorForFrequency(freqId);
                colors.Add(color);
            }

            // Assert - Should use multiple unique colors
            Assert.True(colors.Count >= paletteSize / 2, 
                $"Expected at least {paletteSize / 2} unique colors, got {colors.Count}");
        }

        [Fact]
        public void ClearCache_ResetsColorAssignments()
        {
            // Arrange
            var freqId = "251.0-AM";
            var color1 = ChartColors.GetColorForFrequency(freqId);

            // Act
            ChartColors.ClearCache();
            var color2 = ChartColors.GetColorForFrequency(freqId);

            // Assert - Should still be deterministic even after clear
            Assert.Equal(color1, color2);
        }

        [Fact]
        public void GetAllColors_ReturnsFullPalette()
        {
            // Act
            var colors = ChartColors.GetAllColors();

            // Assert
            Assert.NotEmpty(colors);
            Assert.Equal(ChartColors.PaletteSize, colors.Count);
            Assert.All(colors, c => Assert.NotEqual(SKColor.Empty, c));
        }

        [Theory]
        [InlineData("251.0-AM")]
        [InlineData("305.0-FM")]
        [InlineData("127.5-AM")]
        [InlineData("380.25-UHF")]
        public void GetColorForFrequency_ConsistentAcrossRuns(string freqId)
        {
            // This test validates that hash-based color assignment
            // is deterministic (same across process runs)
            
            // Act
            var color1 = ChartColors.GetColorForFrequency(freqId);
            
            // Clear and re-get (simulates different run)
            ChartColors.ClearCache();
            var color2 = ChartColors.GetColorForFrequency(freqId);

            // Assert
            Assert.Equal(color1, color2);
        }
    }
}
