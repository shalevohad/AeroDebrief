using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Frequency filter update message sent from AeroDebrief to Tacview (bidirectional config)
/// </summary>
public class FrequencyFilterUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "frequency_filter_update";
    
    /// <summary>
    /// Pilot ID (null = general frequencies for non-selected pilots)
    /// </summary>
    [JsonPropertyName("pilot_id")]
    public string? PilotId { get; set; }
    
    /// <summary>
    /// List of enabled frequencies
    /// </summary>
    [JsonPropertyName("enabled_frequencies")]
    public List<double> EnabledFrequencies { get; set; } = new();
}
