# ? FINAL FIX: Graph Visibility Method Name Correction

## Critical Issue Found

The previous fix was calling **non-existent methods**:
- ? `SetFrequencyVisibility(double, bool)` - **DOES NOT EXIST**
- ? `SetPilotVisibility(double, string, bool)` - **DOES NOT EXIST**

The actual methods in `UnifiedGraphViewModel` are:
- ? `SetFrequencyVisible(string frequencyId, bool visible)`
- ? `SetPilotVisible(string frequencyId, string pilotId, bool visible)`

## Root Cause

The code was trying to pass `double` frequency values directly, but the graph methods expect **string keys** in the format `"251000000"` (frequency in Hz as string).

## Final Fix Applied

### 1. OnFrequencySelectionChanged (Lines 552-584)

**Before** (calling non-existent method):
```csharp
_graphViewModel.SetFrequencyVisibility(e.Frequency, true);  // ? Method doesn't exist
```

**After** (correct):
```csharp
var freqId = $"{e.Frequency:F0}";  // Convert 251000000.0 ? "251000000"
_graphViewModel.SetFrequencyVisible(freqId, true);  // ? Correct method
Logger.Debug($"? Phase 12: Graph series shown for {e.Frequency:F1} Hz (key: {freqId})");
```

### 2. UpdatePilotSelection (Lines 1003-1037)

**Before** (calling non-existent method):
```csharp
_graphViewModel.SetPilotVisibility(freq.Frequency, pilotGuid, isSelected);  // ? Method doesn't exist
```

**After** (correct):
```csharp
var freqId = $"{freq.Frequency:F0}";  // Convert to string key
_graphViewModel.SetPilotVisible(freqId, pilotGuid, isSelected);  // ? Correct method
Logger.Debug($"? Graph pilot visibility updated: {freq.Frequency:F1} Hz (key: {freqId}), {pilotGuid} = {isSelected}");
```

### 3. UpdateChannelMute (Lines 989-1001)

**Before** (inconsistent):
```csharp
var freqId = $"{frequency:F0}";
if (muted)
{
    _graphViewModel.SetFrequencyVisible(freqId, false);
}
else
{
    _graphViewModel.SetFrequencyVisible(freqId, true);
}
```

**After** (simplified):
```csharp
var freqId = $"{frequency:F0}";
_graphViewModel.SetFrequencyVisible(freqId, !muted);  // ? Simplified logic
Logger.Debug($"? Graph mute sync: {frequency:F1} Hz (key: {freqId}), muted={muted}");
```

## Key Format Conversion

The conversion from `double` frequency to string key:

```csharp
// Input: frequency = 251000000.0 (Hz)
var freqId = $"{frequency:F0}";
// Output: freqId = "251000000"
```

This matches the internal key format used by `UnifiedGraphViewModel._frequencyPilots` dictionary.

## How It Works Now

### Frequency Selection Flow
```
User clicks frequency checkbox
    ?
FrequencyManager.SelectFrequency(251000000.0)
    ?
OnFrequencySelectionChanged event (double: 251000000.0)
    ?
Convert: $"{251000000.0:F0}" ? "251000000"
    ?
_graphViewModel.SetFrequencyVisible("251000000", true)
    ?
Graph shows ALL pilots on that frequency
```

### Pilot Selection Flow
```
User clicks pilot checkbox
    ?
UpdatePilotSelection(pilotGuid, isSelected)
    ?
Find pilot ? get freq.Frequency (251000000.0)
    ?
Convert: $"{251000000.0:F0}" ? "251000000"
    ?
_graphViewModel.SetPilotVisible("251000000", pilotGuid, true)
    ?
Graph shows/hides THAT specific pilot's series
```

### Mute Button Flow
```
User clicks mute button
    ?
UpdateChannelMute(251000000.0, true)
    ?
_mixerController.SetChannelMuted(251000000.0, true)  // Audio mute
    ?
Convert: $"{251000000.0:F0}" ? "251000000"
    ?
_graphViewModel.SetFrequencyVisible("251000000", false)  // Hide graph
```

## Build Status

```
Build succeeded.
    0 Error(s)
    0 Warning(s)
```

## Testing Checklist

Now that the correct methods are being called, test:

### Frequency Selection
- [ ] Check frequency checkbox ? graph shows all pilots for that frequency
- [ ] Uncheck frequency checkbox ? graph hides all pilots for that frequency
- [ ] Select All button ? all frequencies visible on graph
- [ ] Deselect All button ? all frequencies hidden on graph

### Pilot Selection  
- [ ] Expand frequency ? see pilot list
- [ ] Check pilot checkbox ? that pilot's line appears on graph
- [ ] Uncheck pilot checkbox ? that pilot's line disappears from graph
- [ ] Check multiple pilots ? only selected pilots visible

### Mute Button
- [ ] Click mute on frequency ? graph hides that frequency
- [ ] Unmute frequency ? graph shows that frequency again
- [ ] Mute should sync with checkbox state

### Edge Cases
- [ ] Frequency with no pilots ? no errors
- [ ] Unknown pilot GUID ? logs warning, doesn't crash
- [ ] Rapid checkbox toggling ? no race conditions
- [ ] Multiple frequencies with same pilots ? each handled independently

## Why Previous Fix Didn't Work

1. **Wrong method names**: Called `SetFrequencyVisibility` instead of `SetFrequencyVisible`
2. **Wrong parameter types**: Passed `double` instead of `string`
3. **No key conversion**: Didn't convert Hz values to string format

## Actual UnifiedGraphViewModel API

```csharp
// Frequency-level visibility (all pilots)
public void SetFrequencyVisible(string frequencyId, bool visible)
{
    // frequencyId format: "251000000" (Hz as string)
    // visible: true = show, false = hide
}

// Pilot-level visibility (specific pilot)
public void SetPilotVisible(string frequencyId, string pilotId, bool visible)
{
    // frequencyId format: "251000000" (Hz as string)
    // pilotId: pilot GUID (e.g., "SHARK-1-1")
    // visible: true = show, false = hide
}
```

## Performance

- **Key lookup**: O(1) dictionary lookup by string key
- **Series update**: O(n) where n = number of pilots on frequency
- **No waveform regeneration**: Graph updates visibility flags only
- **No audio pipeline restart**: Independent of audio

## Next Steps

1. **Test manually** with an actual audio file
2. **Verify logging** - check that debug messages show correct keys
3. **Watch for warnings** - "Unknown frequency" messages indicate key mismatch
4. **Check graph behavior** - series should appear/disappear immediately

## Commit Message

```
fix: correct graph visibility method names and parameter types

- Use SetFrequencyVisible(string) instead of non-existent SetFrequencyVisibility(double)
- Use SetPilotVisible(string, string) instead of non-existent SetPilotVisibility(double, string)
- Convert frequency Hz values to string keys: $"{frequency:F0}"
- Add logging to show actual keys being used for debugging

This fixes the issue where frequency/pilot selection changes weren't updating the graph.
The previous fix was calling methods that don't exist in UnifiedGraphViewModel.

Tested: ? Build succeeds
Ready for: Manual testing with audio file
```

---

**Status**: ? **COMPLETE** - Correct methods are now being called with proper key format!
**Build**: ? **SUCCESS** - 0 errors
**Ready for**: Manual testing to verify graph updates work correctly
