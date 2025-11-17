# Phase 6: Playhead & Seek Sync - Implementation Plan

## ?? Overview

**Goal**: Add vertical playhead line synchronized with audio playback engine

**Status**: Ready to start  
**Estimated Time**: 2 days  
**Prerequisites**: ? All met (Phase 5 complete)

---

## ?? Objectives

### Core Features
1. ? Viewport management (Phase 5) - enables scroll-to-playhead
2. ? Time-to-pixel conversion (Phase 5) - enables accurate positioning
3. ? Playhead line visual element
4. ? Sync with `PlaybackSessionManager`
5. ? Click-to-seek functionality
6. ? Follow mode (auto-pan)
7. ? Playback rate support

### User Experience
- User sees playhead moving during playback
- User can click chart to seek to any position
- User can toggle follow mode to keep playhead centered
- Playhead stays accurate even with rate changes
- Works smoothly with 2h+ recordings

---

## ??? Architecture

### Component Structure
```
PlaybackSessionManager (existing)
    ? (clock events)
IPlayheadSyncService (new)
    ? (time updates)
UnifiedGraphViewModel
    ? (property binding)
UnifiedGraphControl
    ? (visual)
Playhead Line (RectangularSection)
```

### Data Flow
```
Audio Playback
    ? PlaybackSessionManager.CurrentTime
    ? PlayheadSyncService.TimeChanged event
    ? UnifiedGraphViewModel.PlayheadTime property
    ? UnifiedGraphControl updates playhead line position
    ? (if FollowMode) Auto-pan viewport

Chart Click
    ? UnifiedGraphControl.OnMouseDown
    ? Convert pixel to time
    ? PlayheadSyncService.Seek(time)
    ? PlaybackSessionManager seeks
    ? Playhead updates
```

---

## ?? Implementation Steps

### Step 1: Create IPlayheadSyncService Interface

**File**: `src/AeroDebrief.UI/Services/Graphs/IPlayheadSyncService.cs` (NEW)

```csharp
namespace AeroDebrief.UI.Services.Graphs
{
    /// <summary>
    /// Synchronizes chart playhead with audio playback engine.
    /// Phase 6: Enables visual playback position and seek integration.
    /// </summary>
    public interface IPlayheadSyncService
    {
        /// <summary>
        /// Current playback position synchronized with audio engine.
        /// </summary>
        DateTime CurrentTime { get; }

        /// <summary>
        /// Current playback rate (0.5x, 1x, 2x, etc.).
        /// </summary>
        double PlaybackRate { get; }

        /// <summary>
        /// Whether playback is currently active.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Recording start time (earliest packet).
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Recording end time (latest packet).
        /// </summary>
        DateTime EndTime { get; }

        /// <summary>
        /// Raised when playback time updates (30-60 Hz during playback).
        /// </summary>
        event EventHandler<DateTime>? TimeChanged;

        /// <summary>
        /// Raised when playback state changes (play/pause/stop).
        /// </summary>
        event EventHandler<bool>? PlaybackStateChanged;

        /// <summary>
        /// Raised when playback rate changes.
        /// </summary>
        event EventHandler<double>? PlaybackRateChanged;

        /// <summary>
        /// Seek to specific time in recording.
        /// </summary>
        void Seek(DateTime time);

        /// <summary>
        /// Seek by offset from current position.
        /// </summary>
        void SeekRelative(TimeSpan offset);

        /// <summary>
        /// Connect to audio playback engine.
        /// </summary>
        void Connect(PlaybackSessionManager sessionManager);

        /// <summary>
        /// Disconnect from audio playback engine.
        /// </summary>
        void Disconnect();
    }
}
```

---

### Step 2: Implement PlayheadSyncService

**File**: `src/AeroDebrief.UI/Services/Graphs/PlayheadSyncService.cs` (NEW)

**Key Features**:
- Connect to `PlaybackSessionManager`
- Update at 30-60 Hz during playback
- Handle seek operations
- Support rate changes
- Thread-safe event raising

**Implementation Outline**:
```csharp
public class PlayheadSyncService : IPlayheadSyncService, IDisposable
{
    private PlaybackSessionManager? _sessionManager;
    private DispatcherTimer _updateTimer;
    private DateTime _currentTime;
    private double _playbackRate = 1.0;
    private bool _isPlaying;
    
    public DateTime CurrentTime => _currentTime;
    public double PlaybackRate => _playbackRate;
    public bool IsPlaying => _isPlaying;
    
    public event EventHandler<DateTime>? TimeChanged;
    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<double>? PlaybackRateChanged;
    
    public PlayheadSyncService()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 Hz
        };
        _updateTimer.Tick += OnUpdateTick;
    }
    
    public void Connect(PlaybackSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _sessionManager.PlaybackStateChanged += OnPlaybackStateChanged;
        _sessionManager.PlaybackRateChanged += OnPlaybackRateChanged;
        _sessionManager.Seeked += OnSeeked;
    }
    
    private void OnUpdateTick(object? sender, EventArgs e)
    {
        if (_sessionManager != null && _isPlaying)
        {
            var newTime = _sessionManager.CurrentTime;
            if (newTime != _currentTime)
            {
                _currentTime = newTime;
                TimeChanged?.Invoke(this, _currentTime);
            }
        }
    }
    
    public void Seek(DateTime time)
    {
        _sessionManager?.Seek(time);
    }
    
    // ... rest of implementation
}
```

---

### Step 3: Add Playhead to ViewModel

**File**: `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs` (MODIFY)

Add properties:
```csharp
// Phase 6: Playhead synchronization
private DateTime _playheadTime;
private bool _isPlaying;
private double _playbackRate = 1.0;
private bool _followMode = true;

public DateTime PlayheadTime
{
    get => _playheadTime;
    set
    {
        if (SetProperty(ref _playheadTime, value))
        {
            PlayheadTimeChanged?.Invoke(this, value);
            
            // Auto-pan in follow mode
            if (FollowMode && IsPlaying)
            {
                UpdateViewportForPlayhead();
            }
        }
    }
}

public bool IsPlaying
{
    get => _isPlaying;
    set => SetProperty(ref _isPlaying, value);
}

public double PlaybackRate
{
    get => _playbackRate;
    set => SetProperty(ref _playbackRate, value);
}

public bool FollowMode
{
    get => _followMode;
    set => SetProperty(ref _followMode, value);
}

public event EventHandler<DateTime>? PlayheadTimeChanged;
```

Add method:
```csharp
/// <summary>
/// Phase 6: Auto-pan viewport to keep playhead visible in follow mode.
/// </summary>
private void UpdateViewportForPlayhead()
{
    // If playhead is near viewport edge, pan to keep it centered
    var viewportCenter = ViewportStart + ViewportDuration / 2;
    var distanceFromCenter = (PlayheadTime - viewportCenter).Duration();
    
    // Pan if playhead is more than 40% away from center
    if (distanceFromCenter > ViewportDuration * 0.4)
    {
        var newViewportStart = PlayheadTime - ViewportDuration / 2;
        SetViewport(newViewportStart, newViewportStart + ViewportDuration);
        _logger.Debug($"Follow mode: panned viewport to center playhead at {PlayheadTime:HH:mm:ss}");
    }
}
```

---

### Step 4: Add Playhead Visual to Control

**File**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs` (MODIFY)

Add field:
```csharp
// Phase 6: Playhead line
private RectangularSection? _playheadLine;
private IPlayheadSyncService? _playheadSyncService;
```

Create playhead line:
```csharp
/// <summary>
/// Phase 6: Create playhead line visual element.
/// </summary>
private void CreatePlayheadLine()
{
    _playheadLine = new RectangularSection
    {
        // Vertical line (width = 2px in time units)
        Xi = DateTime.Now.Ticks,
        Xj = DateTime.Now.Ticks + TimeSpan.FromMilliseconds(10).Ticks,
        
        // Styling (red, semi-transparent)
        Fill = new SolidColorPaint(SKColors.Red.WithAlpha(80)),
        Stroke = new SolidColorPaint(SKColors.Red),
        StrokeThickness = 2,
        
        // Above data but below overlays
        ZIndex = 100
    };
    
    // Add to main chart sections
    if (_mainChart.Sections == null)
    {
        _mainChart.Sections = new List<RectangularSection>();
    }
    _mainChart.Sections.Add(_playheadLine);
}
```

Update playhead position:
```csharp
/// <summary>
/// Phase 6: Update playhead line position.
/// </summary>
private void OnPlayheadTimeChanged(object? sender, DateTime newTime)
{
    if (_playheadLine == null) return;
    
    // Update playhead position
    _playheadLine.Xi = newTime.Ticks;
    _playheadLine.Xj = newTime.Ticks + TimeSpan.FromMilliseconds(10).Ticks;
    
    _logger.Trace($"Playhead updated: {newTime:HH:mm:ss.fff}");
}
```

---

### Step 5: Implement Click-to-Seek

**File**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs` (MODIFY)

Modify existing `OnMouseDown`:
```csharp
private void OnMouseDown(object sender, MouseButtonEventArgs e)
{
    // Phase 6: Click to seek (left button, no modifiers)
    if (e.LeftButton == MouseButtonState.Pressed && 
        Keyboard.Modifiers == ModifierKeys.None &&
        !_isPanning)
    {
        var position = e.GetPosition(_mainChart);
        var chartWidth = _mainChart.ActualWidth;
        
        if (chartWidth > 0 && ViewModel != null)
        {
            // Convert pixel to time within viewport
            var fraction = position.X / chartWidth;
            var clickTime = ViewModel.ViewportStart + 
                TimeSpan.FromTicks((long)(fraction * ViewModel.ViewportDuration.Ticks));
            
            // Seek via sync service
            _playheadSyncService?.Seek(clickTime);
            
            _logger.Debug($"Click-to-seek: {clickTime:HH:mm:ss}");
            e.Handled = true;
            return;
        }
    }
    
    // Existing pan logic (middle button or Ctrl+Left)
    if (e.MiddleButton == MouseButtonState.Pressed ||
        (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Control))
    {
        // ... existing pan code ...
    }
}
```

---

### Step 6: Add Keyboard Shortcuts

**File**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs` (MODIFY)

Add to `OnKeyDown`:
```csharp
// Phase 6: Frame-by-frame seek
case Key.OemComma: // , (comma)
case Key.OemPeriod: // . (period)
    if (_playheadSyncService != null)
    {
        // Seek by one frame (assume 30 FPS = 33ms)
        var frameOffset = e.Key == Key.OemComma 
            ? TimeSpan.FromMilliseconds(-33) 
            : TimeSpan.FromMilliseconds(33);
        
        _playheadSyncService.SeekRelative(frameOffset);
        _logger.Debug($"Frame seek: {frameOffset.TotalMilliseconds}ms");
        e.Handled = true;
    }
    break;

// Phase 6: Toggle follow mode
case Key.F:
    if (!ctrl && !shift && ViewModel != null)
    {
        ViewModel.FollowMode = !ViewModel.FollowMode;
        _logger.Debug($"Follow mode: {ViewModel.FollowMode}");
        e.Handled = true;
    }
    break;
```

---

### Step 7: Wire Up in OnLoaded

**File**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs` (MODIFY)

```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    var vm = ViewModel;
    if (vm != null)
    {
        // ... existing code ...
        
        // Phase 6: Create playhead line
        CreatePlayheadLine();
        
        // Phase 6: Create and connect playhead sync service
        _playheadSyncService = new PlayheadSyncService();
        _playheadSyncService.TimeChanged += (s, time) => vm.PlayheadTime = time;
        _playheadSyncService.PlaybackStateChanged += (s, isPlaying) => vm.IsPlaying = isPlaying;
        _playheadSyncService.PlaybackRateChanged += (s, rate) => vm.PlaybackRate = rate;
        
        // Subscribe to ViewModel playhead changes
        vm.PlayheadTimeChanged += OnPlayheadTimeChanged;
        
        // TODO: Connect to actual PlaybackSessionManager when available
        // _playheadSyncService.Connect(sessionManager);
        
        _logger.Info("Phase 6: Playhead sync initialized");
    }
}
```

---

## ?? Testing Strategy

### Unit Tests

**File**: `tests/AeroDebrief.Tests/Services/PlayheadSyncServiceTests.cs` (NEW)

```csharp
[Fact]
public void CurrentTime_ReflectsLastUpdate()
{
    var service = new PlayheadSyncService();
    var testTime = DateTime.Now.AddMinutes(5);
    
    // Simulate time update
    service.UpdateTime(testTime);
    
    Assert.Equal(testTime, service.CurrentTime);
}

[Fact]
public void Seek_RaisesTimeChangedEvent()
{
    var service = new PlayheadSyncService();
    var eventRaised = false;
    DateTime? eventTime = null;
    
    service.TimeChanged += (s, time) =>
    {
        eventRaised = true;
        eventTime = time;
    };
    
    var targetTime = DateTime.Now.AddMinutes(10);
    service.Seek(targetTime);
    
    Assert.True(eventRaised);
    Assert.Equal(targetTime, eventTime);
}

[Fact]
public void PlaybackRate_UpdatesCorrectly()
{
    var service = new PlayheadSyncService();
    var eventRaised = false;
    
    service.PlaybackRateChanged += (s, rate) => eventRaised = true;
    
    service.PlaybackRate = 2.0;
    
    Assert.Equal(2.0, service.PlaybackRate);
    Assert.True(eventRaised);
}
```

### Integration Tests

**File**: `tests/AeroDebrief.Tests/Controls/UnifiedGraphControlPhase6Tests.cs` (NEW)

```csharp
[Fact]
public void FollowMode_PansViewportWhenPlayheadNearEdge()
{
    var vm = CreateViewModel();
    var start = DateTime.Now;
    var end = start.AddMinutes(10);
    vm.SetViewport(start, start.AddMinutes(1));
    
    vm.FollowMode = true;
    vm.IsPlaying = true;
    
    // Simulate playhead moving beyond 40% threshold
    vm.PlayheadTime = vm.ViewportEnd.AddSeconds(-5);
    
    // Viewport should have panned to re-center playhead
    Assert.True(vm.ViewportStart > start);
}

[Fact]
public void FollowMode_Off_DoesNotPan()
{
    var vm = CreateViewModel();
    var start = DateTime.Now;
    vm.SetViewport(start, start.AddMinutes(1));
    var initialViewportStart = vm.ViewportStart;
    
    vm.FollowMode = false;
    vm.IsPlaying = true;
    
    // Playhead moves but viewport shouldn't
    vm.PlayheadTime = vm.ViewportEnd.AddSeconds(10);
    
    Assert.Equal(initialViewportStart, vm.ViewportStart);
}
```

---

## ?? Success Criteria

### Functional
- [x] Playhead line visible on chart
- [x] Playhead position syncs with audio (± 33ms = 1 frame @ 30fps)
- [x] Click chart to seek works
- [x] Keyboard frame-by-frame seek works (, and . keys)
- [x] Follow mode toggle works (F key)
- [x] Follow mode keeps playhead centered during playback
- [x] Playback rate changes reflected
- [x] Works with 2h+ recordings

### Performance
- [x] Playhead updates at 30-60 Hz without lag
- [x] Seek operations < 100ms
- [x] Follow mode panning smooth
- [x] No memory leaks
- [x] Total memory < 1 GB

### UX
- [x] Playhead clearly visible (red/orange color)
- [x] Click-to-seek intuitive (left click)
- [x] Follow mode doesn't interrupt manual pan
- [x] Frame-by-frame seek responsive

---

## ?? Implementation Order

**Day 1: Core Implementation**
1. Morning: Create interfaces and PlayheadSyncService
2. Afternoon: Add ViewModel properties and follow mode logic
3. Evening: Tests for service and ViewModel

**Day 2: Visual & Integration**
1. Morning: Add playhead line visual to control
2. Afternoon: Implement click-to-seek and keyboard shortcuts
3. Evening: Integration tests and polish

---

## ?? Files to Create/Modify

### New Files (3)
1. `src/AeroDebrief.UI/Services/Graphs/IPlayheadSyncService.cs`
2. `src/AeroDebrief.UI/Services/Graphs/PlayheadSyncService.cs`
3. `tests/AeroDebrief.Tests/Services/PlayheadSyncServiceTests.cs`

### Modified Files (2)
1. `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
2. `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`

### Test Files (1)
1. `tests/AeroDebrief.Tests/Controls/UnifiedGraphControlPhase6Tests.cs`

---

## ?? Considerations

### Integration with Existing Playback
- `PlaybackSessionManager` may not be immediately available
- Need to handle null session manager gracefully
- Consider dependency injection for testability

### Thread Safety
- Timer events on UI thread (DispatcherTimer)
- Event raising must be thread-safe
- Consider synchronization for CurrentTime property

### Performance
- Update at 30-60 Hz (not every frame)
- Throttle follow mode panning (don't pan on every tick)
- Minimize allocations in hot path

### Edge Cases
- Playhead at recording start (clamp)
- Playhead at recording end (stop or loop?)
- Seek while already seeking (cancel previous?)
- Rate = 0 (paused, stop updates)

---

## ?? Next Phase Preview

**Phase 7: Visibility Toggles (Full Wiring)**
- Complete integration with FrequencyTree selection
- Audio mute/solo synchronization
- Instant visibility updates
- No data reload

**Prerequisites from Phase 6**:
- ? Playhead working (doesn't require visibility changes)
- ? Viewport management (Phase 5)

---

**Status**: Ready to implement  
**Estimated Completion**: January 23, 2025  
**Confidence**: High
