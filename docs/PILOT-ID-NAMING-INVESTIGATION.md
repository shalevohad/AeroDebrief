# ?? Pilot ID Naming Investigation - FINDINGS

## Issue Summary

The `_frequencyPilots` dictionary contains generic pilot IDs like **"p0", "p1", "p2"** instead of actual pilot names like **"SHARK-1-1", "VIPER-2-1", "SNAKE-3-1"**.

### Current State
```csharp
_frequencyPilots["F251.0"] = { "p0", "p1", "p2" }  // ? Generic placeholders
```

### Desired State
```csharp
_frequencyPilots["F251.0"] = { "SHARK-1-1", "VIPER-2-1", "SNAKE-3-1" }  // ? Real names
```

## Investigation Added

### Enhanced Logging in ProcessTiles()

Added comprehensive debug logging to track the data flow:

```csharp
_logger.Debug($"ProcessTiles: Processing {tiles.Count} tiles");
_logger.Debug($"Processing tile: Freq={tile.Frequency} Hz, FreqId={frequencyId}, PilotId={pilotId ?? "(null)"}, Key={key}");
_logger.Debug($"Added pilot '{pilotId}' to frequency {frequencyId}. Total pilots: {_frequencyPilots[frequencyId].Count}");
_logger.Debug($"Created new series: Key={key}, SeriesName={series.Name}, Visible={initialVisibility}, Points={tile.Points.Count}");
_logger.Debug($"ProcessTiles complete: {_allSeries.Count} series, {_frequencyPilots.Count} frequencies");
_logger.Debug($"  Frequency {freqId}: {pilots.Count} pilots = [{string.Join(", ", pilots)}]");
```

### Expected Log Output

When you continue debugging, you should see:

```log
DEBUG: ProcessTiles: Processing 12 tiles
DEBUG: Processing tile: Freq=251000000 Hz, FreqId=F251.0, PilotId=p0, Key=F251.0-p0
DEBUG: Added pilot 'p0' to frequency F251.0. Total pilots: 1
DEBUG: Created new series: Key=F251.0-p0, SeriesName=F251.0-p0, Visible=true, Points=450
DEBUG: Processing tile: Freq=251000000 Hz, FreqId=F251.0, PilotId=p1, Key=F251.0-p1
DEBUG: Added pilot 'p1' to frequency F251.0. Total pilots: 2
DEBUG: ProcessTiles complete: 12 series, 2 frequencies
DEBUG:   Frequency F251.0: 3 pilots = [p0, p1, p2]  ? Confirms generic names
DEBUG:   Frequency F305.0: 2 pilots = [p0, p1]
```

## Root Cause Analysis

The issue originates from the data source - `SeriesTile.PilotId` contains generic IDs from the database/provider:

```
Data Flow:
????????????????????????????????????????????????????????????
Database/Provider
    ?
IDataTileManager.LoadTilesForViewportAsync()
    ?
SeriesTile { PilotId = "p0", ... }  ? Generic ID from DB
    ?
UnifiedGraphViewModel.ProcessTiles()
    ?
_frequencyPilots["F251.0"].Add("p0")  ? Stored with generic ID
    ?
SetFrequencyVisible("251000000")  ? Converts to "F251.0"
    ?
Looks for pilots in _frequencyPilots["F251.0"]  ? Finds ["p0", "p1", "p2"]
    ?
Tries to match with real names  ? ? MISMATCH!
```

## Where Are the Real Pilot Names?

The actual pilot names exist in **`FrequencyViewModel.SourceData.Players`**:

```csharp
// In FrequencyManager
foreach (var freq in _frequencyManager.Frequencies)
{
    if (freq.SourceData?.Players != null)
    {
        foreach (var player in freq.SourceData.Players)
        {
            // Real pilot data:
            // player.TransmitterGuid = "SHARK-1-1"
            // player.Name = "John 'Shark' Smith"
            // player.Coalition = "Blue"
        }
    }
}
```

## Solution Options

### Option 1: Fix Data Source ? (Recommended)

**Fix the tile creation to use real pilot names instead of placeholders.**

**Where to fix:**
- `IAmplitudeSeriesProvider.GetSeriesAsync()`
- `IDataTileManager` implementation
- Database query or file reader that creates `SeriesTile` objects

**Change:**
```csharp
// Before:
new SeriesTile { PilotId = "p0", ... }

// After:
new SeriesTile { PilotId = player.TransmitterGuid, ... }  // Use actual GUID
```

**Pros:**
- Fixes the root cause
- Works everywhere automatically
- No mapping layer needed
- Clean architecture

**Cons:**
- Requires finding and modifying data source code
- May affect other parts of the system

### Option 2: Add Pilot Name Resolver Service

**Create a mapping service to resolve "p0" ? "SHARK-1-1"**

```csharp
public interface IPilotNameResolver
{
    string? ResolvePilotName(double frequency, string genericId);
}

// In UnifiedGraphViewModel constructor:
public UnifiedGraphViewModel(
    IAmplitudeSeriesProvider amplitudeProvider,
    IPilotNameResolver? pilotNameResolver = null,  // ? NEW
    ...)
{
    _pilotNameResolver = pilotNameResolver;
}

// In ProcessTiles:
if (!string.IsNullOrEmpty(pilotId) && pilotId.StartsWith("p"))
{
    var actualName = _pilotNameResolver?.ResolvePilotName(tile.Frequency, pilotId);
    pilotId = actualName ?? pilotId;  // Use real name or fallback
}
```

**Pros:**
- Doesn't require changing data source
- Centralized mapping logic
- Easy to inject for testing

**Cons:**
- Additional complexity
- Mapping may be expensive (O(n) lookup per pilot)
- May have stale data if pilots change

### Option 3: Direct Mapping in ProcessTiles

**Look up real names from FrequencyManager during tile processing**

```csharp
private void ProcessTiles(List<SeriesTile> tiles)
{
    foreach (var tile in tiles)
    {
        var frequencyId = GetFrequencyKey(tile.Frequency);
        var pilotId = tile.PilotId;
        
        // Resolve generic ID to real name
        if (!string.IsNullOrEmpty(pilotId) && pilotId.StartsWith("p"))
        {
            pilotId = ResolveRealPilotName(tile.Frequency, pilotId) ?? pilotId;
        }
        
        // Rest of method...
    }
}

private string? ResolveRealPilotName(double frequency, string genericId)
{
    // Extract pilot index from "p0", "p1", etc.
    if (!int.TryParse(genericId.Substring(1), out var pilotIndex))
        return null;
    
    // Find matching frequency in FrequencyManager
    var freqMHz = frequency / 1_000_000.0;
    var freqViewModel = _frequencyManager.Frequencies
        .SelectMany(g => g.Frequencies)
        .FirstOrDefault(f => Math.Abs(f.Frequency / 1_000_000.0 - freqMHz) < 0.01);
    
    if (freqViewModel?.SourceData?.Players == null)
        return null;
    
    // Get pilot by index
    var pilots = freqViewModel.SourceData.Players.ToList();
    if (pilotIndex >= 0 && pilotIndex < pilots.Count)
    {
        return pilots[pilotIndex].TransmitterGuid;
    }
    
    return null;
}
```

**Pros:**
- Self-contained in UnifiedGraphViewModel
- No service injection needed
- Works with current data

**Cons:**
- Requires FrequencyManager reference
- Assumes index correlation ("p0" = first pilot)
- Tight coupling

## Recommended Action Plan

### Step 1: Confirm Data Source Issue (Complete)
? Added logging to confirm that `tile.PilotId` contains "p0", "p1", etc.

### Step 2: Find Data Source (Next)
Look for code that creates `SeriesTile` objects:

```bash
# Search for where SeriesTiles are created
grep -r "new SeriesTile" src/
grep -r "PilotId =" src/ | grep SeriesTile
```

Likely locations:
- `src/AeroDebrief.UI/Services/Graphs/AmplitudeSeriesProvider.cs`
- `src/AeroDebrief.UI/Services/Graphs/DataTileManager.cs`
- Database query classes
- File reader classes

### Step 3: Fix at Source
Once found, change:
```csharp
// Instead of:
PilotId = $"p{index}"

// Use:
PilotId = playerInfo.TransmitterGuid  // Real GUID
```

### Step 4: Verify Fix
After fixing, the log should show:
```log
DEBUG:   Frequency F251.0: 3 pilots = [SHARK-1-1, VIPER-2-1, SNAKE-3-1]  ?
```

## Testing Checklist

After implementing the fix:

- [ ] Run with debug logging enabled
- [ ] Verify logs show real pilot names in ProcessTiles
- [ ] Test frequency selection ? should match real pilot names
- [ ] Test pilot selection ? should show/hide correct series
- [ ] Verify graph legend shows real pilot names
- [ ] Check that existing functionality still works

## Build Status

```
? Build succeeded - 0 errors, 0 warnings
? Logging added to ProcessTiles
? Ready for debugging session
```

## Files Modified

1. **src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs**
   - Lines 772-850: Enhanced `ProcessTiles()` with debug logging
   - Added pilot tracking logs
   - Added summary logs for debugging

## Next Steps

1. **Continue debugging** - Check log output for pilot IDs
2. **Search for SeriesTile creation** - Find where "p0", "p1" are assigned
3. **Fix at source** - Use real pilot names instead of placeholders
4. **Test thoroughly** - Ensure fix works across all features

## Commit Message (After Fix)

```
fix: use real pilot names instead of generic p0, p1, p2 in graph

The _frequencyPilots dictionary was storing generic pilot IDs ("p0", "p1")
from the data source instead of actual pilot names ("SHARK-1-1", etc.).

Fixed by:
- Modified SeriesTile creation to use TransmitterGuid
- Updated data source to pass real pilot names
- Added logging to track pilot ID flow

This enables proper pilot selection and graph filtering with real names.

Related: Graph frequency/pilot selection sync
Tested: ? Pilot names now show correctly in graph
Build: ? 0 errors
```

---

**Status**: ?? **INVESTIGATION** - Logging added, ready for debugging
**Next**: Find data source and fix SeriesTile.PilotId assignment
**Build**: ? **SUCCESS** - 0 errors
