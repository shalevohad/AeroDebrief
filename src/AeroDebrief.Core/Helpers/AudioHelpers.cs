using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NLog;
using NAudio.Wave;
using AeroDebrief.Core.Audio;

namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Helper methods for audio processing, conversion, and analysis
    /// </summary>
    public static class AudioHelpers
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Audio Calculations

        /// <summary>
        /// Converts audio sample count to duration
        /// </summary>
        /// <param name="sampleCount">Number of samples</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Duration as TimeSpan</returns>
        public static TimeSpan SamplesToDuration(int sampleCount, int sampleRate)
        {
            if (sampleRate <= 0) return TimeSpan.Zero;
            
            double seconds = (double)sampleCount / sampleRate;
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// Converts duration to sample count
        /// </summary>
        /// <param name="duration">Duration as TimeSpan</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Number of samples</returns>
        public static int DurationToSamples(TimeSpan duration, int sampleRate)
        {
            return (int)(duration.TotalSeconds * sampleRate);
        }

        /// <summary>
        /// Formats audio size in human-readable format
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        public static string FormatAudioSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Audio Conversion

        /// <summary>
        /// Converts 16-bit PCM audio data to float array
        /// </summary>
        /// <param name="pcmData">16-bit PCM audio data</param>
        /// <returns>Float array with values in -1.0 to 1.0 range</returns>
        public static float[] ConvertPcm16ToFloat(byte[] pcmData)
        {
            var floatData = new float[pcmData.Length / 2];
            for (int i = 0; i < floatData.Length; i++)
            {
                short pcmSample = BitConverter.ToInt16(pcmData, i * 2);
                floatData[i] = pcmSample / 32768.0f; // Convert to -1.0 to 1.0 range
            }
            return floatData;
        }

        /// <summary>
        /// Resamples audio from one sample rate to another using linear interpolation
        /// </summary>
        /// <param name="inputAudio">Input audio data</param>
        /// <param name="inputSampleRate">Input sample rate</param>
        /// <param name="outputSampleRate">Output sample rate</param>
        /// <returns>Resampled audio data</returns>
        public static float[] ResampleAudio(float[] inputAudio, int inputSampleRate, int outputSampleRate)
        {
            if (inputSampleRate == outputSampleRate)
                return inputAudio;

            // Simple linear interpolation resampling
            double ratio = (double)inputSampleRate / outputSampleRate;
            int outputLength = (int)(inputAudio.Length / ratio);
            var outputAudio = new float[outputLength];

            for (int i = 0; i < outputLength; i++)
            {
                double sourceIndex = i * ratio;
                int index1 = (int)Math.Floor(sourceIndex);
                int index2 = Math.Min(index1 + 1, inputAudio.Length - 1);
                float fraction = (float)(sourceIndex - index1);

                if (index1 < inputAudio.Length)
                {
                    outputAudio[i] = inputAudio[index1] * (1 - fraction) + 
                                   (index2 < inputAudio.Length ? inputAudio[index2] * fraction : 0);
                }
            }

#if DEBUG
            Logger.Debug($"Resampled audio: {inputSampleRate}Hz -> {outputSampleRate}Hz ({inputAudio.Length} -> {outputLength} samples)");
#endif
            return outputAudio;
        }

        #endregion

        #region Audio Analysis

        /// <summary>
        /// Detects if audio packet contains OPUS encoded data
        /// </summary>
        /// <param name="packet">Audio packet metadata</param>
        /// <returns>True if OPUS encoded, false if PCM</returns>
        public static bool IsOpusEncoded(AudioPacketMetadata packet)
        {
            if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                return false;

            return IsOpusEncodedByteArray(packet.AudioPayload);
        }

        /// <summary>
        /// Detects if raw audio byte array contains OPUS encoded data
        /// </summary>
        /// <param name="audioData">Raw audio bytes</param>
        /// <returns>True if OPUS encoded, false if PCM</returns>
        public static bool IsOpusEncodedByteArray(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return false;

            // VALIDATION: Reject obviously corrupted data
            // Valid Opus packets are typically 20-400 bytes
            // Valid PCM is typically 1920 bytes (40ms @ 48kHz mono)
            const int minValidOpusSize = 10;     // Minimum for any valid Opus frame
            const int maxValidOpusSize = 1500;   // Maximum reasonable Opus packet
            const int minValidPcmSize = 160;     // Minimum PCM (1ms @ 48kHz)
            const int maxValidPcmSize = 9600;    // Maximum PCM (50ms @ 48kHz stereo)
            
            // Reject suspiciously small packets
            if (audioData.Length < minValidOpusSize)
            {
                return false; // Too small to be valid
            }
            
            // Reject suspiciously large packets (likely corruption)
            if (audioData.Length > maxValidPcmSize)
            {
                return false; // Way too large - corrupted data
            }

            // Size-based heuristic: PCM=1920 bytes, OPUS=60-400 bytes
            const int expectedPcmSize = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000 * 2;
            const int opusMaxSize = 400;
            
            if (audioData.Length <= opusMaxSize && audioData.Length < expectedPcmSize / 3)
                return true;

            // Check for OPUS header patterns
            if (audioData.Length >= 2)
            {
                byte firstByte = audioData[0];
                if ((firstByte & 0x80) != 0)
                    return true;
            }

            return audioData.Length < 500;
        }

        #endregion

        #region Opus Decoding

        /// <summary>
        /// Decodes audio payload to PCM Int16 samples, automatically detecting format.
        /// Includes comprehensive validation to detect and skip corrupted data.
        /// </summary>
        /// <param name="audioData">Audio data (Opus-encoded or raw PCM bytes)</param>
        /// <returns>PCM Int16 samples, or empty array if data is corrupted/invalid</returns>
        public static short[] DecodeAudioToPcm(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return Array.Empty<short>();

            // VALIDATION: Detect obviously corrupted data
            const int minValidSize = 10;        // Minimum for any valid audio
            const int maxValidSize = 10000;     // Maximum reasonable audio packet
            
            if (audioData.Length < minValidSize)
            {
                // Too small - likely corruption
                return Array.Empty<short>();
            }
            
            if (audioData.Length > maxValidSize)
            {
                // Way too large - definitely corruption
                // Only log once per 100 occurrences to reduce spam
                if (audioData.Length % 100 == 0)
                {
                    Logger.Debug($"Rejecting oversized audio payload ({audioData.Length} bytes) - likely corruption");
                }
                return Array.Empty<short>();
            }

            if (IsOpusEncodedByteArray(audioData))
                return DecodeOpusToPcm(audioData);
            else
                return ConvertBytesToPcm16(audioData);
        }

        /// <summary>
        /// Decodes Opus-encoded bytes to PCM Int16 samples
        /// </summary>
        /// <param name="opusData">Opus-encoded audio data</param>
        /// <returns>PCM Int16 samples, or empty array if decoding fails</returns>
        public static short[] DecodeOpusToPcm(byte[] opusData)
        {
            if (opusData == null || opusData.Length == 0)
                return Array.Empty<short>();

            // VALIDATION: Comprehensive size checks
            const int minValidOpusSize = 10;    // Minimum Opus frame
            const int maxValidOpusSize = 1500;  // Maximum reasonable Opus packet
            
            if (opusData.Length < minValidOpusSize)
            {
                // Too small - skip silently (likely corruption)
                return Array.Empty<short>();
            }

            if (opusData.Length > maxValidOpusSize)
            {
                // Way too large - log once per occurrence type
                var sizeCategory = (opusData.Length / 10000) * 10000; // Group by 10KB
                if (sizeCategory % 100000 == 0) // Only log every 100KB category
                {
                    Logger.Debug($"Skipping oversized Opus packet (~{sizeCategory/1000}KB) - corruption detected");
                }
                return Array.Empty<short>();
            }

            try
            {
                using var decoder = Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core.OpusDecoder.Create(
                    Constants.OUTPUT_SAMPLE_RATE, 1);
                
                decoder.ForwardErrorCorrection = false;

                var pcmBuffer = new short[Constants.OPUS_FRAME_SIZE * 6];
                
                if (pcmBuffer.Length == 0)
                {
                    Logger.Error("PCM buffer has zero length");
                    return Array.Empty<short>();
                }

                var samplesDecoded = decoder.DecodeShort(opusData, pcmBuffer, pcmBuffer.Length, false);

                if (samplesDecoded < 0)
                {
                    // Decoder error - skip silently (corruption)
                    return Array.Empty<short>();
                }

                if (samplesDecoded > pcmBuffer.Length)
                {
                    Logger.Error($"Opus decoder buffer overflow prevented: {samplesDecoded} > {pcmBuffer.Length}");
                    return Array.Empty<short>();
                }

                if (samplesDecoded > 0)
                {
                    var result = new short[samplesDecoded];
                    Array.Copy(pcmBuffer, result, samplesDecoded);
                    return result;
                }

                // Zero samples - skip silently
                return Array.Empty<short>();
            }
            catch (Exception ex)
            {
                // Decoder exception - skip silently in release, log in debug
                // This is expected for corrupted packets
#if DEBUG
                if (opusData.Length > 100) // Only log for packets that should be valid
                {
                    Logger.Debug($"Opus decode failed for {opusData.Length}B packet (likely corruption): {ex.Message}");
                }
#endif
                return Array.Empty<short>();
            }
        }

        /// <summary>
        /// Converts raw byte array to PCM Int16 samples
        /// </summary>
        /// <param name="pcmBytes">Raw PCM bytes (16-bit little-endian)</param>
        /// <returns>PCM Int16 samples</returns>
        public static short[] ConvertBytesToPcm16(byte[] pcmBytes)
        {
            if (pcmBytes == null || pcmBytes.Length == 0)
                return Array.Empty<short>();

            // VALIDATION: Reject obviously corrupted data
            const int maxReasonablePcmSize = 10000; // 50ms @ 48kHz stereo
            
            if (pcmBytes.Length > maxReasonablePcmSize)
            {
                // Way too large - corruption
                return Array.Empty<short>();
            }

            if (pcmBytes.Length % 2 != 0)
            {
                // Odd length - truncate last byte (minor corruption)
                // Only log in debug mode to reduce spam
#if DEBUG
                if (pcmBytes.Length > 1000) // Only log for larger packets
                {
                    Logger.Debug($"PCM byte array has odd length ({pcmBytes.Length}), truncating last byte");
                }
#endif
            }

            var sampleCount = pcmBytes.Length / 2;
            var samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = BitConverter.ToInt16(pcmBytes, i * 2);
            }

            return samples;
        }

        /// <summary>
        /// Converts PCM Int16 samples to byte array
        /// </summary>
        /// <param name="pcmSamples">PCM Int16 samples</param>
        /// <returns>Raw PCM bytes (16-bit little-endian)</returns>
        public static byte[] ConvertPcm16ToBytes(short[] pcmSamples)
        {
            if (pcmSamples == null || pcmSamples.Length == 0)
                return Array.Empty<byte>();

            var bytes = new byte[pcmSamples.Length * 2];
            Buffer.BlockCopy(pcmSamples, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Calculates normalized peak amplitude (0.0 to 1.0) from PCM Int16 samples
        /// </summary>
        /// <param name="pcmSamples">PCM Int16 samples</param>
        /// <returns>Normalized peak amplitude in range [0.0, 1.0]</returns>
        public static float CalculateNormalizedAmplitude(short[] pcmSamples)
        {
            if (pcmSamples == null || pcmSamples.Length == 0)
                return 0f;

            // Calculate peak amplitude (maximum absolute value)
            int maxAmplitude = 0;
            foreach (var sample in pcmSamples)
            {
                // Cast to int before taking Abs to avoid Int16.MinValue negation overflow
                int absSample = Math.Abs((int)sample);
                maxAmplitude = Math.Max(maxAmplitude, absSample);
            }

            // Normalize to [0.0, 1.0] range
            var normalized = maxAmplitude / 32768.0f;
            
            return normalized;
        }

        #endregion

        #region Audio Export

        /// <summary>
        /// Exports audio data from recorded file to WAV format for external analysis
        /// </summary>
        /// <param name="sourceFilePath">Path to the source recording file</param>
        /// <param name="outputWavPath">Path where the WAV file should be saved</param>
        /// <param name="maxPackets">Maximum number of packets to export (default: 100)</param>
        /// <returns>Task representing the async export operation</returns>
        public static async Task ExportToWavAsync(string sourceFilePath, string outputWavPath, int maxPackets = 100)
        {
            try
            {
                Logger.Info($"Exporting audio to WAV: {System.IO.Path.GetFileName(outputWavPath)}");
                
                var audioData = new List<float>();
                var packetsProcessed = 0;
                var opusPacketsDecoded = 0;
                var pcmPacketsProcessed = 0;
                
                using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);
                
                while (fs.Position < fs.Length && packetsProcessed < maxPackets)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var packet) && packet != null)
                    {
                        if (packet.AudioPayload?.Length > 0)
                        {
                            var pcmSamples = DecodeAudioToPcm(packet.AudioPayload);
                            
                            if (pcmSamples.Length > 0)
                            {
                                var floatSamples = new float[pcmSamples.Length];
                                for (int i = 0; i < pcmSamples.Length; i++)
                                {
                                    floatSamples[i] = pcmSamples[i] / 32768.0f;
                                }
                                
                                audioData.AddRange(floatSamples);
                                packetsProcessed++;
                                
                                if (IsOpusEncodedByteArray(packet.AudioPayload))
                                    opusPacketsDecoded++;
                                else
                                    pcmPacketsProcessed++;
                            }
                        }
                    }
                    else break;
                }
                
                if (audioData.Count > 0)
                {
                    var pcmData = AudioConverter.FloatToPcm16(audioData.ToArray());
                    
                    using var waveFileWriter = new WaveFileWriter(outputWavPath, 
                        new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 16, 1));
                    waveFileWriter.Write(pcmData, 0, pcmData.Length);
                    
                    Logger.Info($"Exported {packetsProcessed} packets ({opusPacketsDecoded} Opus, {pcmPacketsProcessed} PCM) to WAV");
                }
                else
                {
                    Logger.Warn("No audio data found to export");
                    throw new InvalidOperationException("No exportable audio data found in the recording file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export audio to WAV");
                throw;
            }
        }

        /// <summary>
        /// Estimates the output WAV file size before exporting
        /// </summary>
        /// <param name="sourceFilePath">Path to the source recording file</param>
        /// <param name="maxPackets">Maximum number of packets to analyze</param>
        /// <returns>Estimated output file size in bytes, or -1 if estimation failed</returns>
        public static async Task<long> EstimateWavExportSizeAsync(string sourceFilePath, int maxPackets = 100)
        {
            try
            {
                var totalSamples = 0;
                var packetsAnalyzed = 0;
                
                using var fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);
                
                while (fs.Position < fs.Length && packetsAnalyzed < maxPackets)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var packet) && packet != null)
                    {
                        if (packet.AudioPayload?.Length > 0)
                        {
                            var pcmSamples = DecodeAudioToPcm(packet.AudioPayload);
                            totalSamples += pcmSamples.Length;
                            packetsAnalyzed++;
                        }
                    }
                    else break;
                }
                
                if (totalSamples > 0)
                {
                    const int wavHeaderSize = 44;
                    return wavHeaderSize + (totalSamples * 2);
                }
                
                return -1;
            }
            catch (Exception ex)
            {
#if DEBUG
                Logger.Error(ex, "Failed to estimate WAV export size");
#else
                Logger.Warn("Failed to estimate WAV export size");
#endif
                return -1;
            }
        }

        #endregion
    }
}