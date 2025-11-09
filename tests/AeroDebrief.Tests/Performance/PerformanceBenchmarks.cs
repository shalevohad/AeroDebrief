using Xunit;
using FluentAssertions;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Helpers;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Tests.TestHelpers;
using AeroDebrief.Tests.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.Tests.Performance
{
    /// <summary>
    /// Comprehensive performance benchmarks for AeroDebrief core operations.
    /// Tracks baseline performance and helps detect regressions.
    /// 
    /// Performance Targets (based on modern hardware):
    /// - File indexing: >10,000 packets/sec
    /// - Audio decoding: >5,000 packets/sec (Opus)
    /// - File analysis: Complete 10MB file in <2s
    /// - Packet routing: >2,000,000 packets/sec
    /// - Memory usage: <500MB for 100MB recording
    /// </summary>
    public class PerformanceBenchmarks : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<string> _tempFiles = new();
        
        // Performance thresholds (can be adjusted based on CI hardware)
        private const double FILE_INDEXING_MIN_PACKETS_PER_SEC = 10_000;
        private const double AUDIO_DECODING_MIN_PACKETS_PER_SEC = 5_000;
        private const double FILE_ANALYSIS_MAX_SECONDS = 2.0;
        private const long MAX_MEMORY_USAGE_MB = 500;

        #region File I/O Benchmarks

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileIndexing_SmallFile()
        {
            // Arrange
            const int packetCount = 1_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_index_small_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert & Report
            var packetsPerSec = packetCount / stopwatch.Elapsed.TotalSeconds;
            
            Logger.Info($"?? File Indexing Benchmark (Small):");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetsPerSec:N0} packets/sec");
            
            packetSource.TotalPackets.Should().Be(packetCount);
            packetsPerSec.Should().BeGreaterThan(FILE_INDEXING_MIN_PACKETS_PER_SEC, 
                $"indexing performance below threshold ({FILE_INDEXING_MIN_PACKETS_PER_SEC:N0} pkt/s)");
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileIndexing_MediumFile()
        {
            // Arrange
            const int packetCount = 10_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_index_medium_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert & Report
            var packetsPerSec = packetCount / stopwatch.Elapsed.TotalSeconds;
            var fileSize = new FileInfo(testFile).Length;
            
            Logger.Info($"?? File Indexing Benchmark (Medium):");
            Logger.Info($"   File size: {fileSize / 1024.0:N1} KB");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetsPerSec:N0} packets/sec");
            
            packetSource.TotalPackets.Should().Be(packetCount);
            packetsPerSec.Should().BeGreaterThan(FILE_INDEXING_MIN_PACKETS_PER_SEC);
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileIndexing_LargeFile()
        {
            // Arrange - 50K packets (~2.5-3MB file)
            const int packetCount = 50_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_index_large_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert & Report
            var packetsPerSec = packetCount / stopwatch.Elapsed.TotalSeconds;
            var fileSize = new FileInfo(testFile).Length;
            
            Logger.Info($"?? File Indexing Benchmark (Large):");
            Logger.Info($"   File size: {fileSize / 1024.0 / 1024.0:N2} MB");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetsPerSec:N0} packets/sec");
            Logger.Info($"   Data rate: {fileSize / stopwatch.Elapsed.TotalSeconds / 1024.0 / 1024.0:N2} MB/s");
            
            packetSource.TotalPackets.Should().Be(packetCount);
            packetsPerSec.Should().BeGreaterThan(FILE_INDEXING_MIN_PACKETS_PER_SEC);
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_IndexCache_SecondLoad()
        {
            // Arrange - Create file and index it
            const int packetCount = 10_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_cache_");
            
            // First load - builds index
            using (var packetSource = new FilePacketSource(testFile))
            {
                await packetSource.OpenAsync();
            }
            
            // Act - Second load with cached index
            var stopwatch = Stopwatch.StartNew();
            using var cachedSource = new FilePacketSource(testFile);
            await cachedSource.OpenAsync();
            stopwatch.Stop();
            
            // Assert & Report
            var packetsPerSec = packetCount / stopwatch.Elapsed.TotalSeconds;
            
            Logger.Info($"?? Index Cache Benchmark:");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Time (cached): {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetsPerSec:N0} packets/sec");
            Logger.Info($"   Speedup: Expected 2-5x faster than initial indexing");
            
            cachedSource.TotalPackets.Should().Be(packetCount);
            // Cached loading should be significantly faster
            packetsPerSec.Should().BeGreaterThan(FILE_INDEXING_MIN_PACKETS_PER_SEC * 2);
        }

        #endregion

        #region Audio Decoding Benchmarks

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public void Benchmark_OpusDecoding()
        {
            // Arrange - Create realistic Opus packets
            const int packetCount = 1_000;
            var packets = CreateOpusPackets(packetCount);
            
            // Act - Decode all packets
            var stopwatch = Stopwatch.StartNew();
            var totalSamples = 0L;
            
            foreach (var packet in packets)
            {
                var samples = AudioHelpers.DecodeOpusToPcm(packet);
                totalSamples += samples.Length;
            }
            
            stopwatch.Stop();
            
            // Assert & Report
            var packetsPerSec = packetCount / stopwatch.Elapsed.TotalSeconds;
            var durationDecoded = TimeSpan.FromSeconds(totalSamples / (double)Constants.OUTPUT_SAMPLE_RATE);
            var realtimeRatio = totalSamples > 0 
                ? durationDecoded.TotalSeconds / stopwatch.Elapsed.TotalSeconds 
                : 0;
            
            Logger.Info($"?? Opus Decoding Benchmark:");
            Logger.Info($"   Packets decoded: {packetCount:N0}");
            Logger.Info($"   Samples decoded: {totalSamples:N0}");
            Logger.Info($"   Audio duration: {durationDecoded.TotalSeconds:F2}s");
            Logger.Info($"   Time elapsed: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetsPerSec:N0} packets/sec");
            Logger.Info($"   Real-time ratio: {realtimeRatio:F2}x (higher is better)");
            
            totalSamples.Should().BeGreaterThan(0, "should decode at least some samples from Opus packets");
            packetsPerSec.Should().BeGreaterThan(AUDIO_DECODING_MIN_PACKETS_PER_SEC,
                "Opus decoding performance below threshold");
            realtimeRatio.Should().BeGreaterThan(10, 
                "should decode at least 10x faster than real-time");
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public void Benchmark_PcmConversion()
        {
            // Arrange - Create test PCM data
            const int sampleCount = 1_000_000; // ~21 seconds of audio
            var pcmSamples = CreateTestPcmSamples(sampleCount);
            
            // Act - Convert to bytes and back
            var stopwatch = Stopwatch.StartNew();
            var pcmBytes = AudioHelpers.ConvertPcm16ToBytes(pcmSamples);
            var convertedBack = AudioHelpers.ConvertBytesToPcm16(pcmBytes);
            stopwatch.Stop();
            
            // Assert & Report
            var samplesPerSec = (sampleCount * 2) / stopwatch.Elapsed.TotalSeconds; // *2 for round-trip
            var mbPerSec = samplesPerSec * 2 / 1024.0 / 1024.0;
            
            Logger.Info($"?? PCM Conversion Benchmark:");
            Logger.Info($"   Samples processed: {sampleCount * 2:N0} (round-trip)");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {samplesPerSec:N0} samples/sec");
            Logger.Info($"   Data rate: {mbPerSec:N2} MB/s");
            
            convertedBack.Should().HaveCount(sampleCount);
            samplesPerSec.Should().BeGreaterThan(10_000_000, 
                "PCM conversion should process >10M samples/sec");
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public void Benchmark_AudioAmplitudeCalculation()
        {
            // Arrange
            const int sampleCount = 1_000_000;
            var samples = CreateTestPcmSamples(sampleCount);
            
            // Act - Calculate amplitude 1000 times
            var stopwatch = Stopwatch.StartNew();
            const int iterations = 1000;
            
            for (int i = 0; i < iterations; i++)
            {
                var amplitude = AudioHelpers.CalculateNormalizedAmplitude(samples);
            }
            
            stopwatch.Stop();
            
            // Assert & Report
            var calculationsPerSec = iterations / stopwatch.Elapsed.TotalSeconds;
            var samplesProcessedPerSec = (sampleCount * iterations) / stopwatch.Elapsed.TotalSeconds;
            
            Logger.Info($"?? Amplitude Calculation Benchmark:");
            Logger.Info($"   Samples: {sampleCount:N0}");
            Logger.Info($"   Iterations: {iterations:N0}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {calculationsPerSec:N0} calculations/sec");
            Logger.Info($"   Throughput: {samplesProcessedPerSec:N0} samples/sec");
            
            calculationsPerSec.Should().BeGreaterThan(1000, 
                "should perform >1000 amplitude calculations per second");
        }

        #endregion

        #region File Analysis Benchmarks

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileAnalysis_FrequencyExtraction()
        {
            // Arrange - File with multiple frequencies
            const int packetCount = 10_000;
            const int frequencyCount = 10;
            var testFile = await CreateMultiFrequencyRecordingAsync(
                packetCount, frequencyCount, "bench_freq_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var frequencies = FileAnalyzer.GetAllFrequencyModulations(testFile);
            stopwatch.Stop();
            
            // Assert & Report
            Logger.Info($"?? Frequency Extraction Benchmark:");
            Logger.Info($"   File packets: {packetCount:N0}");
            Logger.Info($"   Frequencies found: {frequencies.Count}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetCount / stopwatch.Elapsed.TotalSeconds:N0} packets/sec");
            
            frequencies.Should().HaveCount(frequencyCount);
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(FILE_ANALYSIS_MAX_SECONDS);
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileAnalysis_DurationCalculation()
        {
            // Arrange
            const int packetCount = 20_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_duration_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var duration = FileAnalyzer.CalculateTotalDuration(testFile);
            stopwatch.Stop();
            
            // Assert & Report
            Logger.Info($"?? Duration Calculation Benchmark:");
            Logger.Info($"   File packets: {packetCount:N0}");
            Logger.Info($"   Calculated duration: {duration}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            
            duration.Should().BeGreaterThan(TimeSpan.Zero);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                "duration calculation should complete in <1s");
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileAnalysis_ActivityAnalysis()
        {
            // Arrange
            const int packetCount = 15_000;
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_activity_");
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var activity = FileAnalyzer.AnalyzeAudioActivity(testFile, silenceThreshold: 500);
            stopwatch.Stop();
            
            // Assert & Report
            Logger.Info($"?? Activity Analysis Benchmark:");
            Logger.Info($"   File packets: {packetCount:N0}");
            Logger.Info($"   Activity periods: {activity.ActivityPeriods.Count}");
            Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Rate: {packetCount / stopwatch.Elapsed.TotalSeconds:N0} packets/sec");
            
            activity.TotalPackets.Should().Be(packetCount);
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(FILE_ANALYSIS_MAX_SECONDS);
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_FileAnalysis_CompleteAnalysis()
        {
            // Arrange - Realistic scenario
            const int packetCount = 25_000;
            const int frequencyCount = 8;
            var testFile = await CreateMultiFrequencyRecordingAsync(
                packetCount, frequencyCount, "bench_complete_");
            var fileSize = new FileInfo(testFile).Length;
            
            // Act - Perform all analysis operations
            var stopwatch = Stopwatch.StartNew();
            
            var frequencies = FileAnalyzer.GetAllFrequencyModulations(testFile);
            var duration = FileAnalyzer.CalculateTotalDuration(testFile);
            var activity = FileAnalyzer.AnalyzeAudioActivity(testFile);
            
            stopwatch.Stop();
            
            // Assert & Report
            Logger.Info($"?? Complete File Analysis Benchmark:");
            Logger.Info($"   File size: {fileSize / 1024.0 / 1024.0:N2} MB");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Frequencies: {frequencies.Count}");
            Logger.Info($"   Duration: {duration}");
            Logger.Info($"   Activity periods: {activity.ActivityPeriods.Count}");
            Logger.Info($"   Total time: {stopwatch.ElapsedMilliseconds}ms");
            Logger.Info($"   Overall rate: {packetCount / stopwatch.Elapsed.TotalSeconds:N0} packets/sec");
            
            frequencies.Should().NotBeEmpty();
            duration.Should().BeGreaterThan(TimeSpan.Zero);
            activity.TotalPackets.Should().Be(packetCount);
            
            // Complete analysis should finish within threshold
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(FILE_ANALYSIS_MAX_SECONDS * 2, 
                "complete analysis taking too long");
        }

        #endregion

        #region Memory Usage Benchmarks

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public async Task Benchmark_Memory_FileLoading()
        {
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var initialMemory = GC.GetTotalMemory(true);
            
            // Arrange & Act - Load large file
            const int packetCount = 100_000; // ~5-6MB file
            var testFile = await CreateTestRecordingAsync(packetCount, "bench_memory_");
            
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = (finalMemory - initialMemory) / 1024.0 / 1024.0;
            
            // Report
            Logger.Info($"?? Memory Usage Benchmark (File Loading):");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Initial memory: {initialMemory / 1024.0 / 1024.0:N2} MB");
            Logger.Info($"   Final memory: {finalMemory / 1024.0 / 1024.0:N2} MB");
            Logger.Info($"   Memory used: {memoryUsed:N2} MB");
            Logger.Info($"   Memory per packet: {(memoryUsed * 1024.0 * 1024.0) / packetCount:N2} bytes");
            
            // Assert - Memory usage should be reasonable
            memoryUsed.Should().BeLessThan(MAX_MEMORY_USAGE_MB, 
                $"memory usage exceeds {MAX_MEMORY_USAGE_MB}MB threshold");
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Benchmark")]
        public void Benchmark_Memory_AudioDecoding()
        {
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var initialMemory = GC.GetTotalMemory(true);
            
            // Act - Decode many packets
            const int packetCount = 10_000;
            var packets = CreateOpusPackets(packetCount);
            var totalSamples = 0L;
            
            foreach (var packet in packets)
            {
                var samples = AudioHelpers.DecodeOpusToPcm(packet);
                totalSamples += samples.Length;
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = (finalMemory - initialMemory) / 1024.0 / 1024.0;
            
            // Report
            Logger.Info($"?? Memory Usage Benchmark (Audio Decoding):");
            Logger.Info($"   Packets decoded: {packetCount:N0}");
            Logger.Info($"   Samples generated: {totalSamples:N0}");
            Logger.Info($"   Memory used: {memoryUsed:N2} MB");
            Logger.Info($"   Memory per packet: {(memoryUsed * 1024.0 * 1024.0) / packetCount:N2} bytes");
            
            // Assert - Memory usage should be reasonable for audio operations
            memoryUsed.Should().BeLessThan(50, "audio decoding memory usage excessive");
        }

        #endregion

        #region Stress Tests

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Category", "Stress")]
        public async Task Stress_LargeFileProcessing()
        {
            // Arrange - Process ~100MB file across 50 frequencies
            // This simulates a realistic high-load scenario with many concurrent channels
            // Approximate calculation: 100MB / 2KB per packet ? 50,000 packets
            const int targetSizeMB = 100;
            const int frequencyCount = 50;
            
            Logger.Info($"?? Starting Large File Stress Test - Generating {targetSizeMB}MB test file with {frequencyCount} frequencies...");
            
            // Generate large file directly with target size
            var testFile = await SyntheticRecordingGenerator.GenerateAsync(
                targetSizeMB: targetSizeMB,
                packetIntervalMs: Constants.OPUS_FRAME_DURATION_MS
            );
            _tempFiles.Add(testFile);
            
            var fileSize = new FileInfo(testFile).Length;
            var fileSizeMB = fileSize / 1024.0 / 1024.0;
            
            Logger.Info($"   Generated file: {fileSizeMB:N2} MB");
            
            // Act - Full pipeline with multi-frequency processing
            var stopwatch = Stopwatch.StartNew();
            
            using var packetSource = new FilePacketSource(testFile);
            await packetSource.OpenAsync();
            
            var frequencies = FileAnalyzer.GetAllFrequencyModulations(testFile);
            var duration = FileAnalyzer.CalculateTotalDuration(testFile);
            var activity = FileAnalyzer.AnalyzeAudioActivity(testFile);
            
            stopwatch.Stop();
            
            var packetCount = packetSource.TotalPackets;
            var avgPacketsPerFreq = frequencies.Count > 0 ? packetCount / frequencies.Count : 0;
            
            // Report
            Logger.Info($"?? Large File Stress Test (Multi-Frequency - {targetSizeMB}MB):");
            Logger.Info($"   File size: {fileSizeMB:N2} MB");
            Logger.Info($"   Packets: {packetCount:N0}");
            Logger.Info($"   Frequencies: {frequencies.Count}");
            Logger.Info($"   Avg packets per frequency: {avgPacketsPerFreq:N0}");
            Logger.Info($"   Duration: {duration}");
            Logger.Info($"   Activity periods: {activity.ActivityPeriods.Count}");
            Logger.Info($"   Processing time: {stopwatch.Elapsed.TotalSeconds:F2}s");
            Logger.Info($"   Overall rate: {packetCount / stopwatch.Elapsed.TotalSeconds:N0} packets/sec");
            Logger.Info($"   Data rate: {fileSize / stopwatch.Elapsed.TotalSeconds / 1024.0 / 1024.0:N2} MB/s");
            Logger.Info($"   Concurrency factor: {frequencies.Count} simultaneous channels");
            
            packetSource.TotalPackets.Should().BeGreaterThan(0);
            frequencies.Should().NotBeEmpty("should have detected frequencies in stress test");
            
            // File size should be approximately target (allow 20% variance due to compression)
            fileSizeMB.Should().BeGreaterThan(targetSizeMB * 0.8, 
                $"generated file too small (target: {targetSizeMB}MB)");
            fileSizeMB.Should().BeLessThan(targetSizeMB * 1.5, 
                $"generated file too large (target: {targetSizeMB}MB)");
            
            // Should complete large multi-frequency file in reasonable time
            // Target: Process 100MB in under 60 seconds (>1.5 MB/s)
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(60, 
                $"large file processing with {frequencies.Count} frequencies taking too long");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test recording file using SyntheticRecordingGenerator
        /// </summary>
        private async Task<string> CreateTestRecordingAsync(int packetCount, string prefix)
        {
            // Use the exact packet count method to ensure we get the requested number of packets
            var result = await SyntheticRecordingGenerator.GenerateWithPacketCountAsync(
                packetCount: packetCount,
                packetIntervalMs: Constants.OPUS_FRAME_DURATION_MS
            );
            
            _tempFiles.Add(result);
            return result;
        }

        /// <summary>
        /// Creates a test recording with multiple frequencies using MockRecordingFileBuilder
        /// </summary>
        private async Task<string> CreateMultiFrequencyRecordingAsync(
            int totalPackets, 
            int frequencyCount, 
            string prefix)
        {
            var fileName = $"{prefix}{Guid.NewGuid()}.adb";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);
            
            using var builder = new MockRecordingFileBuilder(filePath);
            builder.WithHeader();
            
            var packetsPerFreq = totalPackets / frequencyCount;
            var baseFreq = 251_000_000.0; // VHF AM base frequency
            
            for (int i = 0; i < frequencyCount; i++)
            {
                var frequency = baseFreq + (i * 1_000_000); // Increment by 1 MHz
                builder.WithPackets(
                    count: packetsPerFreq,
                    interval: TimeSpan.FromMilliseconds(Constants.OPUS_FRAME_DURATION_MS),
                    frequency: frequency,
                    playerName: $"Pilot-{i + 1}"
                );
            }
            
            var result = builder.Build();
            _tempFiles.Add(result);
            
            return result;
        }

        /// <summary>
        /// Creates realistic Opus-encoded audio packets for testing.
        /// Uses minimal valid Opus frames that meet size validation requirements.
        /// </summary>
        private List<byte[]> CreateOpusPackets(int count)
        {
            var packets = new List<byte[]>(count);
            
            // Create a valid Opus frame that will pass validation (minimum 10 bytes)
            // Opus frame structure: [TOC byte][optional frame length][encoded data]
            // TOC byte: 0xFC = Configuration 31 (CELT-only), code 0 (one frame, 20ms)
            // This creates a minimal but valid Opus silence frame
            var opusFrame = new byte[20]; // Use 20 bytes to safely exceed minimum validation (10 bytes)
            opusFrame[0] = 0xFC; // TOC byte for CELT-only mode
            // Rest filled with zeros (silence)
            
            for (int i = 0; i < count; i++)
            {
                // Each packet gets its own copy to avoid shared references
                var packet = new byte[opusFrame.Length];
                Array.Copy(opusFrame, packet, opusFrame.Length);
                packets.Add(packet);
            }
            
            return packets;
        }

        /// <summary>
        /// Creates test PCM samples with a sine wave at standard sample rate
        /// </summary>
        private short[] CreateTestPcmSamples(int count)
        {
            var samples = new short[count];
            const double frequency = 440.0; // A4 note (440 Hz)
            
            for (int i = 0; i < count; i++)
            {
                var time = i / (double)Constants.OUTPUT_SAMPLE_RATE;
                samples[i] = (short)(Math.Sin(2 * Math.PI * frequency * time) * 8000);
            }
            
            return samples;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                    
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
