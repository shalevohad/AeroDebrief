# Tacview Lua Addon Specification

## Overview

The Tacview Lua addon acts as a **TCP server** that bridges Tacview's mission replay system with AeroDebrief. It runs inside Tacview's Lua scripting environment and provides real-time mission state updates to AeroDebrief.

## Addon Metadata

### Manifest (`manifest.txt`)
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

### Configuration File (`config.lua`)

```lua
-- Configuration settings for AeroDebrief Sync addon
local Config = {
    -- Network settings
    Port = 52001,              -- Default TCP port (can be changed in settings)
    BindAddress = "127.0.0.1", -- Localhost only for security
    
    -- Update settings
    UpdateRate = 10,           -- Hz (times per second to check for changes)
    
    -- Logging
    EnableLogging = true,
    LogLevel = "Info",         -- "Debug", "Info", "Warning", "Error"
    
    -- Reconnection settings
    EnableAutoReconnect = true,
    ReconnectDelay = 5,        -- Seconds between reconnect attempts
    MaxClients = 5,            -- Maximum concurrent AeroDebrief connections
    
    -- Visual effects
    EnableSpeakingIndicators = true,
    HighlightColor = 0x00FF00, -- Green (0xRRGGBB)
    HighlightScale = 1.5,      -- Size multiplier for speaking pilots
    
    -- File paths
    ConfigFilePath = nil,      -- Set at runtime: Tacview.AddOns.GetPath() .. "/config.txt"
}

-- Load configuration from file if exists
function Config.Load()
    if Config.ConfigFilePath and Tacview.IO.FileExists(Config.ConfigFilePath) then
        local file = io.open(Config.ConfigFilePath, "r")
        if file then
            for line in file:lines() do
                local key, value = line:match("^(%S+)%s*=%s*(.+)$")
                if key and value then
                    -- Parse value type
                    if value == "true" then
                        Config[key] = true
                    elseif value == "false" then
                        Config[key] = false
                    elseif tonumber(value) then
                        Config[key] = tonumber(value)
                    else
                        Config[key] = value
                    end
                    Tacview.Log.Info(string.format("Config loaded: %s = %s", key, tostring(Config[key])))
                end
            end
            file:close()
            Tacview.Log.Info("Configuration loaded from file")
        end
    else
        Tacview.Log.Info("No config file found, using defaults")
    end
end

-- Save configuration to file
function Config.Save()
    if Config.ConfigFilePath then
        local file = io.open(Config.ConfigFilePath, "w")
        if file then
            file:write(string.format("Port=%d\n", Config.Port))
            file:write(string.format("BindAddress=%s\n", Config.BindAddress))
            file:write(string.format("UpdateRate=%d\n", Config.UpdateRate))
            file:write(string.format("EnableLogging=%s\n", tostring(Config.EnableLogging)))
            file:write(string.format("EnableAutoReconnect=%s\n", tostring(Config.EnableAutoReconnect)))
            file:write(string.format("ReconnectDelay=%d\n", Config.ReconnectDelay))
            file:write(string.format("MaxClients=%d\n", Config.MaxClients))
            file:write(string.format("EnableSpeakingIndicators=%s\n", tostring(Config.EnableSpeakingIndicators)))
            file:write(string.format("HighlightColor=0x%06X\n", Config.HighlightColor))
            file:write(string.format("HighlightScale=%.1f\n", Config.HighlightScale))
            file:close()
            Tacview.Log.Info("Configuration saved to file")
            return true
        end
    end
    return false
end

-- Initialize config file path at addon load
function Config.Initialize()
    Config.ConfigFilePath = Tacview.AddOns.GetPath() .. "/AeroDebriefSync/config.txt"
    Config.Load()
end

return Config
```

## Addon Architecture

```
????????????????????????????????????????????????????????????????
?                      Tacview Engine                           ?
?  ??????????????????????????????????????????????????????????  ?
?  ?  Mission Replay System                                 ?  ?
?  ?  • Current time (mission time)                        ?  ?
?  ?  • Playback state (play/pause/stop)                   ?  ?
?  ?  ?  Playback speed (1x, 2x, etc.)                     ?  ?
?  ?  • Selected objects (aircraft/units)                  ?  ?
?  ??????????????????????????????????????????????????????????  ?
?                   ? Lua API                                   ?
?  ??????????????????????????????????????????????????????????  ?
?  ?  AeroDebrief Addon (main.lua)                         ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  Tacview Menu Integration                        ?  ?  ?
?  ?  ?  • Pan Configuration UI                          ?  ?  ?
?  ?  ?  • Auto/Manual Pan Mode                          ?  ?  ?
?  ?  ?  • Per-Pilot Pan Sliders                         ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  Event Handlers                                  ?  ?  ?
?  ?  ?  • OnUpdate (10 Hz)                             ?  ?  ?
?  ?  ?  • OnPlaybackStateChange                        ?  ?  ?
?  ?  ?  • OnObjectSelectionChange                      ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  State Manager                                   ?  ?  ?
?  ?  ?  • Track current time                           ?  ?  ?
?  ?  ?  • Track playback state                         ?  ?  ?
?  ?  ?  • Track selected pilots                        ?  ?  ?
?  ?  ?  • Track pan settings (manual/auto)             ?  ?  ?
?  ?  ?  • Detect changes                               ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  Pilot Info Extractor                           ?  ?  ?
?  ?  ?  • Get pilot ID                                 ?  ?  ?
?  ?  ?  • Get coalition                                ?  ?  ?
?  ?  ?  • Get radio frequencies (from properties)      ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  Visual Indicator Manager                       ?  ?  ?
?  ?  ?  • Highlight speaking pilots                    ?  ?  ?
?  ?  ?  • Change icon color/size                       ?  ?  ?
?  ?  ?  • Add visual effects                           ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ????????????????????????????????????????????????????  ?  ?
?  ?  ?  TCP Server (tcp_server.lua)                    ?  ?  ?
?  ?  ?  • Listen on port 52001                         ?  ?  ?
?  ?  ?  • Accept client connections                    ?  ?  ?
?  ?  ?  • Send JSON messages                           ?  ?  ?
?  ?  ?  • Receive JSON messages                        ?  ?  ?
?  ?  ?  • Handle disconnections                        ?  ?  ?
?  ?  ????????????????????????????????????????????????????  ?
?  ??????????????????????????????????????????????????????????  ?
????????????????????????????????????????????????????????????????
                   ? TCP (localhost:52001)
                   ?
????????????????????????????????????????????????????????????????
?                   AeroDebrief Client                          ?
????????????????????????????????????????????????????????????????
```

## File Structure

```
External/Tacview/Addons/AeroDebriefSync/
??? main.lua                    # Entry point and event handlers
??? tcp_server.lua              # TCP server implementation
??? protocol.lua                # JSON encoding/decoding
??? state_manager.lua           # State tracking and change detection
??? pilot_extractor.lua         # Extract pilot info from Tacview
??? visual_effects.lua          # Speaking indicators
??? pan_manager.lua             # NEW: Pan configuration manager
??? menu_ui.lua                 # NEW: Tacview menu integration
??? utils.lua                   # Utility functions
??? config.lua                  # Configuration settings
??? manifest.txt                # Addon metadata
```

## Core Lua Files

### 1. main.lua (Entry Point)

```lua
-- AeroDebrief Voice Sync - Main Entry Point
-- Version: 1.0.0

local tcpServer = require("tcp_server")
local stateManager = require("state_manager")
local pilotExtractor = require("pilot_extractor")
local visualEffects = require("visual_effects")
local protocol = require("protocol")
local config = require("config")
local panManager = require("pan_manager")
local menuUI = require("menu_ui")

local AeroDebriefSync = {}

-- Addon lifecycle
function AeroDebriefSync:OnInitialize()
    Tacview.Log.Info("AeroDebrief Sync: Initializing...")
    
    -- Initialize configuration
    config.Initialize()
    
    -- Start TCP server with configured port
    tcpServer.Start(config.Port, config.BindAddress)
    
    -- Initialize pan manager with default auto mode
    panManager.Initialize()
    
    -- Register Tacview menu
    menuUI.RegisterMenu(self)
    
    -- Register update callback (configured rate Hz)
    Tacview.Events.Update.RegisterListener(function(dt, absoluteTime)
        self:OnUpdate(dt, absoluteTime)
    end)
    
    -- Register playback state change
    Tacview.Events.PlaybackStateChange.RegisterListener(function(isPlaying)
        self:OnPlaybackStateChange(isPlaying)
    end)
    
    -- Register selection change
    Tacview.Events.SelectionChange.RegisterListener(function()
        self:OnSelectionChange()
    end)
    
    Tacview.Log.Info(string.format("AeroDebrief Sync: Initialized successfully on port %d", config.Port))
end

function AeroDebriefSync:OnShutdown()
    Tacview.Log.Info("AeroDebrief Sync: Shutting down...")
    tcpServer.Stop()
    visualEffects.ClearAll()
    menuUI.Unregister()
    config.Save() -- Save configuration on shutdown
end

-- Update loop (called 10 times per second)
function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    -- Update TCP server (handle incoming messages)
    tcpServer.Update()
    
    -- Get current mission time
    local missionTime = Tacview.Context.GetAbsoluteTime()
    local playbackState = Tacview.Context.GetPlaybackState()
    local playbackSpeed = Tacview.Context.GetPlaybackSpeed()
    
    -- Check if state changed
    if stateManager.HasStateChanged(missionTime, playbackState, playbackSpeed) then
        -- Send time update to AeroDebrief
        local message = protocol.CreateTimeUpdate(missionTime, playbackState, playbackSpeed)
        tcpServer.Broadcast(message)
        
        stateManager.UpdateState(missionTime, playbackState, playbackSpeed)
    end
    
    -- Update visual effects
    visualEffects.Update(dt)
end

function AeroDebriefSync:OnPlaybackStateChange(isPlaying)
    Tacview.Log.Debug("AeroDebrief Sync: Playback state changed: " .. tostring(isPlaying))
    
    local command = isPlaying and "play" or "pause"
    local message = protocol.CreatePlaybackCommand(command)
    tcpServer.Broadcast(message)
end

function AeroDebriefSync:OnSelectionChange()
    Tacview.Log.Debug("AeroDebrief Sync: Selection changed")
    
    -- Get selected objects
    local selectedObjects = Tacview.Context.GetSelectedObjects()
    
    if selectedObjects and #selectedObjects > 0 then
        local pilots = {}
        
        for i, objectId in ipairs(selectedObjects) do
            local pilotInfo = pilotExtractor.ExtractPilotInfo(objectId)
            if pilotInfo then
                -- Get pan value from pan manager (either auto or manual)
                pilotInfo.pan = panManager.GetPanForPilot(pilotInfo.pilot_id, i, #selectedObjects)
                table.insert(pilots, pilotInfo)
            end
        end
        
        if #pilots > 0 then
            local message = protocol.CreatePilotSelection(pilots)
            tcpServer.Broadcast(message)
        end
    else
        -- No selection - send empty list
        local message = protocol.CreatePilotSelection({})
        tcpServer.Broadcast(message)
    end
end

-- Handle messages from AeroDebrief
function AeroDebriefSync:OnMessageReceived(message)
    local msgType = message.type
    
    if msgType == "speaking_status" then
        -- Pilot started/stopped speaking
        local pilotId = message.pilot_id
        local isSpeaking = message.is_speaking
        
        if isSpeaking then
            visualEffects.HighlightPilot(pilotId)
        else
            visualEffects.UnhighlightPilot(pilotId)
        end
        
    elseif msgType == "sync_status" then
        -- AeroDebrief sync status update
        Tacview.Log.Debug(string.format("AeroDebrief sync drift: %d ms", message.sync_drift_ms))
        
    elseif msgType == "ready" then
        -- AeroDebrief is ready
        Tacview.Log.Info("AeroDebrief is ready and connected")
        
        -- Send current state immediately
        self:OnUpdate(0, 0)
        self:OnSelectionChange()
    end
end

-- Register message handler
tcpServer.SetMessageHandler(function(message)
    AeroDebriefSync:OnMessageReceived(message)
end)

-- Initialize addon
Tacview.AddOn.Initialize(function()
    AeroDebriefSync:OnInitialize()
end)

-- Shutdown addon
Tacview.AddOn.Shutdown(function()
    AeroDebriefSync:OnShutdown()
end)

return AeroDebriefSync
```

### NEW: 8. pan_manager.lua (Pan Configuration)

```lua
-- Pan Manager - Handles spatial audio pan configuration and frequency filtering
local PanManager = {
    mode = "auto", -- "auto" or "manual"
    manualPanSettings = {}, -- pilot_id -> pan value (-1.0 to +1.0)
    currentPilots = {}, -- Track currently selected pilots
    
    -- NEW: Frequency filtering
    pilotFrequencyFilter = {}, -- pilot_id -> { [frequency] = boolean }
    generalFrequencyFilter = {}, -- [frequency] = boolean (for non-selected pilots)
    allFrequencies = {} -- Set of all known frequencies
}

function PanManager.Initialize()
    PanManager.mode = "auto"
    PanManager.manualPanSettings = {}
    PanManager.currentPilots = {}
    PanManager.pilotFrequencyFilter = {}
    PanManager.generalFrequencyFilter = {}
    PanManager.allFrequencies = {}
    Tacview.Log.Info("Pan Manager initialized in auto mode")
end

-- Get pan value for a pilot
-- pilotIndex: 1-based index in selection list
-- totalPilots: total number of selected pilots
function PanManager.GetPanForPilot(pilotId, pilotIndex, totalPilots)
    if PanManager.mode == "manual" then
        -- Return manual setting if exists
        if PanManager.manualPanSettings[pilotId] then
            return PanManager.manualPanSettings[pilotId]
        end
    end
    
    -- Auto mode: distribute evenly across stereo field
    if totalPilots == 1 then
        return 0.0 -- Center
    end
    
    local panStep = 2.0 / math.max(totalPilots - 1, 1)
    return -1.0 + (pilotIndex - 1) * panStep
end

-- Set pan mode
function PanManager.SetMode(mode)
    if mode ~= "auto" and mode ~= "manual" then
        Tacview.Log.Warning("Invalid pan mode: " .. tostring(mode))
        return
    end
    
    PanManager.mode = mode
    Tacview.Log.Info("Pan mode set to: " .. mode)
end

-- Get current mode
function PanManager.GetMode()
    return PanManager.mode
end

-- Set manual pan for a specific pilot
function PanManager.SetManualPan(pilotId, panValue)
    panValue = math.max(-1.0, math.min(1.0, panValue)) -- Clamp to valid range
    PanManager.manualPanSettings[pilotId] = panValue
    Tacview.Log.Debug(string.format("Manual pan set for %s: %.2f", pilotId, panValue))
end

-- Get manual pan setting for a pilot
function PanManager.GetManualPan(pilotId)
    return PanManager.manualPanSettings[pilotId] or 0.0
end

-- Clear all manual pan settings
function PanManager.ClearManualPans()
    PanManager.manualPanSettings = {}
    Tacview.Log.Info("All manual pan settings cleared")
end

-- Update current pilots list
function PanManager.UpdateCurrentPilots(pilots)
    PanManager.currentPilots = pilots
    
    -- Initialize frequency filters for new pilots if not exists
    for _, pilot in ipairs(pilots) do
        if not PanManager.pilotFrequencyFilter[pilot.pilot_id] then
            PanManager.pilotFrequencyFilter[pilot.pilot_id] = {}
        end
        
        -- Add frequencies to global set
        if pilot.frequencies then
            for _, freq in ipairs(pilot.frequencies) do
                PanManager.allFrequencies[freq] = true
                
                -- Enable frequency by default for selected pilots
                if PanManager.pilotFrequencyFilter[pilot.pilot_id][freq] == nil then
                    PanManager.pilotFrequencyFilter[pilot.pilot_id][freq] = true
                end
                
                -- CHANGED: General frequencies disabled by default (not selected)
                if PanManager.generalFrequencyFilter[freq] == nil then
                    PanManager.generalFrequencyFilter[freq] = false
                end
            end
        end
    end
end

-- Get current pilots
function PanManager.GetCurrentPilots()
    return PanManager.currentPilots
end

-- NEW: Frequency filtering methods

-- Set whether a frequency is enabled for a specific pilot
function PanManager.SetPilotFrequencyEnabled(pilotId, frequency, enabled)
    if not PanManager.pilotFrequencyFilter[pilotId] then
        PanManager.pilotFrequencyFilter[pilotId] = {}
    end
    
    PanManager.pilotFrequencyFilter[pilotId][frequency] = enabled
    Tacview.Log.Debug(string.format("Pilot %s frequency %.1f MHz: %s", 
        pilotId, frequency, enabled and "enabled" or "disabled"))
end

-- Check if a frequency is enabled for a specific pilot
function PanManager.IsPilotFrequencyEnabled(pilotId, frequency)
    if not PanManager.pilotFrequencyFilter[pilotId] then
        return true -- Default: enabled
    end
    
    local enabled = PanManager.pilotFrequencyFilter[pilotId][frequency]
    return enabled == nil or enabled == true -- nil = not set = enabled by default
end

-- Set whether a frequency is enabled for general listening (non-selected pilots)
function PanManager.SetGeneralFrequencyEnabled(frequency, enabled)
    PanManager.generalFrequencyFilter[frequency] = enabled
    Tacview.Log.Debug(string.format("General frequency %.1f MHz: %s", 
        frequency, enabled and "enabled" or "disabled"))
end

-- Check if a frequency is enabled for general listening
function PanManager.IsGeneralFrequencyEnabled(frequency)
    local enabled = PanManager.generalFrequencyFilter[frequency]
    -- CHANGED: Default to false (disabled) if not explicitly set
    return enabled == true
end

-- Get all known frequencies
function PanManager.GetAllFrequencies()
    local freqList = {}
    for freq, _ in pairs(PanManager.allFrequencies) do
        table.insert(freqList, freq)
    end
    table.sort(freqList)
    return freqList
end

-- Enable all frequencies for a pilot
function PanManager.EnableAllPilotFrequencies(pilotId)
    if PanManager.pilotFrequencyFilter[pilotId] then
        for freq, _ in pairs(PanManager.pilotFrequencyFilter[pilotId]) do
            PanManager.pilotFrequencyFilter[pilotId][freq] = true
        end
    end
end

-- Disable all frequencies for a pilot
function PanManager.DisableAllPilotFrequencies(pilotId)
    if PanManager.pilotFrequencyFilter[pilotId] then
        for freq, _ in pairs(PanManager.pilotFrequencyFilter[pilotId]) do
            PanManager.pilotFrequencyFilter[pilotId][freq] = false
        end
    end
end

-- Enable all general frequencies
function PanManager.EnableAllGeneralFrequencies()
    for freq, _ in pairs(PanManager.generalFrequencyFilter) do
        PanManager.generalFrequencyFilter[freq] = true
    end
end

-- Disable all general frequencies
function PanManager.DisableAllGeneralFrequencies()
    for freq, _ in pairs(PanManager.generalFrequencyFilter) do
        PanManager.generalFrequencyFilter[freq] = false
    end
end

return PanManager
```

### NEW: 9. menu_ui.lua (Tacview Menu Integration)

```lua
-- Menu UI - Tacview menu integration for pan and frequency configuration
local panManager = require("pan_manager")

local MenuUI = {
    menuId = nil,
    panDialogOpen = false
}

function MenuUI.RegisterMenu(addon)
    -- Register main menu item
    MenuUI.menuId = Tacview.UI.AddMenu("AeroDebrief Sync")
    
    -- Add submenu items
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Configure Audio Pan...", function()
        MenuUI.ShowPanConfigDialog(addon)
    end)
    
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Auto Pan Mode", function()
        panManager.SetMode("auto")
        Tacview.UI.MessageBox("Pan mode set to Auto\n\nPilots will be automatically distributed across the stereo field.", "AeroDebrief Sync")
        -- Trigger selection update
        addon:OnSelectionChange()
    end)
    
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Manual Pan Mode", function()
        panManager.SetMode("manual")
        Tacview.UI.MessageBox("Pan mode set to Manual\n\nUse 'Configure Audio Pan...' to set individual pilot pan values.", "AeroDebrief Sync")
        -- Trigger selection update
        addon:OnSelectionChange()
    end)
    
    Tacview.UI.AddMenuSeparator(MenuUI.menuId)
    
    -- NEW: Frequency filtering menu items
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Configure Pilot Frequencies...", function()
        MenuUI.ShowPilotFrequencyDialog(addon)
    end)
    
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Configure General Frequencies...", function()
        MenuUI.ShowGeneralFrequencyDialog(addon)
    end)
    
    Tacview.UI.AddMenuSeparator(MenuUI.menuId)
    
    -- NEW: Settings menu item
    Tacview.UI.AddMenuItem(MenuUI.menuId, "Settings...", function()
        MenuUI.ShowSettingsDialog(addon)
    end)
    
    Tacview.UI.AddMenuSeparator(MenuUI.menuId)
    
    Tacview.UI.AddMenuItem(MenuUI.menuId, "About", function()
        MenuUI.ShowAboutDialog()
    end)
    
    Tacview.Log.Info("AeroDebrief Sync menu registered")
end

function MenuUI.Unregister()
    if MenuUI.menuId then
        Tacview.UI.RemoveMenu(MenuUI.menuId)
        MenuUI.menuId = nil
    end
end

function MenuUI.ShowPanConfigDialog(addon)
    if MenuUI.panDialogOpen then
        return -- Prevent multiple dialogs
    end
    
    MenuUI.panDialogOpen = true
    
    local pilots = panManager.GetCurrentPilots()
    
    if #pilots == 0 then
        Tacview.UI.MessageBox("No pilots selected.\n\nPlease select aircraft in Tacview first.", "AeroDebrief Sync")
        MenuUI.panDialogOpen = false
        return
    end
    
    -- Build dialog content
    local dialogText = "Configure Spatial Audio Pan\n"
    dialogText = dialogText .. "????????????????????????\n\n"
    dialogText = dialogText .. "Current Mode: " .. panManager.GetMode() .. "\n\n"
    
    if panManager.GetMode() == "auto" then
        dialogText = dialogText .. "Auto mode distributes pilots evenly:\n\n"
        for i, pilot in ipairs(pilots) do
            local pan = panManager.GetPanForPilot(pilot.pilot_id, i, #pilots)
            local panDesc = MenuUI.GetPanDescription(pan)
            dialogText = dialogText .. string.format("• %s: %s (%.2f)\n", pilot.pilot_name, panDesc, pan)
        end
        dialogText = dialogText .. "\nSwitch to Manual mode to customize."
    else
        dialogText = dialogText .. "Manual pan settings:\n\n"
        for i, pilot in ipairs(pilots) do
            local pan = panManager.GetManualPan(pilot.pilot_id)
            local panDesc = MenuUI.GetPanDescription(pan)
            dialogText = dialogText .. string.format("• %s: %s (%.2f)\n", pilot.pilot_name, panDesc, pan)
        end
        dialogText = dialogText .. "\nUse per-pilot menu items to adjust."
    end
    
    Tacview.UI.MessageBox(dialogText, "AeroDebrief Sync - Audio Pan")
    
    -- If manual mode, show individual pilot pan controls
    if panManager.GetMode() == "manual" then
        MenuUI.ShowPilotPanControls(addon, pilots)
    end
    
    MenuUI.panDialogOpen = false
end

function MenuUI.ShowPilotPanControls(addon, pilots)
    for i, pilot in ipairs(pilots) do
        local currentPan = panManager.GetManualPan(pilot.pilot_id)
        
        -- Create submenu for each pilot
        local options = {
            "Full Left (-1.0)",
            "Mostly Left (-0.7)",
            "Slightly Left (-0.3)",
            "Center (0.0)",
            "Slightly Right (+0.3)",
            "Mostly Right (+0.7)",
            "Full Right (+1.0)",
            "Custom..."
        }
        
        local choice = Tacview.UI.ChoiceBox(
            "Select pan position for: " .. pilot.pilot_name,
            "Current: " .. MenuUI.GetPanDescription(currentPan) .. string.format(" (%.2f)", currentPan),
            options
        )
        
        if choice > 0 then
            local panValue = MenuUI.ChoiceToPanValue(choice)
            
            if choice == 8 then
                -- Custom value
                local customValue = Tacview.UI.InputBox(
                    "Enter custom pan value for: " .. pilot.pilot_name,
                    "Value between -1.0 (left) and +1.0 (right):",
                    string.format("%.2f", currentPan)
                )
                
                if customValue then
                    panValue = tonumber(customValue)
                    if panValue then
                        panValue = math.max(-1.0, math.min(1.0, panValue))
                    else
                        panValue = currentPan -- Invalid input, keep current
                    end
                end
            end
            
            if panValue then
                panManager.SetManualPan(pilot.pilot_id, panValue)
                Tacview.Log.Info(string.format("Pan set for %s: %.2f", pilot.pilot_name, panValue))
                
                -- Trigger selection update to send new pan values
                addon:OnSelectionChange()
            end
        end
    end
end

-- NEW: Frequency filtering dialogs

function MenuUI.ShowPilotFrequencyDialog(addon)
    local pilots = panManager.GetCurrentPilots()
    
    if #pilots == 0 then
        Tacview.UI.MessageBox("No pilots selected.\n\nPlease select aircraft in Tacview first.", "AeroDebrief Sync")
        return
    end
    
    -- Show frequency selection for each pilot
    for _, pilot in ipairs(pilots) do
        if pilot.frequencies and #pilot.frequencies > 0 then
            MenuUI.ShowFrequencyCheckboxes(
                pilot.pilot_name,
                pilot.pilot_id,
                pilot.frequencies,
                true, -- isPilotSpecific
                addon
            )
        end
    end
end

function MenuUI.ShowGeneralFrequencyDialog(addon)
    local allFrequencies = panManager.GetAllFrequencies()
    
    if #allFrequencies == 0 then
        Tacview.UI.MessageBox("No frequencies available.\n\nPlease select aircraft first or load a mission with frequency data.", "AeroDebrief Sync")
        return
    end
    
    MenuUI.ShowFrequencyCheckboxes(
        "All Non-Selected Pilots",
        nil, -- no specific pilot
        allFrequencies,
        false, -- not pilot-specific
        addon
    )
end

function MenuUI.ShowFrequencyCheckboxes(title, pilotId, frequencies, isPilotSpecific, addon)
    -- Build options list
    local options = {}
    local currentStates = {}
    
    -- Add "Select All" and "Deselect All" options
    table.insert(options, "? Select All Frequencies")
    table.insert(options, "? Deselect All Frequencies")
    table.insert(options, "????????????????????")
    
    -- Add individual frequencies
    for _, freq in ipairs(frequencies) do
        local isEnabled
        if isPilotSpecific then
            isEnabled = panManager.IsPilotFrequencyEnabled(pilotId, freq)
        else
            isEnabled = panManager.IsGeneralFrequencyEnabled(freq)
        end
        
        local checkbox = isEnabled and "?" or "?"
        table.insert(options, string.format("%s %.1f MHz", checkbox, freq))
        table.insert(currentStates, isEnabled)
    end
    
    table.insert(options, "????????????????????")
    table.insert(options, "Done")
    
    -- Show dialog
    local dialogTitle = "Configure Frequencies: " .. title
    local dialogText = isPilotSpecific 
        and "Select which frequencies to monitor for this pilot:"
        or "Select which frequencies to monitor for all non-selected pilots:"
    
    while true do
        local choice = Tacview.UI.ChoiceBox(dialogTitle, dialogText, options)
        
        if choice == 0 or choice == #options then
            -- User cancelled or clicked "Done"
            break
        elseif choice == 1 then
            -- Select All
            if isPilotSpecific then
                panManager.EnableAllPilotFrequencies(pilotId)
            else
                panManager.EnableAllGeneralFrequencies()
            end
            
            -- Update checkboxes
            for i = 4, #options - 2 do
                local freqIndex = i - 3
                if freqIndex <= #frequencies then
                    options[i] = string.format("? %.1f MHz", frequencies[freqIndex])
                    currentStates[freqIndex] = true
                end
            end
            
            -- Trigger update
            addon:OnSelectionChange()
            
        elseif choice == 2 then
            -- Deselect All
            if isPilotSpecific then
                panManager.DisableAllPilotFrequencies(pilotId)
            else
                panManager.DisableAllGeneralFrequencies()
            end
            
            -- Update checkboxes
            for i = 4, #options - 2 do
                local freqIndex = i - 3
                if freqIndex <= #frequencies then
                    options[i] = string.format("? %.1f MHz", frequencies[freqIndex])
                    currentStates[freqIndex] = false
                end
            end
            
            -- Trigger update
            addon:OnSelectionChange()
            
        elseif choice > 3 and choice < #options then
            -- Toggle individual frequency
            local freqIndex = choice - 3
            if freqIndex <= #frequencies then
                local freq = frequencies[freqIndex]
                local newState = not currentStates[freqIndex]
                
                if isPilotSpecific then
                    panManager.SetPilotFrequencyEnabled(pilotId, freq, newState)
                else
                    panManager.SetGeneralFrequencyEnabled(freq, newState)
                end
                
                -- Update checkbox
                local checkbox = newState and "?" or "?"
                options[choice] = string.format("%s %.1f MHz", checkbox, freq)
                currentStates[freqIndex] = newState
                
                -- Trigger update
                addon:OnSelectionChange()
            end
        end
    end
end

function MenuUI.ChoiceToPanValue(choice)
    local panMap = {
        -1.0,  -- Full Left
        -0.7,  -- Mostly Left
        -0.3,  -- Slightly Left
        0.0,   -- Center
        0.3,   -- Slightly Right
        0.7,   -- Mostly Right
        1.0,   -- Full Right
        nil    -- Custom (handled separately)
    }
    return panMap[choice]
end

function MenuUI.GetPanDescription(pan)
    if pan <= -0.8 then return "Full Left" end
    if pan <= -0.5 then return "Mostly Left" end
    if pan <= -0.2 then return "Slightly Left" end
    if pan <= 0.2 then return "Center" end
    if pan <= 0.5 then return "Slightly Right" end
    if pan <= 0.8 then return "Mostly Right" end
    return "Full Right"
end

function MenuUI.ShowAboutDialog()
    local aboutText = [[
AeroDebrief Voice Sync
Version 1.0.0

Synchronizes AeroDebrief voice recordings
with Tacview mission replay.

Features:
• Real-time time synchronization
• Pilot-specific audio filtering
• Per-pilot frequency selection
• General frequency monitoring
• Spatial audio (stereo pan)
• Speaking pilot indicators

Configuration:
• Auto Pan: Pilots distributed evenly
• Manual Pan: Custom positioning per pilot
• Frequency Filters: Select which frequencies to hear

TCP Port: 52001 (localhost only)

Visit: https://github.com/yourusername/AeroDebrief
]]
    
    Tacview.UI.MessageBox(aboutText, "About AeroDebrief Sync")
end

-- NEW: Settings dialog
function MenuUI.ShowSettingsDialog(addon)
    local config = require("config")
    
    while true do
        local dialogText = "AeroDebrief Sync Settings\n"
        dialogText = dialogText .. "????????????????????????\n\n"
        dialogText = dialogText .. string.format("TCP Port: %d\n", config.Port)
        dialogText = dialogText .. string.format("Update Rate: %d Hz\n", config.UpdateRate)
        dialogText = dialogText .. string.format("Max Clients: %d\n", config.MaxClients)
        dialogText = dialogText .. string.format("Auto-Reconnect: %s\n", config.EnableAutoReconnect and "Enabled" or "Disabled")
        dialogText = dialogText .. string.format("Speaking Indicators: %s\n", config.EnableSpeakingIndicators and "Enabled" or "Disabled")
        dialogText = dialogText .. "\n"
        dialogText = dialogText .. "Select a setting to change:"
        
        local options = {
            "Change TCP Port...",
            "Change Update Rate...",
            "Change Max Clients...",
            string.format("Toggle Auto-Reconnect (%s)", config.EnableAutoReconnect and "ON" or "OFF"),
            string.format("Toggle Speaking Indicators (%s)", config.EnableSpeakingIndicators and "ON" or "OFF"),
            "????????????????????",
            "Save Settings",
            "Cancel"
        }
        
        local choice = Tacview.UI.ChoiceBox("Settings", dialogText, options)
        
        if choice == 0 or choice == 8 then
            -- Cancel
            break
        elseif choice == 1 then
            -- Change TCP Port
            local newPort = Tacview.UI.InputBox(
                "Change TCP Port",
                string.format("Enter new TCP port (current: %d):\n\nNote: Requires restart to take effect.", config.Port),
                tostring(config.Port)
            )
            
            if newPort then
                local portNum = tonumber(newPort)
                if portNum and portNum >= 1024 and portNum <= 65535 then
                    config.Port = portNum
                    Tacview.UI.MessageBox(
                        string.format("Port changed to %d\n\nRestart Tacview for changes to take effect.", portNum),
                        "Settings Updated"
                    )
                else
                    Tacview.UI.MessageBox(
                        "Invalid port number.\n\nPort must be between 1024 and 65535.",
                        "Error"
                    )
                end
            end
            
        elseif choice == 2 then
            -- Change Update Rate
            local newRate = Tacview.UI.InputBox(
                "Change Update Rate",
                string.format("Enter update rate in Hz (current: %d):\n\nRecommended: 10 Hz", config.UpdateRate),
                tostring(config.UpdateRate)
            )
            
            if newRate then
                local rateNum = tonumber(newRate)
                if rateNum and rateNum >= 1 and rateNum <= 60 then
                    config.UpdateRate = rateNum
                    Tacview.UI.MessageBox(
                        string.format("Update rate changed to %d Hz", rateNum),
                        "Settings Updated"
                    )
                else
                    Tacview.UI.MessageBox(
                        "Invalid update rate.\n\nRate must be between 1 and 60 Hz.",
                        "Error"
                    )
                end
            end
            
        elseif choice == 3 then
            -- Change Max Clients
            local newMax = Tacview.UI.InputBox(
                "Change Max Clients",
                string.format("Enter maximum concurrent clients (current: %d):", config.MaxClients),
                tostring(config.MaxClients)
            )
            
            if newMax then
                local maxNum = tonumber(newMax)
                if maxNum and maxNum >= 1 and maxNum <= 10 then
                    config.MaxClients = maxNum
                    Tacview.UI.MessageBox(
                        string.format("Max clients changed to %d", maxNum),
                        "Settings Updated"
                    )
                else
                    Tacview.UI.MessageBox(
                        "Invalid client count.\n\nMust be between 1 and 10.",
                        "Error"
                    )
                end
            end
            
        elseif choice == 4 then
            -- Toggle Auto-Reconnect
            config.EnableAutoReconnect = not config.EnableAutoReconnect
            
        elseif choice == 5 then
            -- Toggle Speaking Indicators
            config.EnableSpeakingIndicators = not config.EnableSpeakingIndicators
            
        elseif choice == 7 then
            -- Save Settings
            if config.Save() then
                Tacview.UI.MessageBox(
                    "Settings saved successfully.\n\nSome changes may require restarting Tacview.",
                    "Settings Saved"
                )
            else
                Tacview.UI.MessageBox(
                    "Failed to save settings.\n\nCheck log for details.",
                    "Error"
                )
            end
            break
        end
    end
end

return MenuUI
```

## Tacview API Usage

### Key API Functions

```lua
-- Time and playback
Tacview.Context.GetAbsoluteTime()           -- Returns Unix timestamp (seconds since epoch)
Tacview.Context.GetPlaybackState()          -- Returns 0 (stopped), 1 (playing), 2 (paused)
Tacview.Context.GetPlaybackSpeed()          -- Returns playback speed multiplier

-- Object queries
Tacview.Context.GetSelectedObjects()        -- Returns array of selected object IDs
Tacview.Context.GetObjectName(objectId)     -- Returns object name
Tacview.Context.GetObjectCoalition(objectId) -- Returns 1 (red), 2 (blue), 0 (neutral)
Tacview.Context.GetObjectType(objectId)     -- Returns unit type string
Tacview.Context.GetObjectProperty(objectId, propName) -- Returns custom property value

-- Visual modifications
Tacview.Context.SetObjectColor(objectId, color) -- color is 0xRRGGBB
Tacview.Context.SetObjectScale(objectId, scale) -- scale is multiplier (1.0 = normal)

-- Events
Tacview.Events.Update.RegisterListener(callback)
Tacview.Events.PlaybackStateChange.RegisterListener(callback)
Tacview.Events.SelectionChange.RegisterListener(callback)

-- Logging
Tacview.Log.Debug(message)
Tacview.Log.Info(message)
Tacview.Log.Warning(message)
Tacview.Log.Error(message)
```

## Installation & Testing

### Installation Steps
1. Copy addon folder to Tacview's addon directory:
   - Windows: `%APPDATA%\Tacview\AddOns\`
   - The full path: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\`

2. Restart Tacview

3. Check addon loaded in Tacview's log:
   - Go to Help ? Show Log
   - Look for "AeroDebrief Sync: Initialized successfully"

### Testing Checklist
- [ ] Addon loads without errors
- [ ] TCP server starts on port 52001
- [ ] AeroDebrief can connect to addon
- [ ] Time updates sent at 10 Hz
- [ ] Pilot selection changes trigger messages
- [ ] Speaking status updates pilot highlights
- [ ] Visual effects work (color change, scaling)
- [ ] Multiple clients supported
- [ ] Reconnection works after disconnect
- [ ] Tacview menu appears with correct options
- [ ] Pan configuration dialog shows current settings
- [ ] Auto/Manual pan modes switch correctly
- [ ] Per-pilot pan sliders appear in manual mode
- [ ] About dialog shows addon information
- [ ] Frequency configuration dialogs show correct checkboxes
- [ ] Frequency selection updates are sent to AeroDebrief
- [ ] Settings dialog shows correct current values
- [ ] TCP port can be changed and saved
- [ ] Update rate can be changed and saved
- [ ] Max clients can be changed and saved
- [ ] Auto-reconnect toggle works and saves
- [ ] Speaking indicators toggle works and saves

## Known Limitations

1. **Tacview API Constraints**
   - Limited visual customization (color, scale only)
   - No custom 3D rendering
   - Property access depends on Tacview file format

2. **LuaSocket Availability**
   - Tacview may not include LuaSocket by default
   - May need to bundle LuaSocket DLLs with addon
   - Alternative: Use Tacview's built-in networking (if available)

3. **Performance**
   - Lua is interpreted, not compiled
   - Limit update frequency to avoid performance impact
   - Keep message processing lightweight

## Future Enhancements

1. **Configuration UI**
   - Add Tacview menu for addon settings
   - Allow port configuration
   - Enable/disable features

2. **Advanced Visual Effects**
   - Custom 3D indicators above speaking pilots
   - Radio wave animations
   - Directional indicators

3. **Recording Export**
   - Export selected pilot communications
   - Generate voice transcript
   - Sync with Tacview export formats

4. **Multiple Frequency Display**
   - Show all active frequencies per pilot
   - Color-code by frequency
   - Display transmission strength

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
