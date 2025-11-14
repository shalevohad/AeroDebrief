using System;

namespace AeroDebrief.Core.Playback
{
    /// <summary>
    /// Centralizes playback speed validation and clamping logic.
    /// Ensures consistent speed handling across PlaybackController and AudioTimeStretcher.
    /// </summary>
    public static class PlaybackSpeedValidator
    {
        /// <summary>
        /// Clamps the playback speed to the supported range.
        /// </summary>
        /// <param name="speed">Requested playback speed</param>
        /// <returns>Clamped speed within MIN_PLAYBACK_SPEED to MAX_PLAYBACK_SPEED range</returns>
        public static double ClampSpeed(double speed)
        {
            return Math.Clamp(speed, Constants.MIN_PLAYBACK_SPEED, Constants.MAX_PLAYBACK_SPEED);
        }

        /// <summary>
        /// Checks if the requested speed is within the supported range.
        /// </summary>
        /// <param name="speed">Requested playback speed</param>
        /// <returns>True if speed is within supported range, false otherwise</returns>
        public static bool IsSpeedInRange(double speed)
        {
            return speed >= Constants.MIN_PLAYBACK_SPEED && speed <= Constants.MAX_PLAYBACK_SPEED;
        }

        /// <summary>
        /// Checks if the requested speed has been clamped.
        /// </summary>
        /// <param name="requestedSpeed">Originally requested speed</param>
        /// <param name="clampedSpeed">Speed after clamping</param>
        /// <returns>True if speed was clamped, false otherwise</returns>
        public static bool IsSpeedClamped(double requestedSpeed, double clampedSpeed)
        {
            return Math.Abs(requestedSpeed - clampedSpeed) > 0.001;
        }
    }
}
