# Phase 4 Enhanced: Scrubbing Support for Live Recording Playback

## ?? Overview

Enable users to scrub the playback playhead **during live recording** to listen to earlier parts of the recording while new packets continue to arrive. This creates a complete "timeline scrubbing" experience similar to YouTube live streams.

---

## ?? User Experience

### Scenario: Live Recording in Progress

```
Recording Timeline (60 seconds recorded so far):
|----------|----------|----------|----------|----------|-------->
0s        12s        24s        36s        48s        60s (RECORDING)
                                 ?
                            Playback (User scrubbed back)
```

**User Actions:**
1. **Scrub Backward** - Drag playback playhead to 24s while recording continues
2. **Listen** - Hear audio from 24s onwards
3. **Play** - Audio plays forward from 24s
4. **Pause** - Stop playback at 30s
5. **"Go Live"** - Jump back to 60s (recording position)

---

## ?? Implementation Plan

### 1. Add Live Recording Commands

Add these commands to `UnifiedPlayerViewModel`:

```csharp
// Phase 4 Enhanced: Live recording playback commands
public ICommand PlayLiveRecordingCommand { get; }
public ICommand PauseLiveRecordingCommand { get; }
public ICommand SeekLiveRecordingCommand { get; }
public ICommand GoToLivePositionCommand { get; }
```

**Initialize in constructor:**
```csharp
PlayLiveRecordingCommand = new RelayCommand(ExecutePlayLiveRecording, CanExecutePlayLiveRecording);
PauseLiveRecordingCommand = new RelayCommand(ExecutePauseLiveRecording, CanExecutePauseLiveRecording);
SeekLiveRecordingCommand = new RelayCommand<double>(pos => ExecuteSeekLiveRecording(pos), pos => CanExecuteSeekLiveRecording(pos));
GoToLivePositionCommand = new RelayCommand(ExecuteGoToLivePosition, CanExecuteGoToLivePosition);
```

---

### 2. Implement Command Handlers

Add these methods to `UnifiedPlayerViewModel.cs`:

```csharp
#region Phase 4 Enhanced: Live Recording Playback Commands

/// <summary>
/// Can play live recording: recording is active and playback is not already playing
/// </summary>
private bool CanExecutePlayLiveRecording()
{
    return IsLiveRecording && 
           _livePlaybackManager.PlaybackPipeline != null &&
           _livePlaybackManager.PlaybackPipeline.PlaybackController?.IsPlaying == false;
}

/// <summary>
/// Start playing the live recording from current playback position
/// </summary>
private async void ExecutePlayLiveRecording()
{
    try
    {
        if (_livePlaybackManager.PlaybackPipeline == null)
        {
            Logger.Warn("No live playback pipeline available");
            return;
        }
        
        Logger.Info($"?? Starting live recording playback from {PlaybackPosition}");
        await _livePlaybackManager.PlaybackPipeline.PlayAsync();
        StatusMessage = $"?? Playing from {PlaybackPosition:hh\\:mm\\:ss}";
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to play live recording");
        StatusMessage = $"Playback error: {ex.Message}";
    }
}

/// <summary>
/// Can pause live recording: recording is active and playback is playing
/// </summary>
private bool CanExecutePauseLiveRecording()
{
    return IsLiveRecording && 
           _livePlaybackManager.PlaybackPipeline?.PlaybackController?.IsPlaying == true;
}

/// <summary>
/// Pause live recording playback
/// </summary>
private void ExecutePauseLiveRecording()
{
    try
    {
        if (_livePlaybackManager.PlaybackPipeline == null)
        {
            Logger.Warn("No live playback pipeline available");
            return;
        }
        
        Logger.Info($"?? Pausing live recording playback at {PlaybackPosition}");
        _livePlaybackManager.PlaybackPipeline.Pause();
        StatusMessage = $"?? Paused at {PlaybackPosition:hh\\:mm\\:ss}";
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to pause live recording");
    }
}

/// <summary>
/// Can seek live recording: recording is active
/// </summary>
private bool CanExecuteSeekLiveRecording(double? normalizedPosition)
{
    return IsLiveRecording && 
           normalizedPosition.HasValue &&
           _livePlaybackManager.PlaybackPipeline != null;
}

/// <summary>
/// Seek to a specific position in the live recording
/// Position is clamped to [0, RecordingPosition]
/// </summary>
private async void ExecuteSeekLiveRecording(double? normalizedPosition)
{
    if (!normalizedPosition.HasValue || _livePlaybackManager.PlaybackPipeline == null)
        return;
    
    try
    {
        // Calculate target time (clamped to recording position)
        var targetTicks = (long)(RecordingPosition.Ticks * normalizedPosition.Value);
        var targetTime = TimeSpan.FromTicks(Math.Max(0, Math.Min(targetTicks, RecordingPosition.Ticks)));
        
        Logger.Info($"? Seeking live recording to {targetTime} (normalized: {normalizedPosition.Value:F2})");
        
        await _livePlaybackManager.PlaybackPipeline.SeekAsync(targetTime);
        
        // Update playback position (will be synced via PlaybackController events)
        PlaybackPosition = targetTime;
        CurrentPosition = targetTime;
        
        StatusMessage = $"? Seeked to {targetTime:hh\\:mm\\:ss}";
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to seek live recording");
        StatusMessage = $"Seek error: {ex.Message}";
    }
}

/// <summary>
/// Can go to live position: recording is active and playback is behind recording
/// </summary>
private bool CanExecuteGoToLivePosition()
{
    return IsLiveRecording && 
           PlaybackPosition < RecordingPosition &&
           _livePlaybackManager.PlaybackPipeline != null;
}

/// <summary>
/// Jump to the current recording position ("Go Live")
/// </summary>
private async void ExecuteGoToLivePosition()
{
    try
    {
        if (_livePlaybackManager.PlaybackPipeline == null)
        {
            Logger.Warn("No live playback pipeline available");
            return;
        }
        
        Logger.Info($"?? Going live - jumping from {PlaybackPosition} to {RecordingPosition}");
        
        await _livePlaybackManager.PlaybackPipeline.GoLiveAsync();
        
        StatusMessage = "?? LIVE - Caught up to recording";
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to go live");
        StatusMessage = $"Go Live error: {ex.Message}";
    }
}

#endregion
```

---

### 3. Update Seek Clamping in LiveRecordingPlaybackPipeline

Ensure seeking is properly clamped to recording position. This is **already implemented** in `LiveRecordingPlaybackPipeline.cs`:

```csharp
/// <summary>
/// Seeks to a specific position in the recording.
/// Useful for scrubbing through already-recorded audio during live recording.
/// </summary>
public async Task SeekAsync(TimeSpan position)
{
    if (!_isInitialized || _pipeline == null)
        throw new InvalidOperationException("Pipeline not initialized");
    
    // Clamp to valid range (can't seek beyond recording position)
    var clampedPosition = TimeSpan.FromMilliseconds(
        Math.Max(0, Math.Min(position.TotalMilliseconds, RecordingPosition.TotalMilliseconds)));
    
    Logger.Info($"? Seeking to: {clampedPosition} (clamped to recording position)");
    await _pipeline.SeekAsync(clampedPosition);
}
```

? **This ensures users can't seek beyond where recording has reached.**

---

## ?? XAML UI Elements

### 1. Playback Controls for Live Recording

```xml
<!-- Phase 4: Live Recording Playback Controls -->
<StackPanel Orientation="Horizontal" 
            Visibility="{Binding IsLiveRecording, Converter={StaticResource BoolToVisibilityConverter}}"
            Margin="10,5">
    
    <!-- Play Button -->
    <Button Command="{Binding PlayLiveRecordingCommand}"
            ToolTip="Play recorded audio"
            Width="40" Height="40" Margin="0,0,5,0">
        <Path Data="M8,5 L26,16 L8,27 Z" 
              Fill="White" Stretch="Uniform" 
              Width="14" Height="14"/>
    </Button>
    
    <!-- Pause Button -->
    <Button Command="{Binding PauseLiveRecordingCommand}"
            ToolTip="Pause playback"
            Width="40" Height="40" Margin="0,0,5,0">
        <Path Data="M10,5 L14,5 L14,27 L10,27 Z M18,5 L22,5 L22,27 L18,27 Z" 
              Fill="White" Stretch="Uniform" 
              Width="14" Height="14"/>
    </Button>
    
    <!-- Go Live Button -->
    <Button Command="{Binding GoToLivePositionCommand}"
            ToolTip="Jump to live position"
            Background="#FF0000"
            Width="80" Height="40" Margin="0,0,5,0">
        <StackPanel Orientation="Horizontal">
            <Ellipse Width="10" Height="10" Fill="White" Margin="0,0,5,0">
                <!-- Pulse animation -->
                <Ellipse.Triggers>
                    <EventTrigger RoutedEvent="Loaded">
                        <BeginStoryboard>
                            <Storyboard RepeatBehavior="Forever">
                                <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                               From="1.0" To="0.3" Duration="0:0:0.8"
                                               AutoReverse="True"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </Ellipse.Triggers>
            </Ellipse>
            <TextBlock Text="GO LIVE" Foreground="White" FontWeight="Bold"/>
        </StackPanel>
    </Button>
    
    <!-- Position Display -->
    <TextBlock VerticalAlignment="Center" Margin="10,0">
        <Run Text="Playback: "/>
        <Run Text="{Binding PlaybackPosition, StringFormat='hh\\:mm\\:ss', Mode=OneWay}" 
             FontWeight="Bold" Foreground="#2196F3"/>
        <Run Text=" / "/>
        <Run Text="{Binding RecordingPosition, StringFormat='hh\\:mm\\:ss', Mode=OneWay}" 
             FontWeight="Bold" Foreground="#FF0000"/>
    </TextBlock>
</StackPanel>
```

---

### 2. Scrubbing Slider

```xml
<!-- Phase 4: Live Recording Timeline Scrubber -->
<Grid Margin="10,5" 
      Visibility="{Binding IsLiveRecording, Converter={StaticResource BoolToVisibilityConverter}}">
    
    <!-- Timeline Canvas -->
    <Canvas Height="60" Background="#20FFFFFF" ClipToBounds="True">
        
        <!-- Recorded Region (0 to RecordingPosition) -->
        <Rectangle Fill="#40FFFFFF" Height="60"
                   Width="{Binding RecordingPositionNormalized, 
                          Converter={StaticResource NormalizedToPixelConverter}}"
                   ToolTip="Recorded audio available for playback"/>
        
        <!-- Unrecorded Region (RecordingPosition to end) -->
        <Rectangle Fill="#10FFFFFF" Height="60"
                   Canvas.Left="{Binding RecordingPositionNormalized, 
                                Converter={StaticResource NormalizedToPixelConverter}}"
                   Width="{Binding RemainingWidth}"
                   ToolTip="Recording in progress..."/>
        
        <!-- Recording Playhead (Static - Red) -->
        <Line X1="{Binding RecordingPositionNormalized, 
                  Converter={StaticResource NormalizedToPixelConverter}}"
              X2="{Binding RecordingPositionNormalized, 
                  Converter={StaticResource NormalizedToPixelConverter}}"
              Y1="0" Y2="60"
              Stroke="#FF0000" StrokeThickness="3"
              ToolTip="{Binding RecordingPositionDisplay}">
            <Line.Effect>
                <DropShadowEffect Color="Red" BlurRadius="10" 
                                ShadowDepth="0" Opacity="0.8"/>
            </Line.Effect>
        </Line>
        
        <!-- Playback Playhead (Dynamic - Blue) -->
        <Line X1="{Binding PlaybackPositionNormalized, 
                  Converter={StaticResource NormalizedToPixelConverter}}"
              X2="{Binding PlaybackPositionNormalized, 
                  Converter={StaticResource NormalizedToPixelConverter}}"
              Y1="0" Y2="60"
              Stroke="#2196F3" StrokeThickness="2"
              ToolTip="{Binding PlaybackPosition, StringFormat='Playback: {0:hh\\:mm\\:ss}'}"/>
    </Canvas>
    
    <!-- Scrubber Slider (Overlaid) -->
    <Slider Minimum="0" Maximum="1" 
            Value="{Binding PlaybackPositionNormalized, Mode=TwoWay}"
            Command="{Binding SeekLiveRecordingCommand}"
            CommandParameter="{Binding Value, RelativeSource={RelativeSource Self}}"
            VerticalAlignment="Bottom"
            Opacity="0.1" 
            MouseEnter="Slider_MouseEnter"
            MouseLeave="Slider_MouseLeave"
            ToolTip="Drag to scrub through recording"/>
</Grid>
```

**Code-behind for hover effect:**
```csharp
private void Slider_MouseEnter(object sender, MouseEventArgs e)
{
    if (sender is Slider slider)
        slider.Opacity = 0.8; // Show slider clearly on hover
}

private void Slider_MouseLeave(object sender, MouseEventArgs e)
{
    if (sender is Slider slider)
        slider.Opacity = 0.1; // Hide slider when not hovering
}
```

---

## ?? User Interaction Flow

### Scenario 1: Scrub Back and Listen

```
1. User drags slider left to 30s position
   ?
2. ExecuteSeekLiveRecording(0.5) called (30s / 60s)
   ?
3. LiveRecordingPlaybackPipeline.SeekAsync(30s) 
   ?
4. FilePlaybackPipeline seeks to 30s
   ?
5. PlaybackPosition updates to 30s (via PlaybackController.TimeChanged)
   ?
6. Blue playhead moves to 30s on timeline
   ?
7. Audio plays from 30s onwards
```

### Scenario 2: Go Live

```
1. User clicks "GO LIVE" button (playback is at 30s, recording at 60s)
   ?
2. ExecuteGoToLivePosition() called
   ?
3. LiveRecordingPlaybackPipeline.GoLiveAsync()
   ?
4. Seeks to RecordingPosition (60s)
   ?
5. PlaybackPosition jumps to 60s
   ?
6. Blue playhead catches up to red playhead
   ?
7. StatusMessage: "?? LIVE - Caught up to recording"
```

### Scenario 3: Play/Pause Control

```
1. User pauses at 35s
   ?
2. ExecutePauseLiveRecording() called
   ?
3. Playback stops, PlaybackPosition stays at 35s
   ?
4. Recording continues (RecordingPosition keeps moving)
   ?
5. User scrubs to 45s
   ?
6. User clicks Play
   ?
7. ExecutePlayLiveRecording() called
   ?
8. Audio resumes from 45s
```

---

## ?? Safety Constraints

### 1. Seek Clamping
```csharp
// Can't seek beyond recording position
var clampedPosition = TimeSpan.FromMilliseconds(
    Math.Max(0, Math.Min(position.TotalMilliseconds, RecordingPosition.TotalMilliseconds)));
```

**Why?** Recording hasn't reached that point yet - no data available.

### 2. Command Enablement
```csharp
// Play button disabled if:
// - Not recording
// - Already playing
// - No pipeline

// Seek disabled if:
// - Not recording
// - No pipeline

// Go Live disabled if:
// - Not recording
// - Already at live position
// - No pipeline
```

---

## ?? Visual Feedback

### Timeline States

| State | Recording Playhead | Playback Playhead | Slider Opacity |
|-------|-------------------|-------------------|----------------|
| **At Live** | 60s (Red) | 60s (Blue, overlapping) | 0.1 (hidden) |
| **Scrubbed Back** | 60s (Red) | 30s (Blue, separate) | 0.8 (visible) |
| **Paused** | 60s (Red, moving) | 35s (Blue, static) | 0.8 (visible) |
| **Seeking** | 60s (Red) | Animating to target | 1.0 (fully visible) |

### Button States

| Button | Enabled When | Visual State |
|--------|-------------|-------------|
| **Play** | Recording & Not Playing | White play icon |
| **Pause** | Recording & Playing | White pause icon |
| **Go Live** | Recording & Behind Live | Red background, pulsing dot |
| **Scrubber** | Recording | Blue thumb, red track |

---

## ?? Testing Scenarios

### Test 1: Basic Scrubbing
```
1. Start recording (30s recorded)
2. Drag slider to 15s
3. ? Playback jumps to 15s
4. ? Audio plays from 15s
5. ? Blue playhead at 15s, red at 30s
```

### Test 2: Play/Pause
```
1. Recording active (40s recorded)
2. Scrub to 20s
3. Click Play
4. ? Audio plays from 20s
5. Click Pause at 25s
6. ? Playback stops
7. ? Recording continues (red playhead keeps moving)
```

### Test 3: Go Live
```
1. Recording active (60s recorded)
2. Scrub to 20s
3. Click "GO LIVE"
4. ? Playback jumps to 60s
5. ? Blue playhead overlaps red playhead
6. ? StatusMessage shows "?? LIVE"
```

### Test 4: Seek Beyond Limit
```
1. Recording active (45s recorded)
2. Try to drag slider to 90s (beyond recording)
3. ? Seek clamped to 45s (recording position)
4. ? Can't seek into "future"
```

---

## ?? Implementation Checklist

- [ ] Add command properties to UnifiedPlayerViewModel
- [ ] Initialize commands in constructor
- [ ] Implement command handler methods
- [ ] Add XAML controls for playback (Play/Pause buttons)
- [ ] Add XAML scrubber slider
- [ ] Add "Go Live" button with pulse animation
- [ ] Wire up slider to SeekLiveRecordingCommand
- [ ] Add hover effects for slider visibility
- [ ] Test scrubbing during live recording
- [ ] Test play/pause independence from recording
- [ ] Test "Go Live" catch-up functionality
- [ ] Test seek clamping (can't seek beyond recording position)

---

## ?? Expected Result

Users will be able to:
- ? **Scrub backward** in time while recording continues
- ? **Listen to earlier parts** of the recording
- ? **Play/pause** independently of recording state
- ? **Jump back to live** with one click
- ? **See dual playheads** (recording = red, playback = blue)
- ? **Visually distinguish** recorded vs unrecorded regions

**This creates a professional live recording experience similar to professional audio editing software!** ??

---

**Implementation Date**: 2025-01-18  
**Status**: ? Commands defined, handlers pending implementation  
**Complexity**: Medium (reuses existing pipeline infrastructure)
