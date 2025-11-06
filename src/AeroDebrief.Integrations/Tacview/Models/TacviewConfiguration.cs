namespace AeroDebrief.Integrations.Tacview.Models;

/// <summary>
/// Configuration settings for Tacview integration
/// </summary>
public class TacviewConfiguration
{
    /// <summary>
    /// The hostname/IP address of the Tacview server (default: localhost only for security)
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";
    
    /// <summary>
    /// The TCP port for the Tacview addon server (default: 52001)
    /// </summary>
    public int Port { get; set; } = 52001;
    
    /// <summary>
    /// Whether to automatically connect to Tacview on startup
    /// </summary>
    public bool AutoConnect { get; set; } = true;
    
    /// <summary>
    /// Whether to automatically attempt reconnection after disconnect
    /// </summary>
    public bool AutoReconnect { get; set; } = true;
    
    /// <summary>
    /// Maximum number of reconnection attempts before giving up
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;
    
    /// <summary>
    /// Base interval between reconnection attempts (in seconds)
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;
    
    /// <summary>
    /// Whether to enable automatic drift correction during playback
    /// </summary>
    public bool EnableSyncDriftCorrection { get; set; } = true;
    
    /// <summary>
    /// Maximum acceptable drift in milliseconds before correction is applied
    /// </summary>
    public int MaxAcceptableDriftMs { get; set; } = 500;
    
    /// <summary>
    /// Whether to enable spatial audio (pan) feature
    /// </summary>
    public bool EnableSpatialAudio { get; set; } = true;
    
    /// <summary>
    /// Whether to enable frequency-based filtering
    /// </summary>
    public bool EnableFrequencyFiltering { get; set; } = true;
    
    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 10000;
    
    /// <summary>
    /// Receive timeout in milliseconds
    /// </summary>
    public int ReceiveTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Send timeout in milliseconds
    /// </summary>
    public int SendTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Buffer size for TCP socket
    /// </summary>
    public int SocketBufferSize { get; set; } = 8192;
    
    /// <summary>
    /// Enable detailed logging for debugging
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
    
    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <param name="error">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            error = "Host cannot be empty";
            return false;
        }
        
        if (Port < 1 || Port > 65535)
        {
            error = "Port must be between 1 and 65535";
            return false;
        }
        
        if (MaxReconnectAttempts < 0)
        {
            error = "MaxReconnectAttempts cannot be negative";
            return false;
        }
        
        if (ReconnectIntervalSeconds < 1)
        {
            error = "ReconnectIntervalSeconds must be at least 1";
            return false;
        }
        
        if (MaxAcceptableDriftMs < 0)
        {
            error = "MaxAcceptableDriftMs cannot be negative";
            return false;
        }
        
        if (ConnectionTimeoutMs < 1000)
        {
            error = "ConnectionTimeoutMs must be at least 1000";
            return false;
        }
        
        if (ReceiveTimeoutMs < 1000)
        {
            error = "ReceiveTimeoutMs must be at least 1000";
            return false;
        }
        
        if (SendTimeoutMs < 1000)
        {
            error = "SendTimeoutMs must be at least 1000";
            return false;
        }
        
        if (SocketBufferSize < 1024)
        {
            error = "SocketBufferSize must be at least 1024 bytes";
            return false;
        }
        
        error = null;
        return true;
    }
    
    /// <summary>
    /// Saves configuration to a JSON file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        System.IO.File.WriteAllText(filePath, json);
    }
    
    /// <summary>
    /// Loads configuration from a JSON file
    /// </summary>
    public static TacviewConfiguration LoadFromFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return new TacviewConfiguration();
        }
        
        var json = System.IO.File.ReadAllText(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<TacviewConfiguration>(json) ?? new TacviewConfiguration();
    }
}
