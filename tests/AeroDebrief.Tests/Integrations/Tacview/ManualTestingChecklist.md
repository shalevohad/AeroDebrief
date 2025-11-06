# Phase 6: Manual Testing Checklist

**Version**: 1.0  
**Date**: 2024  
**Tester**: _________________  
**Test Environment**: _________________

---

## Test Environment Setup

### Prerequisites
- [ ] Tacview 1.9.0 or higher installed
- [ ] AeroDebrief built in Debug mode
- [ ] AeroDebriefSync addon installed in `%APPDATA%\Tacview\AddOns\`
- [ ] Test recording file available (.srs format)
- [ ] Corresponding Tacview ACMI file available

### Initial Setup
- [ ] Start Tacview and load ACMI file
- [ ] Verify addon loaded (check `Help > Show Log`)
- [ ] Start AeroDebrief and load recording
- [ ] Verify Tacview panel visible in UI

---

## Connection Tests

### TC-001: Initial Connection
**Priority**: Critical

- [ ] Start Tacview (addon should start TCP server)
- [ ] Start AeroDebrief
- [ ] Expected: Connection status shows "Connected" (green)
- [ ] Expected: Port displayed (default: 52001)
- [ ] Expected: No error messages in log

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-002: Auto-Connect on Startup
**Priority**: High

- [ ] Enable "Auto-connect on startup" in settings
- [ ] Restart AeroDebrief (Tacview already running)
- [ ] Expected: Automatically connects within 2 seconds
- [ ] Expected: Status shows "Connected" without manual action

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-003: Manual Reconnect
**Priority**: High

- [ ] Establish connection
- [ ] Click "Disconnect" button
- [ ] Expected: Status shows "Disconnected" (red)
- [ ] Click "Reconnect" button
- [ ] Expected: Status shows "Connecting..." then "Connected"

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-004: Connection Lost - Auto Reconnect
**Priority**: High

- [ ] Enable "Auto-reconnect on disconnect"
- [ ] Establish connection
- [ ] Close Tacview (simulating disconnect)
- [ ] Expected: Status shows "Disconnected" with reconnection attempts
- [ ] Restart Tacview
- [ ] Expected: Automatically reconnects within configured interval

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-005: Connection to Non-Running Server
**Priority**: Medium

- [ ] Stop Tacview
- [ ] Attempt to connect from AeroDebrief
- [ ] Expected: Connection fails with appropriate error message
- [ ] Expected: UI remains stable

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Time Synchronization Tests

### TC-101: Play/Pause Synchronization
**Priority**: Critical

- [ ] Establish connection
- [ ] Load matching files in both applications
- [ ] Press Play in Tacview
- [ ] Expected: AeroDebrief starts playing within 200ms
- [ ] Press Pause in Tacview
- [ ] Expected: AeroDebrief pauses within 200ms

**Result**: ? PASS ? FAIL  
**Measured Latency**: _____ ms  
**Notes**: _________________

### TC-102: Seek Synchronization - Large Jump
**Priority**: Critical

- [ ] Establish connection and start playback
- [ ] Current position: 00:05:00
- [ ] Seek to 00:10:00 in Tacview
- [ ] Expected: AeroDebrief seeks to ~00:10:00 within 500ms
- [ ] Measured drift: _____ ms
- [ ] Expected: Drift < 1 second

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-103: Seek Synchronization - Small Jump
**Priority**: High

- [ ] Establish connection and start playback
- [ ] Current position: 00:05:00
- [ ] Seek to 00:05:05 in Tacview (5 second jump)
- [ ] Expected: AeroDebrief adjusts position smoothly
- [ ] Expected: No audio glitches

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-104: Timeline Scrubbing - Slow
**Priority**: High

- [ ] Establish connection
- [ ] Drag Tacview timeline slider slowly (1-2 seconds)
- [ ] Expected: Audio mutes during scrubbing
- [ ] Release slider
- [ ] Expected: Audio fades back in smoothly (200ms)
- [ ] Expected: Position synchronized correctly

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-105: Timeline Scrubbing - Rapid
**Priority**: High

- [ ] Establish connection
- [ ] Drag Tacview timeline slider rapidly back and forth
- [ ] Expected: Audio stays muted throughout
- [ ] Expected: No clicks or pops
- [ ] Release slider
- [ ] Expected: Audio fades in smoothly
- [ ] Expected: Position stabilizes correctly

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-106: Sync Quality - Short Duration (5 minutes)
**Priority**: High

- [ ] Establish connection and start playback
- [ ] Monitor for 5 minutes
- [ ] Record sync drift every 30 seconds:
  - 0:30 - _____ ms
  - 1:00 - _____ ms
  - 1:30 - _____ ms
  - 2:00 - _____ ms
  - 2:30 - _____ ms
  - 3:00 - _____ ms
  - 3:30 - _____ ms
  - 4:00 - _____ ms
  - 4:30 - _____ ms
  - 5:00 - _____ ms
- [ ] Expected: All measurements < 1000ms
- [ ] Expected: Sync quality > 95%

**Result**: ? PASS ? FAIL  
**Average Drift**: _____ ms  
**Max Drift**: _____ ms  
**Notes**: _________________

### TC-107: Sync Quality - Long Duration (30 minutes)
**Priority**: Medium

- [ ] Establish connection and start playback
- [ ] Monitor for 30 minutes
- [ ] Sample drift every 5 minutes
- [ ] Expected: Drift remains < 1 second
- [ ] Expected: No gradual drift accumulation

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Variable Speed Playback Tests

### TC-201: Speed 0.25x (Quarter Speed)
**Priority**: High

- [ ] Set Tacview playback speed to 0.25x
- [ ] Expected: AeroDebrief adjusts to 0.25x
- [ ] Expected: Audio plays slowly but understandable
- [ ] Expected: Sync maintained

**Result**: ? PASS ? FAIL  
**Audio Quality**: ? Excellent ? Good ? Acceptable ? Poor  
**Notes**: _________________

### TC-202: Speed 0.5x (Half Speed)
**Priority**: High

- [ ] Set Tacview playback speed to 0.5x
- [ ] Expected: Audio clear at half speed
- [ ] Expected: Minimal pitch shift
- [ ] Expected: Sync maintained

**Result**: ? PASS ? FAIL  
**Audio Quality**: ? Excellent ? Good ? Acceptable ? Poor  
**Notes**: _________________

### TC-203: Speed 1.0x (Normal)
**Priority**: Critical

- [ ] Set Tacview playback speed to 1.0x
- [ ] Expected: Normal playback
- [ ] Expected: Perfect audio quality
- [ ] Expected: Perfect sync

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-204: Speed 1.5x (1.5x Speed)
**Priority**: High

- [ ] Set Tacview playback speed to 1.5x
- [ ] Expected: Audio slightly faster but clear
- [ ] Expected: Speech understandable
- [ ] Expected: Sync maintained

**Result**: ? PASS ? FAIL  
**Audio Quality**: ? Excellent ? Good ? Acceptable ? Poor  
**Notes**: _________________

### TC-205: Speed 2.0x (Double Speed)
**Priority**: High

- [ ] Set Tacview playback speed to 2.0x
- [ ] Expected: Audio double-speed, compressed but understandable
- [ ] Expected: Sync maintained

**Result**: ? PASS ? FAIL  
**Audio Quality**: ? Excellent ? Good ? Acceptable ? Poor  
**Notes**: _________________

### TC-206: Speed 4.0x (Maximum)
**Priority**: Medium

- [ ] Set Tacview playback speed to 4.0x
- [ ] Expected: Very fast audio
- [ ] Expected: May be choppy (acceptable)
- [ ] Expected: Sync maintained

**Result**: ? PASS ? FAIL  
**Audio Quality**: ? Excellent ? Good ? Acceptable ? Poor  
**Notes**: _________________

### TC-207: Speed Change During Playback
**Priority**: High

- [ ] Start playback at 1.0x
- [ ] Change to 2.0x while playing
- [ ] Expected: Smooth transition
- [ ] Expected: No audio glitches
- [ ] Change to 0.5x
- [ ] Expected: Smooth transition again

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-208: Speed Change While Paused
**Priority**: Medium

- [ ] Pause playback
- [ ] Change speed to 2.0x
- [ ] Resume playback
- [ ] Expected: Resumes at new speed
- [ ] Expected: No issues

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Pilot Selection & Filtering Tests

### TC-301: Single Pilot Selection
**Priority**: Critical

- [ ] Establish connection
- [ ] Select one aircraft in Tacview (e.g., Viper 1-1)
- [ ] Expected: Pilot appears in AeroDebrief UI
- [ ] Expected: Only selected pilot's audio plays
- [ ] Expected: Other pilots filtered out

**Result**: ? PASS ? FAIL  
**Selected Pilot**: _________________  
**Notes**: _________________

### TC-302: Multiple Pilot Selection
**Priority**: Critical

- [ ] Select 3 aircraft in Tacview
- [ ] Expected: All 3 pilots appear in AeroDebrief UI
- [ ] Expected: Audio from all 3 pilots plays
- [ ] Expected: Non-selected pilots filtered

**Result**: ? PASS ? FAIL  
**Selected Pilots**: _________________  
**Notes**: _________________

### TC-303: Pilot Deselection
**Priority**: High

- [ ] Select 2 pilots
- [ ] Verify their audio plays
- [ ] Deselect 1 pilot in Tacview
- [ ] Expected: Deselected pilot removed from UI
- [ ] Expected: Deselected pilot's audio stops playing

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-304: Per-Pilot Frequency Filtering
**Priority**: High

- [ ] Select pilot with multiple frequencies (e.g., 251 MHz, 305 MHz)
- [ ] In AeroDebrief, disable 305 MHz for that pilot
- [ ] Expected: Only transmissions on 251 MHz play
- [ ] Expected: 305 MHz transmissions filtered
- [ ] Expected: Change syncs to Tacview

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-305: General Frequency Filtering (Default Disabled)
**Priority**: High

- [ ] Select one pilot
- [ ] Verify general frequencies list empty (default)
- [ ] Verify non-selected pilot audio does NOT play
- [ ] Enable 251 MHz in general frequencies
- [ ] Expected: Non-selected pilots on 251 MHz now audible

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-306: Enable All Frequencies for Pilot
**Priority**: Medium

- [ ] Select pilot
- [ ] Disable some frequencies
- [ ] Click "Enable All" button
- [ ] Expected: All frequencies re-enabled
- [ ] Expected: Change syncs to Tacview

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-307: Disable All Frequencies for Pilot
**Priority**: Medium

- [ ] Select pilot with multiple frequencies enabled
- [ ] Click "Disable All" button
- [ ] Expected: All frequencies disabled
- [ ] Expected: Pilot's audio completely filtered

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Spatial Audio (Pan) Tests

### TC-401: Auto Pan Mode - 2 Pilots
**Priority**: High

- [ ] Select 2 pilots in Tacview
- [ ] Verify "Auto Pan Mode" selected
- [ ] Expected: Pilot 1 panned left (-0.5 to -1.0)
- [ ] Expected: Pilot 2 panned right (0.5 to 1.0)
- [ ] Play audio and verify stereo separation

**Result**: ? PASS ? FAIL  
**Pilot 1 Pan**: _____  
**Pilot 2 Pan**: _____  
**Audible Separation**: ? Yes ? No  
**Notes**: _________________

### TC-402: Auto Pan Mode - 4 Pilots
**Priority**: High

- [ ] Select 4 pilots
- [ ] Expected: Pilots distributed across stereo field
- [ ] Expected: Even spacing (approximately -0.75, -0.25, 0.25, 0.75)
- [ ] Verify each pilot audibly separated

**Result**: ? PASS ? FAIL  
**Pan Values**: _____, _____, _____, _____  
**Notes**: _________________

### TC-403: Manual Pan Mode
**Priority**: High

- [ ] Select "Manual Pan Mode"
- [ ] Adjust pilot pan slider to -1.0 (full left)
- [ ] Expected: Audio in left channel only
- [ ] Adjust to +1.0 (full right)
- [ ] Expected: Audio in right channel only
- [ ] Adjust to 0.0 (center)
- [ ] Expected: Audio centered

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-404: Pan Configuration Sync to Tacview
**Priority**: Medium

- [ ] Change pan settings in AeroDebrief
- [ ] Expected: Changes sync to Tacview
- [ ] Verify in Tacview menu: `AeroDebrief Sync > Configure Audio Pan`
- [ ] Expected: Settings match

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-405: Pan Mode Switch
**Priority**: Medium

- [ ] Start in Auto Pan mode
- [ ] Switch to Manual Pan mode
- [ ] Expected: Smooth transition
- [ ] Expected: Current pan values preserved
- [ ] Switch back to Auto
- [ ] Expected: Recalculates auto pan

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## UI Tests

### TC-501: Connection Status Indicator
**Priority**: High

- [ ] Start with Tacview not running
- [ ] Expected: Red indicator, "Disconnected"
- [ ] Start Tacview
- [ ] Connect from AeroDebrief
- [ ] Expected: Green indicator, "Connected"
- [ ] Stop Tacview
- [ ] Expected: Red indicator, "Disconnected"

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-502: Sync Quality Display
**Priority**: Medium

- [ ] Establish connection and start playback
- [ ] Verify sync quality bar visible
- [ ] Expected: Shows percentage (90-100%)
- [ ] Expected: Shows drift in ms
- [ ] Manually cause drift (seek in one app only)
- [ ] Expected: Quality drops, drift increases

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-503: Selected Pilots List
**Priority**: High

- [ ] Select pilots in Tacview
- [ ] Expected: Pilots appear in AeroDebrief list immediately
- [ ] Expected: Pilot names correct
- [ ] Expected: Pan values displayed
- [ ] Expected: Frequency info displayed

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-504: Settings Panel
**Priority**: Medium

- [ ] Open settings panel
- [ ] Change TCP port
- [ ] Save settings
- [ ] Restart AeroDebrief
- [ ] Expected: Settings persisted
- [ ] Test connection with new port

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-505: Test Connection Button
**Priority**: Low

- [ ] Ensure Tacview running
- [ ] Click "Test Connection" button
- [ ] Expected: Success message
- [ ] Stop Tacview
- [ ] Click "Test Connection"
- [ ] Expected: Failure message with details

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-506: Scrubbing Indicator
**Priority**: Medium

- [ ] Start playback
- [ ] Begin scrubbing in Tacview
- [ ] Expected: "SCRUBBING" indicator appears in UI
- [ ] Release scrubber
- [ ] Expected: Indicator disappears after ~500ms

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-507: Speed Indicator
**Priority**: Medium

- [ ] Change playback speed in Tacview
- [ ] Expected: Speed indicator updates in AeroDebrief
- [ ] Expected: Shows format "2.00x", "0.50x", etc.

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Edge Cases & Error Handling

### TC-601: Recording Shorter Than Tacview File
**Priority**: Medium

- [ ] Load recording ending at 00:10:00
- [ ] Load Tacview file ending at 00:15:00
- [ ] Seek to 00:12:00 in Tacview
- [ ] Expected: AeroDebrief handles gracefully
- [ ] Expected: No crash, appropriate behavior

**Result**: ? PASS ? FAIL  
**Behavior**: _________________  
**Notes**: _________________

### TC-602: Recording Longer Than Tacview File
**Priority**: Medium

- [ ] Load recording ending at 00:15:00
- [ ] Load Tacview file ending at 00:10:00
- [ ] Play to end of Tacview file
- [ ] Expected: AeroDebrief continues playing
- [ ] Expected: No errors

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-603: Time Zone Differences
**Priority**: Low

- [ ] Load files recorded in different time zones
- [ ] Expected: UTC time handling prevents issues
- [ ] Expected: Sync remains accurate

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-604: Network Interruption During Playback
**Priority**: Medium

- [ ] Start playback with connection
- [ ] Disconnect network (disable adapter)
- [ ] Expected: Reconnection attempts logged
- [ ] Re-enable network
- [ ] Expected: Auto-reconnects
- [ ] Expected: Playback resumes

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-605: Rapid Pilot Selection Changes
**Priority**: Medium

- [ ] Rapidly select/deselect multiple pilots (5-10 times in 10 seconds)
- [ ] Expected: UI updates smoothly
- [ ] Expected: No crashes
- [ ] Expected: Audio filter updates correctly

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-606: Maximum Pilots Selected (10+)
**Priority**: Low

- [ ] Select 10+ aircraft in Tacview
- [ ] Expected: All appear in UI
- [ ] Expected: Performance remains acceptable
- [ ] Expected: Auto pan distributes evenly

**Result**: ? PASS ? FAIL  
**Performance Impact**: ? None ? Minor ? Moderate ? Severe  
**Notes**: _________________

---

## Performance Tests

### TC-701: CPU Usage - Idle Connection
**Priority**: Medium

**Baseline** (no Tacview connection):
- CPU Usage: _____% (average over 1 minute)

**With Tacview** (connected, not playing):
- CPU Usage: _____% (average over 1 minute)
- Overhead: _____% 

**Expected**: Overhead < 1%

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-702: CPU Usage - Active Playback
**Priority**: Medium

**Baseline** (playback without Tacview):
- CPU Usage: _____% (average over 1 minute)

**With Tacview Sync** (playing, syncing):
- CPU Usage: _____% (average over 1 minute)
- Overhead: _____%

**Expected**: Overhead < 5%

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-703: Memory Usage
**Priority**: Low

**Baseline** (no connection):
- Memory: _____ MB

**After 30 minutes connected**:
- Memory: _____ MB
- Growth: _____ MB

**Expected**: < 10 MB growth

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-704: Network Bandwidth
**Priority**: Low

- [ ] Monitor network usage during active sync
- [ ] Expected: Minimal bandwidth (< 1 KB/s)
- [ ] Measured: _____ KB/s

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Compatibility Tests

### TC-801: Tacview Version Compatibility
**Priority**: High

Test with:
- [ ] Tacview 1.9.0 (minimum required)
- [ ] Tacview 1.9.2 (latest stable)
- [ ] Tacview 2.0+ (if available)

**Results**:
- 1.9.0: ? PASS ? FAIL
- 1.9.2: ? PASS ? FAIL  
- 2.0+: ? PASS ? FAIL

**Notes**: _________________

### TC-802: Recording Format Compatibility
**Priority**: Medium

Test with recordings from:
- [ ] DCS World
- [ ] BMS (Falcon)
- [ ] IL-2 Sturmovik
- [ ] Other: _________________

**Results**: _________________

---

## Regression Tests

### TC-901: Playback Without Tacview
**Priority**: Critical

- [ ] Load recording without Tacview running
- [ ] Expected: Playback works normally
- [ ] Expected: No errors about missing Tacview
- [ ] Expected: Tacview panel shows "Not Connected"

**Result**: ? PASS ? FAIL  
**Notes**: _________________

### TC-902: Existing Features Unchanged
**Priority**: Critical

Verify these still work:
- [ ] File loading
- [ ] Basic playback (play/pause/stop)
- [ ] Seek bar
- [ ] Volume controls
- [ ] Frequency mixer
- [ ] Export functionality

**Result**: ? PASS ? FAIL  
**Notes**: _________________

---

## Sign-Off

**Total Tests**: _____  
**Passed**: _____  
**Failed**: _____  
**Pass Rate**: _____%

**Critical Issues Found**: _____  
**High Priority Issues**: _____  
**Medium Priority Issues**: _____  
**Low Priority Issues**: _____

**Recommendation**:
? Ready for release  
? Ready with minor fixes  
? Requires rework  
? Not ready for release

**Tester Signature**: _________________  
**Date**: _________________

**Additional Comments**:
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
