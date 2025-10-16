using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Settings;

namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Unified helper class providing convenient access to all specialized helper methods.
    /// This class serves as a centralized entry point for common operations across the application.
    /// </summary>
    public static class Helpers
    {
        #region Time Operations

        /// <summary>
        /// Formats a TimeSpan into a user-friendly time string (H:MM:SS or M:SS)
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string</returns>
        public static string FormatTime(TimeSpan timeSpan) => TimeHelpers.FormatTime(timeSpan);

        /// <summary>
        /// Formats a TimeSpan into a detailed time string with milliseconds
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string with milliseconds</returns>
        public static string FormatTimeDetailed(TimeSpan timeSpan) => TimeHelpers.FormatTimeDetailed(timeSpan);

        /// <summary>
        /// Formats duration for display purposes (e.g., "1h 23m 45s")
        /// </summary>
        /// <param name="duration">Duration to format</param>
        /// <returns>Human-readable duration string</returns>
        public static string FormatDuration(TimeSpan duration) => TimeHelpers.FormatDuration(duration);

        #endregion

        #region String Operations

        /// <summary>
        /// Safely truncates a string to a specified length
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="addEllipsis">Whether to add "..." at the end if truncated</param>
        /// <returns>Truncated string</returns>
        public static string TruncateString(string input, int maxLength, bool addEllipsis = true) => 
            StringHelpers.TruncateString(input, maxLength, addEllipsis);

        /// <summary>
        /// Safely gets a substring of a GUID for display
        /// </summary>
        /// <param name="guid">GUID string</param>
        /// <param name="length">Length to display (default: 8)</param>
        /// <returns>Truncated GUID with ellipsis</returns>
        public static string GetDisplayGuid(string guid, int length = 8) => 
            StringHelpers.GetDisplayGuid(guid, length);

        /// <summary>
        /// Gets a safe filename from a potentially unsafe string
        /// </summary>
        /// <param name="filename">Original filename</param>
        /// <returns>Safe filename with invalid characters replaced</returns>
        public static string GetSafeFilename(string filename) => StringHelpers.GetSafeFilename(filename);

        /// <summary>
        /// Gets file extension based on content type
        /// </summary>
        /// <param name="isRaw">Whether the file is raw audio data</param>
        /// <returns>Appropriate file extension</returns>
        public static string GetRecordingFileExtension(bool isRaw = true) => 
            StringHelpers.GetRecordingFileExtension(isRaw);

        #endregion

        #region Validation Operations

        /// <summary>
        /// Validates an IP address string
        /// </summary>
        /// <param name="ipAddress">IP address string</param>
        /// <returns>True if valid IP address</returns>
        public static bool IsValidIpAddress(string ipAddress) => ValidationHelpers.IsValidIpAddress(ipAddress);

        /// <summary>
        /// Validates a port number
        /// </summary>
        /// <param name="port">Port number</param>
        /// <returns>True if valid port (1-65535)</returns>
        public static bool IsValidPort(int port) => ValidationHelpers.IsValidPort(port);

        /// <summary>
        /// Validates a frequency value
        /// </summary>
        /// <param name="frequency">Frequency in Hz</param>
        /// <returns>True if valid frequency (> 0)</returns>
        public static bool IsValidFrequency(double frequency) => ValidationHelpers.IsValidFrequency(frequency);

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is lower than version2</returns>
        public static bool IsVersionLower(string version1, string version2) => 
            ValidationHelpers.IsVersionLower(version1, version2);

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is greater than version2</returns>
        public static bool IsVersionGreater(string version1, string version2) => 
            ValidationHelpers.IsVersionGreater(version1, version2);

        #endregion

        #region DCS Operations

        /// <summary>
        /// Gets the coalition name from an integer value
        /// </summary>
        /// <param name="coalition">Coalition integer value</param>
        /// <returns>Coalition name string</returns>
        public static string GetCoalitionName(int coalition) => DcsHelpers.GetCoalitionName(coalition);

        /// <summary>
        /// Converts integer coalition value to Coalition enum safely
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>Coalition enum value</returns>
        public static DcsHelpers.Coalition ToCoalition(int coalitionValue) => 
            DcsHelpers.ToCoalition(coalitionValue);

        /// <summary>
        /// Gets UI color for coalition
        /// </summary>
        /// <param name="coalition">Coalition enum value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static Color GetCoalitionColor(DcsHelpers.Coalition coalition) => 
            DcsHelpers.GetCoalitionColor(coalition);

        /// <summary>
        /// Gets UI color for coalition from integer value
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static Color GetCoalitionColor(int coalitionValue) => 
            DcsHelpers.GetCoalitionColor(coalitionValue);

        /// <summary>
        /// Gets the modulation name from a byte value
        /// </summary>
        /// <param name="modulation">Modulation byte value</param>
        /// <returns>Modulation name string</returns>
        public static string GetModulationName(byte modulation) => DcsHelpers.GetModulationName(modulation);

        /// <summary>
        /// Determines appropriate radio model based on packet modulation
        /// </summary>
        /// <param name="modulation">Modulation type</param>
        /// <returns>Radio model name for SRS effects pipeline</returns>
        public static string DetermineRadioModel(byte modulation) => DcsHelpers.DetermineRadioModel(modulation);

        /// <summary>
        /// Converts frequency from Hz to MHz
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <returns>Frequency in MHz</returns>
        public static double HzToMHz(double frequencyHz) => DcsHelpers.HzToMHz(frequencyHz);

        /// <summary>
        /// Converts frequency from MHz to Hz
        /// </summary>
        /// <param name="frequencyMHz">Frequency in MHz</param>
        /// <returns>Frequency in Hz</returns>
        public static double MHzToHz(double frequencyMHz) => DcsHelpers.MHzToHz(frequencyMHz);

        /// <summary>
        /// Formats frequency for display
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <param name="decimalPlaces">Number of decimal places (default: 3)</param>
        /// <returns>Formatted frequency string with MHz unit</returns>
        public static string FormatFrequency(double frequencyHz, int decimalPlaces = 3) => 
            DcsHelpers.FormatFrequency(frequencyHz, decimalPlaces);

        #endregion

        #region Audio Operations

        /// <summary>
        /// Converts audio sample count to duration
        /// </summary>
        /// <param name="sampleCount">Number of samples</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Duration as TimeSpan</returns>
        public static TimeSpan SamplesToDuration(int sampleCount, int sampleRate) => 
            AudioHelpers.SamplesToDuration(sampleCount, sampleRate);

        /// <summary>
        /// Converts duration to sample count
        /// </summary>
        /// <param name="duration">Duration as TimeSpan</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Number of samples</returns>
        public static int DurationToSamples(TimeSpan duration, int sampleRate) => 
            AudioHelpers.DurationToSamples(duration, sampleRate);

        /// <summary>
        /// Formats audio size in human-readable format
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        public static string FormatAudioSize(long bytes) => AudioHelpers.FormatAudioSize(bytes);

        /// <summary>
        /// Converts 16-bit PCM audio data to float array
        /// </summary>
        /// <param name="pcmData">16-bit PCM audio data</param>
        /// <returns>Float array with values in -1.0 to 1.0 range</returns>
        public static float[] ConvertPcm16ToFloat(byte[] pcmData) => AudioHelpers.ConvertPcm16ToFloat(pcmData);

        /// <summary>
        /// Resamples audio from one sample rate to another using linear interpolation
        /// </summary>
        /// <param name="inputAudio">Input audio data</param>
        /// <param name="inputSampleRate">Input sample rate</param>
        /// <param name="outputSampleRate">Output sample rate</param>
        /// <returns>Resampled audio data</returns>
        public static float[] ResampleAudio(float[] inputAudio, int inputSampleRate, int outputSampleRate) => 
            AudioHelpers.ResampleAudio(inputAudio, inputSampleRate, outputSampleRate);

        /// <summary>
        /// Detects if audio packet contains OPUS encoded data
        /// </summary>
        /// <param name="packet">Audio packet metadata</param>
        /// <returns>True if OPUS encoded, false if PCM</returns>
        public static bool IsOpusEncoded(AudioPacketMetadata packet) => AudioHelpers.IsOpusEncoded(packet);

        /// <summary>
        /// Exports audio data from recorded file to WAV format for external analysis
        /// </summary>
        /// <param name="sourceFilePath">Path to the source recording file</param>
        /// <param name="outputWavPath">Path where the WAV file should be saved</param>
        /// <param name="maxPackets">Maximum number of packets to export (default: 100)</param>
        /// <returns>Task representing the async export operation</returns>
        public static Task ExportToWavAsync(string sourceFilePath, string outputWavPath, int maxPackets = 100) => 
            AudioHelpers.ExportToWavAsync(sourceFilePath, outputWavPath, maxPackets);

        /// <summary>
        /// Estimates the output WAV file size before exporting
        /// </summary>
        /// <param name="sourceFilePath">Path to the source recording file</param>
        /// <param name="maxPackets">Maximum number of packets to analyze</param>
        /// <returns>Estimated output file size in bytes, or -1 if estimation failed</returns>
        public static Task<long> EstimateWavExportSizeAsync(string sourceFilePath, int maxPackets = 100) => 
            AudioHelpers.EstimateWavExportSizeAsync(sourceFilePath, maxPackets);

        #endregion

        #region Player Operations

        /// <summary>
        /// Gets a fallback player name from PlayerInfo and GUID
        /// </summary>
        /// <param name="playerInfo">Player information</param>
        /// <param name="transmitterGuid">Transmitter GUID as fallback</param>
        /// <returns>Display name for the player</returns>
        public static string GetPlayerNameWithFallback(PlayerInfo? playerInfo, string transmitterGuid) => 
            PlayerHelpers.GetPlayerNameWithFallback(playerInfo, transmitterGuid);

        /// <summary>
        /// Gets seat information with fallback
        /// </summary>
        /// <param name="seat">Seat number</param>
        /// <returns>Formatted seat string</returns>
        public static string GetSeatWithFallback(int seat) => PlayerHelpers.GetSeatWithFallback(seat);

        /// <summary>
        /// Gets recording status display string
        /// </summary>
        /// <param name="allowRecord">Whether recording is allowed</param>
        /// <returns>Recording status string</returns>
        public static string GetRecordingStatus(bool allowRecord) => PlayerHelpers.GetRecordingStatus(allowRecord);

        #endregion

        #region UI Operations

        /// <summary>
        /// Creates a rounded rectangle GraphicsPath for drawing rounded panels and buttons
        /// </summary>
        /// <param name="rect">Rectangle bounds</param>
        /// <param name="cornerRadius">Corner radius in pixels</param>
        /// <returns>GraphicsPath for the rounded rectangle</returns>
        public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius) => 
            UiHelpers.CreateRoundedRectanglePath(rect, cornerRadius);

        /// <summary>
        /// Draws a filled rounded panel using the provided Graphics instance
        /// </summary>
        /// <param name="g">Graphics instance</param>
        /// <param name="bounds">Panel bounds</param>
        /// <param name="cornerRadius">Corner radius in pixels</param>
        /// <param name="fillColor">Fill color</param>
        public static void DrawRoundedPanel(Graphics g, Rectangle bounds, int cornerRadius, Color fillColor) => 
            UiHelpers.DrawRoundedPanel(g, bounds, cornerRadius, fillColor);

        /// <summary>
        /// Gets available theme files
        /// </summary>
        /// <returns>Collection of available theme file names</returns>
        public static IEnumerable<string> GetAvailableThemeFiles() => UiHelpers.GetAvailableThemeFiles();

        /// <summary>
        /// Gets the current recorder theme file
        /// </summary>
        /// <returns>Current recorder theme file name</returns>
        public static string GetRecorderThemeFile() => UiHelpers.GetRecorderThemeFile();

        /// <summary>
        /// Gets the current player theme file
        /// </summary>
        /// <returns>Current player theme file name</returns>
        public static string GetPlayerThemeFile() => UiHelpers.GetPlayerThemeFile();

        /// <summary>
        /// Applies a theme file to the application
        /// </summary>
        /// <param name="fileName">Theme file name</param>
        /// <returns>True if theme was applied successfully</returns>
        public static bool ApplyThemeFile(string fileName) => UiHelpers.ApplyThemeFile(fileName);

        #endregion

        #region Quick Access Properties

        /// <summary>
        /// Quick access to Time helper operations
        /// </summary>
        public static class Time
        {
            /// <summary>
            /// Formats a TimeSpan into a user-friendly time string (H:MM:SS or M:SS)
            /// </summary>
            public static string Format(TimeSpan timeSpan) => TimeHelpers.FormatTime(timeSpan);

            /// <summary>
            /// Formats a TimeSpan into a detailed time string with milliseconds
            /// </summary>
            public static string FormatDetailed(TimeSpan timeSpan) => TimeHelpers.FormatTimeDetailed(timeSpan);

            /// <summary>
            /// Formats duration for display purposes (e.g., "1h 23m 45s")
            /// </summary>
            public static string FormatDuration(TimeSpan duration) => TimeHelpers.FormatDuration(duration);
        }

        /// <summary>
        /// Quick access to String helper operations
        /// </summary>
        public static class String
        {
            /// <summary>
            /// Safely truncates a string to a specified length
            /// </summary>
            public static string Truncate(string input, int maxLength, bool addEllipsis = true) => 
                StringHelpers.TruncateString(input, maxLength, addEllipsis);

            /// <summary>
            /// Gets a safe filename from a potentially unsafe string
            /// </summary>
            public static string SafeFilename(string filename) => StringHelpers.GetSafeFilename(filename);

            /// <summary>
            /// Safely gets a substring of a GUID for display
            /// </summary>
            public static string DisplayGuid(string guid, int length = 8) => 
                StringHelpers.GetDisplayGuid(guid, length);
        }

        /// <summary>
        /// Quick access to Audio helper operations
        /// </summary>
        public static class Audio
        {
            /// <summary>
            /// Converts audio sample count to duration
            /// </summary>
            public static TimeSpan SamplesToDuration(int sampleCount, int sampleRate) => 
                AudioHelpers.SamplesToDuration(sampleCount, sampleRate);

            /// <summary>
            /// Formats audio size in human-readable format
            /// </summary>
            public static string FormatSize(long bytes) => AudioHelpers.FormatAudioSize(bytes);

            /// <summary>
            /// Detects if audio packet contains OPUS encoded data
            /// </summary>
            public static bool IsOpus(AudioPacketMetadata packet) => AudioHelpers.IsOpusEncoded(packet);
        }

        /// <summary>
        /// Quick access to DCS helper operations
        /// </summary>
        public static class Dcs
        {
            /// <summary>
            /// Gets the coalition name from an integer value
            /// </summary>
            public static string CoalitionName(int coalition) => DcsHelpers.GetCoalitionName(coalition);

            /// <summary>
            /// Gets UI color for coalition from integer value
            /// </summary>
            public static Color CoalitionColor(int coalitionValue) => DcsHelpers.GetCoalitionColor(coalitionValue);

            /// <summary>
            /// Formats frequency for display
            /// </summary>
            public static string FormatFrequency(double frequencyHz, int decimalPlaces = 3) => 
                DcsHelpers.FormatFrequency(frequencyHz, decimalPlaces);
        }

        /// <summary>
        /// Quick access to Validation helper operations
        /// </summary>
        public static class Validation
        {
            /// <summary>
            /// Validates an IP address string
            /// </summary>
            public static bool IsValidIp(string ipAddress) => ValidationHelpers.IsValidIpAddress(ipAddress);

            /// <summary>
            /// Validates a port number
            /// </summary>
            public static bool IsValidPort(int port) => ValidationHelpers.IsValidPort(port);

            /// <summary>
            /// Validates a frequency value
            /// </summary>
            public static bool IsValidFrequency(double frequency) => ValidationHelpers.IsValidFrequency(frequency);
        }

        #endregion
    }
}