# Integration Project Design - AeroDebrief.Integrations

## Overview

The `AeroDebrief.Integrations` project contains all third-party integration code, including the Tacview synchronization system. This follows the separation of concerns principle by isolating integration logic from core functionality.

## Project Structure

```
src/AeroDebrief.Integrations/
??? AeroDebrief.Integrations.csproj
??? Tacview/
?   ??? Client/
?   ?   ??? TacviewClient.cs                    # TCP client implementation
?   ?   ??? TacviewConnection.cs                # Connection management
?   ?   ??? TacviewReconnectionStrategy.cs      # Reconnection logic
?   ??? Sync/
?   ?   ??? TacviewSyncService.cs               # Main synchronization service
?   ?   ??? TimeConverter.cs                    # UTC time conversions
?   ?   ??? SyncAlgorithm.cs                    # Drift correction algorithm
?   ?   ??? SyncHealthMonitor.cs                # Health tracking
?   ??? Pilot/
?   ?   ??? PilotMapper.cs                      # Maps Tacview pilots to frequencies
?   ?   ??? PilotFilter.cs                      # Filters audio packets by pilot
?   ?   ??? SpatialAudioCalculator.cs           # Pan calculations
?   ??? Protocol/
?   ?   ??? TacviewProtocol.cs                  # Message serialization
?   ?   ??? Messages/
?   ?   ?   ??? TacviewMessage.cs               # Base message class
?   ?   ?   ??? TimeUpdateMessage.cs
?   ?   ?   ??? PilotSelectionMessage.cs
?   ?   ?   ??? PlaybackCommandMessage.cs
?   ?   ?   ??? SeekMessage.cs
?   ?   ?   ??? ReadyMessage.cs
?   ?   ?   ??? SpeakingStatusMessage.cs
?   ?   ?   ??? SyncStatusMessage.cs
?   ?   ?   ??? ErrorMessage.cs
?   ?   ??? JsonConverter.cs                    # JSON serialization helpers
?   ??? Models/
?       ??? TacviewPilot.cs                     # Pilot model
?       ??? ConnectionState.cs                  # Connection state enum
?       ??? SyncQuality.cs                      # Sync quality metrics
?       ??? TacviewConfiguration.cs             # Configuration model
??? README.md
```

## Core Classes

### 1. TacviewClient (TCP Client)

**Purpose**: Manages TCP connection to Tacview Lua addon

**Responsibilities**:
- Establish TCP connection to localhost:52001
- Send/receive newline-delimited JSON messages
- Handle connection lifecycle
- Buffer incoming messages
- Thread-safe message queue

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Client;

public sealed class TacviewClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly ConcurrentQueue<TacviewMessage> _messageQueue;
    private readonly SemaphoreSlim _writeLock;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    
    public event EventHandler<TacviewMessage>? MessageReceived;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<Exception>? ErrorOccurred;
    
    public bool IsConnected { get; private set; }
    
    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken);
    public async Task DisconnectAsync();
    public async Task SendMessageAsync(TacviewMessage message);
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken);
    private void ProcessReceivedMessage(string messageJson);
}
```

**Key Features**:
- **Async I/O**: All network operations are async
- **Thread-Safe**: Uses locks for concurrent access
- **Message Buffering**: Queues messages for reliable delivery
- **Error Recovery**: Handles connection drops gracefully

---

### 2. TacviewSyncService (Orchestration)

**Purpose**: Central coordinator for Tacview synchronization

**Responsibilities**:
- Manage TacviewClient lifecycle
- Process incoming messages
- Coordinate time synchronization
- Manage pilot filtering
- Publish events to UI and Core

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Sync;

public sealed class TacviewSyncService : IDisposable
{
    private readonly TacviewClient _client;
    private readonly TacviewConnection _connection;
    private readonly SyncAlgorithm _syncAlgorithm;
    private readonly SyncHealthMonitor _healthMonitor;
    private readonly PilotMapper _pilotMapper;
    private readonly TimeConverter _timeConverter;
    
    // Events
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<TimeUpdateEventArgs>? TimeUpdateReceived;
    public event EventHandler<PilotSelectionEventArgs>? PilotSelectionChanged;
    public event EventHandler<PlaybackCommandEventArgs>? PlaybackCommandReceived;
    public event EventHandler<SeekEventArgs>? SeekRequested;
    public event EventHandler<SyncQualityEventArgs>? SyncQualityChanged;
    
    // State
    public bool IsConnected { get; }
    public ConnectionState State { get; }
    public SyncQuality Quality { get; }
    public IReadOnlyList<TacviewPilot> SelectedPilots { get; }
    
    // Methods
    public async Task StartAsync(TacviewConfiguration config);
    public async Task StopAsync();
    public async Task SendReadyAsync(DateTime recordingStart, DateTime recordingEnd, double[] frequencies);
    public async Task SendSpeakingStatusAsync(string pilotId, bool isSpeaking, double frequency);
    public async Task SendSyncStatusAsync();
    
    // Internal handlers
    private void OnMessageReceived(object? sender, TacviewMessage message);
    private void HandleTimeUpdate(TimeUpdateMessage message);
    private void HandlePilotSelection(PilotSelectionMessage message);
    private void HandlePlaybackCommand(PlaybackCommandMessage message);
    private void HandleSeek(SeekMessage message);
}
```

**State Machine**:
```
Disconnected ? Connecting ? Connected ? Synchronized
     ?              ?            ?            ?
     ??????????????????????????????????????????
              (Error or Stop)
```

---

### 3. SyncAlgorithm (Time Synchronization)

**Purpose**: Implements drift correction and time synchronization

**Responsibilities**:
- Calculate time drift between Tacview and AeroDebrief
- Determine required playback speed adjustment
- Trigger seek operations for large drifts
- Track sync quality metrics

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Sync;

public sealed class SyncAlgorithm
{
    private readonly TimeSpan _smallDriftThreshold = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _mediumDriftThreshold = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _largeDriftThreshold = TimeSpan.FromSeconds(1);
    
    private DateTime _lastTacviewTime;
    private DateTime _lastAeroDebriefTime;
    private readonly Queue<TimeSpan> _driftHistory;
    
    public SyncAction CalculateSyncAction(DateTime tacviewTime, DateTime aeroDebriefTime);
    public double CalculateSpeedAdjustment(TimeSpan drift);
    public TimeSpan CalculateAverageDrift();
    public SyncQuality EvaluateQuality();
}

public class SyncAction
{
    public SyncActionType Type { get; set; }  // None, SpeedAdjust, Seek
    public double SpeedMultiplier { get; set; } = 1.0;
    public DateTime? SeekTarget { get; set; }
}

public enum SyncActionType
{
    None,           // No action needed (drift < 100ms)
    SpeedAdjust,    // Adjust playback speed (100ms < drift < 500ms)
    Seek            // Immediate seek (drift > 500ms)
}
```

**Algorithm**:
```csharp
public SyncAction CalculateSyncAction(DateTime tacviewTime, DateTime aeroDebriefTime)
{
    var drift = tacviewTime - aeroDebriefTime;
    var absDrift = drift.Duration();
    
    if (absDrift < _smallDriftThreshold)
    {
        // Perfect sync - no action
        return new SyncAction { Type = SyncActionType.None };
    }
    else if (absDrift < _mediumDriftThreshold)
    {
        // Small drift - speed adjustment
        double speedMultiplier = 1.0 + (drift.TotalSeconds * 0.1); // 10% correction rate
        speedMultiplier = Math.Clamp(speedMultiplier, 0.98, 1.02); // Limit to ±2%
        
        return new SyncAction 
        { 
            Type = SyncActionType.SpeedAdjust,
            SpeedMultiplier = speedMultiplier
        };
    }
    else
    {
        // Large drift - seek
        return new SyncAction 
        { 
            Type = SyncActionType.Seek,
            SeekTarget = tacviewTime
        };
    }
}
```

---

### 4. PilotMapper (Pilot ? Frequency Mapping)

**Purpose**: Maps Tacview pilots to AeroDebrief audio streams

**Responsibilities**:
- Store pilot-to-frequency mappings
- Filter audio packets by pilot
- Apply spatial audio (pan)
- Track active pilots

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Pilot;

public sealed class PilotMapper
{
    private readonly Dictionary<string, TacviewPilot> _pilots;
    private readonly Dictionary<string, double> _pilotPanMap;
    
    public IReadOnlyList<TacviewPilot> SelectedPilots { get; }
    
    public void UpdateSelection(IEnumerable<TacviewPilot> pilots);
    public bool ShouldPlayPacket(AudioPacketMetadata packet);
    public double GetPanForPilot(string pilotId);
    public TacviewPilot? GetPilotByGuid(string guid);
    public IEnumerable<double> GetAllFrequencies();
}
```

**Filtering Logic**:
```csharp
public bool ShouldPlayPacket(AudioPacketMetadata packet)
{
    // No selection = play all
    if (_pilots.Count == 0)
        return true;
    
    // Check if transmitter is a selected pilot
    foreach (var pilot in _pilots.Values)
    {
        // Match by GUID
        if (packet.TransmitterGuid == pilot.PilotId ||
            packet.PlayerData?.TransmitterGuid == pilot.PilotId)
        {
            // Check if frequency matches any of pilot's frequencies
            if (pilot.Frequencies.Contains(packet.Frequency))
            {
                return true;
            }
        }
    }
    
    return false;
}
```

---

### 5. TacviewProtocol (Message Serialization)

**Purpose**: Serialize/deserialize protocol messages

**Responsibilities**:
- Convert messages to/from JSON
- Validate message structure
- Handle protocol versioning

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Protocol;

public static class TacviewProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
    
    public static string Serialize(TacviewMessage message);
    public static TacviewMessage? Deserialize(string json);
    public static bool TryDeserialize(string json, out TacviewMessage? message);
}
```

**Message Base Class**:
```csharp
public abstract class TacviewMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
```

**Example Message Classes**:
```csharp
public class TimeUpdateMessage : TacviewMessage
{
    public override string Type => "time_update";
    
    [JsonPropertyName("mission_time_utc")]
    public DateTime MissionTimeUtc { get; set; }
    
    [JsonPropertyName("playback_state")]
    public string PlaybackState { get; set; } = "stopped";
    
    [JsonPropertyName("playback_speed")]
    public double PlaybackSpeed { get; set; } = 1.0;
}

public class PilotSelectionMessage : TacviewMessage
{
    public override string Type => "pilot_selection";
    
    [JsonPropertyName("selected_pilots")]
    public List<TacviewPilot> SelectedPilots { get; set; } = new();
}

public class SpeakingStatusMessage : TacviewMessage
{
    public override string Type => "speaking_status";
    
    [JsonPropertyName("pilot_id")]
    public string PilotId { get; set; } = string.Empty;
    
    [JsonPropertyName("is_speaking")]
    public bool IsSpeaking { get; set; }
    
    [JsonPropertyName("frequency")]
    public double Frequency { get; set; }
    
    [JsonPropertyName("modulation")]
    public string Modulation { get; set; } = "AM";
    
    [JsonPropertyName("timestamp_utc")]
    public DateTime TimestampUtc { get; set; }
}
```

---

### 6. TimeConverter (UTC Conversions)

**Purpose**: Convert between Tacview UTC times and AeroDebrief TimeSpan offsets

**Responsibilities**:
- Convert ISO 8601 strings to DateTime
- Calculate TimeSpan from recording start
- Validate time ranges

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Sync;

public sealed class TimeConverter
{
    private DateTime _recordingStartUtc;
    private DateTime _recordingEndUtc;
    
    public void SetRecordingBounds(DateTime startUtc, DateTime endUtc);
    
    public TimeSpan ToTimeSpan(DateTime utcTime);
    public DateTime ToUtcTime(TimeSpan offset);
    
    public bool IsTimeInRange(DateTime utcTime);
    public DateTime ClampToRange(DateTime utcTime);
}
```

**Implementation**:
```csharp
public TimeSpan ToTimeSpan(DateTime utcTime)
{
    if (utcTime < _recordingStartUtc)
        return TimeSpan.Zero;
    
    if (utcTime > _recordingEndUtc)
        return _recordingEndUtc - _recordingStartUtc;
    
    return utcTime - _recordingStartUtc;
}

public DateTime ToUtcTime(TimeSpan offset)
{
    var utcTime = _recordingStartUtc + offset;
    return ClampToRange(utcTime);
}
```

---

### 7. TacviewConnection (Connection Management)

**Purpose**: Manage connection lifecycle with retry logic

**Responsibilities**:
- Automatic reconnection on disconnect
- Exponential backoff strategy
- Connection health monitoring
- State transitions

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Client;

public sealed class TacviewConnection : IDisposable
{
    private readonly TacviewClient _client;
    private readonly TacviewReconnectionStrategy _reconnection;
    private ConnectionState _state;
    private Timer? _reconnectTimer;
    
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
    
    public ConnectionState State => _state;
    public bool IsConnected => _state == ConnectionState.Connected || _state == ConnectionState.Synchronized;
    
    public async Task ConnectAsync(string host, int port);
    public async Task DisconnectAsync();
    
    private void OnClientDisconnected(object? sender, EventArgs e);
    private void ScheduleReconnection();
    private async void TryReconnect(object? state);
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Synchronized,
    Degraded,
    Reconnecting
}
```

---

### 8. SyncHealthMonitor (Health Tracking)

**Purpose**: Monitor synchronization quality and connection health

**Responsibilities**:
- Track sync drift over time
- Measure message latency
- Count dropped messages
- Calculate health score

**Code Outline**:
```csharp
namespace AeroDebrief.Integrations.Tacview.Sync;

public sealed class SyncHealthMonitor
{
    private readonly Queue<TimeSpan> _driftSamples;
    private readonly Queue<TimeSpan> _latencySamples;
    private int _totalMessages;
    private int _droppedMessages;
    private DateTime _lastUpdateTime;
    
    public void RecordDrift(TimeSpan drift);
    public void RecordLatency(TimeSpan latency);
    public void RecordMessageDropped();
    
    public SyncQuality EvaluateQuality();
    public SyncStatistics GetStatistics();
}

public class SyncQuality
{
    public SyncHealthLevel Level { get; set; }
    public TimeSpan AverageDrift { get; set; }
    public TimeSpan MaxDrift { get; set; }
    public double MessageSuccessRate { get; set; }
    public TimeSpan AverageLatency { get; set; }
}

public enum SyncHealthLevel
{
    Excellent,  // Drift < 100ms, 99%+ success rate
    Good,       // Drift < 500ms, 95%+ success rate
    Fair,       // Drift < 1s, 90%+ success rate
    Poor,       // Drift < 2s, 80%+ success rate
    Critical    // Drift > 2s or <80% success rate
}
```

---

## Integration with Core & UI

### Core Integration

**Modified Classes**:
1. **PlaybackController**: Add external time sync capability
2. **FrequencyChannelMixer**: Add pilot-based filtering
3. **AudioOutputEngine**: Add spatial audio (pan)

**Integration Points**:
```csharp
// In PlaybackController
public void SetExternalTimeSync(Func<TimeSpan> getTargetTime, Func<SyncAction> getSyncAction);

// In FrequencyChannelMixer
public void SetPilotFilter(Func<AudioPacketMetadata, bool> shouldPlay);

// In AudioOutputEngine
public void SetSpatialAudio(Func<string, double> getPanForPilot);
```

### UI Integration

**Service Registration** (in `UnifiedPlayerViewModel`):
```csharp
public class UnifiedPlayerViewModel : ViewModelBase
{
    private readonly TacviewSyncService _tacviewSync;
    
    public TacviewIntegrationViewModel TacviewIntegration { get; }
    
    private void InitializeTacviewIntegration()
    {
        _tacviewSync = new TacviewSyncService();
        TacviewIntegration = new TacviewIntegrationViewModel(_tacviewSync);
        
        // Wire up events
        _tacviewSync.TimeUpdateReceived += OnTacviewTimeUpdate;
        _tacviewSync.PilotSelectionChanged += OnTacviewPilotSelection;
        _tacviewSync.PlaybackCommandReceived += OnTacviewPlaybackCommand;
        _tacviewSync.SeekRequested += OnTacviewSeek;
    }
    
    private void OnTacviewTimeUpdate(object? sender, TimeUpdateEventArgs e)
    {
        // Apply sync action to PlaybackController
        var syncAction = _tacviewSync.GetCurrentSyncAction();
        
        if (syncAction.Type == SyncActionType.Seek)
        {
            SeekToTimeAsync(syncAction.SeekTarget.Value);
        }
        else if (syncAction.Type == SyncActionType.SpeedAdjust)
        {
            // Apply speed adjustment (if supported)
        }
    }
    
    private void OnTacviewPilotSelection(object? sender, PilotSelectionEventArgs e)
    {
        // Update pilot filter in FrequencyChannelMixer
        _mixerController.SetPilotFilter(_tacviewSync.PilotMapper.ShouldPlayPacket);
        
        // Update spatial audio
        _audioEngine.SetSpatialAudio(_tacviewSync.PilotMapper.GetPanForPilot);
    }
}
```

---

## Configuration

### TacviewConfiguration

```csharp
namespace AeroDebrief.Integrations.Tacview;

/// <summary>
/// Configuration settings for Tacview integration
/// </summary>
public class TacviewConfiguration
{
    /// <summary>
    /// Tacview TCP server host (default: localhost)
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Tacview TCP server port (default: 52001)
    /// </summary>
    public int Port { get; set; } = 52001;

    /// <summary>
    /// Auto-connect when Tacview integration is enabled
    /// </summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>
    /// Auto-reconnect on disconnection
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnect attempts before giving up
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    /// Delay between reconnect attempts (seconds)
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Enable sync drift correction
    /// </summary>
    public bool EnableSyncDriftCorrection { get; set; } = true;

    /// <summary>
    /// Maximum acceptable drift before correction (milliseconds)
    /// </summary>
    public int MaxAcceptableDriftMs { get; set; } = 500;

    /// <summary>
    /// Enable spatial audio (pan) from Tacview
    /// </summary>
    public bool EnableSpatialAudio { get; set; } = true;

    /// <summary>
    /// Enable frequency filtering from Tacview
    /// </summary>
    public bool EnableFrequencyFiltering { get; set; } = true;

    /// <summary>
    /// Connection timeout (milliseconds)
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Message receive timeout (milliseconds)
    /// </summary>
    public int ReceiveTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Validate configuration settings
    /// </summary>
    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            error = "Host cannot be empty";
            return false;
        }

        if (Port < 1024 || Port > 65535)
        {
            error = "Port must be between 1024 and 65535";
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

        error = null;
        return true;
    }

    /// <summary>
    /// Load configuration from settings file
    /// </summary>
    public static TacviewConfiguration LoadFromFile(string filePath)
    {
        var config = new TacviewConfiguration();
        
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<TacviewConfiguration>(json);
                if (loaded != null)
                {
                    config = loaded;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load Tacview configuration from file");
            }
        }
        
        return config;
    }

    /// <summary>
    /// Save configuration to settings file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save Tacview configuration to file");
            throw;
        }
    }

    /// <summary>
    /// Create default configuration
    /// </summary>
    public static TacviewConfiguration CreateDefault()
    {
        return new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            AutoConnect = true,
            AutoReconnect = true,
            MaxReconnectAttempts = 10,
            ReconnectIntervalSeconds = 5,
            EnableSyncDriftCorrection = true,
            MaxAcceptableDriftMs = 500,
            EnableSpatialAudio = true,
            EnableFrequencyFiltering = true,
            ConnectionTimeoutMs = 10000,
            ReceiveTimeoutMs = 5000
        };
    }
}
```

---

## Error Handling

### Exception Types
```csharp
public class TacviewException : Exception { }
public class TacviewConnectionException : TacviewException { }
public class TacviewProtocolException : TacviewException { }
public class TacviewTimeRangeException : TacviewException { }
```

### Error Recovery Strategies
| Error Type | Recovery Strategy |
|------------|-------------------|
| Connection refused | Retry with backoff |
| Connection dropped | Auto-reconnect |
| Invalid message | Log and ignore |
| Time out of range | Clamp to bounds, send error |
| Sync drift high | Immediate seek |

---

## Testing Strategy

### Unit Tests
- `TacviewProtocolTests`: JSON serialization
- `TimeConverterTests`: UTC conversions
- `SyncAlgorithmTests`: Drift correction logic
- `PilotMapperTests`: Pilot filtering

### Integration Tests
- `TacviewClientTests`: TCP communication (mock server)
- `TacviewSyncServiceTests`: End-to-end message flow
- `ReconnectionTests`: Connection recovery

### Manual Tests
- Real Tacview addon connection
- Long-duration sync stability (2+ hours)
- Multiple pilot selection scenarios
- Network interruption recovery

---

## NuGet Dependencies

```xml
<ItemGroup>
  <PackageReference Include="System.Text.Json" Version="9.0.0" />
  <PackageReference Include="NLog" Version="5.3.4" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\AeroDebrief.Core\AeroDebrief.Core.csproj" />
</ItemGroup>
```

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
