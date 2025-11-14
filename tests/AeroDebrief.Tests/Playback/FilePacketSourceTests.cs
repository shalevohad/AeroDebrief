using Microsoft.VisualStudio.TestTools.UnitTesting;
using AeroDebrief.Core.IO;
using AeroDebrief.Tests.TestHelpers;
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
        private readonly List<string> _testFilesToCleanup = new();

        [TestInitialize]
        public void Setup()
        {
            // Tests will use MockRecordingFileBuilder to create test files
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Cleanup test files
            foreach (var file in _testFilesToCleanup)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        
                        // Also delete index file if exists
                        var indexFile = Path.ChangeExtension(file, ".pkidx");
                        if (File.Exists(indexFile))
                            File.Delete(indexFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _testFilesToCleanup.Clear();
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
        public async Task OpenAsync_WithValidFile_OpensSuccessfully()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateMinimalTestFile();
            _testFilesToCleanup.Add(testFile);
            
            var source = new FilePacketSource(testFile);

            // Act
            await source.OpenAsync();

            // Assert
            Assert.IsTrue(source.TotalPackets > 0, "Should have packets");
            Assert.IsTrue(source.TotalDuration > TimeSpan.Zero, "Should have duration");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task OpenAsync_Performance_IsFasterThan1Second()
        {
            // Arrange - create a file with reasonable amount of data
            var testFile = MockRecordingFileBuilder.CreateMultiFrequencyTestFile();
            _testFilesToCleanup.Add(testFile);
            
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
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateMultiFrequencyTestFile();
            _testFilesToCleanup.Add(testFile);
            
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var metadata = source.GetFrequencyMetadata();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds < 10, 
                $"Metadata retrieval took {stopwatch.Elapsed.TotalMilliseconds:F2}ms, expected < 10ms");
            Assert.IsTrue(metadata.Count > 0, "Should have frequency metadata");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task ReadRangeBatched_ProcessesBatchesCorrectly()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateConversationTestFile(durationSeconds: 5);
            _testFilesToCleanup.Add(testFile);
            
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
        public async Task MemoryUsage_IsReasonable()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateMultiFrequencyTestFile();
            _testFilesToCleanup.Add(testFile);
            
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

            // Assert - should be reasonable for a small test file
            Assert.IsTrue(usedMemoryMB < 50.0, 
                $"Memory usage is {usedMemoryMB:F2}MB, expected < 50MB for test file");

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

        [TestMethod]
        public async Task GetFrequencyMetadata_ReturnsCorrectFrequencies()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateMultiFrequencyTestFile();
            _testFilesToCleanup.Add(testFile);
            
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            // Act
            var metadata = source.GetFrequencyMetadata();

            // Assert
            Assert.IsTrue(metadata.Count >= 3, "Should have at least 3 frequencies");
            
            // Check that expected frequencies are present (from CreateMultiFrequencyTestFile)
            Assert.IsTrue(metadata.ContainsKey(251_000_000.0), "Should have 251 MHz");
            Assert.IsTrue(metadata.ContainsKey(127_500_000.0), "Should have 127.5 MHz");
            Assert.IsTrue(metadata.ContainsKey(305_000_000.0), "Should have 305 MHz");

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task ReadRange_ReturnsPacketsInOrder()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateConversationTestFile(durationSeconds: 5);
            _testFilesToCleanup.Add(testFile);
            
            var source = new FilePacketSource(testFile);
            await source.OpenAsync();

            // Act
            var packets = new List<RadioPacket>();
            await foreach (var packet in source.ReadRange(TimeSpan.Zero))
            {
                packets.Add(packet);
                
                // Only read first 100 for performance
                if (packets.Count >= 100)
                    break;
            }

            // Assert
            Assert.IsTrue(packets.Count > 0, "Should have read packets");
            
            // Verify packets are in timestamp order
            for (int i = 1; i < packets.Count; i++)
            {
                Assert.IsTrue(packets[i].Timestamp >= packets[i - 1].Timestamp, 
                    $"Packets should be in timestamp order (packet {i})");
            }

            // Cleanup
            source.Dispose();
        }

        [TestMethod]
        public async Task Index_IsCachedBetweenOpens()
        {
            // Arrange
            var testFile = MockRecordingFileBuilder.CreateConversationTestFile(durationSeconds: 5);
            _testFilesToCleanup.Add(testFile);
            
            // First open - builds index
            var source1 = new FilePacketSource(testFile);
            await source1.OpenAsync();
            var packetCount = source1.TotalPackets;
            source1.Dispose();

            // Verify index file was created
            var indexFile = Path.ChangeExtension(testFile, ".pkidx");
            Assert.IsTrue(File.Exists(indexFile), "Index file should be created after first open");

            // Second open - should load cached index
            var source2 = new FilePacketSource(testFile);
            await source2.OpenAsync();
            var packetCount2 = source2.TotalPackets;
            source2.Dispose();

            // Assert
            // Both opens should have same packet count (validates index was loaded correctly)
            Assert.AreEqual(packetCount, packetCount2, "Packet count should match between opens");
            
            // Index file should still exist
            Assert.IsTrue(File.Exists(indexFile), "Index file should persist after second open");
        }
    }
}
