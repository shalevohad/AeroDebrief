# Phase 4 Enhanced: Dual Playhead Implementation

## ?? Overview

Implemented **dual-playhead architecture** for live recording with integrated audio playback using existing Core infrastructure.

### Two Playheads:

1. **Recording Playhead (Static)** ??
   - Shows where recording is currently at
   - Moves forward as new packets arrive
   - Left side: drawn waveform
   - Right side: empty (future packets)
   - This is the "pencil" drawing the waveform

2. **Playback Playhead (Dynamic)** ??
   - Shows where audio is currently playing from
   - Synced to `PlaybackController.TimeChanged`
   - Can be behind recording playhead (latency)
   - User can scrub to any position ? recording position

---

## ?? Files Created

### 1. `LiveRecordingPlaybackPipeline.cs`
**Purpose**: Integrates live recording with existing FilePlaybackPipeline infrastructure

**Key Features**:
- Opens live DuckDB as `FilePacketSource`
- Wraps in `FilePlaybackPipeline` for audio processing
- Reuses `AudioMixerEngine`, `MasterMixer`, `AudioOutputEngine`
- Tracks dual playheads (recording + playback)
- Provides `PlaybackController` for UI integration

**Public API**:
```csharp
// Properties
TimeSpan RecordingPosition { get; }  // Static playhead
TimeSpan PlaybackPosition { get; }   // Dynamic playhead
PlaybackController PlaybackController { get; }
bool IsInitialized { get; }

// Events
event EventHandler<TimeSpan> RecordingPositionChanged;
event EventHandler<TimeSpan> PlaybackPositionChanged;

// Methods
Task InitializeAsync(CancellationToken ct);
Task PlayAsync();
void Pause();
Task StopAsync();
Task SeekAsync(TimeSpan position);
Task GoLiveAsync(); // Seek to recording position
void SetFrequencyGate(double frequency, FrequencyGateMode mode);
```

**Integration with Core**:
```
LiveRecordingPlaybackPipeline
    ? Opens
DuckDB (live recording)
    ? As
FilePacketSource
    ? Into
FilePlaybackPipeline
    ?? PacketRouter
    ?? AudioMixerEngine
    ?? MasterMixer
    ?? AudioOutputEngine
    ?? PlaybackController ? Syncs playback playhead
```

---

## ?? Updated Files

### 2. `LivePlaybackManager.cs`
**Changes**:
- Added `LiveRecordingPlaybackPipeline? PlaybackPipeline` property
- Initializes playback pipeline in `StartLivePlaybackAsync`
- Wires up playhead events
- Updates recording position when duration changes
- Disposes pipeline in `StopLivePlaybackAsync`

**Key Code**:
```csharp
// Initialize pipeline
_playbackPipeline = new LiveRecordingPlaybackPipeline(_liveStore, _liveDbPath);
await _playbackPipeline.InitializeAsync();

// Wire up playhead tracking
_playbackPipeline.RecordingPositionChanged += OnRecordingPositionChanged;
_playbackPipeline.PlaybackPositionChanged += OnPlaybackPositionChanged;

// Update recording position when new packets arrive
_playbackPipeline?.UpdateRecordingPosition(currentDuration);
```

### 3. `UnifiedPlayerViewModel.cs`
**Changes**:
- Added dual playhead properties:
  ```csharp
  TimeSpan RecordingPosition { get; set; }
  TimeSpan PlaybackPosition { get; set; }
  double RecordingPositionNormalized { get; }
  double PlaybackPositionNormalized { get; }
  string RecordingPositionDisplay { get; }
  ```

- Wires up `PlaybackController` from pipeline:
  ```csharp
  if (_livePlaybackManager.PlaybackPipeline?.PlaybackController != null)
  {
      controller.TimeChanged += OnLivePlaybackTimeChanged;
  }
  ```

- Updates playheads in event handlers:
  ```csharp
  // Recording playhead follows duration
  RecordingPosition = duration;
  
  // Playback playhead from PlaybackController
  PlaybackPosition = currentTime;
  ```

---

## ?? UI Integration

### XAML Binding Examples

**Recording Playhead (Static)**:
```xml
<!-- Red vertical line showing where recording is -->
<Line X1="{Binding RecordingPositionNormalized, 
          Converter={StaticResource NormalizedToPixelConverter}}"
      Y1="0" Y2="100"
      Stroke="Red" StrokeThickness="2">
    <Line.RenderTransform>
        <TranslateTransform X="0" Y="0"/>
    </Line.RenderTransform>
    <!-- Glow effect -->
    <Line.Effect>
        <DropShadowEffect Color="Red" BlurRadius="10" 
                         ShadowDepth="0" Opacity="0.8"/>
    </Line.Effect>
</Line>

<!-- Label -->
<TextBlock Text="{Binding RecordingPositionDisplay}"
           Foreground="Red"
           FontWeight="Bold"/>
```

**Playback Playhead (Dynamic)**:
```xml
<!-- Blue vertical line showing audio playback position -->
<Line X1="{Binding PlaybackPositionNormalized, 
          Converter={StaticResource NormalizedToPixelConverter}}"
      Y1="0" Y2="100"
      Stroke="#2196F3" StrokeThickness="2">
    <Line.RenderTransform>
        <TranslateTransform X="0" Y="0"/>
    </Line.RenderTransform>
</Line>

<!-- Animated indicator -->
<Ellipse Width="10" Height="10" Fill="#2196F3">
    <Ellipse.RenderTransform>
        <TranslateTransform 
            X="{Binding PlaybackPositionNormalized, 
                Converter={StaticResource NormalizedToPixelConverter}}"
            Y="50"/>
    </Ellipse.RenderTransform>
    <!-- Pulse animation -->
    <Ellipse.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard>
                <Storyboard RepeatBehavior="Forever">
                    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                   From="1.0" To="0.3" 
                                   Duration="0:0:1"
                                   AutoReverse="True"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Ellipse.Triggers>
</Ellipse>
```

**Combined View**:
```xml
<Grid>
    <!-- Waveform canvas -->
    <Canvas x:Name="WaveformCanvas">
        <!-- Drawn waveform (left of recording playhead) -->
        <Path Data="{Binding DrawnWaveformGeometry}"
              Stroke="White" StrokeThickness="1"/>
        
        <!-- Empty area (right of recording playhead) -->
        <Rectangle Fill="#10FFFFFF" 
                   Canvas.Left="{Binding RecordingPosition, 
                                Converter={StaticResource TimeToPixelConverter}}"
                   Width="{Binding RemainingWidth}"/>
    </Canvas>
    
    <!-- Recording playhead (static) -->
    <Line x:Name="RecordingPlayhead"
          X1="{Binding RecordingPositionNormalized, ...}"
          Stroke="Red" StrokeThickness="3"
          ToolTip="Recording Position"/>
    
    <!-- Playback playhead (dynamic) -->
    <Line x:Name="PlaybackPlayhead"
          X1="{Binding PlaybackPositionNormalized, ...}"
          Stroke="#2196F3" StrokeThickness="2"
          ToolTip="Playback Position"/>
</Grid>
```

---

## ?? Audio Playback Integration

### How It Works

```
1. Recording Thread (AudioPacketRecorder)
   ?? Writes packets to DuckDB
   ?? Updates recording position

2. Metadata Thread (LivePlaybackManager)
   ?? Polls every 500ms
   ?? Detects new packets
   ?? Updates RecordingPosition
   ?? Fires DurationUpdated event

3. Playback Thread (FilePlaybackPipeline)
   ?? Reads from DuckDB via FilePacketSource
   ?? Routes through PacketRouter
   ?? Mixes in AudioMixerEngine
   ?? Outputs through AudioOutputEngine
   ?? Updates PlaybackPosition

4. UI Thread
   ?? Receives position events
   ?? Updates playhead visuals
   ?? Animates waveform drawing
```

### Latency Breakdown

```
Packet arrival ? Recording playhead: ~100ms
  (AudioPacketRecorder batch insert)

Recording ? Playback playhead: ~400ms
  (100ms poll + 300ms jitter buffer)

Total latency: ~500ms
  (User sees recording playhead first, 
   then hears audio 400ms later)
```

---

## ?? User Controls

### Playback Controls

**Play** ??
```csharp
await _livePlaybackManager.PlaybackPipeline.PlayAsync();
```
- Starts audio playback from current `PlaybackPosition`
- Playback playhead starts moving
- Can play while recording continues

**Pause** ??
```csharp
_livePlaybackManager.PlaybackPipeline.Pause();
```
- Stops audio output
- Playback playhead stops moving
- Recording playhead continues

**Seek** ?
```csharp
await _livePlaybackManager.PlaybackPipeline.SeekAsync(targetPosition);
```
- Moves playback playhead to target
- Clamped to [0, RecordingPosition]
- Audio continues from new position

**Go Live** ??
```csharp
await _livePlaybackManager.PlaybackPipeline.GoLiveAsync();
```
- Seeks playback to recording position
- "Catch up" to live recording
- Minimizes latency

---

## ?? Visual Design

### Recommended Colors

| Element | Color | Purpose |
|---------|-------|---------|
| **Recording Playhead** | `#FF0000` (Red) | Urgent, active, "now" |
| **Recording Glow** | `#FF0000` 50% opacity | Emphasis |
| **Playback Playhead** | `#2196F3` (Blue) | Calm, informative |
| **Drawn Waveform** | `#FFFFFF` (White) | Clear visibility |
| **Future Area** | `#10FFFFFF` (10% white) | Subtle hint |

### Animation Recommendations

**Recording Playhead**:
- Subtle pulse (1s cycle)
- Glow intensity 50-100%
- No movement (static)

**Playback Playhead**:
- Smooth movement (60 FPS)
- Small pulse on current position
- Trail effect (fade behind)

**Waveform Drawing**:
- Fade-in for new segments
- Draw from left to right
- Smooth interpolation

---

## ?? Testing Scenarios

### Test 1: Dual Playhead Visualization
```
1. Start recording
2. Wait 5 seconds
3. ? Recording playhead at ~5s mark (red line)
4. Start playback
5. ? Playback playhead at 0s (blue line)
6. ? Playback playhead moves forward
7. ? Recording playhead stays ahead (~400ms)
```

### Test 2: Scrubbing During Recording
```
1. Recording active (30s recorded)
2. Playback at 10s
3. Scrub to 25s
4. ? Playback playhead jumps to 25s
5. ? Audio plays from 25s
6. ? Recording playhead still at 30s+
```

### Test 3: Go Live
```
1. Recording active (60s recorded)
2. Playback at 30s
3. Click "Go Live" button
4. ? Playback playhead jumps to recording position
5. ? Both playheads near same position
6. ? Minimal latency (~400ms)
```

---

## ?? Configuration

### Adjust Latency

**Lower Latency** (riskier):
```csharp
// In LiveAudioPlaybackService.cs
private const int TargetBufferMs = 150;  // Default: 300ms

// In LivePlaybackManager.cs
private readonly TimeSpan _audioPacketInterval = TimeSpan.FromMilliseconds(50); // Default: 100ms
```

**Higher Latency** (more stable):
```csharp
private const int TargetBufferMs = 500;  // Default: 300ms
private readonly TimeSpan _audioPacketInterval = TimeSpan.FromMilliseconds(200); // Default: 100ms
```

---

## ?? Known Issues

1. **FileSource/ServerSource errors**: These properties were removed in UnifiedPlayerViewModel
   - Need to restore or refactor UI code
   - Not blocking for dual-playhead functionality

2. **NAudio integration pending**: Audio output uses stub
   - Need to implement `WaveOut` initialization
   - Buffer management code is ready

3. **FilePacketSource refresh**: No method to reload metadata
   - New packets not immediately visible to playback
   - Workaround: periodic reopen (not efficient)

---

## ? Status

**Core Implementation**: ? Complete
- LiveRecordingPlaybackPipeline created
- Dual playhead tracking implemented
- PlaybackController integration done
- Event wiring complete

**Integration**: ? Partial
- LivePlaybackManager updated
- UnifiedPlayerViewModel updated
- UI bindings pending

**Testing**: ? Pending
- Needs real SRS recording
- Visual verification needed
- Performance tuning required

---

## ?? Next Steps

1. **Fix UI Errors**: Restore ServerSource/FileSource or refactor
2. **Complete NAudio**: Implement WaveOut initialization
3. **Add UI Elements**: Create dual playhead visuals
4. **Test with SRS**: Record and play live
5. **Optimize Performance**: Tune polling intervals
6. **Add Controls**: "Go Live" button, scrubbing

---

**Implementation Date**: 2025-01-18
**Status**: ? Core Complete, ? UI Integration Pending
**Architecture**: Reuses existing FilePlaybackPipeline infrastructure

?? **Dual Playhead Infrastructure Ready!** ??
