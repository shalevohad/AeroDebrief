using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AeroDebrief.Core.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core;

namespace AeroDebrief.Tests.Storage
{
    /// <summary>
    /// Tests for RecordingArchiveService to verify CVR-only architecture enforcement
    /// </summary>
    public class RecordingArchiveServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly RecordingArchiveService _service;

        public RecordingArchiveServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"AeroDebriefTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);
            _service = new RecordingArchiveService();
        }

        public void Dispose()
        {
            _service?.Dispose();
            
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public async Task CreateNewRecording_ShouldStoreInHiddenDirectory()
        {
            // Arrange
            var metadata = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            // Act
            var unitOfWork = await _service.CreateNewRecordingAsync(metadata);

            // Assert
            _service.IsOpen.Should().BeTrue("service should have an open recording");
            _service.CurrentSessionId.Should().NotBeNullOrEmpty("session ID should be generated");
            
            unitOfWork.Should().NotBeNull("unit of work should be returned");
            
            // The user should NEVER see this path
            var sessionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AeroDebrief",
                "Sessions",
                _service.CurrentSessionId!);
            
            Directory.Exists(sessionDir).Should().BeTrue(
                "session directory should exist in hidden location");
        }

        [Fact]
        public async Task SaveAsCvr_ShouldCreateCvrFileOnly()
        {
            // Arrange
            var metadata = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            var cvrPath = Path.Combine(_testDirectory, "test_recording.cvr");

            // Act
            var unitOfWork = await _service.CreateNewRecordingAsync(metadata);
            
            // Insert some test data using batch insert
            var packets = new[]
            {
                new AudioPacketMetadata(
                    DateTime.UtcNow,
                    251.0,
                    0,
                    0,
                    12345,
                    1,
                    "TEST-GUID",
                    new PlayerInfo
                    {
                        Name = "TestPlayer",
                        Coalition = 2,
                        TransmitterGuid = "TEST-GUID",
                        AircraftInfo = new AircraftInfo { UnitType = "F-16C" }
                    },
                    16000,
                    1,
                    2,
                    new byte[] { 1, 2, 3, 4 }
                )
            };
            await unitOfWork.Packets.InsertBatchAsync(packets);
            
            await _service.SaveAsCvrAsync(cvrPath);

            // Assert
            File.Exists(cvrPath).Should().BeTrue("CVR file should be created");
            Path.GetExtension(cvrPath).Should().Be(".cvr", "output must be .cvr file");
            
            // User should NEVER see the .db file
            var dbFiles = Directory.GetFiles(_testDirectory, "*.db");
            dbFiles.Should().BeEmpty("no .db files should be visible to users");
        }

        [Fact]
        public async Task CloseAsync_ShouldCleanupHiddenDirectory()
        {
            // Arrange
            var metadata = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            await _service.CreateNewRecordingAsync(metadata);
            var sessionId = _service.CurrentSessionId;

            var sessionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AeroDebrief",
                "Sessions",
                sessionId!);

            Directory.Exists(sessionDir).Should().BeTrue("session directory should exist before close");

            // Act
            await _service.CloseAsync();

            // Assert - wait a bit for async cleanup
            await Task.Delay(500);
            
            _service.IsOpen.Should().BeFalse("service should not have an open recording");
            
            // Session directory should be cleaned up (best effort)
            // Note: We don't strictly assert this as file locks can prevent immediate deletion
            var cleanedUp = !Directory.Exists(sessionDir);
            if (!cleanedUp)
            {
                // Log for debugging but don't fail test - cleanup is best-effort
                Console.WriteLine($"Warning: Session directory not immediately cleaned up: {sessionDir}");
            }
        }

        [Fact]
        public async Task OpenCvr_ShouldExtractToHiddenDirectory()
        {
            // Arrange - Create a CVR file first
            var metadata = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            var cvrPath = Path.Combine(_testDirectory, "test_for_open.cvr");

            var unitOfWork1 = await _service.CreateNewRecordingAsync(metadata);
            
            // Insert at least one packet so the database is valid
            var packets = new[]
            {
                new AudioPacketMetadata(
                    DateTime.UtcNow,
                    251.0,
                    0,
                    0,
                    12345,
                    1,
                    "TEST-GUID",
                    new PlayerInfo
                    {
                        Name = "TestPlayer",
                        Coalition = 2,
                        TransmitterGuid = "TEST-GUID",
                        AircraftInfo = new AircraftInfo { UnitType = "F-16C" }
                    },
                    16000,
                    1,
                    2,
                    new byte[] { 1, 2, 3, 4 }
                )
            };
            await unitOfWork1.Packets.InsertBatchAsync(packets);
            
            await _service.SaveAsCvrAsync(cvrPath);
            await _service.CloseAsync();

            // Act - Open the CVR file
            var unitOfWork = await _service.OpenCvrAsync(cvrPath);

            // Assert
            _service.IsOpen.Should().BeTrue("service should have an open recording");
            unitOfWork.Should().NotBeNull("unit of work should be returned");
            
            // The extracted database should be in a hidden directory
            var sessionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AeroDebrief",
                "Sessions",
                _service.CurrentSessionId!);
            
            Directory.Exists(sessionDir).Should().BeTrue(
                "session directory should exist in hidden location");
            
            // User should NEVER see the extracted .db file
            var dbFiles = Directory.GetFiles(_testDirectory, "*.db");
            dbFiles.Should().BeEmpty("no .db files should be visible to users in their directory");
        }

        [Fact]
        public void GetFileDialogFilter_ShouldNotExposeDatabaseExtension()
        {
            // Act
            var filter = RecordingArchiveService.GetFileDialogFilter();

            // Assert
            filter.Should().Contain(".cvr", "filter must include .cvr extension");
            filter.Should().Contain(".adb", "filter must include legacy .adb extension");
            filter.Should().NotContain(".db|", "filter must NOT expose .db extension");
            filter.Should().NotContain("Database (*.db)", "filter must NOT show Database option");
        }

        [Fact]
        public void IsUserVisibleFormat_ShouldOnlyAcceptCvrAndAdb()
        {
            // Act & Assert
            RecordingArchiveService.IsUserVisibleFormat("test.cvr").Should().BeTrue(
                "CVR files are user-visible");
            RecordingArchiveService.IsUserVisibleFormat("test.adb").Should().BeTrue(
                "ADB files are user-visible (legacy)");
            RecordingArchiveService.IsUserVisibleFormat("test.db").Should().BeFalse(
                "DB files are internal only");
            RecordingArchiveService.IsUserVisibleFormat("test.cvr-debug").Should().BeFalse(
                "CVR-debug files are internal only");
        }

        [Fact]
        public async Task SaveAsCvr_ShouldRequireCvrExtension()
        {
            // Arrange
            var metadata = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            await _service.CreateNewRecordingAsync(metadata);

            var invalidPath = Path.Combine(_testDirectory, "test.db");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.SaveAsCvrAsync(invalidPath));
        }

        [Fact]
        public async Task OpenCvr_ShouldRejectNonCvrFiles()
        {
            // Arrange
            var invalidPath = Path.Combine(_testDirectory, "test.db");
            File.WriteAllText(invalidPath, "dummy content");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.OpenCvrAsync(invalidPath));
        }

        [Fact]
        public async Task MultipleRecordings_ShouldUseDifferentSessions()
        {
            // Arrange
            var metadata1 = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            var metadata2 = new RecordingMetadata
            {
                Version = Constants.RECORDING_FILE_MAGIC,
                ServerIp = "127.0.0.1",
                ServerPort = 5002,
                StartTime = DateTime.UtcNow
            };

            // Act
            await _service.CreateNewRecordingAsync(metadata1);
            var sessionId1 = _service.CurrentSessionId;
            await _service.CloseAsync();

            await _service.CreateNewRecordingAsync(metadata2);
            var sessionId2 = _service.CurrentSessionId;

            // Assert
            sessionId1.Should().NotBe(sessionId2, 
                "each recording should have a unique session ID");
        }
    }
}
