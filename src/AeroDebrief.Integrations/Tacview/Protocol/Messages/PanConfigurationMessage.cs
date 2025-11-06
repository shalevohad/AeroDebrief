using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Pan configuration message sent from AeroDebrief to Tacview (bidirectional config)
/// </summary>
public class PanConfigurationMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pan_configuration";
    
    /// <summary>
    /// Pan mode ("auto" or "manual")
    /// </summary>
    [JsonPropertyName("pan_mode")]
    public string PanMode { get; set; } = "auto";
    
    /// <summary>
    /// Per-pilot pan settings (pilot_id -> pan value)
    /// </summary>
    [JsonPropertyName("pilot_pan_settings")]
    public Dictionary<string, double>? PilotPanSettings { get; set; }
}
