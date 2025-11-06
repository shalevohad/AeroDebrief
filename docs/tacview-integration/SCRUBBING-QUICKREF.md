# Scrubbing & Variable-Speed - Quick Reference Card

## ?? Quick Start

### Enable Variable-Speed Playback

```csharp
// Set playback speed (0.25x to 4.0x)
playbackController.SetPlaybackSpeed(2.0); // Double speed

// Subscribe to speed changes
playbackController.PlaybackSpeedChanged += speed =>
{
    Console.WriteLine($"Speed changed to {speed:F2}x");
};
```

### Enable Scrubbing Detection

```csharp
// Create scrubbing manager
var scrubbingManager = new ScrubbingManager
{
    ScrubbingThresholdMs = 100,  // Seeks faster than this = scrubbing
    ScrubbingTimeoutMs = 500,    // Wait before exiting scrubbing mode
    FadeInDurationMs = 200       // Audio fade-in duration
};

// Subscribe to scrubbing events
scrubbingManager.ScrubbingStarted += (s, e) =>
{
    Console.WriteLine("Scrubbing started - audio muted");
};

scrubbingManager.ScrubbingEnded += (s, e) =>
{
    Console.WriteLine("Scrubbing ended - audio fading in");
};

// In seek handler
var isScrubbing = scrubbingManager.ProcessSeek(driftMs, audioEngine);
if (isScrubbing)
{
    // Audio is automatically muted
    await playbackController.SeekAsync(targetPosition);
}

// In update loop
await scrubbingManager.CheckScrubbingTimeout(audioEngine);
```

### Apply Time-Stretching to Audio

```csharp
// Decode audio packet
var audioData = audioProcessor.DecodePacketToFloat(packet);

// Apply time-stretching
var speed = playbackController.PlaybackSpeed;
if (Math.Abs(speed - 1.0) > 0.01)
{
    audioData = AudioTimeStretcher.TimeStretch(audioData, speed);
}

// Calculate scaled delay
var baseDelay = 40; // milliseconds
var scaledDelay = AudioTimeStretcher.CalculateScaledDelay(baseDelay, speed);
await Task.Delay((int)scaledDelay);
```

---

## ?? API Reference

### PlaybackController

```csharp
// Properties
double PlaybackSpeed { get; } // Current speed (0.25x - 4.0x)

// Methods
void SetPlaybackSpeed(double speed)  // Set speed (clamped to 0.25-4.0)
double GetTimeScale()                // Get current time scaling factor

// Events
event Action<double> PlaybackSpeedChanged;
```

### AudioTimeStretcher

```csharp
// Time-stretch audio samples
float[] TimeStretch(float[] audioData, double speed)

// Calculate scaled timing
double CalculateScaledDelay(double baseDelayMs, double speed)

// Packet skipping strategy (for high speeds)
bool ShouldSkipPacket(double speed, Random random)

// Duplication count (for low speeds)
int CalculateDuplicationCount(double speed)
```

### ScrubbingManager

```csharp
// Properties
bool IsScrubbingActive { get; }
int ScrubbingThresholdMs { get; set; } // Default: 100
int ScrubbingTimeoutMs { get; set; }   // Default: 500
int FadeInDurationMs { get; set; }     // Default: 200

// Methods
bool ProcessSeek(double driftMs, IAudioOutputEngine audioEngine)
Task CheckScrubbingTimeout(IAudioOutputEngine audioEngine)
Task ExitScrubbingMode(IAudioOutputEngine audioEngine)
void Reset()

// Events
event EventHandler ScrubbingStarted;
event EventHandler ScrubbingEnded;
```

---

## ?? Common Use Cases

### Use Case 1: Integrate with Tacview Sync

```csharp
public class TacviewSyncService
{
    private ScrubbingManager _scrubbingManager = new();
    
    public async Task HandleTimeUpdate(TimeUpdateMessage msg)
    {
        // Update speed
        if (Math.Abs(msg.PlaybackSpeed - _controller.PlaybackSpeed) > 0.01)
        {
            _controller.SetPlaybackSpeed(msg.PlaybackSpeed);
        }
        
        // Detect scrubbing
        var drift = CalculateDrift(msg);
        var isScrubbing = _scrubbingManager.ProcessSeek(drift, _audioEngine);
        
        // Seek
        if (drift > 500 || isScrubbing)
        {
            await _controller.SeekAsync(targetPosition);
        }
        
        // Check timeout
        await _scrubbingManager.CheckScrubbingTimeout(_audioEngine);
    }
}
```

### Use Case 2: Add Speed Control to UI

```csharp
// ViewModel
public class PlayerViewModel : ObservableObject
{
    private double _playbackSpeed = 1.0;
    
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (SetProperty(ref _playbackSpeed, value))
            {
                _controller.SetPlaybackSpeed(value);
            }
        }
    }
    
    public ICommand SetSpeedCommand { get; }
    
    public PlayerViewModel()
    {
        SetSpeedCommand = new RelayCommand<double>(
            speed => PlaybackSpeed = speed);
        
        _controller.PlaybackSpeedChanged += speed =>
        {
            PlaybackSpeed = speed;
        };
    }
}
```

```xaml
<!-- XAML -->
<StackPanel Orientation="Horizontal">
    <Button Content="0.5x" Command="{Binding SetSpeedCommand}" CommandParameter="0.5"/>
    <Button Content="1.0x" Command="{Binding SetSpeedCommand}" CommandParameter="1.0"/>
    <Button Content="1.5x" Command="{Binding SetSpeedCommand}" CommandParameter="1.5"/>
    <Button Content="2.0x" Command="{Binding SetSpeedCommand}" CommandParameter="2.0"/>
    
    <TextBlock Text="{Binding PlaybackSpeed, StringFormat={}{0:F2}x}" Margin="8,0,0,0"/>
</StackPanel>
```

### Use Case 3: Custom Audio Playback Loop

```csharp
public async Task CustomPlaybackLoop(CancellationToken ct)
{
    var random = new Random();
    
    while (!ct.IsCancellationRequested)
    {
        var packet = await GetNextPacket(ct);
        
        // Check if we should skip (high speeds)
        var speed = _controller.PlaybackSpeed;
        if (AudioTimeStretcher.ShouldSkipPacket(speed, random))
        {
            continue; // Skip this packet
        }
        
        // Decode
        var audioData = DecodePacket(packet);
        
        // Time-stretch
        if (Math.Abs(speed - 1.0) > 0.01)
        {
            audioData = AudioTimeStretcher.TimeStretch(audioData, speed);
        }
        
        // Output
        await _audioEngine.WriteAudioAsync(audioData);
        
        // Scaled delay
        var delay = AudioTimeStretcher.CalculateScaledDelay(
            Constants.OPUS_FRAME_DURATION_MS, speed);
        await Task.Delay((int)delay, ct);
    }
}
```

---

## ? Performance Tips

### Optimize for High Speeds

```csharp
// Skip unnecessary processing at very high speeds
if (speed > 3.0)
{
    // Skip effects processing
    // Skip spectrum analysis
    // Use simpler time-stretching
}

// Reduce buffer size for fast playback
if (speed > 1.5)
{
    audioEngine.AdjustBufferForSpeed(speed); // Smaller buffer = less latency
}
```

### Optimize for Low Speeds

```csharp
// Increase buffer size for slow playback
if (speed < 0.75)
{
    audioEngine.AdjustBufferForSpeed(speed); // Larger buffer = more stability
}

// Pre-calculate duplication count
var dupCount = AudioTimeStretcher.CalculateDuplicationCount(speed);
for (int i = 0; i < dupCount; i++)
{
    await audioEngine.WriteAudioAsync(audioData);
}
```

---

## ?? Debugging Tips

### Enable Verbose Logging

```csharp
// In NLog.config
<logger name="AeroDebrief.Core.Playback.PlaybackController" minlevel="Trace"/>
<logger name="AeroDebrief.Core.Audio.AudioTimeStretcher" minlevel="Trace"/>
<logger name="AeroDebrief.Core.Playback.ScrubbingManager" minlevel="Trace"/>
```

### Monitor Scrubbing State

```csharp
// Add debug output
scrubbingManager.ScrubbingStarted += (s, e) =>
{
    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Scrubbing STARTED");
};

scrubbingManager.ScrubbingEnded += (s, e) =>
{
    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Scrubbing ENDED");
};
```

### Track Speed Changes

```csharp
playbackController.PlaybackSpeedChanged += speed =>
{
    Debug.WriteLine($"Speed: {speed:F2}x, Audio Delay: {AudioTimeStretcher.CalculateScaledDelay(40, speed):F1}ms");
};
```

---

## ?? Recommended Settings

### For Smooth Timeline Scrubbing

```csharp
var scrubbingManager = new ScrubbingManager
{
    ScrubbingThresholdMs = 100,   // Default is good
    ScrubbingTimeoutMs = 500,     // Longer = more stable
    FadeInDurationMs = 200        // Longer = smoother
};
```

### For Responsive Speed Changes

```csharp
// Update speed immediately on Tacview message
if (Math.Abs(message.PlaybackSpeed - _controller.PlaybackSpeed) > 0.01)
{
    _controller.SetPlaybackSpeed(message.PlaybackSpeed);
    // No delay - instant response
}
```

### For Best Audio Quality

```csharp
// Limit speed range for better quality
var clampedSpeed = Math.Clamp(speed, 0.5, 2.0);
_controller.SetPlaybackSpeed(clampedSpeed);

// Use higher quality time-stretching (future: WSOLA)
// For now, simple time-scaling works well in 0.5x-2.0x range
```

---

## ?? Configuration

### appsettings.json

```json
{
  "Tacview": {
    "Scrubbing": {
      "DetectionThresholdMs": 100,
      "TimeoutMs": 500,
      "FadeInDurationMs": 200
    },
    "Playback": {
      "MinSpeed": 0.25,
      "MaxSpeed": 4.0,
      "DefaultSpeed": 1.0,
      "EnableTimeStretching": true
    }
  }
}
```

### Load Configuration

```csharp
var scrubbingConfig = configuration.GetSection("Tacview:Scrubbing");
var scrubbingManager = new ScrubbingManager
{
    ScrubbingThresholdMs = scrubbingConfig.GetValue("DetectionThresholdMs", 100),
    ScrubbingTimeoutMs = scrubbingConfig.GetValue("TimeoutMs", 500),
    FadeInDurationMs = scrubbingConfig.GetValue("FadeInDurationMs", 200)
};
```

---

## ?? Related Documentation

- **Full Implementation**: `IMPLEMENTATION-GUIDE.md` ? Section 10
- **Detailed Reference**: `SCRUBBING-REFERENCE.md`
- **Summary**: `SCRUBBING-SUMMARY.md`
- **Protocol Spec**: `02-PROTOCOL-SPECIFICATION.md` (TimeUpdateMessage)
- **Core API**: `PlaybackController.cs`, `AudioTimeStretcher.cs`, `ScrubbingManager.cs`

---

## ?? Learning Resources

### Understanding Time-Stretching

- Simple Time-Scaling: Changes tempo AND pitch (what we use)
- WSOLA: Changes tempo, preserves pitch (future enhancement)
- Speed ranges:
  - 0.25x-0.5x: Slow-motion (slight pitch drop acceptable)
  - 0.5x-2.0x: Sweet spot (minimal artifacts)
  - 2.0x-4.0x: Fast-forward (some quality loss acceptable)

### Understanding Scrubbing

- **Detection**: Rapid seeks < 100ms apart
- **Behavior**: Mute audio to prevent clicks
- **Timeout**: Wait 500ms after last seek
- **Fade-In**: Smooth transition back to normal audio

### Protocol Integration

- Tacview sends `time_update` with `playback_speed`
- AeroDebrief applies speed via `SetPlaybackSpeed()`
- Time-stretching applied per audio packet
- Scrubbing detected automatically via drift + timing

---

## ? Checklist for New Integration

- [ ] Create `ScrubbingManager` instance
- [ ] Subscribe to `ScrubbingStarted` / `ScrubbingEnded`
- [ ] Call `ProcessSeek()` in seek handler
- [ ] Call `CheckScrubbingTimeout()` in update loop
- [ ] Subscribe to `PlaybackSpeedChanged`
- [ ] Apply `AudioTimeStretcher.TimeStretch()` in audio loop
- [ ] Update UI to show speed indicator
- [ ] Update UI to show scrubbing indicator
- [ ] Test with Tacview integration
- [ ] Verify no audio clicks during scrubbing

---

**Last Updated**: 2024-01-17  
**Version**: 1.0
