using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Seek command message sent from Tacview
/// </summary>
public class SeekMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "seek";
    
    /// <summary>
    /// Target time to seek to in UTC ISO 8601 format
    /// </summary>
    [JsonPropertyName("target_time_utc")]
    public string TargetTimeUtc { get; set; } = string.Empty;
}
