# Phase 12: Integration Guide

**Status**: ?? Integration In Progress  
**Date**: January 24, 2025  
**Purpose**: Connect Phase 12 WaveformDisplayPanel to parent services

---

## ?? Integration Overview

The Phase 12 migration successfully replaced the legacy waveform controls with `UnifiedGraphControl`. Now we need to properly connect it to the parent `UnifiedPlayerControl` and its services.

---

## ?? Integration Points

### 1. PlaybackController Connection ?

**Current Implementation**: UnifiedPlayerControl already connects `UnifiedGraph` to `PlaybackController`:

```csharp
// In UnifiedPlayerControl.xaml.cs - OnFileLoaded()
var playbackController = ViewModel?.PlaybackController;
if (playbackController != null && UnifiedGraph != null)
{
    UnifiedGraph.ConnectPlayheadToPlayback(playbackController);
    _logger.Info("Phase 6: Connected UnifiedGraph playhead to PlaybackController");
}
```

**Phase 12 Update Needed**: ? Now also connect `WaveformDisplayPanel`'s `PlayheadSyncService`:

```csharp
// In UnifiedPlayerControl.xaml.cs - OnFileLoaded()
// After existing UnifiedGraph connection:

// Phase 12: Connect WaveformDisplayPanel PlayheadSyncService
if (playbackController != null && WaveformPanel?.UnifiedGraphViewModel != null)
{
    var playheadService = WaveformPanel._playheadSyncService;
    if (playheadService != null)
    {
        playheadService.Connect(playbackController);
        _logger.Info("Phase 12: Connected WaveformPanel playhead to PlaybackController");
    }
}
```

---

### 2. Data Flow Connection

**Current Bindings** (from UnifiedPlayerControl.xaml):
```xaml
<player:WaveformDisplayPanel x:Name="WaveformPanel"
    WaveformData="{Binding WaveformData}"
    FrequencyWaveforms="{Binding FrequencyWaveforms}"
    PlayheadPosition="{Binding PlayheadPositionNormalized}"
    TotalDuration="{Binding TotalDuration}"
    IsLoading="{Binding IsLoadingWaveform}"
    ... />
```

**Phase 12 Data Flow**:
```
UnifiedPlayerViewModel
  ??? WaveformData (float[]) 
  ?     ??> WaveformDisplayPanel.WaveformData (DependencyProperty)
  ?           ??> UpdateWaveformData()
  ?                 ??> UnifiedGraphViewModel.LoadDataAsync(start, end)
  ?                       ??> DataTileManager.LoadTilesForViewportAsync()
  ?
  ??? FrequencyWaveforms (Dictionary<double, FrequencyWaveformData>)
  ?     ??> WaveformDisplayPanel.FrequencyWaveforms (DependencyProperty)
  ?           ??> UpdateFrequencyWaveforms()
  ?                 ??> UnifiedGraphViewModel.LoadDataAsync(start, end)
  ?
  ??? PlayheadPositionNormalized (double, 0-1)
  ?     ??> WaveformDisplayPanel.PlayheadPosition (DependencyProperty)
  ?           ??> UpdatePlayheadPosition()
  ?                 ??> PlayheadSyncService.Seek(DateTime)
  ?                       ??> UnifiedGraphViewModel.PlayheadTime
  ?
  ??? TotalDuration (TimeSpan)
        ??> WaveformDisplayPanel.TotalDuration (DependencyProperty)
              ??> UpdateTotalDuration()
                    ??> PlayheadSyncService.SetTimeRange(start, end)
                    ??> UnifiedGraphViewModel.Start/End
```

**Status**: ? **Automatic** - DependencyProperty change callbacks handle this

---

### 3. Event Handling

**Current Events** (from UnifiedPlayerControl.xaml):
```xaml
<player:WaveformDisplayPanel
    SeekRequested="OnSeekRequested"
    ZoomChanged="OnZoomChanged"
    WaveformSizeChanged="OnWaveformSizeChanged" />
```

**Event Handlers**:
```csharp
// In UnifiedPlayerControl.xaml.cs

private void OnSeekRequested(object sender, SeekRequestedEventArgs e)
{
    // ? Works as-is - forwards to ViewModel.SeekCommand
    if (ViewModel?.SeekCommand?.CanExecute(e.NormalizedPosition) == true)
    {
        ViewModel.SeekCommand.Execute(e.NormalizedPosition);
    }
}

private void OnZoomChanged(object sender, ZoomChangedEventArgs e)
{
    // ? Works as-is - handled by TwoWay binding on ZoomStartTime/ZoomEndTime
}

private async void OnWaveformSizeChanged(object sender, WaveformSizeChangedEventArgs e)
{
    // ?? May need update for Phase 12
    // Currently calls: ViewModel.UpdateWaveformAsync(width, height)
    // Phase 12: UnifiedGraphControl handles size changes automatically
    // May be able to remove or simplify this
}
```

**Status**: 
- ? `SeekRequested` - Works as-is
- ? `ZoomChanged` - Works as-is
- ?? `WaveformSizeChanged` - May be redundant in Phase 12

---

### 4. Frequency Visibility Sync

**Current Implementation**:
```csharp
// In UnifiedPlayerControl.xaml.cs
private void OnFrequencySelectionChanged(object sender, FrequencySelectionChangedEventArgs e)
{
    ViewModel?.OnFrequencySelectionChanged(e.Frequency, e.IsSelected);
}
```

**Phase 12 Enhancement Needed**: ?
```csharp
private void OnFrequencySelectionChanged(object sender, FrequencySelectionChangedEventArgs e)
{
    // Existing: Update ViewModel
    ViewModel?.OnFrequencySelectionChanged(e.Frequency, e.IsSelected);
    
    // Phase 12: Also update UnifiedGraphViewModel for immediate visual feedback
    if (WaveformPanel?.UnifiedGraphViewModel != null)
    {
        var freqId = e.Frequency.ToString();
        WaveformPanel.UnifiedGraphViewModel.SetSeriesVisibility(freqId, e.IsSelected);
        _logger.Debug($"Phase 12: Set frequency {e.Frequency} visibility to {e.IsSelected}");
    }
}
```

**Benefits**:
- Immediate visual feedback in graph
- Consistent with audio mixer state
- Leverages Phase 7 visibility toggle infrastructure

---

## ?? Integration Checklist

### Core Connections
- [x] ViewModel property created (`UnifiedGraphViewModel`)
- [x] Services initialized (DataTileManager, PlayheadSync, ErrorHandling)
- [x] DependencyProperty bindings working
- [ ] PlayheadSyncService connected to PlaybackController
- [ ] Frequency visibility sync implemented
- [ ] Size change event validated

### Data Flow
- [x] `WaveformData` ? `LoadDataAsync()`
- [x] `FrequencyWaveforms` ? `LoadDataAsync()`
- [x] `PlayheadPosition` ? `PlayheadSyncService.Seek()`
- [x] `TotalDuration` ? `SetTimeRange()`

### Event Flow
- [x] `SeekRequested` ? `ViewModel.SeekCommand`
- [x] `ZoomChanged` ? TwoWay binding
- [ ] `WaveformSizeChanged` ? Validate necessity
- [ ] Frequency selection ? UnifiedGraphViewModel

### Testing
- [ ] Load real SRS file
- [ ] Verify playhead sync during playback
- [ ] Test frequency visibility toggles
- [ ] Validate zoom/pan operations
- [ ] Check seek accuracy
- [ ] Monitor memory usage

---

## ?? Implementation Steps

### Step 1: Add PlayheadSyncService Connection ?

**File**: `src/AeroDebrief.UI/Controls/UnifiedPlayerControl.xaml.cs`

**Location**: In `OnFileLoaded()` method

**Code**:
```csharp
private void OnFileLoaded(string filePath)
{
    Dispatcher.BeginInvoke(() =>
    {
        FileOverlay?.Close();
        
        // Existing Phase 6: Connect UnifiedGraph playhead
        try
        {
            var playbackController = ViewModel?.PlaybackController;
            
            if (playbackController != null && UnifiedGraph != null)
            {
                UnifiedGraph.ConnectPlayheadToPlayback(playbackController);
                _logger.Info("Phase 6: Connected UnifiedGraph playhead to PlaybackController");
            }
            
            // NEW Phase 12: Connect WaveformPanel playhead
            if (playbackController != null && WaveformPanel?.UnifiedGraphViewModel != null)
            {
                // Access private field via reflection or expose as property
                var playheadSyncField = typeof(Player.WaveformDisplayPanel)
                    .GetField("_playheadSyncService", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                
                if (playheadSyncField != null)
                {
                    var playheadService = playheadSyncField.GetValue(WaveformPanel) as Services.Graphs.IPlayheadSyncService;
                    if (playheadService != null)
                    {
                        playheadService.Connect(playbackController);
                        _logger.Info("Phase 12: Connected WaveformPanel playhead to PlaybackController");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Phase 12: Error connecting playhead to PlaybackController");
        }
    });
}
```

**Better Approach**: Expose PlayheadSyncService as public property in WaveformDisplayPanel:

```csharp
// In WaveformDisplayPanel.xaml.cs
public IPlayheadSyncService? PlayheadSyncService => _playheadSyncService;
```

Then use it:
```csharp
if (playbackController != null && WaveformPanel?.PlayheadSyncService != null)
{
    WaveformPanel.PlayheadSyncService.Connect(playbackController);
    _logger.Info("Phase 12: Connected WaveformPanel playhead to PlaybackController");
}
```

---

### Step 2: Add Frequency Visibility Sync ?

**File**: `src/AeroDebrief.UI/Controls/UnifiedPlayerControl.xaml.cs`

**Location**: In `OnFrequencySelectionChanged()` method

**Code**:
```csharp
private void OnFrequencySelectionChanged(object sender, Events.FrequencySelectionChangedEventArgs e)
{
    // Existing: Update ViewModel (audio mixer)
    ViewModel?.OnFrequencySelectionChanged(e.Frequency, e.IsSelected);
    
    // Phase 12: Update UnifiedGraphViewModel (visual)
    if (WaveformPanel?.UnifiedGraphViewModel != null)
    {
        try
        {
            var frequencyKey = $"{e.Frequency:F0}"; // Format as string key
            WaveformPanel.UnifiedGraphViewModel.SetSeriesVisibility(frequencyKey, e.IsSelected);
            _logger.Debug($"Phase 12: Set frequency {e.Frequency} MHz visibility to {e.IsSelected}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Phase 12: Failed to sync frequency visibility for {e.Frequency}");
        }
    }
}
```

---

### Step 3: Validate WaveformSizeChanged Event ?

**Current Implementation**:
```csharp
private async void OnWaveformSizeChanged(object sender, WaveformSizeChangedEventArgs e)
{
    if (ViewModel == null || e.NewWidth <= 0 || e.NewHeight <= 0)
        return;

    try
    {
        var waveformWidth = (int)e.NewWidth;
        var waveformHeight = (int)e.NewHeight;
        await ViewModel.UpdateWaveformAsync(waveformWidth, waveformHeight);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Failed to update waveform on size change");
    }
}
```

**Analysis**:
- This calls `ViewModel.UpdateWaveformAsync()` which regenerates waveform data
- In Phase 12, `UnifiedGraphControl` handles resizing automatically via LiveCharts2
- **Decision**: Keep the event handler for legacy compatibility, but it may not be needed for UnifiedGraphControl rendering

**Recommendation**: Monitor during testing - if resizing works smoothly without this, consider removing

---

## ?? Expected Behavior After Integration

### On File Load
1. ? `WaveformData` updates ? `UnifiedGraphViewModel.LoadDataAsync()` called
2. ? `FrequencyWaveforms` updates ? Multi-frequency series created
3. ? `PlayheadSyncService` connects to `PlaybackController`
4. ? `TotalDuration` updates ? Time range set
5. ? Waveform displays in UnifiedGraphControl

### During Playback
1. ? `PlaybackController` fires `TimeChanged` events (60Hz)
2. ? `PlayheadSyncService` receives updates
3. ? `UnifiedGraphViewModel.PlayheadTime` updates
4. ? Playhead line moves across chart
5. ? Follow mode auto-pans if enabled

### On Frequency Toggle
1. ? User clicks frequency in mixer
2. ? `OnFrequencySelectionChanged` fires
3. ? `ViewModel.OnFrequencySelectionChanged()` - Audio mixer updates
4. ? `UnifiedGraphViewModel.SetSeriesVisibility()` - Visual updates
5. ? Series shows/hides with smooth animation

### On Zoom/Pan
1. ? User interacts with zoom buttons or mouse
2. ? `ZoomStartTime` / `ZoomEndTime` update via TwoWay binding
3. ? `UnifiedGraphViewModel.ViewportStart/End` update
4. ? `DataTileManager` loads appropriate tiles
5. ? Chart displays zoomed region

---

## ?? Known Integration Issues

### Issue 1: Private Field Access
**Problem**: `_playheadSyncService` is private in `WaveformDisplayPanel`  
**Solution**: Add public property:
```csharp
public IPlayheadSyncService? PlayheadSyncService => _playheadSyncService;
```

### Issue 2: SetSeriesVisibility Key Format
**Problem**: Frequency keys must match between mixer and chart  
**Solution**: Use consistent format: `"{frequency:F0}"` (e.g., "251000000")

### Issue 3: WaveformSizeChanged Redundancy
**Problem**: May cause unnecessary regeneration with LiveCharts2  
**Solution**: Monitor during testing, potentially remove

---

## ?? Testing Matrix

| Test Case | Expected Result | Status |
|-----------|----------------|--------|
| Load small file (< 10MB) | Waveform displays in < 3s | ? |
| Load medium file (10-100MB) | Waveform displays in < 10s | ? |
| Load large file (> 100MB) | Progressive loading, tiles visible | ? |
| Start playback | Playhead moves smoothly (60 FPS) | ? |
| Pause playback | Playhead stops | ? |
| Seek by click | Playhead jumps, audio syncs | ? |
| Zoom in (2x) | Chart zooms, tiles reload | ? |
| Zoom out (0.5x) | Chart zooms out, tiles reload | ? |
| Reset zoom | Full waveform visible | ? |
| Toggle frequency on | Series appears with animation | ? |
| Toggle frequency off | Series hides with animation | ? |
| Resize window | Chart resizes smoothly | ? |
| Memory usage (30 min) | < 500 MB | ? |

---

## ?? Success Criteria

- [x] Build successful
- [x] All tests passing
- [ ] PlayheadSyncService connected
- [ ] Frequency visibility sync working
- [ ] Playback synchronized
- [ ] Zoom/pan responsive
- [ ] Memory usage acceptable
- [ ] Performance targets met

---

## ?? Next Steps

1. **Immediate**: Add `PlayheadSyncService` public property
2. **Short-term**: Implement PlaybackController connection
3. **Short-term**: Add frequency visibility sync
4. **Medium-term**: Test with real SRS files
5. **Medium-term**: Performance benchmarking

---

**Document Version**: 1.0  
**Status**: ?? **INTEGRATION GUIDE**  
**Ready for**: Implementation

*Let's connect all the pieces!* ???
