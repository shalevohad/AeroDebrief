using Xunit;
using FluentAssertions;
using AeroDebrief.Core;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Playback;
using AeroDebrief.Tests.TestHelpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using NLog;

namespace AeroDebrief.Tests.Integration
{
    /// <summary>
    /// Integration tests for end-to-end file loading performance.
    /// Measures realistic file loading scenarios with full pipeline.
    /// Tests the Pure FilePacketSource architecture with memory-mapped files.
    /// </summary>
    public class FileLoadingPerformanceTests : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<string> _tempFiles = new();

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task LoadFile_ShouldUseMemoryMappedFile_FastAccess()
        {
            // Arrange - Create a small test file
            const int packetCount = 100;
            var testFile = CreateTestFile("perf_small_", packetCount);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert
            packetSource.TotalPackets.Should().Be(packetCount, "should have indexed all packets");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                $"file opening should be fast (<1s), took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? Small file ({packetCount} packets) opened in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task LoadFile_WithIndex_ShouldNotRebuild()
        {
            // Arrange - Create a test file and build index
            const int packetCount = 500;
            var testFile = CreateTestFile("perf_indexed_", packetCount);
            
            // First load - builds index
            using (var packetSource = new FilePacketSource(testFile))
            {
                await packetSource.OpenAsync();
                packetSource.TotalPackets.Should().Be(packetCount);
            }
            
            // Act - Second load should use existing index
            var stopwatch = Stopwatch.StartNew();
            using var packetSource2 = new FilePacketSource(testFile);
            await packetSource2.OpenAsync();
            stopwatch.Stop();
            
            // Assert
            packetSource2.TotalPackets.Should().Be(packetCount, "should have loaded from index");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, 
                $"loading with existing index should be very fast (<500ms), took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? File with existing index loaded in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task FileLoading_MediumFile_LoadsInUnder2Seconds()
        {
            // Arrange - ~1000 packets (~40-50KB file)
            const int packetCount = 1000;
            var testFile = CreateTestFile("perf_medium_", packetCount);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert
            packetSource.TotalPackets.Should().Be(packetCount);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), 
                $"medium file should load quickly, took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? Medium file ({packetCount} packets) loaded in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task FileLoading_LargeFile_LoadsInUnder5Seconds()
        {
            // Arrange - ~5000 packets (~250KB file)
            const int packetCount = 5000;
            var testFile = CreateTestFile("perf_large_", packetCount);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert
            packetSource.TotalPackets.Should().Be(packetCount);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), 
                $"large file should load in reasonable time, took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? Large file ({packetCount} packets) loaded in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task FileLoading_WithPipeline_InitializesQuickly()
        {
            // Arrange - Create test file
            const int packetCount = 1000;
            var testFile = CreateTestFile("perf_pipeline_", packetCount);
            
            // Act - Open FilePacketSource and create pipeline
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            
            using var pipeline = new FilePlaybackPipeline(packetSource);
            await pipeline.OpenAsync();
            stopwatch.Stop();
            
            // Assert
            pipeline.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3), 
                $"full pipeline initialization should be fast, took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? Full pipeline ({packetCount} packets) initialized in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task FileLoading_FrequencyMetadata_ExtractsInstantly()
        {
            // Arrange - Create file with multiple frequencies
            const int packetCount = 2000;
            var testFile = CreateTestFileMultiFrequency("perf_metadata_", packetCount, frequencyCount: 5);
            
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            
            // Act - Extract frequency metadata (should be instant - no packet reads!)
            var stopwatch = Stopwatch.StartNew();
            var metadata = packetSource.GetFrequencyMetadata();
            stopwatch.Stop();
            
            // Assert
            metadata.Should().NotBeEmpty("should have extracted frequency information");
            metadata.Count.Should().Be(5, "should have detected all frequencies");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
                $"metadata extraction should be instant (<100ms), took {stopwatch.ElapsedMilliseconds}ms");
            
            Logger.Info($"? Frequency metadata for {metadata.Count} frequencies extracted in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "Diagnostic")]
        public void VerifyTestingCorrectAssembly()
        {
            // Verify we're testing the correct version
            var assembly = typeof(FilePacketSource).Assembly;
            var assemblyName = assembly.GetName();
            
            Logger.Info($"Testing assembly: {assemblyName.Name} v{assemblyName.Version}");
            Logger.Info($"Location: {assembly.Location}");
            
            // Should be testing AeroDebrief.Core
            assemblyName.Name.Should().Be("AeroDebrief.Core");
            
            // Verify FilePacketSource exists and has expected methods
            var type = typeof(FilePacketSource);
            type.Should().NotBeNull();
            type.GetMethod("OpenAsync").Should().NotBeNull();
            type.GetMethod("GetFrequencyMetadata").Should().NotBeNull();
            
            Logger.Info("? Testing correct assembly with expected types");
        }

        #region Helper Methods

        /// <summary>
        /// Creates a test recording file with specified number of packets on a single frequency
        /// </summary>
        private string CreateTestFile(string prefix, int packetCount)
        {
            using var builder = new MockRecordingFileBuilder();
            
            var filePath = Path.Combine(Path.GetTempPath(), $"{prefix}{Guid.NewGuid()}.adb");
            builder.WithHeader();
            builder.WithPackets(
                count: packetCount,
                interval: TimeSpan.FromMilliseconds(Constants.OPUS_FRAME_DURATION_MS),
                startTime: DateTime.UtcNow,
                frequency: 251_000_000.0, // 251 MHz
                playerName: "TestPilot",
                coalition: 2 // Blue
            );
            
            var result = builder.Build();
            File.Move(result, filePath, overwrite: true);
            _tempFiles.Add(filePath);
            
            Logger.Debug($"Created test file: {filePath} with {packetCount} packets");
            
            return filePath;
        }

        /// <summary>
        /// Creates a test recording file with packets across multiple frequencies
        /// </summary>
        private string CreateTestFileMultiFrequency(string prefix, int totalPackets, int frequencyCount)
        {
            using var builder = new MockRecordingFileBuilder();
            
            var filePath = Path.Combine(Path.GetTempPath(), $"{prefix}{Guid.NewGuid()}.adb");
            builder.WithHeader();
            
            var baseFreq = 251_000_000.0; // 251 MHz
            var baseTime = DateTime.UtcNow;
            
            // Distribute packets evenly across frequencies
            var packetsPerFreq = totalPackets / frequencyCount;
            
            for (int freq = 0; freq < frequencyCount; freq++)
            {
                var frequency = baseFreq + (freq * 1_000_000); // 1 MHz spacing
                var playerName = $"Pilot-{freq + 1}";
                
                builder.WithPackets(
                    count: packetsPerFreq,
                    interval: TimeSpan.FromMilliseconds(Constants.OPUS_FRAME_DURATION_MS),
                    startTime: baseTime,
                    frequency: frequency,
                    playerName: playerName,
                    coalition: (freq % 2) + 1 // Alternate Red/Blue
                );
            }
            
            var result = builder.Build();
            File.Move(result, filePath, overwrite: true);
            _tempFiles.Add(filePath);
            
            Logger.Debug($"Created multi-frequency test file: {filePath} with {totalPackets} packets across {frequencyCount} frequencies");
            
            return filePath;
        }

        /// <summary>
        /// Cleanup test files
        /// </summary>
        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                    
                    // Also delete index file if it exists
                    var indexFile = Path.ChangeExtension(file, ".pkidx");
                    if (File.Exists(indexFile))
                        File.Delete(indexFile);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to delete test file: {file}");
                }
            }
        }

        #endregion
    }
}
