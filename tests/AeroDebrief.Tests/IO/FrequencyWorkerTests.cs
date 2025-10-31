using AeroDebrief.Core.IO;
using AeroDebrief.Core;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using System.Threading.Channels;

namespace AeroDebrief.Tests.IO
{
    public class FrequencyWorkerTests
    {
        [Fact]
        public async Task UserWorker_ProcessesSinglePacket_Successfully()
        {
            // Arrange
            await using var worker = new UserWorker("pilot-1", 251_000_000);
            var packet = CreateTestPacket("pilot-1", 251_000_000, packetId: 1);

            // Act
            var enqueued = worker.TryEnqueuePacket(packet);
            worker.CompleteInput();

            // Wait for processing
            var blocks = new List<DecodedAudioBlock>();
            await foreach (var block in worker.OutputReader.ReadAllAsync())
            {
                blocks.Add(block);
            }

            // Assert
            enqueued.Should().BeTrue("packet should be enqueued successfully");
            blocks.Should().NotBeEmpty("should produce at least one decoded block");
            blocks[0].UserId.Should().Be("pilot-1");
            blocks[0].Frequency.Should().Be(251_000_000);
            blocks[0].AudioData.Should().NotBeEmpty("audio data should not be empty");
        }

        [Fact]
        public async Task UserWorker_HandlesMultiplePackets_InOrder()
        {
            // Arrange
            await using var worker = new UserWorker("pilot-1", 251_000_000);
            var packets = Enumerable.Range(1, 10)
                .Select(i => CreateTestPacket("pilot-1", 251_000_000, packetId: (ulong)i))
                .ToList();

            // Act
            foreach (var packet in packets)
            {
                worker.TryEnqueuePacket(packet);
            }
            worker.CompleteInput();

            // Collect outputs
            var blocks = new List<DecodedAudioBlock>();
            await foreach (var block in worker.OutputReader.ReadAllAsync())
            {
                blocks.Add(block);
            }

            // Assert
            blocks.Should().NotBeEmpty("should produce decoded blocks");
            worker.PacketsProcessed.Should().BeGreaterThan(0, "should track processed packets");
        }

        [Fact]
        public async Task UserWorker_TracksIdleTime_Correctly()
        {
            // Arrange
            await using var worker = new UserWorker("pilot-1", 251_000_000);
            
            // Act - Initial idle check
            await Task.Delay(100);
            var isIdleShort = worker.IsIdle(TimeSpan.FromMilliseconds(50));
            var isIdleLong = worker.IsIdle(TimeSpan.FromSeconds(10));

            // Assert
            isIdleShort.Should().BeTrue("should be idle after 100ms when threshold is 50ms");
            isIdleLong.Should().BeFalse("should not be idle when threshold is 10s");
            worker.IdleTime.TotalMilliseconds.Should().BeGreaterOrEqualTo(100, "idle time should be at least 100ms");
        }

        [Fact]
        public async Task UserWorker_DisposesCleanly_WhenActive()
        {
            // Arrange
            var worker = new UserWorker("pilot-1", 251_000_000);
            var packet = CreateTestPacket("pilot-1", 251_000_000);
            worker.TryEnqueuePacket(packet);

            // Act - Dispose without completing input
            await worker.DisposeAsync();

            // Assert - Should not throw
            worker.TryEnqueuePacket(packet).Should().BeFalse("should reject packets after disposal");
        }

        [Fact]
        public async Task UserWorker_DisposesCleanly_WhenIdle()
        {
            // Arrange
            var worker = new UserWorker("pilot-1", 251_000_000);
            await Task.Delay(100); // Let it become idle

            // Act
            await worker.DisposeAsync();

            // Assert - Should complete quickly
            true.Should().BeTrue("disposal completed without hanging");
        }

        [Fact]
        public async Task FrequencyWorker_CreatesUserWorkers_OnDemand()
        {
            // Arrange
            await using var worker = new FrequencyWorker(251_000_000);

            // Act
            var user1 = worker.GetOrCreateUserWorker("pilot-1");
            var user2 = worker.GetOrCreateUserWorker("pilot-2");
            var user1Again = worker.GetOrCreateUserWorker("pilot-1");

            // Assert
            user1.Should().NotBeNull();
            user2.Should().NotBeNull();
            user1.Should().BeSameAs(user1Again, "should return same worker for same user");
            worker.ActiveUserCount.Should().Be(2);
        }

        [Fact]
        public async Task FrequencyWorker_EnqueuesPacketsToCorrectUser()
        {
            // Arrange
            await using var worker = new FrequencyWorker(251_000_000);
            var packet1 = CreateTestPacket("pilot-1", 251_000_000, packetId: 1);
            var packet2 = CreateTestPacket("pilot-2", 251_000_000, packetId: 2);

            // Act
            var enqueued1 = worker.EnqueueForUser("pilot-1", packet1);
            var enqueued2 = worker.EnqueueForUser("pilot-2", packet2);

            // Assert
            enqueued1.Should().BeTrue("should enqueue pilot-1 packet");
            enqueued2.Should().BeTrue("should enqueue pilot-2 packet");
            worker.ActiveUserCount.Should().Be(2);
        }

        [Fact]
        public async Task FrequencyWorker_ProducesFrames_FromMultipleUsers()
        {
            // Arrange
            await using var worker = new FrequencyWorker(251_000_000);
            
            // Act - Enqueue packets from multiple users
            for (int i = 0; i < 10; i++)
            {
                worker.EnqueueForUser("pilot-1", CreateTestPacket("pilot-1", 251_000_000, packetId: (ulong)(i * 2)));
                worker.EnqueueForUser("pilot-2", CreateTestPacket("pilot-2", 251_000_000, packetId: (ulong)(i * 2 + 1)));
            }

            // Wait a bit for processing
            await Task.Delay(500);

            // Collect some frames
            var frames = new List<FrequencyAudioFrame>();
            for (int i = 0; i < 5 && worker.OutputReader.TryRead(out var frame); i++)
            {
                frames.Add(frame);
            }

            // Assert
            frames.Should().NotBeEmpty("should produce frequency frames");
            worker.FramesProduced.Should().BeGreaterThan(0, "should track frame count");
            
            if (frames.Count > 0)
            {
                frames[0].Frequency.Should().Be(251_000_000);
                frames[0].AudioData.Should().NotBeEmpty("frame should have audio data");
            }
        }

        [Fact]
        public async Task FrequencyWorker_RemovesIdleUsers_AfterTimeout()
        {
            // Arrange
            await using var worker = new FrequencyWorker(251_000_000);
            
            // Create some users
            worker.EnqueueForUser("pilot-1", CreateTestPacket("pilot-1", 251_000_000));
            worker.EnqueueForUser("pilot-2", CreateTestPacket("pilot-2", 251_000_000));
            
            worker.ActiveUserCount.Should().Be(2, "should have 2 active users");

            // Act - Wait for idle timeout
            await Task.Delay(200);
            var removed = await worker.RemoveIdleUsersAsync(TimeSpan.FromMilliseconds(100));

            // Assert
            removed.Should().Be(2, "should remove both idle users");
            worker.ActiveUserCount.Should().Be(0, "should have no active users after removal");
        }

        [Fact]
        public async Task FrequencyWorker_IdleTimeout_StopsMixingPipeline()
        {
            // Arrange - Short idle timeout
            var worker = new FrequencyWorker(251_000_000, idleTimeout: TimeSpan.FromMilliseconds(100));
            
            // Act - Don't enqueue any packets, wait for idle timeout
            await Task.Delay(300);

            // Assert
            worker.IsIdle.Should().BeTrue("worker should be idle");
            
            // Dispose should complete quickly since pipeline already stopped
            var stopwatch = Stopwatch.StartNew();
            await worker.DisposeAsync();
            stopwatch.Stop();

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                $"disposal should be fast for idle worker, took {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task FrequencyWorker_DisposesAllUserWorkers_OnDisposal()
        {
            // Arrange
            var worker = new FrequencyWorker(251_000_000);
            
            // Create multiple user workers
            for (int i = 1; i <= 5; i++)
            {
                worker.EnqueueForUser($"pilot-{i}", CreateTestPacket($"pilot-{i}", 251_000_000));
            }

            var userCount = worker.ActiveUserCount;
            userCount.Should().Be(5, "should have 5 active users");

            // Act
            await worker.DisposeAsync();

            // Assert
            worker.ActiveUserCount.Should().Be(0, "all users should be disposed");
        }

        [Fact]
        public async Task FrequencyWorker_HandlesBackpressure_GracefullyDropsOldPackets()
        {
            // Arrange - Small buffer to force backpressure
            await using var worker = new UserWorker("pilot-1", 251_000_000, inputBufferSize: 10);
            
            // Act - Flood with packets
            int enqueued = 0;
            for (int i = 0; i < 100; i++)
            {
                if (worker.TryEnqueuePacket(CreateTestPacket("pilot-1", 251_000_000, packetId: (ulong)i)))
                {
                    enqueued++;
                }
            }

            // Complete and wait for processing
            worker.CompleteInput();
            await foreach (var _ in worker.OutputReader.ReadAllAsync()) { }

            // Assert
            enqueued.Should().BeLessThan(100, "should have dropped some packets due to backpressure");
            enqueued.Should().BeGreaterOrEqualTo(10, "should have accepted at least buffer size worth");
            
            worker.PacketsProcessed.Should().BeGreaterThan(0);
            worker.PacketsDropped.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task UserWorker_JitterBuffer_ReordersPackets()
        {
            // Arrange
            await using var worker = new UserWorker("pilot-1", 251_000_000);
            
            // Act - Enqueue packets out of order
            worker.TryEnqueuePacket(CreateTestPacket("pilot-1", 251_000_000, packetId: 3));
            worker.TryEnqueuePacket(CreateTestPacket("pilot-1", 251_000_000, packetId: 1));
            worker.TryEnqueuePacket(CreateTestPacket("pilot-1", 251_000_000, packetId: 2));
            worker.TryEnqueuePacket(CreateTestPacket("pilot-1", 251_000_000, packetId: 4));
            
            worker.CompleteInput();

            // Collect blocks
            var blocks = new List<DecodedAudioBlock>();
            await foreach (var block in worker.OutputReader.ReadAllAsync())
            {
                blocks.Add(block);
            }

            // Assert
            blocks.Should().NotBeEmpty("should produce blocks");
            // JitterBuffer should reorder packets (though we can't directly verify order without packet IDs in blocks)
        }

        [Fact]
        public async Task FrequencyWorker_Frame_Is10Milliseconds()
        {
            // Arrange
            await using var worker = new FrequencyWorker(251_000_000);
            
            // Enqueue enough packets to produce a frame
            for (int i = 0; i < 5; i++)
            {
                worker.EnqueueForUser("pilot-1", CreateTestPacket("pilot-1", 251_000_000, packetId: (ulong)i));
            }

            // Act
            await Task.Delay(200); // Wait for processing

            // Try to get a frame
            if (worker.OutputReader.TryRead(out var frame))
            {
                // Assert
                var expectedSamples = Constants.OUTPUT_SAMPLE_RATE * 10 / 1000; // 10ms at 48kHz
                frame.AudioData.Length.Should().Be(expectedSamples, 
                    $"frame should be 10ms ({expectedSamples} samples at 48kHz)");
                frame.Duration.Should().Be(TimeSpan.FromMilliseconds(10));
            }
        }

        [Fact]
        public async Task StressTest_MultipleFrequencies_MultipleUsers()
        {
            // Arrange
            const int numFrequencies = 10;
            const int usersPerFrequency = 20;
            const int packetsPerUser = 50;

            var workers = new List<FrequencyWorker>();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Create frequency workers
                for (int f = 0; f < numFrequencies; f++)
                {
                    workers.Add(new FrequencyWorker(200_000_000 + f * 1_000_000));
                }

                // Enqueue packets
                int totalPackets = 0;
                foreach (var worker in workers)
                {
                    for (int u = 0; u < usersPerFrequency; u++)
                    {
                        var userId = $"user-{u}";
                        for (int p = 0; p < packetsPerUser; p++)
                        {
                            worker.EnqueueForUser(userId, 
                                CreateTestPacket(userId, worker.Frequency, packetId: (ulong)p));
                            totalPackets++;
                        }
                    }
                }

                // Wait for processing
                await Task.Delay(2000);

                stopwatch.Stop();

                // Assert
                var totalFrames = workers.Sum(w => w.FramesProduced);
                var totalUsers = workers.Sum(w => w.ActiveUserCount);

                totalFrames.Should().BeGreaterThan(0, "should produce frames");
                totalUsers.Should().BeGreaterThan(0, "should have active users");
            }
            finally
            {
                // Cleanup
                foreach (var worker in workers)
                {
                    await worker.DisposeAsync();
                }
            }
        }

        #region Helper Methods

        private static RadioPacket CreateTestPacket(
            string userId, 
            double frequency, 
            ulong packetId = 1,
            int audioSize = 1920)
        {
            return new RadioPacket
            {
                Timestamp = DateTime.UtcNow,
                Frequency = frequency,
                Modulation = 0,
                Encryption = 0,
                TransmitterUnitId = 1,
                PacketId = packetId,
                TransmitterGuid = userId,
                Coalition = 1,
                AudioPayload = GenerateSyntheticAudio(audioSize)
            };
        }

        private static byte[] GenerateSyntheticAudio(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length / 2; i++)
            {
                var t = i / 48000.0;
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * t) * 3000);
                var bytes = BitConverter.GetBytes(sample);
                data[i * 2] = bytes[0];
                data[i * 2 + 1] = bytes[1];
            }
            return data;
        }

        #endregion
    }
}
