using System;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Helpers
{
    /// <summary>
    /// Helper methods for string manipulation and formatting
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Safely truncates a string to a specified length
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="addEllipsis">Whether to add "..." at the end if truncated</param>
        /// <returns>Truncated string</returns>
        public static string TruncateString(string input, int maxLength, bool addEllipsis = true)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            if (addEllipsis && maxLength > 3)
            {
                return input[..(maxLength - 3)] + "...";
            }
            
            return input[..maxLength];
        }

        /// <summary>
        /// Safely gets a substring of a GUID for display
        /// </summary>
        /// <param name="guid">GUID string</param>
        /// <param name="length">Length to display (default: 8)</param>
        /// <returns>Truncated GUID with ellipsis</returns>
        public static string GetDisplayGuid(string guid, int length = 8)
        {
            if (string.IsNullOrEmpty(guid))
                return "Unknown";
            
            return TruncateString(guid, length, true);
        }

        /// <summary>
        /// Gets a safe filename from a potentially unsafe string
        /// </summary>
        /// <param name="filename">Original filename</param>
        /// <returns>Safe filename with invalid characters replaced</returns>
        public static string GetSafeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "unnamed";

            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            var safeName = filename;
            
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }
            
            return safeName;
        }

        /// <summary>
        /// Gets file extension based on content type
        /// </summary>
        /// <param name="isRaw">Whether the file is raw audio data</param>
        /// <returns>Appropriate file extension</returns>
        public static string GetRecordingFileExtension(bool isRaw = true)
        {
            return isRaw ? ".raw" : ".wav";
        }
    }
}