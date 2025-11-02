using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Ready acknowledgment message sent from AeroDebrief to Tacview
/// </summary>
public class ReadyMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ready";
    
    /// <summary>
    /// AeroDebrief version
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Recording start time in UTC ISO 8601 format
    /// </summary>
    [JsonPropertyName("recording_start_utc")]
    public string? RecordingStartUtc { get; set; }
}
