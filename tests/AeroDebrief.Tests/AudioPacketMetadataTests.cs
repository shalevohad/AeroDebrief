using AeroDebrief.Core;
using AeroDebrief.Tests.TestBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Unit tests for AudioPacketMetadata using test builders
    /// </summary>
    [TestClass]
    public class AudioPacketMetadataTests
    {
        [TestMethod]
        public void AudioPacketMetadata_CanBeCreated_WithBuilder()
        {
            // Arrange & Act
            var packet = new AudioPacketMetadataBuilder()
                .WithFrequencyMHz(251.0)
                .WithAMModulation()
                .WithBlueCoalition()
                .Build();

            // Assert
            Assert.IsNotNull(packet);
            Assert.AreEqual(251_000_000, packet.Frequency);
            Assert.AreEqual((byte)0, packet.Modulation);
            Assert.AreEqual(2, packet.Coalition);
        }

        [TestMethod]
        public void AudioPacketMetadata_CanBeCreated_WithPlayerData()
        {
            // Arrange & Act
            var packet = new AudioPacketMetadataBuilder()
                .WithPlayerData(p => p
                    .WithF16Pilot()
                    .WithName("Maverick")
                    .WithBlueCoalition())
                .Build();

            // Assert
            Assert.IsNotNull(packet.PlayerData);
            Assert.AreEqual("Maverick", packet.PlayerData.Name);
            Assert.AreEqual("F-16C_50", packet.PlayerData.AircraftInfo.UnitType);
        }

        [TestMethod]
        public void AudioPacketMetadata_CanBeCreated_WithOpusAudio()
        {
            // Arrange & Act
            var packet = new AudioPacketMetadataBuilder()
                .WithOpusSilence()
                .Build();

            // Assert
            Assert.IsNotNull(packet.AudioPayload);
            Assert.AreEqual(2, packet.AudioPayload.Length);
        }

        [TestMethod]
        public void AudioPacketMetadata_CanBeCreated_WithPcmTone()
        {
            // Arrange & Act
            var packet = new AudioPacketMetadataBuilder()
                .WithPcmTone(440.0, 0.02)
                .Build();

            // Assert
            Assert.IsNotNull(packet.AudioPayload);
            Assert.IsTrue(packet.AudioPayload.Length > 0);
        }

        [TestMethod]
        public void AudioPacketMetadata_CanSerializeAndDeserialize()
        {
            // Arrange
            var original = new AudioPacketMetadataBuilder()
                .WithFrequencyMHz(305.0)
                .WithFMModulation()
                .WithPlayerData(p => p
                    .WithName("Iceman")
                    .WithA10Pilot())
                .WithOpusSilence()
                .Build();

            // Act
            using var memoryStream = new MemoryStream();
            using (var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true))
            {
                original.TryWriteMetadata(writer);
            }

            memoryStream.Position = 0;

            AudioPacketMetadata? deserialized;
            using (var reader = new BinaryReader(memoryStream))
            {
                AudioPacketMetadata.TryReadMetadata(reader, out deserialized);
            }

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Frequency, deserialized.Frequency);
            Assert.AreEqual(original.Modulation, deserialized.Modulation);
            Assert.AreEqual(original.PlayerData.Name, deserialized.PlayerData.Name);
            Assert.AreEqual(original.AudioPayload.Length, deserialized.AudioPayload.Length);
        }

        [TestMethod]
        public void AudioPacketMetadata_MultiplePackets_CanBeSerialized()
        {
            // Arrange
            var packets = new[]
            {
                new AudioPacketMetadataBuilder()
                    .WithFrequencyMHz(251.0)
                    .WithPacketId(1)
                    .WithPlayerData(p => p.WithName("Alpha1"))
                    .Build(),
                new AudioPacketMetadataBuilder()
                    .WithFrequencyMHz(251.0)
                    .WithPacketId(2)
                    .WithPlayerData(p => p.WithName("Alpha2"))
                    .Build(),
                new AudioPacketMetadataBuilder()
                    .WithFrequencyMHz(305.0)
                    .WithPacketId(3)
                    .WithPlayerData(p => p.WithName("Bravo1"))
                    .Build()
            };

            // Act
            using var memoryStream = new MemoryStream();
            using (var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true))
            {
                foreach (var packet in packets)
                {
                    packet.TryWriteMetadata(writer);
                }
            }

            memoryStream.Position = 0;

            var deserializedPackets = new List<AudioPacketMetadata>();
            using (var reader = new BinaryReader(memoryStream))
            {
                while (memoryStream.Position < memoryStream.Length)
                {
                    if (AudioPacketMetadata.TryReadMetadata(reader, out var packet) && packet != null)
                    {
                        deserializedPackets.Add(packet);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Assert
            Assert.AreEqual(packets.Length, deserializedPackets.Count);
            Assert.AreEqual("Alpha1", deserializedPackets[0].PlayerData.Name);
            Assert.AreEqual("Alpha2", deserializedPackets[1].PlayerData.Name);
            Assert.AreEqual("Bravo1", deserializedPackets[2].PlayerData.Name);
        }
    }
}
