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
    /// Phase 4 Update: Includes marker geometry identifier for visualization
    /// </summary>
    public class PlayerFrequencyInfo
    {
        public string Name { get; init; } = string.Empty;
        public string TransmitterGuid { get; init; } = string.Empty;
        public string Coalition { get; init; } = string.Empty;
        public string Aircraft { get; init; } = string.Empty;
        public int PacketCount { get; init; }
        public DateTime FirstSeen { get; init; }
        public DateTime LastSeen { get; init; }

        /// <summary>
        /// Indicates if the pilot is selected/enabled for playback (checkbox state)
        /// When false, the pilot's audio will be filtered out
        /// </summary>
        public bool IsSelected { get; set; } = true; // Default to selected

        /// <summary>
        /// Indicates if this pilot was just discovered during live recording
        /// Used for visual feedback (glow effect animation)
        /// </summary>
        public bool IsNewlyDiscovered { get; set; }

        /// <summary>
        /// Phase 4: Unique identifier for this pilot (used for marker assignment)
        /// Typically: TransmitterGuid or Name if guid not available
        /// </summary>
        public string PilotId => !string.IsNullOrEmpty(TransmitterGuid) ? TransmitterGuid : Name;

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

        /// <summary>
        /// Gets a display name with marker indicator for legends and tooltips
        /// Phase 4: Can be enhanced to include visual marker representation
        /// </summary>
        public string GetDisplayTextWithMarker()
        {
            // Future: This could include a visual marker representation
            // For now, returns standard display text
            return GetDisplayText();
        }
    }
}