# ? PHASE 12: REAL DATA CONNECTION - QUICK REFERENCE

## What Was Done

Fixed the graph to show **real recording frequencies** instead of synthetic test data (251-260 MHz).

---

## Code Changes Summary

### 1?? UnifiedPlayerViewModel.cs
```csharp
// Added public property
public PlaybackSessionManager SessionManager => _sessionManager;

// Added method to reconnect data source
public void SetDataSource(IAmplitudeSeriesProvider provider)
{
    _amplitudeProvider = provider;
    var tileCache = new DataTileCache(budgetMB: 300.0);
    _tileManager = new DataTileManager(tileCache);
}
```

### 2?? AmplitudeSeriesProvider.cs
```csharp
// FIXED frequency format bug
var frequencyMHz = frequency / 1_000_000.0;  // ? Convert Hz to MHz
var key = $"F{frequencyMHz:F1}-P{GetPilotIndex(transmitter)}";
```

### 3?? UnifiedPlayerControl.xaml.cs
```csharp
private void OnFileLoaded(string filePath)
{
    var packetSource = ViewModel.SessionManager.PacketSource;
    var audioEngine = new Core.Audio.AudioProcessingEngine();
    var amplitudeProvider = new Services.Graphs.AmplitudeSeriesProvider(
        packetSource, audioEngine);
    
    ViewModel.GraphViewModel.SetDataSource(amplitudeProvider);  // ? Connect!
}
```

---

## How to Test

1. **Open a recording file** (`.adb`)
2. **Check the logs** for:
   ```
   ? Phase 12: GraphViewModel connected to real recording data successfully!
   ```
3. **Verify the graph** shows YOUR actual frequencies (not 251-260 MHz)
4. **Test frequency toggles** - should work with real data
5. **Test "Select None"** - should clear the graph completely

---

## What to Expect

### ? Before (Broken)
- Graph showed 251-260 MHz (test data)
- Warnings: "Unknown frequency: 31000000"
- Frequency toggles didn't work

### ? After (Fixed)
- Graph shows YOUR frequencies from the recording
- No warnings for valid frequencies
- Frequency toggles work correctly
- "Select None" / "Select All" work properly

---

## Files Modified

- `UnifiedPlayerViewModel.cs` - Added SessionManager property + SetDataSource
- `AmplitudeSeriesProvider.cs` - Fixed MHz/Hz format bug
- `UnifiedPlayerControl.xaml.cs` - Connected real data on file load
- `CoreApiService.cs` - Added PacketSource property
- `WaveformDisplayPanel.xaml.cs` - Added SetRecordingSource infrastructure

---

## Build Status

? **Build:** Successful
? **Implementation:** Complete
? **Testing:** Ready

---

**Quick Link:** See `docs/Phase12-Real-Data-Connection-SUMMARY.md` for full details.
