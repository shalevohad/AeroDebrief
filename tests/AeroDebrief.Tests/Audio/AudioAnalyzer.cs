using System;
using System.Linq;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Audio analysis utilities for quality testing
    /// Provides peak detection, RMS calculation, click/pop detection, and basic FFT analysis
    /// </summary>
    public static class AudioAnalyzer
    {
        /// <summary>
        /// Calculates peak amplitude in audio data
        /// </summary>
        /// <param name="audioData">Audio samples (-1.0 to 1.0)</param>
        /// <returns>Peak amplitude (0.0 to 1.0)</returns>
        public static float CalculatePeakAmplitude(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return 0f;

            return audioData.Max(Math.Abs);
        }

        /// <summary>
        /// Calculates RMS (Root Mean Square) amplitude
        /// </summary>
        /// <param name="audioData">Audio samples (-1.0 to 1.0)</param>
        /// <returns>RMS amplitude</returns>
        public static float CalculateRMS(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return 0f;

            var sumSquares = audioData.Sum(sample => sample * sample);
            return (float)Math.Sqrt(sumSquares / audioData.Length);
        }

        /// <summary>
        /// Detects clicks/pops by finding sudden amplitude changes
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="threshold">Threshold for click detection (default: 0.5 = 50% jump)</param>
        /// <returns>True if clicks/pops detected</returns>
        public static bool HasClicksOrPops(float[] audioData, float threshold = 0.5f)
        {
            if (audioData == null || audioData.Length < 2)
                return false;

            for (int i = 1; i < audioData.Length; i++)
            {
                var delta = Math.Abs(audioData[i] - audioData[i - 1]);
                if (delta > threshold)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Counts the number of clicks/pops in audio data
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="threshold">Threshold for click detection</param>
        /// <returns>Number of clicks detected</returns>
        public static int CountClicksAndPops(float[] audioData, float threshold = 0.5f)
        {
            if (audioData == null || audioData.Length < 2)
                return 0;

            int count = 0;
            for (int i = 1; i < audioData.Length; i++)
            {
                var delta = Math.Abs(audioData[i] - audioData[i - 1]);
                if (delta > threshold)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Verifies audio is within valid range (-1.0 to 1.0)
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <returns>True if all samples are within range</returns>
        public static bool IsWithinValidRange(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return true;

            return audioData.All(sample => sample >= -1.0f && sample <= 1.0f);
        }

        /// <summary>
        /// Detects clipping (samples at or beyond ±1.0)
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="threshold">Clipping threshold (default: 0.99 to account for rounding)</param>
        /// <returns>True if clipping detected</returns>
        public static bool HasClipping(float[] audioData, float threshold = 0.99f)
        {
            if (audioData == null || audioData.Length == 0)
                return false;

            return audioData.Any(sample => Math.Abs(sample) >= threshold);
        }

        /// <summary>
        /// Counts the number of clipped samples
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="threshold">Clipping threshold</param>
        /// <returns>Number of clipped samples</returns>
        public static int CountClippedSamples(float[] audioData, float threshold = 0.99f)
        {
            if (audioData == null || audioData.Length == 0)
                return 0;

            return audioData.Count(sample => Math.Abs(sample) >= threshold);
        }

        /// <summary>
        /// Verifies fade envelope is approximately linear
        /// </summary>
        /// <param name="audioData">Audio samples during fade</param>
        /// <param name="fadeSamples">Number of samples in fade</param>
        /// <param name="isFadeIn">True for fade-in, false for fade-out</param>
        /// <param name="tolerance">Allowed deviation from linear (0.0-1.0)</param>
        /// <returns>True if fade is approximately linear</returns>
        public static bool IsFadeLinear(float[] audioData, int fadeSamples, bool isFadeIn, float tolerance = 0.1f)
        {
            if (audioData == null || audioData.Length < fadeSamples)
                return false;

            // Extract fade region
            var fadeRegion = isFadeIn 
                ? audioData.Take(fadeSamples).ToArray()
                : audioData.Skip(audioData.Length - fadeSamples).ToArray();

            // Calculate expected amplitude at each point
            for (int i = 0; i < fadeSamples - 1; i++)
            {
                var expectedGain = isFadeIn 
                    ? (float)i / fadeSamples 
                    : 1.0f - ((float)i / fadeSamples);

                var actualAmplitude = Math.Abs(fadeRegion[i]);
                var expectedAmplitude = expectedGain * Math.Abs(fadeRegion[fadeSamples - 1]);

                // Allow some tolerance for floating point and audio data variations
                if (Math.Abs(actualAmplitude - expectedAmplitude) > tolerance)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Detects if audio is mostly silence
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="threshold">Silence threshold (default: 0.01 = 1%)</param>
        /// <returns>True if audio is mostly silence</returns>
        public static bool IsSilent(float[] audioData, float threshold = 0.01f)
        {
            if (audioData == null || audioData.Length == 0)
                return true;

            return audioData.All(sample => Math.Abs(sample) < threshold);
        }

        /// <summary>
        /// Calculates signal-to-noise ratio (SNR)
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="noiseSamples">Known noise samples for baseline</param>
        /// <returns>SNR in decibels</returns>
        public static double CalculateSNR(float[] audioData, float[] noiseSamples)
        {
            if (audioData == null || noiseSamples == null)
                return 0.0;

            var signalRMS = CalculateRMS(audioData);
            var noiseRMS = CalculateRMS(noiseSamples);

            if (noiseRMS == 0)
                return double.PositiveInfinity;

            return 20.0 * Math.Log10(signalRMS / noiseRMS);
        }

        /// <summary>
        /// Calculates crest factor (peak to RMS ratio)
        /// Useful for detecting compression artifacts
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <returns>Crest factor</returns>
        public static float CalculateCrestFactor(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return 0f;

            var peak = CalculatePeakAmplitude(audioData);
            var rms = CalculateRMS(audioData);

            if (rms == 0)
                return 0f;

            return peak / rms;
        }

        /// <summary>
        /// Verifies amplitude envelope is continuous (no sudden jumps)
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="maxDelta">Maximum allowed change between samples</param>
        /// <returns>True if envelope is continuous</returns>
        public static bool HasContinuousEnvelope(float[] audioData, float maxDelta = 0.3f)
        {
            if (audioData == null || audioData.Length < 2)
                return true;

            for (int i = 1; i < audioData.Length; i++)
            {
                var delta = Math.Abs(audioData[i] - audioData[i - 1]);
                if (delta > maxDelta)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Extracts amplitude envelope from audio signal
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="windowSize">Window size for envelope detection</param>
        /// <returns>Amplitude envelope</returns>
        public static float[] ExtractEnvelope(float[] audioData, int windowSize = 64)
        {
            if (audioData == null || audioData.Length == 0)
                return Array.Empty<float>();

            var envelope = new float[audioData.Length];

            for (int i = 0; i < audioData.Length; i++)
            {
                var start = Math.Max(0, i - windowSize / 2);
                var end = Math.Min(audioData.Length, i + windowSize / 2);

                float maxAmp = 0f;
                for (int j = start; j < end; j++)
                {
                    maxAmp = Math.Max(maxAmp, Math.Abs(audioData[j]));
                }

                envelope[i] = maxAmp;
            }

            return envelope;
        }
    }
}
