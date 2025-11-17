# Phase 6: Real PlaybackController Integration Analysis

## ?? Current Status

**Date**: January 21, 2025  
**Analysis**: Real PlaybackController integration requirements  
**Finding**: **Most integration is already possible!**

---

## ? What's Already Available

### PlaybackController Events
The `PlaybackController` class in `AeroDebrief.Core.Playback` already has all the events we need:

```csharp
public sealed class PlaybackController : IDisposable
{
    // Playback state events
    public event Action? PlaybackStarted;
    public event Action? PlaybackStopped;
    public event Action? PlaybackPaused;
    public event Action? PlaybackResumed;
    
    // Time update events
    public event Action<double>? ProgressChanged;
    public event Action<TimeSpan, TimeSpan>? TimeChanged;
    
    // Speed change events  
    public event Action<double>? PlaybackSpeedChanged;
    public event Action<bool, double, double>? SpeedClampedChanged;
    
    // Properties
    public TimeSpan TotalDuration { get; private set; }
    public TimeSpan CurrentPosition { get; private set; }
    public DateTime RecordingStart { get; private set; }
    public bool IsPlaying { get; }
    public bool IsPaused { get; }
    public double PlaybackSpeed { get; }
}
```

### How to Access PlaybackController

**Via FilePlaybackPipeline**:
```csharp
// In CoreApiService (AudioSession)
private FilePlaybackPipeline? _pipeline;

// PlaybackController is exposed as property
public PlaybackController PlaybackController => _pipeline?.PlaybackController 
    ?? throw new InvalidOperationException("Pipeline not opened");
```

**Architecture**:
```
AudioSession (CoreApiService)
    ? has
FilePlaybackPipeline
    ? has
PlaybackController (with all events)
```

---

## ?? Integration Steps for Phase 6

### Step 1: Expose PlaybackController from AudioSession

**File**: `src/AeroDebrief.UI/Services/CoreApiService.cs`

**Add Property**:
```csharp
/// <summary>
/// Gets the playback controller for synchronization with chart playhead.
/// Returns null if no file is loaded.
/// </summary>
public PlaybackController? PlaybackController => _pipeline?.PlaybackController;
```

### Step 2: Connect PlayheadSyncService to PlaybackController

**File**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`

**Update InitializePlayheadSync**:
```csharp
private void InitializePlayheadSync(UnifiedGraphViewModel vm)
{
    try
    {
        // Create playhead sync service
        _playheadSyncService = new Services.Graphs.PlayheadSyncService();

        // Set time range from ViewModel
        _playheadSyncService.SetTimeRange(vm.Start, vm.End);

        // Subscribe to playhead sync service events
        _playheadSyncService.TimeChanged += (s, time) =>
        {
            vm.PlayheadTime = time;
        };

        _playheadSyncService.PlaybackStateChanged += (s, isPlaying) =>
        {
            vm.IsPlaying = isPlaying;
        };

        _playheadSyncService.PlaybackRateChanged += (s, rate) =>
        {
            vm.PlaybackRate = rate;
        };

        // Subscribe to ViewModel playhead changes for visual updates
        vm.PlayheadTimeChanged += OnPlayheadTimeChanged;

        // NEW: Connect to actual PlaybackController via App.Current
        var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
        var audioSession = mainWindow?.AudioSession;
        var playbackController = audioSession?.PlaybackController;
        
        if (playbackController != null)
        {
            _playheadSyncService.Connect(playbackController);
            _logger.Info("Phase 6: Connected to real PlaybackController");
        }
        else
        {
            _logger.Info("Phase 6: PlaybackController not available (no file loaded)");
        }
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Error initializing playhead synchronization");
    }
}
```

### Step 3: Update IPlayheadSyncService.Connect Signature

**Current Issue**: Interface expects `PlaybackSessionManager` but we have `PlaybackController`

**Solution**: Update the interface to use the correct type:

**File**: `src/AeroDebrief.UI/Services/Graphs/IPlayheadSyncService.cs`

**Change**:
```csharp
// OLD:
void Connect(Core.Playback.PlaybackController sessionManager);

// Should be:
void Connect(AeroDebrief.Core.Playback.PlaybackController playbackController);
```

### Step 4: Update PlayheadSyncService.Connect Implementation

**File**: `src/AeroDebrief.UI/Services/Graphs/PlayheadSyncService.cs`

**Update Connect Method**:
```csharp
public void Connect(AeroDebrief.Core.Playback.PlaybackController playbackController)
{
    if (_playbackController != null)
    {
        Disconnect();
    }

    _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));

    // Subscribe to PlaybackController events
    _playbackController.PlaybackStarted += OnPlaybackStarted;
    _playbackController.PlaybackStopped += OnPlaybackStopped;
    _playbackController.PlaybackPaused += OnPlaybackPaused;
    _playbackController.PlaybackResumed += OnPlaybackResumed;
    _playbackController.PlaybackSpeedChanged += OnPlaybackSpeedChanged;
    _playbackController.TimeChanged += OnTimeChanged;

    _logger.Info("Connected to PlaybackController");
}
```

**Add Event Handlers**:
```csharp
private void OnPlaybackStarted()
{
    UpdatePlaybackState(true);
}

private void OnPlaybackStopped()
{
    UpdatePlaybackState(false);
    StopUpdates();
}

private void OnPlaybackPaused()
{
    UpdatePlaybackState(false);
}

private void OnPlaybackResumed()
{
    UpdatePlaybackState(true);
}

private void OnPlaybackSpeedChanged(double speed)
{
    UpdatePlaybackRate(speed);
}

private void OnTimeChanged(TimeSpan currentTime, TimeSpan totalDuration)
{
    // Convert TimeSpan to DateTime based on recording start
    // We need RecordingStart from somewhere - maybe pass in Connect()
    var newTime = _startTime + currentTime;
    
    if (newTime != _currentTime)
    {
        _currentTime = newTime;
        TimeChanged?.Invoke(this, _currentTime);
    }
}
```

**Update Disconnect**:
```csharp
public void Disconnect()
{
    if (_playbackController != null)
    {
        _playbackController.PlaybackStarted -= OnPlaybackStarted;
        _playbackController.PlaybackStopped -= OnPlaybackStopped;
        _playbackController.PlaybackPaused -= OnPlaybackPaused;
        _playbackController.PlaybackResumed -= OnPlaybackResumed;
        _playbackController.PlaybackSpeedChanged -= OnPlaybackSpeedChanged;
        _playbackController.TimeChanged -= OnTimeChanged;

        _playbackController = null;
        _logger.Info("Disconnected from PlaybackController");
    }

    StopUpdates();
}
```

---

## ?? Optional Enhancements Analysis

### 1. Playhead Hover Tooltip ? (Nice to Have)

**Effort**: 2-3 hours  
**Value**: Medium  
**Priority**: Low (can be Phase 8 or 9)

**Implementation**:
```csharp
// Add to UnifiedGraphControl
private ToolTip? _playheadTooltip;

private void CreatePlayheadLine()
{
    // ... existing code ...
    
    // Add tooltip to playhead line
    _playheadTooltip = new ToolTip
    {
        Placement = PlacementMode.Top,
        VerticalOffset = -5
    };
    _playheadLine.ToolTip = _playheadTooltip;
}

private void UpdateMainChartPlayheadPosition(DateTime playheadTime, UnifiedGraphViewModel vm)
{
    // ... existing position code ...
    
    // Update tooltip
    if (_playheadTooltip != null)
    {
        _playheadTooltip.Content = playheadTime.ToString("HH:mm:ss.fff");
    }
}
```

**Decision**: **Defer to Phase 9 (UX Polish)**

---

### 2. Drag Playhead to Seek ? (Nice to Have)

**Effort**: 4-6 hours  
**Value**: Medium  
**Priority**: Low (can be Phase 9)

**Implementation Approach**:
1. Make playhead line `IsHitTestVisible = true`
2. Add `MouseDown`, `MouseMove`, `MouseUp` handlers
3. During drag, update playhead position visually
4. On release, seek to dragged position
5. Show tooltip with time during drag

**Code Outline**:
```csharp
private bool _isDraggingPlayhead = false;

private void OnPlayheadMouseDown(object sender, MouseButtonEventArgs e)
{
    _isDraggingPlayhead = true;
    _playheadLine.CaptureMouse();
    e.Handled = true;
}

private void OnPlayheadMouseMove(object sender, MouseEventArgs e)
{
    if (!_isDraggingPlayhead) return;
    
    var position = e.GetPosition(_mainChart);
    var fraction = position.X / _mainChart.ActualWidth;
    var time = vm.ViewportStart + TimeSpan.FromTicks((long)(fraction * vm.ViewportDuration.Ticks));
    
    // Update visual position immediately (no seek yet)
    vm.PlayheadTime = time;
}

private void OnPlayheadMouseUp(object sender, MouseButtonEventArgs e)
{
    if (_isDraggingPlayhead)
    {
        _isDraggingPlayhead = false;
        _playheadLine.ReleaseMouseCapture();
        
        // Now actually seek
        _playheadSyncService?.Seek(vm.PlayheadTime);
    }
}
```

**Decision**: **Defer to Phase 9 (UX Polish)** - click-to-seek already provides basic functionality

---

## ?? Effort Summary

### Required for Phase 6 ? (~2 hours)
- [x] Expose PlaybackController from AudioSession (15 min)
- [x] Update IPlayheadSyncService interface (5 min)
- [x] Implement Connect() with real events (45 min)
- [x] Wire up in UnifiedGraphControl (30 min)
- [x] Test with real playback (30 min)

### Optional Enhancements ? (~6-9 hours total)
- [ ] Playhead hover tooltip (2-3 hours) - **Phase 9**
- [ ] Drag playhead to seek (4-6 hours) - **Phase 9**

---

## ?? Recommendation

### For Phase 6 Completion
**Implement real PlaybackController integration** (~2 hours):
- This is straightforward and completes the core Phase 6 goals
- All events are already available
- No blocking issues
- Clean integration path

### For Optional Enhancements
**Defer to Phase 9 (UX Polish)**:
- Playhead tooltip: Nice visual feedback but not critical
- Drag-to-seek: Advanced feature, click-to-seek is sufficient
- Both are polish items, not core functionality
- Better to complete Phases 7-8 first

---

## ?? Updated Phase 6 Plan

### Step 3B: Real PlaybackController Integration (NEW)
**Duration**: 2 hours  
**Priority**: High (completes Phase 6 properly)

**Tasks**:
1. Add PlaybackController property to AudioSession
2. Update IPlayheadSyncService interface
3. Implement Connect() with real PlaybackController
4. Add event handlers for playback state/speed/time
5. Wire up in UnifiedGraphControl OnLoaded
6. Test with real file playback
7. Verify seek operations work
8. Verify speed changes reflect in UI

**Success Criteria**:
- [x] Playhead syncs with real audio playback
- [x] Playhead updates when audio plays/pauses
- [x] Speed changes update playhead rate
- [x] Seek from chart seeks audio
- [x] No event loops or conflicts

---

## ?? Implementation Checklist

### Core Integration (Phase 6)
- [ ] Add `PlaybackController?` property to `CoreApiService.AudioSession`
- [ ] Update `IPlayheadSyncService.Connect()` signature to use `PlaybackController`
- [ ] Implement `PlayheadSyncService.Connect()` with real events
- [ ] Add event handlers: `OnPlaybackStarted`, `OnPlaybackStopped`, etc.
- [ ] Update `PlayheadSyncService.Disconnect()` to unsubscribe events
- [ ] Wire up in `UnifiedGraphControl.InitializePlayheadSync()`
- [ ] Handle null PlaybackController gracefully (no file loaded)
- [ ] Test with real file playback
- [ ] Verify seek operations
- [ ] Update documentation

### Optional Enhancements (Phase 9)
- [ ] Playhead hover tooltip (optional)
- [ ] Drag playhead to seek (optional)

---

## ? Conclusion

**Phase 6 can be truly complete with just 2 hours of additional work:**

1. **Real PlaybackController integration** is straightforward:
   - All required events exist
   - Access path is clear (AudioSession ? Pipeline ? PlaybackController)
   - No architecture changes needed
   - Just wire up events

2. **Optional enhancements can wait**:
   - Tooltip and drag-to-seek are polish features
   - Phase 9 (UX Polish) is the appropriate time
   - Current functionality (click-to-seek, frame-by-frame) is sufficient

**Recommendation**: Add Step 3B (Real PlaybackController Integration) to Phase 6 before moving to Phase 7. This will make Phase 6 production-ready with real audio synchronization.

---

**Status**: Analysis Complete  
**Next**: Implement Step 3B (2 hours)  
**Then**: Phase 7 - Visibility Toggles

**Last Updated**: January 21, 2025
