-- AeroDebrief Sync - Main Entry Point
-- Synchronizes AeroDebrief voice playback with Tacview

local AeroDebriefSync = {}

-- Module dependencies
local config = require("config")
local tcpServer = require("tcp_server")
local protocol = require("protocol")
local stateManager = require("state_manager")
local pilotExtractor = require("pilot_extractor")
local panManager = require("pan_manager")
local menuUI = require("menu_ui")
local visualEffects = require("visual_effects")
local utils = require("utils")

-- State
local isInitialized = false

----------------------------------------------------------------
-- Initialize addon
----------------------------------------------------------------

function AeroDebriefSync:OnInitialize()
    Tacview.Log.Info("==============================================")
    Tacview.Log.Info("AeroDebrief Sync: Initializing...")
    Tacview.Log.Info("==============================================")
    
    -- Initialize configuration
    config.Initialize()
    
    -- Initialize visual effects
    visualEffects.Initialize()
    
    -- Start TCP server
    local success, err = pcall(function()
        tcpServer.Start(config.Port, config.BindAddress)
    end)
    
    if not success then
        Tacview.Log.Error(string.format("Failed to start TCP server: %s", tostring(err)))
        Tacview.UI.MessageBox.Error("Failed to start AeroDebrief Sync TCP server. Check port " .. config.Port)
        return
    end
    
    Tacview.Log.Info(string.format("TCP server started on %s:%d", config.BindAddress, config.Port))
    
    -- Initialize pan manager
    panManager.Initialize()
    
    -- Register menu
    menuUI.RegisterMenu(self)
    
    -- Set message handler for incoming messages from AeroDebrief
    tcpServer.SetMessageHandler(function(message)
        self:OnMessageReceived(message)
    end)
    
    isInitialized = true
    
    Tacview.Log.Info("==============================================")
    Tacview.Log.Info("AeroDebrief Sync: Initialized successfully")
    Tacview.Log.Info(string.format("  Port: %d", config.Port))
    Tacview.Log.Info(string.format("  Update rate: %d Hz", config.UpdateRate))
    Tacview.Log.Info(string.format("  Visual effects: enabled"))
    Tacview.Log.Info("==============================================")
end

----------------------------------------------------------------
-- Update loop (called every frame)
----------------------------------------------------------------

function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    if not isInitialized then
        return
    end
    
    -- Update TCP server (process incoming/outgoing messages)
    tcpServer.Update()
    
    -- Get current mission state
    local currentTime = Tacview.Context.GetAbsoluteTime()
    local isPlaying = Tacview.Context.GetPlaybackState() == Tacview.Context.PlaybackState.Playing
    local playbackSpeed = Tacview.Context.GetPlaybackSpeed() or 1.0
    
    -- Check if state has changed (including playback speed)
    local stateChanged = stateManager.HasStateChanged(
        currentTime,
        isPlaying and "playing" or "paused",
        playbackSpeed
    )
    
    -- Broadcast time updates at configured rate
    if stateChanged then
        local playbackState = isPlaying and "playing" or "paused"
        local message = protocol.CreateTimeUpdate(currentTime, playbackState, playbackSpeed)
        
        if message then
            tcpServer.Broadcast(message)
            stateManager.UpdateState(currentTime, playbackState, playbackSpeed)
        end
    end
    
    -- Update visual effects (transmission indicators)
    visualEffects.Update(dt, absoluteTime)
end

----------------------------------------------------------------
-- Playback state change handler
----------------------------------------------------------------

function AeroDebriefSync:OnPlaybackStateChange(isPlaying)
    if not isInitialized then
        return
    end
    
    Tacview.Log.Debug(string.format("Playback state changed: %s", isPlaying and "playing" or "paused"))
    
    local currentTime = Tacview.Context.GetAbsoluteTime()
    local playbackSpeed = Tacview.Context.GetPlaybackSpeed() or 1.0
    local playbackState = isPlaying and "playing" or "paused"
    
    -- Broadcast state change
    local message = protocol.CreateTimeUpdate(currentTime, playbackState, playbackSpeed)
    if message then
        tcpServer.Broadcast(message)
        stateManager.UpdateState(currentTime, playbackState, playbackSpeed)
    end
    
    -- Clear visual effects when seeking
    if not isPlaying then
        visualEffects.Clear()
    end
end

----------------------------------------------------------------
-- Selection change handler
----------------------------------------------------------------

function AeroDebriefSync:OnSelectionChange()
    if not isInitialized then
        return
    end
    
    Tacview.Log.Debug("Selection changed")
    
    -- Get selected objects
    local selectedObjects = {}
    local selection = Tacview.Context.GetSelectedObjects()
    
    if selection then
        for i = 0, #selection - 1 do
            table.insert(selectedObjects, selection[i])
        end
    end
    
    -- Extract pilot information
    local pilots = {}
    for _, objectId in ipairs(selectedObjects) do
        local pilotInfo = pilotExtractor.ExtractPilotInfo(objectId)
        if pilotInfo then
            -- Add pan information
            pilotInfo.pan = panManager.GetPilotPan(pilotInfo.pilot_id)
            
            -- Add enabled frequencies
            pilotInfo.enabled_frequencies = panManager.GetPilotEnabledFrequencies(pilotInfo.pilot_id)
            
            table.insert(pilots, pilotInfo)
        end
    end
    
    -- Get pan mode and general frequencies
    local panMode = panManager.GetMode()
    local generalFrequencies = panManager.GetGeneralEnabledFrequencies()
    
    -- Broadcast pilot selection
    local message = protocol.CreatePilotSelection(pilots, panMode, generalFrequencies)
    if message then
        tcpServer.Broadcast(message)
    end
end

----------------------------------------------------------------
-- Handle incoming messages from AeroDebrief
----------------------------------------------------------------

function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if not decoded then
        Tacview.Log.Warn("Failed to decode message from AeroDebrief")
        return
    end
    
    if decoded.type == "speaking_status" then
        -- Update visual effects for transmission
        visualEffects.UpdateTransmission(
            decoded.pilot_id,
            decoded.pilot_name,
            decoded.frequency,
            decoded.is_speaking
        )
        
        Tacview.Log.Debug(string.format(
            "Transmission %s: %s on %.3f MHz",
            decoded.is_speaking and "started" or "ended",
            decoded.pilot_name or decoded.pilot_id,
            decoded.frequency / 1000000.0
        ))
        
    elseif decoded.type == "frequency_filter_update" then
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
                if not utils.Contains(frequencies, freq) then
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
                if not utils.Contains(frequencies, freq) then
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

----------------------------------------------------------------
-- Shutdown handler
----------------------------------------------------------------

function AeroDebriefSync:OnShutdown()
    if not isInitialized then
        return
    end
    
    Tacview.Log.Info("AeroDebrief Sync: Shutting down...")
    
    -- Save configuration
    config.Save()
    
    -- Stop TCP server
    tcpServer.Stop()
    
    -- Clear visual effects
    visualEffects.Clear()
    
    isInitialized = false
    
    Tacview.Log.Info("AeroDebrief Sync: Shutdown complete")
end

----------------------------------------------------------------
-- Register addon with Tacview
----------------------------------------------------------------

Tacview.AddOns.Register(AeroDebriefSync)

return AeroDebriefSync
