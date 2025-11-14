namespace AeroDebrief.Integrations.Tacview.Models;

/// <summary>
/// Represents a pilot in Tacview with associated frequency filtering and pan settings
/// </summary>
public class TacviewPilot
{
    /// <summary>
    /// Unique identifier for the pilot (from Tacview object ID or pilot name)
    /// </summary>
    public string PilotId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the pilot
    /// </summary>
    public string PilotName { get; set; } = string.Empty;
    
    /// <summary>
    /// Coalition/side (e.g., "Red", "Blue", "Neutral")
    /// </summary>
    public string? Coalition { get; set; }
    
    /// <summary>
    /// Aircraft/unit type
    /// </summary>
    public string? UnitType { get; set; }
    
    /// <summary>
    /// All known frequencies this pilot can use
    /// </summary>
    public List<double> AllFrequencies { get; set; } = new();
    
    /// <summary>
    /// Frequencies that are currently enabled for playback (null = all enabled)
    /// </summary>
    public List<double>? EnabledFrequencies { get; set; }
    
    /// <summary>
    /// Spatial audio pan value (-1.0 = full left, 0.0 = center, 1.0 = full right)
    /// </summary>
    public double Pan { get; set; } = 0.0;
}
