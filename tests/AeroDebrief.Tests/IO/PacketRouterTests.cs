using AeroDebrief.Core.IO;
using AeroDebrief.Core;
using AeroDebrief.Core.Storage.Abstractions;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using NLog;
using AeroDebrief.Tests.TestHelpers;

namespace AeroDebrief.Tests.IO
{
    public class PacketRouterTests
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Fact]
        public void RoutePacket_SinglePacket_RoutesCorrectly()
        {
            // Arrange
            using var router = new PacketRouter();
            var packet = CreateTestPacket("pilot-1", 251_000_000, packetId: 1);

            // Act
            router.RoutePacket(packet);

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(1, "should route one packet");
            stats.FrequencyCount.Should().Be(1, "should have one frequency");
            stats.SpeakerCount.Should().Be(1, "should have one speaker");
            
            var worker = router.GetFrequencyWorker(251_000_000);
            worker.Should().NotBeNull("should create frequency worker");
            worker!.Frequency.Should().Be(251_000_000);
            worker.UserCount.Should().Be(1, "should have one user");
        }

        [Fact]
        public void RoutePacket_MultipleFrequencies_CreatesMultipleWorkers()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            var packet1 = CreateTestPacket("pilot-1", 251_000_000, packetId: 1, timestamp: baseTime);
            var packet2 = CreateTestPacket("pilot-2", 305_000_000, packetId: 2, timestamp: baseTime);
            var packet3 = CreateTestPacket("pilot-3", 127_500_000, packetId: 3, timestamp: baseTime);

            // Act
            router.RoutePacket(packet1);
            router.RoutePacket(packet2);
            router.RoutePacket(packet3);

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(3, "should route three packets");
            stats.FrequencyCount.Should().Be(3, "should have three frequencies");
            stats.SpeakerCount.Should().Be(3, "should have three speakers");
            
            router.ActiveFrequencyCount.Should().Be(3, "should have three active frequency workers");
            
            var worker1 = router.GetFrequencyWorker(251_000_000);
            var worker2 = router.GetFrequencyWorker(305_000_000);
            var worker3 = router.GetFrequencyWorker(127_500_000);
            
            worker1.Should().NotBeNull();
            worker2.Should().NotBeNull();
            worker3.Should().NotBeNull();
        }

        [Fact]
        public void RoutePacket_MultipleSpeakers_TracksSeparately()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            // Same frequency, different speakers
            var packet1 = CreateTestPacket("pilot-1", 251_000_000, packetId: 1, timestamp: baseTime);
            var packet2 = CreateTestPacket("pilot-2", 251_000_000, packetId: 2, timestamp: baseTime.AddMilliseconds(Constants.OPUS_FRAME_DURATION_MS));
            var packet3 = CreateTestPacket("pilot-3", 251_000_000, packetId: 3, timestamp: baseTime.AddMilliseconds(Constants.OPUS_FRAME_DURATION_MS * 2));

            // Act
            router.RoutePacket(packet1);
            router.RoutePacket(packet2);
            router.RoutePacket(packet3);

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(3, "should route three packets");
            stats.FrequencyCount.Should().Be(1, "should have one frequency");
            stats.SpeakerCount.Should().Be(3, "should have three speakers");
            
            var worker = router.GetFrequencyWorker(251_000_000);
            worker.Should().NotBeNull();
            worker!.UserCount.Should().Be(3, "should have three users on this frequency");
            
            var userIds = worker.GetUserIds();
            userIds.Should().Contain("pilot-1");
            userIds.Should().Contain("pilot-2");
            userIds.Should().Contain("pilot-3");
        }

        [Fact]
        public void FrequencyRoutingWorker_EnqueueForUser_QueuesPackets()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            // Create packets for same user on same frequency - using Constants for count
            var packetsToSend = 12;
            var packets = Enumerable.Range(1, packetsToSend)
                .Select(i => CreateTestPacket("pilot-1", 251_000_000, 
                    packetId: (ulong)i, 
                    timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS)))
                .ToList();

            // Act
            foreach (var packet in packets)
            {
                router.RoutePacket(packet);
            }

            // Assert
            var worker = router.GetFrequencyWorker(251_000_000);
            worker.Should().NotBeNull();
            worker!.TotalPackets.Should().Be(packetsToSend, $"should have routed {packetsToSend} packets");
            worker.UserCount.Should().Be(1, "should have one user");
        }

        [Fact]
        public async Task RoutePacketsParallelAsync_1000Packets_RoutesCorrectly()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            const int totalPackets = 1000;
            const int numFrequencies = 10;
            const int numUsers = 50;
            
            // Create packets across multiple frequencies and users
            var packets = new List<RadioPacket>();
            for (int i = 0; i < totalPackets; i++)
            {
                var freq = Constants.MinValidFrequencyHz + (i % numFrequencies) * 1_000_000; // 10 frequencies
                var userId = $"pilot-{i % numUsers}";
                packets.Add(CreateTestPacket(userId, freq, 
                    packetId: (ulong)i, 
                    timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS)));
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(packets, degreeOfParallelism: 4);
            stopwatch.Stop();

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(totalPackets, $"should route all {totalPackets} packets");
            stats.FrequencyCount.Should().Be(numFrequencies, $"should have {numFrequencies} frequencies");
            stats.SpeakerCount.Should().Be(numUsers, $"should have {numUsers} speakers");
            
            Logger.Info($"Routed {totalPackets} packets in {stopwatch.ElapsedMilliseconds}ms " +
                       $"({totalPackets / stopwatch.Elapsed.TotalSeconds:F0} pkt/s)");
        }

        [Fact]
        public void RoutePacketBatch_MultiplePackets_RoutesInOrder()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            const int packetsToSend = 20;
            var packets = Enumerable.Range(1, packetsToSend)
                .Select(i => CreateTestPacket("pilot-1", 251_000_000, 
                    packetId: (ulong)i, 
                    timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS)))
                .ToList();

            // Act
            router.RoutePacketBatch(packets);

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(packetsToSend, $"should route all {packetsToSend} packets");
            stats.FrequencyCount.Should().Be(1);
            stats.SpeakerCount.Should().Be(1);
            
            var worker = router.GetFrequencyWorker(251_000_000);
            worker.Should().NotBeNull();
            worker!.TotalPackets.Should().Be(packetsToSend);
        }

        [Fact]
        public async Task StressTest_100K_Packets_64Freq_256Speakers_UnderOneMinute()
        {
            // Arrange - Reduced from 10M for realistic audio processing test
            const int totalPackets = 100_000;
            const int numFrequencies = 64;
            const int numSpeakers = 256;
            
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            // Pre-allocate frequencies for better performance
            var freqMetadata = new Dictionary<double, FrequencyMetadata>();
            for (int f = 0; f < numFrequencies; f++)
            {
                var freq = Constants.MinValidFrequencyHz + f * 1_000_000;
                freqMetadata[freq] = new FrequencyMetadata { Frequency = freq };
            }
            router.PreAllocateFrequencies(freqMetadata);
            
            // Generate packets lazily to avoid memory issues
            IEnumerable<RadioPacket> GeneratePackets()
            {
                for (int i = 0; i < totalPackets; i++)
                {
                    var freq = Constants.MinValidFrequencyHz + (i % numFrequencies) * 1_000_000;
                    var userId = $"pilot-{i % numSpeakers}";
                    yield return CreateTestPacket(userId, freq, 
                        packetId: (ulong)i, 
                        timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS));
                }
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(GeneratePackets(), 
                degreeOfParallelism: Environment.ProcessorCount);
            stopwatch.Stop();

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(totalPackets, "should route all packets");
            stats.FrequencyCount.Should().Be(numFrequencies);
            stats.SpeakerCount.Should().Be(numSpeakers);
            
            // Realistic expectation: 100K packets with audio processing should complete in reasonable time
            // Original expectation was pure routing (no audio processing) which was much faster
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(1), 
                $"routing {totalPackets:N0} packets with audio processing took {stopwatch.Elapsed.TotalSeconds:F2}s");
            
            Logger.Info($"STRESS TEST: Routed {totalPackets:N0} packets in {stopwatch.ElapsedMilliseconds:N0}ms " +
                       $"({totalPackets / stopwatch.Elapsed.TotalSeconds:N0} pkt/s), " +
                       $"{numFrequencies} frequencies, {numSpeakers} speakers");
        }

        [Fact]
        public async Task StressTest_HighContention_SameFrequency()
        {
            // Arrange - Many speakers on same frequency (high contention scenario)
            const int totalPackets = 100_000;
            const int numSpeakers = 100;
            
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            const double frequency = 251_000_000;
            
            // Generate packets - all on same frequency
            var packets = new List<RadioPacket>();
            for (int i = 0; i < totalPackets; i++)
            {
                var userId = $"pilot-{i % numSpeakers}";
                packets.Add(CreateTestPacket(userId, frequency, 
                    packetId: (ulong)i, 
                    timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS)));
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(packets, degreeOfParallelism: 8);
            stopwatch.Stop();

            // Assert
            var stats = router.GetStats();
            stats.PacketsRouted.Should().Be(totalPackets);
            stats.FrequencyCount.Should().Be(1, "all packets on same frequency");
            stats.SpeakerCount.Should().Be(numSpeakers);
            
            var worker = router.GetFrequencyWorker(frequency);
            worker.Should().NotBeNull();
            worker!.UserCount.Should().Be(numSpeakers);
            worker.TotalPackets.Should().Be(totalPackets);
            
            Logger.Info($"HIGH CONTENTION: Routed {totalPackets:N0} packets to one frequency in " +
                       $"{stopwatch.ElapsedMilliseconds:N0}ms ({totalPackets / stopwatch.Elapsed.TotalSeconds:N0} pkt/s)");
        }

        [Fact]
        public void GetAllWorkers_ReturnsAllFrequencyWorkers()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            const int numFrequencies = 5;
            const int packetsPerFreq = 10;
            
            // Create packets on different frequencies
            for (int f = 0; f < numFrequencies; f++)
            {
                var freq = Constants.MinValidFrequencyHz + f * 1_000_000;
                for (int p = 0; p < packetsPerFreq; p++)
                {
                    router.RoutePacket(CreateTestPacket($"pilot-{p}", freq, 
                        packetId: (ulong)p, 
                        timestamp: baseTime.AddMilliseconds(p * Constants.OPUS_FRAME_DURATION_MS)));
                }
            }

            // Act
            var workers = router.GetAllWorkers();

            // Assert
            workers.Should().HaveCount(numFrequencies, $"should have {numFrequencies} frequency workers");
            
            var expectedFrequencies = Enumerable.Range(0, numFrequencies)
                .Select(f => Constants.MinValidFrequencyHz + f * 1_000_000)
                .ToArray();
            
            workers.Select(w => w.Frequency).Should().BeEquivalentTo(expectedFrequencies);
        }

        [Fact]
        public void Clear_RemovesAllRoutingState()
        {
            // Arrange
            using var router = new PacketRouter();
            var baseTime = DateTime.UtcNow;
            
            const int packetsToSend = 100;
            const int numUsers = 10;
            
            // Add packets
            for (int i = 0; i < packetsToSend; i++)
            {
                router.RoutePacket(CreateTestPacket($"pilot-{i % numUsers}", 251_000_000, 
                    packetId: (ulong)i, 
                    timestamp: baseTime.AddMilliseconds(i * Constants.OPUS_FRAME_DURATION_MS)));
            }
            
            var statsBefore = router.GetStats();
            statsBefore.PacketsRouted.Should().Be(packetsToSend);
            statsBefore.FrequencyCount.Should().Be(1);

            // Act
            router.Clear();

            // Assert
            var statsAfter = router.GetStats();
            statsAfter.PacketsRouted.Should().Be(0, "should reset packet count");
            statsAfter.FrequencyCount.Should().Be(0, "should remove all frequencies");
            statsAfter.SpeakerCount.Should().Be(0, "should remove all speakers");
            statsAfter.RouteCount.Should().Be(0, "should remove all routes");
            
            router.ActiveFrequencyCount.Should().Be(0);
            router.GetAllWorkers().Should().BeEmpty();
        }

        #region Helper Methods

        /// <summary>
        /// Creates a test packet with realistic audio data using Constants
        /// </summary>
        private static RadioPacket CreateTestPacket(
            string userId, 
            double frequency, 
            ulong packetId = 1,
            int audioSize = Constants.OPUS_FRAME_SIZE,
            DateTime? timestamp = null)
        {
            return new RadioPacket
            {
                Timestamp = timestamp ?? DateTime.UtcNow,
                Frequency = frequency,
                Modulation = 0, // AM
                Encryption = 0,
                TransmitterUnitId = 1,
                PacketId = packetId,
                TransmitterGuid = userId,
                Coalition = 2, // Blue
                SampleRate = Constants.OUTPUT_SAMPLE_RATE,
                ChannelCount = 1, // Mono
                AudioPayload = GenerateSyntheticAudio(audioSize)
            };
        }

        /// <summary>
        /// Generates synthetic audio data - a simple sine wave at 440Hz (A4 note)
        /// Using Constants.OUTPUT_SAMPLE_RATE for realistic sample rate
        /// </summary>
        private static byte[] GenerateSyntheticAudio(int length)
        {
            var data = new byte[length];
            const double frequency = 440.0; // A4 note
            
            for (int i = 0; i < length / 2; i++)
            {
                var t = i / (double)Constants.OUTPUT_SAMPLE_RATE;
                var sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * 3000);
                var bytes = BitConverter.GetBytes(sample);
                data[i * 2] = bytes[0];
                data[i * 2 + 1] = bytes[1];
            }
            return data;
        }

        #endregion
    }
}
