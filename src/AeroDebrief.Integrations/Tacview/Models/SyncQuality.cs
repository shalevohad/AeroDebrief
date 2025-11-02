namespace AeroDebrief.Integrations.Tacview.Models;

/// <summary>
/// Represents sync quality metrics between AeroDebrief and Tacview
/// </summary>
public class SyncQuality
{
    /// <summary>
    /// Current drift in milliseconds (negative = AeroDebrief ahead, positive = Tacview ahead)
    /// </summary>
    public int DriftMs { get; set; }
    
    /// <summary>
    /// Sync quality as a percentage (0-100%, 100% = perfect sync)
    /// </summary>
    public double QualityPercent { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime LastUpdate { get; set; }
    
    /// <summary>
    /// Whether the sync is currently healthy
    /// </summary>
    public bool IsHealthy => Math.Abs(DriftMs) <= 500;
}
