using AeroDebrief.Core.Interfaces.Audio;
using System;
using System.Collections.Generic;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.UI.Services.Audio;
using LiveChartsCore.Defaults;
using NLog;

namespace AeroDebrief.UI.Services.Audio
{
    /// <summary>
    /// Extracts amplitude envelopes from audio packets for visualization.
    /// Computes instantaneous peak amplitude in linear scale (0.0 to 1.0).
    /// Uses time offset from recording start (in seconds) for X-axis values.
    /// Phase 2: Real implementation for amplitude timeline generation.
    /// </summary>
    public class AmplitudeExtractor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly IAudioProcessingEngine _audioEngine;
        private readonly int _sampleRate;
        private readonly int _windowSizeMs;
        private readonly int _hopSizeMs;
        
        // Computed values
        private readonly int _windowSizeSamples;
        private readonly int _hopSizeSamples;
        
        // Scale mode for amplitude display
        private readonly bool _useDbScale;
        
        /// <summary>
        /// Create amplitude extractor with specified parameters.
        /// /// <param name="audioEngine">Audio processing engine for decoding</param>
        /// <param name="sampleRate">Audio sample rate (typically 48000 Hz)</param>
        /// <param name="windowSizeMs">Peak detection window size in milliseconds (default 10ms)</param>
        /// <param name="hopSizeMs">Hop size between windows in milliseconds (default 5ms)</param>
        /// <param name="useDbScale">Use dB scale (true) or linear amplitude scale 0-1 (false, default)</param>
        public AmplitudeExtractor(
            IAudioProcessingEngine audioEngine,
            int sampleRate = 48000,
            int windowSizeMs = 10,
            int hopSizeMs = 5,
            bool useDbScale = false)
        {
            _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
            _sampleRate = sampleRate;
            _windowSizeMs = windowSizeMs;
            _hopSizeMs = hopSizeMs;
            _useDbScale = useDbScale;
            
            _windowSizeSamples = (sampleRate * windowSizeMs) / 1000;
            _hopSizeSamples = (sampleRate * hopSizeMs) / 1000;
            
            var scaleMode = _useDbScale ? "dBFS" : "linear (0-1)";
            Logger.Info($"AmplitudeExtractor initialized: {sampleRate}Hz, {windowSizeMs}ms window, {hopSizeMs}ms hop, scale={scaleMode} (peak amplitude, time-offset mode)");
            Logger.Debug($"Window: {_windowSizeSamples} samples, Hop: {_hopSizeSamples} samples");
            
            // Enable caching in the audio engine for performance
            _audioEngine.EnableAmplitudeCache();
            Logger.Info("Amplitude caching enabled in audio engine");
        }
        
        /// <summary>
        /// Extract amplitude envelope from a single audio packet using cached decoding.
        /// Returns peak amplitude points per window using sliding window.
        /// X-axis uses time offset in seconds from recording start.
        /// Phase 13: Now uses cached decoding for better performance on repeated queries.
        /// </summary>
        /// <param name="packet">Audio packet to process</param>
        /// <param name="recordingStart">Recording start time for offset calculation</param>
        /// <returns>Sequence of amplitude points (time offset in seconds, amplitude in linear or dBFS scale)</returns>
        public IEnumerable<ObservablePoint> ExtractEnvelope(AudioPacketMetadata packet, DateTime recordingStart)
        {
            if (packet?.AudioPayload == null || packet.AudioPayload.Length == 0)
            {
                Logger.Trace($"Empty packet, returning silence point");
                var timeOffset = (packet?.Timestamp ?? recordingStart) - recordingStart;
                var silenceValue = _useDbScale ? DbFSConverter.MinDbFS : 0.0;
                yield return new ObservablePoint(timeOffset.TotalSeconds, silenceValue);
                yield break;
            }
            
            // Process packet outside try-catch to allow yield
            List<ObservablePoint> points = ProcessPacket(packet, recordingStart);
            
            // Yield all collected points
            foreach (var point in points)
            {
                yield return point;
            }
        }
        
        /// <summary>
        /// Internal method to process a packet and return points.
        /// Separated to allow proper exception handling without yield issues.
        /// Phase 13: Uses cached decoding for performance.
        /// </summary>
        private List<ObservablePoint> ProcessPacket(AudioPacketMetadata packet, DateTime recordingStart)
        {
            try
            {
                // Decode packet to PCM float samples (WITH CACHING)
                var samples = _audioEngine.DecodePacketToFloatCached(packet);
                
                if (samples == null || samples.Length == 0)
                {
                    Logger.Warn($"Failed to decode packet from {packet.TransmitterGuid}");
                    var timeOffset = packet.Timestamp - recordingStart;
                    var silenceValue = _useDbScale ? DbFSConverter.MinDbFS : 0.0;
                    return new List<ObservablePoint> { new ObservablePoint(timeOffset.TotalSeconds, silenceValue) };
                }
                
                // Extract peak amplitude values using sliding window
                var peakValues = ComputePeakAmplitudeEnvelope(samples);
                
                // Convert to appropriate scale and create time series
                var packetDuration = TimeSpan.FromSeconds((double)samples.Length / _sampleRate);
                var pointCount = peakValues.Count;
                
                var points = new List<ObservablePoint>(pointCount);
                
                // Calculate base time offset for this packet
                var baseTimeOffset = (packet.Timestamp - recordingStart).TotalSeconds;
                
                for (int i = 0; i < pointCount; i++)
                {
                    var peakAmplitude = peakValues[i];
                    
                    // Convert to appropriate scale (dBFS or linear)
                    var amplitudeValue = _useDbScale 
                        ? DbFSConverter.LinearToDbFS(peakAmplitude)
                        : peakAmplitude; // Use linear amplitude directly (0.0 to 1.0)
                    
                    // Calculate time offset for this point within the packet
                    var pointOffsetMs = i * _hopSizeMs;
                    var timeOffsetSeconds = baseTimeOffset + (pointOffsetMs / 1000.0);
                    
                    points.Add(new ObservablePoint(timeOffsetSeconds, amplitudeValue));
                }
                
                var scaleType = _useDbScale ? "dBFS" : "linear";
                Logger.Trace($"Extracted {pointCount} peak amplitude points ({scaleType}) from packet at offset {baseTimeOffset:F3}s, duration {packetDuration.TotalMilliseconds:F1}ms");
                return points;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error extracting amplitude from packet {packet.PacketId}");
                var timeOffset = packet.Timestamp - recordingStart;
                var silenceValue = _useDbScale ? DbFSConverter.MinDbFS : 0.0;
                return new List<ObservablePoint> { new ObservablePoint(timeOffset.TotalSeconds, silenceValue) };
            }
        }
        
        /// <summary>
        /// Compute peak amplitude envelope using sliding window.
        /// Returns the maximum absolute amplitude in each window.
        /// </summary>
        /// <param name="samples">Audio samples (float, -1.0 to 1.0)</param>
        /// <returns>List of peak amplitude values</returns>
        private List<double> ComputePeakAmplitudeEnvelope(float[] samples)
        {
            var peakValues = new List<double>();
            
            // Slide window across samples with hop size
            for (int start = 0; start < samples.Length; start += _hopSizeSamples)
            {
                var end = Math.Min(start + _windowSizeSamples, samples.Length);
                var windowLength = end - start;
                
                if (windowLength < _windowSizeSamples / 2)
                {
                    // Skip partial windows at the end that are too small
                    break;
                }
                
                // Find maximum absolute amplitude in this window
                double peakAmplitude = 0.0;
                for (int i = start; i < end; i++)
                {
                    var absoluteValue = Math.Abs(samples[i]);
                    if (absoluteValue > peakAmplitude)
                    {
                        peakAmplitude = absoluteValue;
                    }
                }
                
                peakValues.Add(peakAmplitude);
            }
            
            return peakValues;
        }
        
        /// <summary>
        /// Extract amplitude envelope from multiple packets efficiently.
        /// Processes packets in sequence and yields results as they're computed.
        /// </summary>
        /// <param name="packets">Sequence of audio packets</param>
        /// <param name="recordingStart">Recording start time for offset calculation</param>
        /// <returns>Amplitude points in time order (time offset in seconds, amplitude in linear or dBFS scale)</returns>
        public IEnumerable<ObservablePoint> ExtractEnvelopeFromPackets(IEnumerable<AudioPacketMetadata> packets, DateTime recordingStart)
        {
            var packetCount = 0;
            var pointCount = 0;
            
            foreach (var packet in packets)
            {
                packetCount++;
                
                foreach (var point in ExtractEnvelope(packet, recordingStart))
                {
                    pointCount++;
                    yield return point;
                }
            }
            
            var scaleType = _useDbScale ? "dBFS" : "linear";
            Logger.Debug($"Extracted {pointCount} peak amplitude points ({scaleType}) from {packetCount} packets");
        }
        
        /// <summary>
        /// Calculate peak amplitude directly from float samples.
        /// Returns the maximum absolute value.
        /// </summary>
        /// <param name="samples">Audio samples</param>
        /// <returns>Peak amplitude (0.0 to 1.0)</returns>
        public static double CalculatePeakAmplitude(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return 0.0;
            
            double peak = 0.0;
            foreach (var sample in samples)
            {
                var absoluteValue = Math.Abs(sample);
                if (absoluteValue > peak)
                {
                    peak = absoluteValue;
                }
            }
            
            return peak;
        }
        
        /// <summary>
        /// Calculate peak amplitude from a span of samples.
        /// Efficient for processing sub-sections.
        /// </summary>
        /// <param name="samples">Audio sample span</param>
        /// <returns>Peak amplitude</returns>
        public static double CalculatePeakAmplitude(ReadOnlySpan<float> samples)
        {
            if (samples.Length == 0)
                return 0.0;
            
            double peak = 0.0;
            foreach (var sample in samples)
            {
                var absoluteValue = Math.Abs(sample);
                if (absoluteValue > peak)
                {
                    peak = absoluteValue;
                }
            }
            
            return peak;
        }
        
        /// <summary>
        /// Calculate RMS amplitude directly from float samples.
        /// Useful for comparison or alternative visualization.
        /// </summary>
        /// <param name="samples">Audio samples</param>
        /// <returns>RMS amplitude (0.0 to 1.0)</returns>
        public static double CalculateRMS(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return 0.0;
            
            double sumOfSquares = 0.0;
            foreach (var sample in samples)
            {
                sumOfSquares += sample * sample;
            }
            
            return Math.Sqrt(sumOfSquares / samples.Length);
        }
        
        /// <summary>
        /// Calculate RMS amplitude from a span of samples.
        /// Useful for comparison or alternative visualization.
        /// </summary>
        /// <param name="samples">Audio sample span</param>
        /// <returns>RMS amplitude</returns>
        public static double CalculateRMS(ReadOnlySpan<float> samples)
        {
            if (samples.Length == 0)
                return 0.0;
            
            double sumOfSquares = 0.0;
            foreach (var sample in samples)
            {
                sumOfSquares += sample * sample;
            }
            
            return Math.Sqrt(sumOfSquares / samples.Length);
        }
    }
}
