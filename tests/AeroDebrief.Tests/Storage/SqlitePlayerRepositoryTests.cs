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
    /// Unit tests for SqlitePlayerRepository.
    /// Tests player statistics, aggregations, and caching.
    /// </summary>
    public class SqlitePlayerRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteUnitOfWork _unitOfWork;

        public SqlitePlayerRepositoryTests()
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
        public async Task RebuildStatsAsync_Should_Generate_Player_Statistics()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            await _unitOfWork.Players.RebuildStatsAsync();

            // Assert
            var players = await _unitOfWork.Players.GetAllAsync();
            players.Should().NotBeEmpty();
            players.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task GetAllAsync_Should_Return_Ordered_Players()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var players = await _unitOfWork.Players.GetAllAsync();

            // Assert
            players.Should().BeInAscendingOrder(p => p.PlayerName);
        }

        [Fact]
        public async Task GetAllAsync_Should_Use_Cache_On_Second_Call()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var firstCall = await _unitOfWork.Players.GetAllAsync();
            var secondCall = await _unitOfWork.Players.GetAllAsync();

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
            await _unitOfWork.Players.RebuildStatsAsync();
            
            var firstCall = await _unitOfWork.Players.GetAllAsync();

            // Act
            ((SqlitePlayerRepository)_unitOfWork.Players).InvalidateCache();
            var secondCall = await _unitOfWork.Players.GetAllAsync();

            // Assert
            firstCall.Should().BeEquivalentTo(secondCall);
            firstCall.Should().NotBeSameAs(secondCall); // Different reference = new query
        }

        [Fact]
        public async Task GetByNameAsync_Should_Return_Specific_Player()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var player = await _unitOfWork.Players.GetByNameAsync("TestPilot0");

            // Assert
            player.Should().NotBeNull();
            player!.PlayerName.Should().Be("TestPilot0");
        }

        [Fact]
        public async Task GetMostActiveAsync_Should_Return_Top_Players()
        {
            // Arrange
            var packets = GenerateTestPackets(1000);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var topPlayers = await _unitOfWork.Players.GetMostActiveAsync(3);

            // Assert
            topPlayers.Should().HaveCount(3);
            topPlayers.Should().BeInDescendingOrder(p => p.TransmissionCount);
        }

        [Fact]
        public async Task GetByCoalitionAsync_Should_Return_Coalition_Players()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var players = await _unitOfWork.Players.GetByCoalitionAsync(1); // Red

            // Assert
            players.Should().NotBeEmpty();
            players.Should().OnlyContain(p => p.Coalition == 1);
        }

        [Fact]
        public async Task GetByFrequencyAsync_Should_Return_Frequency_Players()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var players = await _unitOfWork.Players.GetByFrequencyAsync(127.5);

            // Assert
            players.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetByAircraftTypeAsync_Should_Return_Aircraft_Players()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var players = await _unitOfWork.Players.GetByAircraftTypeAsync("F-16C");

            // Assert
            players.Should().NotBeEmpty();
            players.Should().OnlyContain(p => p.UnitType == "F-16C");
        }

        [Fact]
        public async Task GetCountAsync_Should_Return_Player_Count()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var count = await _unitOfWork.Players.GetCountAsync();

            // Assert
            count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task PlayerStats_Should_Include_Frequency_List()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();

            // Act
            var player = await _unitOfWork.Players.GetByNameAsync("TestPilot0");

            // Assert
            player.Should().NotBeNull();
            player!.Frequencies.Should().NotBeEmpty();
            player.Frequencies.Should().Contain(127.5);
        }

        [Fact]
        public async Task RebuildStatsAsync_Should_Invalidate_Cache()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);
            await _unitOfWork.Players.RebuildStatsAsync();
            
            var beforeRebuild = await _unitOfWork.Players.GetAllAsync();

            // Act
            var morePackets = GenerateTestPackets(50);
            await _unitOfWork.Packets.InsertBatchAsync(morePackets);
            await _unitOfWork.Players.RebuildStatsAsync();
            
            var afterRebuild = await _unitOfWork.Players.GetAllAsync();

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
