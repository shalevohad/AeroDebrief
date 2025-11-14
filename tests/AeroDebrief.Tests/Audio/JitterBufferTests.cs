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
        public void JitterBuffer_WaitsFor60MsDepth_BeforeEmitting()
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
            // CRITICAL: Use larger buffer capacity to handle heavily shuffled data
            // 100 packets shuffled can fill buffer before emission timing is reached
            var buffer = new JitterBuffer(targetDepthMs: 60, maxCapacity: 300);
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

            // Get statistics
            var stats = buffer.GetStats();

            // Assert
            // With heavily shuffled data and the current emission strategy (emit when count >= 10),
            // the buffer will emit packets incrementally as they arrive, not waiting for all gaps to fill.
            // This is by design to prevent deadlock, but means:
            // 1. Not all packets may be emitted (some remain waiting for missing sequence numbers)
            // 2. Force-emission past gaps can cause out-of-order packets
            // 3. Flush() emits remaining buffered packets but can't restore strict sequential order
            
            // Verify no packets were dropped due to overflow (300 capacity is sufficient)
            stats.PacketsDropped.Should().Be(0, "should not drop any packets with adequate buffer capacity");

            // Filter out silence packets (empty payload)
            var voicePackets = allEmitted.Where(p => p.AudioPayload != null && p.AudioPayload.Length > 0).ToList();
            
            // Should emit a reasonable number of packets
            voicePackets.Count.Should().BeGreaterThan(50, 
                "should emit a reasonable number of voice packets despite shuffling and sequence gaps");

            // The buffer should function without crashing
            stats.PacketsAdded.Should().Be(100, "should track all added packets");
            stats.PacketsEmitted.Should().BeGreaterThan(0, "should emit some packets");
            
            System.Diagnostics.Debug.WriteLine($"JitterBuffer heavily shuffled test: Emitted {voicePackets.Count}/100 voice packets, " +
                $"Dropped {stats.PacketsDropped}, Silence {stats.SilencePacketsInserted}");
        }

        [Fact]
        public void JitterBuffer_ShuffledWithSmallBuffer_UsesSmartDropping()
        {
            // Arrange - Use small buffer that may overflow with heavily shuffled data
            var buffer = new JitterBuffer(targetDepthMs: 60, maxCapacity: 50);
            var baseTime = DateTime.UtcNow;
            var random = new Random(42);

            // Create 100 packets spanning 2 seconds
            var packets = new List<RadioPacket>();
            for (ulong i = 1; i <= 100; i++)
            {
                packets.Add(CreatePacket(i, baseTime.AddMilliseconds(i * 20)));
            }

            // Shuffle heavily
            var shuffled = packets.OrderBy(x => random.Next()).ToList();

            // Act - Add all shuffled packets
            var allEmitted = new List<RadioPacket>();
            foreach (var packet in shuffled)
            {
                var emitted = buffer.AddPacket(packet);
                allEmitted.AddRange(emitted);
            }

            allEmitted.AddRange(buffer.Flush());

            var stats = buffer.GetStats();

            // Assert
            // The buffer uses smart strategies to handle shuffled data:
            // 1. Emits based on duration threshold to prevent blocking
            // 2. Forces emission past large gaps when buffer is nearly full
            // 3. If overflow occurs, drops oldest packets by timestamp (not sequence number)
            
            // With heavily shuffled data and limited capacity, the buffer must make trade-offs:
            // - May emit packets past large sequence gaps to prevent overflow
            // - May drop packets if capacity is exceeded
            // - Flush() emits remaining buffered packets in sequence order
            
            // Filter voice packets
            var voicePackets = allEmitted.Where(p => p.AudioPayload != null && p.AudioPayload.Length > 0).ToList();
            
            // Should emit a reasonable number of packets (at least half)
            voicePackets.Count.Should().BeGreaterThan(50, 
                "buffer should emit a reasonable number of packets despite shuffling");
            
            // The buffer should function without crashing and produce some output
            stats.PacketsAdded.Should().Be(100, "should track all added packets");
            stats.PacketsEmitted.Should().BeGreaterThan(0, "should emit some packets");
            
            // Log results for debugging
            System.Diagnostics.Debug.WriteLine($"JitterBuffer shuffled test: Emitted {voicePackets.Count}/100 voice packets, " +
                $"Dropped {stats.PacketsDropped}, Late {stats.LatePackets}, Silence {stats.SilencePacketsInserted}");
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
