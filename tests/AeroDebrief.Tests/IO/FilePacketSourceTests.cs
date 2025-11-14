using AeroDebrief.Core;
using AeroDebrief.Core.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace AeroDebrief.Tests.IO
{
    [TestClass]
    public class FilePacketSourceTests
    {
        [TestMethod]
        public async Task OpenAsync_CreatesIndex_WhenNoIndexExists()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(100);
            var source = new FilePacketSource(tempFile);

            try
            {
                // Act
                await source.OpenAsync();

                // Assert
                Assert.IsTrue(source.TotalPackets > 0);
                Assert.IsTrue(File.Exists(Path.ChangeExtension(tempFile, ".pkidx")));
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task OpenAsync_ReusesIndex_WhenIndexExists()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(100);
            var indexPath = Path.ChangeExtension(tempFile, ".pkidx");
            
            // Create index first time
            var source1 = new FilePacketSource(tempFile);
            await source1.OpenAsync();
            var firstIndexTime = File.GetLastWriteTimeUtc(indexPath);
            source1.Dispose();

            // Wait a bit to ensure timestamp difference
            await Task.Delay(100);

            try
            {
                // Act - Open again
                var source2 = new FilePacketSource(tempFile);
                await source2.OpenAsync();
                var secondIndexTime = File.GetLastWriteTimeUtc(indexPath);

                // Assert - Index was not rebuilt
                Assert.AreEqual(firstIndexTime, secondIndexTime);
                
                source2.Dispose();
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(indexPath);
            }
        }

        [TestMethod]
        public async Task ReadRange_ReturnsPacketsInOrder()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(1000);
            var source = new FilePacketSource(tempFile);

            try
            {
                await source.OpenAsync();

                // Act
                var packets = new List<RadioPacket>();
                await foreach (var packet in source.ReadRange(TimeSpan.Zero))
                {
                    packets.Add(packet);
                }

                // Assert
                Assert.IsTrue(packets.Count > 0, "Should have packets");
                for (int i = 1; i < packets.Count; i++)
                {
                    Assert.IsTrue(packets[i].Timestamp >= packets[i - 1].Timestamp,
                        $"Packet {i} timestamp {packets[i].Timestamp} should be >= packet {i-1} timestamp {packets[i-1].Timestamp}");
                }
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task ReadRange_SeeksCorrectly()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(5000);
            var source = new FilePacketSource(tempFile);

            try
            {
                await source.OpenAsync();
                var halfDuration = source.TotalDuration / 2;

                // Act - Read from middle
                var packets = new List<RadioPacket>();
                await foreach (var packet in source.ReadRange(halfDuration))
                {
                    packets.Add(packet);
                    if (packets.Count >= 10) break; // Get first 10 packets after seek
                }

                // Assert
                Assert.IsTrue(packets.Count > 0, "Should return packets after seek position");
                
                // Verify packets are from second half
                var firstPacket = await GetFirstPacketAsync(source);
                if (firstPacket != null)
                {
                    var seekTargetTime = firstPacket.Timestamp.Add(halfDuration);
                    Assert.IsTrue(packets[0].Timestamp >= seekTargetTime, 
                        $"First packet after seek should be at or after {seekTargetTime}, was {packets[0].Timestamp}");
                }
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task ReadRange_HandlesEmptyRange()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(100);
            var source = new FilePacketSource(tempFile);

            try
            {
                await source.OpenAsync();
                var beyondEnd = source.TotalDuration + TimeSpan.FromHours(1);

                // Act
                var packets = new List<RadioPacket>();
                await foreach (var packet in source.ReadRange(beyondEnd))
                {
                    packets.Add(packet);
                }

                // Assert
                Assert.AreEqual(0, packets.Count, "Should return no packets for seek beyond end");
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task ReadPacketByIndex_ReturnsCorrectPacket()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(1000);
            var source = new FilePacketSource(tempFile);

            try
            {
                await source.OpenAsync();
                
                // Act - Read packet at index 100
                var packet = source.ReadPacketByIndex(100);

                // Assert
                Assert.IsNotNull(packet);
                Assert.IsTrue(packet.AudioPayload.Length > 0, "Packet should have audio payload");
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task ReadPacketByIndex_ThrowsOnInvalidIndex()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(100);
            var source = new FilePacketSource(tempFile);

            try
            {
                await source.OpenAsync();
                
                // Act & Assert
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
                    source.ReadPacketByIndex(-1));
                    
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
                    source.ReadPacketByIndex((int)source.TotalPackets + 1));
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        [TestMethod]
        public async Task ReadRange_HandlesCancellation()
        {
            // Arrange
            var tempFile = await SyntheticRecordingGenerator.GenerateAsync(10000);
            var source = new FilePacketSource(tempFile);
            var cts = new CancellationTokenSource();

            try
            {
                await source.OpenAsync();

                // Act
                var packets = new List<RadioPacket>();
                await foreach (var packet in source.ReadRange(TimeSpan.Zero, cts.Token))
                {
                    packets.Add(packet);
                    if (packets.Count >= 100)
                    {
                        cts.Cancel();
                    }
                }

                // Assert - Should stop early
                Assert.IsTrue(packets.Count <= 100, "Should stop reading after cancellation");
                Assert.IsTrue(packets.Count < source.TotalPackets, "Should not read all packets");
            }
            finally
            {
                source.Dispose();
                File.Delete(tempFile);
                File.Delete(Path.ChangeExtension(tempFile, ".pkidx"));
            }
        }

        private async Task<RadioPacket?> GetFirstPacketAsync(FilePacketSource source)
        {
            await foreach (var packet in source.ReadRange(TimeSpan.Zero))
            {
                return packet;
            }
            return null;
        }
    }
}
