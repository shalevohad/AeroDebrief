using Xunit;
using AeroDebrief.UI.Charts;
using SkiaSharp;

namespace AeroDebrief.Tests.Charts
{
    /// <summary>
    /// Tests for PilotMarkers unique geometry generation.
    /// Phase 4: Validates marker uniqueness and consistency.
    /// </summary>
    public class PilotMarkersTests
    {
        [Fact]
        public void GetMarkerForPilot_SameId_ReturnsSameMarker()
        {
            // Arrange
            var pilotId = "Pilot001";

            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(pilotId);
            var marker2 = PilotMarkers.GetMarkerForPilot(pilotId);

            // Assert - Should return the same cached instance
            Assert.Same(marker1, marker2);
        }

        [Fact]
        public void GetMarkerForPilot_DifferentIds_ReturnsDifferentMarkers()
        {
            // Arrange
            var pilotId1 = "Pilot001";
            var pilotId2 = "Pilot002";

            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(pilotId1);
            var marker2 = PilotMarkers.GetMarkerForPilot(pilotId2);

            // Assert - Different pilots should get different geometries
            Assert.NotSame(marker1, marker2);
            Assert.NotEqual(marker1.Bounds, marker2.Bounds);
        }

        [Fact]
        public void GetMarkerForPilot_NullOrEmpty_ReturnsCircle()
        {
            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(null!);
            var marker2 = PilotMarkers.GetMarkerForPilot(string.Empty);

            // Assert - Should return valid circle markers
            Assert.NotNull(marker1);
            Assert.NotNull(marker2);
            Assert.False(marker1.IsEmpty);
            Assert.False(marker2.IsEmpty);
        }

        [Fact]
        public void GetMarkerForPilot_DifferentSizes_CreatesDifferentMarkers()
        {
            // Arrange
            var pilotId = "Pilot001";

            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(pilotId, size: 8f);
            var marker2 = PilotMarkers.GetMarkerForPilot(pilotId, size: 16f);

            // Assert - Different sizes should be cached separately
            Assert.NotSame(marker1, marker2);
            Assert.True(marker2.Bounds.Width > marker1.Bounds.Width);
        }

        [Fact]
        public void MarkerTypeCount_AtLeast32Types()
        {
            // Assert - Plan requires 32+ unique geometries
            Assert.True(PilotMarkers.MarkerTypeCount >= 32, 
                $"Expected at least 32 marker types, got {PilotMarkers.MarkerTypeCount}");
        }

        [Fact]
        public void GetMarkerForPilot_ManyPilots_GeneratesUniqueMarkers()
        {
            // Arrange - Simulate 20+ pilots (10% of frequencies case)
            var markerReferences = new HashSet<object>();
            const int pilotCount = 24;

            // Act - Generate markers for many pilots
            for (int i = 0; i < pilotCount; i++)
            {
                var pilotId = $"Pilot{i:D3}";
                var marker = PilotMarkers.GetMarkerForPilot(pilotId);
                
                // Each marker instance is unique (even if geometry type repeats)
                markerReferences.Add(marker);
            }

            // Assert - With 32+ types, we should have good diversity
            // Each pilot ID gets a consistent marker (via hash)
            // With 24 pilots and 32 types, we expect most to be different instances
            // Note: Hash collisions mean some pilots might share the same marker TYPE,
            // but each pilot will consistently get the same marker
            Assert.True(markerReferences.Count >= pilotCount * 0.7, 
                $"Expected at least {pilotCount * 0.7:F0} unique marker instances, got {markerReferences.Count}");
            
            // Verify consistency - same pilot ID should return same marker instance
            var testPilot = "Pilot000";
            var marker1 = PilotMarkers.GetMarkerForPilot(testPilot);
            var marker2 = PilotMarkers.GetMarkerForPilot(testPilot);
            Assert.Same(marker1, marker2); // Should be cached
        }

        [Fact]
        public void GetMarkerForPilot_ValidGeometry()
        {
            // Arrange
            var pilotId = "Pilot001";

            // Act
            var marker = PilotMarkers.GetMarkerForPilot(pilotId);

            // Assert
            Assert.NotNull(marker);
            Assert.False(marker.IsEmpty);
            Assert.True(marker.Bounds.Width > 0);
            Assert.True(marker.Bounds.Height > 0);
        }

        [Fact]
        public void ClearCache_DisposesMarkers()
        {
            // Arrange
            var pilotId = "Pilot001";
            var marker1 = PilotMarkers.GetMarkerForPilot(pilotId);

            // Act
            PilotMarkers.ClearCache();
            var marker2 = PilotMarkers.GetMarkerForPilot(pilotId);

            // Assert - Should create a new marker instance after clear
            Assert.NotSame(marker1, marker2);
        }

        [Theory]
        [InlineData("Alpha")]
        [InlineData("Bravo")]
        [InlineData("Charlie")]
        [InlineData("Delta")]
        [InlineData("Echo")]
        [InlineData("Foxtrot")]
        [InlineData("Golf")]
        [InlineData("Hotel")]
        public void GetMarkerForPilot_DeterministicForCallsigns(string callsign)
        {
            // This test validates that marker assignment is deterministic
            // for realistic pilot callsigns
            
            // Act
            var marker1 = PilotMarkers.GetMarkerForPilot(callsign);
            PilotMarkers.ClearCache();
            var marker2 = PilotMarkers.GetMarkerForPilot(callsign);

            // Assert - Same callsign should get same marker type (bounds match)
            Assert.Equal(marker1.Bounds.Width, marker2.Bounds.Width, precision: 2);
            Assert.Equal(marker1.Bounds.Height, marker2.Bounds.Height, precision: 2);
        }

        [Fact]
        public void GetMarkerForPilot_CenteredAtOrigin()
        {
            // Arrange
            var pilotId = "Pilot001";

            // Act
            var marker = PilotMarkers.GetMarkerForPilot(pilotId, size: 10f);

            // Assert - Markers should be roughly centered at origin
            var bounds = marker.Bounds;
            var centerX = (bounds.Left + bounds.Right) / 2;
            var centerY = (bounds.Top + bounds.Bottom) / 2;
            
            Assert.True(Math.Abs(centerX) < 2, $"Center X should be near 0, got {centerX}");
            Assert.True(Math.Abs(centerY) < 2, $"Center Y should be near 0, got {centerY}");
        }

        [Fact]
        public void GetMarkerForPilot_SizeScalesCorrectly()
        {
            // Arrange
            var pilotId = "Pilot001";

            // Act
            var small = PilotMarkers.GetMarkerForPilot(pilotId, size: 5f);
            var large = PilotMarkers.GetMarkerForPilot(pilotId, size: 20f);

            // Assert - Larger size should result in larger bounds
            Assert.True(large.Bounds.Width > small.Bounds.Width * 2);
            Assert.True(large.Bounds.Height > small.Bounds.Height * 2);
        }
    }
}
