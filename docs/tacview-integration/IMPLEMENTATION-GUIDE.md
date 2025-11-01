# Tacview Integration - Complete Implementation Guide

## Document Overview

This is the **master implementation guide** for integrating AeroDebrief with Tacview. Follow this document sequentially to build the complete integration system.

**Status**: ?? Implementation Ready  
**Version**: 1.0  
**Estimated Effort**: 4-5 weeks  
**Target**: .NET 9, Lua 5.1+

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Architecture Overview](#architecture-overview)
3. [Phase 1: Project Setup](#phase-1-project-setup)
4. [Phase 2: Tacview Lua Addon](#phase-2-tacview-lua-addon)
5. [Phase 3: C# Integration Layer](#phase-3-c-integration-layer)
6. [Phase 4: Core Modifications](#phase-4-core-modifications)
7. [Phase 5: UI Implementation](#phase-5-ui-implementation)
8. [Phase 6: Testing & Validation](#phase-6-testing--validation)
9. [Phase 7: Documentation & Deployment](#phase-7-documentation--deployment)
10. [Reference Documentation](#reference-documentation)

---

## Prerequisites

### Development Environment

**Required**:
- ? Visual Studio 2022 (17.8+) or VS Code with C# DevKit
- ? .NET 9 SDK
- ? Tacview Advanced/Enterprise (for addon testing)
- ? Lua 5.1+ (included with Tacview)
- ? Git for version control

**Recommended**:
- Tacview SDK documentation access
- LuaSocket library (if not included with Tacview)
- Network debugging tool (Wireshark, Fiddler)

### Knowledge Requirements

**Essential**:
- C# async/await patterns
- WPF/XAML development
- TCP/IP networking
- JSON serialization

**Helpful**:
- Lua scripting basics
- Audio processing concepts
- Real-time synchronization algorithms

### Existing Codebase Familiarity

Read these before starting:
- `docs/Technical-Architecture.md` - Core system architecture
- `docs/CENTRALIZED-EVENT-SYSTEM.md` - Event system patterns
- `src/AeroDebrief.Core/Playback/PlaybackController.cs` - Playback engine
- `src/AeroDebrief.Core/Audio/AudioOutputEngine.cs` - Audio output

---

## Architecture Overview

### System Components

```
???????????????????????????????????????????????????????????????
? TACVIEW (Master - Point of Control)                         ?
? ??????????????????????????????????????????????????????????? ?
? ? Lua Addon (TCP Server on localhost:52001)              ? ?
? ? • Time updates (10 Hz)                                 ? ?
? ? • Pilot selection events                               ? ?
? ? • Frequency filters                                    ? ?
? ? • Pan configuration                                    ? ?
? ??????????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????????
                       ? JSON over TCP
???????????????????????????????????????????????????????????????
? AERODEBRIEF (Slave - Follower)                              ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.Integrations (New Project)                 ? ?
? ? ??????????????????????????????????????????????????????? ? ?
? ? ? TacviewClient - TCP client                         ? ? ?
? ? ? TacviewSyncService - Sync engine                   ? ? ?
? ? ? TacviewProtocol - Message handlers                 ? ? ?
? ? ? TacviewConfiguration - Settings                    ? ? ?
? ? ??????????????????????????????????????????????????????? ? ?
? ??????????????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.UI (Modifications)                         ? ?
? ? • TacviewStatusControl - Status display               ? ?
? ? • TacviewIntegrationViewModel - MVVM                  ? ?
? ? • Settings panel with port config                     ? ?
? ??????????????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.Core (Enhancements)                        ? ?
? ? • PlaybackController - External sync                  ? ?
? ? • AudioOutputEngine - Spatial audio (pan)             ? ?
? ? • FrequencyChannelMixer - Frequency filtering         ? ?
? ??????????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????????
```

### Key Principles

1. **Tacview is Point of Control (POC)**: All sync commands originate from Tacview
2. **UTC Time Only**: Single time format, no timezone conversions
3. **Localhost Only**: Security by binding to 127.0.0.1
4. **Event-Driven**: Loose coupling via existing event system
5. **Backward Compatible**: Works without Tacview (feature flag)
6. **Bidirectional Configuration** (NEW): Configuration changes can flow both directions:
   - **Tacview ? AeroDebrief**: Pan mode, frequency filters (primary)
   - **AeroDebrief ? Tacview**: Configuration overrides (secondary)

---

## Phase 1: Project Setup

**Duration**: 1-2 days  
**Goal**: Create project structure and dependencies

### Step 1.1: Create Integration Project

```bash
cd src
dotnet new classlib -n AeroDebrief.Integrations -f net9.0
dotnet sln ../../AeroDebrief.sln add AeroDebrief.Integrations/AeroDebrief.Integrations.csproj
```

### Step 1.2: Add Dependencies

```bash
cd AeroDebrief.Integrations
dotnet add package System.Text.Json --version 9.0.0
dotnet add package NLog --version 5.3.2
dotnet add reference ../AeroDebrief.Core/AeroDebrief.Core.csproj
```

### Step 1.3: Create Folder Structure

```
src/AeroDebrief.Integrations/
??? Tacview/
?   ??? Client/
?   ?   ??? TacviewClient.cs
?   ?   ??? TacviewConnection.cs
?   ?   ??? TacviewReconnectionStrategy.cs
?   ??? Sync/
?   ?   ??? TacviewSyncService.cs
?   ?   ??? SyncAlgorithm.cs
?   ?   ??? TimeConverter.cs
?   ?   ??? SyncHealthMonitor.cs
?   ??? Pilot/
?   ?   ??? PilotMapper.cs
?   ?   ??? PilotFilter.cs
?   ?   ??? FrequencyFilter.cs
?   ?   ??? SpatialAudioCalculator.cs
?   ??? Protocol/
?   ?   ??? TacviewProtocol.cs
?   ?   ??? Messages/
?   ?       ??? TimeUpdateMessage.cs
?   ?       ??? PilotSelectionMessage.cs
?   ?       ??? PlaybackCommandMessage.cs
?   ?       ??? SeekMessage.cs
?   ?       ??? SpeakingStatusMessage.cs
?   ?       ??? SyncStatusMessage.cs
?   ?       ??? ReadyMessage.cs
?   ??? Models/
?       ??? TacviewConfiguration.cs
?       ??? TacviewPilot.cs
?       ??? ConnectionState.cs
?       ??? SyncQuality.cs
??? Placeholder.cs (delete this)
```

### Step 1.4: Create Lua Addon Structure

```
External/Tacview/Addons/AeroDebriefSync/
??? main.lua
??? config.lua
??? tcp_server.lua
??? protocol.lua
??? state_manager.lua
??? pilot_extractor.lua
??? visual_effects.lua
??? pan_manager.lua
??? menu_ui.lua
??? utils.lua
??? manifest.txt
```

**Verification**: Project compiles successfully with no errors.

---

## Phase 2: Tacview Lua Addon

**Duration**: 3-4 days  
**Goal**: Working TCP server in Tacview that sends messages

**Reference**: See `docs/tacview-integration/01-LUA-ADDON-SPECIFICATION.md`

### Step 2.1: Create Manifest File

**File**: `External/Tacview/Addons/AeroDebriefSync/manifest.txt`

```ini
[Addon]
Title=AeroDebrief Voice Sync
Author=AeroDebrief Team
Version=1.0.0
MinimumTacviewVersion=1.9.0
Description=Synchronizes AeroDebrief voice recordings with Tacview mission replay

[Settings]
DefaultPort=52001
UpdateRate=10
EnableLogging=true
```

### Step 2.2: Implement Configuration System

**File**: `config.lua`

Copy implementation from `01-LUA-ADDON-SPECIFICATION.md` ? Configuration File section.

**Key Features**:
- Load/save config.txt
- Port configuration (default: 52001)
- Update rate (default: 10 Hz)
- Auto-reconnect settings

### Step 2.3: Implement TCP Server

**File**: `tcp_server.lua`

**Requirements**:
- Use LuaSocket library
- Bind to 127.0.0.1 (localhost only)
- Support multiple concurrent clients
- Newline-delimited JSON messages
- Non-blocking I/O

**Key Functions**:
```lua
function TcpServer.Start(port, bindAddress)
function TcpServer.Stop()
function TcpServer.Update()  -- Call from OnUpdate
function TcpServer.Broadcast(message)
function TcpServer.SetMessageHandler(handler)
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? tcp_server.lua

### Step 2.4: Implement Protocol Handler

**File**: `protocol.lua`

**Key Functions**:
```lua
function Protocol.Encode(message)  -- Table ? JSON string
function Protocol.Decode(messageStr)  -- JSON string ? Table
function Protocol.CreateTimeUpdate(missionTime, playbackState, playbackSpeed)
function Protocol.CreatePilotSelection(pilots)
function Protocol.CreatePlaybackCommand(command)
function Protocol.CreateSeek(targetTime)
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION-PROTOCOL.md`

### Step 2.5: Implement State Manager

**File**: `state_manager.lua`

Track state changes to avoid spamming updates:
```lua
function StateManager.HasStateChanged(time, state, speed)
function StateManager.UpdateState(time, state, speed)
```

### Step 2.6: Implement Pilot Extractor

**File**: `pilot_extractor.lua`

Extract pilot info from Tacview objects:
```lua
function PilotExtractor.ExtractPilotInfo(objectId)
-- Returns: { pilot_id, pilot_name, coalition, unit_type, frequencies }
```

### Step 2.7: Implement Pan Manager

**File**: `pan_manager.lua`

**Key Features**:
- Auto/Manual pan modes
- Per-pilot pan settings
- Frequency filtering (per-pilot and general)

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? pan_manager.lua

**Important**: General frequencies disabled by default (see `10-DEFAULT-BEHAVIOR-CHANGE.md`)

### Step 2.8: Implement Menu UI

**File**: `menu_ui.lua`

**Menu Structure**:
```
AeroDebrief Sync
??? Configure Audio Pan...
??? Auto Pan Mode
??? Manual Pan Mode
??? ?????????????????????????????
??? Configure Pilot Frequencies...
??? Configure General Frequencies...
??? ?????????????????????????????
??? Settings...
?   ??? Change TCP Port...
?   ??? Change Update Rate...
?   ??? Save Settings
??? ?????????????????????????????
??? About
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? menu_ui.lua

### Step 2.9: Implement Main Entry Point

**File**: `main.lua`

**Lifecycle**:
```lua
function AeroDebriefSync:OnInitialize()
    config.Initialize()
    tcpServer.Start(config.Port, config.BindAddress)
    panManager.Initialize()
    menuUI.RegisterMenu(self)
    -- Register event listeners
    -- Set message handler for incoming messages from AeroDebrief
    tcpServer.SetMessageHandler(function(message)
        self:OnMessageReceived(message)
    end)
end

function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    tcpServer.Update()
    -- Check for state changes
    -- Broadcast time updates
    -- Update visual effects
end

function AeroDebriefSync:OnPlaybackStateChange(isPlaying)
function AeroDebriefSync:OnSelectionChange()

-- NEW: Handle incoming configuration updates from AeroDebrief
function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if decoded.type == "frequency_filter_update" then
        -- Update frequency filter from AeroDebrief
        if decoded.pilot_id then
            -- Per-pilot frequency update
            local frequencies = decoded.enabled_frequencies or {}
            for _, freq in ipairs(frequencies) do
                panManager.SetPilotFrequencyEnabled(decoded.pilot_id, freq, true)
            end
            -- Disable frequencies not in list
            local allFreqs = panManager.GetAllFrequencies()
            for _, freq in ipairs(allFreqs) do
                if not contains(frequencies, freq) then
                    panManager.SetPilotFrequencyEnabled(decoded.pilot_id, freq, false)
                end
            end
        else
            -- General frequency update
            local frequencies = decoded.enabled_frequencies or {}
            for _, freq in ipairs(frequencies) do
                panManager.SetGeneralFrequencyEnabled(freq, true)
            end
            -- Disable frequencies not in list
            local allFreqs = panManager.GetAllFrequencies()
            for _, freq in ipairs(allFreqs) do
                if not contains(frequencies, freq) then
                    panManager.SetGeneralFrequencyEnabled(freq, false)
                end
            end
        end
        
        -- Trigger selection update to broadcast new state
        self:OnSelectionChange()
        
    elseif decoded.type == "pan_configuration" then
        -- Update pan configuration from AeroDebrief
        panManager.SetMode(decoded.pan_mode or "auto")
        
        if decoded.pilot_pan_settings then
            for pilotId, panValue in pairs(decoded.pilot_pan_settings) do
                panManager.SetPilotPan(pilotId, panValue)
            end
        end
        
        -- Trigger selection update to broadcast new state
        self:OnSelectionChange()
        
        Tacview.Log.Info(string.format("Pan configuration updated from AeroDebrief: %s mode", decoded.pan_mode))
    end
end

function AeroDebriefSync:OnShutdown()
    config.Save()
    tcpServer.Stop()
end

-- Helper function
local function contains(table, value)
    for _, v in ipairs(table) do
        if v == value then
            return true
        end
    end
    return false
end
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? main.lua

### Step 2.10: Install & Test Addon

**Installation**:
1. Copy `AeroDebriefSync/` folder to: `%APPDATA%\Tacview\AddOns\`
2. Restart Tacview
3. Check: `Help ? Show Log` for "AeroDebrief Sync: Initialized successfully"

**Testing**:
1. Open Tacview with a mission replay
2. Check: Menu item "AeroDebrief Sync" appears
3. Test: Settings ? Change TCP Port (should save/load)
4. Test: Select aircraft ? Should see "pilot_selection" in log
5. Test: Play/pause ? Should see "playback_command" in log

**Verification**:
- ? Addon loads without errors
- ? TCP server starts on configured port
- ? Menu appears with all items
- ? Messages logged when events occur

---

## Phase 3: C# Integration Layer

**Duration**: 5-7 days  
**Goal**: Connect to Tacview, parse messages, sync time

**Reference**: See `docs/tacview-integration/03-INTEGRATION-PROJECT-DESIGN.md`

### Step 3.1: Create Configuration Model

**File**: `src/AeroDebrief.Integrations/Tacview/Models/TacviewConfiguration.cs`

```csharp
public class TacviewConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 52001;
    public bool AutoConnect { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int ReconnectIntervalSeconds { get; set; } = 5;
    public bool EnableSyncDriftCorrection { get; set; } = true;
    public int MaxAcceptableDriftMs { get; set; } = 500;
    public bool EnableSpatialAudio { get; set; } = true;
    public bool EnableFrequencyFiltering { get; set; } = true;
    public int ConnectionTimeoutMs { get; set; } = 10000;
    public int ReceiveTimeoutMs { get; set; } = 5000;
    
    public bool Validate(out string? error) { /* ... */ }
    public void SaveToFile(string filePath) { /* ... */ }
    public static TacviewConfiguration LoadFromFile(string filePath) { /* ... */ }
}
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewConfiguration

### Step 3.2: Create Protocol Message Models

**Files**: `Protocol/Messages/*.cs`

Create classes for all message types:
- `TimeUpdateMessage.cs`
- `PilotSelectionMessage.cs`
- `PlaybackCommandMessage.cs`
- `SeekMessage.cs`
- `SpeakingStatusMessage.cs`
- `SyncStatusMessage.cs`
- `ReadyMessage.cs`
- **NEW**: `ConfigurationUpdateMessage.cs` (AeroDebrief ? Tacview)
- **NEW**: `PanConfigurationMessage.cs` (AeroDebrief ? Tacview)
- **NEW**: `FrequencyFilterUpdateMessage.cs` (AeroDebrief ? Tacview)

**Example** (`TimeUpdateMessage.cs`):
```csharp
public class TimeUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "time_update";
    
    [JsonPropertyName("mission_time_utc")]
    public string MissionTimeUtc { get; set; } = string.Empty;
    
    [JsonPropertyName("playback_state")]
    public string PlaybackState { get; set; } = string.Empty;
    
    [JsonPropertyName("playback_speed")]
    public double PlaybackSpeed { get; set; }
}
```

**NEW** (`FrequencyFilterUpdateMessage.cs`):
```csharp
public class FrequencyFilterUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "frequency_filter_update";
    
    [JsonPropertyName("pilot_id")]
    public string? PilotId { get; set; // null = general frequencies
    
    [JsonPropertyName("enabled_frequencies")]
    public List<double> EnabledFrequencies { get; set; } = new();
}
```

**NEW** (`PanConfigurationMessage.cs`):
```csharp
public class PanConfigurationMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pan_configuration";
    
    [JsonPropertyName("pan_mode")]
    public string PanMode { get; set; } = "auto"; // "auto" or "manual"
    
    [JsonPropertyName("pilot_pan_settings")]
    public Dictionary<string, double>? PilotPanSettings { get; set; // pilot_id -> pan value
}
```

**Reference**: See `02-PROTOCOL-SPECIFICATION.md` for all message formats

### Step 3.3: Implement TCP Client

**File**: `Client/TacviewClient.cs`

**Key Features**:
- Async TCP connection
- Message receive loop (newline-delimited JSON)
- Message send with queue
- Disconnection detection
- Events for connection state changes

**Key Methods**:
```csharp
public async Task ConnectAsync(CancellationToken cancellationToken)
public async Task DisconnectAsync()
public async Task SendMessageAsync(object message, CancellationToken cancellationToken)
private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)

public event EventHandler<TacviewMessage>? MessageReceived;
public event EventHandler? Connected;
public event EventHandler<string>? Disconnected;
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewClient

### Step 3.4: Implement Protocol Handler

**File**: `Protocol/TacviewProtocol.cs`

**Key Features**:
- Parse JSON messages by type
- Serialize outgoing messages
- Error handling and logging

```csharp
public static object? ParseMessage(string json)
{
    // Deserialize to base object, check "type" field
    // Deserialize to specific message type
    // Return typed message
}

public static string SerializeMessage(object message)
{
    // Serialize to JSON
    // Add newline delimiter
}
```

### Step 3.5: Implement Time Synchronization

**File**: `Sync/TacviewSyncService.cs`

**Sync Algorithm** (see `02-PROTOCOL-SPECIFICATION.md` ? Time Synchronization):

```csharp
public class TacviewSyncService
{
    private readonly PlaybackController _playbackController;
    private DateTime _recordingStartUtc;
    
    public async Task HandleTimeUpdate(TimeUpdateMessage message)
    {
        // Parse Tacview time to UTC
        var tacviewTime = DateTime.Parse(message.MissionTimeUtc, null, DateTimeStyles.RoundtripKind);
        
        // Convert to offset from recording start
        var targetOffset = tacviewTime - _recordingStartUtc;
        
        // Get current playback position
        var currentOffset = _playbackController.CurrentPosition;
        
        // Calculate drift
        var drift = Math.Abs((targetOffset - currentOffset).TotalMilliseconds);
        
        // Apply correction
        if (drift > 500)
        {
            // Large drift: immediate seek
            await _playbackController.SeekAsync(targetOffset);
        }
        else if (drift > 100)
        {
            // Medium drift: speed adjustment
            var speedAdjust = drift > 0 ? 1.02 : 0.98;
            _playbackController.SetPlaybackSpeed(speedAdjust);
        }
        else
        {
            // Small drift: normal speed
            _playbackController.SetPlaybackSpeed(1.0);
        }
        
        // Update playback state
        if (message.PlaybackState == "playing" && !_playbackController.IsPlaying)
        {
            await _playbackController.PlayAsync();
        }
        else if (message.PlaybackState == "paused" && _playbackController.IsPlaying)
        {
            _playbackController.Pause();
        }
    }
}
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewSyncService

### Step 3.6: Implement Pilot Filter

**File**: `Pilot/FrequencyFilter.cs`

**Frequency Filtering** (see `08-FREQUENCY-FILTERING-FEATURE.md`):

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
            if (selectedPilot.EnabledFrequencies == null || !selectedPilot.EnabledFrequencies.Any())
                return true;
            
            return selectedPilot.EnabledFrequencies.Contains(packet.Frequency);
        }
        else
        {
            // Non-selected pilot - check general enabled frequencies
            // Default: all DISABLED (empty list) - no audio from non-selected pilots
            if (_currentSelection.GeneralEnabledFrequencies == null || 
                !_currentSelection.GeneralEnabledFrequencies.Any())
                return false;
            
            return _currentSelection.GeneralEnabledFrequencies.Contains(packet.Frequency);
        }
    }
    
    public void UpdateSelection(TacviewPilotSelection selection)
    {
        _currentSelection = selection;
    }
}
```

### Step 3.7: Implement Connection Manager

**File**: `Client/TacviewReconnectionStrategy.cs`

**Auto-Reconnection**:
- Exponential backoff
- Configurable max attempts
- Event notifications

```csharp
public class TacviewReconnectionStrategy
{
    public async Task<bool> TryReconnectAsync(TacviewClient client, TacviewConfiguration config)
    {
        int attempt = 0;
        while (attempt < config.MaxReconnectAttempts)
        {
            attempt++;
            var delay = Math.Min(config.ReconnectIntervalSeconds * attempt, 30);
            
            Logger.Info($"Reconnection attempt {attempt}/{config.MaxReconnectAttempts} in {delay}s");
            await Task.Delay(TimeSpan.FromSeconds(delay));
            
            try
            {
                await client.ConnectAsync(CancellationToken.None);
                Logger.Info("Reconnection successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Reconnection attempt {attempt} failed");
            }
        }
        
        return false;
    }
}
```

### Step 3.8: Create Integration Service

**File**: `Tacview/TacviewIntegrationService.cs`

**Orchestrator** that coordinates all components:

```csharp
public class TacviewIntegrationService
{
    private readonly TacviewClient _client;
    private readonly TacviewSyncService _syncService;
    private readonly TacviewAudioFilter _audioFilter;
    private readonly TacviewConfiguration _config;
    private readonly TacviewReconnectionStrategy _reconnectionStrategy;
    
    public async Task StartAsync()
    {
        // Load configuration
        _config = TacviewConfiguration.LoadFromFile(ConfigPath);
        
        // Initialize components
        _client.MessageReceived += OnMessageReceived;
        _client.Disconnected += OnDisconnected;
        
        // Connect if auto-connect enabled
        if (_config.AutoConnect)
        {
            await ConnectAsync();
        }
    }
    
    private void OnMessageReceived(object? sender, TacviewMessage message)
    {
        // Dispatch to appropriate handler
        switch (message)
        {
            case TimeUpdateMessage timeUpdate:
                await _syncService.HandleTimeUpdate(timeUpdate);
                break;
            
            case PilotSelectionMessage pilotSelection:
                _audioFilter.UpdateSelection(pilotSelection);
                break;
            
            case PlaybackCommandMessage command:
                await _syncService.HandlePlaybackCommand(command);
                break;
            
            case SeekMessage seek:
                await _syncService.HandleSeek(seek);
                break;
        }
    }
    
    private async void OnDisconnected(object? sender, string reason)
    {
        Logger.Warn($"Disconnected from Tacview: {reason}");
        
        if (_config.AutoReconnect)
        {
            await _reconnectionStrategy.TryReconnectAsync(_client, _config);
        }
    }
}
```

### Step 3.9: Test Integration Layer

**Unit Tests** (`tests/AeroDebrief.Tests/Integrations/Tacview/*.cs`):

```csharp
[Fact]
public async Task TacviewClient_ConnectsSuccessfully()
{
    var client = new TacviewClient();
    await client.ConnectAsync("127.0.0.1", 52001, CancellationToken.None);
    Assert.True(client.IsConnected);
}

[Fact]
public void FrequencyFilter_FiltersSelectedPilot()
{
    var filter = new TacviewAudioFilter();
    var selection = new TacviewPilotSelection
    {
        SelectedPilots = new List<TacviewPilot>
        {
            new() { PilotId = "Test-1", EnabledFrequencies = new List<double> { 251.0 } }
        }
    };
    filter.UpdateSelection(selection);
    
    var packet = new AudioPacketMetadata(/* frequency: 251.0, guid: "Test-1" */);
    Assert.True(filter.ShouldPlayPacket(packet));
    
    var packet2 = new AudioPacketMetadata(/* frequency: 305.0, guid: "Test-1" */);
    Assert.False(filter.ShouldPlayPacket(packet2));
}
```

**Verification**:
- ? Can connect to Tacview TCP server
- ? Can receive and parse JSON messages
- ? Configuration loads/saves correctly
- ? Frequency filter works as expected
- ? Auto-reconnect works

---

## Phase 4: Core Modifications

**Duration**: 3-4 days  
**Goal**: Enable external sync, frequency filtering, spatial audio

**Reference**: See `docs/tacview-integration/05-CORE-MODIFICATIONS.md`

### Step 4.1: Add External Sync to PlaybackController

**File**: `src/AeroDebrief.Core/Playback/PlaybackController.cs`

**Add Interface**:
```csharp
public interface IExternalTimeSource
{
    TimeSpan CurrentTime { get; }
    bool IsPlaying { get; }
    double PlaybackSpeed { get; }
    
    event EventHandler<TimeSpan>? TimeChanged;
    event EventHandler<bool>? PlaybackStateChanged;
}
```

**Modify PlaybackController**:
```csharp
public class PlaybackController
{
    private IExternalTimeSource? _externalTimeSource;
    
    public void SetExternalTimeSource(IExternalTimeSource? source)
    {
        if (_externalTimeSource != null)
        {
            _externalTimeSource.TimeChanged -= OnExternalTimeChanged;
            _externalTimeSource.PlaybackStateChanged -= OnExternalPlaybackStateChanged;
        }
        
        _externalTimeSource = source;
        
        if (_externalTimeSource != null)
        {
            _externalTimeSource.TimeChanged += OnExternalTimeChanged;
            _externalTimeSource.PlaybackStateChanged += OnExternalPlaybackStateChanged;
        }
    }
    
    private void OnExternalTimeChanged(object? sender, TimeSpan targetTime)
    {
        // Calculate drift
        var drift = Math.Abs((targetTime - CurrentPosition).TotalMilliseconds);
        
        if (drift > 500)
        {
            // Large drift: seek
            SeekAsync(targetTime).GetAwaiter().GetResult();
        }
        else if (drift > 100)
        {
            // Medium drift: adjust speed
            var speedAdjust = targetTime > CurrentPosition ? 1.02 : 0.98;
            SetPlaybackSpeed(speedAdjust);
        }
        else
        {
            // Small drift: normal
            SetPlaybackSpeed(1.0);
        }
    }
}
```

### Step 4.2: Add Frequency Filtering to Audio Pipeline

**File**: `src/AeroDebrief.Core/Audio/FrequencyChannelMixer.cs`

**Add Filter Interface**:
```csharp
public interface IAudioPacketFilter
{
    bool ShouldPlayPacket(AudioPacketMetadata packet);
}
```

**Modify FrequencyChannelMixer**:
```csharp
public class FrequencyChannelMixer
{
    private IAudioPacketFilter? _packetFilter;
    
    public void SetPacketFilter(IAudioPacketFilter? filter)
    {
        _packetFilter = filter;
    }
    
    private void ProcessPacket(AudioPacketMetadata packet)
    {
        // Apply filter if set
        if (_packetFilter != null && !_packetFilter.ShouldPlayPacket(packet))
        {
            return; // Skip packet
        }
        
        // ... existing processing logic
    }
}
```

### Step 4.3: Add Spatial Audio (Pan) Support

**File**: `src/AeroDebrief.Core/Audio/AudioOutputEngine.cs`

**Add Pan Interface**:
```csharp
public interface ISpatialAudioProvider
{
    double GetPanForPilot(string pilotId);
}
```

**Modify AudioOutputEngine**:
```csharp
public class AudioOutputEngine
{
    private ISpatialAudioProvider? _spatialAudioProvider;
    
    public void SetSpatialAudioProvider(ISpatialAudioProvider? provider)
    {
        _spatialAudioProvider = provider;
    }
    
    private void ApplySpatialAudio(AudioPacketMetadata packet, float[] audioData)
    {
        if (_spatialAudioProvider == null)
            return; // No spatial audio
        
        var pan = _spatialAudioProvider.GetPanForPilot(packet.TransmitterGuid);
        
        // Calculate L/R gains
        var leftGain = (float)((1.0 - pan) / 2.0);
        var rightGain = (float)((1.0 + pan) / 2.0);
        
        // Apply pan to stereo output
        for (int i = 0; i < audioData.Length; i += 2)
        {
            audioData[i] *= leftGain;     // Left channel
            audioData[i + 1] *= rightGain; // Right channel
        }
    }
}
```

### Step 4.4: Wire Up Interfaces

**In** `TacviewIntegrationService`:

```csharp
public void Initialize(PlaybackController playbackController, 
                       FrequencyChannelMixer mixer, 
                       AudioOutputEngine audioEngine)
{
    // Set up external time source
    playbackController.SetExternalTimeSource(_syncService);
    
    // Set up frequency filter
    mixer.SetPacketFilter(_audioFilter);
    
    // Set up spatial audio
    audioEngine.SetSpatialAudioProvider(_audioFilter);
}
```

### Step 4.5: Test Core Modifications

**Unit Tests**:

```csharp
[Fact]
public void PlaybackController_SyncsWithExternalSource()
{
    var controller = new PlaybackController();
    var externalSource = new MockExternalTimeSource();
    
    controller.SetExternalTimeSource(externalSource);
    
    externalSource.TriggerTimeChange(TimeSpan.FromSeconds(100));
    
    Assert.InRange(controller.CurrentPosition.TotalSeconds, 99, 101);
}

[Fact]
public void FrequencyChannelMixer_AppliesFilter()
{
    var mixer = new FrequencyChannelMixer();
    var filter = new MockPacketFilter(shouldAllow: false);
    
    mixer.SetPacketFilter(filter);
    
    // Process packet - should be filtered out
    // Verify packet was not added to output
}
```

**Verification**:
- ? External sync works without breaking normal playback
- ? Frequency filter correctly blocks packets
- ? Spatial audio applies pan correctly
- ? All existing tests still pass

---

## Phase 5: UI Implementation

**Duration**: 3-4 days  
**Goal**: Display connection status, sync quality, settings

**Reference**: See `docs/tacview-integration/04-UI-DESIGN.md`

### Step 5.1: Create View Model

**File**: `src/AeroDebrief.UI/ViewModels/TacviewIntegrationViewModel.cs`

```csharp
public class TacviewIntegrationViewModel : ObservableObject
{
    private readonly TacviewIntegrationService _integrationService;
    
    // Connection state
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string _statusMessage = "Disconnected";
    private Brush _statusColor = Brushes.Red;
    
    // Sync quality
    private int _syncDriftMs;
    private double _syncQualityPercent;
    
    // Selected pilots
    private ObservableCollection<TacviewPilotViewModel> _selectedPilots = new();
    
    // Configuration
    private TacviewConfiguration _config;
    
    // Commands
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    
    // NEW: Configuration update commands
    public ICommand UpdatePilotFrequenciesCommand { get; }
    public ICommand UpdateGeneralFrequenciesCommand { get; }
    public ICommand UpdatePanConfigurationCommand { get; }
    public ICommand ToggleFrequencyCommand { get; }
    
    public TacviewIntegrationViewModel(TacviewIntegrationService integrationService)
    {
        _integrationService = integrationService;
        
        // Subscribe to events
        _integrationService.ConnectionStateChanged += OnConnectionStateChanged;
        _integrationService.SyncQualityChanged += OnSyncQualityChanged;
        _integrationService.PilotSelectionChanged += OnPilotSelectionChanged;
        
        // Load configuration
        _config = TacviewConfiguration.LoadFromFile(ConfigPath);
        
        // Initialize commands
        UpdatePilotFrequenciesCommand = new RelayCommand<TacviewPilotViewModel>(
            pilot => UpdatePilotFrequencies(pilot),
            pilot => pilot != null && IsConnected);
        
        UpdateGeneralFrequenciesCommand = new RelayCommand(
            () => UpdateGeneralFrequencies(),
            () => IsConnected);
        
        UpdatePanConfigurationCommand = new RelayCommand(
            () => UpdatePanConfiguration(),
            () => IsConnected);
        
        ToggleFrequencyCommand = new RelayCommand<(TacviewPilotViewModel pilot, double frequency)>(
            param => ToggleFrequency(param.pilot, param.frequency),
            param => IsConnected);
    }
    
    // NEW: Send pilot frequency update to Tacview
    private async void UpdatePilotFrequencies(TacviewPilotViewModel pilot)
    {
        try
        {
            var message = new FrequencyFilterUpdateMessage
            {
                PilotId = pilot.PilotId,
                EnabledFrequencies = pilot.EnabledFrequencies.ToList()
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info($"Sent pilot frequency update to Tacview: {pilot.PilotName}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send pilot frequency update");
            MessageBox.Show($"Failed to update frequencies in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Send general frequency update to Tacview
    private async void UpdateGeneralFrequencies()
    {
        try
        {
            var message = new FrequencyFilterUpdateMessage
            {
                PilotId = null, // null = general frequencies
                EnabledFrequencies = GeneralEnabledFrequencies.ToList()
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info("Sent general frequency update to Tacview");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send general frequency update");
            MessageBox.Show($"Failed to update general frequencies in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Send pan configuration to Tacview
    private async void UpdatePanConfiguration()
    {
        try
        {
            var pilotPanSettings = new Dictionary<string, double>();
            foreach (var pilot in SelectedPilots)
            {
                pilotPanSettings[pilot.PilotId] = pilot.Pan;
            }
            
            var message = new PanConfigurationMessage
            {
                PanMode = PanMode, // "auto" or "manual"
                PilotPanSettings = pilotPanSettings
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info($"Sent pan configuration update to Tacview: {PanMode} mode");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send pan configuration update");
            MessageBox.Show($"Failed to update pan configuration in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Toggle frequency for a pilot
    private async void ToggleFrequency(TacviewPilotViewModel pilot, double frequency)
    {
        if (pilot.EnabledFrequencies.Contains(frequency))
        {
            pilot.EnabledFrequencies.Remove(frequency);
        }
        else
        {
            pilot.EnabledFrequencies.Add(frequency);
        }
        
        // Send update to Tacview
        await UpdatePilotFrequencies(pilot);
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        ConnectionState = state;
        StatusMessage = state switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Synchronized => "Synchronized",
            ConnectionState.Degraded => "Degraded",
            _ => "Disconnected"
        };
        
        StatusColor = state switch
        {
            ConnectionState.Synchronized => Brushes.Green,
            ConnectionState.Connected => Brushes.Yellow,
            ConnectionState.Degraded => Brushes.Orange,
            _ => Brushes.Red
        };
        
        // Update command can-execute state
        CommandManager.InvalidateRequerySuggested();
    }
}
```

### Step 5.2: Create Status Control

**File**: `src/AeroDebrief.UI/Controls/Tacview/TacviewStatusControl.xaml`

```xaml
<UserControl x:Class="AeroDebrief.UI.Controls.Tacview.TacviewStatusControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Style="{StaticResource PanelStyle}">
        <StackPanel>
            <!-- Header -->
            <Grid>
                <TextBlock Text="?? Tacview Integration" 
                          Style="{StaticResource HeadingStyle}"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="??" Command="{Binding OpenSettingsCommand}"
                           ToolTip="Settings"/>
                    <Button Content="??" Command="{Binding ReconnectCommand}"
                           ToolTip="Reconnect"/>
                </StackPanel>
            </Grid>
            
            <!-- Connection Status -->
            <Grid Margin="0,8,0,0">
                <Ellipse Width="12" Height="12" 
                        Fill="{Binding StatusColor}"
                        ToolTip="{Binding StatusMessage}"/>
                <TextBlock Text="{Binding StatusMessage}" Margin="20,0,0,0"/>
                <TextBlock Text="{Binding PortDisplay}" 
                          HorizontalAlignment="Right"
                          Foreground="Gray"
                          FontSize="10"/>
            </Grid>
            
            <!-- Sync Quality Bar -->
            <Border Visibility="{Binding IsSynchronized, Converter={StaticResource BoolToVisibilityConverter}}"
                   Margin="0,8,0,0">
                <Grid>
                    <TextBlock Text="Sync Quality" FontSize="10" Foreground="Gray"/>
                    <ProgressBar Value="{Binding SyncQualityPercent}" 
                                Maximum="100" Height="6"
                                Margin="0,16,0,0"/>
                    <TextBlock Text="{Binding SyncDriftDisplay}" 
                              HorizontalAlignment="Right"
                              FontSize="10" Margin="0,16,0,0"/>
                </Grid>
            </Border>
            
            <!-- NEW: Pan Configuration -->
            <Expander Header="??? Spatial Audio (Pan)" IsExpanded="False"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <StackPanel Padding="12" Spacing="8">
                    <TextBlock Text="Pan Mode:" FontWeight="SemiBold"/>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <RadioButton Content="Auto" GroupName="PanMode"
                                    IsChecked="{Binding IsAutoPan}"
                                    Command="{Binding UpdatePanConfigurationCommand}"/>
                        <RadioButton Content="Manual" GroupName="PanMode"
                                    IsChecked="{Binding IsManualPan}"
                                    Command="{Binding UpdatePanConfigurationCommand}"/>
                    </StackPanel>
                    
                    <Separator Margin="0,8"/>
                    
                    <TextBlock Text="Pilot Pan Settings:" FontWeight="SemiBold"
                              Visibility="{Binding IsManualPan, Converter={StaticResource BoolToVisibilityConverter}}"/>
                    <ItemsControl ItemsSource="{Binding SelectedPilots}"
                                 Visibility="{Binding IsManualPan, Converter={StaticResource BoolToVisibilityConverter}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Style="{StaticResource PilotItemStyle}" Margin="0,4">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="100"/>
                                        </Grid.ColumnDefinitions>
                                        
                                        <TextBlock Grid.Column="0" Text="{Binding PilotName}" 
                                                  VerticalAlignment="Center"/>
                                        
                                        <Slider Grid.Column="2" 
                                               Minimum="-1" Maximum="1" 
                                               Value="{Binding Pan, Mode=TwoWay}"
                                               TickFrequency="0.1"
                                               IsSnapToTickEnabled="True"
                                               ToolTip="Pan: -1 (left) to +1 (right)">
                                            <Slider.CommandParameter>
                                                <MultiBinding Converter="{StaticResource TupleConverter}">
                                                    <Binding Path="DataContext" RelativeSource="{RelativeSource AncestorType=UserControl}"/>
                                                    <Binding/>
                                                </MultiBinding>
                                            </Slider.CommandParameter>
                                            <Slider.Command>
                                                <Binding Path="DataContext.UpdatePanConfigurationCommand" 
                                                        RelativeSource="{RelativeSource AncestorType=UserControl}"/>
                                            </Slider.Command>
                                        </Slider>
                                        
                                        <TextBlock Grid.Column="1" 
                                                  Text="{Binding PanDisplay}"
                                                  Margin="8,0"
                                                  VerticalAlignment="Center"
                                                  FontFamily="Consolas"/>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <TextBlock Text="?? Changes sync to Tacview automatically"
                              FontSize="10" Foreground="Gray" Margin="0,8,0,0"/>
                </StackPanel>
            </Expander>
            
            <!-- NEW: Frequency Filtering -->
            <Expander Header="?? Frequency Filtering" IsExpanded="False"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <StackPanel Padding="12" Spacing="12">
                    <!-- Per-Pilot Frequencies -->
                    <TextBlock Text="Per-Pilot Frequencies:" FontWeight="SemiBold"/>
                    <ItemsControl ItemsSource="{Binding SelectedPilots}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Expander Header="{Binding PilotName}" Margin="0,4">
                                    <StackPanel Padding="8" Spacing="4">
                                        <ItemsControl ItemsSource="{Binding AllFrequencies}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <CheckBox Content="{Binding Display}"
                                                             IsChecked="{Binding IsEnabled, Mode=TwoWay}"
                                                             Command="{Binding DataContext.ToggleFrequencyCommand, 
                                                                             RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                             CommandParameter="{Binding}"/>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                        
                                        <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                                            <Button Content="? Enable All" 
                                                   Command="{Binding EnableAllFrequenciesCommand}"/>
                                            <Button Content="? Disable All" 
                                                   Command="{Binding DisableAllFrequenciesCommand}"/>
                                        </StackPanel>
                                    </StackPanel>
                                </Expander>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <Separator Margin="0,8"/>
                    
                    <!-- General Frequencies -->
                    <TextBlock Text="General Frequencies (Non-Selected Pilots):" FontWeight="SemiBold"/>
                    <TextBlock Text="Default: All disabled" FontSize="10" Foreground="Gray"/>
                    
                    <ItemsControl ItemsSource="{Binding AllKnownFrequencies}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <CheckBox Content="{Binding Display}"
                                         IsChecked="{Binding IsGeneralEnabled, Mode=TwoWay}"
                                         Command="{Binding DataContext.UpdateGeneralFrequenciesCommand, 
                                                         RelativeSource={RelativeSource AncestorType=UserControl}}"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                        <Button Content="? Enable All General" 
                               Command="{Binding EnableAllGeneralFrequenciesCommand}"/>
                        <Button Content="? Disable All General" 
                               Command="{Binding DisableAllGeneralFrequenciesCommand}"/>
                    </StackPanel>
                    
                    <TextBlock Text="?? Changes sync to Tacview automatically"
                              FontSize="10" Foreground="Gray" Margin="0,8,0,0"/>
                </StackPanel>
            </Expander>
            
            <!-- Selected Pilots (Read-Only Summary) -->
            <Expander Header="Selected Pilots" IsExpanded="True"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <ItemsControl ItemsSource="{Binding SelectedPilots}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Style="{StaticResource PilotItemStyle}">
                                <Grid>
                                    <TextBlock Text="{Binding PilotName}"/>
                                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                                        <TextBlock Text="{Binding PanDisplay}" 
                                                  Foreground="Gray" FontSize="10"/>
                                        <TextBlock Text="{Binding FrequencyDisplay}" 
                                                  Foreground="Gray" FontSize="10" Margin="8,0,0,0"/>
                                    </StackPanel>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Expander>
            
            <!-- Settings Panel -->
            <Expander Header="?? Settings">
                <StackPanel Padding="12" Spacing="8">
                    <CheckBox Content="Auto-connect on startup"
                             IsChecked="{Binding Config.AutoConnect}"/>
                    <CheckBox Content="Auto-reconnect on disconnect"
                             IsChecked="{Binding Config.AutoReconnect}"/>
                    
                    <Separator Margin="0,8"/>
                    
                    <TextBlock Text="Connection" FontWeight="SemiBold"/>
                    <Grid ColumnDefinitions="Auto,*,Auto,Auto">
                        <TextBlock Text="Host:" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding Config.Host}"/>
                        
                        <TextBlock Grid.Column="2" Text="Port:" 
                                  VerticalAlignment="Center" Margin="8,0,0,0"/>
                        <TextBox Grid.Column="3" Text="{Binding Config.Port}" Width="80"/>
                    </Grid>
                    
                    <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                        <Button Content="Test Connection" 
                               Command="{Binding TestConnectionCommand}"/>
                        <Button Content="Save Settings" 
                               Command="{Binding SaveSettingsCommand}"/>
                    </StackPanel>
                    
                    <!-- Info about configuration sync -->
                    <Border Background="{StaticResource InfoBackgroundBrush}"
                           BorderBrush="{StaticResource InfoBorderBrush}"
                           BorderThickness="1" Padding="8" CornerRadius="4"
                           Margin="0,8,0,0">
                        <StackPanel Spacing="4">
                            <TextBlock Text="?? Bidirectional Sync" 
                                      FontWeight="SemiBold" FontSize="11"/>
                            <TextBlock TextWrapping="Wrap" FontSize="10">
                                Configuration changes made in AeroDebrief are automatically synced to Tacview:
                            </TextBlock>
                            <TextBlock FontSize="10" Foreground="Gray" Margin="8,0,0,0">
                                <Run Text="• Pan mode and values"/>
                                <LineBreak/>
                                <Run Text="• Per-pilot frequency filters"/>
                                <LineBreak/>
                                <Run Text="• General frequency filters"/>
                            </TextBlock>
                            <TextBlock TextWrapping="Wrap" FontSize="10" Margin="0,4,0,0">
                                You can also configure these in Tacview's menu:
                            </TextBlock>
                            <TextBlock FontSize="10" Foreground="Gray" Margin="8,0,0,0">
                                <Run Text="• Tacview ? AeroDebrief Sync ? Configure Audio Pan"/>
                                <LineBreak/>
                                <Run Text="• Tacview ? AeroDebrief Sync ? Configure Frequencies"/>
                            </TextBlock>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Expander>
        </StackPanel>
    </Border>
</UserControl>
```

### Step 5.3: Integrate with Main Player UI

**File**: `src/AeroDebrief.UI/ViewModels/UnifiedPlayerViewModel.cs`

```csharp
public class UnifiedPlayerViewModel : ObservableObject
{
    // Add Tacview integration
    private TacviewIntegrationViewModel? _tacviewIntegration;
    
    public TacviewIntegrationViewModel? TacviewIntegration
    {
        get => _tacviewIntegration;
        set => SetProperty(ref _tacviewIntegration, value);
    }
    
    public UnifiedPlayerViewModel(/* existing params */, 
                                  TacviewIntegrationService tacviewService)
    {
        // ... existing initialization
        
        // Initialize Tacview integration
        TacviewIntegration = new TacviewIntegrationViewModel(tacviewService);
        
        // Wire up to playback controller
        tacviewService.Initialize(_playbackController, _mixer, _audioEngine);
    }
}
```

**Update Main Window XAML**:

```xaml
<!-- Add to main player window -->
<Grid>
    <!-- Existing controls -->
    
    <!-- Add Tacview status in side panel -->
    <controls:TacviewStatusControl 
        DataContext="{Binding TacviewIntegration}"
        Grid.Row="0" Grid.Column="2"
        Margin="8"/>
</Grid>
```

### Step 5.4: Test UI

**Manual Testing**:
1. Start AeroDebrief
2. Check: Tacview status control appears
3. Check: Shows "Disconnected" (red) initially
4. Start Tacview with addon
5. Check: Status changes to "Connected" (green)
6. Select pilots in Tacview
7. Check: Selected pilots appear in UI
8. Check: Pan and frequency info displays correctly
9. Test: Settings panel (change port, save)
10. Test: Reconnect button

**Verification**:
- ? Status control displays correctly
- ? Connection state updates in real-time
- ? Selected pilots list updates
- ? Settings can be changed and saved
- ? UI remains responsive during connection

---

## Phase 6: Testing & Validation

**Duration**: 3-5 days  
**Goal**: Comprehensive testing and bug fixes

**Reference**: See `docs/tacview-integration/06-TESTING-STRATEGY.md`

### Step 6.1: Unit Tests

**Create tests for all components**:

```
tests/AeroDebrief.Tests/Integrations/Tacview/
??? Client/
?   ??? TacviewClientTests.cs
?   ??? ReconnectionStrategyTests.cs
??? Sync/
?   ??? TacviewSyncServiceTests.cs
?   ??? TimeConverterTests.cs
??? Pilot/
?   ??? FrequencyFilterTests.cs
?   ??? SpatialAudioCalculatorTests.cs
??? Protocol/
    ??? TacviewProtocolTests.cs
```

**Example Tests**:

```csharp
[Fact]
public async Task SyncService_HandlesLargeDrift_WithSeek()
{
    var controller = new MockPlaybackController();
    var syncService = new TacviewSyncService(controller);
    
    controller.SetCurrentPosition(TimeSpan.FromSeconds(100));
    
    var message = new TimeUpdateMessage
    {
        MissionTimeUtc = _recordingStart.AddSeconds(200).ToString("o")
    };
    
    await syncService.HandleTimeUpdate(message);
    
    // Should seek to 200 seconds (drift > 500ms)
    Assert.Equal(200, controller.CurrentPosition.TotalSeconds, 1);
}

[Fact]
public void FrequencyFilter_DefaultDisablesGeneralFrequencies()
{
    var filter = new TacviewAudioFilter();
    var selection = new TacviewPilotSelection
    {
        SelectedPilots = new List<TacviewPilot>
        {
            new() { PilotId = "Test-1", EnabledFrequencies = new List<double> { 251.0 } }
        },
        GeneralEnabledFrequencies = new List<double>() // Empty = disabled
    };
    filter.UpdateSelection(selection);
    
    // Selected pilot on enabled frequency: should play
    var packet1 = new AudioPacketMetadata(/* guid: Test-1, freq: 251.0 */);
    Assert.True(filter.ShouldPlayPacket(packet1));
    
    // Non-selected pilot: should NOT play (general frequencies disabled)
    var packet2 = new AudioPacketMetadata(/* guid: Test-2, freq: 251.0 */);
    Assert.False(filter.ShouldPlayPacket(packet2));
}
```

**Coverage Goal**: >80% for integration layer

### Step 6.2: Integration Tests

**End-to-End Scenarios**:

```csharp
[Fact]
public async Task FullIntegration_ConnectSyncAndFilter()
{
    // Start mock Tacview server
    var mockServer = new MockTacviewServer(52001);
    await mockServer.StartAsync();
    
    // Start AeroDebrief integration
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    // Wait for connection
    await Task.Delay(1000);
    Assert.True(integrationService.IsConnected);
    
    // Send time update from Tacview
    mockServer.SendTimeUpdate(DateTime.UtcNow, "playing", 1.0);
    
    // Verify playback controller received update
    await Task.Delay(100);
    Assert.True(_playbackController.IsPlaying);
    
    // Send pilot selection
    mockServer.SendPilotSelection(new[]
    {
        new TacviewPilot { PilotId = "Test-1", EnabledFrequencies = new[] { 251.0 } }
    });
    
    // Verify filter updated
    await Task.Delay(100);
    Assert.False(_audioFilter.ShouldPlayPacket(CreatePacket("Test-2", 251.0)));
}
```

### Step 6.3: Long-Duration Stability Test

**2+ Hour Test**:

```csharp
[Fact(Timeout = 7_200_000)] // 2 hours
public async Task LongDuration_MaintainsSyncAccuracy()
{
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    var startTime = DateTime.UtcNow;
    var driftSamples = new List<int>();
    
    while ((DateTime.UtcNow - startTime).TotalHours < 2)
    {
        // Check drift every 10 seconds
        await Task.Delay(10_000);
        
        var drift = Math.Abs((_playbackController.CurrentPosition - 
                             _syncService.TargetPosition).TotalMilliseconds);
        driftSamples.Add((int)drift);
        
        // Assert: 99.9% of samples within ±1 second
    }
    
    var within1Second = driftSamples.Count(d => d <= 1000);
    var accuracy = (double)within1Second / driftSamples.Count;
    
    Assert.True(accuracy >= 0.999, $"Accuracy: {accuracy:P2}");
}
```

### Step 6.4: Performance Testing

**CPU Overhead Test**:

```csharp
[Fact]
public async Task Performance_CpuOverheadUnder5Percent()
{
    var baselineCpu = GetCpuUsage();
    
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    // Run for 5 minutes
    await Task.Delay(TimeSpan.FromMinutes(5));
    
    var withIntegrationCpu = GetCpuUsage();
    
    var overhead = withIntegrationCpu - baselineCpu;
    Assert.True(overhead < 5.0, $"CPU overhead: {overhead:F2}%");
}
```

### Step 6.5: Manual Testing Checklist

**Connection**:
- [ ] Can connect to Tacview
- [ ] Auto-connect works on startup
- [ ] Auto-reconnect works after disconnect
- [ ] Manual reconnect button works
- [ ] Connection status updates correctly

**Time Synchronization**:
- [ ] Playback starts/stops with Tacview
- [ ] Seek in Tacview syncs AeroDebrief
- [ ] Drift stays within ±1 second over 2 hours
- [ ] Speed adjustment works for small drift
- [ ] Immediate seek works for large drift

**Pilot Filtering**:
- [ ] Selecting pilots filters audio correctly
- [ ] Deselecting pilots updates filter
- [ ] Multiple pilots work simultaneously
- [ ] Frequency filtering works per-pilot
- [ ] General frequency filtering works (default: disabled)

**Spatial Audio**:
- [ ] Pan applies to audio output
- [ ] Auto pan distributes pilots evenly
- [ ] Manual pan applies custom values
- [ ] Pan changes in Tacview

````````

This is the description of what the code block changes:
Add bidirectional configuration reference to implementation guide

This is the code block that represents the suggested code change:

````````markdown
| Document | Purpose |
|----------|---------|
| `00-INTEGRATION-OVERVIEW.md` | Architecture and overview |
| `01-LUA-ADDON-SPECIFICATION.md` | Complete Lua addon code |
| `02-PROTOCOL-SPECIFICATION.md` | JSON protocol messages |
| `03-INTEGRATION-PROJECT-DESIGN.md` | C# integration layer design |
| `04-UI-DESIGN.md` | WPF UI components |
| `05-CORE-MODIFICATIONS.md` | Core system changes |
| `06-TESTING-STRATEGY.md` | Test plans and strategies |
| `07-TACVIEW-MENU-GUIDE.md` | Tacview menu configuration |
| `08-FREQUENCY-FILTERING-FEATURE.md` | Frequency filtering implementation |
| `09-FREQUENCY-FILTERING-USER-GUIDE.md` | Frequency filtering user guide |
| `10-DEFAULT-BEHAVIOR-CHANGE.md` | General frequency default behavior |
| `11-TCP-PORT-CONFIGURATION-GUIDE.md` | Port configuration guide |
| `12-TCP-PORT-CONFIGURATION-SUMMARY.md` | Port configuration summary |
| `13-BIDIRECTIONAL-CONFIGURATION.md` | **Bidirectional configuration feature** (NEW) |

````````


# Response
````````markdown
# Tacview Integration - Complete Implementation Guide

## Document Overview

This is the **master implementation guide** for integrating AeroDebrief with Tacview. Follow this document sequentially to build the complete integration system.

**Status**: ?? Implementation Ready  
**Version**: 1.0  
**Estimated Effort**: 4-5 weeks  
**Target**: .NET 9, Lua 5.1+

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Architecture Overview](#architecture-overview)
3. [Phase 1: Project Setup](#phase-1-project-setup)
4. [Phase 2: Tacview Lua Addon](#phase-2-tacview-lua-addon)
5. [Phase 3: C# Integration Layer](#phase-3-c-integration-layer)
6. [Phase 4: Core Modifications](#phase-4-core-modifications)
7. [Phase 5: UI Implementation](#phase-5-ui-implementation)
8. [Phase 6: Testing & Validation](#phase-6-testing--validation)
9. [Phase 7: Documentation & Deployment](#phase-7-documentation--deployment)
10. [Reference Documentation](#reference-documentation)

---

## Prerequisites

### Development Environment

**Required**:
- ? Visual Studio 2022 (17.8+) or VS Code with C# DevKit
- ? .NET 9 SDK
- ? Tacview Advanced/Enterprise (for addon testing)
- ? Lua 5.1+ (included with Tacview)
- ? Git for version control

**Recommended**:
- Tacview SDK documentation access
- LuaSocket library (if not included with Tacview)
- Network debugging tool (Wireshark, Fiddler)

### Knowledge Requirements

**Essential**:
- C# async/await patterns
- WPF/XAML development
- TCP/IP networking
- JSON serialization

**Helpful**:
- Lua scripting basics
- Audio processing concepts
- Real-time synchronization algorithms

### Existing Codebase Familiarity

Read these before starting:
- `docs/Technical-Architecture.md` - Core system architecture
- `docs/CENTRALIZED-EVENT-SYSTEM.md` - Event system patterns
- `src/AeroDebrief.Core/Playback/PlaybackController.cs` - Playback engine
- `src/AeroDebrief.Core/Audio/AudioOutputEngine.cs` - Audio output

---

## Architecture Overview

### System Components

```
???????????????????????????????????????????????????????????????
? TACVIEW (Master - Point of Control)                         ?
? ??????????????????????????????????????????????????????????? ?
? ? Lua Addon (TCP Server on localhost:52001)              ? ?
? ? • Time updates (10 Hz)                                 ? ?
? ? • Pilot selection events                               ? ?
? ? • Frequency filters                                    ? ?
? ? • Pan configuration                                    ? ?
? ??????????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????????
                       ? JSON over TCP
???????????????????????????????????????????????????????????????
? AERODEBRIEF (Slave - Follower)                              ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.Integrations (New Project)                 ? ?
? ? ??????????????????????????????????????????????????????? ? ?
? ? ? TacviewClient - TCP client                         ? ? ?
? ? ? TacviewSyncService - Sync engine                   ? ? ?
? ? ? TacviewProtocol - Message handlers                 ? ? ?
? ? ? TacviewConfiguration - Settings                    ? ? ?
? ? ??????????????????????????????????????????????????????? ? ?
? ??????????????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.UI (Modifications)                         ? ?
? ? • TacviewStatusControl - Status display               ? ?
? ? • TacviewIntegrationViewModel - MVVM                  ? ?
? ? • Settings panel with port config                     ? ?
? ??????????????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????????????? ?
? ? AeroDebrief.Core (Enhancements)                        ? ?
? ? • PlaybackController - External sync                  ? ?
? ? • AudioOutputEngine - Spatial audio (pan)             ? ?
? ? • FrequencyChannelMixer - Frequency filtering         ? ?
? ??????????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????????
```

### Key Principles

1. **Tacview is Point of Control (POC)**: All sync commands originate from Tacview
2. **UTC Time Only**: Single time format, no timezone conversions
3. **Localhost Only**: Security by binding to 127.0.0.1
4. **Event-Driven**: Loose coupling via existing event system
5. **Backward Compatible**: Works without Tacview (feature flag)
6. **Bidirectional Configuration** (NEW): Configuration changes can flow both directions:
   - **Tacview ? AeroDebrief**: Pan mode, frequency filters (primary)
   - **AeroDebrief ? Tacview**: Configuration overrides (secondary)

---

## Phase 1: Project Setup

**Duration**: 1-2 days  
**Goal**: Create project structure and dependencies

### Step 1.1: Create Integration Project

```bash
cd src
dotnet new classlib -n AeroDebrief.Integrations -f net9.0
dotnet sln ../../AeroDebrief.sln add AeroDebrief.Integrations/AeroDebrief.Integrations.csproj
```

### Step 1.2: Add Dependencies

```bash
cd AeroDebrief.Integrations
dotnet add package System.Text.Json --version 9.0.0
dotnet add package NLog --version 5.3.2
dotnet add reference ../AeroDebrief.Core/AeroDebrief.Core.csproj
```

### Step 1.3: Create Folder Structure

```
src/AeroDebrief.Integrations/
??? Tacview/
?   ??? Client/
?   ?   ??? TacviewClient.cs
?   ?   ??? TacviewConnection.cs
?   ?   ??? TacviewReconnectionStrategy.cs
?   ??? Sync/
?   ?   ??? TacviewSyncService.cs
?   ?   ??? SyncAlgorithm.cs
?   ?   ??? TimeConverter.cs
?   ?   ??? SyncHealthMonitor.cs
?   ??? Pilot/
?   ?   ??? PilotMapper.cs
?   ?   ??? PilotFilter.cs
?   ?   ??? FrequencyFilter.cs
?   ?   ??? SpatialAudioCalculator.cs
?   ??? Protocol/
?   ?   ??? TacviewProtocol.cs
?   ?   ??? Messages/
?   ?       ??? TimeUpdateMessage.cs
?   ?       ??? PilotSelectionMessage.cs
?   ?       ??? PlaybackCommandMessage.cs
?   ?       ??? SeekMessage.cs
?   ?       ??? SpeakingStatusMessage.cs
?   ?       ??? SyncStatusMessage.cs
?   ?       ??? ReadyMessage.cs
?   ??? Models/
?       ??? TacviewConfiguration.cs
?       ??? TacviewPilot.cs
?       ??? ConnectionState.cs
?       ??? SyncQuality.cs
??? Placeholder.cs (delete this)
```

### Step 1.4: Create Lua Addon Structure

```
External/Tacview/Addons/AeroDebriefSync/
??? main.lua
??? config.lua
??? tcp_server.lua
??? protocol.lua
??? state_manager.lua
??? pilot_extractor.lua
??? visual_effects.lua
??? pan_manager.lua
??? menu_ui.lua
??? utils.lua
??? manifest.txt
```

**Verification**: Project compiles successfully with no errors.

---

## Phase 2: Tacview Lua Addon

**Duration**: 3-4 days  
**Goal**: Working TCP server in Tacview that sends messages

**Reference**: See `docs/tacview-integration/01-LUA-ADDON-SPECIFICATION.md`

### Step 2.1: Create Manifest File

**File**: `External/Tacview/Addons/AeroDebriefSync/manifest.txt`

```ini
[Addon]
Title=AeroDebrief Voice Sync
Author=AeroDebrief Team
Version=1.0.0
MinimumTacviewVersion=1.9.0
Description=Synchronizes AeroDebrief voice recordings with Tacview mission replay

[Settings]
DefaultPort=52001
UpdateRate=10
EnableLogging=true
```

### Step 2.2: Implement Configuration System

**File**: `config.lua`

Copy implementation from `01-LUA-ADDON-SPECIFICATION.md` ? Configuration File section.

**Key Features**:
- Load/save config.txt
- Port configuration (default: 52001)
- Update rate (default: 10 Hz)
- Auto-reconnect settings

### Step 2.3: Implement TCP Server

**File**: `tcp_server.lua`

**Requirements**:
- Use LuaSocket library
- Bind to 127.0.0.1 (localhost only)
- Support multiple concurrent clients
- Newline-delimited JSON messages
- Non-blocking I/O

**Key Functions**:
```lua
function TcpServer.Start(port, bindAddress)
function TcpServer.Stop()
function TcpServer.Update()  -- Call from OnUpdate
function TcpServer.Broadcast(message)
function TcpServer.SetMessageHandler(handler)
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? tcp_server.lua

### Step 2.4: Implement Protocol Handler

**File**: `protocol.lua`

**Key Functions**:
```lua
function Protocol.Encode(message)  -- Table ? JSON string
function Protocol.Decode(messageStr)  -- JSON string ? Table
function Protocol.CreateTimeUpdate(missionTime, playbackState, playbackSpeed)
function Protocol.CreatePilotSelection(pilots)
function Protocol.CreatePlaybackCommand(command)
function Protocol.CreateSeek(targetTime)
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION-PROTOCOL.md`

### Step 2.5: Implement State Manager

**File**: `state_manager.lua`

Track state changes to avoid spamming updates:
```lua
function StateManager.HasStateChanged(time, state, speed)
function StateManager.UpdateState(time, state, speed)
```

### Step 2.6: Implement Pilot Extractor

**File**: `pilot_extractor.lua`

Extract pilot info from Tacview objects:
```lua
function PilotExtractor.ExtractPilotInfo(objectId)
-- Returns: { pilot_id, pilot_name, coalition, unit_type, frequencies }
```

### Step 2.7: Implement Pan Manager

**File**: `pan_manager.lua`

**Key Features**:
- Auto/Manual pan modes
- Per-pilot pan settings
- Frequency filtering (per-pilot and general)

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? pan_manager.lua

**Important**: General frequencies disabled by default (see `10-DEFAULT-BEHAVIOR-CHANGE.md`)

### Step 2.8: Implement Menu UI

**File**: `menu_ui.lua`

**Menu Structure**:
```
AeroDebrief Sync
??? Configure Audio Pan...
??? Auto Pan Mode
??? Manual Pan Mode
??? ?????????????????????????????
??? Configure Pilot Frequencies...
??? Configure General Frequencies...
??? ?????????????????????????????
??? Settings...
?   ??? Change TCP Port...
?   ??? Change Update Rate...
?   ??? Save Settings
??? ?????????????????????????????
??? About
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? menu_ui.lua

### Step 2.9: Implement Main Entry Point

**File**: `main.lua`

**Lifecycle**:
```lua
function AeroDebriefSync:OnInitialize()
    config.Initialize()
    tcpServer.Start(config.Port, config.BindAddress)
    panManager.Initialize()
    menuUI.RegisterMenu(self)
    -- Register event listeners
    -- Set message handler for incoming messages from AeroDebrief
    tcpServer.SetMessageHandler(function(message)
        self:OnMessageReceived(message)
    end)
end

function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    tcpServer.Update()
    -- Check for state changes
    -- Broadcast time updates
    -- Update visual effects
end

function AeroDebriefSync:OnPlaybackStateChange(isPlaying)
function AeroDebriefSync:OnSelectionChange()

-- NEW: Handle incoming configuration updates from AeroDebrief
function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if decoded.type == "frequency_filter_update" then
        -- Update frequency filter from AeroDebrief
        if decoded.pilot_id then
            -- Per-pilot frequency update
            local frequencies = decoded.enabled_frequencies or {}
            for _, freq in ipairs(frequencies) do
                panManager.SetPilotFrequencyEnabled(decoded.pilot_id, freq, true)
            end
            -- Disable frequencies not in list
            local allFreqs = panManager.GetAllFrequencies()
            for _, freq in ipairs(allFreqs) do
                if not contains(frequencies, freq) then
                    panManager.SetPilotFrequencyEnabled(decoded.pilot_id, freq, false)
                end
            end
        else
            -- General frequency update
            local frequencies = decoded.enabled_frequencies or {}
            for _, freq in ipairs(frequencies) do
                panManager.SetGeneralFrequencyEnabled(freq, true)
            end
            -- Disable frequencies not in list
            local allFreqs = panManager.GetAllFrequencies()
            for _, freq in ipairs(allFreqs) do
                if not contains(frequencies, freq) then
                    panManager.SetGeneralFrequencyEnabled(freq, false)
                end
            end
        end
        
        -- Trigger selection update to broadcast new state
        self:OnSelectionChange()
        
    elseif decoded.type == "pan_configuration" then
        -- Update pan configuration from AeroDebrief
        panManager.SetMode(decoded.pan_mode or "auto")
        
        if decoded.pilot_pan_settings then
            for pilotId, panValue in pairs(decoded.pilot_pan_settings) do
                panManager.SetPilotPan(pilotId, panValue)
            end
        end
        
        -- Trigger selection update to broadcast new state
        self:OnSelectionChange()
        
        Tacview.Log.Info(string.format("Pan configuration updated from AeroDebrief: %s mode", decoded.pan_mode))
    end
end

function AeroDebriefSync:OnShutdown()
    config.Save()
    tcpServer.Stop()
end

-- Helper function
local function contains(table, value)
    for _, v in ipairs(table) do
        if v == value then
            return true
        end
    end
    return false
end
```

**Reference**: See `01-LUA-ADDON-SPECIFICATION.md` ? main.lua

### Step 2.10: Install & Test Addon

**Installation**:
1. Copy `AeroDebriefSync/` folder to: `%APPDATA%\Tacview\AddOns\`
2. Restart Tacview
3. Check: `Help ? Show Log` for "AeroDebrief Sync: Initialized successfully"

**Testing**:
1. Open Tacview with a mission replay
2. Check: Menu item "AeroDebrief Sync" appears
3. Test: Settings ? Change TCP Port (should save/load)
4. Test: Select aircraft ? Should see "pilot_selection" in log
5. Test: Play/pause ? Should see "playback_command" in log

**Verification**:
- ? Addon loads without errors
- ? TCP server starts on configured port
- ? Menu appears with all items
- ? Messages logged when events occur

---

## Phase 3: C# Integration Layer

**Duration**: 5-7 days  
**Goal**: Connect to Tacview, parse messages, sync time

**Reference**: See `docs/tacview-integration/03-INTEGRATION-PROJECT-DESIGN.md`

### Step 3.1: Create Configuration Model

**File**: `src/AeroDebrief.Integrations/Tacview/Models/TacviewConfiguration.cs`

```csharp
public class TacviewConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 52001;
    public bool AutoConnect { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int ReconnectIntervalSeconds { get; set; } = 5;
    public bool EnableSyncDriftCorrection { get; set; } = true;
    public int MaxAcceptableDriftMs { get; set; } = 500;
    public bool EnableSpatialAudio { get; set; } = true;
    public bool EnableFrequencyFiltering { get; set; } = true;
    public int ConnectionTimeoutMs { get; set; } = 10000;
    public int ReceiveTimeoutMs { get; set; } = 5000;
    
    public bool Validate(out string? error) { /* ... */ }
    public void SaveToFile(string filePath) { /* ... */ }
    public static TacviewConfiguration LoadFromFile(string filePath) { /* ... */ }
}
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewConfiguration

### Step 3.2: Create Protocol Message Models

**Files**: `Protocol/Messages/*.cs`

Create classes for all message types:
- `TimeUpdateMessage.cs`
- `PilotSelectionMessage.cs`
- `PlaybackCommandMessage.cs`
- `SeekMessage.cs`
- `SpeakingStatusMessage.cs`
- `SyncStatusMessage.cs`
- `ReadyMessage.cs`
- **NEW**: `ConfigurationUpdateMessage.cs` (AeroDebrief ? Tacview)
- **NEW**: `PanConfigurationMessage.cs` (AeroDebrief ? Tacview)
- **NEW**: `FrequencyFilterUpdateMessage.cs` (AeroDebrief ? Tacview)

**Example** (`TimeUpdateMessage.cs`):
```csharp
public class TimeUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "time_update";
    
    [JsonPropertyName("mission_time_utc")]
    public string MissionTimeUtc { get; set; } = string.Empty;
    
    [JsonPropertyName("playback_state")]
    public string PlaybackState { get; set; } = string.Empty;
    
    [JsonPropertyName("playback_speed")]
    public double PlaybackSpeed { get; set; }
}
```

**NEW** (`FrequencyFilterUpdateMessage.cs`):
```csharp
public class FrequencyFilterUpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "frequency_filter_update";
    
    [JsonPropertyName("pilot_id")]
    public string? PilotId { get; set; // null = general frequencies
    
    [JsonPropertyName("enabled_frequencies")]
    public List<double> EnabledFrequencies { get; set; } = new();
}
```

**NEW** (`PanConfigurationMessage.cs`):
```csharp
public class PanConfigurationMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pan_configuration";
    
    [JsonPropertyName("pan_mode")]
    public string PanMode { get; set; } = "auto"; // "auto" or "manual"
    
    [JsonPropertyName("pilot_pan_settings")]
    public Dictionary<string, double>? PilotPanSettings { get; set; // pilot_id -> pan value
}
```

**Reference**: See `02-PROTOCOL-SPECIFICATION.md` for all message formats

### Step 3.3: Implement TCP Client

**File**: `Client/TacviewClient.cs`

**Key Features**:
- Async TCP connection
- Message receive loop (newline-delimited JSON)
- Message send with queue
- Disconnection detection
- Events for connection state changes

**Key Methods**:
```csharp
public async Task ConnectAsync(CancellationToken cancellationToken)
public async Task DisconnectAsync()
public async Task SendMessageAsync(object message, CancellationToken cancellationToken)
private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)

public event EventHandler<TacviewMessage>? MessageReceived;
public event EventHandler? Connected;
public event EventHandler<string>? Disconnected;
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewClient

### Step 3.4: Implement Protocol Handler

**File**: `Protocol/TacviewProtocol.cs`

**Key Features**:
- Parse JSON messages by type
- Serialize outgoing messages
- Error handling and logging

```csharp
public static object? ParseMessage(string json)
{
    // Deserialize to base object, check "type" field
    // Deserialize to specific message type
    // Return typed message
}

public static string SerializeMessage(object message)
{
    // Serialize to JSON
    // Add newline delimiter
}
```

### Step 3.5: Implement Time Synchronization

**File**: `Sync/TacviewSyncService.cs`

**Sync Algorithm** (see `02-PROTOCOL-SPECIFICATION.md` ? Time Synchronization):

```csharp
public class TacviewSyncService
{
    private readonly PlaybackController _playbackController;
    private DateTime _recordingStartUtc;
    
    public async Task HandleTimeUpdate(TimeUpdateMessage message)
    {
        // Parse Tacview time to UTC
        var tacviewTime = DateTime.Parse(message.MissionTimeUtc, null, DateTimeStyles.RoundtripKind);
        
        // Convert to offset from recording start
        var targetOffset = tacviewTime - _recordingStartUtc;
        
        // Get current playback position
        var currentOffset = _playbackController.CurrentPosition;
        
        // Calculate drift
        var drift = Math.Abs((targetOffset - currentOffset).TotalMilliseconds);
        
        // Apply correction
        if (drift > 500)
        {
            // Large drift: immediate seek
            await _playbackController.SeekAsync(targetOffset);
        }
        else if (drift > 100)
        {
            // Medium drift: speed adjustment
            var speedAdjust = drift > 0 ? 1.02 : 0.98;
            _playbackController.SetPlaybackSpeed(speedAdjust);
        }
        else
        {
            // Small drift: normal speed
            _playbackController.SetPlaybackSpeed(1.0);
        }
        
        // Update playback state
        if (message.PlaybackState == "playing" && !_playbackController.IsPlaying)
        {
            await _playbackController.PlayAsync();
        }
        else if (message.PlaybackState == "paused" && _playbackController.IsPlaying)
        {
            _playbackController.Pause();
        }
    }
}
```

**Reference**: See `03-INTEGRATION-PROJECT-DESIGN.md` ? TacviewSyncService

### Step 3.6: Implement Pilot Filter

**File**: `Pilot/FrequencyFilter.cs`

**Frequency Filtering** (see `08-FREQUENCY-FILTERING-FEATURE.md`):

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
            if (selectedPilot.EnabledFrequencies == null || !selectedPilot.EnabledFrequencies.Any())
                return true;
            
            return selectedPilot.EnabledFrequencies.Contains(packet.Frequency);
        }
        else
        {
            // Non-selected pilot - check general enabled frequencies
            // Default: all DISABLED (empty list) - no audio from non-selected pilots
            if (_currentSelection.GeneralEnabledFrequencies == null || 
                !_currentSelection.GeneralEnabledFrequencies.Any())
                return false;
            
            return _currentSelection.GeneralEnabledFrequencies.Contains(packet.Frequency);
        }
    }
    
    public void UpdateSelection(TacviewPilotSelection selection)
    {
        _currentSelection = selection;
    }
}
```

### Step 3.7: Implement Connection Manager

**File**: `Client/TacviewReconnectionStrategy.cs`

**Auto-Reconnection**:
- Exponential backoff
- Configurable max attempts
- Event notifications

```csharp
public class TacviewReconnectionStrategy
{
    public async Task<bool> TryReconnectAsync(TacviewClient client, TacviewConfiguration config)
    {
        int attempt = 0;
        while (attempt < config.MaxReconnectAttempts)
        {
            attempt++;
            var delay = Math.Min(config.ReconnectIntervalSeconds * attempt, 30);
            
            Logger.Info($"Reconnection attempt {attempt}/{config.MaxReconnectAttempts} in {delay}s");
            await Task.Delay(TimeSpan.FromSeconds(delay));
            
            try
            {
                await client.ConnectAsync(CancellationToken.None);
                Logger.Info("Reconnection successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Reconnection attempt {attempt} failed");
            }
        }
        
        return false;
    }
}
```

### Step 3.8: Create Integration Service

**File**: `Tacview/TacviewIntegrationService.cs`

**Orchestrator** that coordinates all components:

```csharp
public class TacviewIntegrationService
{
    private readonly TacviewClient _client;
    private readonly TacviewSyncService _syncService;
    private readonly TacviewAudioFilter _audioFilter;
    private readonly TacviewConfiguration _config;
    private readonly TacviewReconnectionStrategy _reconnectionStrategy;
    
    public async Task StartAsync()
    {
        // Load configuration
        _config = TacviewConfiguration.LoadFromFile(ConfigPath);
        
        // Initialize components
        _client.MessageReceived += OnMessageReceived;
        _client.Disconnected += OnDisconnected;
        
        // Connect if auto-connect enabled
        if (_config.AutoConnect)
        {
            await ConnectAsync();
        }
    }
    
    private void OnMessageReceived(object? sender, TacviewMessage message)
    {
        // Dispatch to appropriate handler
        switch (message)
        {
            case TimeUpdateMessage timeUpdate:
                await _syncService.HandleTimeUpdate(timeUpdate);
                break;
            
            case PilotSelectionMessage pilotSelection:
                _audioFilter.UpdateSelection(pilotSelection);
                break;
            
            case PlaybackCommandMessage command:
                await _syncService.HandlePlaybackCommand(command);
                break;
            
            case SeekMessage seek:
                await _syncService.HandleSeek(seek);
                break;
        }
    }
    
    private async void OnDisconnected(object? sender, string reason)
    {
        Logger.Warn($"Disconnected from Tacview: {reason}");
        
        if (_config.AutoReconnect)
        {
            await _reconnectionStrategy.TryReconnectAsync(_client, _config);
        }
    }
}
```

### Step 3.9: Test Integration Layer

**Unit Tests** (`tests/AeroDebrief.Tests/Integrations/Tacview/*.cs`):

```csharp
[Fact]
public async Task TacviewClient_ConnectsSuccessfully()
{
    var client = new TacviewClient();
    await client.ConnectAsync("127.0.0.1", 52001, CancellationToken.None);
    Assert.True(client.IsConnected);
}

[Fact]
public void FrequencyFilter_FiltersSelectedPilot()
{
    var filter = new TacviewAudioFilter();
    var selection = new TacviewPilotSelection
    {
        SelectedPilots = new List<TacviewPilot>
        {
            new() { PilotId = "Test-1", EnabledFrequencies = new List<double> { 251.0 } }
        }
    };
    filter.UpdateSelection(selection);
    
    var packet = new AudioPacketMetadata(/* frequency: 251.0, guid: "Test-1" */);
    Assert.True(filter.ShouldPlayPacket(packet));
    
    var packet2 = new AudioPacketMetadata(/* frequency: 305.0, guid: "Test-1" */);
    Assert.False(filter.ShouldPlayPacket(packet2));
}
```

**Verification**:
- ? Can connect to Tacview TCP server
- ? Can receive and parse JSON messages
- ? Configuration loads/saves correctly
- ? Frequency filter works as expected
- ? Auto-reconnect works

---

## Phase 4: Core Modifications

**Duration**: 3-4 days  
**Goal**: Enable external sync, frequency filtering, spatial audio

**Reference**: See `docs/tacview-integration/05-CORE-MODIFICATIONS.md`

### Step 4.1: Add External Sync to PlaybackController

**File**: `src/AeroDebrief.Core/Playback/PlaybackController.cs`

**Add Interface**:
```csharp
public interface IExternalTimeSource
{
    TimeSpan CurrentTime { get; }
    bool IsPlaying { get; }
    double PlaybackSpeed { get; }
    
    event EventHandler<TimeSpan>? TimeChanged;
    event EventHandler<bool>? PlaybackStateChanged;
}
```

**Modify PlaybackController**:
```csharp
public class PlaybackController
{
    private IExternalTimeSource? _externalTimeSource;
    
    public void SetExternalTimeSource(IExternalTimeSource? source)
    {
        if (_externalTimeSource != null)
        {
            _externalTimeSource.TimeChanged -= OnExternalTimeChanged;
            _externalTimeSource.PlaybackStateChanged -= OnExternalPlaybackStateChanged;
        }
        
        _externalTimeSource = source;
        
        if (_externalTimeSource != null)
        {
            _externalTimeSource.TimeChanged += OnExternalTimeChanged;
            _externalTimeSource.PlaybackStateChanged += OnExternalPlaybackStateChanged;
        }
    }
    
    private void OnExternalTimeChanged(object? sender, TimeSpan targetTime)
    {
        // Calculate drift
        var drift = Math.Abs((targetTime - CurrentPosition).TotalMilliseconds);
        
        if (drift > 500)
        {
            // Large drift: seek
            SeekAsync(targetTime).GetAwaiter().GetResult();
        }
        else if (drift > 100)
        {
            // Medium drift: adjust speed
            var speedAdjust = targetTime > CurrentPosition ? 1.02 : 0.98;
            SetPlaybackSpeed(speedAdjust);
        }
        else
        {
            // Small drift: normal
            SetPlaybackSpeed(1.0);
        }
    }
}
```

### Step 4.2: Add Frequency Filtering to Audio Pipeline

**File**: `src/AeroDebrief.Core/Audio/FrequencyChannelMixer.cs`

**Add Filter Interface**:
```csharp
public interface IAudioPacketFilter
{
    bool ShouldPlayPacket(AudioPacketMetadata packet);
}
```

**Modify FrequencyChannelMixer**:
```csharp
public class FrequencyChannelMixer
{
    private IAudioPacketFilter? _packetFilter;
    
    public void SetPacketFilter(IAudioPacketFilter? filter)
    {
        _packetFilter = filter;
    }
    
    private void ProcessPacket(AudioPacketMetadata packet)
    {
        // Apply filter if set
        if (_packetFilter != null && !_packetFilter.ShouldPlayPacket(packet))
        {
            return; // Skip packet
        }
        
        // ... existing processing logic
    }
}
```

### Step 4.3: Add Spatial Audio (Pan) Support

**File**: `src/AeroDebrief.Core/Audio/AudioOutputEngine.cs`

**Add Pan Interface**:
```csharp
public interface ISpatialAudioProvider
{
    double GetPanForPilot(string pilotId);
}
```

**Modify AudioOutputEngine**:
```csharp
public class AudioOutputEngine
{
    private ISpatialAudioProvider? _spatialAudioProvider;
    
    public void SetSpatialAudioProvider(ISpatialAudioProvider? provider)
    {
        _spatialAudioProvider = provider;
    }
    
    private void ApplySpatialAudio(AudioPacketMetadata packet, float[] audioData)
    {
        if (_spatialAudioProvider == null)
            return; // No spatial audio
        
        var pan = _spatialAudioProvider.GetPanForPilot(packet.TransmitterGuid);
        
        // Calculate L/R gains
        var leftGain = (float)((1.0 - pan) / 2.0);
        var rightGain = (float)((1.0 + pan) / 2.0);
        
        // Apply pan to stereo output
        for (int i = 0; i < audioData.Length; i += 2)
        {
            audioData[i] *= leftGain;     // Left channel
            audioData[i + 1] *= rightGain; // Right channel
        }
    }
}
```

### Step 4.4: Wire Up Interfaces

**In** `TacviewIntegrationService`:

```csharp
public void Initialize(PlaybackController playbackController, 
                       FrequencyChannelMixer mixer, 
                       AudioOutputEngine audioEngine)
{
    // Set up external time source
    playbackController.SetExternalTimeSource(_syncService);
    
    // Set up frequency filter
    mixer.SetPacketFilter(_audioFilter);
    
    // Set up spatial audio
    audioEngine.SetSpatialAudioProvider(_audioFilter);
}
```

### Step 4.5: Test Core Modifications

**Unit Tests**:

```csharp
[Fact]
public void PlaybackController_SyncsWithExternalSource()
{
    var controller = new PlaybackController();
    var externalSource = new MockExternalTimeSource();
    
    controller.SetExternalTimeSource(externalSource);
    
    externalSource.TriggerTimeChange(TimeSpan.FromSeconds(100));
    
    Assert.InRange(controller.CurrentPosition.TotalSeconds, 99, 101);
}

[Fact]
public void FrequencyChannelMixer_AppliesFilter()
{
    var mixer = new FrequencyChannelMixer();
    var filter = new MockPacketFilter(shouldAllow: false);
    
    mixer.SetPacketFilter(filter);
    
    // Process packet - should be filtered out
    // Verify packet was not added to output
}
```

**Verification**:
- ? External sync works without breaking normal playback
- ? Frequency filter correctly blocks packets
- ? Spatial audio applies pan correctly
- ? All existing tests still pass

---

## Phase 5: UI Implementation

**Duration**: 3-4 days  
**Goal**: Display connection status, sync quality, settings

**Reference**: See `docs/tacview-integration/04-UI-DESIGN.md`

### Step 5.1: Create View Model

**File**: `src/AeroDebrief.UI/ViewModels/TacviewIntegrationViewModel.cs`

```csharp
public class TacviewIntegrationViewModel : ObservableObject
{
    private readonly TacviewIntegrationService _integrationService;
    
    // Connection state
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string _statusMessage = "Disconnected";
    private Brush _statusColor = Brushes.Red;
    
    // Sync quality
    private int _syncDriftMs;
    private double _syncQualityPercent;
    
    // Selected pilots
    private ObservableCollection<TacviewPilotViewModel> _selectedPilots = new();
    
    // Configuration
    private TacviewConfiguration _config;
    
    // Commands
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    
    // NEW: Configuration update commands
    public ICommand UpdatePilotFrequenciesCommand { get; }
    public ICommand UpdateGeneralFrequenciesCommand { get; }
    public ICommand UpdatePanConfigurationCommand { get; }
    public ICommand ToggleFrequencyCommand { get; }
    
    public TacviewIntegrationViewModel(TacviewIntegrationService integrationService)
    {
        _integrationService = integrationService;
        
        // Subscribe to events
        _integrationService.ConnectionStateChanged += OnConnectionStateChanged;
        _integrationService.SyncQualityChanged += OnSyncQualityChanged;
        _integrationService.PilotSelectionChanged += OnPilotSelectionChanged;
        
        // Load configuration
        _config = TacviewConfiguration.LoadFromFile(ConfigPath);
        
        // Initialize commands
        UpdatePilotFrequenciesCommand = new RelayCommand<TacviewPilotViewModel>(
            pilot => UpdatePilotFrequencies(pilot),
            pilot => pilot != null && IsConnected);
        
        UpdateGeneralFrequenciesCommand = new RelayCommand(
            () => UpdateGeneralFrequencies(),
            () => IsConnected);
        
        UpdatePanConfigurationCommand = new RelayCommand(
            () => UpdatePanConfiguration(),
            () => IsConnected);
        
        ToggleFrequencyCommand = new RelayCommand<(TacviewPilotViewModel pilot, double frequency)>(
            param => ToggleFrequency(param.pilot, param.frequency),
            param => IsConnected);
    }
    
    // NEW: Send pilot frequency update to Tacview
    private async void UpdatePilotFrequencies(TacviewPilotViewModel pilot)
    {
        try
        {
            var message = new FrequencyFilterUpdateMessage
            {
                PilotId = pilot.PilotId,
                EnabledFrequencies = pilot.EnabledFrequencies.ToList()
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info($"Sent pilot frequency update to Tacview: {pilot.PilotName}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send pilot frequency update");
            MessageBox.Show($"Failed to update frequencies in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Send general frequency update to Tacview
    private async void UpdateGeneralFrequencies()
    {
        try
        {
            var message = new FrequencyFilterUpdateMessage
            {
                PilotId = null, // null = general frequencies
                EnabledFrequencies = GeneralEnabledFrequencies.ToList()
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info("Sent general frequency update to Tacview");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send general frequency update");
            MessageBox.Show($"Failed to update general frequencies in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Send pan configuration to Tacview
    private async void UpdatePanConfiguration()
    {
        try
        {
            var pilotPanSettings = new Dictionary<string, double>();
            foreach (var pilot in SelectedPilots)
            {
                pilotPanSettings[pilot.PilotId] = pilot.Pan;
            }
            
            var message = new PanConfigurationMessage
            {
                PanMode = PanMode, // "auto" or "manual"
                PilotPanSettings = pilotPanSettings
            };
            
            await _integrationService.SendMessageAsync(message);
            
            Logger.Info($"Sent pan configuration update to Tacview: {PanMode} mode");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send pan configuration update");
            MessageBox.Show($"Failed to update pan configuration in Tacview: {ex.Message}", 
                          "Tacview Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // NEW: Toggle frequency for a pilot
    private async void ToggleFrequency(TacviewPilotViewModel pilot, double frequency)
    {
        if (pilot.EnabledFrequencies.Contains(frequency))
        {
            pilot.EnabledFrequencies.Remove(frequency);
        }
        else
        {
            pilot.EnabledFrequencies.Add(frequency);
        }
        
        // Send update to Tacview
        await UpdatePilotFrequencies(pilot);
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        ConnectionState = state;
        StatusMessage = state switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Synchronized => "Synchronized",
            ConnectionState.Degraded => "Degraded",
            _ => "Disconnected"
        };
        
        StatusColor = state switch
        {
            ConnectionState.Synchronized => Brushes.Green,
            ConnectionState.Connected => Brushes.Yellow,
            ConnectionState.Degraded => Brushes.Orange,
            _ => Brushes.Red
        };
        
        // Update command can-execute state
        CommandManager.InvalidateRequerySuggested();
    }
}
```

### Step 5.2: Create Status Control

**File**: `src/AeroDebrief.UI/Controls/Tacview/TacviewStatusControl.xaml`

```xaml
<UserControl x:Class="AeroDebrief.UI.Controls.Tacview.TacviewStatusControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Style="{StaticResource PanelStyle}">
        <StackPanel>
            <!-- Header -->
            <Grid>
                <TextBlock Text="?? Tacview Integration" 
                          Style="{StaticResource HeadingStyle}"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="??" Command="{Binding OpenSettingsCommand}"
                           ToolTip="Settings"/>
                    <Button Content="??" Command="{Binding ReconnectCommand}"
                           ToolTip="Reconnect"/>
                </StackPanel>
            </Grid>
            
            <!-- Connection Status -->
            <Grid Margin="0,8,0,0">
                <Ellipse Width="12" Height="12" 
                        Fill="{Binding StatusColor}"
                        ToolTip="{Binding StatusMessage}"/>
                <TextBlock Text="{Binding StatusMessage}" Margin="20,0,0,0"/>
                <TextBlock Text="{Binding PortDisplay}" 
                          HorizontalAlignment="Right"
                          Foreground="Gray"
                          FontSize="10"/>
            </Grid>
            
            <!-- Sync Quality Bar -->
            <Border Visibility="{Binding IsSynchronized, Converter={StaticResource BoolToVisibilityConverter}}"
                   Margin="0,8,0,0">
                <Grid>
                    <TextBlock Text="Sync Quality" FontSize="10" Foreground="Gray"/>
                    <ProgressBar Value="{Binding SyncQualityPercent}" 
                                Maximum="100" Height="6"
                                Margin="0,16,0,0"/>
                    <TextBlock Text="{Binding SyncDriftDisplay}" 
                              HorizontalAlignment="Right"
                              FontSize="10" Margin="0,16,0,0"/>
                </Grid>
            </Border>
            
            <!-- NEW: Pan Configuration -->
            <Expander Header="??? Spatial Audio (Pan)" IsExpanded="False"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <StackPanel Padding="12" Spacing="8">
                    <TextBlock Text="Pan Mode:" FontWeight="SemiBold"/>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <RadioButton Content="Auto" GroupName="PanMode"
                                    IsChecked="{Binding IsAutoPan}"
                                    Command="{Binding UpdatePanConfigurationCommand}"/>
                        <RadioButton Content="Manual" GroupName="PanMode"
                                    IsChecked="{Binding IsManualPan}"
                                    Command="{Binding UpdatePanConfigurationCommand}"/>
                    </StackPanel>
                    
                    <Separator Margin="0,8"/>
                    
                    <TextBlock Text="Pilot Pan Settings:" FontWeight="SemiBold"
                              Visibility="{Binding IsManualPan, Converter={StaticResource BoolToVisibilityConverter}}"/>
                    <ItemsControl ItemsSource="{Binding SelectedPilots}"
                                 Visibility="{Binding IsManualPan, Converter={StaticResource BoolToVisibilityConverter}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Style="{StaticResource PilotItemStyle}" Margin="0,4">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="100"/>
                                        </Grid.ColumnDefinitions>
                                        
                                        <TextBlock Grid.Column="0" Text="{Binding PilotName}" 
                                                  VerticalAlignment="Center"/>
                                        
                                        <Slider Grid.Column="2" 
                                               Minimum="-1" Maximum="1" 
                                               Value="{Binding Pan, Mode=TwoWay}"
                                               TickFrequency="0.1"
                                               IsSnapToTickEnabled="True"
                                               ToolTip="Pan: -1 (left) to +1 (right)">
                                            <Slider.CommandParameter>
                                                <MultiBinding Converter="{StaticResource TupleConverter}">
                                                    <Binding Path="DataContext" RelativeSource="{RelativeSource AncestorType=UserControl}"/>
                                                    <Binding/>
                                                </MultiBinding>
                                            </Slider.CommandParameter>
                                            <Slider.Command>
                                                <Binding Path="DataContext.UpdatePanConfigurationCommand" 
                                                        RelativeSource="{RelativeSource AncestorType=UserControl}"/>
                                            </Slider.Command>
                                        </Slider>
                                        
                                        <TextBlock Grid.Column="1" 
                                                  Text="{Binding PanDisplay}"
                                                  Margin="8,0"
                                                  VerticalAlignment="Center"
                                                  FontFamily="Consolas"/>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <TextBlock Text="?? Changes sync to Tacview automatically"
                              FontSize="10" Foreground="Gray" Margin="0,8,0,0"/>
                </StackPanel>
            </Expander>
            
            <!-- NEW: Frequency Filtering -->
            <Expander Header="?? Frequency Filtering" IsExpanded="False"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <StackPanel Padding="12" Spacing="12">
                    <!-- Per-Pilot Frequencies -->
                    <TextBlock Text="Per-Pilot Frequencies:" FontWeight="SemiBold"/>
                    <ItemsControl ItemsSource="{Binding SelectedPilots}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Expander Header="{Binding PilotName}" Margin="0,4">
                                    <StackPanel Padding="8" Spacing="4">
                                        <ItemsControl ItemsSource="{Binding AllFrequencies}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <CheckBox Content="{Binding Display}"
                                                             IsChecked="{Binding IsEnabled, Mode=TwoWay}"
                                                             Command="{Binding DataContext.ToggleFrequencyCommand, 
                                                                             RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                             CommandParameter="{Binding}"/>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                        
                                        <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                                            <Button Content="? Enable All" 
                                                   Command="{Binding EnableAllFrequenciesCommand}"/>
                                            <Button Content="? Disable All" 
                                                   Command="{Binding DisableAllFrequenciesCommand}"/>
                                        </StackPanel>
                                    </StackPanel>
                                </Expander>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <Separator Margin="0,8"/>
                    
                    <!-- General Frequencies -->
                    <TextBlock Text="General Frequencies (Non-Selected Pilots):" FontWeight="SemiBold"/>
                    <TextBlock Text="Default: All disabled" FontSize="10" Foreground="Gray"/>
                    
                    <ItemsControl ItemsSource="{Binding AllKnownFrequencies}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <CheckBox Content="{Binding Display}"
                                         IsChecked="{Binding IsGeneralEnabled, Mode=TwoWay}"
                                         Command="{Binding DataContext.UpdateGeneralFrequenciesCommand, 
                                                         RelativeSource={RelativeSource AncestorType=UserControl}}"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    
                    <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                        <Button Content="? Enable All General" 
                               Command="{Binding EnableAllGeneralFrequenciesCommand}"/>
                        <Button Content="? Disable All General" 
                               Command="{Binding DisableAllGeneralFrequenciesCommand}"/>
                    </StackPanel>
                    
                    <TextBlock Text="?? Changes sync to Tacview automatically"
                              FontSize="10" Foreground="Gray" Margin="0,8,0,0"/>
                </StackPanel>
            </Expander>
            
            <!-- Selected Pilots (Read-Only Summary) -->
            <Expander Header="Selected Pilots" IsExpanded="True"
                     Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
                <ItemsControl ItemsSource="{Binding SelectedPilots}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Style="{StaticResource PilotItemStyle}">
                                <Grid>
                                    <TextBlock Text="{Binding PilotName}"/>
                                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                                        <TextBlock Text="{Binding PanDisplay}" 
                                                  Foreground="Gray" FontSize="10"/>
                                        <TextBlock Text="{Binding FrequencyDisplay}" 
                                                  Foreground="Gray" FontSize="10" Margin="8,0,0,0"/>
                                    </StackPanel>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Expander>
            
            <!-- Settings Panel -->
            <Expander Header="?? Settings">
                <StackPanel Padding="12" Spacing="8">
                    <CheckBox Content="Auto-connect on startup"
                             IsChecked="{Binding Config.AutoConnect}"/>
                    <CheckBox Content="Auto-reconnect on disconnect"
                             IsChecked="{Binding Config.AutoReconnect}"/>
                    
                    <Separator Margin="0,8"/>
                    
                    <TextBlock Text="Connection" FontWeight="SemiBold"/>
                    <Grid ColumnDefinitions="Auto,*,Auto,Auto">
                        <TextBlock Text="Host:" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding Config.Host}"/>
                        
                        <TextBlock Grid.Column="2" Text="Port:" 
                                  VerticalAlignment="Center" Margin="8,0,0,0"/>
                        <TextBox Grid.Column="3" Text="{Binding Config.Port}" Width="80"/>
                    </Grid>
                    
                    <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                        <Button Content="Test Connection" 
                               Command="{Binding TestConnectionCommand}"/>
                        <Button Content="Save Settings" 
                               Command="{Binding SaveSettingsCommand}"/>
                    </StackPanel>
                    
                    <!-- Info about configuration sync -->
                    <Border Background="{StaticResource InfoBackgroundBrush}"
                           BorderBrush="{StaticResource InfoBorderBrush}"
                           BorderThickness="1" Padding="8" CornerRadius="4"
                           Margin="0,8,0,0">
                        <StackPanel Spacing="4">
                            <TextBlock Text="?? Bidirectional Sync" 
                                      FontWeight="SemiBold" FontSize="11"/>
                            <TextBlock TextWrapping="Wrap" FontSize="10">
                                Configuration changes made in AeroDebrief are automatically synced to Tacview:
                            </TextBlock>
                            <TextBlock FontSize="10" Foreground="Gray" Margin="8,0,0,0">
                                <Run Text="• Pan mode and values"/>
                                <LineBreak/>
                                <Run Text="• Per-pilot frequency filters"/>
                                <LineBreak/>
                                <Run Text="• General frequency filters"/>
                            </TextBlock>
                            <TextBlock TextWrapping="Wrap" FontSize="10" Margin="0,4,0,0">
                                You can also configure these in Tacview's menu:
                            </TextBlock>
                            <TextBlock FontSize="10" Foreground="Gray" Margin="8,0,0,0">
                                <Run Text="• Tacview ? AeroDebrief Sync ? Configure Audio Pan"/>
                                <LineBreak/>
                                <Run Text="• Tacview ? AeroDebrief Sync ? Configure Frequencies"/>
                            </TextBlock>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Expander>
        </StackPanel>
    </Border>
</UserControl>
```

### Step 5.3: Integrate with Main Player UI

**File**: `src/AeroDebrief.UI/ViewModels/UnifiedPlayerViewModel.cs`

```csharp
public class UnifiedPlayerViewModel : ObservableObject
{
    // Add Tacview integration
    private TacviewIntegrationViewModel? _tacviewIntegration;
    
    public TacviewIntegrationViewModel? TacviewIntegration
    {
        get => _tacviewIntegration;
        set => SetProperty(ref _tacviewIntegration, value);
    }
    
    public UnifiedPlayerViewModel(/* existing params */, 
                                  TacviewIntegrationService tacviewService)
    {
        // ... existing initialization
        
        // Initialize Tacview integration
        TacviewIntegration = new TacviewIntegrationViewModel(tacviewService);
        
        // Wire up to playback controller
        tacviewService.Initialize(_playbackController, _mixer, _audioEngine);
    }
}
```

**Update Main Window XAML**:

```xaml
<!-- Add to main player window -->
<Grid>
    <!-- Existing controls -->
    
    <!-- Add Tacview status in side panel -->
    <controls:TacviewStatusControl 
        DataContext="{Binding TacviewIntegration}"
        Grid.Row="0" Grid.Column="2"
        Margin="8"/>
</Grid>
```

### Step 5.4: Test UI

**Manual Testing**:
1. Start AeroDebrief
2. Check: Tacview status control appears
3. Check: Shows "Disconnected" (red) initially
4. Start Tacview with addon
5. Check: Status changes to "Connected" (green)
6. Select pilots in Tacview
7. Check: Selected pilots appear in UI
8. Check: Pan and frequency info displays correctly
9. Test: Settings panel (change port, save)
10. Test: Reconnect button

**Verification**:
- ? Status control displays correctly
- ? Connection state updates in real-time
- ? Selected pilots list updates
- ? Settings can be changed and saved
- ? UI remains responsive during connection

---

## Phase 6: Testing & Validation

**Duration**: 3-5 days  
**Goal**: Comprehensive testing and bug fixes

**Reference**: See `docs/tacview-integration/06-TESTING-STRATEGY.md`

### Step 6.1: Unit Tests

**Create tests for all components**:

```
tests/AeroDebrief.Tests/Integrations/Tacview/
??? Client/
?   ??? TacviewClientTests.cs
?   ??? ReconnectionStrategyTests.cs
??? Sync/
?   ??? TacviewSyncServiceTests.cs
?   ??? TimeConverterTests.cs
??? Pilot/
?   ??? FrequencyFilterTests.cs
?   ??? SpatialAudioCalculatorTests.cs
??? Protocol/
    ??? TacviewProtocolTests.cs
```

**Example Tests**:

```csharp
[Fact]
public async Task SyncService_HandlesLargeDrift_WithSeek()
{
    var controller = new MockPlaybackController();
    var syncService = new TacviewSyncService(controller);
    
    controller.SetCurrentPosition(TimeSpan.FromSeconds(100));
    
    var message = new TimeUpdateMessage
    {
        MissionTimeUtc = _recordingStart.AddSeconds(200).ToString("o")
    };
    
    await syncService.HandleTimeUpdate(message);
    
    // Should seek to 200 seconds (drift > 500ms)
    Assert.Equal(200, controller.CurrentPosition.TotalSeconds, 1);
}

[Fact]
public void FrequencyFilter_DefaultDisablesGeneralFrequencies()
{
    var filter = new TacviewAudioFilter();
    var selection = new TacviewPilotSelection
    {
        SelectedPilots = new List<TacviewPilot>
        {
            new() { PilotId = "Test-1", EnabledFrequencies = new List<double> { 251.0 } }
        },
        GeneralEnabledFrequencies = new List<double>() // Empty = disabled
    };
    filter.UpdateSelection(selection);
    
    // Selected pilot on enabled frequency: should play
    var packet1 = new AudioPacketMetadata(/* guid: Test-1, freq: 251.0 */);
    Assert.True(filter.ShouldPlayPacket(packet1));
    
    // Non-selected pilot: should NOT play (general frequencies disabled)
    var packet2 = new AudioPacketMetadata(/* guid: Test-2, freq: 251.0 */);
    Assert.False(filter.ShouldPlayPacket(packet2));
}
```

**Coverage Goal**: >80% for integration layer

### Step 6.2: Integration Tests

**End-to-End Scenarios**:

```csharp
[Fact]
public async Task FullIntegration_ConnectSyncAndFilter()
{
    // Start mock Tacview server
    var mockServer = new MockTacviewServer(52001);
    await mockServer.StartAsync();
    
    // Start AeroDebrief integration
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    // Wait for connection
    await Task.Delay(1000);
    Assert.True(integrationService.IsConnected);
    
    // Send time update from Tacview
    mockServer.SendTimeUpdate(DateTime.UtcNow, "playing", 1.0);
    
    // Verify playback controller received update
    await Task.Delay(100);
    Assert.True(_playbackController.IsPlaying);
    
    // Send pilot selection
    mockServer.SendPilotSelection(new[]
    {
        new TacviewPilot { PilotId = "Test-1", EnabledFrequencies = new[] { 251.0 } }
    });
    
    // Verify filter updated
    await Task.Delay(100);
    Assert.False(_audioFilter.ShouldPlayPacket(CreatePacket("Test-2", 251.0)));
}
```

### Step 6.3: Long-Duration Stability Test

**2+ Hour Test**:

```csharp
[Fact(Timeout = 7_200_000)] // 2 hours
public async Task LongDuration_MaintainsSyncAccuracy()
{
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    var startTime = DateTime.UtcNow;
    var driftSamples = new List<int>();
    
    while ((DateTime.UtcNow - startTime).TotalHours < 2)
    {
        // Check drift every 10 seconds
        await Task.Delay(10_000);
        
        var drift = Math.Abs((_playbackController.CurrentPosition - 
                             _syncService.TargetPosition).TotalMilliseconds);
        driftSamples.Add((int)drift);
        
        // Assert: 99.9% of samples within ±1 second
    }
    
    var within1Second = driftSamples.Count(d => d <= 1000);
    var accuracy = (double)within1Second / driftSamples.Count;
    
    Assert.True(accuracy >= 0.999, $"Accuracy: {accuracy:P2}");
}
```

### Step 6.4: Performance Testing

**CPU Overhead Test**:

```csharp
[Fact]
public async Task Performance_CpuOverheadUnder5Percent()
{
    var baselineCpu = GetCpuUsage();
    
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    // Run for 5 minutes
    await Task.Delay(TimeSpan.FromMinutes(5));
    
    var withIntegrationCpu = GetCpuUsage();
    
    var overhead = withIntegrationCpu - baselineCpu;
    Assert.True(overhead < 5.0, $"CPU overhead: {overhead:F2}%");
}
```

### Step 6.5: Manual Testing Checklist

**Connection**:
- [ ] Can connect to Tacview
- [ ] Auto-connect works on startup
- [ ] Auto-reconnect works after disconnect
- [ ] Manual reconnect button works
- [ ] Connection status updates correctly

**Time Synchronization**:
- [ ] Playback starts/stops with Tacview
- [ ] Seek in Tacview syncs AeroDebrief
- [ ] Drift stays within ±1 second over 2 hours
- [ ] Speed adjustment works for small drift
- [ ] Immediate seek works for large drift

**Pilot Filtering**:
- [ ] Selecting pilots filters audio correctly
- [ ] Deselecting pilots updates filter
- [ ] Multiple pilots work simultaneously
- [ ] Frequency filtering works per-pilot
- [ ] General frequency filtering works (default: disabled)

**Spatial Audio**:
- [ ] Pan applies to audio output
- [ ] Auto pan distributes pilots evenly
- [ ] Manual pan applies custom values
- [ ] Pan changes in Tacview
