using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Ready acknowledgment message sent from Tacview or AeroDebrief
/// </summary>
public class ReadyMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ready";
    
    /// <summary>
    /// Version (AeroDebrief or Tacview server version)
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Server version (when received from Tacview)
    /// </summary>
    [JsonPropertyName("server_version")]
    public string? ServerVersion { get; set; }
    
    /// <summary>
    /// Recording start time in UTC ISO 8601 format
    /// </summary>
    [JsonPropertyName("recording_start_utc")]
    public string? RecordingStartUtc { get; set; }
}
