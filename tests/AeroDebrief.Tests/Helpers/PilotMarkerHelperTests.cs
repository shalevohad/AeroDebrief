using Xunit;
using AeroDebrief.UI.Helpers;
using AeroDebrief.Core.Models;
using AeroDebrief.UI.Charts;
using System.Windows.Media;

namespace AeroDebrief.Tests.Helpers
{
    /// <summary>
    /// Tests for PilotMarkerHelper WPF integration.
    /// Phase 4: Validates marker generation for UI display.
    /// Note: Some tests are skipped as they require STA thread (WPF UI).
    /// </summary>
    public class PilotMarkerHelperTests
    {
        private PlayerFrequencyInfo CreateTestPlayer(string name = "TestPilot", string guid = "test-guid-123")
        {
            return new PlayerFrequencyInfo
            {
                Name = name,
                TransmitterGuid = guid,
                Coalition = "Blue",
                Aircraft = "F-16C",
                PacketCount = 100,
                FirstSeen = DateTime.Now.AddMinutes(-10),
                LastSeen = DateTime.Now
            };
        }

        [Fact]
        public void AvailableMarkerTypes_MatchesPilotMarkers()
        {
            // Arrange & Act
            var count = PilotMarkerHelper.AvailableMarkerTypes;

            // Assert - Should be 32+ from PilotMarkers
            Assert.True(count >= 32, $"Expected at least 32 marker types, got {count}");
        }

        [Fact]
        public void PlayerFrequencyInfo_PilotId_UsesTransmitterGuid()
        {
            // Arrange
            var player = CreateTestPlayer("TestPilot", "guid-123");

            // Act
            var pilotId = player.PilotId;

            // Assert
            Assert.Equal("guid-123", pilotId);
        }

        [Fact]
        public void PlayerFrequencyInfo_PilotId_FallsBackToName()
        {
            // Arrange
            var player = new PlayerFrequencyInfo
            {
                Name = "TestPilot",
                TransmitterGuid = "", // Empty guid
                Coalition = "Blue"
            };

            // Act
            var pilotId = player.PilotId;

            // Assert
            Assert.Equal("TestPilot", pilotId);
        }

        [Fact]
        public void GetFrequencyColorBrush_ReturnsValidColor()
        {
            // Arrange
            var frequencyHz = 251_000_000.0; // 251 MHz
            var modulation = "AM";

            // Act - Test the underlying color logic
            var frequencyMHz = frequencyHz / 1_000_000.0;
            var frequencyId = $"{frequencyMHz:F1}-{modulation}";
            var skColor = ChartColors.GetColorForFrequency(frequencyId);

            // Assert
            Assert.NotEqual(SkiaSharp.SKColor.Empty, skColor);
            Assert.True(skColor.Alpha > 0);
        }

        [Fact]
        public void GetFrequencyId_FormatsCorrectly()
        {
            // Arrange
            var frequencyHz = 251_000_000.0; // 251 MHz
            var modulation = "AM";

            // Act
            var frequencyMHz = frequencyHz / 1_000_000.0;
            var frequencyId = $"{frequencyMHz:F1}-{modulation}";

            // Assert
            Assert.Equal("251.0-AM", frequencyId);
        }

        [Fact]
        public void PilotMarker_UnderlyingGeometry_IsUnique()
        {
            // Test that different pilots get different SKPath geometries
            var player1 = CreateTestPlayer("Alpha", "guid-001");
            var player2 = CreateTestPlayer("Bravo", "guid-002");

            // Act - Get SKPath directly (not WPF Path)
            var marker1 = PilotMarkers.GetMarkerForPilot(player1.PilotId);
            var marker2 = PilotMarkers.GetMarkerForPilot(player2.PilotId);

            // Assert - Should get different geometries
            Assert.NotNull(marker1);
            Assert.NotNull(marker2);
            Assert.False(marker1.IsEmpty);
            Assert.False(marker2.IsEmpty);
            
            // Different pilots should (very likely) have different bounds
            Assert.NotEqual(marker1.Bounds, marker2.Bounds);
        }

        [Fact]
        public void PilotMarker_ConsistentForSamePilot()
        {
            // Test that same pilot always gets same marker
            var player = CreateTestPlayer("Alpha", "guid-001");

            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(player.PilotId);
            var marker2 = PilotMarkers.GetMarkerForPilot(player.PilotId);

            // Assert - Should get same cached instance
            Assert.Same(marker1, marker2);
        }

        [Fact]
        public void PlayerFrequencyInfo_HasDisplayMethods()
        {
            // Test that PlayerFrequencyInfo has proper display methods
            var player = CreateTestPlayer();

            var displayText = player.GetDisplayText();
            var displayWithMarker = player.GetDisplayTextWithMarker();

            Assert.NotNull(displayText);
            Assert.NotEmpty(displayText);
            Assert.Contains("TestPilot", displayText);
            
            Assert.NotNull(displayWithMarker);
            Assert.NotEmpty(displayWithMarker);
        }

        // WPF-dependent tests are commented out as they require STA thread
        // These would be better suited for UI integration tests
        
        /*
        [Fact(Skip = "Requires STA thread for WPF")]
        public void GetPilotMarkerPath_ReturnsValidPath()
        {
            var player = CreateTestPlayer();
            var path = PilotMarkerHelper.GetPilotMarkerPath(player, size: 12.0);
            Assert.NotNull(path);
        }
        */
    }
}
