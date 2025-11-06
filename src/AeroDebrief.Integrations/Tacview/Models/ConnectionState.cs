namespace AeroDebrief.Integrations.Tacview.Models;

/// <summary>
/// Represents the connection state between AeroDebrief and Tacview
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Not connected to Tacview
    /// </summary>
    Disconnected,
    
    /// <summary>
    /// Attempting to connect to Tacview
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Connected to Tacview but not yet synchronized
    /// </summary>
    Connected,
    
    /// <summary>
    /// Connected and synchronized with Tacview (ideal state)
    /// </summary>
    Synchronized,
    
    /// <summary>
    /// Connected but experiencing synchronization issues
    /// </summary>
    Degraded
}
