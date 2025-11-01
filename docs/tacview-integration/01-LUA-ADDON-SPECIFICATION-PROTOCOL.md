# protocol.lua - Updated with Frequency Filtering

This document contains the updated `protocol.lua` file with frequency filtering support.

## protocol.lua

```lua
-- Protocol Message Encoding/Decoding with Frequency Filtering
local json = require("json") -- Tacview includes JSON library
local panManager = require("pan_manager")

local Protocol = {}

-- Encode message to JSON string
function Protocol.Encode(message)
    local success, result = pcall(json.encode, message)
    if success then
        return result
    else
        Tacview.Log.Error("Failed to encode message: " .. tostring(result))
        return nil
    end
end

-- Decode JSON string to message
function Protocol.Decode(messageStr)
    local success, result = pcall(json.decode, messageStr)
    if success then
        return result
    else
        Tacview.Log.Error("Failed to decode message: " .. tostring(result))
        return nil
    end
end

-- Create time update message
function Protocol.CreateTimeUpdate(missionTime, playbackState, playbackSpeed)
    -- Convert Tacview time to ISO 8601 UTC
    local utcTime = os.date("!%Y-%m-%dT%H:%M:%S", missionTime) .. "Z"
    
    return {
        type = "time_update",
        mission_time_utc = utcTime,
        playback_state = playbackState == 1 and "playing" or "paused",
        playback_speed = playbackSpeed
    }
end

-- Create pilot selection message with frequency filtering
function Protocol.CreatePilotSelection(pilots)
    local panMode = panManager.GetMode()
    
    -- Update current pilots in pan manager (for frequency tracking)
    panManager.UpdateCurrentPilots(pilots)
    
    -- Add frequency filter information to each pilot
    local pilotsWithFilters = {}
    for _, pilot in ipairs(pilots) do
        local pilotData = {
            pilot_id = pilot.pilot_id,
            pilot_name = pilot.pilot_name,
            coalition = pilot.coalition,
            unit_type = pilot.unit_type,
            unit_id = pilot.unit_id,
            frequencies = pilot.frequencies or {},
            pan = pilot.pan,
            enabled_frequencies = {} -- NEW: List of enabled frequencies for this pilot
        }
        
        -- Add which frequencies are enabled for this pilot
        -- Default: all frequencies enabled for selected pilots
        if pilot.frequencies then
            for _, freq in ipairs(pilot.frequencies) do
                if panManager.IsPilotFrequencyEnabled(pilot.pilot_id, freq) then
                    table.insert(pilotData.enabled_frequencies, freq)
                end
            end
        end
        
        table.insert(pilotsWithFilters, pilotData)
    end
    
    -- Get general frequency filter (for non-selected pilots)
    -- Default: all DISABLED (empty list) - user must explicitly enable
    local generalEnabledFreqs = {}
    local allFrequencies = panManager.GetAllFrequencies()
    for _, freq in ipairs(allFrequencies) do
        if panManager.IsGeneralFrequencyEnabled(freq) then
            table.insert(generalEnabledFreqs, freq)
        end
    end
    
    -- Note: generalEnabledFreqs will be empty by default (all disabled)
    -- This provides audio focus on selected pilots only
    
    return {
        type = "pilot_selection",
        pan_mode = panMode,
        selected_pilots = pilotsWithFilters,
        general_enabled_frequencies = generalEnabledFreqs -- Empty by default = no general audio
    }
end

-- Create playback command message
function Protocol.CreatePlaybackCommand(command)
    return {
        type = "playback_command",
        command = command
    }
end

-- Create seek message
function Protocol.CreateSeek(targetTime)
    local utcTime = os.date("!%Y-%m-%dT%H:%M:%S", targetTime) .. "Z"
    return {
        type = "seek",
        target_time_utc = utcTime
    }
end

return Protocol
```

## Message Format Changes

### Updated pilot_selection Message

```json
{
  "type": "pilot_selection",
  "pan_mode": "manual",
  "selected_pilots": [
    {
      "pilot_id": "F-16C-001-GUID",
      "pilot_name": "Viper 1-1",
      "coalition": "blue",
      "unit_type": "F-16C_50",
      "unit_id": 12345,
      "frequencies": [251.0, 305.0, 133.0],
      "pan": -0.8,
      "enabled_frequencies": [251.0, 305.0]  // NEW: Only these frequencies are enabled
    },
    {
      "pilot_id": "F-16C-002-GUID",
      "pilot_name": "Viper 1-2",
      "coalition": "blue",
      "unit_type": "F-16C_50",
      "unit_id": 12346,
      "frequencies": [251.0, 305.0],
      "pan": 0.8,
      "enabled_frequencies": [251.0]  // NEW: Only 251.0 enabled for this pilot
    }
  ],
  "general_enabled_frequencies": []  // NEW: Empty by default = no general audio
}
```

**Note**: The `general_enabled_frequencies` array is **empty by default**, meaning no audio from non-selected pilots unless explicitly enabled by the user.

### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `enabled_frequencies` | array[number] | Frequencies enabled for this specific pilot (subset of `frequencies`) |
| `general_enabled_frequencies` | array[number] | Frequencies enabled for general listening (non-selected pilots) |

### Usage Examples

#### Example 1: Selected pilot with frequency filtering

```json
{
  "pilot_id": "A-10C-001-GUID",
  "pilot_name": "Hawg 1-1",
  "frequencies": [251.0, 305.0, 133.0],
  "enabled_frequencies": [251.0, 305.0],  // 133.0 disabled via Tacview menu
  "pan": 0.0
}
```

**Behavior**: AeroDebrief will only play audio from this pilot on 251.0 MHz and 305.0 MHz, ignoring transmissions on 133.0 MHz.

#### Example 2: General frequency filtering

```json
{
  "selected_pilots": [
    { "pilot_id": "F-16C-001", "frequencies": [251.0], "enabled_frequencies": [251.0] }
  ],
  "general_enabled_frequencies": [124.0, 243.0]  // User explicitly enabled tower and GCI
}
```

**Behavior**: 
- Selected pilot (F-16C-001) is heard on 251.0 MHz
- All non-selected pilots are heard only on 124.0 MHz and 243.0 MHz
- Non-selected pilots on other frequencies are muted
- **Note**: The user had to explicitly enable 124.0 and 243.0 - they were disabled by default

#### Example 3: All frequencies enabled (default)

```json
{
  "pilot_id": "F-15C-001-GUID",
  "frequencies": [251.0, 305.0],
  "enabled_frequencies": [251.0, 305.0],  // All frequencies enabled
  "pan": -0.5
}
```

**Behavior**: Standard operation - all pilot frequencies are audible.

## Integration Notes

### AeroDebrief Implementation

AeroDebrief's `TacviewSyncService` should:

1. Parse `enabled_frequencies` from each pilot
2. Filter audio packets based on:
   - If packet is from a selected pilot ? check if frequency is in `enabled_frequencies`
   - If packet is from non-selected pilot ? check if frequency is in `general_enabled_frequencies`
3. Only play packets that pass the frequency filter

### Pseudo-code Example

```csharp
bool ShouldPlayPacket(AudioPacketMetadata packet, TacviewPilotSelection selection)
{
    // Check if pilot is selected
    var selectedPilot = selection.SelectedPilots.FirstOrDefault(p => p.PilotId == packet.TransmitterGuid);
    
    if (selectedPilot != null)
    {
        // Selected pilot - check their enabled frequencies
        return selectedPilot.EnabledFrequencies.Contains(packet.Frequency);
    }
    else
    {
        // Non-selected pilot - check general enabled frequencies
        return selection.GeneralEnabledFrequencies.Contains(packet.Frequency);
    }
}
```

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
