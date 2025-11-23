using System;
using System.Collections.Generic;

namespace AeroDebrief.Core.Storage.Abstractions
{
    /// <summary>
    /// Player statistics and aggregated information.
    /// Contains aggregated data about a player's radio transmissions.
    /// </summary>
    public class PlayerStats
    {
        /// <summary>
        /// Player display name
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Unique transmitter GUID
        /// </summary>
        public string TransmitterGuid { get; set; } = string.Empty;

        /// <summary>
        /// Coalition (0=Spectator, 1=Red, 2=Blue)
        /// </summary>
        public byte Coalition { get; set; }

        /// <summary>
        /// Aircraft/unit type (e.g., "F-16C")
        /// </summary>
        public string? UnitType { get; set; }

        /// <summary>
        /// Total number of transmissions
        /// </summary>
        public long TransmissionCount { get; set; }

        /// <summary>
        /// First transmission timestamp
        /// </summary>
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// Last transmission timestamp
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// List of frequencies used by this player
        /// </summary>
        public List<double> Frequencies { get; set; } = new();

        /// <summary>
        /// Human-readable coalition name
        /// </summary>
        public string CoalitionName => Coalition switch
        {
            0 => "Spectator",
            1 => "Red",
            2 => "Blue",
            _ => "Unknown"
        };

        /// <summary>
        /// Time span between first and last transmission
        /// </summary>
        public TimeSpan ActiveDuration => LastSeen - FirstSeen;

        /// <summary>
        /// Number of unique frequencies used
        /// </summary>
        public int FrequencyCount => Frequencies.Count;
    }
}
