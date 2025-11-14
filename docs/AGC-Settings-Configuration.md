# AGC Settings Configuration

## Overview
Automatic Gain Control (AGC) settings have been moved from hardcoded constants to user-configurable settings via the `PlayerSettingsStore`. This allows users to customize AGC behavior through the UI without recompiling.

## Changes Made

### 1. Settings Infrastructure (`PlayerSettingsStore.cs`)

#### New Settings Keys
```csharp
public enum PlayerSettingKeys
{
    // ... existing settings ...
    
    // Audio Mixing Settings (AGC)
    AGC_TargetDB,      // Target RMS level in dB
    AGC_MaxBoostDB,    // Maximum amplification in dB
    AGC_MaxCutDB,      // Maximum attenuation in dB
    AGC_Enabled,       // Enable/disable AGC
}
```

#### Default Values
```csharp
AGC_TargetDB     = -20.0 dB  // Good speaking level
AGC_MaxBoostDB   = +20.0 dB  // Maximum 10x amplification
AGC_MaxCutDB     = -10.0 dB  // Maximum 0.316x attenuation
AGC_Enabled      = true      // AGC enabled by default
```

#### Helper Methods
```csharp
// Getters
double GetAGCTargetDB()
double GetAGCMaxBoostDB()
double GetAGCMaxCutDB()
bool GetAGCEnabled()

// Setter
void SaveAGCSettings(double targetDB, double maxBoostDB, double maxCutDB, bool enabled)
```

### 2. Constants Fallback (`Constants.cs`)

Constants remain in `Constants.cs` as fallback values if settings are corrupt or missing:

```csharp
public const double AGC_TARGET_DB = -20.0;
public const double AGC_MAX_BOOST_DB = 20.0;
public const double AGC_MAX_CUT_DB = -10.0;
```

### 3. MasterMixer Integration (`MasterMixer.cs`)

#### Initialization Logging
```csharp
public MasterMixer(IAudioOutputEngine audioOutput)
{
    // ... initialization ...
    
    var settings = Settings.PlayerSettingsStore.Instance;
    Logger.Info($"AGC Configuration: Enabled={settings.GetAGCEnabled()}, " +
                $"Target={settings.GetAGCTargetDB():F1} dB");
}
```

#### AGC Calculation
```csharp
private float CalculateAGCGain(ReadOnlySpan<float> audioBlock)
{
    // Check if AGC is enabled
    if (!settings.GetAGCEnabled())
        return 1.0f; // Unity gain, no normalization
    
    // Use settings with fallback to constants
    var targetDb = settings.GetAGCTargetDB();
    var maxBoostDb = settings.GetAGCMaxBoostDB();
    var maxCutDb = settings.GetAGCMaxCutDB();
    
    // Apply RMS-based normalization...
}
```

## Configuration File

Settings are stored in `configs/player.cfg`:

```ini
[Player Settings]
# Audio Mixing Settings
AGC_TargetDB = -20.0
AGC_MaxBoostDB = 20.0
AGC_MaxCutDB = -10.0
AGC_Enabled = true
```

## UI Implementation (Future)

### Recommended UI Controls

**Settings Panel > Audio Mixing > Automatic Gain Control**

```
???????????????????????????????????????????
? Automatic Gain Control (AGC)           ?
???????????????????????????????????????????
? [?] Enable AGC                          ?
?                                         ?
? Target Level:      -20 dB ???????????  ?
?                    (Range: -30 to -10)  ?
?                                         ?
? Max Boost:         +20 dB ???????????  ?
?                    (Range: 0 to 30)     ?
?                                         ?
? Max Cut:           -10 dB ???????????  ?
?                    (Range: -20 to 0)    ?
?                                         ?
? [Reset to Defaults]                     ?
???????????????????????????????????????????
```

### Usage Example (XAML)

```xml
<GroupBox Header="Automatic Gain Control (AGC)">
    <StackPanel>
        <CheckBox x:Name="AGCEnabledCheckbox" 
                  Content="Enable AGC"
                  IsChecked="{Binding AGCEnabled}"/>
        
        <Label Content="Target Level (dB):"/>
        <Slider x:Name="AGCTargetSlider"
                Minimum="-30" Maximum="-10" 
                Value="{Binding AGCTargetDB}"
                TickFrequency="1" IsSnapToTickEnabled="True"/>
        
        <Label Content="Max Boost (dB):"/>
        <Slider x:Name="AGCMaxBoostSlider"
                Minimum="0" Maximum="30"
                Value="{Binding AGCMaxBoostDB}"
                TickFrequency="1" IsSnapToTickEnabled="True"/>
        
        <Label Content="Max Cut (dB):"/>
        <Slider x:Name="AGCMaxCutSlider"
                Minimum="-20" Maximum="0"
                Value="{Binding AGCMaxCutDB}"
                TickFrequency="1" IsSnapToTickEnabled="True"/>
                
        <Button Content="Reset to Defaults" 
                Click="ResetAGCDefaults_Click"/>
    </StackPanel>
</GroupBox>
```

### ViewModel (C#)

```csharp
public class AudioSettingsViewModel : INotifyPropertyChanged
{
    private readonly PlayerSettingsStore _settings;
    
    public bool AGCEnabled
    {
        get => _settings.GetAGCEnabled();
        set
        {
            _settings.SetPlayerSetting(PlayerSettingKeys.AGC_Enabled, value);
            OnPropertyChanged();
        }
    }
    
    public double AGCTargetDB
    {
        get => _settings.GetAGCTargetDB();
        set
        {
            _settings.SetPlayerSetting(PlayerSettingKeys.AGC_TargetDB, value);
            OnPropertyChanged();
        }
    }
    
    // Similar for MaxBoostDB and MaxCutDB...
    
    public void ResetToDefaults()
    {
        _settings.SaveAGCSettings(
            Constants.AGC_TARGET_DB,
            Constants.AGC_MAX_BOOST_DB,
            Constants.AGC_MAX_CUT_DB,
            true);
        
        // Refresh UI
        OnPropertyChanged(nameof(AGCEnabled));
        OnPropertyChanged(nameof(AGCTargetDB));
        OnPropertyChanged(nameof(AGCMaxBoostDB));
        OnPropertyChanged(nameof(AGCMaxCutDB));
    }
}
```

## Benefits

### For Users
- ? **Customizable Audio Levels**: Adjust target loudness to preference
- ? **Control Over Dynamics**: Fine-tune boost/cut limits
- ? **Quick Toggle**: Enable/disable AGC without restart
- ? **Persistent Settings**: Configuration saved between sessions

### For Developers
- ? **No Recompilation**: Change AGC behavior via config file
- ? **Easy Testing**: Test different AGC parameters quickly
- ? **Fallback Safety**: Constants provide safe defaults
- ? **Runtime Flexibility**: Settings read per-frame for live updates

## AGC Behavior Guide

### Target Level (`AGC_TargetDB`)
- **Purpose**: Desired RMS loudness level for all pilots
- **Range**: -30 dB to -10 dB
- **Default**: -20 dB (good speaking level)
- **Effect**:
  - Lower (e.g., -25 dB): Quieter overall, more headroom
  - Higher (e.g., -15 dB): Louder overall, less headroom

### Max Boost (`AGC_MaxBoostDB`)
- **Purpose**: Maximum amplification for quiet pilots
- **Range**: 0 dB to +30 dB
- **Default**: +20 dB (10x amplification)
- **Effect**:
  - Lower (e.g., +10 dB): Less boost for quiet pilots
  - Higher (e.g., +30 dB): More aggressive boost (may increase noise)

### Max Cut (`AGC_MaxCutDB`)
- **Purpose**: Maximum attenuation for loud pilots
- **Range**: -20 dB to 0 dB
- **Default**: -10 dB (0.316x attenuation)
- **Effect**:
  - Lower (e.g., -15 dB): More aggressive cutting of loud pilots
  - Higher (e.g., -5 dB): Less attenuation, preserves dynamics

## Testing

### Verify Settings Load
```csharp
var settings = PlayerSettingsStore.Instance;
Assert.AreEqual(-20.0, settings.GetAGCTargetDB());
Assert.IsTrue(settings.GetAGCEnabled());
```

### Test AGC Behavior
```csharp
// With AGC enabled
settings.SaveAGCSettings(-20.0, 20.0, -10.0, true);
var gain = CalculateAGCGain(quietAudio);
Assert.IsTrue(gain > 1.0f); // Should boost quiet audio

// With AGC disabled
settings.SetPlayerSetting(PlayerSettingKeys.AGC_Enabled, false);
gain = CalculateAGCGain(quietAudio);
Assert.AreEqual(1.0f, gain); // Should pass through unmodified
```

## Migration Notes

- **Existing Users**: Settings will be initialized with defaults on first run with the new version
- **Config Files**: Old `player.cfg` files will be updated automatically with AGC settings
- **Backward Compatibility**: Constants remain as fallback for tests and edge cases

## Summary

AGC settings are now fully configurable via `PlayerSettingsStore`, allowing users to customize audio normalization behavior through the UI. The system maintains fallback to `Constants.cs` for safety and provides a robust, user-friendly approach to audio level management.
