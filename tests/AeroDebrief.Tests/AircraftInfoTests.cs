using AeroDebrief.Core;
using AeroDebrief.Tests.TestBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Unit tests for AircraftInfo using test builders
    /// </summary>
    [TestClass]
    public class AircraftInfoTests
    {
        [TestMethod]
        public void AircraftInfo_CanBeCreated_WithBuilder()
        {
            // Arrange & Act
            var aircraft = new AircraftInfoBuilder()
                .WithUnitType("F-16C_50")
                .WithUnitId(1001)
                .Build();

            // Assert
            Assert.IsNotNull(aircraft);
            Assert.AreEqual("F-16C_50", aircraft.UnitType);
            Assert.AreEqual((uint)1001, aircraft.UnitId);
        }

        [TestMethod]
        public void AircraftInfo_WithF16_HasCorrectType()
        {
            // Arrange & Act
            var aircraft = new AircraftInfoBuilder()
                .WithF16()
                .Build();

            // Assert
            Assert.AreEqual("F-16C_50", aircraft.UnitType);
        }

        [TestMethod]
        public void AircraftInfo_WithA10_HasCorrectType()
        {
            // Arrange & Act
            var aircraft = new AircraftInfoBuilder()
                .WithA10()
                .Build();

            // Assert
            Assert.AreEqual("A-10C", aircraft.UnitType);
        }

        [TestMethod]
        public void AircraftInfo_WithFA18_HasCorrectType()
        {
            // Arrange & Act
            var aircraft = new AircraftInfoBuilder()
                .WithFA18()
                .Build();

            // Assert
            Assert.AreEqual("FA-18C_hornet", aircraft.UnitType);
        }

        [TestMethod]
        public void AircraftInfo_ToString_WithValidType_ReturnsFormattedString()
        {
            // Arrange
            var aircraft = new AircraftInfoBuilder()
                .WithF16()
                .Build();

            // Act
            var result = aircraft.ToString();

            // Assert
            Assert.IsTrue(result.Contains("F-16C_50"));
            Assert.IsTrue(result.Contains("1001"));
        }

        [TestMethod]
        public void AircraftInfo_ToString_WithEmptyType_ReturnsUnknown()
        {
            // Arrange
            var aircraft = new AircraftInfoBuilder()
                .WithUnitType(string.Empty)
                .WithUnitId(999)
                .Build();

            // Act
            var result = aircraft.ToString();

            // Assert
            Assert.IsTrue(result.Contains("Unknown Aircraft"));
            Assert.IsTrue(result.Contains("999"));
        }

        [TestMethod]
        public void AircraftInfo_CanSerializeAndDeserialize()
        {
            // Arrange
            var original = new AircraftInfoBuilder()
                .WithA10()
                .Build();

            // Act
            using var memoryStream = new MemoryStream();
            using (var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true))
            {
                original.WriteToStream(writer);
            }

            memoryStream.Position = 0;

            AircraftInfo? deserialized;
            using (var reader = new BinaryReader(memoryStream))
            {
                AircraftInfo.TryReadFromStream(reader, out deserialized);
            }

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.UnitType, deserialized.UnitType);
            Assert.AreEqual(original.UnitId, deserialized.UnitId);
        }
    }
}
