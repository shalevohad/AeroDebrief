# Visual Transmission Indicators - Quick Reference

## ?? Quick Start

### Enable Visual Effects

1. Open Tacview with AeroDebrief Sync addon installed
2. Navigate to: `AeroDebrief Sync ? Visual Effects`
3. Check: ? Show Frequency Labels
4. Check: ? Show Radio Waves

### Send Speaking Status from AeroDebrief

```csharp
// When audio packet starts
var message = new SpeakingStatusMessage
{
    PilotId = packet.TransmitterGuid,
    PilotName = "Viper 1-1",
    Frequency = 251000000.0,  // 251.0 MHz
    IsSpeaking = true,
    TimestampUtc = DateTime.UtcNow.ToString("o")
};
await tacviewClient.SendMessageAsync(message);

// When audio packet ends
message.IsSpeaking = false;
await tacviewClient.SendMessageAsync(message);
```

---

## ?? Visual Elements

### Frequency Label

**Appearance**:
```
  TX: 251.000 MHz  ? Color-coded text
  Viper 1-1        ? Pilot name (optional)
      ?
    [F-16]         ? Aircraft
```

**Properties**:
- Height: 50m above aircraft (configurable)
- Size: 1.5x normal text (configurable)
- Duration: Visible during transmission + 1s fade-out
- Color: Auto-assigned based on frequency

### Radio Wave Effect

**Appearance**:
```
   ? ? ? ? ?      ? Expanding circles
  ?   [F-16]  ?
   ? ? ? ? ?
```

**Properties**:
- Radius: 100m (configurable: 50-500m)
- Duration: 1 second per wave
- Color: Matches frequency label
- Animation: Continuous while transmitting

---

## ?? Frequency Colors

| Frequency | Color | Hex Code |
|-----------|-------|----------|
| 251.0 MHz | ?? Green | 0xFF00FF00 |
| 264.0 MHz | ?? Light Blue | 0xFF0080FF |
| 305.0 MHz | ?? Orange | 0xFFFF8000 |
| 127.5 MHz | ?? Magenta | 0xFFFF00FF |
| Other | ?? Auto-generated | HSV-based |

---

## ?? Configuration

### Menu Access

**Path**: `AeroDebrief Sync ? Visual Effects`

**Quick Toggles**:
- ? Show Frequency Labels
- ? Show Radio Waves

**Advanced Settings**: `Configure Effect Settings...`

### Effect Settings Dialog

| Parameter | Range | Default | Description |
|-----------|-------|---------|-------------|
| Label Height | 10-200m | 50m | Height above aircraft |
| Wave Radius | 50-500m | 100m | Maximum wave expansion |

### Config File

**Location**: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt`

**Format**:
```ini
[VisualEffects]
EnableTextLabels=true
EnableRadioWaves=true
TextLabelHeight=50.0
RadioWaveRadius=100.0
```

---

## ?? Protocol Reference

### Message Format

```json
{
  "type": "speaking_status",
  "pilot_id": "b1c3f7a2-4d5e-6f89-0123-456789abcdef",
  "pilot_name": "Viper 1-1",
  "frequency": 251000000.0,
  "is_speaking": true,
  "timestamp_utc": "2024-01-15T14:30:45.123Z"
}
```

### C# Message Class

```csharp
public class SpeakingStatusMessage
{
    public string Type { get; set; } = "speaking_status";
    public string PilotId { get; set; } = string.Empty;
    public string? PilotName { get; set; }
    public double Frequency { get; set; }
    public bool IsSpeaking { get; set; }
    public string TimestampUtc { get; set; } = string.Empty;
}
```

### Lua Handler

```lua
-- In main.lua
function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if decoded.type == "speaking_status" then
        visualEffects.UpdateTransmission(
            decoded.pilot_id,
            decoded.pilot_name,
            decoded.frequency,
            decoded.is_speaking
        )
    end
end
```

---

## ?? API Reference

### Lua Functions

```lua
-- Initialize (called once)
VisualEffects.Initialize()

-- Update every frame
VisualEffects.Update(dt, absoluteTime)

-- Update transmission
VisualEffects.UpdateTransmission(pilotId, pilotName, frequency, isSpeaking)

-- Clear all
VisualEffects.Clear()

-- Configuration
VisualEffects.SetTextLabelsEnabled(true)
VisualEffects.SetRadioWavesEnabled(true)
VisualEffects.SetTextLabelHeight(50)
VisualEffects.SetRadioWaveRadius(100)

-- Get/Set config
local config = VisualEffects.GetConfig()
VisualEffects.SetConfig(config)
```

### C# Integration

```csharp
// In audio playback loop
public async Task PlayAudioPacket(AudioPacketMetadata packet)
{
    // Start transmission
    await SendSpeakingStatus(packet, true);
    
    // Play audio
    await audioEngine.WriteAudioAsync(packet.AudioPayload);
    
    // End transmission
    await SendSpeakingStatus(packet, false);
}

private async Task SendSpeakingStatus(AudioPacketMetadata packet, bool isSpeaking)
{
    if (_tacviewClient?.IsConnected != true)
        return;
    
    var message = new SpeakingStatusMessage
    {
        PilotId = packet.TransmitterGuid,
        PilotName = GetPilotName(packet.TransmitterGuid),
        Frequency = packet.Frequency,
        IsSpeaking = isSpeaking,
        TimestampUtc = DateTime.UtcNow.ToString("o")
    };
    
    await _tacviewClient.SendMessageAsync(message);
}
```

---

## ?? Troubleshooting

### Labels Not Appearing

**Check**:
1. ? Feature enabled: `Visual Effects ? Show Frequency Labels`
2. ? Pilot found in Tacview objects
3. ? Camera close enough to aircraft
4. ? Speaking status messages being sent

**Solution**: Verify pilot GUID matches Tacview object GUID

### Radio Waves Not Visible

**Check**:
1. ? Feature enabled: `Visual Effects ? Show Radio Waves`
2. ? Wave radius large enough
3. ? Camera angle suitable (top-down best)

**Solution**: Increase wave radius or adjust camera

### Performance Issues

**Symptoms**: Low FPS with multiple transmissions

**Solutions**:
1. Disable radio waves (keep labels only)
2. Reduce wave radius
3. Disable visual effects entirely

**Expected Performance**:
- <1% CPU overhead
- ~200 bytes per transmission
- No FPS impact with <10 simultaneous transmissions

---

## ? Testing Checklist

**Visual**:
- [ ] Single transmission shows label
- [ ] Multiple transmissions show distinct colors
- [ ] Labels fade out correctly
- [ ] Radio waves expand smoothly
- [ ] Colors match frequency mapping

**Functional**:
- [ ] Menu toggles work
- [ ] Settings dialog updates parameters
- [ ] Configuration persists
- [ ] No crashes with rapid transmissions

**Performance**:
- [ ] No FPS drop with 10+ transmissions
- [ ] No memory leaks
- [ ] CPU usage <1% overhead

---

## ?? Related Documentation

- **Full Feature Guide**: `VISUAL-EFFECTS-FEATURE.md`
- **Implementation Guide**: `IMPLEMENTATION-GUIDE.md` ? Visual Transmission Indicators
- **Protocol Spec**: Message type `speaking_status`
- **Lua Module**: `visual_effects.lua`

---

## ?? Examples

### Example 1: Single Pilot

**Scenario**: Viper 1-1 transmits on 251.0 MHz

**Visual**:
```
  TX: 251.000 MHz
  Viper 1-1
      ?
   ?? [F-16] ??
```

### Example 2: Multiple Pilots

**Scenario**: Two pilots transmitting

**Visual**:
```
TX: 251.000 MHz       TX: 305.000 MHz
Viper 1-1             Eagle 2-1
    ?                     ?
 ?? [F-16] ??          ?? [F-15] ??
```

### Example 3: Fade-Out

**Timeline**:
```
t=0s:  ????????  Transmission starts (100% opacity)
t=2s:  ????????  Still transmitting
t=2s:  ????????  Transmission ends
t=2.5s: ????????  Fading (50% opacity)
t=3s:  ????????  Disappeared
```

---

**Last Updated**: 2024-01-17  
**Version**: 1.0
