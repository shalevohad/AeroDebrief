using System.Text.Json;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using NLog;

namespace AeroDebrief.Integrations.Tacview.Protocol;

/// <summary>
/// Handles encoding and decoding of Tacview protocol messages
/// Provides message serialization/deserialization with proper error handling
/// </summary>
public static class TacviewProtocol
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    /// <summary>
    /// Parses a JSON message from Tacview
    /// </summary>
    /// <param name="json">JSON string</param>
    /// <returns>Parsed message object or null if parsing failed</returns>
    public static object? ParseMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Logger.Warn("Received empty message from Tacview");
            return null;
        }
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("type", out var typeElement))
            {
                Logger.Warn("Message missing 'type' field");
                return null;
            }
            
            var type = typeElement.GetString();
            
            return type switch
            {
                "time_update" => JsonSerializer.Deserialize<TimeUpdateMessage>(json, SerializerOptions),
                "pilot_selection" => JsonSerializer.Deserialize<PilotSelectionMessage>(json, SerializerOptions),
                "playback_command" => JsonSerializer.Deserialize<PlaybackCommandMessage>(json, SerializerOptions),
                "seek" => JsonSerializer.Deserialize<SeekMessage>(json, SerializerOptions),
                "ready" => JsonSerializer.Deserialize<ReadyMessage>(json, SerializerOptions),
                _ => throw new NotSupportedException($"Unknown message type: {type}")
            };
        }
        catch (JsonException ex)
        {
            Logger.Error(ex, $"Failed to parse JSON message: {json}");
            return null;
        }
        catch (NotSupportedException ex)
        {
            Logger.Warn(ex, $"Unsupported message type: {json}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Unexpected error parsing message: {json}");
            return null;
        }
    }
    
    /// <summary>
    /// Serializes a message to JSON for sending to Tacview
    /// </summary>
    /// <param name="message">Message object</param>
    /// <returns>JSON string with newline delimiter</returns>
    public static string SerializeMessage(object message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        try
        {
            var json = JsonSerializer.Serialize(message, message.GetType(), SerializerOptions);
            return json + "\n"; // Newline-delimited JSON
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to serialize message: {message.GetType().Name}");
            throw;
        }
    }
    
    /// <summary>
    /// Creates a speaking status message (AeroDebrief ? Tacview)
    /// </summary>
    public static SpeakingStatusMessage CreateSpeakingStatus(
        string pilotId,
        string? pilotName,
        double frequency,
        bool isSpeaking)
    {
        return new SpeakingStatusMessage
        {
            PilotId = pilotId,
            PilotName = pilotName,
            Frequency = frequency,
            IsSpeaking = isSpeaking,
            TimestampUtc = DateTime.UtcNow.ToString("o")
        };
    }
    
    /// <summary>
    /// Creates a frequency filter update message (AeroDebrief ? Tacview)
    /// </summary>
    public static FrequencyFilterUpdateMessage CreateFrequencyFilterUpdate(
        string? pilotId,
        List<double> enabledFrequencies)
    {
        return new FrequencyFilterUpdateMessage
        {
            PilotId = pilotId,
            EnabledFrequencies = enabledFrequencies ?? new List<double>()
        };
    }
    
    /// <summary>
    /// Creates a pan configuration message (AeroDebrief ? Tacview)
    /// </summary>
    public static PanConfigurationMessage CreatePanConfiguration(
        string panMode,
        Dictionary<string, double>? pilotPanSettings = null)
    {
        return new PanConfigurationMessage
        {
            PanMode = panMode,
            PilotPanSettings = pilotPanSettings
        };
    }
    
    /// <summary>
    /// Validates a message structure
    /// </summary>
    public static bool ValidateMessage(object message, out string? error)
    {
        error = null;
        
        if (message == null)
        {
            error = "Message is null";
            return false;
        }
        
        // Type-specific validation
        return message switch
        {
            TimeUpdateMessage timeUpdate => ValidateTimeUpdate(timeUpdate, out error),
            PilotSelectionMessage pilotSelection => ValidatePilotSelection(pilotSelection, out error),
            PlaybackCommandMessage playbackCommand => ValidatePlaybackCommand(playbackCommand, out error),
            SeekMessage seek => ValidateSeek(seek, out error),
            SpeakingStatusMessage speakingStatus => ValidateSpeakingStatus(speakingStatus, out error),
            FrequencyFilterUpdateMessage freqFilter => ValidateFrequencyFilter(freqFilter, out error),
            PanConfigurationMessage panConfig => ValidatePanConfiguration(panConfig, out error),
            _ => true // Unknown message types are allowed
        };
    }
    
    private static bool ValidateTimeUpdate(TimeUpdateMessage message, out string? error)
    {
        if (string.IsNullOrWhiteSpace(message.MissionTimeUtc))
        {
            error = "MissionTimeUtc is required";
            return false;
        }
        
        if (!DateTime.TryParse(message.MissionTimeUtc, out _))
        {
            error = "MissionTimeUtc is not a valid ISO 8601 date";
            return false;
        }
        
        if (message.PlaybackSpeed < 0.1 || message.PlaybackSpeed > 10.0)
        {
            error = "PlaybackSpeed must be between 0.1 and 10.0";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidatePilotSelection(PilotSelectionMessage message, out string? error)
    {
        if (message.SelectedPilots == null)
        {
            error = "SelectedPilots is required";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidatePlaybackCommand(PlaybackCommandMessage message, out string? error)
    {
        if (string.IsNullOrWhiteSpace(message.Command))
        {
            error = "Command is required";
            return false;
        }
        
        var validCommands = new[] { "play", "pause", "stop" };
        if (!validCommands.Contains(message.Command.ToLowerInvariant()))
        {
            error = $"Command must be one of: {string.Join(", ", validCommands)}";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidateSeek(SeekMessage message, out string? error)
    {
        if (string.IsNullOrWhiteSpace(message.TargetTimeUtc))
        {
            error = "TargetTimeUtc is required";
            return false;
        }
        
        if (!DateTime.TryParse(message.TargetTimeUtc, out _))
        {
            error = "TargetTimeUtc is not a valid ISO 8601 date";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidateSpeakingStatus(SpeakingStatusMessage message, out string? error)
    {
        if (string.IsNullOrWhiteSpace(message.PilotId))
        {
            error = "PilotId is required";
            return false;
        }
        
        if (message.Frequency <= 0)
        {
            error = "Frequency must be positive";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidateFrequencyFilter(FrequencyFilterUpdateMessage message, out string? error)
    {
        if (message.EnabledFrequencies == null)
        {
            error = "EnabledFrequencies is required";
            return false;
        }
        
        error = null;
        return true;
    }
    
    private static bool ValidatePanConfiguration(PanConfigurationMessage message, out string? error)
    {
        if (string.IsNullOrWhiteSpace(message.PanMode))
        {
            error = "PanMode is required";
            return false;
        }
        
        var validModes = new[] { "auto", "manual" };
        if (!validModes.Contains(message.PanMode.ToLowerInvariant()))
        {
            error = $"PanMode must be one of: {string.Join(", ", validModes)}";
            return false;
        }
        
        error = null;
        return true;
    }
}
