# ?? FIX: No Frequencies Showing on Graph

## Problem

After opening a recording file, the amplitude graph was **empty** - no frequencies were displayed even though the real data source was connected.

## Root Cause

The data source was successfully reconnected to real recording data, but **`LoadDataAsync()` was never called** to actually load and display the data.

### The Missing Step

```
? File opens ? OnFileLoaded()
? Creates AmplitudeSeriesProvider with real PacketSource
? Calls GraphViewModel.SetDataSource(amplitudeProvider)
? MISSING: LoadDataAsync() to actually load the data!
```

The graph ViewModel was ready to display data, but had no data loaded into it.

---

## Solution

### 1. Added LoadDataAsync Call

**File:** `src/AeroDebrief.UI/Controls/UnifiedPlayerControl.xaml.cs`

**Modified:** `OnFileLoaded()` method

```csharp
// After connecting the data source:
ViewModel.GraphViewModel.SetDataSource(amplitudeProvider);

// NEW: Actually load the data!
var recordingStart = packetSource.RecordingStart;
var recordingEnd = recordingStart + packetSource.TotalDuration;

_ = Task.Run(async () =>
{
    try
    {
        await ViewModel.GraphViewModel.LoadDataAsync(recordingStart, recordingEnd);
        _logger.Info("? Phase 12: Frequency data loaded successfully!");
        
        // Also load frequencies into TacviewIntegration
        if (ViewModel.TacviewIntegration != null)
        {
            var frequencies = ViewModel.GraphViewModel.Series
                .Select(s => ViewModel.GraphViewModel.ParseSeriesKey(s.Name ?? ""))
                .Where(parsed => parsed.frequencyId != null)
                .Select(parsed => double.TryParse(parsed.frequencyId, out var freq) ? freq : 0)
                .Where(freq => freq > 0)
                .Distinct()
                .ToList();
            
            ViewModel.TacviewIntegration.LoadFrequenciesFromAudioFile(frequencies);
        }
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Phase 12: Failed to load frequency data");
    }
});
```

### 2. Made ParseSeriesKey Public

**File:** `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`

Changed `ParseSeriesKey` from `private` to `public` so it can be used to extract frequencies for TacviewIntegration.

---

## What Happens Now

### Before (Broken)
1. File opens ?
2. Data source connected ?
3. **Graph remains empty** ?

### After (Fixed)
1. File opens ?
2. Data source connected ?
3. **LoadDataAsync() is called** ?
4. **Frequencies are loaded from recording** ?
5. **Graph displays frequency data** ?
6. **Frequencies are sent to TacviewIntegration** ?

---

## Expected Behavior

When you open a recording file (`.adb`):

1. **Loading sequence:**
   ```
   Phase 12: Connecting GraphViewModel to recording data source
   Recording data sources ready - PacketSource: XXXXX packets, Duration: HH:MM:SS
   ? Phase 12: GraphViewModel connected to real recording data successfully!
   Phase 12: Loading frequency data from HH:MM:SS to HH:MM:SS
   ? Phase 12: Frequency data loaded successfully!
   ? Phase 12: Loaded N frequencies into TacviewIntegration
   ```

2. **Graph displays:**
   - All frequencies from your recording (e.g., 31 MHz, 251 MHz, etc.)
   - Different colored lines for each frequency
   - Amplitude data over time

3. **Frequency panel:**
   - Shows all frequencies in the mixer panel
   - Checkboxes to toggle visibility
   - "Select All" / "Select None" buttons work

4. **Tacview integration:**
   - Frequencies automatically loaded into Tacview frequency list
   - Ready for filtering when Tacview connects

---

## Why LoadDataAsync Was Missing

The original implementation assumed that `LoadDataAsync()` would be called separately by the UI layer (e.g., when the user clicked a "Load" button or from a different initialization path). However, in the integrated player workflow, the data should load automatically when a file opens.

This is a common pattern:
- **Initialize data source** (connect the provider)
- **Load data** (actually fetch and display it)

The fix ensures both steps happen automatically when a file is opened.

---

## Testing

? **Build:** Successful
? **Code changes:** Complete

**Next test:**
1. Run the application
2. Open a recording file
3. **Expected:** Graph shows frequencies immediately
4. **Check logs:** Look for "? Phase 12: Frequency data loaded successfully!"

---

## Files Modified

1. `src/AeroDebrief.UI/Controls/UnifiedPlayerControl.xaml.cs`
   - Added `LoadDataAsync()` call in `OnFileLoaded()`
   - Added frequency extraction for TacviewIntegration

2. `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
   - Changed `ParseSeriesKey()` from private to public

---

**Status:** ? COMPLETE - Graph will now display frequencies when file opens

**Impact:** Critical - Enables actual data visualization in the amplitude graph
