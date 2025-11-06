# Tacview Integration - Implementation Summary

## Created Documentation

I've created a comprehensive plan for integrating AeroDebrief with Tacview. Here's what has been documented:

### ?? Planning Documents Created

1. **00-INTEGRATION-OVERVIEW.md** - Project overview and architecture
2. **01-LUA-ADDON-SPECIFICATION.md** - Tacview Lua addon design
3. **02-PROTOCOL-SPECIFICATION.md** - JSON communication protocol
4. **03-INTEGRATION-PROJECT-DESIGN.md** - C# integration layer architecture
5. **04-UI-DESIGN.md** - WPF user interface components
6. **05-CORE-MODIFICATIONS.md** - Core system modifications
7. **06-TESTING-STRATEGY.md** - Testing and quality assurance
8. **07-TACVIEW-MENU-GUIDE.md** - Tacview menu configuration guide
9. **08-FREQUENCY-FILTERING-FEATURE.md** - Frequency filtering implementation

### ?? Key Features Planned

#### 1. Bidirectional TCP Communication
- **Tacview ? AeroDebrief**: Time updates (10 Hz), pilot selection, playback commands, frequency filters
- **AeroDebrief ? Tacview**: Speaking status, sync health, connection state
- **Protocol**: Newline-delimited JSON over TCP (localhost:52001 by default)
- **Configurable Port**: Both Tacview and AeroDebrief support custom TCP ports

#### 2. Time Synchronization
- **UTC-based**: Single source of truth using ISO 8601 timestamps
- **Drift Correction**: Automatic speed adjustment for <500ms drift, seek for >500ms
- **Precision**: ±1 second accuracy maintained over 2+ hour sessions
- **Algorithm**: Three-tier correction (none, speed adjust, seek)

#### 3. Pilot Filtering & Spatial Audio
- **Frequency Mapping**: Tacview provides pilot ? frequencies mapping
- **Packet Filtering**: Only play packets from selected pilots on their frequencies
- **Frequency Filtering**: Per-pilot and general frequency selection via checkboxes
- **Spatial Audio (Pan)**: Configured in **Tacview menu** with Auto/Manual modes
  - **Auto Mode**: Evenly distribute pilots across stereo field
  - **Manual Mode**: Per-pilot pan configuration with presets or custom values
  - AeroDebrief **displays** pan but Tacview **controls** it

#### 4. Frequency Filtering (NEW)
- **Per-Pilot Frequency Selection**: Choose which frequencies to hear for each selected pilot (default: all enabled)
- **General Frequency Selection**: Filter frequencies for all non-selected pilots (default: all disabled)
- **Checkbox Interface**: Interactive checkboxes with "Select All" / "Deselect All"
- **Real-time Updates**: Changes immediately affect playback
- **Focus by Default**: General frequencies disabled by default to prevent audio clutter
- **Use Cases**:
  - Focus on selected pilots only (default behavior - no configuration needed)
  - Add tower communications from non-selected pilots
  - Monitor specific tactical frequencies
  - Complete audio isolation for analysis

#### 5. Configurable TCP Port (NEW)
- **Custom Port**: Change TCP port from default 52001 to any port (1024-65535)
- **UI Configuration**: Change port in Tacview menu and AeroDebrief settings
- **File Configuration**: Edit config files for automated setups
- **Multiple Instances**: Run multiple Tacview/AeroDebrief pairs with different ports
- **Port Validation**: Ensures port is available and in valid range
- **Auto-Save**: Configuration persists across restarts
- **Use Cases**:
  - Resolve port conflicts with other applications
  - Run multiple analysis sessions simultaneously
  - Comply with firewall/security policies
  - Separate test and production environments

#### 6. Bidirectional Configuration (NEW)
- **Configure Anywhere**: Make changes in either Tacview menu or AeroDebrief UI
- **Automatic Sync**: Changes propagate automatically between applications
- **Pan Configuration**: Adjust spatial audio pan in AeroDebrief UI ? syncs to Tacview
- **Frequency Filters**: Toggle frequency checkboxes in AeroDebrief ? syncs to Tacview
- **Real-Time Updates**: Changes apply immediately with visual confirmation
- **Debouncing**: Smart throttling prevents message spam during slider drags
- **Conflict Resolution**: Last-write-wins with confirmation loop
- **Use Cases**:
  - Quick frequency adjustments during playback review
  - Fine-tune pan without leaving AeroDebrief window
  - Experiment with filters without switching applications
  - Collaborative configuration (multiple users)

#### 7. Visual Feedback
- **Connection Status**: Color-coded indicator (red/yellow/green) with port display
- **Sync Quality Bar**: Real-time drift display with percentage
- **Selected Pilots List**: Shows callsign, aircraft, frequencies, pan indicator
- **Pan Mode Display**: Shows "Auto" or "Manual" mode set in Tacview
- **Frequency Filter Status**: Shows which frequencies are enabled
- **Speaking Indicators**: Highlights pilots in both Tacview and AeroDebrief
- **Port Display**: Shows current port in connection status
- **Interactive Controls**: Sliders, checkboxes, and buttons for configuration
- **Configuration Confirmation**: Visual feedback when changes sync to Tacview

#### 8. Robust Connection Management
- **Auto-Reconnection**: Exponential backoff with configurable retry
- **Health Monitoring**: Tracks drift, latency, message success rate
- **Error Recovery**: Graceful handling of disconnections and protocol errors
- **Connection Testing**: Test connection without full reconnect
- **Configurable Timeouts**: Adjust connection and receive timeouts

### ??? Tacview Menu Integration

Users configure spatial audio pan, frequency filters, **and connection settings** in Tacview via:

```
Tacview Menu ? AeroDebrief Sync
??? Configure Audio Pan...            (View/adjust pan settings)
??? Auto Pan Mode                     (Automatic distribution)
??? Manual Pan Mode                   (Per-pilot custom control)
??? ????????????????????????????????
??? Configure Pilot Frequencies...    (Per-pilot frequency checkboxes) ??? NEW
??? Configure General Frequencies...  (General frequency checkboxes)   ??? NEW
??? ????????????????????????????????
??? Settings...                       (TCP port, update rate, etc.) ??? NEW
??? ????????????????????????????????
??? About
```

**Settings Configuration** (NEW):
- **TCP Port**: Change port from default 52001 (requires restart)
- **Update Rate**: Adjust how often Tacview sends updates (1-60 Hz)
- **Max Clients**: Limit concurrent AeroDebrief connections
- **Auto-Reconnect**: Toggle automatic reconnection on disconnect
- **Speaking Indicators**: Toggle visual pilot highlighting
- **Save Settings**: Persist configuration across restarts

**Pan Modes**:
- **Auto**: Pilots distributed evenly from left (-1.0) to right (+1.0)
- **Manual**: User sets specific pan per pilot with presets or custom values
  - Presets: Full Left, Mostly Left, Slightly Left, Center, Slightly Right, Mostly Right, Full Right
  - Custom: Any value between -1.0 and +1.0

**Frequency Filtering**:
- **Per-Pilot**: Select which frequencies to monitor for each selected pilot
  - Default: All frequencies enabled for selected pilots
  - Example: Pilot has [251.0, 305.0, 133.0] but you only want [251.0, 305.0]
- **General**: Select which frequencies to monitor from ALL non-selected pilots
  - **Default: All frequencies disabled** - Focus on selected pilots only
  - Must explicitly enable frequencies to hear non-selected pilots
  - Example: Enable only tower [124.0] and GCI [243.0] frequencies to hear context from others

**AeroDebrief Role**: Displays pan, frequency filter status, and connection info but **does not allow editing** (except port configuration)

### ??? Architecture Overview

```
????????????????????????
?  Tacview (Master)    ?
?  Lua Addon           ?
?  TCP Server :52001   ?
?  + Menu UI           ? ??? User configures pan & frequencies
?    - Pan Config      ?
?    - Freq Filters    ?
????????????????????????
           ? JSON/TCP (includes pan_mode, pan values, freq filters)
????????????????????????
?  AeroDebrief.        ?
?  Integrations        ?
?  TCP Client          ?
?  Sync Engine         ?
?  Freq Filter         ? ??? Applies frequency filtering
????????????????????????
           ? Events
????????????????????????
?  AeroDebrief.UI      ?
?  Status Display      ?
?  Pilot List (Pan)    ? ??? Displays pan & freq filters
?  Freq Indicators     ?
????????????????????????
           ? Commands
????????????????????????
?  AeroDebrief.Core    ?
?  Playback Engine     ?
?  Audio Mixer (Pan)   ?
?  Packet Filter       ? ??? Filters by frequency
????????????????????????
```

### ?? Project Structure

```
src/AeroDebrief.Integrations/Tacview/
??? Client/                     # TCP client and connection management
?   ??? TacviewClient.cs
?   ??? TacviewConnection.cs
?   ??? TacviewReconnectionStrategy.cs
??? Sync/                       # Time synchronization engine
?   ??? TacviewSyncService.cs
?   ??? SyncAlgorithm.cs
?   ??? TimeConverter.cs
?   ??? SyncHealthMonitor.cs
??? Pilot/                      # Pilot filtering and spatial audio
?   ??? PilotMapper.cs
?   ??? PilotFilter.cs
?   ??? FrequencyFilter.cs      # NEW: Frequency filtering logic
?   ??? SpatialAudioCalculator.cs
??? Protocol/                   # JSON protocol messages
?   ??? TacviewProtocol.cs
?   ??? Messages/
?       ??? TimeUpdateMessage.cs
?       ??? PilotSelectionMessage.cs (includes freq filters)
?       ??? SpeakingStatusMessage.cs
?       ??? ... (8 message types total)
??? Models/                     # Data models
    ??? TacviewPilot.cs
    ??? ConnectionState.cs
    ??? SyncQuality.cs

External/Tacview/Addons/AeroDebriefSync/
??? main.lua                    # Entry point and event handlers
??? tcp_server.lua              # TCP server (LuaSocket)
??? protocol.lua                # JSON encoding/decoding (with freq filters)
??? pilot_extractor.lua         # Extract pilot info from Tacview
??? visual_effects.lua          # Speaking pilot highlights
??? pan_manager.lua             # Pan + Frequency filtering manager
??? menu_ui.lua                 # Tacview menu integration (pan + freq)
??? state_manager.lua           # State tracking
??? config.lua                  # Configuration
```

### ?? Message Flow Examples

#### Normal Playback
```
Tacview (10 Hz) ? time_update ? AeroDebrief
                                 ? Apply sync correction
AeroDebrief ? speaking_status ? Packet detected
             (Highlight pilot)
```

#### Pilot Selection with Pan and Frequency Filters
```
User selects aircraft in Tacview
User configures pan in Tacview menu (Auto or Manual)
User configures frequency filters (per-pilot and general)
        ?
pilot_selection (includes pan_mode, pan values, enabled frequencies) ? AeroDebrief
                                                                         ? Apply frequency filter
                                                                         ? Apply spatial audio pan
Only selected pilot audio plays on enabled frequencies with spatial positioning
```

#### Frequency Configuration in Tacview
```
User ? Tacview Menu ? AeroDebrief Sync ? Configure Pilot Frequencies...
        ?
Dialog shows checkboxes for each frequency:
? 251.0 MHz
? 305.0 MHz
? 133.0 MHz (user unchecks this)
        ?
pilot_selection message sent with enabled_frequencies: [251.0, 305.0]
        ?
AeroDebrief filters packets: only plays 251.0 and 305.0 for this pilot
```

### ?? UI Components

1. **TacviewStatusControl**: Main status display with connection indicator
2. **Sync Quality Bar**: Visual drift and quality display
3. **Selected Pilots List**: Shows active pilots with pan and frequency indicators
4. **Pan Mode Display**: Shows "Auto" or "Manual" (read-only)
5. **Frequency Filter Display**: Shows enabled/disabled frequencies per pilot
6. **Info Banner**: Explains how to configure pan and frequencies in Tacview
7. **Settings Panel**: Connection settings (pan/freq settings are in Tacview)

### ?? Core Modifications Required

1. **PlaybackController**: Add external time sync support
2. **FrequencyChannelMixer**: Add pilot-based filtering
3. **AudioOutputEngine**: Add spatial audio (pan) capability
4. **PacketFilter**: Add frequency filtering logic (NEW)

### ? Success Criteria

- **Timing**: 99.9% within ±1 second over 2+ hours
- **Latency**: <100ms for Tacview commands
- **Uptime**: 99.5% connection stability
- **CPU Overhead**: <5% additional load
- **User Experience**: Seamless integration with clear visual feedback
- **Pan Configuration**: Intuitive menu in Tacview, clear feedback in AeroDebrief
- **Frequency Filtering**: Accurate filtering with <1?s per-packet overhead (NEW)

### ?? Development Phases

1. **Week 1**: Foundation + Lua addon development (including menu UI + freq filters)
2. **Week 2-3**: Integration layer + UI components (display pan/freq, no editing)
3. **Week 3-4**: Core modifications + frequency filtering + testing
4. **Week 4-5**: Long-duration testing + optimization
5. **Week 5**: Documentation + deployment

### ?? Security & Performance

- **Localhost Only**: Binds to 127.0.0.1 (no external network)
- **Efficient Protocol**: ~1 KB/s bandwidth (negligible)
- **Low Latency**: <10ms message processing
- **Input Validation**: All messages validated and sanitized
- **Frequency Filter Performance**: O(1) hash table lookup per packet

### ?? Success Indicators

- ? Synchronization: 99.9% within ±1 second
- ? Uptime: 99.5% over 2-hour session
- ? Latency: <100ms for commands
- ? CPU Overhead: <5%
- ? User Experience: Seamless and intuitive
- ? Pan Configuration: Clear and accessible in Tacview menu
- ? Frequency Filtering: Real-time updates with minimal overhead (NEW)

### ?? Document Reference

| Document | Purpose |
|----------|---------|
| 00-INTEGRATION-OVERVIEW.md | Complete project architecture and overview |
| 01-LUA-ADDON-SPECIFICATION.md | Tacview Lua addon code and structure |
| 02-PROTOCOL-SPECIFICATION.md | JSON protocol message definitions |
| 03-INTEGRATION-PROJECT-DESIGN.md | C# integration layer classes |
| 04-UI-DESIGN.md | WPF controls and view models |
| 05-CORE-MODIFICATIONS.md | Core playback engine changes |
| 06-TESTING-STRATEGY.md | Test plans and quality assurance |
| 07-TACVIEW-MENU-GUIDE.md | Tacview menu pan configuration guide |
| 08-FREQUENCY-FILTERING-FEATURE.md | Frequency filtering implementation |
| 09-FREQUENCY-FILTERING-USER-GUIDE.md | Frequency filtering user guide |
| 10-DEFAULT-BEHAVIOR-CHANGE.md | General frequency default behavior change |
| 11-TCP-PORT-CONFIGURATION-GUIDE.md | TCP port configuration guide |
| 12-TCP-PORT-CONFIGURATION-SUMMARY.md | TCP port configuration summary |
| 13-BIDIRECTIONAL-CONFIGURATION.md | **Bidirectional configuration feature** (NEW) |
| IMPLEMENTATION-GUIDE.md | **Master implementation guide** |

### ?? Key Design Decisions

1. **Tacview is POC** - Tacview drives all sync operations
2. **UTC Only** - Single time format eliminates conversion issues
3. **Localhost Only** - Security by design (no network exposure)
4. **Opt-In Integration** - Fully backward compatible
5. **Event-Driven** - Loose coupling via events/delegates
6. **Pan in Tacview** - User configures spatial audio in Tacview menu, AeroDebrief displays and applies
7. **Frequency Filters in Tacview** - User configures frequency filters in Tacview menu (NEW)

---

## Quick Start for Development

### 1. Create Integrations Project
```bash
cd src
dotnet new classlib -n AeroDebrief.Integrations -f net9.0
dotnet sln add AeroDebrief.Integrations/AeroDebrief.Integrations.csproj
```

### 2. Add Dependencies
```bash
cd AeroDebrief.Integrations
dotnet add package System.Text.Json
dotnet add package NLog
dotnet add reference ../AeroDebrief.Core/AeroDebrief.Core.csproj
```

### 3. Create Lua Addon Structure
```bash
mkdir -p External/Tacview/Addons/AeroDebriefSync
# Copy Lua files from 01-LUA-ADDON-SPECIFICATION.md
# Include: main.lua, pan_manager.lua, menu_ui.lua, protocol.lua, etc.
```

### 4. Test TCP Communication
- Start Tacview with addon loaded
- Test menu appears: Tacview ? AeroDebrief Sync
- Run simple C# TCP client
- Send/receive test messages
- Verify JSON parsing

### 5. Test Pan Configuration
- Select aircraft in Tacview
- Open pan configuration menu
- Switch between Auto/Manual modes
- Set custom pan values
- Verify messages sent to AeroDebrief

### 6. Test Frequency Filtering (NEW)
- Select aircraft in Tacview
- Open "Configure Pilot Frequencies..."
- Toggle frequency checkboxes
- Verify immediate updates to AeroDebrief
- Test "Select All" / "Deselect All" buttons
- Test "Configure General Frequencies..."

### 7. Integrate with UI
- Add TacviewIntegrationViewModel to UnifiedPlayerViewModel
- Create TacviewStatusControl with pan + frequency display
- Show pan mode and frequency filter indicators
- Add info banner about Tacview menu
- Wire up events

---

**Document Status**: ? Planning Complete (with Frequency Filtering)  
**Next Phase**: Implementation  
**Estimated Effort**: 4-5 weeks  
**Risk Level**: Medium (dependent on Tacview API capabilities)  
**Pan Configuration**: Handled in Tacview menu (Auto/Manual modes)  
**Frequency Filtering**: Handled in Tacview menu (Per-pilot + General) (NEW)
