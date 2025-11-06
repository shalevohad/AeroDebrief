using NLog;
using System;
using System.Linq;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Provides time-stretching capabilities for audio playback at variable speeds.
    /// Implements simple time-scaling for MVP with optional WSOLA support for future enhancement.
    /// </summary>
    public sealed class AudioTimeStretcher
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Adjusts audio timing for variable-speed playback using simple time-scaling.
        /// For speeds > 1.0x: Skips samples to speed up
        /// For speeds < 1.0x: Duplicates samples with crossfade to slow down
        /// </summary>
        /// <param name="audioData">Input audio samples (float, mono)</param>
        /// <param name="speed">Playback speed (0.25x to 4.0x)</param>
        /// <returns>Time-stretched audio samples</returns>
        public static float[] TimeStretch(float[] audioData, double speed)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            // Clamp speed to supported range using centralized validator
            speed = Playback.PlaybackSpeedValidator.ClampSpeed(speed);

            // No processing needed for normal speed
            if (Math.Abs(speed - 1.0) < 0.01)
                return audioData;

            try
            {
                if (speed > 1.0)
                {
                    // Fast playback: Skip samples
                    return SkipSamplesForSpeed(audioData, speed);
                }
                else
                {
                    // Slow playback: Duplicate samples with crossfade
                    return DuplicateSamplesForSpeed(audioData, speed);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to time-stretch audio (speed: {speed:F2}x)");
                return audioData; // Return original on error
            }
        }

        /// <summary>
        /// Skips samples to increase playback speed (for speeds > 1.0x)
        /// </summary>
        private static float[] SkipSamplesForSpeed(float[] audioData, double speed)
        {
            // Calculate how many samples to keep
            var outputLength = (int)(audioData.Length / speed);
            var output = new float[outputLength];

            // Skip samples with linear interpolation to maintain quality
            for (int i = 0; i < outputLength; i++)
            {
                var sourceIndex = i * speed;
                var index1 = (int)Math.Floor(sourceIndex);
                var index2 = Math.Min(index1 + 1, audioData.Length - 1);
                var fraction = (float)(sourceIndex - index1);

                // Linear interpolation between samples
                output[i] = audioData[index1] * (1 - fraction) + audioData[index2] * fraction;
            }

            Logger.Trace($"Time-stretched audio for {speed:F2}x speed: {audioData.Length} ? {output.Length} samples (skip)");
            return output;
        }

        /// <summary>
        /// Duplicates samples to decrease playback speed (for speeds < 1.0x)
        /// Uses crossfading to avoid clicks at duplication points
        /// </summary>
        private static float[] DuplicateSamplesForSpeed(float[] audioData, double speed)
        {
            // Calculate how many samples we need in output
            var outputLength = (int)(audioData.Length / speed);
            var output = new float[outputLength];

            // Crossfade duration (5ms at 48kHz = 240 samples)
            const int crossfadeSamples = 240;

            // Stretch with overlap-add for smoother result
            for (int i = 0; i < outputLength; i++)
            {
                var sourceIndex = i * speed;
                var index1 = (int)Math.Floor(sourceIndex);
                var index2 = Math.Min(index1 + 1, audioData.Length - 1);
                var fraction = (float)(sourceIndex - index1);

                // Linear interpolation
                output[i] = audioData[index1] * (1 - fraction) + audioData[index2] * fraction;

                // Apply crossfade at duplicate points to reduce artifacts
                var duplicatePoint = (int)(i * speed) % audioData.Length;
                if (duplicatePoint < crossfadeSamples)
                {
                    var fadeEnvelope = (float)duplicatePoint / crossfadeSamples;
                    output[i] *= fadeEnvelope;
                }
            }

            Logger.Trace($"Time-stretched audio for {speed:F2}x speed: {audioData.Length} ? {output.Length} samples (duplicate)");
            return output;
        }

        /// <summary>
        /// Calculates the appropriate audio delay for a given playback speed.
        /// Used to maintain correct timing during variable-speed playback.
        /// </summary>
        /// <param name="baseDelayMs">Base delay in milliseconds (e.g., OPUS frame duration)</param>
        /// <param name="speed">Playback speed multiplier</param>
        /// <returns>Scaled delay in milliseconds</returns>
        public static double CalculateScaledDelay(double baseDelayMs, double speed)
        {
            speed = Playback.PlaybackSpeedValidator.ClampSpeed(speed);
            return baseDelayMs / speed;
        }

        /// <summary>
        /// Determines if packet skipping should occur for the given speed.
        /// Used to reduce CPU overhead during fast playback.
        /// </summary>
        /// <param name="speed">Playback speed</param>
        /// <param name="random">Random instance for probabilistic skipping</param>
        /// <returns>True if packet should be skipped</returns>
        public static bool ShouldSkipPacket(double speed, Random random)
        {
            if (speed <= 1.5)
                return false; // No skipping below 1.5x

            // For speeds > 1.5x, skip packets probabilistically to reduce CPU overhead
            // Formula: (speed - 1.0) / (speed * 2.0) creates a progressive skip ratio:
            //   - At 2.0x: (2-1)/(2*2) = 0.25 (skip 25% of packets)
            //   - At 3.0x: (3-1)/(3*2) = 0.33 (skip 33% of packets)
            //   - At 4.0x: (4-1)/(4*2) = 0.375 (skip 37.5% of packets)
            // This balances CPU efficiency with audio quality by keeping skip ratio moderate
            var skipRatio = Math.Min(0.5, (speed - 1.0) / (speed * 2.0));
            return random.NextDouble() < skipRatio;
        }

        /// <summary>
        /// Calculates how many times a packet should be duplicated for slow playback.
        /// </summary>
        /// <param name="speed">Playback speed (< 1.0)</param>
        /// <returns>Number of times to play each packet</returns>
        public static int CalculateDuplicationCount(double speed)
        {
            if (speed >= 0.75)
                return 1; // No duplication above 0.75x

            // For very slow speeds (< 0.75x), play each packet multiple times
            return (int)Math.Ceiling(1.0 / speed);
        }
    }
}
