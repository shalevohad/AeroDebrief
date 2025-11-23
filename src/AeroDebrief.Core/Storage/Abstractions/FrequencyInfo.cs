using System;

namespace AeroDebrief.Core.Storage.Abstractions
{
    /// <summary>
    /// Frequency information with statistics.
    /// Contains aggregated data about radio frequency usage.
    /// UNIT STANDARD: All frequencies stored in Hz for precision and consistency.
    /// </summary>
    public class FrequencyInfo
    {
        /// <summary>
        /// Radio frequency in Hz (e.g., 127500000.0 for 127.5 MHz)
        /// UNIT STANDARD: Always Hz internally
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Modulation type (0=AM, 1=FM, 2=INTERCOM, 3=DISABLED)
        /// </summary>
        public byte Modulation { get; set; }

        /// <summary>
        /// Total number of packets on this frequency
        /// </summary>
        public long PacketCount { get; set; }

        /// <summary>
        /// First transmission timestamp on this frequency
        /// </summary>
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// Last transmission timestamp on this frequency
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Number of unique players who used this frequency
        /// </summary>
        public int PlayerCount { get; set; }

        /// <summary>
        /// Time span of frequency usage
        /// </summary>
        public TimeSpan Duration => LastSeen - FirstSeen;

        /// <summary>
        /// Human-readable frequency (e.g., "127.500 MHz")
        /// Converts internal Hz to display MHz
        /// </summary>
        public string FormattedFrequency => $"{Frequency / 1_000_000.0:F3} MHz";

        /// <summary>
        /// Human-readable modulation name
        /// </summary>
        public string ModulationName => Modulation switch
        {
            0 => "AM",
            1 => "FM",
            2 => "INTERCOM",
            3 => "DISABLED",
            _ => "UNKNOWN"
        };
    }
}
