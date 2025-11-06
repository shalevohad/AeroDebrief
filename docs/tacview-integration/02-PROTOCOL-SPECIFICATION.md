# Protocol Specification - AeroDebrief ? Tacview

## Overview

This document defines the bidirectional JSON-based communication protocol between the Tacview Lua addon (server) and AeroDebrief (client) over TCP.

## Transport Layer

### Connection Details
- **Protocol**: TCP/IP
- **Host**: `127.0.0.1` (localhost only for security)
- **Port**: `52001` (configurable)
- **Encoding**: UTF-8
- **Message Format**: Newline-delimited JSON (`\n` separator)

### Message Framing
Each message is a single-line JSON object terminated by a newline character:
```
{"type":"time_update","mission_time_utc":"2024-01-15T14:30:45Z"}\n
{"type":"pilot_selection","selected_pilots":[]}\n
```

### Connection Lifecycle

```
1. Tacview Addon starts ? TCP server listening on 52001
2. AeroDebrief connects ? TCP client establishes connection
3. AeroDebrief sends "ready" ? Handshake
4. Tacview responds with current state ? Initial sync
5. Bidirectional message exchange begins
6. Either side can close connection gracefully
```

### Heartbeat (Optional)
- AeroDebrief sends `sync_status` every 5 seconds
- Tacview considers client alive if received within last 10 seconds
- Automatic reconnection on timeout

## Message Types

### Direction Key
- ?? **Tacview ? AeroDebrief** (Server to Client)
- ?? **AeroDebrief ? Tacview** (Client to Server)

---

## ?? Tacview ? AeroDebrief Messages

### 1. Time Update (High Frequency - 10 Hz)

**Purpose**: Continuous time synchronization during playback

**Message**:
```json
{
  "type": "time_update",
  "mission_time_utc": "2024-01-15T14:30:45.123Z",
  "playback_state": "playing",
  "playback_speed": 1.0
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"time_update"` |
| `mission_time_utc` | string | ISO 8601 UTC timestamp with milliseconds |
| `playback_state` | string | `"playing"`, `"paused"`, or `"stopped"` |
| `playback_speed` | number | Playback speed multiplier (1.0 = normal, 2.0 = 2x, etc.) |

**Frequency**: 10 times per second (100ms interval) when playing

**Example**:
```json
{
  "type": "time_update",
  "mission_time_utc": "2024-01-15T14:30:45.123Z",
  "playback_state": "playing",
  "playback_speed": 1.0
}
```

---

### 2. Pilot Selection

**Purpose**: Notify AeroDebrief which pilots are selected in Tacview and which frequencies to monitor

**Message**:
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
      "enabled_frequencies": [251.0, 305.0]
    },
    {
      "pilot_id": "F-16C-002-GUID",
      "pilot_name": "Viper 1-2",
      "coalition": "blue",
      "unit_type": "F-16C_50",
      "unit_id": 12346,
      "frequencies": [251.0, 305.0],
      "pan": 0.8,
      "enabled_frequencies": [251.0]
    }
  ],
  "general_enabled_frequencies": [124.0, 243.0]
}
```

**Root Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"pilot_selection"` |
| `pan_mode` | string | `"auto"` or `"manual"` - pan configuration mode set in Tacview |
| `selected_pilots` | array[object] | Array of selected pilot objects |
| `general_enabled_frequencies` | array[number] | **NEW**: Frequencies enabled for non-selected pilots (in MHz) |

**Pilot Object Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `pilot_id` | string | Unique identifier (GUID or Tacview object ID) |
| `pilot_name` | string | Human-readable pilot callsign |
| `coalition` | string | `"red"`, `"blue"`, or `"neutral"` |
| `unit_type` | string | Aircraft/unit type (e.g., "F-16C_50") |
| `unit_id` | number | Tacview object ID |
| `frequencies` | array[number] | All radio frequencies for this pilot in MHz |
| `pan` | number | Stereo pan: -1.0 (left) to +1.0 (right) |
| `enabled_frequencies` | array[number] | **NEW**: Subset of `frequencies` that are enabled for listening |

**Frequency**: On selection change (event-driven) or when frequency filters are modified

**Empty Selection**:
```json
{
  "type": "pilot_selection",
  "pan_mode": "auto",
  "selected_pilots": [],
  "general_enabled_frequencies": []
}
```

**Pan Mode Details**:
- **Auto Mode**: Tacview calculates pan automatically, distributing pilots evenly across stereo field
  - 1 pilot: center (0.0)
  - 2 pilots: left (-1.0) and right (+1.0)
  - 3 pilots: left (-1.0), center (0.0), right (+1.0)
  - N pilots: evenly distributed from -1.0 to +1.0

- **Manual Mode**: User configures pan for each pilot via Tacview menu
  - User selects specific pan values per pilot
  - Values persist for that pilot across selections
  - Default to 0.0 (center) if not configured

**Frequency Filtering Details**:
- **Per-Pilot Filtering** (`enabled_frequencies`):
  - User configures which frequencies to hear for each selected pilot
  - Configured via: `Tacview Menu ? AeroDebrief Sync ? Configure Pilot Frequencies...`
  - Default: All pilot frequencies enabled
  - Example: Pilot has [251.0, 305.0, 133.0] but user only wants to hear [251.0, 305.0]

- **General Filtering** (`general_enabled_frequencies`):
  - Frequencies to monitor for ALL non-selected pilots
  - Configured via: `Tacview Menu ? AeroDebrief Sync ? Configure General Frequencies...`
  - **Default: All frequencies disabled** - No audio from non-selected pilots unless explicitly enabled
  - This prevents audio clutter from non-selected pilots by default
  - Example: Mission has many frequencies but user only wants to monitor tower frequencies [124.0, 243.0]

**Use Case**: When user selects/deselects aircraft in Tacview or modifies frequency filters, AeroDebrief:
1. Filters audio to only play selected pilots' transmissions
2. Applies spatial positioning per pilot
3. Only plays frequencies that are enabled (per-pilot or general)
4. Ignores transmissions on disabled frequencies

**Tacview Menu Configuration**:
Users configure pan mode, pan values, and frequency filters in Tacview via:
```
Tacview Menu Bar ? AeroDebrief Sync
  ?? Configure Audio Pan...            (Opens pan configuration dialog)
  ?? Auto Pan Mode                     (Switch to automatic distribution)
  ?? Manual Pan Mode                   (Switch to manual per-pilot control)
  ?? ?????????????????????????????
  ?? Configure Pilot Frequencies...    (Per-pilot frequency checkboxes)
  ?? Configure General Frequencies...  (General frequency checkboxes)
  ?? ?????????????????????????????
  ?? About
```

**Frequency Configuration UI**:

Per-Pilot Frequencies:
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

General Frequencies:
```
????????????????????????????????????????????
? Configure Frequencies: All Non-Selected  ?
? ????????????????????????????????????? ?
? Select which frequencies to monitor for  ?
? all non-selected pilots:                 ?
?                                          ?
? ? Select All Frequencies                ?
? ? Deselect All Frequencies              ?
? ????????????????????????????????????? ?
? ? 124.0 MHz  (Tower)                    ?
? ? 243.0 MHz  (GCI)                      ?
? ? 251.0 MHz  (Combat)                   ?
? ? 305.0 MHz  (Datalink)                 ?
? ????????????????????????????????????? ?
? Done                                     ?
????????????????????????????????????????????
```

**Filtering Logic in AeroDebrief**:
```csharp
bool ShouldPlayAudioPacket(AudioPacketMetadata packet, TacviewPilotSelection selection)
{
    // Check if pilot is selected
    var selectedPilot = selection.SelectedPilots
        .FirstOrDefault(p => p.PilotId == packet.TransmitterGuid);
    
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

**Example Scenarios**:

1. **Focus on specific frequency for selected pilot**:
   - User selects "Viper 1-1" who has frequencies [251.0, 305.0, 133.0]
   - User disables 133.0 MHz via "Configure Pilot Frequencies..."
   - Result: Only hear Viper 1-1 on 251.0 and 305.0, not on 133.0

2. **Monitor only tower communications from non-selected pilots**:
   - User enables only [124.0, 243.0] in "Configure General Frequencies..."
   - Result: Hear all non-selected pilots on tower/GCI frequencies, ignore combat chatter

3. **Complete audio isolation**:
   - User selects 2 pilots, enables only [251.0] for each
   - User disables all general frequencies
   - Result: Only hear those 2 pilots on 251.0 MHz, complete silence from everyone else

---

### 3. Playback Command

**Purpose**: Control AeroDebrief playback state

**Message**:
```json
{
  "type": "playback_command",
  "command": "play"
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"playback_command"` |
| `command` | string | `"play"`, `"pause"`, or `"stop"` |

**Frequency**: On playback state change (event-driven)

**Examples**:
```json
{"type": "playback_command", "command": "play"}
{"type": "playback_command", "command": "pause"}
{"type": "playback_command", "command": "stop"}
```

---

### 4. Seek Command

**Purpose**: Jump to specific time in mission

**Message**:
```json
{
  "type": "seek",
  "target_time_utc": "2024-01-15T14:35:00.000Z"
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"seek"` |
| `target_time_utc` | string | ISO 8601 UTC timestamp to seek to |

**Frequency**: On user seek action (event-driven)

**Example**:
```json
{
  "type": "seek",
  "target_time_utc": "2024-01-15T14:35:00.000Z"
}
```

---

## ?? AeroDebrief ? Tacview Messages

### 1. Ready (Handshake)

**Purpose**: Inform Tacview that AeroDebrief is ready to receive commands

**Message**:
```json
{
  "type": "ready",
  "version": "1.0.0",
  "recording_start_utc": "2024-01-15T14:00:00.000Z",
  "recording_end_utc": "2024-01-15T16:00:00.000Z",
  "recording_duration_seconds": 7200,
  "frequencies_available": [251.0, 305.0, 133.0, 127.5, 124.0]
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"ready"` |
| `version` | string | AeroDebrief protocol version |
| `recording_start_utc` | string | Recording start time (ISO 8601 UTC) |
| `recording_end_utc` | string | Recording end time (ISO 8601 UTC) |
| `recording_duration_seconds` | number | Total duration in seconds |
| `frequencies_available` | array[number] | All frequencies in recording (MHz) |

**Frequency**: Once on connection, once on file load

**Example**:
```json
{
  "type": "ready",
  "version": "1.0.0",
  "recording_start_utc": "2024-01-15T14:00:00.000Z",
  "recording_end_utc": "2024-01-15T16:00:00.000Z",
  "recording_duration_seconds": 7200,
  "frequencies_available": [251.0, 305.0, 133.0, 127.5, 124.0]
}
```

---

### 2. Speaking Status

**Purpose**: Notify Tacview which pilot is currently transmitting

**Message**:
```json
{
  "type": "speaking_status",
  "pilot_id": "F-16C-001-GUID",
  "is_speaking": true,
  "frequency": 251.0,
  "timestamp_utc": "2024-01-15T14:30:45.123Z"
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"speaking_status"` |
| `pilot_id` | string | Unique pilot identifier |
| `is_speaking` | boolean | `true` if started speaking, `false` if stopped |
| `frequency` | number | Frequency being transmitted on (MHz) |
| `timestamp_utc` | string | When this status occurred (ISO 8601 UTC) |

**Frequency**: On transmission start/end (event-driven)

**Examples**:
```json
// Pilot starts speaking
{
  "type": "speaking_status",
  "pilot_id": "F-16C-001-GUID",
  "is_speaking": true,
  "frequency": 251.0,
  "timestamp_utc": "2024-01-15T14:30:45.123Z"
}

// Pilot stops speaking
{
  "type": "speaking_status",
  "pilot_id": "F-16C-001-GUID",
  "is_speaking": false,
  "frequency": 251.0,
  "timestamp_utc": "2024-01-15T14:30:47.456Z"
}
```

**Use Case**: Tacview highlights the speaking pilot's aircraft on the map.

---

### 3. Sync Status (Heartbeat)

**Purpose**: Inform Tacview of synchronization health and provide heartbeat

**Message**:
```json
{
  "type": "sync_status",
  "connected": true,
  "sync_drift_ms": 150,
  "current_time_utc": "2024-01-15T14:30:45.273Z",
  "playback_state": "playing",
  "messages_received": 12345,
  "messages_sent": 234
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"sync_status"` |
| `connected` | boolean | AeroDebrief connection health |
| `sync_drift_ms` | number | Current drift in milliseconds |
| `current_time_utc` | string | AeroDebrief's current playback time |
| `playback_state` | string | `"playing"`, `"paused"`, `"stopped"`, `"seeking"` |
| `messages_received` | number | Total messages received from Tacview |
| `messages_sent` | number | Total messages sent to Tacview |

**Frequency**: Every 5 seconds (heartbeat)

**Example**:
```json
{
  "type": "sync_status",
  "connected": true,
  "sync_drift_ms": 150,
  "current_time_utc": "2024-01-15T14:30:45.273Z",
  "playback_state": "playing",
  "messages_received": 12345,
  "messages_sent": 234
}
```

---

### 4. Error

**Purpose**: Report errors to Tacview for debugging

**Message**:
```json
{
  "type": "error",
  "error_code": "FILE_NOT_FOUND",
  "error_message": "Recording file not found",
  "timestamp_utc": "2024-01-15T14:30:45.123Z"
}
```

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Always `"error"` |
| `error_code` | string | Error code (uppercase, underscore-separated) |
| `error_message` | string | Human-readable error description |
| `timestamp_utc` | string | When error occurred |

**Error Codes**:
- `FILE_NOT_FOUND`: Recording file not found
- `TIME_OUT_OF_RANGE`: Requested time outside recording bounds
- `FREQUENCY_NOT_FOUND`: Requested frequency not in recording
- `SYNC_DRIFT_HIGH`: Synchronization drift exceeds threshold
- `PLAYBACK_ERROR`: General playback error

**Example**:
```json
{
  "type": "error",
  "error_code": "TIME_OUT_OF_RANGE",
  "error_message": "Requested time 16:00:00 is beyond recording end at 15:59:59",
  "timestamp_utc": "2024-01-15T14:30:45.123Z"
}
```

---

## Message Sequence Diagrams

### Initial Connection & Handshake

```
Tacview Addon                     AeroDebrief
     ?                                  ?
     ????????????? TCP Connect ??????????
     ?                                  ?
     ???????????????? ready ?????????????
     ?   {version, recording_info}      ?
     ?                                  ?
     ???????? time_update ???????????????
     ?   {current_time, state}          ?
     ?                                  ?
     ?????? pilot_selection ?????????????
     ?   {selected_pilots: []}          ?
     ?                                  ?
```

### Normal Playback Synchronization

```
Tacview Addon                     AeroDebrief
     ?                                  ?
     ???? time_update (every 100ms) ?????
     ?                                  ?
     ?                                  ? (Audio playback)
     ?                                  ? (Detect transmission)
     ?                                  ?
     ??????? speaking_status ????????????
     ?   {pilot_id, is_speaking=true}   ?
     ?                                  ?
     ?  (Highlight pilot in Tacview)    ?
     ?                                  ?
     ??????? speaking_status ????????????
     ?   {pilot_id, is_speaking=false}  ?
     ?                                  ?
     ?  (Remove highlight)              ?
     ?                                  ?
     ???????? sync_status (5s) ??????????
     ?   {drift_ms, connected}          ?
     ?                                  ?
```

### Pilot Selection Change

```
Tacview Addon                     AeroDebrief
     ?                                  ?
     ?  (User selects aircraft)         ?
     ?                                  ?
     ?????? pilot_selection ?????????????
     ?   {selected_pilots: [...]        ?
     ?    frequencies, pan}             ?
     ?                                  ?
     ?                                  ? (Apply pilot filter)
     ?                                  ? (Apply spatial audio pan)
     ?                                  ? (Audio now filtered)
     ?                                  ?
```

### Seek Operation

```
Tacview Addon                     AeroDebrief
     ?                                  ?
     ?  (User seeks to new time)        ?
     ?                                  ?
     ??????????? seek ???????????????????
     ?   {target_time_utc}              ?
     ?                                  ?
     ?                                  ? (Stop current playback)
     ?                                  ? (Seek to new position)
     ?                                  ? (Resume playback)
     ?                                  ?
     ???????? sync_status ????????????? ?
     ?   {current_time, drift}          ?
     ?                                  ?
```

### Error Handling

```
Tacview Addon                     AeroDebrief
     ?                                  ?
     ??????????? seek ???????????????????
     ?   {target_time_utc}              ?
     ?                                  ?
     ?                                  ? (Time out of range!)
     ?                                  ?
     ???????????? error ?????????????????
     ?   {error_code, message}          ?
     ?                                  ?
     ?  (Log error, notify user)        ?
     ?                                  ?
```

---

## Time Format Specification

### ISO 8601 UTC Format
All timestamps MUST be in ISO 8601 format with UTC timezone:

**Format**: `YYYY-MM-DDTHH:MM:SS.sssZ`

**Examples**:
- `2024-01-15T14:30:45.123Z` (with milliseconds)
- `2024-01-15T14:30:45Z` (without milliseconds, acceptable)

**Parsing Rules**:
- Always UTC (trailing `Z`)
- Milliseconds optional but recommended
- 24-hour format
- Zero-padded components

**C# Parsing**:
```csharp
DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind)
```

**Lua Formatting**:
```lua
os.date("!%Y-%m-%dT%H:%M:%S", timestamp) .. "Z"
```

---

## Frequency Format

All frequencies are in **MHz** (Megahertz):
- `251.0` = 251 MHz
- `305.0` = 305 MHz
- `133.0` = 133 MHz
- `127.5` = 127.5 MHz (UHF with decimals)

**Not in Hz or kHz** - always MHz for consistency.

---

## Pan Value Format

Stereo pan values range from `-1.0` (full left) to `+1.0` (full right):

| Pan Value | Description |
|-----------|-------------|
| `-1.0` | Full left ear |
| `-0.8` | Mostly left |
| `0.0` | Center (both ears equally) |
| `+0.8` | Mostly right |
| `+1.0` | Full right ear |

**Calculation** (for N pilots):
```
panStep = 2.0 / max(N - 1, 1)
pan[i] = -1.0 + (i - 1) * panStep
```

**Example** (3 pilots):
- Pilot 1: pan = -1.0 (left)
- Pilot 2: pan = 0.0 (center)
- Pilot 3: pan = +1.0 (right)

---

## Error Handling

### Connection Errors
- **TCP connection refused**: Retry with exponential backoff
- **Connection dropped**: Automatic reconnection every 5 seconds
- **Timeout**: 10-second timeout, then reconnect

### Parsing Errors
- **Invalid JSON**: Log warning, ignore message
- **Unknown message type**: Log warning, ignore message
- **Missing required fields**: Log error, ignore message
- **Invalid time format**: Log error, request re-sync

### State Errors
- **Time out of range**: Send error message, clamp to bounds
- **Frequency not found**: Send error, ignore filter request
- **Invalid pilot ID**: Log warning, ignore speaking status

---

## Performance Considerations

### Message Frequency
- **High frequency** (10 Hz): `time_update`
- **Medium frequency** (0.2 Hz): `sync_status`
- **Event-driven**: All other messages

### Bandwidth
- Typical `time_update`: ~100 bytes
- Typical `pilot_selection`: ~500 bytes
- Average bandwidth: ~1 KB/s (negligible)

### Latency
- Target: <10ms for message send/receive
- Network: Localhost TCP has <1ms latency
- Processing: JSON parsing <1ms

---

## Security Considerations

### Localhost Only
- **Bind to 127.0.0.1 only** (not 0.0.0.0)
- Prevents external network access
- No authentication required

### Input Validation
- Validate all message fields
- Sanitize pilot names (prevent injection)
- Clamp numeric values to reasonable ranges
- Maximum message size: 64 KB

---

## Protocol Versioning

### Version Format
`MAJOR.MINOR.PATCH` (Semantic Versioning)

### Current Version
`1.0.0`

### Compatibility
- **Major**: Breaking changes (incompatible)
- **Minor**: New features (backward-compatible)
- **Patch**: Bug fixes (fully compatible)

### Version Negotiation
- AeroDebrief sends version in `ready` message
- Tacview checks compatibility
- If incompatible, Tacview logs warning

---

## Testing & Validation

### Test Tools
1. **Manual Testing**: Use `telnet` or `netcat` to send raw JSON
2. **Unit Tests**: Test JSON serialization/deserialization
3. **Integration Tests**: Full roundtrip between Tacview and AeroDebrief
4. **Load Tests**: High-frequency message stress test

### Sample Test Messages

**Test 1: Valid time update**
```json
{"type":"time_update","mission_time_utc":"2024-01-15T14:30:45.123Z","playback_state":"playing","playback_speed":1.0}
```

**Test 2: Invalid JSON (should be ignored)**
```json
{invalid json
```

**Test 3: Unknown message type (should be ignored)**
```json
{"type":"unknown_type","data":"test"}
```

---

## Future Protocol Extensions

### Planned Features
1. **Voice Transcript**: Send real-time speech-to-text
2. **Audio Streaming**: Stream audio directly to Tacview for recording
3. **Frequency Scanning**: Request specific frequency data
4. **Mission Export**: Export selected audio clips
5. **Multi-Mission Sync**: Support multiple recordings simultaneously

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
