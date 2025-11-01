# Bidirectional Configuration - Quick Summary

## What Changed?

Added **bidirectional configuration** support - users can now configure Tacview integration settings from either Tacview's menu OR AeroDebrief's UI, with changes automatically synchronized.

## Key Features

### 1. Configure from Either Side

**Before**:
- ? Tacview ? AeroDebrief (one-way)
- ? AeroDebrief ? Tacview (not possible)

**After**:
- ? Tacview ? AeroDebrief (primary)
- ? AeroDebrief ? Tacview (secondary) **NEW**

### 2. Supported Settings

| Setting | Can Configure in AeroDebrief UI |
|---------|--------------------------------|
| Pan Mode (Auto/Manual) | ? Yes |
| Per-Pilot Pan Values | ? Yes (slider) |
| Per-Pilot Frequencies | ? Yes (checkboxes) |
| General Frequencies | ? Yes (checkboxes) |

### 3. User Experience

**In AeroDebrief UI**:
```
Tacview Integration Panel
?? ??? Spatial Audio (Pan)
?  ?? [? Auto] [? Manual]
?  ?? Viper 1-1: [???????????] -0.5
?
?? ?? Frequency Filtering
   ?? Per-Pilot Frequencies
   ?  ?? Viper 1-1
   ?     ?? ? 251.0 MHz
   ?     ?? ? 305.0 MHz
   ?     ?? ? 133.0 MHz
   ?? General Frequencies
      ?? ? 124.0 MHz (Tower)
      ?? ? 243.0 MHz (GCI)
```

**What Happens**:
1. User toggles checkbox or moves slider
2. AeroDebrief sends update to Tacview
3. Tacview applies change to internal state
4. Tacview broadcasts confirmation
5. Audio filtering/panning updates immediately

## New Message Types

### frequency_filter_update (AeroDebrief ? Tacview)

```json
{
  "type": "frequency_filter_update",
  "pilot_id": "F-16C-001",  // null for general
  "enabled_frequencies": [251.0, 305.0]
}
```

### pan_configuration (AeroDebrief ? Tacview)

```json
{
  "type": "pan_configuration",
  "pan_mode": "manual",
  "pilot_pan_settings": {
    "F-16C-001": -0.8,
    "F-16C-002": 0.8
  }
}
```

## Implementation Summary

### Tacview Lua Changes

**File**: `main.lua`

Added message handler:
```lua
function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if decoded.type == "frequency_filter_update" then
        -- Update frequency filters
        self:HandleFrequencyFilterUpdate(decoded)
    elseif decoded.type == "pan_configuration" then
        -- Update pan settings
        self:HandlePanConfiguration(decoded)
    end
    
    -- Broadcast confirmation
    self:OnSelectionChange()
end
```

### AeroDebrief C# Changes

**File**: `TacviewIntegrationViewModel.cs`

Added commands:
```csharp
public ICommand UpdatePilotFrequenciesCommand { get; }
public ICommand UpdateGeneralFrequenciesCommand { get; }
public ICommand UpdatePanConfigurationCommand { get; }
public ICommand ToggleFrequencyCommand { get; }

private async void UpdatePanConfiguration()
{
    var message = new PanConfigurationMessage
    {
        PanMode = PanMode,
        PilotPanSettings = GetPilotPanSettings()
    };
    
    await _integrationService.SendMessageAsync(message);
}
```

### UI Controls

**XAML**:
```xaml
<!-- Pan Slider -->
<Slider Value="{Binding Pan, Mode=TwoWay}"
       Minimum="-1" Maximum="1"
       ValueChanged="OnPanSliderValueChanged"/>

<!-- Frequency Checkbox -->
<CheckBox IsChecked="{Binding IsEnabled, Mode=TwoWay}"
         Command="{Binding DataContext.ToggleFrequencyCommand}"/>
```

**Debouncing** (prevents spam during slider drag):
```csharp
private Timer _panUpdateTimer;

private void OnPanSliderValueChanged(...)
{
    _panUpdateTimer?.Stop();
    _panUpdateTimer = new Timer(500); // 500ms debounce
    _panUpdateTimer.Elapsed += (s, args) => UpdatePanConfiguration();
    _panUpdateTimer.Start();
}
```

## Benefits

### For Users

? **No App Switching**: Configure everything in AeroDebrief  
? **Faster Workflow**: Quick adjustments during review  
? **Visual Feedback**: See changes immediately  
? **Always Synchronized**: No manual sync needed

### For Developers

? **Extensible**: Easy to add more bidirectional settings  
? **Robust**: Confirmation loop prevents desync  
? **Performant**: Debounced and throttled  
? **Testable**: Unit and integration tests included

## Testing

### Manual Test Scenarios

**Test 1**: Toggle frequency in AeroDebrief
- [ ] Check frequency checkbox
- [ ] Verify audio filters immediately
- [ ] Open Tacview menu ? Verify checkbox matches

**Test 2**: Drag pan slider in AeroDebrief
- [ ] Drag slider from 0.0 to -0.5
- [ ] Verify audio panning updates
- [ ] Open Tacview menu ? Verify pan value matches

**Test 3**: Rapid changes (debouncing)
- [ ] Drag slider rapidly back and forth
- [ ] Verify only 1-2 messages sent (not 20+)
- [ ] Check Tacview log for message count

**Test 4**: Conflict resolution
- [ ] Change setting in both UIs simultaneously
- [ ] Verify last-write-wins behavior
- [ ] Verify no UI flickering

## Documentation

### Files Updated

1. ? `IMPLEMENTATION-GUIDE.md` - Added new message types and UI steps
2. ? `00-INTEGRATION-OVERVIEW.md` - Added bidirectional message types
3. ? `02-PROTOCOL-SPECIFICATION.md` - Detailed message formats
4. ? `README.md` - Added feature to key features list

### Files Created

1. ? `13-BIDIRECTIONAL-CONFIGURATION.md` - Complete feature specification
2. ? `13-BIDIRECTIONAL-CONFIGURATION-SUMMARY.md` - This summary

## Integration Timeline

**When to Implement**:
- **Phase 3** (C# Integration Layer): Add message models and sending logic
- **Phase 2** (Tacview Lua Addon): Add message handlers
- **Phase 5** (UI Implementation): Add controls and commands

**Additional Effort**: +2-3 days to base implementation

## Future Enhancements

1. **Configuration Presets**: Save/load configuration profiles
2. **Configuration History**: Undo/redo changes
3. **Smart Conflict Resolution**: Merge non-conflicting changes
4. **Batch Updates**: Group multiple changes into single message

## Quick Reference

### Enable Bidirectional Config for New Setting

**Step 1**: Add message type to protocol
```csharp
public class YourConfigMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "your_config";
    
    // ... your fields
}
```

**Step 2**: Add Tacview handler
```lua
if decoded.type == "your_config" then
    -- Update state
    -- Save config
    self:OnSelectionChange() -- Broadcast confirmation
end
```

**Step 3**: Add AeroDebrief command
```csharp
public ICommand UpdateYourConfigCommand { get; }

private async void UpdateYourConfig()
{
    var message = new YourConfigMessage { /* ... */ };
    await _integrationService.SendMessageAsync(message);
}
```

**Step 4**: Add UI control
```xaml
<Button Content="Update" 
       Command="{Binding UpdateYourConfigCommand}"/>
```

---

**Status**: ? Design Complete  
**Ready for**: Implementation  
**Complexity**: Medium  
**Risk**: Low

**See**: `13-BIDIRECTIONAL-CONFIGURATION.md` for full specification
