# Phase 4: Live Playback - Quick Reference

## ?? Quick Start for Developers

### How to Use Live Playback

```csharp
// In your ViewModel or Controller:

// 1. Get the LivePlaybackManager instance
var livePlaybackManager = new LivePlaybackManager(frequencyManager);

// 2. Subscribe to events
livePlaybackManager.FrequencyDetected += OnNewFrequency;
livePlaybackManager.PlayerDetected += OnNewPlayer;
livePlaybackManager.PacketsAvailable += OnNewPackets;
livePlaybackManager.DurationUpdated += OnDurationUpdate;

// 3. Start monitoring when recording begins
await livePlaybackManager.StartLivePlaybackAsync(tempDatabasePath);

// 4. Stop monitoring when recording ends
await livePlaybackManager.StopLivePlaybackAsync();
```

### Event Handling Examples

```csharp
private void OnNewFrequency(object? sender, FrequencyDetectedEventArgs e)
{
    // e.Frequency contains:
    // - Frequency (Hz)
    // - Modulation
    // - PacketCount
    // - FirstSeen, LastSeen
    // - PlayerCount
    
    Console.WriteLine($"New frequency: {e.Frequency.Frequency / 1_000_000.0:F3} MHz");
    
    // Add to UI
    AddFrequencyToUI(e.Frequency);
}

private void OnNewPlayer(object? sender, PlayerDetectedEventArgs e)
{
    // e.Player contains:
    // - PlayerName
    // - TransmitterGuid
    // - Coalition
    // - UnitType
    // - TransmissionCount
    // - Frequencies (list)
    
    Console.WriteLine($"New player: {e.Player.PlayerName}");
    
    // Update UI
    AddPlayerToUI(e.Player);
}

private void OnNewPackets(object? sender, LivePacketsEventArgs e)
{
    // e.NewPacketCount - number of packets since last update
    // e.CurrentDuration - current recording duration
    
    Console.WriteLine($"{e.NewPacketCount} new packets, duration: {e.CurrentDuration}");
    
    // Refresh waveform
    RefreshWaveform();
}

private void OnDurationUpdate(object? sender, TimeSpan duration)
{
    Console.WriteLine($"Duration: {duration}");
    
    // Update timeline
    UpdateTimeline(duration);
}
```

---

## ?? Key Classes

### LivePlaybackManager

**Location**: `src\AeroDebrief.UI\Services\LivePlaybackManager.cs`

```csharp
public sealed class LivePlaybackManager : IDisposable
{
    // Properties
    public bool IsLivePlaybackActive { get; }
    public TimeSpan CurrentDuration { get; }
    
    // Methods
    public async Task StartLivePlaybackAsync(string liveDatabasePath);
    public async Task StopLivePlaybackAsync();
    public DuckDBStore? GetLiveStore();
    
    // Events
    public event EventHandler<FrequencyDetectedEventArgs>? FrequencyDetected;
    public event EventHandler<PlayerDetectedEventArgs>? PlayerDetected;
    public event EventHandler<LivePacketsEventArgs>? PacketsAvailable;
    public event EventHandler<TimeSpan>? DurationUpdated;
}
```

### DuckDBStore (Live Methods)

**Location**: `src\AeroDebrief.Core\Storage\DuckDBStore.cs`

```csharp
// Get recording metadata
var metadata = await store.GetMetadataAsync();
// Returns: Version, ServerIp, ServerPort, StartTime

// Get current stats
var stats = await store.GetRecordingStatsAsync();
// Returns: TotalPackets, Duration, IsLive

// Get all frequencies
var frequencies = await store.GetUniqueFrequenciesAsync();
// Returns: List<FrequencyInfo>

// Get all players
var players = await store.GetUniquePlayersAsync();
// Returns: List<PlayerInfo>
```

---

## ?? Configuration

### Enable Live Playback

**File**: `configs/recorder.cfg`

```ini
[Recorder Settings]
EnableLivePlayback = true  # Enable real-time monitoring
```

### Adjust Polling Interval (Code)

**File**: `src\AeroDebrief.UI\Services\LivePlaybackManager.cs`

```csharp
// Default: 2 seconds
private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(2);

// To change: modify the field (requires recompile)
private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1); // Faster updates
```

---

## ?? UI Integration

### WPF Binding Example

```xml
<!-- Live Recording Indicator -->
<Border Background="Red" Visibility="{Binding IsLiveRecording, Converter={StaticResource BoolToVisibilityConverter}}">
    <TextBlock Text="?? LIVE RECORDING" Foreground="White"/>
</Border>

<!-- Live Duration -->
<TextBlock Text="{Binding TotalDuration, StringFormat='Duration: {0:hh\\:mm\\:ss}'}"
           Visibility="{Binding IsLiveRecording, Converter={StaticResource BoolToVisibilityConverter}}"/>

<!-- Status Message -->
<TextBlock Text="{Binding StatusMessage}"/>
```

### ViewModel Properties

```csharp
public class UnifiedPlayerViewModel : ViewModelBase
{
    // Phase 4: Live recording state
    public bool IsLiveRecording { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string StatusMessage { get; set; }
    
    // Methods
    public async Task StartLivePlaybackAsync(string dbPath);
    public async Task StopLivePlaybackAsync();
}
```

---

## ?? Testing

### Manual Test: New Frequency

```csharp
[Test]
public async Task TestLiveFrequencyDetection()
{
    // 1. Start recording
    var recorder = new AudioPacketRecorder();
    await recorder.ConnectAsync("127.0.0.1", 5002);
    await recorder.StartRecordingAsync();
    
    // 2. Start live playback
    var liveManager = new LivePlaybackManager(frequencyManager);
    bool frequencyDetected = false;
    
    liveManager.FrequencyDetected += (s, e) =>
    {
        frequencyDetected = true;
        Assert.AreEqual(251000000.0, e.Frequency.Frequency); // 251 MHz
    };
    
    await liveManager.StartLivePlaybackAsync(recorder.TempDatabasePath);
    
    // 3. Simulate transmission on 251 MHz
    // (connect SRS client and transmit)
    
    // 4. Wait for detection
    await Task.Delay(5000); // Wait 5 seconds
    
    Assert.IsTrue(frequencyDetected, "Frequency should be detected");
}
```

### Manual Test: New Player

```csharp
[Test]
public async Task TestLivePlayerDetection()
{
    // Similar to above, but check PlayerDetected event
    liveManager.PlayerDetected += (s, e) =>
    {
        Assert.AreEqual("Maverick", e.Player.PlayerName);
    };
}
```

---

## ?? Troubleshooting

### Live Playback Not Working

**Symptom**: No events fired during recording

**Possible Causes**:
1. `EnableLivePlayback` setting is `false`
   - **Fix**: Set to `true` in `recorder.cfg`

2. Database path is incorrect
   - **Fix**: Check `AudioPacketRecorder._tempDatabasePath`

3. Polling interval too slow
   - **Fix**: Reduce `_updateInterval` in `LivePlaybackManager`

4. Database locked
   - **Fix**: Ensure WAL mode is enabled (Phase 1)

### High CPU Usage

**Symptom**: CPU usage increases during live playback

**Possible Causes**:
1. Polling interval too fast
   - **Fix**: Increase to 3-5 seconds

2. Too many events firing
   - **Fix**: Debounce UI updates

3. Waveform redrawing too often
   - **Fix**: Batch waveform updates (every 5-10 seconds)

### Memory Leak

**Symptom**: Memory grows during recording

**Possible Causes**:
1. Event handlers not unsubscribed
   - **Fix**: Call `Dispose()` on `LivePlaybackManager`

2. `_knownFrequencies` / `_knownPlayers` growing indefinitely
   - **Fix**: Clear on stop

---

## ?? API Reference

### FrequencyDetectedEventArgs

```csharp
public class FrequencyDetectedEventArgs : EventArgs
{
    public FrequencyInfo Frequency { get; }
}
```

### PlayerDetectedEventArgs

```csharp
public class PlayerDetectedEventArgs : EventArgs
{
    public PlayerInfo Player { get; }
}
```

### LivePacketsEventArgs

```csharp
public class LivePacketsEventArgs : EventArgs
{
    public long NewPacketCount { get; }
    public TimeSpan CurrentDuration { get; }
}
```

### RecordingStats

```csharp
public class RecordingStats
{
    public long TotalPackets { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsLive { get; set; }
}
```

---

## ?? Best Practices

### 1. Always Dispose

```csharp
using var liveManager = new LivePlaybackManager(frequencyManager);
// ... use liveManager ...
// Automatically disposed at end of scope
```

### 2. Use Dispatcher for UI Updates

```csharp
liveManager.FrequencyDetected += async (s, e) =>
{
    await Application.Current.Dispatcher.InvokeAsync(() =>
    {
        // Update UI here
        FrequencyList.Add(e.Frequency);
    });
};
```

### 3. Debounce Frequent Updates

```csharp
private DateTime _lastWaveformUpdate = DateTime.MinValue;

liveManager.PacketsAvailable += (s, e) =>
{
    var now = DateTime.UtcNow;
    if ((now - _lastWaveformUpdate).TotalSeconds < 5)
        return; // Skip update
    
    _lastWaveformUpdate = now;
    RefreshWaveform();
};
```

### 4. Handle Errors Gracefully

```csharp
try
{
    await liveManager.StartLivePlaybackAsync(dbPath);
}
catch (Exception ex)
{
    Logger.Error(ex, "Live playback failed");
    StatusMessage = "Live playback unavailable";
    // Continue without live playback
}
```

---

## ?? Related Documentation

- [Phase 4 Complete Documentation](DuckDB-Phase4-Complete.md)
- [DuckDB Implementation Roadmap](DuckDB-Implementation-Complete-Plan.md)
- [Quick Reference](DuckDB-Quick-Reference.md)

---

**Last Updated**: 2025-01-18  
**Phase**: 4  
**Status**: ? Complete
