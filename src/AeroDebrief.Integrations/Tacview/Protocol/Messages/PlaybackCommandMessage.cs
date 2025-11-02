using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Playback command message sent from Tacview
/// </summary>
public class PlaybackCommandMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "playback_command";
    
    /// <summary>
    /// Command to execute ("play", "pause", "stop")
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;
}
