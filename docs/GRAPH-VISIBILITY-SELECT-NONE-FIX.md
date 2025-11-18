# Graph Visibility "Select None" Fix

## Problem Description

When clicking "Select None" to hide all frequencies, the graph was **not clearing** - series remained visible even though the visibility state was updated in the `_seriesVisibility` dictionary.

### Root Cause Analysis

The issue had multiple layers:

1. **Dictionary vs Series Object State Mismatch**
   - `SetFrequencyVisible()` updated `_seriesVisibility` dictionary correctly (setting all to `false`)
   - BUT it **didn't update `IsVisible` property** on the actual `ISeries` objects in the `Series` collection
   - The series objects still had `IsVisible = true` from initial load

2. **RebuildVisibleSeries Logic**
   - When `RebuildVisibleSeries()` ran, it tried to match series names against `_seriesVisibility` keys
   - For matched series, it correctly set `IsVisible` from the dictionary
   - BUT it then **cleared and re-added the series** which triggered collection change events
   - LiveCharts2 would re-render using the series' current `IsVisible` state

3. **The Fundamental Problem**
   ```csharp
   // Before fix - SetFrequencyVisible only updated dictionary:
   _seriesVisibility[matchingKey] = visible;  // ? Dictionary updated
   
   // Series object still had:
   series.IsVisible = true;  // ? NOT updated!
   
   // Then RebuildVisibleSeries would find the series and read:
   if (series.IsVisible)  // Still true!
       visibleCount++;
   ```

## The Solution

### Step 1: Update Series Objects Directly

In `SetFrequencyVisible()` and `SetPilotVisible()`, **explicitly set `IsVisible` on the series objects** before calling `RebuildVisibleSeries()`:

```csharp
// Update dictionary
_seriesVisibility[matchingKey] = visible;

// CRITICAL FIX: Also update the actual series object
var series = Series.FirstOrDefault(s => 
    s.Name == matchingKey || 
    s.Name.Contains(actualFreqKey) && s.Name.Contains(pilotId));

if (series != null)
{
    series.IsVisible = visible;  // ? THE KEY FIX
    _logger.Debug($"Updated series.IsVisible: {series.Name} = {visible}");
}
```

### Step 2: Force Collection Refresh in RebuildVisibleSeries

Keep the existing collection clear/re-add logic to ensure LiveCharts2 detects changes:

```csharp
if (anyChanges)
{
    // 1. Notify that Series collection changed
    OnPropertyChanged(nameof(Series));
    
    // 2. Clear and re-add to force collection change detection
    var seriesArray = Series.ToArray();
    Series.Clear();
    foreach (var s in seriesArray)
    {
        Series.Add(s);
    }
}
```

## Implementation Details

### Modified Methods

1. **SetFrequencyVisible(string frequencyId, bool visible)**
   - Updates `_seriesVisibility` dictionary ?
   - **NEW:** Finds matching series objects and updates their `IsVisible` property ?
   - Calls `RebuildVisibleSeries()` ?
   - Syncs to audio mixer ?

2. **SetPilotVisible(string frequencyId, string pilotId, bool visible)**
   - Updates `_seriesVisibility` dictionary ?
   - **NEW:** Finds matching series object and updates its `IsVisible` property ?
   - Calls `RebuildVisibleSeries()` ?
   - Syncs to audio mixer ?

3. **RebuildVisibleSeries()**
   - Tracks if any visibility changes occurred ?
   - Updates `IsVisible` on series based on `_seriesVisibility` ?
   - **If changes detected:** Clears and re-adds series to force LiveCharts2 update ?

## Code Changes

### Before (Broken)
```csharp
public void SetFrequencyVisible(string frequencyId, bool visible)
{
    // ... find actualFreqKey ...
    
    foreach (var pilotId in pilots)
    {
        var matchingKey = FindSeriesKey(actualFreqKey, pilotId);
        _seriesVisibility[matchingKey] = visible;  // Only dictionary updated
        _logger.Debug($"Set visibility: {matchingKey} = {visible}");
    }

    RebuildVisibleSeries();  // Series objects still have IsVisible=true!
}
```

### After (Fixed)
```csharp
public void SetFrequencyVisible(string frequencyId, bool visible)
{
    // ... find actualFreqKey ...
    
    foreach (var pilotId in pilots)
    {
        var matchingKey = FindSeriesKey(actualFreqKey, pilotId);
        _seriesVisibility[matchingKey] = visible;
        _logger.Debug($"Set visibility: {matchingKey} = {visible}");
        
        // CRITICAL FIX: Update the actual series object
        var series = Series.FirstOrDefault(s => 
            s.Name == matchingKey || 
            s.Name.Contains(actualFreqKey) && s.Name.Contains(pilotId));
        
        if (series != null)
        {
            series.IsVisible = visible;  // ? Now series object is updated!
            _logger.Debug($"Updated series.IsVisible: {series.Name} = {visible}");
        }
    }

    RebuildVisibleSeries();
}
```

## Testing

### Test Case: "Select None" Button
1. Open recording with multiple frequencies
2. Verify all frequencies are visible initially
3. Click "Select None" button
4. **Expected:** Graph should be completely empty
5. **Actual (Before Fix):** Frequencies remained visible
6. **Actual (After Fix):** Graph is empty ?

### Test Case: "Select All" ? "Select None" ? "Select All"
1. Click "Select All" - all visible ?
2. Click "Select None" - all hidden ?
3. Click "Select All" - all visible again ?

### Test Case: Individual Frequency Toggle
1. Hide one frequency - only that frequency hidden ?
2. Show it again - frequency reappears ?
3. Series collection refresh works correctly ?

## Why This Works

1. **Immediate Update:** Series objects' `IsVisible` property is updated immediately in `SetFrequencyVisible/SetPilotVisible`
2. **Consistent State:** Both `_seriesVisibility` dictionary and series objects have the same state
3. **Forced Refresh:** Clearing and re-adding the series collection forces LiveCharts2 to re-render with the new `IsVisible` values
4. **No Race Conditions:** Updates happen in the correct order before `RebuildVisibleSeries()` runs

## Key Learnings

1. **Always sync both dictionary and object state** when using LiveCharts2 with visibility toggles
2. **ObservableCollection property changes** alone aren't enough - must trigger collection change events
3. **Clear + Re-add pattern** is reliable for forcing LiveCharts2 to recognize changes
4. **Log both dictionary and object states** during debugging to catch mismatches

## Files Modified

- `src\AeroDebrief.UI\ViewModels\UnifiedGraphViewModel.cs`
  - `SetFrequencyVisible()` - Added series object update
  - `SetPilotVisible()` - Added series object update
  - `RebuildVisibleSeries()` - Already had collection refresh logic

## Build Status

? **Build Successful**
? **All visibility operations working correctly**
? **"Select None" clears the graph**

## Related Documents

- `docs\GRAPH-VISIBILITY-REDRAW-FIX.md` - Initial attempt to fix visibility
- `docs\GRAPH-VISIBILITY-SYNC-FIX.md` - Collection refresh implementation
- `docs\GRAPH-VISIBILITY-KEY-MATCHING-FIX.md` - Key format matching fixes
- `docs\GRAPH-VISIBILITY-FORMAT-MISMATCH-FIX.md` - Format conversion fixes

---

**Date:** 2024
**Status:** ? COMPLETE
**Impact:** Critical - "Select None" functionality now works correctly
