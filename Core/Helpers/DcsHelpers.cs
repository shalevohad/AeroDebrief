using System;
using System.Drawing;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Helpers
{
    /// <summary>
    /// Helper methods for DCS coalitions and modulation handling
    /// </summary>
    public static class DcsHelpers
    {
        /// <summary>
        /// Coalition enum compatible with SRS standards
        /// </summary>
        public enum Coalition
        {
            /// <summary>
            /// Spectator or neutral (SRS value: 0)
            /// </summary>
            Spectator = 0,
            
            /// <summary>
            /// Red coalition (SRS value: 1)
            /// </summary>
            Red = 1,
            
            /// <summary>
            /// Blue coalition (SRS value: 2)
            /// </summary>
            Blue = 2
        }

        #region Coalition Helpers

        /// <summary>
        /// Gets the coalition name from an integer value
        /// </summary>
        /// <param name="coalition">Coalition integer value</param>
        /// <returns>Coalition name string</returns>
        public static string GetCoalitionName(int coalition)
        {
            if (Enum.IsDefined(typeof(Coalition), coalition))
            {
                return ((Coalition)coalition).ToString();
            }
            
            // Fallback for unknown values (future-proofing)
            return coalition switch
            {
                1 => "Red",
                2 => "Blue",
                0 => "Spectator",
                _ => $"Unknown ({coalition})"
            };
        }

        /// <summary>
        /// Converts integer coalition value to Coalition enum safely
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>Coalition enum value</returns>
        public static Coalition ToCoalition(int coalitionValue)
        {
            return Enum.IsDefined(typeof(Coalition), coalitionValue) 
                ? (Coalition)coalitionValue 
                : Coalition.Spectator;
        }

        /// <summary>
        /// Gets UI color for coalition
        /// </summary>
        /// <param name="coalition">Coalition enum value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static Color GetCoalitionColor(Coalition coalition)
        {
            return coalition switch
            {
                Coalition.Red => Color.Red,
                Coalition.Blue => Color.Blue,
                Coalition.Spectator => Color.Gray,
                _ => SystemColors.ControlText
            };
        }

        /// <summary>
        /// Gets UI color for coalition from integer value
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static Color GetCoalitionColor(int coalitionValue)
        {
            return GetCoalitionColor(ToCoalition(coalitionValue));
        }

        #endregion

        #region Modulation Helpers

        /// <summary>
        /// Gets the modulation name from a byte value
        /// </summary>
        /// <param name="modulation">Modulation byte value</param>
        /// <returns>Modulation name string</returns>
        public static string GetModulationName(byte modulation)
        {
            // Use the SRS Common Modulation enum for accurate mapping
            var mod = Enum.IsDefined(typeof(Modulation), (int)modulation) 
                ? (Modulation)modulation 
                : Modulation.DISABLED;
                
            return mod.ToString();
        }

        /// <summary>
        /// Determines appropriate radio model based on packet modulation
        /// </summary>
        /// <param name="modulation">Modulation type</param>
        /// <returns>Radio model name for SRS effects pipeline</returns>
        public static string DetermineRadioModel(byte modulation)
        {
            var mod = (Modulation)modulation;
            
            // Map modulation types to appropriate radio models
            // These should match the radio models available in your SRS Common setup
            return mod switch
            {
                Modulation.AM => "AN/PRC-152", // Common military AM radio
                Modulation.FM => "AN/PRC-148", // Common military FM radio
                Modulation.INTERCOM => "Intercom", // Aircraft intercom
                Modulation.DISABLED => "NoEffect", // No radio effects
                _ => "GenericAM" // Fallback
            };
        }

        #endregion

        #region Frequency Helpers

        /// <summary>
        /// Converts frequency from Hz to MHz
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <returns>Frequency in MHz</returns>
        public static double HzToMHz(double frequencyHz)
        {
            return frequencyHz / 1_000_000.0;
        }

        /// <summary>
        /// Converts frequency from MHz to Hz
        /// </summary>
        /// <param name="frequencyMHz">Frequency in MHz</param>
        /// <returns>Frequency in Hz</returns>
        public static double MHzToHz(double frequencyMHz)
        {
            return frequencyMHz * 1_000_000.0;
        }

        /// <summary>
        /// Formats frequency for display
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <param name="decimalPlaces">Number of decimal places (default: 3)</param>
        /// <returns>Formatted frequency string with MHz unit</returns>
        public static string FormatFrequency(double frequencyHz, int decimalPlaces = 3)
        {
            var frequencyMhz = HzToMHz(frequencyHz);
            return $"{frequencyMhz.ToString($"F{decimalPlaces}")} MHz";
        }

        #endregion
    }
}