# Default Behavior Change - General Frequency Filtering

## Summary

Changed the default behavior for **General Frequency Filtering** from "all enabled" to "all disabled (not selected)".

## Rationale

### Problem with "All Enabled" Default
When general frequencies are enabled by default:
- Users hear ALL non-selected pilots on ALL frequencies
- Creates audio clutter and information overload
- Defeats the purpose of selecting specific pilots for focus
- User must manually disable many frequencies to achieve focus

### Benefits of "All Disabled" Default
When general frequencies are disabled by default:
- ? **Immediate Focus**: Users hear ONLY selected pilots after selection
- ? **No Audio Clutter**: Clean audio environment by default
- ? **Intentional Addition**: User explicitly enables frequencies as needed
- ? **Better UX**: Aligns with user intent when selecting pilots
- ? **Simpler Configuration**: Most use cases need no general frequency configuration

## Impact

### User Experience Flow

#### Before (All Enabled Default):
```
1. User selects pilot in Tacview
2. Hears selected pilot + ALL other pilots on ALL frequencies
3. Must go to menu and disable unwanted general frequencies
4. Finally achieves focus on selected pilot
```

#### After (All Disabled Default):
```
1. User selects pilot in Tacview
2. Hears ONLY selected pilot (immediate focus)
3. Optionally enables specific general frequencies if context needed
4. Clean, focused audio by default
```

### Common Use Cases

#### Use Case 1: Focus on Flight Lead
**Scenario**: Analyze specific pilot's communications

**Before**: Select pilot ? Hear everyone ? Disable all general frequencies  
**After**: Select pilot ? Hear only that pilot ? (zero configuration)

#### Use Case 2: Add Tower Context
**Scenario**: Hear selected pilots + tower from everyone

**Before**: Select pilots ? Already hear everyone ? Works but cluttered  
**After**: Select pilots ? Enable tower frequency ? Clean tower + selected pilots ?

#### Use Case 3: Combat Analysis
**Scenario**: Hear specific combat frequencies only

**Before**: Select pilots ? Disable all general ? Enable specific combat freq  
**After**: Select pilots ? Enable specific combat freq ? (one less step)

## Technical Changes

### Lua Code Changes

**pan_manager.lua - UpdateCurrentPilots()**:
```lua
-- OLD: Enable in general filter by default
if PanManager.generalFrequencyFilter[freq] == nil then
    PanManager.generalFrequencyFilter[freq] = true
end

-- NEW: Disable in general filter by default
if PanManager.generalFrequencyFilter[freq] == nil then
    PanManager.generalFrequencyFilter[freq] = false
end
```

**pan_manager.lua - IsGeneralFrequencyEnabled()**:
```lua
-- OLD: Default to enabled if not set
return enabled == nil or enabled == true

-- NEW: Default to disabled if not set
return enabled == true
```

### Protocol Message Changes

**Default Message**:
```json
{
  "type": "pilot_selection",
  "selected_pilots": [...],
  "general_enabled_frequencies": []  // Empty by default now
}
```

### C# Implementation Changes

**TacviewAudioFilter.ShouldPlayPacket()**:
```csharp
// For non-selected pilots:
// OLD: Play all if general frequencies not specified
if (_currentSelection.GeneralEnabledFrequencies == null)
    return true;

// NEW: Silent if general frequencies not specified or empty
if (_currentSelection.GeneralEnabledFrequencies == null || 
    !_currentSelection.GeneralEnabledFrequencies.Any())
    return false;
```

## Documentation Updates

### Files Modified
1. ? `08-FREQUENCY-FILTERING-FEATURE.md` - Updated overview and examples
2. ? `01-LUA-ADDON-SPECIFICATION.md` - Updated pan_manager.lua code
3. ? `02-PROTOCOL-SPECIFICATION.md` - Updated protocol specification
4. ? `09-FREQUENCY-FILTERING-USER-GUIDE.md` - Updated user guide with default behavior
5. ? `01-LUA-ADDON-SPECIFICATION-PROTOCOL.md` - Updated protocol.lua implementation
6. ? `README.md` - Updated feature description

### Key Documentation Changes
- All references to "Default: All enabled" changed to "Default: All disabled"
- Added explanation of why general frequencies are disabled by default
- Updated examples to reflect zero-configuration focus on selected pilots
- Added FAQ entry explaining why non-selected pilots are silent by default
- Updated UI mockups to show all checkboxes unchecked by default

## User Communication

### What Users Need to Know

**Simple Version**:
> When you select pilots in Tacview, you'll now hear ONLY those pilots by default. To also hear other pilots, go to "Configure General Frequencies..." and enable the specific frequencies you want.

**Detailed Version**:
> **Change**: General frequency filtering now defaults to "all disabled" instead of "all enabled".
> 
> **What This Means**:
> - Selecting pilots gives you immediate audio focus - you only hear those pilots
> - You won't hear background chatter from non-selected pilots unless you enable it
> - To add context (tower, GCI, etc.), enable specific frequencies in "Configure General Frequencies..."
> 
> **Why**: This provides cleaner audio by default and aligns with the intent of selecting specific pilots for analysis.

## Migration Notes

### For Existing Users
- No action required for most users
- If you previously relied on hearing everyone by default:
  - Go to: `Tacview ? AeroDebrief Sync ? Configure General Frequencies...`
  - Click: "? Select All Frequencies"
  - This restores the previous "hear everyone" behavior

### For New Users
- Default behavior provides better first experience
- Clean, focused audio without configuration
- Explicit opt-in for additional audio context

## Testing Impact

### Test Cases to Update
- [ ] Default message contains empty `general_enabled_frequencies` array
- [ ] Non-selected pilots are silent when general frequencies not explicitly enabled
- [ ] Enabling general frequency allows hearing non-selected pilots on that frequency
- [ ] "Select All" button in general frequencies enables all frequencies
- [ ] UI shows all general frequency checkboxes unchecked by default

### Regression Testing
- [ ] Selected pilot frequencies still default to "all enabled"
- [ ] Per-pilot frequency filtering works correctly
- [ ] Pan configuration unaffected
- [ ] Existing recordings playback correctly with new filtering logic

## Performance Impact

**No Performance Change**: The filtering logic is identical, only the default values changed.

## Rollback Plan

If needed, rollback by reverting these changes:
1. Revert `pan_manager.lua` initialization to enable by default
2. Revert `IsGeneralFrequencyEnabled()` to return true by default
3. Revert C# filter to play all by default when not specified

## Conclusion

This change significantly improves the user experience by:
- Providing immediate audio focus on selected pilots
- Reducing configuration burden for most use cases
- Maintaining full flexibility through explicit opt-in
- Aligning behavior with user expectations

**Status**: ? Documentation Updated  
**Ready for**: Implementation  
**Breaking Change**: No (backward compatible - just changes default)  
**User Impact**: Positive - Better default behavior

---

**Change Version**: 1.0  
**Date**: 2024-01-XX  
**Author**: AeroDebrief Team
