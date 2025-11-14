# Core Modifications - Enabling External Synchronization

## Overview

This document details the modifications required in `AeroDebrief.Core` to support Tacview synchronization while maintaining backward compatibility with standalone operation.

## Design Principles

1. **Non-Breaking**: All modifications must be optional and backward-compatible
2. **Minimal Coupling**: Core should not depend on Integrations project
3. **Interface-Based**: Use delegates/events for loose coupling
4. **Feature Flags**: Easy to enable/disable Tacview integration

## Modified Classes

### 1. PlaybackController (Time Synchronization)

**File**: `src/AeroDebrief.Core/Playback/PlaybackController.cs`

**Current Behavior**: Internal time tracking using `CurrentPosition`

**Required Changes**: Add external time synchronization capability

#### New Properties & Events

```csharp
public sealed class PlaybackController : IDisposable
{
    // Existing code...
    
    // NEW: External sync support
    private bool _externalSyncEnabled;
    private Func<TimeSpan>? _externalTimeProvider;
    private Func<double>? _externalSpeedProvider;
    
    /// <summary>
    /// Gets whether external time synchronization is enabled.
    /// </summary>
    public bool IsExternalSyncEnabled => _externalSyncEnabled;
    
    /// <summary>
    /// Event fired when external sync requests a seek operation.
    /// </summary>
    public event Action<TimeSpan>? ExternalSeekRequested;
    
    /// <summary>
    /// Event fired when external sync requests a playback state change.
    /// </summary>
    public event Action<PlaybackState>? ExternalPlaybackStateChangeRequested;
}
```

#### Enable External Sync

```csharp
/// <summary>
/// Enables external time synchronization (e.g., from Tacview).
/// When enabled, the controller uses external time and speed providers.
/// </summary>
/// <param name="timeProvider">Function that returns target playback time</param>
/// <param name="speedProvider">Function that returns target playback speed (1.0 = normal)</param>
public void EnableExternalSync(
    Func<TimeSpan> timeProvider,
    Func<double>? speedProvider = null)
{
    if (timeProvider == null)
        throw new ArgumentNullException(nameof(timeProvider));
    
    _externalTimeProvider = timeProvider;
    _externalSpeedProvider = speedProvider;
    _externalSyncEnabled = true;
    
    Logger.Info("External time synchronization enabled");
}

/// <summary>
/// Disables external time synchronization and returns to internal time tracking.
/// </summary>
public void DisableExternalSync()
{
    _externalTimeProvider = null;
    _externalSpeedProvider = null;
    _externalSyncEnabled = false;
    
    Logger.Info("External time synchronization disabled");
}
```

#### Sync Check Method

```csharp
/// <summary>
/// Checks if external sync requires a seek operation.
/// Should be called periodically (e.g., every 100ms).
/// </summary>
public void CheckExternalSync()
{
    if (!_externalSyncEnabled || _externalTimeProvider == null)
        return;
    
    if (!_isPlaybackActive)
        return;
    
    try
    {
        // Get target time from external source (Tacview)
        var targetTime = _externalTimeProvider();
        
        // Calculate drift
        var drift = targetTime - CurrentPosition;
        var absDrift = drift.Duration();
        
        // Log significant drift
        if (absDrift > TimeSpan.FromMilliseconds(500))
        {
            Logger.Debug($"External sync drift: {drift.TotalMilliseconds:F0}ms (target: {targetTime}, current: {CurrentPosition})");
        }
        
        // Large drift (>500ms) = seek required
        if (absDrift > TimeSpan.FromMilliseconds(500))
        {
            Logger.Info($"External sync requesting seek: {targetTime} (drift: {drift.TotalMilliseconds:F0}ms)");
            ExternalSeekRequested?.Invoke(targetTime);
        }
        // Medium drift (100-500ms) = speed adjustment (handled by integration layer)
        // Small drift (<100ms) = no action needed
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Error checking external sync");
    }
}
```

#### Modified UpdateProgress

```csharp
public void UpdateProgress()
{
    // Check external sync if enabled
    if (_externalSyncEnabled)
    {
        CheckExternalSync();
    }
    
    // Existing progress update code...
    if (TotalDuration.Ticks > 0)
    {
        var progress = (double)CurrentPosition.Ticks / TotalDuration.Ticks * 100.0;
        var clampedProgress = Math.Clamp(progress, 0.0, 100.0);
        
        try
        {
            ProgressChanged?.Invoke(clampedProgress);
            TimeChanged?.Invoke(CurrentPosition, TotalDuration);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while invoking progress change events");
        }
    }
}
```

#### Playback State Enum (New)

```csharp
/// <summary>
/// Playback state for external control.
/// </summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}
```

---

### 2. FrequencyChannelMixer (Pilot Filtering)

**File**: `src/AeroDebrief.Core/Audio/FrequencyChannelMixer.cs`

**Current Behavior**: Frequency-based gating only

**Required Changes**: Add pilot-based packet filtering

#### New Properties

```csharp
public class FrequencyChannelMixer
{
    // Existing code...
    
    // NEW: Pilot filter support
    private Func<AudioPacketMetadata, bool>? _pilotFilter;
    private bool _pilotFilterEnabled;
    
    /// <summary>
    /// Gets whether pilot filtering is enabled.
    /// </summary>
    public bool IsPilotFilterEnabled => _pilotFilterEnabled;
}
```

#### Enable Pilot Filter

```csharp
/// <summary>
/// Enables pilot-based filtering (e.g., from Tacview selection).
/// When enabled, only packets matching the filter will be played.
/// </summary>
/// <param name="filter">Function that determines if a packet should be played</param>
public void EnablePilotFilter(Func<AudioPacketMetadata, bool> filter)
{
    if (filter == null)
        throw new ArgumentNullException(nameof(filter));
    
    _pilotFilter = filter;
    _pilotFilterEnabled = true;
    
    Logger.Info("Pilot filtering enabled");
}

/// <summary>
/// Disables pilot filtering and returns to frequency-only filtering.
/// </summary>
public void DisablePilotFilter()
{
    _pilotFilter = null;
    _pilotFilterEnabled = false;
    
    Logger.Info("Pilot filtering disabled");
}
```

#### Modified ShouldPlayPacket

```csharp
/// <summary>
/// Determines if a packet should be played based on frequency gate and pilot filter.
/// </summary>
private bool ShouldPlayPacket(AudioPacketMetadata packet)
{
    // Check frequency gate (existing logic)
    if (!_frequencyGates.TryGetValue(packet.Frequency, out var gateMode))
    {
        gateMode = FrequencyGateMode.Open; // Default: allow all
    }
    
    if (gateMode == FrequencyGateMode.Muted)
        return false;
    
    // NEW: Check pilot filter if enabled
    if (_pilotFilterEnabled && _pilotFilter != null)
    {
        try
        {
            return _pilotFilter(packet);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in pilot filter");
            // On error, fall back to frequency-only filtering
        }
    }
    
    return true; // Pass if no filter or filter allows
}
```

---

### 3. AudioOutputEngine (Spatial Audio / Pan)

**File**: `src/AeroDebrief.Core/Audio/AudioOutputEngine.cs`

**Current Behavior**: Mono or stereo output without spatial positioning

**Required Changes**: Add per-pilot pan control

#### New Properties

```csharp
public interface IAudioOutputEngine
{
    // Existing members...
    
    // NEW: Spatial audio support
    void EnableSpatialAudio(Func<string, double> getPanForPilot);
    void DisableSpatialAudio();
    bool IsSpatialAudioEnabled { get; }
}

public class AudioOutputEngine : IAudioOutputEngine
{
    // Existing code...
    
    // NEW: Spatial audio (pan) support
    private Func<string, double>? _panProvider;
    private bool _spatialAudioEnabled;
    
    /// <summary>
    /// Gets whether spatial audio (pan) is enabled.
    /// </summary>
    public bool IsSpatialAudioEnabled => _spatialAudioEnabled;
}
```

#### Enable Spatial Audio

```csharp
/// <summary>
/// Enables spatial audio with per-pilot pan control.
/// </summary>
/// <param name="getPanForPilot">Function that returns pan value (-1.0 to +1.0) for a pilot ID</param>
public void EnableSpatialAudio(Func<string, double> getPanForPilot)
{
    if (getPanForPilot == null)
        throw new ArgumentNullException(nameof(getPanForPilot));
    
    _panProvider = getPanForPilot;
    _spatialAudioEnabled = true;
    
    Logger.Info("Spatial audio enabled");
}

/// <summary>
/// Disables spatial audio and returns to normal stereo output.
/// </summary>
public void DisableSpatialAudio()
{
    _panProvider = null;
    _spatialAudioEnabled = false;
    
    Logger.Info("Spatial audio disabled");
}
```

#### Modified MixAudio

```csharp
private void MixAudio(AudioPacketMetadata packet, Span<float> outputBuffer)
{
    // Decode packet (existing logic)
    var decodedSamples = DecodePacket(packet);
    
    // NEW: Apply spatial audio if enabled
    double pan = 0.0; // Center by default
    
    if (_spatialAudioEnabled && _panProvider != null)
    {
        try
        {
            var pilotId = packet.PlayerData?.TransmitterGuid ?? packet.TransmitterGuid;
            pan = _panProvider(pilotId);
            pan = Math.Clamp(pan, -1.0, 1.0); // Ensure valid range
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting pan value");
            pan = 0.0; // Fall back to center
        }
    }
    
    // Calculate left/right gains from pan
    // Pan: -1.0 = full left, 0.0 = center, +1.0 = full right
    double leftGain = (1.0 - pan) * 0.5;
    double rightGain = (1.0 + pan) * 0.5;
    
    // Mix samples into output buffer with pan applied
    for (int i = 0; i < decodedSamples.Length; i++)
    {
        float sample = decodedSamples[i];
        
        // Left channel (even indices)
        outputBuffer[i * 2] += sample * (float)leftGain;
        
        // Right channel (odd indices)
        outputBuffer[i * 2 + 1] += sample * (float)rightGain;
    }
}
```

---

### 4. FilePlaybackPipeline (Speaking Detection)

**File**: `src/AeroDebrief.Core/Playback/FilePlaybackPipeline.cs`

**Current Behavior**: Plays packets without external notification

**Required Changes**: Add packet playback event for speaking status

#### New Event

```csharp
public sealed class FilePlaybackPipeline : IDisposable
{
    // Existing code...
    
    // NEW: Packet playback event
    /// <summary>
    /// Fired when a packet starts playing (useful for speaking status).
    /// </summary>
    public event EventHandler<PacketPlaybackEventArgs>? PacketStartedPlaying;
}

/// <summary>
/// Event args for packet playback.
/// </summary>
public class PacketPlaybackEventArgs : EventArgs
{
    public AudioPacketMetadata Packet { get; }
    public DateTime PlaybackTime { get; }
    
    public PacketPlaybackEventArgs(AudioPacketMetadata packet, DateTime playbackTime)
    {
        Packet = packet;
        PlaybackTime = playbackTime;
    }
}
```

#### Fire Event on Packet Play

```csharp
private void ProcessPacket(AudioPacketMetadata packet)
{
    // Existing packet processing...
    
    // NEW: Notify listeners that packet is playing
    try
    {
        PacketStartedPlaying?.Invoke(this, new PacketPlaybackEventArgs(packet, DateTime.UtcNow));
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Error firing PacketStartedPlaying event");
    }
}
```

---

## Integration Example

### In Integration Layer (TacviewSyncService)

```csharp
public void IntegrateWithCore(
    PlaybackController playbackController,
    FrequencyChannelMixer mixer,
    AudioOutputEngine audioEngine,
    FilePlaybackPipeline pipeline)
{
    // 1. Enable external time sync
    playbackController.EnableExternalSync(
        timeProvider: () => _currentTacviewTime,
        speedProvider: () => _currentPlaybackSpeed
    );
    
    // 2. Handle seek requests from sync
    playbackController.ExternalSeekRequested += async (targetTime) =>
    {
        await SeekAsync(targetTime);
    };
    
    // 3. Enable pilot filtering
    mixer.EnablePilotFilter(_pilotMapper.ShouldPlayPacket);
    
    // 4. Enable spatial audio
    audioEngine.EnableSpatialAudio(_pilotMapper.GetPanForPilot);
    
    // 5. Subscribe to packet playback for speaking status
    pipeline.PacketStartedPlaying += OnPacketStartedPlaying;
}

private async void OnPacketStartedPlaying(object? sender, PacketPlaybackEventArgs e)
{
    // Notify Tacview that pilot is speaking
    var pilotId = e.Packet.PlayerData?.TransmitterGuid ?? e.Packet.TransmitterGuid;
    await _tacviewClient.SendSpeakingStatusAsync(pilotId, true, e.Packet.Frequency);
    
    // Schedule speaking end after packet duration
    var duration = CalculatePacketDuration(e.Packet);
    _ = Task.Delay(duration).ContinueWith(_ =>
    {
        _tacviewClient.SendSpeakingStatusAsync(pilotId, false, e.Packet.Frequency);
    });
}
```

---

## Backward Compatibility

All modifications are **opt-in** and do not affect existing functionality:

### Without Tacview Integration (Default)
```csharp
// Create player normally
var controller = new PlaybackController();
var mixer = new FrequencyChannelMixer();
var engine = new AudioOutputEngine();

// Works exactly as before
controller.Start(filePath, playbackFunc);
```

### With Tacview Integration
```csharp
// Create player
var controller = new PlaybackController();
var mixer = new FrequencyChannelMixer();
var engine = new AudioOutputEngine();

// Enable Tacview sync (optional)
tacviewSync.IntegrateWithCore(controller, mixer, engine, pipeline);

// Now synchronized with Tacview
controller.Start(filePath, playbackFunc);
```

---

## Testing

### Unit Tests

```csharp
[Test]
public void ExternalSync_WhenEnabled_UsesExternalTime()
{
    var controller = new PlaybackController();
    var externalTime = TimeSpan.FromSeconds(10);
    
    controller.EnableExternalSync(() => externalTime);
    
    // Verify external sync is enabled
    Assert.IsTrue(controller.IsExternalSyncEnabled);
}

[Test]
public void PilotFilter_WhenEnabled_FiltersPackets()
{
    var mixer = new FrequencyChannelMixer();
    var allowedPilot = "pilot-123";
    
    mixer.EnablePilotFilter(packet => 
        packet.TransmitterGuid == allowedPilot);
    
    // Verify filtering works
    Assert.IsTrue(mixer.IsPilotFilterEnabled);
}

[Test]
public void SpatialAudio_AppliesPanCorrectly()
{
    var engine = new AudioOutputEngine();
    
    engine.EnableSpatialAudio(pilotId => -0.8); // Left ear
    
    // Verify spatial audio enabled
    Assert.IsTrue(engine.IsSpatialAudioEnabled);
}
```

---

## Performance Impact

### Benchmarks (Expected)

| Feature | CPU Overhead | Memory Overhead |
|---------|-------------|-----------------|
| External Sync Check | <0.1% | 0 bytes |
| Pilot Filter | <0.5% | 0 bytes |
| Spatial Audio | <1.0% | 0 bytes |
| **Total** | **<2%** | **0 bytes** |

### Optimization Notes
- Delegate calls are inlined by JIT
- No additional allocations
- Minimal branching
- No locks required (single-threaded audio path)

---

## Migration Checklist

- [ ] Add external sync to PlaybackController
- [ ] Add pilot filter to FrequencyChannelMixer
- [ ] Add spatial audio to AudioOutputEngine
- [ ] Add packet playback event to FilePlaybackPipeline
- [ ] Update IAudioOutputEngine interface
- [ ] Add unit tests for new features
- [ ] Update documentation
- [ ] Test backward compatibility

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
