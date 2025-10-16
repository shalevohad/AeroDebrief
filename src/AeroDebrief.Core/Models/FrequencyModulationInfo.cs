using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.Core.Models
{
    /// <summary>
    /// Represents a frequency-modulation combination for filtering and display purposes
    /// </summary>
    public record FrequencyModulationInfo(double Frequency, Modulation Modulation)
    {
        /// <summary>
        /// List of players who transmitted on this frequency-modulation combination
        /// </summary>
        public List<PlayerFrequencyInfo> Players { get; init; } = new();

        /// <summary>
        /// Gets the human-readable modulation name
        /// </summary>
        public string GetModulationName() => Modulation.ToString();
        
        /// <summary>
        /// Gets a formatted display string for UI presentation
        /// </summary>
        public string GetDisplayText()
        {
            var frequencyMhz = Frequency / 1_000_000.0;
            return $"{frequencyMhz:F3} MHz ({GetModulationName()})";
        }
    }

    /// <summary>
    /// Represents player information for a specific frequency
    /// </summary>
    public record PlayerFrequencyInfo
    {
        public string Name { get; init; } = string.Empty;
        public string TransmitterGuid { get; init; } = string.Empty;
        public string Coalition { get; init; } = string.Empty;
        public string Aircraft { get; init; } = string.Empty;
        public int PacketCount { get; init; }
        public DateTime FirstSeen { get; init; }
        public DateTime LastSeen { get; init; }

        /// <summary>
        /// Gets a formatted display string for UI presentation
        /// </summary>
        public string GetDisplayText()
        {
            var name = !string.IsNullOrEmpty(Name) && Name != TransmitterGuid 
                ? Name 
                : $"Unknown ({TransmitterGuid[..Math.Min(8, TransmitterGuid.Length)]})";
            
            var aircraft = !string.IsNullOrEmpty(Aircraft) ? $" [{Aircraft}]" : "";
            var coalition = !string.IsNullOrEmpty(Coalition) ? $" ({Coalition})" : "";
            
            return $"{name}{aircraft}{coalition} - {PacketCount} packets";
        }
    }
}