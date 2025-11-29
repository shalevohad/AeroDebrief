using System;
using System.Collections.Generic;
using System.Linq;
using AeroDebrief.Core.Helpers;
using NLog;

namespace AeroDebrief.Core.Storage
{
    /// <summary>
    /// Phase 2.1: Service for pre-computing amplitude data during recording.
    /// Computes peak amplitude at configurable resolution for visualization.
    /// </summary>
    public class AmplitudePrecomputationService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // Default: 5ms resolution = 200 Hz sampling rate for amplitude
        // This provides good balance between storage size and visualization quality
        private const int DEFAULT_RESOLUTION_MS = 5;
        
        public AmplitudePrecomputationService()
        {
        }
        
        /// <summary>
        /// Compute amplitude data from audio packet for storage in database.
        /// </summary>
        /// <param name="audioPayload">Encoded audio data (OPUS)</param>
        /// <param name="sampleRate">Audio sample rate (typically 48000 Hz)</param>
        /// <param name="resolutionMs">Time resolution in milliseconds (default 5ms)</param>
        /// <returns>Byte array of float32 amplitude values (linear 0.0-1.0)</returns>
        public byte[]? ComputeAmplitudeData(byte[] audioPayload, int sampleRate, int resolutionMs = DEFAULT_RESOLUTION_MS)
        {
            if (audioPayload == null || audioPayload.Length == 0)
            {
                return null; // No audio data, no amplitude data
            }
            
            try
            {
                // Decode audio to PCM using AudioHelpers
                var pcmData16 = AudioHelpers.DecodeOpusToPcm(audioPayload);
                if (pcmData16 == null || pcmData16.Length == 0)
                {
                    Logger.Trace("Failed to decode audio or empty PCM data");
                    return null;
                }
                
                // Convert short[] PCM to float[] (normalize to -1.0 to 1.0)
                var pcmDataFloat = new float[pcmData16.Length];
                for (int i = 0; i < pcmData16.Length; i++)
                {
                    pcmDataFloat[i] = pcmData16[i] / 32768.0f; // Normalize int16 to float
                }
                
                // Calculate window size in samples
                var windowSizeSamples = (sampleRate * resolutionMs) / 1000;
                if (windowSizeSamples <= 0)
                {
                    Logger.Warn($"Invalid window size: {windowSizeSamples} samples");
                    return null;
                }
                
                // Compute peak amplitude for each window
                var amplitudes = new List<float>();
                
                for (int i = 0; i < pcmDataFloat.Length; i += windowSizeSamples)
                {
                    var windowEnd = Math.Min(i + windowSizeSamples, pcmDataFloat.Length);
                    
                    // Find peak amplitude in window (linear scale 0.0-1.0)
                    float peak = 0.0f;
                    for (int j = i; j < windowEnd; j++)
                    {
                        var sample = Math.Abs(pcmDataFloat[j]);
                        if (sample > peak)
                            peak = sample;
                    }
                    
                    // Peak is already normalized to 0.0-1.0 range
                    amplitudes.Add(peak);
                }
                
                // Convert float list to byte array (4 bytes per float32)
                var byteArray = new byte[amplitudes.Count * sizeof(float)];
                Buffer.BlockCopy(amplitudes.ToArray(), 0, byteArray, 0, byteArray.Length);
                
                Logger.Trace($"Computed {amplitudes.Count} amplitude points from {pcmDataFloat.Length} samples (window: {windowSizeSamples} samples)");
                
                return byteArray;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to compute amplitude data");
                return null; // Return null on error, will be computed on-demand later
            }
        }
        
        /// <summary>
        /// Deserialize amplitude data from database storage.
        /// </summary>
        /// <param name="amplitudeData">Byte array from database</param>
        /// <returns>Array of float amplitude values (linear 0.0-1.0)</returns>
        public static float[]? DeserializeAmplitudeData(byte[]? amplitudeData)
        {
            if (amplitudeData == null || amplitudeData.Length == 0)
            {
                return null;
            }
            
            try
            {
                var floatCount = amplitudeData.Length / sizeof(float);
                var floats = new float[floatCount];
                Buffer.BlockCopy(amplitudeData, 0, floats, 0, amplitudeData.Length);
                return floats;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to deserialize amplitude data");
                return null;
            }
        }
        
        /// <summary>
        /// Get the default amplitude resolution in milliseconds.
        /// </summary>
        public static int GetDefaultResolutionMs() => DEFAULT_RESOLUTION_MS;
    }
}
