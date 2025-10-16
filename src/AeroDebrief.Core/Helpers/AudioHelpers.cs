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

            Logger.Debug($"Resampled audio from {inputSampleRate}Hz to {outputSampleRate}Hz: {inputAudio.Length} -> {outputLength} samples");
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

            // Multiple detection methods for robustness
            
            // Method 1: Size-based heuristic
            // PCM audio for 20ms at 48kHz mono 16-bit = 1920 bytes
            // OPUS for same duration is typically 60-200 bytes
            const int expectedPcmSize = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000 * 2; // 1920 bytes
            const int opusMaxSize = 400; // Conservative threshold
            
            if (audioData.Length <= opusMaxSize && audioData.Length < expectedPcmSize / 3)
            {
                //Logger.Debug($"Detected OPUS by size: {audioData.Length} bytes (expected PCM: {expectedPcmSize})");
                return true;
            }

            // Method 2: Check for OPUS header patterns (first few bytes)
            // OPUS packets often start with specific bit patterns
            if (audioData.Length >= 2)
            {
                byte firstByte = audioData[0];
                // Check for OPUS configuration bits in first byte
                // This is a simplified check - OPUS has complex headers
                if ((firstByte & 0x80) != 0) // Check if it looks like OPUS config
                {
                    //Logger.Debug($"Detected OPUS by header pattern: 0x{firstByte:X2}");
                    return true;
                }
            }

            // Method 3: Fallback - assume smaller packets are OPUS
            return audioData.Length < 500; // Conservative threshold
        }

        #endregion

        #region Opus Decoding

        /// <summary>
        /// Decodes audio payload to PCM Int16 samples, automatically detecting format (Opus or raw PCM)
        /// </summary>
        /// <param name="audioData">Audio data (Opus-encoded or raw PCM bytes)</param>
        /// <returns>PCM Int16 samples</returns>
        public static short[] DecodeAudioToPcm(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return Array.Empty<short>();

            // Auto-detect format and decode
            if (IsOpusEncodedByteArray(audioData))
            {
                return DecodeOpusToPcm(audioData);
            }
            else
            {
                return ConvertBytesToPcm16(audioData);
            }
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

            try
            {
                using var decoder = Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core.OpusDecoder.Create(
                    Constants.OUTPUT_SAMPLE_RATE, 1); // 48kHz mono
                
                // CRITICAL FIX: Match SimpleRawExport's FEC setting for consistent decoding
                decoder.ForwardErrorCorrection = false;

                // Allocate buffer for decoded PCM (max 120ms = 5760 samples @ 48kHz)
                var pcmBuffer = new short[Constants.OPUS_FRAME_SIZE * 6]; // Conservative size
                var samplesDecoded = decoder.DecodeShort(opusData, pcmBuffer, pcmBuffer.Length, false);

                if (samplesDecoded > 0)
                {
                    var result = new short[samplesDecoded];
                    Array.Copy(pcmBuffer, result, samplesDecoded);
                    //Logger.Debug($"Decoded Opus packet: {opusData.Length} bytes -> {samplesDecoded} samples");
                    return result;
                }

                Logger.Warn($"Opus decoder returned 0 samples for {opusData.Length} bytes");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to decode Opus packet (size: {opusData.Length} bytes), returning empty");
            }

            return Array.Empty<short>();
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

            if (pcmBytes.Length % 2 != 0)
            {
                Logger.Warn($"PCM byte array has odd length ({pcmBytes.Length}), truncating last byte");
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
        /// Calculates normalized amplitude (0.0 to 1.0) from PCM Int16 samples
        /// </summary>
        /// <param name="pcmSamples">PCM Int16 samples</param>
        /// <returns>Normalized amplitude in range [0.0, 1.0]</returns>
        public static float CalculateNormalizedAmplitude(short[] pcmSamples)
        {
            if (pcmSamples == null || pcmSamples.Length == 0)
                return 0f;

            double totalAmplitude = 0.0;
            foreach (var sample in pcmSamples)
            {
                // Cast to int before taking Abs to avoid Int16.MinValue negation overflow
                totalAmplitude += Math.Abs((int)sample);
            }

            return (float)(totalAmplitude / pcmSamples.Length / 32768.0);
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
                Logger.Info($"Exporting audio from {sourceFilePath} to {outputWavPath}");
                
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
                            // Decode audio automatically (handles both Opus and raw PCM)
                            var pcmSamples = DecodeAudioToPcm(packet.AudioPayload);
                            
                            if (pcmSamples.Length > 0)
                            {
                                // Convert PCM Int16 to float for accumulation
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
                    // Convert to PCM16 and write WAV file
                    var pcmData = AudioConverter.FloatToPcm16(audioData.ToArray());
                    
                    using var waveFileWriter = new WaveFileWriter(outputWavPath, 
                        new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 16, 1));
                    waveFileWriter.Write(pcmData, 0, pcmData.Length);
                    
                    Logger.Info($"Exported {audioData.Count} samples from {packetsProcessed} packets ({opusPacketsDecoded} Opus, {pcmPacketsProcessed} PCM) to {outputWavPath}");
                }
                else
                {
                    Logger.Warn("No audio data found to export");
                    throw new InvalidOperationException("No exportable audio data found in the recording file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to export audio to WAV: {outputWavPath}");
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
                            // Decode audio (handles both Opus and PCM)
                            var pcmSamples = DecodeAudioToPcm(packet.AudioPayload);
                            totalSamples += pcmSamples.Length;
                            packetsAnalyzed++;
                        }
                    }
                    else break;
                }
                
                if (totalSamples > 0)
                {
                    // WAV file size = header (44 bytes) + (samples * 2 bytes per sample for 16-bit)
                    const int wavHeaderSize = 44;
                    var estimatedSize = wavHeaderSize + (totalSamples * 2);
                    
                    Logger.Debug($"Estimated WAV export size: {estimatedSize} bytes from {totalSamples} samples in {packetsAnalyzed} packets");
                    return estimatedSize;
                }
                
                return -1; // No exportable data found
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to estimate WAV export size for: {sourceFilePath}");
                return -1;
            }
        }

        #endregion
    }
}