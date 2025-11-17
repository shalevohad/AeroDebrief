# Phase 7: Visibility Toggles (Full Wiring) - Implementation Plan

## ?? Overview

**Phase**: 7 of 11  
**Status**: ?? ~75% Complete (Steps 1-3 done)  
**Prerequisites**: ? All met (Phases 0-6 complete)  
**Estimated Duration**: 1-2 days  
**Complexity**: Medium

**Progress Tracking**:
- ? Step 1: Event Infrastructure (Complete)
- ? Step 2: Visibility Toggle Logic (Complete)
- ? Step 3: Audio Synchronization (Complete - NEW!)
- ? Step 4: UI Integration & Testing (In Progress)

**See Also**:
- [Step 1 & 2 Complete](./Phase7-Step1-2-Complete.md)
- [Step 3 Complete - Audio Sync](./Phase7-Step3-Complete.md)

---

## ?? Objectives

Complete integration between chart series visibility and FrequencyTree selection state, enabling instant show/hide of frequencies and pilots without data reloading.

### Key Goals
1. Wire chart series to FrequencyTree selection changes
2. Synchronize chart visibility with audio mute/solo state
3. Enable instant visibility toggles (no data reload)
4. Support frequency-level and pilot-level visibility
5. Maintain consistency across UI and audio

---

## ??? Architecture

### Current State (Phase 4)
```
FrequencyManager (existing)
    ? manages
Frequency/Pilot State
    ?
Series in ViewModel._allSeries
    ?
No visibility synchronization
```

### Target State (Phase 7)
```
FrequencyTree Selection (UI)
    ? events
FrequencyManager
    ? visibility change events
UnifiedGraphViewModel
    ? update series.IsVisible
Chart Series (LiveCharts2)
    ? visual update (instant)
User sees changes

Bidirectional Sync:
MixerController (audio mute/solo)
    ? syncs with ?
Chart Visibility
```

---

## ?? Implementation Steps

### Step 1: Event Infrastructure (4 hours)

#### 1.1 Identify Existing Events
**Search for**:
- FrequencyManager events (FrequencyAdded, FrequencyRemoved, etc.)
- FrequencyTree selection events
- MixerController mute/solo events

**Files to check**:
- `src/AeroDebrief.UI/Services/FrequencyManager.cs`
- `src/AeroDebrief.UI/Controls/FrequencyTree.cs` or similar
- `src/AeroDebrief.UI/Services/MixerController.cs`

#### 1.2 Add Missing Events (if needed)
If events don't exist, add them:

**FrequencyManager.cs**:
```csharp
public event EventHandler<FrequencyVisibilityChangedEventArgs>? FrequencyVisibilityChanged;
public event EventHandler<PilotVisibilityChangedEventArgs>? PilotVisibilityChanged;

public class FrequencyVisibilityChangedEventArgs : EventArgs
{
    public double Frequency { get; set; }
    public bool IsVisible { get; set; }
}

public class PilotVisibilityChangedEventArgs : EventArgs
{
    public double Frequency { get; set; }
    public string PilotId { get; set; }
    public bool IsVisible { get; set; }
}
```

#### 1.3 Subscribe in UnifiedGraphViewModel
**File**: `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`

```csharp
private void InitializeFrequencyManager(FrequencyManager frequencyManager)
{
    _frequencyManager = frequencyManager;
    
    // Subscribe to visibility events
    _frequencyManager.FrequencyVisibilityChanged += OnFrequencyVisibilityChanged;
    _frequencyManager.PilotVisibilityChanged += OnPilotVisibilityChanged;
    
    _logger.Debug("Phase 7: Subscribed to FrequencyManager visibility events");
}
```

**Deliverables**:
- [ ] Event definitions (if needed)
- [ ] Event subscriptions in ViewModel
- [ ] Unsubscription in Dispose()

---

### Step 2: Visibility Toggle Logic (3 hours)

#### 2.1 Series Key Format
Define consistent key format for series identification:

```csharp
// Frequency-level key: "freq:251.0"
// Pilot-level key: "pilot:251.0:SHARK-1-1"

private string GetFrequencyKey(double frequency)
{
    return $"freq:{frequency:F1}";
}

private string GetPilotKey(double frequency, string pilotId)
{
    return $"pilot:{frequency:F1}:{pilotId}";
}
```

#### 2.2 Visibility State Tracking
**File**: `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`

```csharp
// Phase 7: Visibility state tracking
private readonly Dictionary<string, bool> _seriesVisibility = new();

/// <summary>
/// Phase 7: Sets visibility for a specific series.
/// Updates LiveCharts2 series IsVisible property instantly (no data reload).
/// </summary>
public void SetSeriesVisibility(string seriesKey, bool isVisible)
{
    // Update state dictionary
    _seriesVisibility[seriesKey] = isVisible;
    
    // Find matching series in _allSeries
    var matchingSeries = _allSeries.Where(s => 
        s.Name?.Contains(seriesKey) == true).ToList();
    
    foreach (var series in matchingSeries)
    {
        series.IsVisible = isVisible;
        _logger.Trace($"Series visibility: {series.Name} = {isVisible}");
    }
    
    // Raise property changed for Series collection
    OnPropertyChanged(nameof(Series));
}
```

#### 2.3 Frequency-Level Visibility
```csharp
/// <summary>
/// Phase 7: Sets visibility for all series of a frequency.
/// </summary>
public void SetFrequencyVisibility(double frequency, bool isVisible)
{
    var key = GetFrequencyKey(frequency);
    
    // Get all pilot IDs for this frequency
    if (_frequencyPilots.TryGetValue(key, out var pilots))
    {
        // Set visibility for each pilot
        foreach (var pilotId in pilots)
        {
            var pilotKey = GetPilotKey(frequency, pilotId);
            SetSeriesVisibility(pilotKey, isVisible);
        }
    }
    
    _logger.Debug($"Frequency visibility: {frequency:F1} MHz = {isVisible}");
}
```

#### 2.4 Pilot-Level Visibility
```csharp
/// <summary>
/// Phase 7: Sets visibility for a specific pilot's series.
/// </summary>
public void SetPilotVisibility(double frequency, string pilotId, bool isVisible)
{
    var key = GetPilotKey(frequency, pilotId);
    SetSeriesVisibility(key, isVisible);
    
    _logger.Debug($"Pilot visibility: {pilotId} on {frequency:F1} MHz = {isVisible}");
}
```

**Deliverables**:
- [ ] Series key format utilities
- [ ] SetSeriesVisibility() method
- [ ] SetFrequencyVisibility() method
- [ ] SetPilotVisibility() method
- [ ] State tracking dictionary

---

### Step 3: Audio Synchronization (3 hours)

#### 3.1 Find MixerController
**Search for**:
- MixerController instance in UnifiedPlayerViewModel
- Mute/Solo methods
- Existing event system

#### 3.2 Bidirectional Sync
**Chart ? Audio** (user hides series, audio should mute):
```csharp
public void SetFrequencyVisibility(double frequency, bool isVisible)
{
    // ... existing code ...
    
    // Phase 7: Sync with audio
    if (_mixerController != null)
    {
        _mixerController.SetFrequencyMute(frequency, !isVisible);
    }
}
```

**Audio ? Chart** (user mutes audio, chart should hide):
```csharp
private void OnAudioMuteChanged(double frequency, bool isMuted)
{
    // Prevent event loop
    if (_isSyncingAudio) return;
    
    try
    {
        _isSyncingAudio = true;
        SetFrequencyVisibility(frequency, !isMuted);
    }
    finally
    {
        _isSyncingAudio = false;
    }
}
```

#### 3.3 Solo Support
**Solo Logic**:
```csharp
private void OnAudioSoloChanged(double frequency, bool isSolo)
{
    if (_isSyncingAudio) return;
    
    try
    {
        _isSyncingAudio = true;
        
        if (isSolo)
        {
            // Hide all other frequencies
            foreach (var freq in _frequencyPilots.Keys)
            {
                bool shouldBeVisible = freq.Contains($"freq:{frequency:F1}");
                // Extract frequency from key and set visibility
            }
        }
        else
        {
            // Check if any other frequency is still solo
            bool anySolo = _mixerController.HasAnySolo();
            if (!anySolo)
            {
                // Show all frequencies
                foreach (var freq in _frequencyPilots.Keys)
                {
                    SetFrequencyVisibility(/* parse freq */, true);
                }
            }
        }
    }
    finally
    {
        _isSyncingAudio = false;
    }
}
```

**Deliverables**:
- [ ] MixerController integration
- [ ] Chart ? Audio sync
- [ ] Audio ? Chart sync
- [ ] Event loop prevention
- [ ] Solo support

---

### Step 4: Testing (4 hours)

#### 4.1 Unit Tests
**File**: `tests/AeroDebrief.Tests/ViewModels/UnifiedGraphViewModelPhase7Tests.cs`

**Test Cases**:
```csharp
[Fact]
public void SetFrequencyVisibility_UpdatesSeriesVisibility()
{
    // Arrange
    var vm = CreateViewModelWithSeries();
    
    // Act
    vm.SetFrequencyVisibility(251.0, false);
    
    // Assert
    var series251 = vm.Series.Where(s => s.Name.Contains("251.0"));
    Assert.All(series251, s => Assert.False(s.IsVisible));
}

[Fact]
public void SetPilotVisibility_UpdatesSinglePilot()
{
    // Arrange
    var vm = CreateViewModelWithSeries();
    
    // Act
    vm.SetPilotVisibility(251.0, "SHARK-1-1", false);
    
    // Assert
    var pilotSeries = vm.Series.FirstOrDefault(s => 
        s.Name.Contains("251.0") && s.Name.Contains("SHARK-1-1"));
    Assert.NotNull(pilotSeries);
    Assert.False(pilotSeries.IsVisible);
}

[Fact]
public void SetFrequencyVisibility_SyncsWithAudioMute()
{
    // Arrange
    var vm = CreateViewModelWithMixer();
    
    // Act
    vm.SetFrequencyVisibility(251.0, false);
    
    // Assert
    Assert.True(vm.MixerController.IsFrequencyMuted(251.0));
}

[Fact]
public void VisibilityToggle_RapidChanges_HandlesCorrectly()
{
    // Arrange
    var vm = CreateViewModelWithSeries();
    
    // Act - Rapid toggles
    for (int i = 0; i < 100; i++)
    {
        vm.SetFrequencyVisibility(251.0, i % 2 == 0);
    }
    
    // Assert - Should end visible
    var series251 = vm.Series.Where(s => s.Name.Contains("251.0"));
    Assert.All(series251, s => Assert.False(s.IsVisible));
}

[Fact]
public void SetVisibility_PreventEventLoop()
{
    // Arrange
    var vm = CreateViewModelWithMixer();
    int eventCount = 0;
    vm.SeriesVisibilityChanged += (s, e) => eventCount++;
    
    // Act
    vm.SetFrequencyVisibility(251.0, false);
    
    // Assert - Should only fire once, not loop
    Assert.Equal(1, eventCount);
}
```

#### 4.2 Integration Tests
**Test with real FrequencyTree UI**:
- Manual testing: Check/uncheck frequencies
- Verify chart updates instantly
- Verify audio mute syncs
- Test with 20+ pilots per frequency

#### 4.3 Performance Tests
```csharp
[Fact]
public void VisibilityToggle_HighPilotCount_PerformsWell()
{
    // Arrange
    var vm = CreateViewModelWithManySeries(60); // 60 frequencies
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    vm.SetFrequencyVisibility(251.0, false);
    stopwatch.Stop();
    
    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 50, "Toggle should be < 50ms");
}
```

**Deliverables**:
- [ ] 10-15 unit tests
- [ ] Integration test plan
- [ ] Performance validation
- [ ] Edge case coverage

---

## ?? Success Criteria

### Functional ?
- [ ] Chart series visibility toggles with FrequencyTree
- [ ] Audio mute state syncs with chart visibility
- [ ] Pilot show/hide works instantly
- [ ] Frequency show/hide works instantly
- [ ] No data reload on visibility change
- [ ] Consistent state across UI and audio
- [ ] No event loops

### Performance ?
- [ ] Toggle operation < 50ms
- [ ] Handles rapid toggles smoothly
- [ ] No memory leaks
- [ ] Works with 60+ frequencies
- [ ] Works with 20+ pilots per frequency

### UX ?
- [ ] Instant visual feedback
- [ ] No flicker or jitter
- [ ] Smooth transitions
- [ ] Clear indication of state
- [ ] Consistent with rest of UI

---

## ?? Technical Considerations

### 1. Event Loop Prevention
**Problem**: Chart change ? Audio change ? Chart change ? ...

**Solution**: Use flag
```csharp
private bool _isSyncingAudio = false;

private void OnAudioMuteChanged(...)
{
    if (_isSyncingAudio) return;
    try
    {
        _isSyncingAudio = true;
        // Update chart
    }
    finally
    {
        _isSyncingAudio = false;
    }
}
```

### 2. Series Lookup Performance
**Problem**: Finding series by name in large collections

**Solution**: Cache series references
```csharp
private readonly Dictionary<string, List<ISeries>> _seriesCache = new();
```

### 3. Batch Updates
**Problem**: Setting 20 pilots invisible one at a time = 20 visual updates

**Solution**: Batch operation
```csharp
public void SetFrequencyVisibility(double frequency, bool isVisible)
{
    // Collect all changes
    var changesToApply = new List<(ISeries, bool)>();
    
    // Apply all at once
    foreach (var (series, visible) in changesToApply)
    {
        series.IsVisible = visible;
    }
    
    // Single property changed notification
    OnPropertyChanged(nameof(Series));
}
```

---

## ?? Files to Create/Modify

### Production Code (2-3 files)
1. **`src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`** (MODIFY)
   - Add visibility state tracking
   - Add SetSeriesVisibility() method
   - Add SetFrequencyVisibility() method
   - Add SetPilotVisibility() method
   - Add audio sync methods
   - Subscribe to events
   - +150-200 lines

2. **`src/AeroDebrief.UI/Services/FrequencyManager.cs`** (MODIFY if needed)
   - Add visibility events (if missing)
   - +20-50 lines

3. **`src/AeroDebrief.UI/Services/MixerController.cs`** (CHECK)
   - Verify mute/solo event system exists
   - Add events if needed

### Test Code (1 file)
1. **`tests/AeroDebrief.Tests/ViewModels/UnifiedGraphViewModelPhase7Tests.cs`** (NEW)
   - 10-15 unit tests
   - Visibility toggle tests
   - Audio sync tests
   - Performance tests
   - ~300-400 lines

### Documentation (2-3 files)
1. **`docs/Phase7-Implementation-Plan.md`** (this document)
2. **`docs/Phase7-Complete-Summary.md`** (when done)
3. **Update**: `docs/AeroDebrief-Rewrite-Plan-LiveCharts2.md`

---

## ?? Known Challenges

### Challenge 1: Finding FrequencyManager Events
**Issue**: May need to add visibility events to FrequencyManager  
**Solution**: Check existing event system, add if needed  
**Risk**: Low - straightforward event implementation

### Challenge 2: Series Identification
**Issue**: Matching series to frequencies/pilots  
**Solution**: Use consistent naming convention from Phase 4  
**Risk**: Low - already established in Phase 4

### Challenge 3: Audio Sync Timing
**Issue**: Chart update may happen before/after audio update  
**Solution**: Event-driven sync, not timing-dependent  
**Risk**: Low - events are atomic

### Challenge 4: High Pilot Count
**Issue**: 60 frequencies × 20 pilots = 1200 series  
**Solution**: Batch updates, efficient lookups  
**Risk**: Medium - needs performance testing

---

## ?? Progress Tracking

### Step 1: Event Infrastructure ? COMPLETE
- [x] Identify existing events (FrequencyManager.SelectionChanged exists)
- [x] Series key format defined (GetFrequencyKey, GetPilotKey)
- [x] Event loop prevention implemented (_isSyncingVisibility flag)

### Step 2: Visibility Logic ? COMPLETE
- [x] Series key format (GetFrequencyKey, GetPilotKey methods)
- [x] SetSeriesVisibility() method
- [x] SetFrequencyVisibility() method
- [x] SetPilotVisibility() method
- [x] GetFrequencyVisibility() method
- [x] GetPilotVisibility() method
- [x] State tracking (_seriesVisibility dictionary)
- [x] 14 unit tests passing

### Step 3: Audio Sync ? COMPLETE
- [x] MixerController integration
- [x] Chart ? Audio sync
- [x] Audio ? Chart sync
- [x] Solo support

### Step 4: Testing ?
- [x] Unit tests (14 tests created and passing)
- [ ] Integration tests with real UI
- [ ] Performance tests
- [ ] Edge cases

---

## ?? Definition of Done

- [ ] Chart series visibility toggles correctly
- [ ] Audio mute/solo syncs with chart
- [ ] All unit tests passing (10-15 new tests)
- [ ] Performance < 50ms per toggle
- [ ] No memory leaks
- [ ] No event loops
- [ ] Build successful
- [ ] Documentation complete
- [ ] Code reviewed
- [ ] Ready for Phase 8

---

**Status**: ? Phase 7 In Progress  
**Next Step**: Step 1 - Event Infrastructure  
**Estimated Completion**: 1-2 days

**Last Updated**: January 21, 2025  
**Phase**: 7 of 11 (55% ? 64%)  
**Branch**: `livechart2-integration`
