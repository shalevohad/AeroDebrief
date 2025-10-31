using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.IO;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AeroDebrief.Tests.Playback
{
    /// <summary>
    /// Tests for FilePacketSource - memory-mapped file access and indexing
    /// </summary>
    [TestClass]
    public class FilePacketSourceTests
    {
        private string? _testFilePath;

        [TestInitialize]
        public void Setup()
        {
            // Tests will use actual recording files if available
            // For now, we'll test the API surface and error handling
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Cleanup test files if created
        }

        [TestMethod]
        public void Constructor_WithNullPath_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new FilePacketSource(null!));
        }

        [TestMethod]
        public void Constructor_WithEmptyPath_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new FilePacketSource(string.Empty));
        }

        [TestMethod]
        public void Constructor_WithValidPath_CreatesInstance()
        {
            // Arrange & Act
            var source = new FilePacketSource("test.srs");

            // Assert
            Assert.IsNotNull(source);
        }

        [TestMethod]
        public async Task OpenAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var source = new FilePacketSource("nonexistent.srs");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => source.OpenAsync());
        }

        [TestMethod]
        public async Task OpenAsync_Performance_IsFasterThan1Second()
        {
            // This test requires an actual recording file
            // For now, we'll skip it if no test file is available
            var testFile = GetTestRecordingFile();
            if (testFile == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(testFile);
            var stopwatch = Stopwatch.StartNew();

            // Act
            await source.OpenAsync();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, 
                $"File open took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

            // Verify basic properties
            Assert.IsTrue(source.TotalPackets > 0, "Should have packets");
            Assert.IsTrue(source.TotalDuration > TimeSpan.Zero, "Should have duration");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task GetFrequencyMetadata_Performance_IsInstant()
        {
            // This test requires an actual recording file
            var testFile = GetTestRecordingFile();
            if (testFile == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var metadata = source.GetFrequencyMetadata();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds < 1, 
                $"Metadata retrieval took {stopwatch.Elapsed.TotalMilliseconds:F2}ms, expected < 1ms");
            Assert.IsTrue(metadata.Count > 0, "Should have frequency metadata");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task ReadRangeBatched_ProcessesBatchesCorrectly()
        {
            // This test requires an actual recording file
            var testFile = GetTestRecordingFile();
            if (testFile == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            const int batchSize = 100;
            var totalPackets = 0;
            var batchCount = 0;

            // Act
            await foreach (var batch in source.ReadRangeBatched(TimeSpan.Zero, batchSize))
            {
                totalPackets += batch.Length;
                batchCount++;

                // Each batch should have batchSize packets (except possibly the last one)
                Assert.IsTrue(batch.Length <= batchSize, "Batch size should not exceed limit");
                Assert.IsTrue(batch.Length > 0, "Batch should not be empty");

                // Only process first few batches for performance
                if (batchCount >= 10)
                    break;
            }

            // Assert
            Assert.IsTrue(batchCount > 0, "Should have processed batches");
            Assert.IsTrue(totalPackets > 0, "Should have processed packets");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task MemoryUsage_IsUnder15MB()
        {
            // This test requires an actual recording file
            var testFile = GetTestRecordingFile();
            if (testFile == null)
            {
                Assert.Inconclusive("No test recording file available");
                return;
            }

            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeMemory = GC.GetTotalMemory(forceFullCollection: true);

            // Act
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var afterMemory = GC.GetTotalMemory(forceFullCollection: true);
            var usedMemoryMB = (afterMemory - beforeMemory) / 1_000_000.0;

            // Assert
            Assert.IsTrue(usedMemoryMB < 15.0, 
                $"Memory usage is {usedMemoryMB:F2}MB, expected < 15MB");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public void Properties_BeforeOpen_HaveDefaultValues()
        {
            // Arrange
            var source = new FilePacketSource("test.srs");

            // Act & Assert
            Assert.AreEqual(0, source.TotalPackets);
            Assert.AreEqual(TimeSpan.Zero, source.TotalDuration);
        }

        [TestMethod]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var source = new FilePacketSource("test.srs");

            // Act & Assert
            source.Dispose();
            source.Dispose(); // Should not throw
        }

        /// <summary>
        /// Helper to find a test recording file
        /// </summary>
        private string? GetTestRecordingFile()
        {
            // Look for test files in common locations
            var testPaths = new[]
            {
                @"..\..\..\..\TestData\sample.srs",
                @"TestData\sample.srs",
                @"C:\Temp\test.srs"
            };

            foreach (var path in testPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
