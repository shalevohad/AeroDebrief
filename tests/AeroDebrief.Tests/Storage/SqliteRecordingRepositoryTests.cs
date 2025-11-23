using System;
using System.IO;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Storage.Sqlite;
using Xunit;
using FluentAssertions;

namespace AeroDebrief.Tests.Storage
{
    /// <summary>
    /// Unit tests for SqliteRecordingRepository.
    /// Tests recording metadata and statistics tracking.
    /// </summary>
    public class SqliteRecordingRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteUnitOfWork _unitOfWork;

        public SqliteRecordingRepositoryTests()
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
        public async Task GetMetadataAsync_Should_Return_Recording_Info()
        {
            // Act
            var metadata = await _unitOfWork.Recording.GetMetadataAsync();

            // Assert
            metadata.Should().NotBeNull();
            metadata.Version.Should().Be("Test-1.0");
            metadata.ServerIp.Should().Be("127.0.0.1");
            metadata.ServerPort.Should().Be(5002);
        }

        [Fact]
        public async Task UpdateMetadataAsync_Should_Update_Recording_Info()
        {
            // Arrange
            var newMetadata = new RecordingMetadata
            {
                Version = "Updated-2.0",
                ServerIp = "192.168.1.1",
                ServerPort = 6002,
                StartTime = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            await _unitOfWork.Recording.UpdateMetadataAsync(newMetadata);

            // Assert
            var retrieved = await _unitOfWork.Recording.GetMetadataAsync();
            retrieved.Version.Should().Be("Updated-2.0");
            retrieved.ServerIp.Should().Be("192.168.1.1");
            retrieved.ServerPort.Should().Be(6002);
        }

        [Fact]
        public async Task GetStatsAsync_Should_Return_Empty_Stats_For_No_Packets()
        {
            // Act
            var stats = await _unitOfWork.Recording.GetStatsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalPackets.Should().Be(0);
            stats.Duration.Should().Be(TimeSpan.Zero);
            stats.IsLive.Should().BeTrue(); // New recording is live
        }

        [Fact]
        public async Task GetStatsAsync_Should_Calculate_Stats_From_Packets()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var stats = await _unitOfWork.Recording.GetStatsAsync();

            // Assert
            stats.TotalPackets.Should().Be(100);
            stats.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            stats.IsLive.Should().BeTrue();
        }

        [Fact]
        public async Task MarkFinalizedAsync_Should_Set_Live_Flag_False()
        {
            // Arrange
            var packets = GenerateTestPackets(10);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            await _unitOfWork.Recording.MarkFinalizedAsync();

            // Assert
            var stats = await _unitOfWork.Recording.GetStatsAsync();
            stats.IsLive.Should().BeFalse();
        }

        [Fact]
        public async Task GetStatsAsync_Should_Calculate_Duration_From_RelativeMs()
        {
            // Arrange - Generate packets with specific time offsets
            var packets = GenerateTestPackets(10);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var stats = await _unitOfWork.Recording.GetStatsAsync();

            // Assert
            stats.Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(9));
            stats.Duration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task GetStatsAsync_Should_Have_LastUpdate_Timestamp()
        {
            // Arrange
            var packets = GenerateTestPackets(10);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var stats = await _unitOfWork.Recording.GetStatsAsync();

            // Assert
            stats.LastUpdate.Should().NotBeNull();
            stats.LastUpdate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task GetStatsAsync_Should_Calculate_PacketsPerSecond()
        {
            // Arrange - 100 packets over 50 seconds = 2 packets/second
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var stats = await _unitOfWork.Recording.GetStatsAsync();

            // Assert
            stats.PacketsPerSecond.Should().BeGreaterThan(0);
            stats.PacketsPerSecond.Should().BeLessOrEqualTo(2); // Approximately 1 packet per second
        }

        [Fact]
        public async Task Multiple_Stats_Queries_Should_Show_Live_Updates()
        {
            // Arrange - Initial packets
            var packets1 = GenerateTestPackets(50);
            await _unitOfWork.Packets.InsertBatchAsync(packets1);

            // Act 1
            var stats1 = await _unitOfWork.Recording.GetStatsAsync();

            // Arrange - Add more packets
            var packets2 = GenerateTestPackets(50);
            await _unitOfWork.Packets.InsertBatchAsync(packets2);

            // Act 2
            var stats2 = await _unitOfWork.Recording.GetStatsAsync();

            // Assert - Stats should reflect new packets
            stats2.TotalPackets.Should().BeGreaterThan(stats1.TotalPackets);
            stats2.Duration.Should().BeGreaterThan(stats1.Duration);
        }

        private AudioPacketMetadata[] GenerateTestPackets(int count)
        {
            var startTime = _unitOfWork.Packets.RecordingStart;
            var packets = new AudioPacketMetadata[count];

            for (int i = 0; i < count; i++)
            {
                var playerInfo = new PlayerInfo
                {
                    Name = "TestPilot",
                    TransmitterGuid = Guid.NewGuid().ToString(),
                    Coalition = 1,
                    Seat = -1,
                    AllowRecord = true,
                    Position = new Position(),
                    AircraftInfo = new AircraftInfo
                    {
                        UnitType = "F-16C",
                        UnitId = 1
                    }
                };

                packets[i] = new AudioPacketMetadata(
                    startTime.AddSeconds(i),
                    127.5,
                    0,
                    0,
                    1,
                    (ulong)(i + 1),
                    playerInfo.TransmitterGuid,
                    playerInfo,
                    48000,
                    1,
                    1,
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
