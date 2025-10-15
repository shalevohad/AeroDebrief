using System;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Helpers
{
    /// <summary>
    /// Helper methods for validation operations
    /// </summary>
    public static class ValidationHelpers
    {
        /// <summary>
        /// Validates an IP address string
        /// </summary>
        /// <param name="ipAddress">IP address string</param>
        /// <returns>True if valid IP address</returns>
        public static bool IsValidIpAddress(string ipAddress)
        {
            return System.Net.IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Validates a port number
        /// </summary>
        /// <param name="port">Port number</param>
        /// <returns>True if valid port (1-65535)</returns>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
        }

        /// <summary>
        /// Validates a frequency value
        /// </summary>
        /// <param name="frequency">Frequency in Hz</param>
        /// <returns>True if valid frequency (> 0)</returns>
        public static bool IsValidFrequency(double frequency)
        {
            return frequency > 0;
        }

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is lower than version2</returns>
        public static bool IsVersionLower(string version1, string version2)
        {
            if (Version.TryParse(version1, out var ver1) && Version.TryParse(version2, out var ver2))
            {
                return ver1 < ver2;
            }
            return false; // If parsing fails, assume not lower
        }

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is greater than version2</returns>
        public static bool IsVersionGreater(string version1, string version2)
        {
            if (Version.TryParse(version1, out var ver1) && Version.TryParse(version2, out var ver2))
            {
                return ver1 > ver2;
            }
            return false; // If parsing fails, assume not greater
        }
    }
}