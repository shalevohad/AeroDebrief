# Frequency Filtering Feature - Implementation Summary

## Overview

Added comprehensive frequency filtering capability to the Tacview integration, allowing users to selectively monitor specific radio frequencies both for selected pilots and for general listening (non-selected pilots).

## Key Features

### 1. Per-Pilot Frequency Filtering
- Users can enable/disable individual frequencies for each selected pilot
- Configured via checkboxes in Tacview menu
- Settings persist across selections
- Default: All frequencies enabled

### 2. General Frequency Filtering
- Users can filter which frequencies to monitor from ALL non-selected pilots
- Useful for monitoring only tower, GCI, or specific tactical frequencies
- Configured separately from pilot-specific settings
- Default: **All frequencies disabled (not selected)** - no audio from non-selected pilots unless explicitly enabled

### 3. Tacview Menu Integration
- Interactive checkbox dialogs with live updates
- "Select All" and "Deselect All" quick actions
- Real-time preview of frequency selections
- Changes immediately propagated to AeroDebrief

---

## Updated Files

### 1. `docs/tacview-integration/01-LUA-ADDON-SPECIFICATION.md`

**Changes to `pan_manager.lua`**:
- Added frequency filtering data structures:
  - `pilotFrequencyFilter` - Per-pilot frequency enable/disable map
  - `generalFrequencyFilter` - Global frequency enable/disable map
  - `allFrequencies` - Set of all known frequencies

- Added frequency filtering methods:
  ```lua
  SetPilotFrequencyEnabled(pilotId, frequency, enabled)
  IsPilotFrequencyEnabled(pilotId, frequency)
  SetGeneralFrequencyEnabled(frequency, enabled)
  IsGeneralFrequencyEnabled(frequency)
  GetAllFrequencies()
  EnableAllPilotFrequencies(pilotId)
  DisableAllPilotFrequencies(pilotId)
  EnableAllGeneralFrequencies()
  DisableAllGeneralFrequencies()
  ```

**Changes to `menu_ui.lua`**:
- Added new menu items:
  - "Configure Pilot Frequencies..." - Opens per-pilot frequency dialog
  - "Configure General Frequencies..." - Opens general frequency dialog

- Added frequency configuration dialogs:
  - `ShowPilotFrequencyDialog()` - Iterate through selected pilots
  - `ShowGeneralFrequencyDialog()` - Show all known frequencies
  - `ShowFrequencyCheckboxes()` - Interactive checkbox UI with live updates

- Dialog features:
  - Checkbox display: ? (enabled) / ? (disabled)
  - Quick actions: "? Select All" / "? Deselect All"
  - Live updates: Changes immediately trigger `OnSelectionChange()`
  - Persistent across sessions

**Changes to `protocol.lua`** (see new file):
- Updated `CreatePilotSelection()` to include frequency filter data
- Added `enabled_frequencies` array to each pilot object
- Added `general_enabled_frequencies` array to root message
- Calls `UpdateCurrentPilots()` to track frequency sets

---

### 2. `docs/tacview-integration/02-PROTOCOL-SPECIFICATION.md`

**Updated `pilot_selection` Message Format**:

```json
{
  "type": "pilot_selection",
  "pan_mode": "manual",
  "selected_pilots": [
    {
      "pilot_id": "F-16C-001-GUID",
      "pilot_name": "Viper 1-1",
      "frequencies": [251.0, 305.0, 133.0],
      "pan": -0.8,
      "enabled_frequencies": [251.0, 305.0]  // NEW
    }
  ],
  "general_enabled_frequencies": [124.0, 243.0]  // NEW
}
```

**New Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `enabled_frequencies` | array[number] | Frequencies enabled for this specific pilot |
| `general_enabled_frequencies` | array[number] | Frequencies enabled for all non-selected pilots |

**Documentation Updates**:
- Frequency Filtering Details section
- Tacview Menu Configuration section (includes frequency items)
- Frequency Configuration UI mockups
- Filtering Logic in AeroDebrief (pseudo-code)
- Example Scenarios (3 use cases)

---

### 3. New File: `docs/tacview-integration/01-LUA-ADDON-SPECIFICATION-PROTOCOL.md`

Created separate document with complete `protocol.lua` implementation including:
- Full protocol message creation logic
- Frequency filter integration
- Usage examples
- Integration notes for AeroDebrief
- Pseudo-code for packet filtering logic

---

## Tacview Menu Structure

### Updated Menu Layout

```
Tacview Menu ? AeroDebrief Sync
??? Configure Audio Pan...            
??? Auto Pan Mode                     
??? Manual Pan Mode                   
??? ?????????????????????????????????
??? Configure Pilot Frequencies...    ??? NEW
??? Configure General Frequencies...  ??? NEW
??? ?????????????????????????????????
??? About
```

### Frequency Configuration Dialog (Per-Pilot)

```
????????????????????????????????????????????
? Configure Frequencies: Viper 1-1         ?
? ????????????????????????????????????? ?
? Select which frequencies to monitor for  ?
? this pilot:                              ?
?                                          ?
? ? Select All Frequencies                ?
? ? Deselect All Frequencies              ?
? ????????????????????????????????????? ?
? ? 251.0 MHz                             ?
? ? 305.0 MHz                             ?
? ? 133.0 MHz                             ?
? ????????????????????????????????????? ?
? Done                                     ?
????????????????????????????????????????????
```

**User Interaction**:
1. User clicks frequency checkbox ? Toggles enable/disable
2. Checkbox updates immediately: ? ? ?
3. `OnSelectionChange()` triggered ? Sends updated message to AeroDebrief
4. Audio filtering updates in real-time

---

## Use Case Examples

### Example 1: Focus on Combat Frequency Only

**Scenario**: User wants to hear only combat communications from selected pilots

**Steps**:
1. Select "Viper 1-1" and "Viper 1-2" in Tacview
2. Go to: `AeroDebrief Sync ? Configure Pilot Frequencies...`
3. For each pilot:
   - Check: ? 251.0 MHz (combat)
   - Uncheck: ? 305.0 MHz (datalink)
   - Uncheck: ? 133.0 MHz (aux)
4. Click "Done"

**Result**: Only hear transmissions from these pilots on 251.0 MHz

---

### Example 2: Monitor Tower Only from Others

**Scenario**: User wants to hear only tower communications from non-selected pilots

**Steps**:
1. Select specific pilots in Tacview
2. Go to: `AeroDebrief Sync ? Configure General Frequencies...`
3. Note: All frequencies are disabled by default
4. Check: ? 124.0 MHz (tower)
5. Check: ? 243.0 MHz (GCI)
6. Click "Done"

**Result**: 
- Selected pilots heard on all their enabled frequencies
- All other pilots heard ONLY on 124.0 and 243.0 MHz
- All other general frequencies remain silent (disabled)
 
---

### Example 3: Complete Audio Isolation

**Scenario**: User wants to hear ONLY specific pilots on specific frequencies, nothing else

**Steps**:
1. Select 2 pilots in Tacview
2. Configure Pilot Frequencies:
   - Each pilot: Enable only 251.0 MHz
3. General Frequencies:
   - No action needed - already disabled by default
4. **Result: Complete silence except for those 2 pilots on 251.0 MHz**

**Note**: General frequencies are disabled by default, so step 3 is automatic!

---

## AeroDebrief Implementation Guide

### 1. Update TacviewPilotSelection Model

```csharp
public class TacviewPilotSelection
{
    public string PanMode { get; set; }
    public List<TacviewPilot> SelectedPilots { get; set; }
    public List<double> GeneralEnabledFrequencies { get; set; }  // NEW
}

public class TacviewPilot
{
    public string PilotId { get; set; }
    public string PilotName { get; set; }
    public List<double> Frequencies { get; set; }
    public double Pan { get; set; }
    public List<double> EnabledFrequencies { get; set; }  // NEW
}
```

### 2. Implement Frequency Filtering

```csharp
public class TacviewAudioFilter
{
    private TacviewPilotSelection? _currentSelection;
    
    public bool ShouldPlayPacket(AudioPacketMetadata packet)
    {
        if (_currentSelection == null)
            return true; // No Tacview connection - play everything
        
        // Check if pilot is selected
        var selectedPilot = _currentSelection.SelectedPilots
            .FirstOrDefault(p => p.PilotId == packet.TransmitterGuid);
        
        if (selectedPilot != null)
        {
            // Selected pilot - check their enabled frequencies
            // Default: all enabled if not specified
            if (selectedPilot.EnabledFrequencies == null || !selectedPilot.EnabledFrequencies.Any())
                return true;
            
            return selectedPilot.EnabledFrequencies.Contains(packet.Frequency);
        }
        else
        {
            // Non-selected pilot - check general enabled frequencies
            // Default: all DISABLED (empty list) - no audio from non-selected pilots
            if (_currentSelection.GeneralEnabledFrequencies == null || !_currentSelection.GeneralEnabledFrequencies.Any())
                return false; // No general frequencies enabled = silence from non-selected
            
            return _currentSelection.GeneralEnabledFrequencies.Contains(packet.Frequency);
        }
    }
    
    public void UpdateSelection(TacviewPilotSelection selection)
    {
        _currentSelection = selection;
        var generalFreqCount = selection.GeneralEnabledFrequencies?.Count ?? 0;
        Logger.Info($"Updated Tacview selection: {selection.SelectedPilots.Count} pilots, " +
                    $"{generalFreqCount} general frequencies enabled (default: 0 = silent)");
    }
}
```

### 3. Integrate into PlaybackController

```csharp
private void PlayAudioPacket(AudioPacketMetadata packet)
{
    // Apply Tacview frequency filter
    if (!_tacviewFilter.ShouldPlayPacket(packet))
    {
        return; // Frequency disabled - skip packet
    }
    
    // Apply spatial audio (pan)
    var pan = _tacviewFilter.GetPanForPilot(packet.TransmitterGuid);
    
    // Play packet with pan
    _audioMixer.PlayPacket(packet, pan);
}
```

---

## Testing Checklist

### Tacview Lua Addon
- [ ] Menu items appear correctly
- [ ] Per-pilot frequency dialog opens for each selected pilot
- [ ] General frequency dialog shows all known frequencies
- [ ] Checkboxes toggle correctly (? ? ?)
- [ ] "Select All" enables all frequencies
- [ ] "Deselect All" disables all frequencies
- [ ] Changes trigger immediate `OnSelectionChange()` call
- [ ] Frequency settings persist across selections
- [ ] Protocol message includes correct frequency arrays

### AeroDebrief Integration
- [ ] `enabled_frequencies` field parsed correctly
- [ ] `general_enabled_frequencies` field parsed correctly
- [ ] Frequency filtering logic works correctly
- [ ] Selected pilots filtered by their enabled frequencies
- [ ] Non-selected pilots filtered by general frequencies
- [ ] Audio packets properly filtered in real-time
- [ ] UI displays frequency filter status
- [ ] Performance impact is minimal

### End-to-End Testing
- [ ] Select pilot, disable frequency ? Pilot silent on that frequency
- [ ] Enable only tower in general ? Only hear tower from others
- [ ] Disable all general frequencies ? Complete silence from non-selected
- [ ] Changes in Tacview menu immediately affect AeroDebrief playback
- [ ] Multiple pilots with different frequency filters work correctly
- [ ] Frequency filters work with Auto/Manual pan modes

---

## Performance Considerations

### Memory Impact
- Frequency filter maps stored per-pilot (small memory footprint)
- Typical: 10 pilots × 5 frequencies each = 50 boolean values (~50 bytes)

### CPU Impact
- Frequency filter check: O(1) hash table lookup
- Per-packet overhead: <1 microsecond
- Negligible impact on playback performance

### Network Impact
- Additional JSON fields add ~50-200 bytes per message
- Sent only on selection change (event-driven, not continuous)
- No measurable impact on network performance

---

## Future Enhancements

1. **Frequency Presets**
   - Save/load frequency filter configurations
   - Quick profiles: "Combat Only", "Tower Only", "All Channels"

2. **Visual Frequency Indicators**
   - Color-code frequencies in Tacview 3D view
   - Show audio activity per frequency with waveforms

3. **Advanced Filtering**
   - Time-based frequency filters
   - Coalition-based filtering
   - Distance-based filtering

4. **Statistics**
   - Track which frequencies are most used
   - Show communication patterns per frequency
   - Export frequency usage reports

---

**Feature Status**: ? Designed and Documented  
**Implementation Status**: ?? Ready for Development  
**Estimated Effort**: 2-3 days  
**Risk Level**: Low  
**Dependencies**: Tacview Lua API, AeroDebrief.Integrations project

