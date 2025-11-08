using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Sync status message sent between AeroDebrief and Tacview
/// Indicates current synchronization state and quality
/// </summary>
public class SyncStatusMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "sync_status";
    
    /// <summary>
    /// Current sync quality (0-100%)
    /// </summary>
    [JsonPropertyName("sync_quality")]
    public double SyncQuality { get; set; }
    
    /// <summary>
    /// Current drift in milliseconds
    /// </summary>
    [JsonPropertyName("drift_ms")]
    public int DriftMs { get; set; }
    
    /// <summary>
    /// Whether currently synchronized (optional field)
    /// </summary>
    [JsonPropertyName("is_synchronized")]
    public bool? IsSynchronized { get; set; }
}
