using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.Tests.TestHelpers;
using AeroDebrief.UI.Services.Audio;
using AeroDebrief.UI.Services.Graphs;
using NLog;

namespace AeroDebrief.Tests.Graphs
{
    /// <summary>
    /// Phase 2.2: Tests amplitude extraction pipeline with mock recordings.
    /// Validates the complete flow from FilePacketSource to amplitude visualization.
    /// </summary>
    public static class AmplitudeExtractionPipelineTests
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Runs all Phase 2.2 pipeline tests.
        /// </summary>
        public static async Task RunAllTests()
        {
            Logger.Info("========================================");
            Logger.Info("Phase 2.2: Amplitude Extraction Pipeline Tests");
            Logger.Info("========================================");
            
            var passedTests = 0;
            var totalTests = 0;
            
            // Test 1: Simple single-frequency recording
            totalTests++;
            if (await Test_SimpleSingleFrequency())
            {
                passedTests++;
                Logger.Info("? Test 1 PASSED: Simple single frequency");
            }
            else
            {
                Logger.Error("? Test 1 FAILED: Simple single frequency");
            }
            
            // Test 2: Multiple frequencies with talking patterns
            totalTests++;
            if (await Test_MultipleFrequenciesWithTalkingPatterns())
            {
                passedTests++;
                Logger.Info("? Test 2 PASSED: Multiple frequencies with talking patterns");
            }
            else
            {
                Logger.Error("? Test 2 FAILED: Multiple frequencies with talking patterns");
            }
            
            // Test 3: Realistic radio chatter simulation
            totalTests++;
            if (await Test_RealisticRadioChatter())
            {
                passedTests++;
                Logger.Info("? Test 3 PASSED: Realistic radio chatter");
            }
            else
            {
                Logger.Error("? Test 3 FAILED: Realistic radio chatter");
            }
            
            // Test 4: Performance with synthetic data fallback
            totalTests++;
            if (await Test_SyntheticDataFallback())
            {
                passedTests++;
                Logger.Info("? Test 4 PASSED: Synthetic data fallback");
            }
            else
            {
                Logger.Error("? Test 4 FAILED: Synthetic data fallback");
            }
            
            Logger.Info("========================================");
            Logger.Info($"Tests Completed: {passedTests}/{totalTests} passed");
            Logger.Info("========================================");
        }
        
        /// <summary>
        /// Test 1: Simple single-frequency recording with one pilot.
        /// </summary>
        private static async Task<bool> Test_SimpleSingleFrequency()
        {
            string? recordingFile = null;
            try
            {
                Logger.Info("Creating simple mock recording (single frequency, single pilot)...");
                
                // Create a simple mock recording
                using var builder = new MockRecordingFileBuilder();
                var startTime = DateTime.UtcNow;
                
                builder
                    .WithHeader(startTime: startTime)
                    .WithPackets(
                        count: 100,  // ~4 seconds of audio (40ms per packet)
                        startTime: startTime,
                        frequency: 251_000_000.0,  // 251 MHz
                        playerName: "Viper-1",
                        coalition: 2  // Blue
                    );
                
                recordingFile = builder.FilePath;
                builder.Dispose();
                
                Logger.Info($"Mock recording created: {recordingFile}");
                Logger.Info($"Packets: {100}, Duration: ~4 seconds");
                
                // Open the recording with FilePacketSource
                var packetSource = new FilePacketSource(recordingFile);
                await packetSource.OpenAsync();
                
                Logger.Info($"Recording loaded: {packetSource.TotalPackets} packets, {packetSource.TotalDuration.TotalSeconds:F1}s duration");
                
                // Create amplitude extraction pipeline
                var audioEngine = new AudioProcessingEngine();
                audioEngine.Initialize();
                
                var provider = new AmplitudeSeriesProvider(packetSource, audioEngine);
                
                // Extract amplitude data
                var endTime = startTime.AddSeconds(5);
                var seriesCount = 0;
                var totalPoints = 0;
                
                await foreach (var (key, points) in provider.GetSeriesAsync(startTime, endTime))
                {
                    seriesCount++;
                    var pointsList = points.ToList();
                    totalPoints += pointsList.Count;
                    
                    Logger.Info($"Series: {key}, Points: {pointsList.Count}");
                    
                    // Validate points
                    if (pointsList.Count == 0)
                    {
                        Logger.Error($"Series {key} has no points!");
                        return false;
                    }
                    
                    // Check that points are in time order
                    for (int i = 1; i < pointsList.Count; i++)
                    {
                        if (pointsList[i].X < pointsList[i - 1].X)
                        {
                            Logger.Error($"Points not in time order at index {i}");
                            return false;
                        }
                    }
                    
                    // Check dBFS range
                    var maxDb = pointsList.Max(p => p.Y);
                    var minDb = pointsList.Min(p => p.Y);
                    
                    if (maxDb > 0 || minDb < -120)
                    {
                        Logger.Error($"dBFS values out of range: {minDb:F1} to {maxDb:F1}");
                        return false;
                    }
                    
                    Logger.Info($"  dBFS range: {minDb:F1} to {maxDb:F1} dBFS");
                    Logger.Info($"  Time range: {pointsList.First().X:F3}s to {pointsList.Last().X:F3}s");
                }
                
                Logger.Info($"? Extracted {seriesCount} series with {totalPoints} total points");
                
                // Cleanup
                packetSource.Dispose();
                
                return seriesCount > 0 && totalPoints > 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Test failed with exception");
                return false;
            }
            finally
            {
                // Cleanup temp file
                if (recordingFile != null && File.Exists(recordingFile))
                {
                    try { File.Delete(recordingFile); } catch { }
                }
            }
        }
        
        /// <summary>
        /// Test 2: Multiple frequencies with different pilots and talking patterns.
        /// </summary>
        private static async Task<bool> Test_MultipleFrequenciesWithTalkingPatterns()
        {
            string? recordingFile = null;
            try
            {
                Logger.Info("Creating mock recording with multiple frequencies and talking patterns...");
                
                using var builder = new MockRecordingFileBuilder();
                var startTime = DateTime.UtcNow;
                
                builder.WithHeader(startTime: startTime);
                
                // Simulate 3 frequencies with different pilots
                var transmitters = new[]
                {
                    ("Viper-1", 251_000_000.0, 2),   // Blue, 251 MHz
                    ("Enfield-1", 243_000_000.0, 2), // Blue, 243 MHz
                    ("Frogfoot-1", 305_000_000.0, 1) // Red, 305 MHz
                };
                
                builder.WithMultipleTransmitters(
                    packetsPerTransmitter: 50,  // 50 packets each = ~2 seconds each
                    transmitters
                );
                
                recordingFile = builder.FilePath;
                builder.Dispose();
                
                Logger.Info($"Mock recording created with {transmitters.Length} transmitters");
                
                // Open and process
                var packetSource = new FilePacketSource(recordingFile);
                await packetSource.OpenAsync();
                
                Logger.Info($"Recording loaded: {packetSource.TotalPackets} packets");
                
                var audioEngine = new AudioProcessingEngine();
                audioEngine.Initialize();
                
                var provider = new AmplitudeSeriesProvider(packetSource, audioEngine);
                
                // Extract amplitude data
                var endTime = startTime.AddSeconds(10);
                var seriesCount = 0;
                var frequencies = new System.Collections.Generic.HashSet<string>();
                
                await foreach (var (key, points) in provider.GetSeriesAsync(startTime, endTime))
                {
                    seriesCount++;
                    frequencies.Add(key.Split('-')[0]); // Extract frequency part
                    
                    var pointsList = points.ToList();
                    Logger.Info($"Series: {key}, Points: {pointsList.Count}");
                }
                
                Logger.Info($"? Extracted {seriesCount} series across {frequencies.Count} frequencies");
                
                // Validate we got data from multiple frequencies
                if (frequencies.Count < 2)
                {
                    Logger.Error($"Expected multiple frequencies, got {frequencies.Count}");
                    return false;
                }
                
                packetSource.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Test failed with exception");
                return false;
            }
            finally
            {
                if (recordingFile != null && File.Exists(recordingFile))
                {
                    try { File.Delete(recordingFile); } catch { }
                }
            }
        }
        
        /// <summary>
        /// Test 3: Realistic radio chatter with varying amplitude patterns.
        /// </summary>
        private static async Task<bool> Test_RealisticRadioChatter()
        {
            string? recordingFile = null;
            try
            {
                Logger.Info("Creating realistic radio chatter simulation...");
                
                // Generate synthetic recording with realistic patterns
                var progress = new Progress<int>(percent =>
                {
                    if (percent % 100 == 0)
                        Logger.Info($"Generating: {percent} packets");
                });
                
                recordingFile = await IO.SyntheticRecordingGenerator.GenerateAsync(
                    targetSizeMB: 1,  // Small 1MB file for quick testing
                    packetIntervalMs: 40,
                    cancellationToken: default,
                    progress: progress
                );
                
                Logger.Info($"Synthetic recording generated: {new FileInfo(recordingFile).Length / 1024}KB");
                
                // Open and process
                var stopwatch = Stopwatch.StartNew();
                
                var packetSource = new FilePacketSource(recordingFile);
                await packetSource.OpenAsync();
                
                Logger.Info($"Recording opened in {stopwatch.ElapsedMilliseconds}ms");
                Logger.Info($"Total packets: {packetSource.TotalPackets}");
                Logger.Info($"Total duration: {packetSource.TotalDuration.TotalSeconds:F1}s");
                
                var audioEngine = new AudioProcessingEngine();
                audioEngine.Initialize();
                
                var provider = new AmplitudeSeriesProvider(packetSource, audioEngine);
                
                // Extract amplitude data
                stopwatch.Restart();
                var startTime = packetSource.RecordingStart;
                var endTime = startTime.Add(packetSource.TotalDuration);
                
                var seriesCount = 0;
                var totalPoints = 0;
                
                await foreach (var (key, points) in provider.GetSeriesAsync(startTime, endTime))
                {
                    seriesCount++;
                    var pointsList = points.ToList();
                    totalPoints += pointsList.Count;
                    
                    // Log sample of points
                    if (pointsList.Count > 0)
                    {
                        var first = pointsList.First();
                        var last = pointsList.Last();
                        var maxDb = pointsList.Max(p => p.Y);
                        
                        Logger.Info($"Series {key}: {pointsList.Count} points, " +
                                  $"time: {first.X:F3}s-{last.X:F3}s, " +
                                  $"peak: {maxDb:F1} dBFS");
                    }
                }
                
                stopwatch.Stop();
                
                Logger.Info($"? Extracted {seriesCount} series with {totalPoints} points in {stopwatch.ElapsedMilliseconds}ms");
                Logger.Info($"Processing speed: {totalPoints / stopwatch.Elapsed.TotalSeconds:F0} points/sec");
                
                // Validate reasonable performance
                if (stopwatch.ElapsedMilliseconds > 5000)  // Should process in <5 seconds
                {
                    Logger.Warn($"Processing took {stopwatch.ElapsedMilliseconds}ms (>5000ms threshold)");
                }
                
                packetSource.Dispose();
                return seriesCount > 0 && totalPoints > 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Test failed with exception");
                return false;
            }
            finally
            {
                if (recordingFile != null && File.Exists(recordingFile))
                {
                    try { File.Delete(recordingFile); } catch { }
                }
            }
        }
        
        /// <summary>
        /// Test 4: Synthetic data fallback mode (no FilePacketSource).
        /// </summary>
        private static async Task<bool> Test_SyntheticDataFallback()
        {
            try
            {
                Logger.Info("Testing synthetic data fallback mode...");
                
                // Create provider without dependencies
                var provider = new AmplitudeSeriesProvider();
                
                var startTime = DateTime.UtcNow;
                var endTime = startTime.AddSeconds(5);
                
                var stopwatch = Stopwatch.StartNew();
                
                var seriesCount = 0;
                var totalPoints = 0;
                
                await foreach (var (key, points) in provider.GetSeriesAsync(startTime, endTime))
                {
                    seriesCount++;
                    var pointsList = points.ToList();
                    totalPoints += pointsList.Count;
                    
                    Logger.Info($"Synthetic series {key}: {pointsList.Count} points");
                    
                    // Validate synthetic data
                    if (pointsList.Count == 0)
                    {
                        Logger.Error("Synthetic series has no points");
                        return false;
                    }
                    
                    // Check time offsets start at 0
                    var firstPoint = pointsList.First();
                    if (firstPoint.X.HasValue && Math.Abs(firstPoint.X.Value) > 0.1)  // Should start near 0 seconds
                    {
                        Logger.Error($"Synthetic data doesn't start at 0: {firstPoint.X}s");
                        return false;
                    }
                }
                
                stopwatch.Stop();
                
                Logger.Info($"? Generated {seriesCount} synthetic series with {totalPoints} points in {stopwatch.ElapsedMilliseconds}ms");
                
                return seriesCount > 0 && totalPoints > 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Test failed with exception");
                return false;
            }
        }
    }
}
