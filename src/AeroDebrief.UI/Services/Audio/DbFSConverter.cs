using System;

namespace AeroDebrief.UI.Services.Audio
{
    /// <summary>
    /// Utility class for converting audio amplitudes to dBFS (decibels relative to full scale).
    /// dBFS is a logarithmic scale commonly used for digital audio levels.
    /// Range: -? to 0 dBFS (0 = maximum amplitude, negative = quieter)
    /// </summary>
    public static class DbFSConverter
    {
        /// <summary>
        /// Minimum dBFS value to prevent log(0) issues.
        /// -120 dBFS is effectively silence (below noise floor for 16-bit audio).
        /// </summary>
        public const double MinDbFS = -120.0;
        
        /// <summary>
        /// Maximum dBFS value (full scale).
        /// </summary>
        public const double MaxDbFS = 0.0;
        
        /// <summary>
        /// Minimum amplitude ratio to avoid log(0).
        /// Corresponds to -120 dBFS.
        /// </summary>
        private const double MinRatio = 1e-6;
        
        /// <summary>
        /// Convert RMS amplitude to dBFS.
        /// </summary>
        /// <param name="rms">RMS amplitude (0.0 to 1.0)</param>
        /// <param name="bitDepth">Bit depth of audio (default 16)</param>
        /// <returns>Amplitude in dBFS (-120 to 0)</returns>
        public static double RmsToDbFS(double rms, int bitDepth = 16)
        {
            if (rms <= 0 || double.IsNaN(rms) || double.IsInfinity(rms))
                return MinDbFS;
            
            // For float samples normalized to -1.0 to 1.0, maximum RMS is 1.0
            // Calculate ratio relative to full scale
            var ratio = Math.Abs(rms);
            
            // Clamp to prevent log(0)
            if (ratio < MinRatio)
                return MinDbFS;
            
            // Convert to dBFS: 20 * log10(ratio)
            var dbFS = 20.0 * Math.Log10(ratio);
            
            // Clamp to valid range
            return Math.Clamp(dbFS, MinDbFS, MaxDbFS);
        }
        
        /// <summary>
        /// Convert linear amplitude (0.0 to 1.0) to dBFS.
        /// For peak amplitude measurements.
        /// </summary>
        /// <param name="linear">Linear amplitude (0.0 to 1.0)</param>
        /// <returns>Amplitude in dBFS (-120 to 0)</returns>
        public static double LinearToDbFS(double linear)
        {
            if (linear <= 0 || double.IsNaN(linear) || double.IsInfinity(linear))
                return MinDbFS;
            
            var ratio = Math.Abs(linear);
            
            if (ratio < MinRatio)
                return MinDbFS;
            
            var dbFS = 20.0 * Math.Log10(ratio);
            return Math.Clamp(dbFS, MinDbFS, MaxDbFS);
        }
        
        /// <summary>
        /// Convert dBFS to linear amplitude (0.0 to 1.0).
        /// </summary>
        /// <param name="dbFS">Amplitude in dBFS</param>
        /// <returns>Linear amplitude (0.0 to 1.0)</returns>
        public static double DbFSToLinear(double dbFS)
        {
            if (dbFS <= MinDbFS)
                return 0.0;
            
            if (dbFS >= MaxDbFS)
                return 1.0;
            
            // Convert from dB: 10^(dB/20)
            return Math.Pow(10.0, dbFS / 20.0);
        }
        
        /// <summary>
        /// Convert PCM sample value to dBFS.
        /// For 16-bit audio, maximum value is ±32768.
        /// </summary>
        /// <param name="pcmValue">PCM sample value</param>
        /// <param name="bitDepth">Bit depth of audio (default 16)</param>
        /// <returns>Amplitude in dBFS</returns>
        public static double PcmToDbFS(short pcmValue, int bitDepth = 16)
        {
            if (pcmValue == 0)
                return MinDbFS;
            
            var maxAmplitude = Math.Pow(2, bitDepth - 1);
            var ratio = Math.Abs(pcmValue) / maxAmplitude;
            
            if (ratio < MinRatio)
                return MinDbFS;
            
            var dbFS = 20.0 * Math.Log10(ratio);
            return Math.Clamp(dbFS, MinDbFS, MaxDbFS);
        }
        
        /// <summary>
        /// Check if a dBFS value is within valid range.
        /// </summary>
        /// <param name="dbFS">dBFS value to check</param>
        /// <returns>True if valid</returns>
        public static bool IsValidDbFS(double dbFS)
        {
            return !double.IsNaN(dbFS) && 
                   !double.IsInfinity(dbFS) && 
                   dbFS >= MinDbFS && 
                   dbFS <= MaxDbFS;
        }
        
        /// <summary>
        /// Clamp dBFS to valid range.
        /// </summary>
        /// <param name="dbFS">dBFS value to clamp</param>
        /// <returns>Clamped dBFS value</returns>
        public static double ClampDbFS(double dbFS)
        {
            if (double.IsNaN(dbFS) || double.IsInfinity(dbFS) || dbFS < MinDbFS)
                return MinDbFS;
            
            if (dbFS > MaxDbFS)
                return MaxDbFS;
            
            return dbFS;
        }
        
        /// <summary>
        /// Format dBFS value as string with appropriate precision.
        /// </summary>
        /// <param name="dbFS">dBFS value</param>
        /// <returns>Formatted string (e.g., "-12.3 dBFS")</returns>
        public static string FormatDbFS(double dbFS)
        {
            if (dbFS <= MinDbFS)
                return $"{MinDbFS:F0} dBFS (silence)";
            
            return $"{dbFS:F1} dBFS";
        }
    }
}
