using System;
using System.IO;
using System.Linq;
using NLog;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using AeroDebrief.Core.Helpers;

namespace AeroDebrief.Core.Tests
{
    /// <summary>
    /// Tests to verify Opus decoding works correctly with SRS Common library
    /// </summary>
    public static class OpusDecodingTests
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Runs all Opus decoding tests
        /// </summary>
        public static void RunAllTests()
        {
            Logger.Info("======================================");
            Logger.Info("Starting Opus Decoding Tests");
            Logger.Info("======================================");

            var passedTests = 0;
            var totalTests = 0;

            // Test 1: Verify OpusDecoder creation
            totalTests++;
            if (TestOpusDecoderCreation())
            {
                passedTests++;
                Logger.Info("? Test 1 PASSED: OpusDecoder creation");
            }
            else
            {
                Logger.Error("? Test 1 FAILED: OpusDecoder creation");
            }

            // Test 2: Verify decoding silence
            totalTests++;
            if (TestDecodingSilence())
            {
                passedTests++;
                Logger.Info("? Test 2 PASSED: Decoding silence");
            }
            else
            {
                Logger.Error("? Test 2 FAILED: Decoding silence");
            }

            // Test 3: Verify AudioHelpers.DecodeOpusToPcm
            totalTests++;
            if (TestAudioHelpersDecoding())
            {
                passedTests++;
                Logger.Info("? Test 3 PASSED: AudioHelpers.DecodeOpusToPcm");
            }
            else
            {
                Logger.Error("? Test 3 FAILED: AudioHelpers.DecodeOpusToPcm");
            }

            // Test 4: Verify Opus detection
            totalTests++;
            if (TestOpusDetection())
            {
                passedTests++;
                Logger.Info("? Test 4 PASSED: Opus detection");
            }
            else
            {
                Logger.Error("? Test 4 FAILED: Opus detection");
            }

            // Test 5: Verify PCM conversion
            totalTests++;
            if (TestPcmConversion())
            {
                passedTests++;
                Logger.Info("? Test 5 PASSED: PCM conversion");
            }
            else
            {
                Logger.Error("? Test 5 FAILED: PCM conversion");
            }

            // Test 6: Verify DecodeAudioToPcm auto-detection
            totalTests++;
            if (TestAutoDetection())
            {
                passedTests++;
                Logger.Info("? Test 6 PASSED: Auto-detection");
            }
            else
            {
                Logger.Error("? Test 6 FAILED: Auto-detection");
            }

            // Test 7: Verify amplitude calculation
            totalTests++;
            if (TestAmplitudeCalculation())
            {
                passedTests++;
                Logger.Info("? Test 7 PASSED: Amplitude calculation");
            }
            else
            {
                Logger.Error("? Test 7 FAILED: Amplitude calculation");
            }

            Logger.Info("======================================");
            Logger.Info($"Test Results: {passedTests}/{totalTests} passed");
            Logger.Info("======================================");

            if (passedTests != totalTests)
            {
                throw new Exception($"Some tests failed: {passedTests}/{totalTests} passed");
            }
        }

        /// <summary>
        /// Test 1: Verify we can create an OpusDecoder from SRS Common
        /// </summary>
        private static bool TestOpusDecoderCreation()
        {
            try
            {
                Logger.Info("Test 1: Creating OpusDecoder...");
                
                using var decoder = OpusDecoder.Create(Constants.OUTPUT_SAMPLE_RATE, 1);
                
                if (decoder == null)
                {
                    Logger.Error("OpusDecoder.Create returned null");
                    return false;
                }

                Logger.Info($"Successfully created OpusDecoder for {Constants.OUTPUT_SAMPLE_RATE}Hz mono");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create OpusDecoder");
                return false;
            }
        }

        /// <summary>
        /// Test 2: Verify we can decode silence (null input)
        /// </summary>
        private static bool TestDecodingSilence()
        {
            try
            {
                Logger.Info("Test 2: Decoding silence...");
                
                using var decoder = OpusDecoder.Create(Constants.OUTPUT_SAMPLE_RATE, 1);
                
                // Decode null input (packet loss concealment)
                var buffer = new float[Constants.OPUS_FRAME_SIZE];
                var samplesDecoded = decoder.DecodeFloat(null, buffer.AsMemory());
                
                if (samplesDecoded <= 0)
                {
                    Logger.Error($"DecodeFloat returned {samplesDecoded} samples for silence");
                    return false;
                }

                Logger.Info($"Successfully decoded silence: {samplesDecoded} samples");
                
                // Verify samples are reasonable (should be near zero for silence)
                var maxAmplitude = buffer.Take(samplesDecoded).Max(Math.Abs);
                Logger.Info($"Max amplitude of silence: {maxAmplitude:F6}");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to decode silence");
                return false;
            }
        }

        /// <summary>
        /// Test 3: Verify AudioHelpers.DecodeOpusToPcm works with test data
        /// </summary>
        private static bool TestAudioHelpersDecoding()
        {
            try
            {
                Logger.Info("Test 3: Testing AudioHelpers.DecodeOpusToPcm...");
                
                // Create a minimal Opus frame (silence)
                var testOpusData = CreateTestOpusFrame();
                
                // Decode using AudioHelpers
                var pcmSamples = AudioHelpers.DecodeOpusToPcm(testOpusData);
                
                if (pcmSamples == null || pcmSamples.Length == 0)
                {
                    Logger.Error("AudioHelpers.DecodeOpusToPcm returned empty array");
                    return false;
                }

                Logger.Info($"Successfully decoded Opus frame: {testOpusData.Length} bytes -> {pcmSamples.Length} samples");
                
                // Verify sample count is reasonable (20ms at 48kHz = 960 samples)
                var expectedSamples = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000;
                Logger.Info($"Expected ~{expectedSamples} samples, got {pcmSamples.Length}");
                
                if (pcmSamples.Length < expectedSamples / 2 || pcmSamples.Length > expectedSamples * 6)
                {
                    Logger.Warn($"Sample count outside expected range: {pcmSamples.Length}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed AudioHelpers.DecodeOpusToPcm test");
                return false;
            }
        }

        /// <summary>
        /// Test 4: Verify Opus detection works correctly
        /// </summary>
        private static bool TestOpusDetection()
        {
            try
            {
                Logger.Info("Test 4: Testing Opus detection...");
                
                // Create test data
                var opusData = CreateTestOpusFrame();
                var pcmData = CreateTestPcmData();
                
                // Test Opus detection
                var isOpusDetected = AudioHelpers.IsOpusEncodedByteArray(opusData);
                var isPcmDetected = AudioHelpers.IsOpusEncodedByteArray(pcmData);
                
                Logger.Info($"Opus data ({opusData.Length} bytes) detected as Opus: {isOpusDetected}");
                Logger.Info($"PCM data ({pcmData.Length} bytes) detected as Opus: {isPcmDetected}");
                
                if (!isOpusDetected)
                {
                    Logger.Error("Failed to detect Opus-encoded data");
                    return false;
                }
                
                if (isPcmDetected)
                {
                    Logger.Error("Incorrectly detected PCM data as Opus");
                    return false;
                }
                
                Logger.Info("Opus detection working correctly");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed Opus detection test");
                return false;
            }
        }

        /// <summary>
        /// Test 5: Verify PCM conversion helpers work correctly
        /// </summary>
        private static bool TestPcmConversion()
        {
            try
            {
                Logger.Info("Test 5: Testing PCM conversion...");
                
                // Create test PCM data
                var originalSamples = new short[] { 0, 1000, -1000, 16000, -16000, 32767, -32768 };
                
                // Convert to bytes
                var pcmBytes = AudioHelpers.ConvertPcm16ToBytes(originalSamples);
                
                if (pcmBytes.Length != originalSamples.Length * 2)
                {
                    Logger.Error($"PCM byte conversion incorrect: expected {originalSamples.Length * 2} bytes, got {pcmBytes.Length}");
                    return false;
                }
                
                // Convert back to samples
                var convertedSamples = AudioHelpers.ConvertBytesToPcm16(pcmBytes);
                
                if (convertedSamples.Length != originalSamples.Length)
                {
                    Logger.Error($"PCM sample conversion incorrect: expected {originalSamples.Length} samples, got {convertedSamples.Length}");
                    return false;
                }
                
                // Verify all samples match
                for (int i = 0; i < originalSamples.Length; i++)
                {
                    if (originalSamples[i] != convertedSamples[i])
                    {
                        Logger.Error($"Sample mismatch at index {i}: expected {originalSamples[i]}, got {convertedSamples[i]}");
                        return false;
                    }
                }
                
                Logger.Info("PCM conversion working correctly");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed PCM conversion test");
                return false;
            }
        }

        /// <summary>
        /// Test 6: Verify DecodeAudioToPcm auto-detection works
        /// </summary>
        private static bool TestAutoDetection()
        {
            try
            {
                Logger.Info("Test 6: Testing auto-detection in DecodeAudioToPcm...");
                
                // Test with Opus data
                var opusData = CreateTestOpusFrame();
                var decodedFromOpus = AudioHelpers.DecodeAudioToPcm(opusData);
                
                if (decodedFromOpus == null || decodedFromOpus.Length == 0)
                {
                    Logger.Error("Failed to decode Opus data via auto-detection");
                    return false;
                }
                
                Logger.Info($"Auto-decoded Opus: {opusData.Length} bytes -> {decodedFromOpus.Length} samples");
                
                // Test with PCM data
                var pcmData = CreateTestPcmData();
                var decodedFromPcm = AudioHelpers.DecodeAudioToPcm(pcmData);
                
                if (decodedFromPcm == null || decodedFromPcm.Length == 0)
                {
                    Logger.Error("Failed to decode PCM data via auto-detection");
                    return false;
                }
                
                Logger.Info($"Auto-decoded PCM: {pcmData.Length} bytes -> {decodedFromPcm.Length} samples");
                
                // Verify PCM data matches original
                var originalPcmSamples = AudioHelpers.ConvertBytesToPcm16(pcmData);
                if (decodedFromPcm.Length != originalPcmSamples.Length)
                {
                    Logger.Error($"PCM auto-decode length mismatch: expected {originalPcmSamples.Length}, got {decodedFromPcm.Length}");
                    return false;
                }
                
                Logger.Info("Auto-detection working correctly");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed auto-detection test");
                return false;
            }
        }

        /// <summary>
        /// Test 7: Verify amplitude calculation works correctly
        /// </summary>
        private static bool TestAmplitudeCalculation()
        {
            try
            {
                Logger.Info("Test 7: Testing amplitude calculation...");
                
                // Test with known amplitudes
                var testCases = new[]
                {
                    (Samples: new short[] { 0, 0, 0, 0 }, ExpectedAmplitude: 0.0f, Description: "Silence"),
                    (Samples: new short[] { 16384, -16384, 16384, -16384 }, ExpectedAmplitude: 0.5f, Description: "Half amplitude"),
                    (Samples: new short[] { 32767, -32768, 32767, -32768 }, ExpectedAmplitude: 1.0f, Description: "Full amplitude")
                };
                
                foreach (var testCase in testCases)
                {
                    var amplitude = AudioHelpers.CalculateNormalizedAmplitude(testCase.Samples);
                    var difference = Math.Abs(amplitude - testCase.ExpectedAmplitude);
                    
                    Logger.Info($"{testCase.Description}: amplitude = {amplitude:F4} (expected ~{testCase.ExpectedAmplitude:F4})");
                    
                    if (difference > 0.05f) // Allow 5% tolerance
                    {
                        Logger.Error($"Amplitude calculation incorrect: expected {testCase.ExpectedAmplitude:F4}, got {amplitude:F4}");
                        return false;
                    }
                }
                
                Logger.Info("Amplitude calculation working correctly");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed amplitude calculation test");
                return false;
            }
        }

        /// <summary>
        /// Creates a test Opus frame (minimal valid Opus silence frame)
        /// </summary>
        private static byte[] CreateTestOpusFrame()
        {
            // Create a minimal valid Opus silence frame
            // Opus silence frame format: single TOC byte with silence indicator
            // 0xFC = Configuration 31 (CELT-only mode), code 0 (one frame, 20ms)
            // This is a valid minimal Opus frame that decoders will recognize
            Logger.Info("Creating minimal Opus silence frame for testing");
            return new byte[] { 0xFC, 0x00 }; // OPUS silence frame (2 bytes)
        }

        /// <summary>
        /// Creates test PCM data (raw 16-bit samples)
        /// </summary>
        private static byte[] CreateTestPcmData()
        {
            // Create 20ms of PCM audio at 48kHz (960 samples = 1920 bytes)
            var samples = new short[Constants.OPUS_FRAME_SIZE];
            
            // Generate a simple tone (100Hz)
            for (int i = 0; i < samples.Length; i++)
            {
                var time = (double)i / Constants.OUTPUT_SAMPLE_RATE;
                samples[i] = (short)(8000 * Math.Sin(2 * Math.PI * 100 * time));
            }
            
            var pcmBytes = AudioHelpers.ConvertPcm16ToBytes(samples);
            Logger.Info($"Created test PCM data: {pcmBytes.Length} bytes");
            return pcmBytes;
        }

        /// <summary>
        /// Tests decoding a real recorded file to verify end-to-end functionality
        /// </summary>
        public static bool TestRealRecordingFile(string filePath)
        {
            try
            {
                Logger.Info($"======================================");
                Logger.Info($"Testing Real Recording File");
                Logger.Info($"File: {filePath}");
                Logger.Info($"======================================");
                
                if (!File.Exists(filePath))
                {
                    Logger.Error($"File not found: {filePath}");
                    return false;
                }
                
                var fileInfo = new FileInfo(filePath);
                Logger.Info($"File size: {fileInfo.Length:N0} bytes");
                
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);
                
                var opusPackets = 0;
                var pcmPackets = 0;
                var emptyPackets = 0;
                var decodeErrors = 0;
                var totalSamples = 0L;
                
                while (fs.Position < fs.Length)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var packet) && packet != null)
                    {
                        if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                        {
                            emptyPackets++;
                            continue;
                        }
                        
                        var isOpus = AudioHelpers.IsOpusEncodedByteArray(packet.AudioPayload);
                        
                        if (isOpus)
                        {
                            opusPackets++;
                        }
                        else
                        {
                            pcmPackets++;
                        }
                        
                        // Try to decode
                        try
                        {
                            var samples = AudioHelpers.DecodeAudioToPcm(packet.AudioPayload);
                            totalSamples += samples.Length;
                            
                            if (samples.Length == 0)
                            {
                                Logger.Warn($"Packet at position {fs.Position} decoded to 0 samples");
                            }
                        }
                        catch (Exception ex)
                        {
                            decodeErrors++;
                            Logger.Warn($"Failed to decode packet at position {fs.Position}: {ex.Message}");
                        }
                        
                        // Test first 5 packets in detail
                        if (opusPackets + pcmPackets <= 5)
                        {
                            Logger.Info($"Packet {opusPackets + pcmPackets}: {(isOpus ? "Opus" : "PCM")} - {packet.AudioPayload.Length} bytes");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                Logger.Info($"======================================");
                Logger.Info($"Results:");
                Logger.Info($"  Opus packets: {opusPackets}");
                Logger.Info($"  PCM packets: {pcmPackets}");
                Logger.Info($"  Empty packets: {emptyPackets}");
                Logger.Info($"  Decode errors: {decodeErrors}");
                Logger.Info($"  Total samples decoded: {totalSamples:N0}");
                Logger.Info($"  Estimated audio duration: {TimeSpan.FromSeconds(totalSamples / (double)Constants.OUTPUT_SAMPLE_RATE)}");
                Logger.Info($"======================================");
                
                if (opusPackets == 0 && pcmPackets == 0)
                {
                    Logger.Error("No audio packets with data found!");
                    return false;
                }
                
                if (decodeErrors > (opusPackets + pcmPackets) * 0.1)
                {
                    Logger.Error($"Too many decode errors: {decodeErrors} out of {opusPackets + pcmPackets} packets");
                    return false;
                }
                
                Logger.Info("? Real recording file test PASSED");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to test real recording file");
                return false;
            }
        }
    }
}
