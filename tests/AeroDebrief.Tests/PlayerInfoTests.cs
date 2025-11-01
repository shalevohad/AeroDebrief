using AeroDebrief.Core;
using AeroDebrief.Tests.TestBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Unit tests for PlayerInfo using test builders
    /// </summary>
    [TestClass]
    public class PlayerInfoTests
    {
        [TestMethod]
        public void PlayerInfo_CanBeCreated_WithBuilder()
        {
            // Arrange & Act
            var player = new PlayerInfoBuilder()
                .WithName("Viper")
                .WithBlueCoalition()
                .Build();

            // Assert
            Assert.IsNotNull(player);
            Assert.AreEqual("Viper", player.Name);
            Assert.AreEqual(2, player.Coalition);
        }

        [TestMethod]
        public void PlayerInfo_GetCoalitionName_ReturnsCorrectValue()
        {
            // Arrange
            var redPlayer = new PlayerInfoBuilder().WithRedCoalition().Build();
            var bluePlayer = new PlayerInfoBuilder().WithBlueCoalition().Build();
            var spectator = new PlayerInfoBuilder().WithSpectatorCoalition().Build();

            // Act & Assert
            Assert.AreEqual("Red", redPlayer.GetCoalitionName());
            Assert.AreEqual("Blue", bluePlayer.GetCoalitionName());
            Assert.AreEqual("Spectator", spectator.GetCoalitionName());
        }

        [TestMethod]
        public void PlayerInfo_GetDisplayName_WithValidName_ReturnsName()
        {
            // Arrange
            var player = new PlayerInfoBuilder()
                .WithName("Maverick")
                .Build();

            // Act
            var displayName = player.GetDisplayName();

            // Assert
            Assert.IsTrue(displayName.Contains("Maverick"));
        }

        [TestMethod]
        public void PlayerInfo_GetDisplayName_WithEmptyName_ReturnsGuid()
        {
            // Arrange
            var guid = "test-guid-12345";
            var player = new PlayerInfoBuilder()
                .WithName(string.Empty)
                .WithTransmitterGuid(guid)
                .Build();

            // Act
            var displayName = player.GetDisplayName();

            // Assert
            Assert.IsTrue(displayName.Contains(guid));
        }

        [TestMethod]
        public void PlayerInfo_CanBeCreated_WithAircraftInfo()
        {
            // Arrange & Act
            var player = new PlayerInfoBuilder()
                .WithF16Pilot()
                .Build();

            // Assert
            Assert.IsNotNull(player.AircraftInfo);
            Assert.AreEqual("F-16C_50", player.AircraftInfo.UnitType);
        }

        [TestMethod]
        public void PlayerInfo_CanBeCreated_WithPosition()
        {
            // Arrange & Act
            var player = new PlayerInfoBuilder()
                .WithPosition(p => p.WithValidPosition())
                .Build();

            // Assert
            Assert.IsNotNull(player.Position);
            Assert.IsTrue(player.Position.IsValid());
        }

        [TestMethod]
        public void PlayerInfo_CanSerializeAndDeserialize()
        {
            // Arrange
            var original = new PlayerInfoBuilder()
                .WithName("Goose")
                .WithBlueCoalition()
                .WithA10Pilot()
                .WithPosition(p => p.WithValidPosition())
                .Build();

            // Act
            using var memoryStream = new MemoryStream();
            using (var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true))
            {
                original.WriteToStream(writer);
            }

            memoryStream.Position = 0;

            PlayerInfo? deserialized;
            using (var reader = new BinaryReader(memoryStream))
            {
                PlayerInfo.TryReadFromStream(reader, out deserialized);
            }

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Coalition, deserialized.Coalition);
            Assert.AreEqual(original.AircraftInfo.UnitType, deserialized.AircraftInfo.UnitType);
            Assert.AreEqual(original.Position.Latitude, deserialized.Position.Latitude);
        }
    }
}
