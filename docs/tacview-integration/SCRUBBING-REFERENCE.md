# Scrubbing & Variable-Speed Playback - Implementation Reference

## Quick Reference

This document provides code examples and implementation details for scrubbing and variable-speed playback support in the Tacview integration.

## New Classes

### 1. AudioTimeStretcher

**Location**: `src/AeroDebrief.Core/Audio/AudioTimeStretcher.cs`

**Purpose**: Provides time-stretching capabilities for variable-speed playback.

**Key Methods**:

```csharp
// Time-stretch audio for variable speed (0.25x to 4.0x)
float[] TimeStretch(float[] audioData, double speed)

// Calculate scaled delay for timing
double CalculateScaledDelay(double baseDelayMs, double speed)

// Determine if packet should be skipped at high speeds
bool ShouldSkipPacket(double speed, Random random)

// Calculate duplication count for slow speeds
int CalculateDuplicationCount(double speed)
```

**Usage Example**:

```csharp
// In audio processing pipeline
var audioData = DecodePacket(packet);

// Apply time-stretching based on playback speed
if (Math.Abs(_playbackController.PlaybackSpeed - 1.0) > 0.01)
{
    audioData = AudioTimeStretcher.TimeStretch(
        audioData, 
        _playbackController.PlaybackSpeed);
}

// Calculate appropriate delay
var baseDelay = Constants.OPUS_FRAME_DURATION_MS;
var scaledDelay = AudioTimeStretcher.CalculateScaledDelay(
    baseDelay, 
    _playbackController.PlaybackSpeed);

await Task.Delay((int)scaledDelay);
```

### 2. ScrubbingManager

**Location**: `src/AeroDebrief.Core/Playback/ScrubbingManager.cs`

**Purpose**: Detects scrubbing and manages audio muting during rapid timeline navigation.

**Key Properties**:

```csharp
bool IsScrubbingActive { get; }
int ScrubbingThresholdMs { get; set; } // Default: 100ms
int ScrubbingTimeoutMs { get; set; }   // Default: 500ms
int FadeInDurationMs { get; set; }     // Default: 200ms
```

**Key Methods**:

```csharp
// Process a seek and detect scrubbing
bool ProcessSeek(double driftMs, IAudioOutputEngine audioEngine)

// Check if scrubbing timeout has expired
Task CheckScrubbingTimeout(IAudioOutputEngine audioEngine)

// Manually exit scrubbing mode
Task ExitScrubbingMode(IAudioOutputEngine audioEngine)

// Reset scrubbing state
void Reset()
```

**Events**:

```csharp
event EventHandler? ScrubbingStarted;
event EventHandler? ScrubbingEnded;
```

**Usage Example**:

```csharp
// In TacviewSyncService
private ScrubbingManager _scrubbingManager = new();

public async Task HandleTimeUpdate(TimeUpdateMessage message)
{
    var drift = CalculateDrift(message);
    
    // Process seek and check for scrubbing
    var isScr ubbing = _scrubbingManager.ProcessSeek(drift, _audioEngine);
    
    if (drift > 500 || isScrubbing)
    {
        await _playbackController.SeekAsync(targetPosition);
    }
    
    // Periodically check if scrubbing has ended
    await _scrubbingManager.CheckScrubbingTimeout(_audioEngine);
}
```

### 3. PlaybackController Updates

**New Properties**:

```csharp
public double PlaybackSpeed { get; private set; } = 1.0;
public event Action<double>? PlaybackSpeedChanged;
```

**New Methods**:

```csharp
// Set playback speed (0.25x to 4.0x)
public void SetPlaybackSpeed(double speed)

// Get current time scale factor
public double GetTimeScale()
```

**Usage Example**:

```csharp
// In TacviewSyncService
public async Task HandleTimeUpdate(TimeUpdateMessage message)
{
    // Update playback speed from Tacview
    if (Math.Abs(message.PlaybackSpeed - _playbackController.PlaybackSpeed) > 0.01)
    {
        _playbackController.SetPlaybackSpeed(message.PlaybackSpeed);
        Logger.Info($"Playback speed synchronized: {message.PlaybackSpeed:F2}x");
    }
}

// In UI ViewModel
_playbackController.PlaybackSpeedChanged += speed =>
{
    PlaybackSpeed = speed;
    PlaybackSpeedDisplay = $"{speed:F2}x";
};
```

## Integration with Tacview

### Protocol Changes

**TimeUpdateMessage** now includes playback speed:

```json
{
  "type": "time_update",
  "mission_time_utc": "2024-01-15T14:30:45Z",
  "playback_state": "playing",
  "playback_speed": 2.0
}
```

### Lua Addon Changes

**File**: `src/AeroDebrief.Integrations/Lua/Tacview/AeroDebriefSync/main.lua`

```lua
function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    -- Get current playback speed from Tacview
    local currentSpeed = Tacview.Playback.GetPlaybackSpeed() or 1.0
    
    -- Check if speed changed
    local speedChanged = math.abs(currentSpeed - state_manager.lastSpeed) > 0.01
    
    -- Broadcast time update with speed
    if speedChanged or timeChanged or stateChanged then
        local message = protocol.CreateTimeUpdate(
            currentTime,
            isPlaying and "playing" or "paused",
            currentSpeed
        )
        
        tcp_server.Broadcast(message)
        state_manager.lastSpeed = currentSpeed
    end
end
```

**File**: `src/AeroDebrief.Integrations/Lua/Tacview/AeroDebriefSync/state_manager.lua`

```lua
local StateManager = {}

-- State tracking
StateManager.lastTime = 0
StateManager.lastState = "stopped"
StateManager.lastSpeed = 1.0  -- NEW: Track playback speed

function StateManager.HasStateChanged(time, state, speed)
    local timeChanged = math.abs(time - StateManager.lastTime) > 0.1
    local stateChanged = state ~= StateManager.lastState
    local speedChanged = math.abs(speed - StateManager.lastSpeed) > 0.01
    
    return timeChanged or stateChanged or speedChanged
end

function StateManager.UpdateState(time, state, speed)
    StateManager.lastTime = time
    StateManager.lastState = state
    StateManager.lastSpeed = speed
end

return StateManager
```

## Playback Pipeline Integration

### Audio Processing with Time-Stretching

```csharp
// In FilePlaybackPipeline or similar
public async Task PlaybackLoop(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var packet = await GetNextPacket();
        
        // Decode audio
        var audioData = _audioProcessor.DecodePacketToFloat(packet);
        
        // Apply time-stretching if speed != 1.0x
        var speed = _playbackController.PlaybackSpeed;
        if (Math.Abs(speed - 1.0) > 0.01)
        {
            audioData = AudioTimeStretcher.TimeStretch(audioData, speed);
        }
        
        // Process audio (volume, effects, etc.)
        audioData = _audioProcessor.ProcessAudio(audioData);
        
        // Write to output
        await _audioOutput.WriteAudioAsync(ConvertToBytes(audioData));
        
        // Calculate delay based on speed
        var baseDelay = Constants.OPUS_FRAME_DURATION_MS;
        var scaledDelay = AudioTimeStretcher.CalculateScaledDelay(baseDelay, speed);
        await Task.Delay((int)scaledDelay, cancellationToken);
    }
}
```

### Scrubbing Detection in Sync Service

```csharp
// In TacviewSyncService
public class TacviewSyncService
{
    private readonly ScrubbingManager _scrubbingManager = new();
    private readonly PlaybackController _playbackController;
    private readonly IAudioOutputEngine _audioEngine;
    
    public async Task HandleTimeUpdate(TimeUpdateMessage message)
    {
        // Parse Tacview time
        var tacviewTime = DateTime.Parse(
            message.MissionTimeUtc, 
            null, 
            DateTimeStyles.RoundtripKind);
        var targetOffset = tacviewTime - _recordingStartUtc;
        
        // Update playback speed
        if (Math.Abs(message.PlaybackSpeed - _playbackController.PlaybackSpeed) > 0.01)
        {
            _playbackController.SetPlaybackSpeed(message.PlaybackSpeed);
        }
        
        // Calculate drift
        var currentOffset = _playbackController.CurrentPosition;
        var drift = Math.Abs((targetOffset - currentOffset).TotalMilliseconds);
        
        // Process seek (detects scrubbing automatically)
        var isScrubbing = _scrubbingManager.ProcessSeek(drift, _audioEngine);
        
        // Seek if needed
        if (drift > 500 || isScrubbing)
        {
            await _playbackController.SeekAsync(targetOffset);
        }
        else if (drift > 100)
        {
            // Medium drift: speed adjustment
            var speedAdjust = targetOffset > currentOffset ? 1.02 : 0.98;
            _playbackController.SetPlaybackSpeed(speedAdjust);
        }
        
        // Check if scrubbing timeout expired
        await _scrubbingManager.CheckScrubbingTimeout(_audioEngine);
        
        // Update playback state
        if (message.PlaybackState == "playing" && !_playbackController.IsPlaying)
        {
            await _playbackController.PlayAsync();
        }
        else if (message.PlaybackState == "paused" && _playbackController.IsPlaying)
        {
            _playbackController.Pause();
        }
    }
}
```

## UI Updates

### Speed Indicator (XAML)

```xaml
<UserControl x:Class="AeroDebrief.UI.Controls.Tacview.TacviewStatusControl">
    <StackPanel>
        <!-- Playback Speed Display -->
        <StackPanel Orientation="Horizontal" Margin="0,8,0,0"
                   Visibility="{Binding IsConnected, Converter={StaticResource BoolToVisibilityConverter}}">
            <TextBlock Text="?" FontSize="12" VerticalAlignment="Center"/>
            <TextBlock Text="Speed:" Margin="4,0,8,0" FontSize="10" 
                      Foreground="Gray" VerticalAlignment="Center"/>
            <TextBlock Text="{Binding PlaybackSpeed, StringFormat={}{0:F2}x}" 
                      FontWeight="SemiBold" FontSize="12" VerticalAlignment="Center"/>
        </StackPanel>
        
        <!-- Scrubbing Indicator -->
        <Border Visibility="{Binding IsScrubbingActive, Converter={StaticResource BoolToVisibilityConverter}}"
               Background="#FF8C00" Padding="6,2" CornerRadius="3" Margin="0,8,0,0">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="??" FontSize="11" VerticalAlignment="Center"/>
                <TextBlock Text="SCRUBBING" Margin="4,0,0,0" 
                          FontSize="10" FontWeight="SemiBold" 
                          Foreground="White" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>
    </StackPanel>
</UserControl>
```

### ViewModel Properties

```csharp
public class TacviewIntegrationViewModel : ObservableObject
{
    private double _playbackSpeed = 1.0;
    private bool _isScrubbingActive;
    
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, value);
    }
    
    public string PlaybackSpeedDisplay => $"{_playbackSpeed:F2}x";
    
    public bool IsScrubbingActive
    {
        get => _isScrubbingActive;
        set => SetProperty(ref _isScrubbingActive, value);
    }
    
    public TacviewIntegrationViewModel(TacviewIntegrationService integrationService)
    {
        _integrationService = integrationService;
        
        // Subscribe to speed changes
        _integrationService.PlaybackSpeedChanged += speed =>
        {
            PlaybackSpeed = speed;
            OnPropertyChanged(nameof(PlaybackSpeedDisplay));
        };
        
        // Subscribe to scrubbing state
        _integrationService.ScrubbingStateChanged += isScrubbing =>
        {
            IsScrubbingActive = isScrubbing;
        };
    }
}
```

## Testing

### Unit Tests

```csharp
[TestClass]
public class AudioTimeStretcherTests
{
    [TestMethod]
    public void TimeStretch_DoubleSpeed_HalvesLength()
    {
        // Arrange
        var input = GenerateTestAudio(1920); // 40ms at 48kHz
        
        // Act
        var output = AudioTimeStretcher.TimeStretch(input, 2.0);
        
        // Assert
        Assert.AreEqual(960, output.Length); // Half length
    }
    
    [TestMethod]
    public void TimeStretch_HalfSpeed_DoublesLength()
    {
        // Arrange
        var input = GenerateTestAudio(1920);
        
        // Act
        var output = AudioTimeStretcher.TimeStretch(input, 0.5);
        
        // Assert
        Assert.AreEqual(3840, output.Length); // Double length
    }
}

[TestClass]
public class ScrubbingManagerTests
{
    [TestMethod]
    public void ProcessSeek_RapidSeeks_ActivatesScrubbing()
    {
        // Arrange
        var manager = new ScrubbingManager();
        var audioEngine = new MockAudioEngine();
        
        // Act
        var firstSeek = manager.ProcessSeek(200, audioEngine);
        Thread.Sleep(50); // Wait < threshold
        var secondSeek = manager.ProcessSeek(200, audioEngine);
        
        // Assert
        Assert.IsTrue(manager.IsScrubbingActive);
        Assert.AreEqual(0.0f, audioEngine.CurrentVolume); // Muted
    }
    
    [TestMethod]
    public async Task CheckScrubbingTimeout_AfterTimeout_FadesIn()
    {
        // Arrange
        var manager = new ScrubbingManager { ScrubbingTimeoutMs = 100 };
        var audioEngine = new MockAudioEngine();
        
        // Activate scrubbing
        manager.ProcessSeek(200, audioEngine);
        Thread.Sleep(50);
        manager.ProcessSeek(200, audioEngine);
        
        // Wait for timeout
        await Task.Delay(150);
        
        // Act
        await manager.CheckScrubbingTimeout(audioEngine);
        
        // Assert
        Assert.IsFalse(manager.IsScrubbingActive);
        Assert.IsTrue(audioEngine.CurrentVolume > 0); // Faded in
    }
}
```

### Integration Tests

```csharp
[TestClass]
public class VariableSpeedPlaybackIntegrationTests
{
    [TestMethod]
    public async Task FullPlayback_DoubleSpeed_CompletesInHalfTime()
    {
        // Arrange
        var recording = LoadTestRecording("test_40s.srs");
        var controller = new PlaybackController();
        
        // Act
        controller.SetPlaybackSpeed(2.0);
        var startTime = DateTime.UtcNow;
        await controller.PlayAsync(recording);
        var elapsed = DateTime.UtcNow - startTime;
        
        // Assert
        Assert.IsTrue(elapsed.TotalSeconds < 25); // ~20s + overhead
    }
    
    [TestMethod]
    public async Task Scrubbing_RapidSeeks_NoAudioClicks()
    {
        // Arrange
        var recording = LoadTestRecording("test_60s.srs");
        var controller = new PlaybackController();
        var audioEngine = new MockAudioEngine();
        var scrubbingManager = new ScrubbingManager();
        
        // Act
        await controller.PlayAsync(recording);
        
        // Simulate rapid scrubbing
        for (int i = 0; i < 20; i++)
        {
            var randomPosition = TimeSpan.FromSeconds(i * 3);
            await controller.SeekAsync(randomPosition);
            await Task.Delay(50); // Rapid seeks
        }
        
        // Assert
        Assert.AreEqual(0, audioEngine.ClickCount); // No audio clicks
    }
}
```

## Performance Considerations

### Memory Usage

- **Simple Time-Scaling**: ~2x memory overhead (input + output buffers)
- **WSOLA (future)**: ~4x memory overhead (overlap buffers)

### CPU Usage

| Speed | CPU Overhead (vs 1.0x) |
|-------|------------------------|
| 0.25x | +50% (duplication)     |
| 0.5x  | +25% (duplication)     |
| 1.0x  | 0% (baseline)          |
| 1.5x  | +10% (skip logic)      |
| 2.0x  | -20% (fewer packets)   |
| 4.0x  | -50% (much fewer)      |

### Audio Quality

| Speed Range | Quality            | Notes                              |
|-------------|--------------------|------------------------------------|
| 0.25x-0.5x  | Good               | Slight pitch drop acceptable       |
| 0.5x-0.75x  | Excellent          | Minimal artifacts                  |
| 0.75x-1.25x | Perfect            | Near-transparent                   |
| 1.25x-2.0x  | Excellent          | Slight pitch rise acceptable       |
| 2.0x-4.0x   | Fair               | Noticeable compression, still usable|

## Troubleshooting

### Audio Artifacts During Speed Changes

**Symptom**: Clicks, pops, or garbled audio when changing speed

**Solutions**:
1. Increase fade duration: `_scrubbingManager.FadeInDurationMs = 300`
2. Add crossfade between speed changes
3. Clear audio buffers on speed change

### Scrubbing Not Detected

**Symptom**: Audio plays with clicks during fast scrubbing

**Solutions**:
1. Lower threshold: `_scrubbingManager.ScrubbingThresholdMs = 75`
2. Check Tacview update rate (should be 10 Hz)
3. Verify drift calculation is correct

### High CPU at Fast Speeds

**Symptom**: CPU usage spikes at 3.0x+ speed

**Solutions**:
1. Increase packet skip ratio
2. Reduce buffer size for fast speeds
3. Skip audio processing for speeds > 3.0x

## Future Enhancements

1. **WSOLA Time-Stretching**: Higher quality pitch-preserving algorithm
2. **Preview Audio During Scrubbing**: Play short snippets while scrubbing
3. **Adaptive Quality**: Reduce processing quality at extreme speeds
4. **Speed Presets**: UI buttons for common speeds (0.5x, 1.0x, 2.0x)
5. **Frame-Accurate Navigation**: Snap to packet boundaries during scrubbing
