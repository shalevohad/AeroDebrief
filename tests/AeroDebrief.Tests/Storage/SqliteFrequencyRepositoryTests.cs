using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Storage.Sqlite;
using Xunit;
using FluentAssertions;

namespace AeroDebrief.Tests.Storage
{
    /// <summary>
    /// Unit tests for SqliteFrequencyRepository.
    /// Tests frequency statistics, aggregations, and caching.
    /// </summary>
    public class SqliteFrequencyRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteUnitOfWork _unitOfWork;

        public SqliteFrequencyRepositoryTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
            
            var metadata = new RecordingMetadata
            {
                Version = "Test-1.0",
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            _unitOfWork = new SqliteUnitOfWork(_testDbPath, createNew: true);
            _unitOfWork.InitializeAsync(metadata).Wait();
        }

        [Fact]
        public async Task RebuildStatsAsync_Should_Generate_Frequency_Statistics()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Assert
            var frequencies = await _unitOfWork.Frequencies.GetAllAsync();
            frequencies.Should().NotBeEmpty();
            frequencies.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task GetAllAsync_Should_Return_Ordered_Frequencies()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var frequencies = await _unitOfWork.Frequencies.GetAllAsync();

            // Assert
            frequencies.Should().BeInAscendingOrder(f => f.Frequency);
        }

        [Fact]
        public async Task GetAllAsync_Should_Use_Cache_On_Second_Call()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act - First call (cache miss)
            var firstCall = await _unitOfWork.Frequencies.GetAllAsync();
            
            // Act - Second call (should use cache)
            var secondCall = await _unitOfWork.Frequencies.GetAllAsync();

            // Assert
            firstCall.Should().BeEquivalentTo(secondCall);
            firstCall.Should().BeSameAs(secondCall); // Same reference = from cache
        }

        [Fact]
        public async Task InvalidateCache_Should_Force_Database_Query()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();
            
            var firstCall = await _unitOfWork.Frequencies.GetAllAsync();

            // Act - Invalidate and call again
            ((SqliteFrequencyRepository)_unitOfWork.Frequencies).InvalidateCache();
            var secondCall = await _unitOfWork.Frequencies.GetAllAsync();

            // Assert
            firstCall.Should().BeEquivalentTo(secondCall);
            firstCall.Should().NotBeSameAs(secondCall); // Different reference = new query
        }

        [Fact]
        public async Task GetByFrequencyAsync_Should_Return_Specific_Frequency()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var frequency = await _unitOfWork.Frequencies.GetByFrequencyAsync(127.5, 0);

            // Assert
            frequency.Should().NotBeNull();
            frequency!.Frequency.Should().Be(127.5);
            frequency.Modulation.Should().Be(0);
        }

        [Fact]
        public async Task GetMostActiveAsync_Should_Return_Top_Frequencies()
        {
            // Arrange
            var packets = GenerateTestPackets(1000);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var topFrequencies = await _unitOfWork.Frequencies.GetMostActiveAsync(3);

            // Assert
            topFrequencies.Should().HaveCount(3);
            topFrequencies.Should().BeInDescendingOrder(f => f.PacketCount);
        }

        [Fact]
        public async Task GetByPlayerAsync_Should_Return_Player_Frequencies()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var frequencies = await _unitOfWork.Frequencies.GetByPlayerAsync("TestPilot0");

            // Assert
            frequencies.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetByCoalitionAsync_Should_Return_Coalition_Frequencies()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var frequencies = await _unitOfWork.Frequencies.GetByCoalitionAsync(1); // Red

            // Assert
            frequencies.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetCountAsync_Should_Return_Frequency_Count()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();

            // Act
            var count = await _unitOfWork.Frequencies.GetCountAsync();

            // Assert
            count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task RebuildStatsAsync_Should_Invalidate_Cache()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();
            
            var beforeRebuild = await _unitOfWork.Frequencies.GetAllAsync();

            // Act - Add more packets and rebuild
            var morePackets = GenerateTestPackets(50);
            await _unitOfWork.Packets.InsertBatchAsync(morePackets);
            await _unitOfWork.Frequencies.RebuildStatsAsync();
            
            var afterRebuild = await _unitOfWork.Frequencies.GetAllAsync();

            // Assert
            beforeRebuild.Should().NotBeSameAs(afterRebuild); // Cache was invalidated
        }

        private AudioPacketMetadata[] GenerateTestPackets(int count)
        {
            var startTime = _unitOfWork.Packets.RecordingStart;
            var packets = new AudioPacketMetadata[count];

            for (int i = 0; i < count; i++)
            {
                var playerInfo = new PlayerInfo
                {
                    Name = $"TestPilot{i % 5}",
                    TransmitterGuid = Guid.NewGuid().ToString(),
                    Coalition = i % 3,
                    Seat = -1,
                    AllowRecord = true,
                    Position = new Position(),
                    AircraftInfo = new AircraftInfo
                    {
                        UnitType = "F-16C",
                        UnitId = (uint)(i % 5)
                    }
                };

                packets[i] = new AudioPacketMetadata(
                    startTime.AddSeconds(i),
                    127.5 + (i % 3) * 10,
                    0,
                    0,
                    (uint)(i % 1000),
                    (ulong)(i + 1),
                    playerInfo.TransmitterGuid,
                    playerInfo,
                    48000,
                    1,
                    i % 3,
                    new byte[960]
                );
            }

            return packets;
        }

        public void Dispose()
        {
            _unitOfWork?.Dispose();
            
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                    var walPath = _testDbPath + "-wal";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    var shmPath = _testDbPath + "-shm";
                    if (File.Exists(shmPath)) File.Delete(shmPath);
                }
                catch { }
            }
        }
    }
}
