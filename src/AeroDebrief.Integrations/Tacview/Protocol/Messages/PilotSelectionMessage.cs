using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Pilot selection message sent from Tacview when user selects aircraft
/// </summary>
public class PilotSelectionMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pilot_selection";
    
    /// <summary>
    /// List of selected pilots with their configuration
    /// </summary>
    [JsonPropertyName("selected_pilots")]
    public List<PilotData> SelectedPilots { get; set; } = new();
    
    /// <summary>
    /// Pan mode ("auto" or "manual")
    /// </summary>
    [JsonPropertyName("pan_mode")]
    public string PanMode { get; set; } = "auto";
    
    /// <summary>
    /// Frequencies enabled for non-selected pilots (general filter)
    /// Empty list = all disabled by default
    /// </summary>
    [JsonPropertyName("general_enabled_frequencies")]
    public List<double> GeneralEnabledFrequencies { get; set; } = new();
}

/// <summary>
/// Pilot data within a selection message
/// </summary>
public class PilotData
{
    [JsonPropertyName("pilot_id")]
    public string PilotId { get; set; } = string.Empty;
    
    [JsonPropertyName("pilot_name")]
    public string PilotName { get; set; } = string.Empty;
    
    [JsonPropertyName("coalition")]
    public string? Coalition { get; set; }
    
    [JsonPropertyName("unit_type")]
    public string? UnitType { get; set; }
    
    [JsonPropertyName("frequencies")]
    public List<double> Frequencies { get; set; } = new();
    
    [JsonPropertyName("enabled_frequencies")]
    public List<double>? EnabledFrequencies { get; set; }
    
    [JsonPropertyName("pan")]
    public double Pan { get; set; } = 0.0;
}
