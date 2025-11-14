using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Time update message sent from Tacview to AeroDebrief
/// Indicates current mission time, playback state, and playback speed
/// </summary>
public class TimeUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "time_update";
    
    /// <summary>
    /// Mission time in UTC ISO 8601 format
    /// </summary>
    [JsonPropertyName("mission_time_utc")]
    public string MissionTimeUtc { get; set; } = string.Empty;
    
    /// <summary>
    /// Current playback state ("playing", "paused", "stopped")
    /// </summary>
    [JsonPropertyName("playback_state")]
    public string PlaybackState { get; set; } = string.Empty;
    
    /// <summary>
    /// Playback speed multiplier (1.0 = normal, 2.0 = 2x, etc.)
    /// </summary>
    [JsonPropertyName("playback_speed")]
    public double PlaybackSpeed { get; set; } = 1.0;
}
