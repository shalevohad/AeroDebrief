using AeroDebrief.Core;
using AeroDebrief.Tests.TestBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Unit tests for Position using test builders
    /// </summary>
    [TestClass]
    public class PositionTests
    {
        [TestMethod]
        public void Position_CanBeCreated_WithBuilder()
        {
            // Arrange & Act
            var position = new PositionBuilder()
                .WithLatitude(40.7128)
                .WithLongitude(-74.0060)
                .WithAltitude(100)
                .Build();

            // Assert
            Assert.IsNotNull(position);
            Assert.AreEqual(40.7128, position.Latitude);
            Assert.AreEqual(-74.0060, position.Longitude);
            Assert.AreEqual(100, position.Altitude);
        }

        [TestMethod]
        public void Position_WithValidPosition_IsValid()
        {
            // Arrange & Act
            var position = new PositionBuilder()
                .WithValidPosition()
                .Build();

            // Assert
            Assert.IsTrue(position.IsValid());
        }

        [TestMethod]
        public void Position_WithInvalidPosition_IsNotValid()
        {
            // Arrange & Act
            var position = new PositionBuilder()
                .WithInvalidPosition()
                .Build();

            // Assert
            Assert.IsFalse(position.IsValid());
        }

        [TestMethod]
        public void Position_ToString_WithValidPosition_ReturnsFormattedString()
        {
            // Arrange
            var position = new PositionBuilder()
                .WithValidPosition()
                .Build();

            // Act
            var result = position.ToString();

            // Assert
            Assert.IsTrue(result.Contains("Lat:"));
            Assert.IsTrue(result.Contains("Lng:"));
            Assert.IsTrue(result.Contains("Alt:"));
        }

        [TestMethod]
        public void Position_ToString_WithInvalidPosition_ReturnsUnknown()
        {
            // Arrange
            var position = new PositionBuilder()
                .WithInvalidPosition()
                .Build();

            // Act
            var result = position.ToString();

            // Assert
            Assert.AreEqual("Unknown Position", result);
        }

        [TestMethod]
        public void Position_CanSerializeAndDeserialize()
        {
            // Arrange
            var original = new PositionBuilder()
                .WithLatitude(51.5074)
                .WithLongitude(-0.1278)
                .WithAltitude(500)
                .Build();

            // Act
            using var memoryStream = new MemoryStream();
            using (var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true))
            {
                original.WriteToStream(writer);
            }

            memoryStream.Position = 0;

            Position? deserialized;
            using (var reader = new BinaryReader(memoryStream))
            {
                Position.TryReadFromStream(reader, out deserialized);
            }

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Latitude, deserialized.Latitude);
            Assert.AreEqual(original.Longitude, deserialized.Longitude);
            Assert.AreEqual(original.Altitude, deserialized.Altitude);
        }
    }
}
