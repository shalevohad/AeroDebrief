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
    /// Connects to FilePacketSource and AmplitudeExtractor for real data pipeline.
    /// </summary>
    public sealed class AmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly FilePacketSource? _packetSource;
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
        /// <param name="packetSource">Source for reading audio packets</param>
        /// <param name="audioEngine">Engine for decoding audio</param>
        public AmplitudeSeriesProvider(FilePacketSource packetSource, IAudioProcessingEngine audioEngine)
        {
            _packetSource = packetSource ?? throw new ArgumentNullException(nameof(packetSource));
            _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
            
            // Create amplitude extractor
            _extractor = new AmplitudeExtractor(
                _audioEngine,
                sampleRate: 48000,
                windowSizeMs: 10,
                hopSizeMs: 5
            );
            
            Logger.Info("AmplitudeSeriesProvider initialized (real data pipeline)");
        }

        /// <summary>
        /// Get amplitude series for the specified time range.
        /// Phase 2: Returns real data if dependencies provided, otherwise synthetic data.
        /// X-axis uses time offset in seconds from recording start (0:00:00).
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
                await foreach (var series in GetRealDataAsync(start, end, ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        Logger.Info("Series generation cancelled");
                        yield break;
                    }
                    
                    yield return series;
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
        /// </summary>
        private async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetRealDataAsync(
            DateTime start,
            DateTime end,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (_packetSource == null || _extractor == null)
                yield break;
            
            Logger.Info($"Extracting real amplitude data from {start:HH:mm:ss} to {end:HH:mm:ss}");
            
            var recordingStart = _packetSource.RecordingStart;
            var timeOffset = start - recordingStart;
            
            // Group packets by frequency and transmitter as we read them
            var packetGroups = new Dictionary<(double frequency, string transmitter), List<AudioPacketMetadata>>();
            
            await foreach (var radioPacket in _packetSource.ReadRange(timeOffset, ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;
                
                // Convert RadioPacket to AudioPacketMetadata
                var metadata = radioPacket.ToMetadata();
                
                // Filter by time range
                if (metadata.Timestamp < start || metadata.Timestamp > end)
                    continue;
                
                // Group by frequency and transmitter
                var key = (metadata.Frequency, metadata.TransmitterGuid);
                if (!packetGroups.ContainsKey(key))
                {
                    packetGroups[key] = new List<AudioPacketMetadata>();
                }
                packetGroups[key].Add(metadata);
                
                // Process in batches to avoid memory buildup (every 1000 packets)
                if (packetGroups.Values.Sum(list => list.Count) >= 1000)
                {
                    await foreach (var series in ProcessPacketBatch(packetGroups, recordingStart, ct))
                    {
                        yield return series;
                    }
                    packetGroups.Clear();
                }
            }
            
            // Process remaining packets
            if (packetGroups.Count > 0)
            {
                await foreach (var series in ProcessPacketBatch(packetGroups, recordingStart, ct))
                {
                    yield return series;
                }
            }
            
            Logger.Info("Real amplitude extraction complete");
        }
        
        /// <summary>
        /// Process a batch of packets grouped by frequency/pilot.
        /// </summary>
        private async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> ProcessPacketBatch(
            Dictionary<(double frequency, string transmitter), List<AudioPacketMetadata>> packetGroups,
            DateTime recordingStart,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (_extractor == null)
                yield break;
            
            Logger.Debug($"Processing {packetGroups.Count} frequency/pilot combinations");
            
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
                    foreach (var point in _extractor.ExtractEnvelope(packet, recordingStart))
                    {
                        points.Add(point);
                    }
                }
                
                if (points.Count > 0)
                {
                    Logger.Trace($"Generated series {key} with {points.Count} points");
                    yield return (key, points);
                }
                
                await Task.Yield(); // Allow UI updates
            }
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
