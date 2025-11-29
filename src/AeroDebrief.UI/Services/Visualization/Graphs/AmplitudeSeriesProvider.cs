using AeroDebrief.UI.Interfaces.Visualization;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Interfaces.Audio;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Storage.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.UI.Services.Audio;
using LiveChartsCore.Defaults;
using NLog;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Phase 2 implementation: Provides amplitude time series data for visualization.
    /// Connects to IPacketSource and AmplitudeExtractor for real data pipeline.
    /// Supports DuckDB-based packet sources (DuckDBPacketSource) and legacy FilePacketSource.
    /// </summary>
    public sealed class AmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly IPacketSource? _packetSource;
        private readonly IAudioProcessingEngine? _audioEngine;
        private readonly AmplitudeExtractor? _extractor;
        
        /// <summary>
        /// Create provider with no dependencies (for testing with synthetic data).
        /// </summary>
        public AmplitudeSeriesProvider()
        {
            Logger.Info("AmplitudeSeriesProvider initialized (synthetic data mode)");
        }
        
        /// <summary>
        /// Create provider with real data pipeline dependencies.
        /// </summary>
        /// <param name="packetSource">Source for reading audio packets (FilePacketSource or DuckDBPacketSource)</param>
        /// <param name="audioEngine">Engine for decoding audio</param>
        public AmplitudeSeriesProvider(IPacketSource packetSource, IAudioProcessingEngine audioEngine)
        {
            _packetSource = packetSource ?? throw new ArgumentNullException(nameof(packetSource));
            _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
            
            // Read scale preference from settings
            var useDbScale = Core.Settings.PlayerSettingsStore.Instance.GetUseDbScale();
            
            // Create amplitude extractor with configured scale
            _extractor = new AmplitudeExtractor(
                _audioEngine,
                sampleRate: 48000,
                windowSizeMs: 10,
                hopSizeMs: 5,
                useDbScale: useDbScale  // Read from settings
            );
            
            var scaleMode = useDbScale ? "dBFS" : "linear (0-1)";
            Logger.Info($"AmplitudeSeriesProvider initialized (real data pipeline, {scaleMode} amplitude scale)");
        }

        /// <summary>
        /// Get amplitude series for the specified time range.
        /// Phase 2: Returns real data if dependencies provided, otherwise synthetic data.
        /// X-axis uses time offset in seconds from recording start (0:00:00).
        /// Phase 13: Progressive rendering - yields data in batches for immediate UI feedback.
        /// </summary>
        public async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
            DateTime start, 
            DateTime end, 
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            Logger.Debug($"GetSeriesAsync called: {start:HH:mm:ss} to {end:HH:mm:ss}");
            
            if (_packetSource != null && _extractor != null)
            {
                // Real implementation: Use packet source and extractor
                // Phase 13: Stream results as they're processed (progressive rendering)
                await foreach (var series in GetRealDataAsync(start, end, ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        Logger.Info("Series generation cancelled");
                        yield break;
                    }
                    
                    yield return series; // Stream each series as soon as it's ready!
                }
            }
            else
            {
                // Fallback: Generate synthetic data for testing
                Logger.Info("Using synthetic data (no packet source provided)");
                await foreach (var series in GenerateSyntheticDataAsync(start, end, ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        Logger.Info("Series generation cancelled");
                        yield break;
                    }
                    
                    yield return series;
                }
            }
        }
        
        /// <summary>
        /// Get real amplitude data from packet source.
        /// Groups packets by frequency and pilot, extracts amplitudes.
        /// X-axis uses time offset in seconds from recording start.
        /// Phase 13: Progressive rendering - yields batches immediately instead of waiting for all packets.
        /// </summary>
        private async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetRealDataAsync(
            DateTime start,
            DateTime end,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (_packetSource == null || _extractor == null)
                yield break;
            
            // CRITICAL: Ensure we're using UTC times to match database timestamps
            // RecordingStart from database is stored as UTC, so all comparisons must use UTC
            var startUtc = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
            var endUtc = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();
            
            Logger.Info($"Extracting real amplitude data from {startUtc:yyyy-MM-dd HH:mm:ss} UTC to {endUtc:yyyy-MM-dd HH:mm:ss} UTC");
            Logger.Info($"Progressive rendering enabled: Graph will update as packets are processed");
            
            var recordingStart = _packetSource.RecordingStart;
            var timeOffset = startUtc - recordingStart;
            
            Logger.Debug($"Recording start: {recordingStart:yyyy-MM-dd HH:mm:ss} (Kind={recordingStart.Kind})");
            Logger.Debug($"Time offset for packet query: {timeOffset.TotalSeconds:F1} seconds");
            
            // Phase 2.1: Group packets by frequency and transmitter, keep as RadioPacket to preserve amplitude_data
            var packetGroups = new Dictionary<(double frequency, string transmitter), List<RadioPacket>>();
            
            var packetCount = 0;
            var filteredOutCount = 0;
            var batchesYielded = 0;
            DateTime? firstPacketTime = null;
            DateTime? lastPacketTime = null;
            
            // Phase 13: Smaller batch size for more responsive UI updates
            const int BATCH_SIZE = 500; // Process 500 packets, then update UI (was 1000)
            
            await foreach (var radioPacket in _packetSource.ReadRange(timeOffset, ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;
                
                packetCount++;
                
                // Track first and last packet times for debugging
                if (firstPacketTime == null)
                    firstPacketTime = radioPacket.Timestamp;
                lastPacketTime = radioPacket.Timestamp;
                
                // CRITICAL: Ensure packet timestamp is UTC for comparison
                var packetTimestamp = radioPacket.Timestamp.Kind == DateTimeKind.Utc 
                    ? radioPacket.Timestamp 
                    : radioPacket.Timestamp.ToUniversalTime();
                
                // Filter by time range (using UTC times)
                if (packetTimestamp < startUtc || packetTimestamp > endUtc)
                {
                    filteredOutCount++;
                    
                    // Log first few filtered packets for debugging
                    if (filteredOutCount <= 3)
                    {
                        Logger.Debug($"Packet filtered out: {packetTimestamp:yyyy-MM-dd HH:mm:ss} (Kind={packetTimestamp.Kind}) is outside range {startUtc:yyyy-MM-dd HH:mm:ss} to {endUtc:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    continue;
                }
                
                // Group by frequency and transmitter
                var key = (radioPacket.Frequency, radioPacket.TransmitterGuid);
                if (!packetGroups.ContainsKey(key))
                {
                    packetGroups[key] = new List<RadioPacket>();
                }
                packetGroups[key].Add(radioPacket);

                // Phase 13: PROGRESSIVE RENDERING - Yield batches immediately!
                // Process in smaller batches and update UI more frequently
                var totalPacketsInBatch = packetGroups.Values.Sum(list => list.Count);
                if (totalPacketsInBatch >= BATCH_SIZE)
                {
                    batchesYielded++;
                    Logger.Debug($"Processing batch #{batchesYielded} ({totalPacketsInBatch} packets, {packetGroups.Count} series)");
                    
                    // Process and yield this batch immediately
                    await foreach (var series in ProcessPacketBatch(packetGroups, recordingStart, ct))
                    {
                        yield return series; // ? UI updates immediately!
                    }
                    
                    // Clear for next batch
                    packetGroups.Clear();
                    
                    // Yield to UI thread so user sees updates
                    await Task.Yield();
                }
            }
            
            Logger.Info($"Scanned {packetCount} packets from packet source");
            Logger.Info($"Filtered out {filteredOutCount} packets (outside time range)");
            Logger.Info($"Kept {packetCount - filteredOutCount} packets for visualization");
            Logger.Info($"Yielded {batchesYielded} progressive batches for responsive UI");
            
            if (firstPacketTime.HasValue && lastPacketTime.HasValue)
            {
                Logger.Info($"Packet time range in file: {firstPacketTime.Value:yyyy-MM-dd HH:mm:ss} to {lastPacketTime.Value:yyyy-MM-dd HH:mm:ss}");
                Logger.Info($"Requested time range:      {startUtc:yyyy-MM-dd HH:mm:ss} to {endUtc:yyyy-MM-dd HH:mm:ss}");
            }
            
            Logger.Debug($"Grouped into {packetGroups.Count} frequency/pilot combinations");
            
            // Phase 13: Process final remaining packets
            if (packetGroups.Count > 0)
            {
                Logger.Debug($"Processing final batch ({packetGroups.Values.Sum(list => list.Count)} packets)");
                await foreach (var series in ProcessPacketBatch(packetGroups, recordingStart, ct))
                {
                    yield return series; // ? Final UI update!
                }
            }
            
            Logger.Info($"Real amplitude extraction complete - processed {batchesYielded + (packetGroups.Count > 0 ? 1 : 0)} total batches");
        }
        
        /// <summary>
        /// Phase 2.1: Process a batch of packets grouped by frequency/pilot.
        /// Uses pre-computed amplitude data when available for 50x speedup.
        /// </summary>
        private async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> ProcessPacketBatch(
            Dictionary<(double frequency, string transmitter), List<RadioPacket>> packetGroups,
            DateTime recordingStart,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (_extractor == null)
                yield break;
            
            Logger.Debug($"Processing {packetGroups.Count} frequency/pilot combinations");
            
            var precomputedCount = 0;
            var onDemandCount = 0;
            
            foreach (var group in packetGroups)
            {
                if (ct.IsCancellationRequested)
                    yield break;
                
                var (frequency, transmitter) = group.Key;
                var packets = group.Value.OrderBy(p => p.Timestamp).ToList();
                
                // Convert frequency from Hz to MHz for key format
                var frequencyMHz = frequency / 1_000_000.0;
                var key = $"F{frequencyMHz:F1}-P{GetPilotIndex(transmitter)}";
                
                // Extract amplitude points from all packets for this pilot
                var points = new List<ObservablePoint>();
                
                foreach (var packet in packets)
                {
                    // Phase 2.1: Use new method that supports pre-computed amplitude
                    foreach (var point in _extractor.ExtractEnvelopeFromRadioPacket(packet, recordingStart))
                    {
                        points.Add(point);
                    }
                    
                    // Track which packets used pre-computed data
                    if (packet.AmplitudeData != null)
                        precomputedCount++;
                    else
                        onDemandCount++;
                }
                
                if (points.Count > 0)
                {
                    Logger.Trace($"Generated series {key} with {points.Count} points");
                    yield return (key, points);
                }
            }
            
            // Log statistics about pre-computed vs on-demand
            if (precomputedCount > 0 || onDemandCount > 0)
            {
                var total = precomputedCount + onDemandCount;
                var precomputedPct = (precomputedCount * 100.0) / total;
                Logger.Info($"Amplitude extraction: {precomputedCount} pre-computed ({precomputedPct:F1}%), {onDemandCount} on-demand");
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Get pilot index from GUID for consistent series naming.
        /// </summary>
        private static int GetPilotIndex(string transmitterGuid)
        {
            // Use hash code for consistent pilot numbering
            if (string.IsNullOrEmpty(transmitterGuid))
                return 0;
            
            return Math.Abs(transmitterGuid.GetHashCode() % 1000);
        }

        /// <summary>
        /// Temporary synthetic data generation for Phase 2 development.
        /// Used when no packet source is available (testing/fallback).
        /// X-axis uses time offset in seconds from start (0.0).
        /// </summary>
        private async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GenerateSyntheticDataAsync(
            DateTime start, 
            DateTime end,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var duration = end - start;
            var totalSeconds = Math.Max(1, (int)duration.TotalSeconds);
            var rnd = new Random(42);

            Logger.Debug($"Generating synthetic data: {totalSeconds}s duration");

            // Simulate 90% normal frequencies (4 pilots) + 10% high-traffic (24 pilots)
            int totalFreqs = 10; // Reduced for faster testing
            int highTrafficFreqs = Math.Max(1, (int)(totalFreqs * 0.1));
            int normalFreqs = totalFreqs - highTrafficFreqs;

            int freqIndex = 0;
            
            // Normal frequencies (4 pilots each)
            for (int i = 0; i < normalFreqs; i++, freqIndex++)
            {
                foreach (var series in GenerateSeriesForFrequency(freqIndex, 4, totalSeconds, rnd))
                {
                    yield return series;
                    await Task.Yield(); // Allow UI updates
                }
            }
            
            // High-traffic frequencies (24 pilots each)
            for (int i = 0; i < highTrafficFreqs; i++, freqIndex++)
            {
                foreach (var series in GenerateSeriesForFrequency(freqIndex, 24, totalSeconds, rnd))
                {
                    yield return series;
                    await Task.Yield();
                }
            }
            
            Logger.Debug($"Generated {freqIndex} frequencies");
        }

        /// <summary>
        /// Generate synthetic series for a single frequency with multiple pilots.
        /// Simulates realistic voice transmission patterns with peak amplitudes.
        /// X-axis starts at 0.0 seconds.
        /// </summary>
        private IEnumerable<(string key, IEnumerable<ObservablePoint> points)> GenerateSeriesForFrequency(
            int freqIndex, 
            int pilotCount, 
            int totalSeconds, 
            Random rnd)
        {
            var frequency = 251.0 + freqIndex;
            
            for (int pilotIdx = 0; pilotIdx < pilotCount; pilotIdx++)
            {
                var key = $"F{frequency:F1}-P{pilotIdx}";
                var points = new List<ObservablePoint>();
                
                // Generate points at 4 Hz (250ms intervals)
                // X-axis is time offset in seconds from start (0.0, 0.25, 0.5, ...)
                for (int sample = 0; sample < totalSeconds * 4; sample++)
                {
                    var timeOffsetSeconds = sample * 0.25; // 250ms = 0.25 seconds
                    
                    // Simulate voice amplitude pattern:
                    // - Base noise floor: -80 to -60 dBFS
                    // - Voice transmission: -40 to -20 dBFS with natural variation
                    var isTransmitting = rnd.NextDouble() < 0.3; // 30% transmission probability
                    
                    double amplitude;
                    if (isTransmitting)
                    {
                        // Voice transmission with natural variation (peak amplitude)
                        var baseAmplitude = -30.0; // dBFS
                        var variation = Math.Sin((sample + pilotIdx * 7) * 0.1) * 10.0;
                        var noise = rnd.NextDouble() * 5.0 - 2.5;
                        amplitude = baseAmplitude + variation + noise;
                    }
                    else
                    {
                        // Background noise
                        amplitude = -80.0 + rnd.NextDouble() * 20.0;
                    }
                    
                    // Clamp to valid dBFS range
                    amplitude = Math.Clamp(amplitude, -120.0, 0.0);
                    
                    points.Add(new ObservablePoint(timeOffsetSeconds, amplitude));
                }
                
                yield return (key, points);
            }
        }
    }
}
