using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Storage.Sqlite;
using Xunit;
using FluentAssertions;

namespace AeroDebrief.Tests.Storage
{
    /// <summary>
    /// Unit tests for SqlitePacketRepository.
    /// Tests packet CRUD operations, batch inserts, and streaming queries.
    /// </summary>
    public class SqlitePacketRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteUnitOfWork _unitOfWork;

        public SqlitePacketRepositoryTests()
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
        public async Task InsertBatchAsync_Should_Insert_Multiple_Packets()
        {
            // Arrange
            var packets = GenerateTestPackets(10);

            // Act
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Assert
            var count = await _unitOfWork.Packets.GetCountAsync();
            count.Should().Be(10);
        }

        [Fact]
        public async Task StreamAsync_Should_Return_Packets_In_Order()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var streamed = new List<RadioPacket>();
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(TimeSpan.Zero))
            {
                streamed.Add(packet);
            }

            // Assert
            streamed.Should().HaveCount(100);
            streamed.Should().BeInAscendingOrder(p => p.Timestamp);
        }

        [Fact]
        public async Task StreamAsync_Should_Filter_By_TimeRange()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var streamed = new List<RadioPacket>();
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)))
            {
                streamed.Add(packet);
            }

            // Assert
            streamed.Should().NotBeEmpty();
            streamed.Should().OnlyContain(p => 
                (p.Timestamp - _unitOfWork.Packets.RecordingStart).TotalSeconds >= 10 &&
                (p.Timestamp - _unitOfWork.Packets.RecordingStart).TotalSeconds <= 20);
        }

        [Fact]
        public async Task StreamAsync_Should_Filter_By_Frequency()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var frequencies = new[] { 127.5 };
            var streamed = new List<RadioPacket>();
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(TimeSpan.Zero, frequencies: frequencies))
            {
                streamed.Add(packet);
            }

            // Assert
            streamed.Should().OnlyContain(p => p.Frequency == 127.5);
        }

        [Fact]
        public async Task StreamAsync_Should_Filter_By_Player()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var players = new[] { "TestPilot0" };
            var streamed = new List<RadioPacket>();
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(TimeSpan.Zero, players: players))
            {
                streamed.Add(packet);
            }

            // Assert
            streamed.Should().OnlyContain(p => p.PlayerName == "TestPilot0");
        }

        [Fact]
        public async Task StreamAsync_Should_Handle_Large_Batch_Efficiently()
        {
            // Arrange - Insert 10,000 packets
            var packets = GenerateTestPackets(10000);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var count = 0;
            await foreach (var packet in _unitOfWork.Packets.StreamAsync(TimeSpan.Zero))
            {
                count++;
                // Verify we're getting packets progressively, not all at once
                if (count == 1000) break; // Test first 1000
            }

            // Assert
            count.Should().Be(1000);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Correct_Packet()
        {
            // Arrange
            var packets = GenerateTestPackets(10);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var packet = await _unitOfWork.Packets.GetByIdAsync(5);

            // Assert
            packet.Should().NotBeNull();
            packet!.PacketId.Should().Be(5);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_For_NonExistent()
        {
            // Act
            var packet = await _unitOfWork.Packets.GetByIdAsync(999);

            // Assert
            packet.Should().BeNull();
        }

        [Fact]
        public async Task GetCountAsync_Should_Return_Total_Packets()
        {
            // Arrange
            var packets = GenerateTestPackets(42);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var count = await _unitOfWork.Packets.GetCountAsync();

            // Assert
            count.Should().Be(42);
        }

        [Fact]
        public async Task GetCountAsync_WithTimeRange_Should_Return_Filtered_Count()
        {
            // Arrange
            var packets = GenerateTestPackets(100);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            var count = await _unitOfWork.Packets.GetCountAsync(
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20));

            // Assert
            count.Should().BeGreaterThan(0);
            count.Should().BeLessThan(100);
        }

        [Fact]
        public async Task FinalizeAsync_Should_Mark_Recording_Complete()
        {
            // Arrange
            var packets = GenerateTestPackets(10);
            await _unitOfWork.Packets.InsertBatchAsync(packets);

            // Act
            await _unitOfWork.Packets.FinalizeAsync();

            // Assert
            _unitOfWork.Packets.IsLiveRecording.Should().BeFalse();
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
                    startTime.AddSeconds(i),                        // Timestamp
                    127.5 + (i % 3) * 10,                           // Frequency
                    0,                                              // Modulation
                    0,                                              // Encryption
                    (uint)(i % 1000),                               // TransmitterUnitId
                    (ulong)(i + 1),                                 // PacketId (not used for DB id)
                    playerInfo.TransmitterGuid,                     // TransmitterGuid
                    playerInfo,                                     // PlayerData
                    48000,                                          // SampleRate
                    1,                                              // ChannelCount
                    i % 3,                                          // Coalition
                    new byte[960]                                   // AudioPayload
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
                    // Clean up WAL files
                    var walPath = _testDbPath + "-wal";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    var shmPath = _testDbPath + "-shm";
                    if (File.Exists(shmPath)) File.Delete(shmPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
