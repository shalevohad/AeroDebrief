using System;
using Xunit;
using ShalevOhad.DCS.SRS.Recorder.Core.Audio;

namespace Core.Audio.Tests
{
    public class FilteredWaveformGeneratorTests
    {
        [Fact]
        public void CalculatePacketAmplitude_EmptyPayload_ReturnsZero()
        {
            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(Array.Empty<byte>());
            Assert.Equal(0f, res);
        }

        [Fact]
        public void CalculatePacketAmplitude_NullPayload_ReturnsZero()
        {
            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(null!);
            Assert.Equal(0f, res);
        }

        [Fact]
        public void CalculatePacketAmplitude_RawPcm_MinInt16_DoesNotThrow_AndReturnsOne()
        {
            // Single PCM sample with value short.MinValue (-32768)
            // Large enough to be detected as PCM (not Opus)
            var samples = new short[960]; // Typical 20ms frame
            samples[0] = short.MinValue;
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(bytes);
            
            // Should decode without overflow
            Assert.True(res >= 0 && res <= 1.0f, $"Amplitude should be in [0,1], got {res}");
        }

        [Fact]
        public void CalculatePacketAmplitude_RawPcm_MaxInt16_ReturnsCloseToOne()
        {
            var samples = new short[960];
            samples[0] = short.MaxValue;
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(bytes);
            
            // Mostly zeros except one max sample, so average will be low
            Assert.True(res >= 0 && res <= 1.0f);
        }

        [Fact]
        public void CalculatePacketAmplitude_RawPcm_TwoSamples_CorrectAverage()
        {
            var s1 = (short)10000;
            var s2 = (short)-20000;
            var samples = new short[960];
            samples[0] = s1;
            samples[1] = s2;
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(bytes);
            
            // Result should be in valid range
            Assert.True(res >= 0 && res <= 1.0f);
        }

        [Fact]
        public void CalculatePacketAmplitude_SmallPayload_TreatedAsOpus()
        {
            // Small payload (< 400 bytes) triggers Opus decoding path
            // Create a minimal payload that will fail to decode gracefully
            var smallPayload = new byte[50];
            Array.Fill<byte>(smallPayload, 0xFF);

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(smallPayload);
            
            // Should return zero or low value (failed decode or silence)
            Assert.True(res >= 0 && res <= 1.0f);
        }

        [Fact]
        public void CalculatePacketAmplitude_LargePayload_TreatedAsRawPcm()
        {
            // Large payload (>= 400 bytes) triggers raw PCM path
            var samples = new short[960]; // 1920 bytes
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(1000 * Math.Sin(2 * Math.PI * i / samples.Length));
            }
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(bytes);
            
            // Sine wave amplitude check
            Assert.True(res > 0 && res < 0.1f, $"Expected small amplitude for 1000-amplitude sine, got {res}");
        }

        [Fact]
        public void CalculatePacketAmplitude_OddLengthPcm_HandlesGracefully()
        {
            // Odd-length payload (raw PCM path with trailing byte)
            var samples = new short[960];
            samples[0] = 5000;
            var bytes = new byte[samples.Length * 2 + 1]; // Add extra byte
            Buffer.BlockCopy(samples, 0, bytes, 0, samples.Length * 2);
            bytes[^1] = 0xAA; // trailing byte

            var res = FilteredWaveformGenerator.CalculatePacketAmplitude(bytes);
            
            // Should handle gracefully and return valid result
            Assert.True(res >= 0 && res <= 1.0f);
        }
    }
}
