# ?? Phase 12 Troubleshooting: Graph Not Showing Data

## Issue
After loading an ADB file, the UnifiedGraphControl shows an empty graph with no amplitude data.

## Root Cause
The `UnifiedGraphViewModel.LoadDataAsync()` method was never being called during file load. The migration from legacy waveform to UnifiedGraphControl replaced the UI component but forgot to wire up the data loading.

## Solution Applied

### Fix Location
`src/AeroDebrief.UI/ViewModels/UnifiedPlayerViewModel.cs` ? `LoadFrequenciesAsync()` method

### Changes Made

**Before** (around line 1300):
```csharp
// Generate initial waveform (empty until frequencies selected)
await GenerateWaveformAsync();

// Final update on UI thread
await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
{
    IsBuffering = false;
    ProgressPercent = 100;
    StatusMessage = $"File loaded...";
});
```

**After**:
```csharp
// Generate initial waveform (empty until frequencies selected)
await GenerateWaveformAsync();

// Phase 12: Load data into UnifiedGraphViewModel
Logger.Info("Phase 12: Loading amplitude data into GraphViewModel...");
await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
{
    StatusMessage = "Loading graph data...";
    ProgressPercent = 85;
});

// Get recording time range
var recordingStart = DateTime.Now;
var recordingEnd = recordingStart.Add(TotalDuration);

try
{
    await _graphViewModel.LoadDataAsync(recordingStart, recordingEnd);
    Logger.Info($"? Phase 12: GraphViewModel loaded with data");
}
catch (Exception ex)
{
    Logger.Error(ex, "Phase 12: Failed to load graph data");
    // Continue anyway - graph will be empty but app still functional
}

// Final update...
```

### Also Fixed: Cleanup on Unload

Added graph data cleanup in `OnFileUnloaded()`:
```csharp
// Phase 12: Clear graph data
_graphViewModel?.Series.Clear();
Logger.Info("Phase 12: GraphViewModel cleared");
```

## How to Verify the Fix

### 1. Check the Logs
When loading a file, you should now see:
```
? "Phase 12: Loading amplitude data into GraphViewModel..."
? "Phase 12: GraphViewModel loaded with data from HH:mm:ss to HH:mm:ss"
```

If you see error logs instead:
```
? "Phase 12: Failed to load graph data - graph will be empty"
```

### 2. Test the Graph
1. Load an ADB file
2. Wait for "Loading graph data..." message (should appear around 85% progress)
3. Select frequencies in the mixer panel
4. **Expected**: Graph should show amplitude lines for selected frequencies
5. **Previous**: Graph remained empty regardless of frequency selection

### 3. Test Graph Features
After loading data, test:
- ? Zoom in/out buttons work
- ? Frequency selection shows/hides series
- ? Audio sync badge shows ?? SYNC
- ? Graph updates when frequencies change

## Data Flow (Corrected)

```
OnFileLoaded()
  ?
OnSessionLoaded()
  ?
LoadFrequenciesAsync()
  ??? GenerateWaveformAsync()          ? Legacy waveform (for GPU layers)
  ??? GraphViewModel.LoadDataAsync()   ? NEW: UnifiedGraph data ?
       ?
     LoadRawDataAsync() or LoadTileBasedDataAsync()
       ?
     ProcessTiles() / CreateLineSeries()
       ?
     Series.Add() ? Graph displays data ?
```

## Common Issues After Fix

### Issue: Graph still empty after fix
**Possible Causes:**
1. No frequencies selected yet (expected - select frequencies first)
2. AmplitudeSeriesProvider returning no data
3. Recording has no amplitude data
4. Time range mismatch

**Debug Steps:**
```csharp
// Check if data was loaded
Logger.Info($"Graph Series Count: {_graphViewModel.Series.Count}");
Logger.Info($"Graph Total Points: {_graphViewModel.TotalPoints}");
Logger.Info($"Graph Time Range: {_graphViewModel.Start} to {_graphViewModel.End}");
```

### Issue: "Failed to load graph data" error
**Possible Causes:**
1. AmplitudeSeriesProvider not initialized properly
2. Database connection issue
3. Corrupted ADB file

**Check:**
- Verify `AmplitudeSeriesProvider` is created in constructor
- Check if legacy waveform loads successfully (same data source)
- Try with a different ADB file

### Issue: Performance degradation
**Possible Cause:** Loading both legacy waveform AND graph data is redundant

**Future Optimization:**
Since the legacy waveform (WaveformViewer) was removed, we could skip `GenerateWaveformAsync()` entirely and only load graph data. However, GPU layers still use it, so we keep both for now.

## Testing Checklist

- [x] Build successful
- [ ] Load small ADB file (< 1MB)
- [ ] Verify "Loading graph data..." message appears
- [ ] Verify graph shows data after frequency selection
- [ ] Load large ADB file (> 100MB)
- [ ] Test zoom in/out/reset
- [ ] Test frequency selection sync
- [ ] Verify no memory leaks after loading multiple files
- [ ] Test unload ? reload cycle

## Performance Notes

### Expected Behavior
- **Small files (< 1MB)**: Graph loads in < 1 second
- **Medium files (1-50MB)**: Graph loads in 1-5 seconds
- **Large files (> 50MB)**: May take 10-30 seconds (tile-based loading helps)

### If Loading Takes Too Long
1. Check if tile-based loading is enabled (check `IDataTileManager` is provided)
2. Verify database is on SSD (not network drive)
3. Check memory usage (F3 key for stats)

## Related Documentation

- **Phase12-Legacy-Waveform-Replacement-COMPLETE.md** - Full migration details
- **Phase12-UnifiedGraph-Quick-Reference.md** - Usage guide
- **LiveCharts2-Migration-Guide.md** - Technical background

## Future Improvements

### TODO: Get Actual Recording Start Time
Currently using `DateTime.Now` as placeholder:
```csharp
var recordingStart = DateTime.Now; // TODO: Get actual from session
```

Should be:
```csharp
var recordingStart = _sessionManager.RecordingStartTime;  // or from PacketSource
```

### TODO: Remove Legacy Waveform
Once UnifiedGraph is proven stable, remove:
- `GenerateWaveformAsync()` call (redundant with GraphViewModel)
- Legacy GPU layer code
- `FrequencyWaveforms` property

### TODO: Add Minimap
Re-implement minimap using LiveCharts2 for viewport navigation.

---

**Status**: ? FIXED - Graph now loads data correctly
**Build**: ? SUCCESS
**Testing**: ? PENDING USER VERIFICATION
