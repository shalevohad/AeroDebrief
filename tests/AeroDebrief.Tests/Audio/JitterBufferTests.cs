using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using Xunit;
using FluentAssertions;
using System.Diagnostics;

namespace AeroDebrief.Tests.Audio
{
    public class JitterBufferTests
    {
        [Fact]
        public void JitterBuffer_ReordersOutOfSequencePackets()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            // Create packets out of order
            var packet3 = CreatePacket(3, baseTime.AddMilliseconds(60));
            var packet1 = CreatePacket(1, baseTime);
            var packet2 = CreatePacket(2, baseTime.AddMilliseconds(40));
            var packet4 = CreatePacket(4, baseTime.AddMilliseconds(80));

            // Act - Add in shuffled order
            var emitted1 = buffer.AddPacket(packet3).ToList();
            var emitted2 = buffer.AddPacket(packet1).ToList();
            var emitted3 = buffer.AddPacket(packet2).ToList();
            var emitted4 = buffer.AddPacket(packet4).ToList();

            // Collect all emitted packets
            var allEmitted = emitted1.Concat(emitted2).Concat(emitted3).Concat(emitted4).ToList();

            // Assert - Should emit in order once buffer fills
            allEmitted.Should().NotBeEmpty("should emit packets");
            
            // Verify order
            for (int i = 1; i < allEmitted.Count; i++)
            {
                allEmitted[i].PacketId.Should().BeGreaterOrEqualTo(allEmitted[i - 1].PacketId,
                    $"packet {i} should be >= previous");
            }
        }

        [Fact]
        public void JitterBuffer_WaitsFor60msDepth_BeforeEmitting()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            // Act - Add packets that span < 60ms
            var emitted1 = buffer.AddPacket(CreatePacket(1, baseTime)).ToList();
            var emitted2 = buffer.AddPacket(CreatePacket(2, baseTime.AddMilliseconds(20))).ToList();
            var emitted3 = buffer.AddPacket(CreatePacket(3, baseTime.AddMilliseconds(40))).ToList();

            // Assert - Should not emit yet (< 60ms)
            (emitted1.Count + emitted2.Count + emitted3.Count).Should().Be(0, 
                "should not emit before 60ms target depth");

            // Act - Add packet that crosses 60ms threshold
            var emitted4 = buffer.AddPacket(CreatePacket(4, baseTime.AddMilliseconds(65))).ToList();

            // Assert - Should start emitting now
            emitted4.Should().NotBeEmpty("should emit after reaching 60ms depth");
        }

        [Fact]
        public void JitterBuffer_DropsLatePackets()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            // Add packets 1-5 and let them emit
            for (ulong i = 1; i <= 5; i++)
            {
                buffer.AddPacket(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
            }

            // Trigger emission
            buffer.AddPacket(CreatePacket(6, baseTime.AddMilliseconds(120)));
            
            var initialStats = buffer.GetStats();
            var initialEmitted = initialStats.PacketsEmitted;

            // Act - Try to add late packet (id=2, already emitted)
            var lateEmitted = buffer.AddPacket(CreatePacket(2, baseTime.AddMilliseconds(40))).ToList();

            // Assert
            var finalStats = buffer.GetStats();
            lateEmitted.Should().BeEmpty("late packet should not be emitted");
            finalStats.LatePackets.Should().Be(1, "should track late packet");
            finalStats.PacketsEmitted.Should().Be(initialEmitted, "emit count should not change");
        }

        [Fact]
        public void JitterBuffer_HandlesMissingPackets_EmitsWithGap()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            // Act - Add packets with gap (skip packet 3)
            buffer.AddPacket(CreatePacket(1, baseTime));
            buffer.AddPacket(CreatePacket(2, baseTime.AddMilliseconds(40)));
            // Skip 3
            buffer.AddPacket(CreatePacket(4, baseTime.AddMilliseconds(80)));
            buffer.AddPacket(CreatePacket(5, baseTime.AddMilliseconds(100)));

            var allEmitted = new List<RadioPacket>();
            
            // Keep adding packets to force emission
            for (ulong i = 6; i <= 20; i++)
            {
                var emitted = buffer.AddPacket(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
                allEmitted.AddRange(emitted);
            }

            // Assert - Should eventually emit despite gap
            allEmitted.Should().NotBeEmpty("should emit packets despite gap");
        }

        [Fact]
        public void JitterBuffer_Flush_EmitsAllBuffered()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            // Add packets that won't emit naturally (< 60ms)
            buffer.AddPacket(CreatePacket(1, baseTime));
            buffer.AddPacket(CreatePacket(2, baseTime.AddMilliseconds(20)));
            buffer.AddPacket(CreatePacket(3, baseTime.AddMilliseconds(40)));

            var statsBeforeFlush = buffer.GetStats();
            statsBeforeFlush.CurrentDepth.Should().BeGreaterThan(0, "buffer should have packets");

            // Act
            var flushed = buffer.Flush().ToList();

            // Assert
            flushed.Should().HaveCount(3, "should flush all buffered packets");
            buffer.GetStats().CurrentDepth.Should().Be(0, "buffer should be empty after flush");

            // Verify order
            flushed[0].PacketId.Should().Be(1);
            flushed[1].PacketId.Should().Be(2);
            flushed[2].PacketId.Should().Be(3);
        }

        [Fact]
        public void JitterBuffer_Statistics_TrackCorrectly()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60, maxCapacity: 10);
            var baseTime = DateTime.UtcNow;

            // Act - Add packets
            for (ulong i = 1; i <= 5; i++)
            {
                buffer.AddPacket(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
            }

            // Force emission
            for (ulong i = 6; i <= 15; i++)
            {
                buffer.AddPacket(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
            }

            var stats = buffer.GetStats();

            // Assert
            stats.PacketsAdded.Should().Be(15, "should track packets added");
            stats.PacketsEmitted.Should().BeGreaterThan(0, "should have emitted packets");
            stats.BufferDurationMs.Should().BeGreaterOrEqualTo(0, "should track buffer duration");
        }

        [Fact]
        public void JitterBuffer_MaxCapacity_DropsOldest()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60, maxCapacity: 5);
            var baseTime = DateTime.UtcNow;

            // Act - Add more packets than capacity (but keep duration < 60ms to prevent natural emission)
            for (ulong i = 1; i <= 10; i++)
            {
                buffer.AddPacket(CreatePacket(i, baseTime.AddMilliseconds(i * 5))); // Only 50ms total
            }

            var stats = buffer.GetStats();

            // Assert
            stats.PacketsDropped.Should().BeGreaterThan(0, "should have dropped packets due to capacity");
            stats.CurrentDepth.Should().BeLessOrEqualTo(5, "depth should not exceed max capacity");
        }

        [Fact]
        public void JitterBuffer_Reset_ClearsState()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;

            buffer.AddPacket(CreatePacket(1, baseTime));
            buffer.AddPacket(CreatePacket(2, baseTime.AddMilliseconds(40)));

            buffer.GetStats().CurrentDepth.Should().BeGreaterThan(0, "buffer should have packets");

            // Act
            buffer.Reset();

            // Assert
            var stats = buffer.GetStats();
            stats.CurrentDepth.Should().Be(0, "depth should be 0");
            stats.PacketsAdded.Should().Be(0, "stats should be reset");
            stats.PacketsEmitted.Should().Be(0, "stats should be reset");
        }

        [Fact]
        public void JitterBuffer_HeavilyShuffled_ProducesOrderedOutput()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;
            var random = new Random(42); // Seed for reproducibility

            // Create 100 packets
            var packets = new List<RadioPacket>();
            for (ulong i = 1; i <= 100; i++)
            {
                packets.Add(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
            }

            // Shuffle packets heavily
            var shuffled = packets.OrderBy(x => random.Next()).ToList();

            // Act - Add all shuffled packets
            var allEmitted = new List<RadioPacket>();
            foreach (var packet in shuffled)
            {
                var emitted = buffer.AddPacket(packet);
                allEmitted.AddRange(emitted);
            }

            // Flush remaining
            allEmitted.AddRange(buffer.Flush());

            // Assert - All packets should be emitted in order
            allEmitted.Should().HaveCount(100, "should emit all packets");

            for (int i = 1; i < allEmitted.Count; i++)
            {
                allEmitted[i].PacketId.Should().BeGreaterThan(allEmitted[i - 1].PacketId,
                    $"packet at index {i} should be > previous");
            }
        }

        [Fact]
        public void JitterBuffer_SimulatedNetworkJitter_HandlesCorrectly()
        {
            // Arrange
            var buffer = new JitterBuffer(targetDepthMs: 60);
            var baseTime = DateTime.UtcNow;
            var random = new Random(123);

            // Simulate network: packets arrive out of order with variable delay
            var packets = new List<(RadioPacket packet, int delayMs)>();
            for (ulong i = 1; i <= 50; i++)
            {
                // Add random jitter: -20ms to +40ms
                int jitter = random.Next(-20, 40);
                packets.Add((CreatePacket(i, baseTime.AddMilliseconds(i * 40)), jitter));
            }

            // Sort by arrival time (sent time + jitter)
            var arrivals = packets
                .Select(p => (p.packet, arrivalTime: p.packet.Timestamp.AddMilliseconds(p.delayMs)))
                .OrderBy(x => x.arrivalTime)
                .ToList();

            // Act - Process packets in arrival order
            var allEmitted = new List<RadioPacket>();
            foreach (var (packet, _) in arrivals)
            {
                var emitted = buffer.AddPacket(packet);
                allEmitted.AddRange(emitted);
            }

            allEmitted.AddRange(buffer.Flush());

            // Assert
            allEmitted.Should().HaveCount(50, "should emit all packets");

            // Verify sequential order
            for (int i = 1; i < allEmitted.Count; i++)
            {
                allEmitted[i].PacketId.Should().BeGreaterOrEqualTo(allEmitted[i - 1].PacketId,
                    "output should be in sequence");
            }
        }

        #region Helper Methods

        private static RadioPacket CreatePacket(ulong id, DateTime timestamp, int audioSize = 960)
        {
            return new RadioPacket
            {
                PacketId = id,
                Timestamp = timestamp,
                Frequency = 251_000_000,
                Modulation = 0,
                Encryption = 0,
                TransmitterUnitId = 1,
                TransmitterGuid = "test-user",
                Coalition = 1,
                AudioPayload = new byte[audioSize]
            };
        }

        #endregion
    }
}
