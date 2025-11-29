# Frequency Data Format Mismatch Fix

## Problem Description

The `_frequencyPilots` dictionary was not getting populated with the real frequency data. When users clicked "Select None" or tried to toggle frequency visibility, nothing happened because the frequency keys couldn't be matched.

## Root Cause

**Format Mismatch Between Data Provider and ViewModel:**

1. **AmplitudeSeriesProvider** returns keys in format: **`"F251.0-P1"`**
   - Frequency in MHz with "F" prefix
   - Pilot index with "P" prefix
   - Example: `"F251.0-P1"`, `"F305.5-P2"`

2. **UnifiedGraphViewModel** expected format: **`"251000000-PILOT123"`**
   - Frequency in Hz (no prefix)
   - Pilot identifier as string
   - Example: `"251000000-PILOT1"`

3. **`GetFrequencyKey`** formats as: **`"{frequency:F0}"`** ? `"251000000"`
   - Integer Hz value with no decimals

### The Disconnect

```csharp
// AmplitudeSeriesProvider.cs line 184:
var key = $"F{frequency:F1}-P{GetPilotIndex(transmitter)}";
// Returns: "F251.0-P1"

// UnifiedGraphViewModel ParseSeriesKey (BEFORE FIX):
var parts = key.Split('-');
return (parts[0], parts[1]);
// Returned: ("F251.0", "P1") ? 
// Expected: ("251000000", "P1") ?

// GetFrequencyKey formats as:
return $"{frequency:F0}";
// Returns: "251000000"

// MISMATCH: "F251.0" ? "251000000"
```

## The Solution

Updated `ParseSeriesKey` to:
1. **Detect the new format** (`"F{MHz}-P{index}"`)
2. **Convert MHz to Hz** for internal consistency
3. **Keep pilot ID** in original format
4. **Fall back to legacy format** for backwards compatibility

### Code Changes

```csharp
private (string? frequencyId, string? pilotId) ParseSeriesKey(string key)
{
    if (string.IsNullOrEmpty(key))
        return (null, null);
    
    // Check for new format: "F251.0-P1"
    if (key.StartsWith("F") && key.Contains("-P"))
    {
        var parts = key.Split('-');
        if (parts.Length >= 2)
        {
            // Extract frequency: "F251.0" -> "251.0"
            var freqStr = parts[0].Substring(1); // Remove "F" prefix
            
            // Extract pilot: "P1" -> "P1"
            var pilotStr = parts.Length > 1 ? parts[1] : null;
            
            // Convert MHz to Hz for internal storage
            if (double.TryParse(freqStr, out var freqMHz))
            {
                var freqHz = freqMHz * 1_000_000.0;
                return ($"{freqHz:F0}", pilotStr);  // ? "251000000", "P1"
            }
        }
    }
    
    // Legacy format fallback...
}
```

## How It Works Now

### Data Flow

1. **AmplitudeSeriesProvider returns:** `"F251.0-P1"`

2. **ParseSeriesKey converts to:** `("251000000", "P1")`

3. **GetFrequencyKey formats as:** `"251000000"`

4. **`_frequencyPilots` dictionary stores:**
   ```csharp
   {
       "251000000": ["P1", "P2", "P3"]
   }
   ```

5. **SetFrequencyVisible searches for:** `"251000000"` ? **MATCH!**

### Example Transformation

| Input Key | Parsed Frequency | Parsed Pilot | Stored in `_frequencyPilots` |
|-----------|------------------|--------------|------------------------------|
| `"F251.0-P1"` | `"251000000"` | `"P1"` | `_frequencyPilots["251000000"]` = `["P1"]` |
| `"F305.5-P2"` | `"305500000"` | `"P2"` | `_frequencyPilots["305500000"]` = `["P2"]` |
| `"F251.0-P3"` | `"251000000"` | `"P3"` | `_frequencyPilots["251000000"]` = `["P1", "P3"]` |

## Testing

### Test Case 1: Load Recording
1. Open recording with multiple frequencies
2. **Expected:** `_frequencyPilots` is populated with frequency keys in Hz
3. **Verify:** Check debug logs for "Added pilot 'P1' to frequency 251000000"

### Test Case 2: Toggle Frequency Visibility
1. Load recording
2. Click checkbox for a frequency in the mixer panel
3. **Expected:** Series visibility updates correctly
4. **Verify:** Graph shows/hides the frequency

### Test Case 3: Select None
1. Load recording with multiple frequencies
2. Click "Select None" button
3. **Expected:** All frequencies hidden, graph is empty
4. **Verify:** `_frequencyPilots` still has data, but all series have `IsVisible=false`

### Test Case 4: Select All
1. After "Select None"
2. Click "Select All" button
3. **Expected:** All frequencies visible again
4. **Verify:** Graph shows all frequencies

## Why This Fixes the Issue

### Before Fix
```
AmplitudeProvider ? "F251.0-P1"
    ?
ParseSeriesKey ? ("F251.0", "P1")  ? Wrong format
    ?
_frequencyPilots["F251.0"] = ["P1"]
    ?
SetFrequencyVisible("251000000") ? Not found ?
```

### After Fix
```
AmplitudeProvider ? "F251.0-P1"
    ?
ParseSeriesKey ? ("251000000", "P1")  ? Correct format (MHz?Hz)
    ?
_frequencyPilots["251000000"] = ["P1"]
    ?
SetFrequencyVisible("251000000") ? Found! ?
```

## Impact

? **Frequency data now loads correctly into `_frequencyPilots`**
? **"Select None" works correctly**
? **"Select All" works correctly**
? **Individual frequency toggles work correctly**
? **Backwards compatible with legacy format**

## Related Files

- `src\AeroDebrief.UI\ViewModels\UnifiedGraphViewModel.cs` - Fixed `ParseSeriesKey`
- `src\AeroDebrief.UI\Services\Graphs\AmplitudeSeriesProvider.cs` - Source of format (line 184)

## Key Learnings

1. **Always check data provider format** before writing parsing code
2. **Log frequency keys** during debugging to identify mismatches
3. **MHz vs Hz confusion** is a common source of bugs
4. **String prefix formats** ("F", "P") require careful parsing
5. **Unit consistency** is critical - stick to one unit (Hz) internally

---

**Date:** 2024
**Status:** ? COMPLETE
**Impact:** Critical - Fixes core frequency data loading and visibility functionality
