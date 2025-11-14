# AGC Settings UI Implementation

## Overview
The Automatic Gain Control (AGC) settings UI has been successfully implemented in the AeroDebrief.UI project, allowing users to configure AGC behavior through an intuitive settings interface.

## Implementation Details

### 1. UI Layout (SettingsWindow.xaml)

#### Location
**Settings Window ? Audio Tab ? Automatic Gain Control Section**

#### Features
- **Prominent Section Header** with emoji icon (???)
- **Enable/Disable Toggle** - Master switch for AGC
- **Collapsible Controls** - Settings only visible when AGC is enabled
- **Visual Hierarchy** - Highlighted border with accent color
- **Three Configuration Sliders**:
  1. **Target Level** (-30 to -10 dB)
  2. **Maximum Boost** (0 to +30 dB)
  3. **Maximum Cut** (-20 to 0 dB)
- **Reset Button** - Quick restore to defaults
- **Real-time Value Display** - Shows current slider values

#### UI Controls

```xml
???????????????????????????????????????????????????????
? ??? Automatic Gain Control (AGC)   [?] Enable AGC  ?
???????????????????????????????????????????????????????
? Normalizes pilot audio levels for consistent volume?
?                                                     ?
? Target Level (dB)                         -20.0 dB ?
? ??????????????????????????????????????????        ?
? Desired loudness level for all pilots              ?
?                                                     ?
? Maximum Boost (dB)                        +20.0 dB ?
? ??????????????????????????????????????????        ?
? Maximum amplification for quiet pilots             ?
?                                                     ?
? Maximum Cut (dB)                          -10.0 dB ?
? ???????????????????????????????????????????        ?
? Maximum attenuation for loud pilots                ?
?                                                     ?
? [Reset AGC to Defaults]                            ?
???????????????????????????????????????????????????????
```

### 2. ViewModel (SettingsViewModel.cs)

#### New Properties

```csharp
public bool AGCEnabled { get; set; }        // Enable/disable toggle
public double AGCTargetDB { get; set; }     // Target level (-30 to -10)
public double AGCMaxBoostDB { get; set; }   // Max boost (0 to +30)
public double AGCMaxCutDB { get; set; }     // Max cut (-20 to 0)
```

#### Key Methods

```csharp
// Load AGC settings from PlayerSettingsStore
public void LoadSettings()
{
    AGCEnabled = store.GetAGCEnabled();
    AGCTargetDB = store.GetAGCTargetDB();
    AGCMaxBoostDB = store.GetAGCMaxBoostDB();
    AGCMaxCutDB = store.GetAGCMaxCutDB();
}

// Save AGC settings to PlayerSettingsStore
public void SaveSettings()
{
    store.SaveAGCSettings(AGCTargetDB, AGCMaxBoostDB, AGCMaxCutDB, AGCEnabled);
}

// Reset AGC to default values
public void ResetAGCToDefaults()
{
    AGCEnabled = true;
    AGCTargetDB = Constants.AGC_TARGET_DB;      // -20.0 dB
    AGCMaxBoostDB = Constants.AGC_MAX_BOOST_DB; // +20.0 dB
    AGCMaxCutDB = Constants.AGC_MAX_CUT_DB;     // -10.0 dB
}
```

### 3. Code-Behind (SettingsWindow.xaml.cs)

#### Reset AGC Button Handler

```csharp
private void ResetAGC_Click(object sender, RoutedEventArgs e)
{
    var result = MessageBox.Show(
        "Reset Automatic Gain Control settings to default values?\n\n" +
        $"Target Level: {Constants.AGC_TARGET_DB:F1} dB\n" +
        $"Max Boost: +{Constants.AGC_MAX_BOOST_DB:F1} dB\n" +
        $"Max Cut: {Constants.AGC_MAX_CUT_DB:F1} dB",
        "Reset AGC Settings",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result == MessageBoxResult.Yes)
    {
        _viewModel.ResetAGCToDefaults();
        _hasUnsavedChanges = true;
    }
}
```

## User Experience

### Opening Settings
1. **Menu**: Tools ? Settings (or press `F10` if configured)
2. **Tab**: Click "Audio" in the left sidebar
3. **Section**: AGC settings appear at the top of the Audio panel

### Configuring AGC
1. **Enable/Disable**: Check/uncheck the "Enable AGC" checkbox
2. **Adjust Sliders**: Drag sliders to desired values
3. **Real-time Feedback**: Current values displayed next to each slider
4. **Tooltips**: Hover over controls for help text

### Saving Changes
- **Apply**: Saves settings without closing window
- **OK**: Saves settings and closes window
- **Cancel**: Discards changes (warns if unsaved)

### Resetting AGC
1. Click "Reset AGC to Defaults" button
2. Confirm in dialog showing default values
3. Click "Apply" or "OK" to save

## Visual Design

### Color Scheme
- **Primary**: Accent blue border (#1976D2)
- **Background**: Secondary background (lighter than main)
- **Text**: Primary text for headers, secondary for descriptions
- **Sliders**: Modern flat design matching app theme

### Responsive Layout
- **Minimum Width**: 700px
- **Slider Width**: Full width with value display
- **Button Alignment**: Left-aligned for consistency
- **Spacing**: Consistent 12-16px margins

### Accessibility
- **Clear Labels**: Descriptive text for each control
- **Help Text**: Gray sub-text explaining each setting
- **Value Display**: Monospace font for precise values
- **Keyboard Support**: Full tab navigation

## Integration with Core

### Settings Flow
```
UI (SettingsWindow)
    ?
ViewModel (SettingsViewModel)
    ?
Settings Store (PlayerSettingsStore)
    ?
Configuration File (player.cfg)
    ?
Audio Engine (MasterMixer)
```

### Real-time Updates
- Settings saved to `configs/player.cfg` immediately
- MasterMixer reads settings on next audio frame
- No application restart required for AGC changes

### Fallback Safety
- If settings corrupt, falls back to `Constants.AGC_*`
- Default values always available
- Settings validated on load

## Testing Checklist

### UI Tests
- ? AGC section visible in Audio tab
- ? Enable checkbox toggles controls visibility
- ? Sliders snap to tick marks (1 dB increments)
- ? Value displays update in real-time
- ? Reset button restores defaults
- ? Apply/OK/Cancel buttons work correctly
- ? Unsaved changes warning appears

### Integration Tests
- ? Settings load from `player.cfg` on open
- ? Settings save to `player.cfg` on Apply/OK
- ? AGC_Enabled toggle respected by MasterMixer
- ? Slider values applied to audio processing
- ? Reset to defaults restores Constants values

### Edge Cases
- ? Settings file missing ? defaults used
- ? Invalid values ? clamped to valid ranges
- ? Corrupt config ? fallback to Constants
- ? Rapid slider changes ? smooth updates

## Usage Examples

### Example 1: Quiet Pilots
**Problem**: Some pilots are too quiet
**Solution**:
1. Open Settings ? Audio
2. Increase "Maximum Boost" from +20 dB to +25 dB
3. Click Apply
4. Quiet pilots will be amplified more

### Example 2: Loud Pilots
**Problem**: Some pilots are too loud
**Solution**:
1. Open Settings ? Audio
2. Decrease "Maximum Cut" from -10 dB to -15 dB
3. Click Apply
4. Loud pilots will be attenuated more

### Example 3: Overall Volume
**Problem**: Everyone sounds too quiet
**Solution**:
1. Open Settings ? Audio
2. Increase "Target Level" from -20 dB to -15 dB
3. Click Apply
4. All pilots will be normalized to a louder level

### Example 4: Disable AGC
**Problem**: Want natural dynamics, no normalization
**Solution**:
1. Open Settings ? Audio
2. Uncheck "Enable AGC"
3. Click Apply
4. Audio passes through without gain control

## Future Enhancements

### Potential Features
- **AGC Presets**: "Soft", "Moderate", "Aggressive"
- **Per-Frequency AGC**: Different settings per radio channel
- **Visual Meter**: Real-time AGC gain display
- **Attack/Release**: Time-based AGC response controls
- **Limiter**: Additional hard limiter for safety

### Advanced Options
- **Noise Gate**: Threshold for silence detection
- **Compression Ratio**: More sophisticated dynamics control
- **Look-ahead**: Predictive gain reduction
- **Side-chain**: Ducking for ATC priority

## Documentation Links

- **AGC Configuration Guide**: `docs/AGC-Settings-Configuration.md`
- **Settings Store API**: `src/AeroDebrief.Core/Settings/PlayerSettingsStore.cs`
- **Master Mixer Implementation**: `src/AeroDebrief.Core/Audio/MasterMixer.cs`
- **Constants Reference**: `src/AeroDebrief.Core/Constants.cs`

## Summary

The AGC settings UI provides a user-friendly interface for configuring automatic gain control in AeroDebrief. With intuitive sliders, real-time feedback, and persistent settings storage, users can easily customize audio normalization to their preferences without requiring technical knowledge or code changes.

**Key Benefits**:
- ? **Easy Configuration**: No editing config files manually
- ? **Real-time Preview**: See values as you adjust
- ? **Persistent Settings**: Saved between sessions
- ? **Quick Reset**: One-click restore to defaults
- ? **Professional UI**: Matches application design language
- ? **Fully Integrated**: Connected to audio engine
