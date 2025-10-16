using System;

namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Helper methods for time formatting and manipulation
    /// </summary>
    public static class TimeHelpers
    {
        /// <summary>
        /// Formats a TimeSpan into a user-friendly time string
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string (H:MM:SS or M:SS)</returns>
        public static string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
        }

        /// <summary>
        /// Formats a TimeSpan into a detailed time string with milliseconds
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string with milliseconds</returns>
        public static string FormatTimeDetailed(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
            }
            else
            {
                return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
            }
        }

        /// <summary>
        /// Formats duration for display purposes
        /// </summary>
        /// <param name="duration">Duration to format</param>
        /// <returns>Human-readable duration string</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}.{duration.Milliseconds:D3}s";
        }
    }
}