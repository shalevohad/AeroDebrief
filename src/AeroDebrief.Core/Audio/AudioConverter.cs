using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>Audio conversion utilities</summary>
    public static class AudioConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static byte[] FloatToPcm16(float[] floatData)
        {
            if (floatData == null || floatData.Length == 0)
            {
                Logger.Trace("FloatToPcm16: Empty input data");
                return Array.Empty<byte>();
            }

            var pcmData = new byte[floatData.Length * 2];
            var maxInput = 0f;
            var maxOutput = 0;
            var nonZeroSamples = 0;

            for (int i = 0; i < floatData.Length; i++)
            {
                // Clamp the input to valid range
                var sample = Math.Clamp(floatData[i], -1.0f, 1.0f);
                maxInput = Math.Max(maxInput, Math.Abs(sample));
                
                // Convert to 16-bit PCM with proper scaling
                short pcmSample = (short)(sample * 32767f);
                maxOutput = Math.Max(maxOutput, Math.Abs(pcmSample));
                
                if (Math.Abs(pcmSample) > 100) // Count non-trivial samples
                    nonZeroSamples++;

                // Write as little-endian bytes
                BitConverter.TryWriteBytes(pcmData.AsSpan(i * 2), pcmSample);
            }

            // Log conversion statistics for debugging
            Logger.Debug($"FloatToPcm16: {floatData.Length} samples converted. " +
                        $"Max input: {maxInput:F4}, Max output: {maxOutput}/32767, " +
                        $"Active samples: {nonZeroSamples}/{floatData.Length}");

            // Warn about potential issues
            if (maxInput == 0)
            {
                Logger.Warn("FloatToPcm16: All input samples are zero - no audio will be heard");
            }
            else if (maxInput < 0.01f)
            {
                Logger.Warn($"FloatToPcm16: Input amplitude very low ({maxInput:F4}) - audio may be too quiet");
            }
            else if (maxInput > 0.95f)
            {
                Logger.Info($"FloatToPcm16: High input amplitude ({maxInput:F4}) - good signal level");
            }

            return pcmData;
        }

        /// <summary>
        /// Convert PCM16 bytes back to float for analysis/debugging
        /// </summary>
        public static float[] Pcm16ToFloat(byte[] pcmData)
        {
            if (pcmData == null || pcmData.Length == 0 || pcmData.Length % 2 != 0)
            {
                return Array.Empty<float>();
            }

            var floatData = new float[pcmData.Length / 2];
            for (int i = 0; i < floatData.Length; i++)
            {
                short pcmSample = BitConverter.ToInt16(pcmData, i * 2);
                floatData[i] = pcmSample / 32768.0f; // Convert to -1.0 to 1.0 range
            }
            return floatData;
        }

        /// <summary>
        /// Amplify audio data by a given factor
        /// </summary>
        public static float[] AmplifyAudio(float[] audioData, float amplificationFactor)
        {
            if (audioData == null || audioData.Length == 0 || amplificationFactor <= 0)
                return audioData;

            var amplified = new float[audioData.Length];
            for (int i = 0; i < audioData.Length; i++)
            {
                amplified[i] = Math.Clamp(audioData[i] * amplificationFactor, -1.0f, 1.0f);
            }

            Logger.Debug($"Amplified audio by factor {amplificationFactor:F2}");
            return amplified;
        }
    }
}