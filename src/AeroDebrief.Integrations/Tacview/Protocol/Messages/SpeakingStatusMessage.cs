using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages
{
    /// <summary>
    /// Speaking status message sent from AeroDebrief to Tacview
    /// Indicates when a pilot starts/stops transmitting on a frequency
    /// </summary>
    public class SpeakingStatusMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "speaking_status";
        
        [JsonPropertyName("pilot_id")]
        public string PilotId { get; set; } = string.Empty;
        
        [JsonPropertyName("pilot_name")]
        public string? PilotName { get; set; }
        
        [JsonPropertyName("frequency")]
        public double Frequency { get; set; }
        
        [JsonPropertyName("is_speaking")]
        public bool IsSpeaking { get; set; }
        
        [JsonPropertyName("timestamp_utc")]
        public string TimestampUtc { get; set; } = string.Empty;
    }
}
