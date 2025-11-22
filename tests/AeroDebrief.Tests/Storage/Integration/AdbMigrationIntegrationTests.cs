using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Xunit;
using FluentAssertions;

namespace AeroDebrief.Tests.Storage.Integration
{
    /// <summary>
    /// Integration tests for ADB to SQLite migration pipeline.
    /// Tests the complete conversion process from legacy ADB files to modern SQLite databases.
    /// </summary>
    public class AdbMigrationIntegrationTests : IDisposable
    {
        private readonly string _testAdbPath;
        private readonly string _testDbPath;
        private readonly string _testCvrPath;

        public AdbMigrationIntegrationTests()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"adb_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            
            _testAdbPath = Path.Combine(tempDir, "test.adb");
            _testDbPath = Path.Combine(tempDir, "test.db");
            _testCvrPath = Path.Combine(tempDir, "test.cvr");
        }

        [Fact(Skip = "Requires real ADB file - manual testing only")]
        public async Task ConvertAsync_Should_Convert_ADB_To_SQLite()
        {
            // Note: This test requires a real ADB file
            // For automated testing, we would need to generate a synthetic ADB file
            
            // Arrange
            // Create or copy a test ADB file to _testAdbPath
            
            var converter = new AdbToDatabaseConverter();

            // Act
            var result = await converter.ConvertAsync(_testAdbPath, _testDbPath);

            // Assert
            result.Success.Should().BeTrue();
            result.TotalPackets.Should().BeGreaterThan(0);
            File.Exists(_testDbPath).Should().BeTrue();
        }

        [Fact(Skip = "Requires real ADB file - manual testing only")]
        public async Task ConvertAsync_With_CVR_Should_Create_Compressed_File()
        {
            // Arrange
            var converter = new AdbToDatabaseConverter();

            // Act
            var result = await converter.ConvertAsync(
                _testAdbPath, 
                _testDbPath, 
                compressToCvr: true);

            // Assert
            result.Success.Should().BeTrue();
            File.Exists(_testCvrPath).Should().BeTrue();
            
            // CVR should be smaller than DB
            var cvrSize = new FileInfo(_testCvrPath).Length;
            var dbSize = new FileInfo(_testDbPath).Length;
            cvrSize.Should().BeLessThan(dbSize);
        }

        [Fact(Skip = "Requires real files - manual testing only")]
        public async Task RecordingFileLoader_Should_Open_All_Formats()
        {
            // Arrange
            // Requires test files: .adb, .db, .cvr

            // Act & Assert - ADB
            var (adbUow, adbTemp) = await RecordingFileLoader.OpenAsync(_testAdbPath);
            adbUow.Should().NotBeNull();
            var adbCount = await adbUow.Packets.GetCountAsync();
            adbCount.Should().BeGreaterThan(0);
            adbUow.Dispose();
            RecordingFileLoader.Cleanup(adbTemp);

            // Act & Assert - DB
            var (dbUow, dbTemp) = await RecordingFileLoader.OpenAsync(_testDbPath);
            dbUow.Should().NotBeNull();
            var dbCount = await dbUow.Packets.GetCountAsync();
            dbCount.Should().Be(adbCount); // Same packets
            dbUow.Dispose();
            RecordingFileLoader.Cleanup(dbTemp);

            // Act & Assert - CVR
            var (cvrUow, cvrTemp) = await RecordingFileLoader.OpenAsync(_testCvrPath);
            cvrUow.Should().NotBeNull();
            var cvrCount = await cvrUow.Packets.GetCountAsync();
            cvrCount.Should().Be(adbCount); // Same packets
            cvrUow.Dispose();
            RecordingFileLoader.Cleanup(cvrTemp);
        }

        [Fact]
        public void GetFileFilters_Should_Include_All_Formats()
        {
            // Act
            var filters = RecordingFileLoader.GetFileFilters();

            // Assert
            filters.Should().Contain(".cvr");
            filters.Should().Contain(".adb");
            filters.Should().Contain(".db");
        }

        [Fact]
        public void GetSupportedExtensions_Should_Return_All_Extensions()
        {
            // Act
            var extensions = RecordingFileLoader.GetSupportedExtensions();

            // Assert
            extensions.Should().Contain(".cvr");
            extensions.Should().Contain(".adb");
            extensions.Should().Contain(".db");
            extensions.Should().HaveCount(3);
        }

        [Fact]
        public void CvrFormat_IsCvrFile_Should_Detect_CVR_Files()
        {
            // Act & Assert
            CvrFormat.IsCvrFile("test.cvr").Should().BeTrue();
            CvrFormat.IsCvrFile("test.CVR").Should().BeTrue();
            CvrFormat.IsCvrFile("test.db").Should().BeFalse();
            CvrFormat.IsCvrFile("test.adb").Should().BeFalse();
        }

        [Fact]
        public void CvrFormat_IsAdbFile_Should_Detect_ADB_Files()
        {
            // Act & Assert
            CvrFormat.IsAdbFile("test.adb").Should().BeTrue();
            CvrFormat.IsAdbFile("test.ADB").Should().BeTrue();
            CvrFormat.IsAdbFile("test.db").Should().BeFalse();
            CvrFormat.IsAdbFile("test.cvr").Should().BeFalse();
        }

        [Fact]
        public void CvrFormat_GetFormatName_Should_Return_Correct_Format()
        {
            // Act & Assert
            CvrFormat.GetFormatName("test.cvr").Should().Contain("CVR");
            CvrFormat.GetFormatName("test.adb").Should().Contain("ADB");
            CvrFormat.GetFormatName("test.db").Should().Contain("SQLite");
        }

        public void Dispose()
        {
            // Cleanup test files
            try
            {
                if (File.Exists(_testAdbPath))
                {
                    File.Delete(_testAdbPath);
                }
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                    // WAL files
                    var walPath = _testDbPath + "-wal";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    var shmPath = _testDbPath + "-shm";
                    if (File.Exists(shmPath)) File.Delete(shmPath);
                }
                if (File.Exists(_testCvrPath))
                {
                    File.Delete(_testCvrPath);
                }
                
                var tempDir = Path.GetDirectoryName(_testAdbPath);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
