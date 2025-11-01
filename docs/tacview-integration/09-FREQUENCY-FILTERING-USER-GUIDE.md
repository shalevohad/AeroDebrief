# Frequency Filtering - User Guide

## Overview

The frequency filtering feature allows you to selectively monitor specific radio frequencies in your AeroDebrief recordings, giving you precise control over which communications you hear during replay with Tacview integration.

## Why Use Frequency Filtering?

### Problem: Information Overload
In large-scale missions with many aircraft, multiple frequencies create audio chaos:
- Combat frequencies (251.0 MHz)
- Datalink frequencies (305.0 MHz)
- Tower frequencies (124.0 MHz)
- GCI frequencies (243.0 MHz)
- Auxiliary frequencies (133.0 MHz, 225.0 MHz)

**Result**: Overwhelming audio mix with too many simultaneous transmissions

### Solution: Selective Frequency Monitoring
Filter frequencies to hear only what matters:
- **Per-Pilot**: Choose frequencies for each selected pilot
- **General**: Choose frequencies for all non-selected pilots

---

## Access Frequency Filtering

### In Tacview Menu

```
Tacview Menu Bar
?? AeroDebrief Sync
   ?? Configure Audio Pan...
   ?? Auto Pan Mode
   ?? Manual Pan Mode
   ?? ?????????????????????????????????
   ?? Configure Pilot Frequencies...    ??? Per-Pilot Filtering
   ?? Configure General Frequencies...  ??? General Filtering
   ?? ?????????????????????????????????
   ?? About
```

---

## Per-Pilot Frequency Filtering

### Step 1: Select Aircraft in Tacview

Select the aircraft you want to monitor:
- Click aircraft in 3D view
- Or use object tree panel
- Multiple selections supported

### Step 2: Open Frequency Configuration

```
Tacview ? AeroDebrief Sync ? Configure Pilot Frequencies...
```

### Step 3: Configure Each Pilot

Dialog appears for each selected pilot:

```
????????????????????????????????????????????????????????
? Configure Frequencies: Viper 1-1                     ?
? ???????????????????????????????????????????????? ?
? Select which frequencies to monitor for this pilot:  ?
?                                                       ?
? ? Select All Frequencies                            ?
? ? Deselect All Frequencies                          ?
? ???????????????????????????????????????????????? ?
? ? 251.0 MHz    (Combat)                             ?
? ? 305.0 MHz    (Datalink)                           ?
? ? 133.0 MHz    (Auxiliary)                          ?
? ? 225.0 MHz    (Guard)                              ?
? ???????????????????????????????????????????????? ?
? Done                                                 ?
????????????????????????????????????????????????????????
```

**How to Use**:
- Click checkbox to toggle: ? (enabled) ? ? (disabled)
- "? Select All" - Enable all frequencies instantly
- "? Deselect All" - Disable all frequencies instantly
- Changes apply immediately to playback
- Click "Done" when finished

### Step 4: Verify in AeroDebrief

AeroDebrief displays enabled frequencies:

```
?????????????????????????????????????????????????????????
? Selected Pilots (1) | Pan Mode: Auto                 ?
?????????????????????????????????????????????????????????
? ?? Viper 1-1 (F-16C)                            ??   ?
?    ? 251.0 MHz  ? 305.0 MHz                          ?
?    ? 133.0 MHz  ? 225.0 MHz                          ?
?    ?? Blue Coalition                                   ?
?????????????????????????????????????????????????????????
```

**Legend**:
- ? = Frequency enabled (you will hear this)
- ? = Frequency disabled (you won't hear this)

---

## General Frequency Filtering

### What is General Filtering?

Control which frequencies you hear from **all non-selected pilots** - everyone else in the mission.

**Default Behavior**: All general frequencies are **disabled by default**. This means:
- When you first select pilots in Tacview, you will ONLY hear those selected pilots
- You will NOT hear any non-selected pilots until you explicitly enable frequencies in "Configure General Frequencies..."
- This prevents audio clutter and allows you to focus on selected pilots first

### Use Cases

- **Monitor Tower Only**: Enable only ATC communications from non-selected pilots
- **Add Combat Chatter**: Enable combat frequencies to hear context from others
- **Complete Focus**: Keep all disabled for exclusive focus on selected pilots (default)
?
### Step 1: Open General Configuration

```
Tacview ? AeroDebrief Sync ? Configure General Frequencies...
```

### Step 2: Select Frequencies

**Note**: By default, all general frequencies are **disabled**. You must explicitly enable frequencies to hear non-selected pilots.

```
????????????????????????????????????????????????????????
? Configure Frequencies: All Non-Selected Pilots       ?
? ???????????????????????????????????????????????? ?
? Select which frequencies to monitor for all          ?
? non-selected pilots:                                 ?
?                                                       ?
? ? Select All Frequencies                            ?
? ? Deselect All Frequencies                          ?
? ???????????????????????????????????????????????? ?
? ? 124.0 MHz    (Tower)                              ?
? ? 243.0 MHz    (GCI)                                ?
? ? 251.0 MHz    (Combat)                             ?
? ? 305.0 MHz    (Datalink)                           ?
? ? 133.0 MHz    (Auxiliary)                          ?
? ? 225.0 MHz    (Guard)                              ?
? ???????????????????????????????????????????????? ?
? Done                                                 ?
????????????????????????????????????????????????????????
```

**Default State**: All checkboxes are **unchecked (?)** - no general frequencies enabled.

**To enable frequencies**:
1. Click individual frequency checkboxes to enable them: ? ? ?
2. Or use "? Select All Frequencies" to enable all at once

**In this example (after enabling tower and GCI)**:
- ? Tower (124.0) and GCI (243.0) enabled
- ? All other frequencies disabled
- **Result**: You only hear tower and GCI from non-selected pilots

---

## Practical Examples

### Example 1: Focus on Flight Lead Only

**Scenario**: You're flying as wingman and want to hear ONLY your flight lead

**Steps**:
1. Select flight lead aircraft in Tacview
2. Per-Pilot: Enable only combat frequency (251.0 MHz)
3. General: *(Leave all disabled - this is the default)*

**Result**:
- ? Hear flight lead on 251.0 MHz
- ? Don't hear flight lead on other frequencies
- ? Complete silence from everyone else (general frequencies disabled by default)

**Note**: No need to explicitly disable general frequencies - they're already off!

---

### Example 2: Monitor Combat + Tower

**Scenario**: You want combat comms from your element and tower from everyone

**Steps**:
1. Select your element (2 aircraft)
2. Per-Pilot: Enable 251.0 MHz (combat) for both
3. General: Enable only 124.0 MHz (tower)

**Result**:
- ? Hear your element on 251.0 MHz
- ? Hear tower communications from all pilots
- ? Don't hear other frequencies

---

### Example 3: Complete Mission Audio

**Scenario**: Hear everything (default behavior)

**Steps**:
1. Select all aircraft you want spatial audio for
2. Per-Pilot: "? Select All Frequencies" for each
3. General: "? Select All Frequencies"

**Result**:
- ? Hear all selected pilots on all frequencies
- ? Hear all non-selected pilots on all frequencies
- Full mission audio with spatial positioning

---

### Example 4: Datalink Isolation

**Scenario**: You're analyzing datalink communications only

**Steps**:
1. Select relevant aircraft
2. Per-Pilot: Enable only 305.0 MHz (datalink)
3. General: *(Leave all disabled - default behavior)*

**Result**:
- ? Hear only datalink from selected pilots
- ? Everything else silent (general frequencies disabled by default)
- Perfect for datalink analysis

**Note**: The default disabled state for general frequencies makes this scenario effortless!

---

## Visual Indicators in AeroDebrief

### Selected Pilot Display

```
?????????????????????????????????????????????????????????
? ?? Viper 1-1 (F-16C)                            ??   ?
?    Frequencies: ? 251.0  ? 305.0  ? 133.0           ?
?    Pan: Mostly Left (-0.7)                           ?
?    ?? Blue Coalition                                  ?
?????????????????????????????????????????????????????????
```

**Quick Visual Scan**:
- Count ? symbols = number of enabled frequencies
- Count ? symbols = number of disabled frequencies

### Speaking Indicator with Frequency

When pilot speaks:

```
?????????????????????????????????????????????????????????
? ?? Viper 1-1 (F-16C) [SPEAKING on 251.0 MHz]   ??   ?
?    ??????????????????????????????????????????? ?
?    Frequencies: ? 251.0  ? 305.0  ? 133.0           ?
?????????????????????????????????????????????????????????
```

Shows which frequency the transmission is on.

---

## Performance & Efficiency

### CPU Impact
- Frequency check: **<1 microsecond per packet**
- O(1) hash table lookup
- No noticeable performance impact

### Memory Impact
- 10 pilots × 5 frequencies = **~50 bytes**
- Negligible memory footprint

### Network Impact
- Frequency data adds **~100 bytes per message**
- Sent only on configuration change
- No continuous overhead

---

## Tips & Best Practices

### 1. Start Broad, Then Narrow

**Initial Setup**:
- Enable all frequencies
- Listen to full mission

**Then Refine**:
- Identify noisy frequencies
- Disable non-essential frequencies
- Focus on key communications

### 2. Use Quick Actions

**Speed Up Configuration**:
- "? Deselect All" ? then enable only what you need
- Much faster than unchecking many boxes

### 3. Frequency Presets (Future)

**Coming Soon**:
- Save frequency configurations
- Quick profiles: "Combat Only", "Tower Only", "All"

### 4. Combine with Pan

**Best Experience**:
- Use Auto Pan for spatial positioning
- Use frequency filters for audio clarity
- **Result**: Clear, spatially-positioned audio

### 5. Trial and Error

**Don't Worry**:
- Changes apply instantly
- Easy to toggle frequencies on/off
- Experiment to find best configuration

---

## Troubleshooting

### Problem: Can't Hear Specific Pilot

**Check**:
1. Is pilot selected in Tacview?
2. Are their frequencies enabled in "Configure Pilot Frequencies..."?
3. Is the packet on an enabled frequency?

**Solution**: Enable relevant frequencies for that pilot

---

### Problem: Hearing Too Many People

**Check**:
1. General frequencies enabled?
2. Too many frequencies enabled for selected pilots?

**Solution**: 
- Disable non-essential general frequencies
- Narrow per-pilot frequency selection

---

### Problem: Hearing No One

**Check**:
1. Are any frequencies enabled?
2. Tacview connection active?

**Solution**:
- Click "? Select All Frequencies" in both dialogs
- Verify Tacview connection in AeroDebrief status

---

### Problem: Changes Not Taking Effect

**Check**:
1. Tacview still connected?
2. Clicked "Done" in dialog?

**Solution**:
- Verify connection indicator is green
- Close and reopen frequency dialog

---

## Keyboard Shortcuts (Future)

**Planned**:
- `Ctrl+F` - Open frequency configuration
- `Ctrl+Shift+F` - Toggle all frequencies
- `Alt+1-9` - Quick frequency presets

---

## Frequently Asked Questions

**Q: Why can't I hear non-selected pilots by default?**  
A: General frequencies are **disabled by default** to prevent audio clutter. This allows you to focus on selected pilots first. Enable specific frequencies in "Configure General Frequencies..." to hear others.

**Q: Do frequency filters persist across missions?**  
A: Yes, per-pilot settings are remembered based on pilot ID. General frequency settings also persist.

**Q: Can I save frequency configurations?**  
A: Not yet, but this is planned for future versions.

**Q: What happens if I disable all frequencies?**  
A: Complete silence - useful for focused analysis.

**Q: Can I filter by time periods?**  
A: Not yet, but time-based filtering is planned.

**Q: Does this work with existing recordings?**  
A: Yes! Filter any recording during Tacview replay.

**Q: Can I export frequency filter settings?**  
A: Not yet, but this is on the roadmap.

**Q: How do I quickly hear everyone like before?**  
A: Go to "Configure General Frequencies..." and click "? Select All Frequencies" to enable all general frequencies at once.
