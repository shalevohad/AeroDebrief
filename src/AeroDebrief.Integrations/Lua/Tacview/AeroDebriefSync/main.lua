-- AeroDebrief Sync - Main Entry Point
-- Synchronizes AeroDebrief voice playback with Tacview

-- Note: lua-strict is disabled to allow access to Tacview's built-in global modules (JSON, etc.)
-- If you want to re-enable strict mode, uncomment the line below and add local declarations for all globals
-- require("lua-strict")

local Tacview = require("Tacview190")

-- Log that main.lua is being executed
Tacview.Log.Info("AeroDebrief Sync: main.lua loaded, creating addon table...")

local AeroDebriefSync = {}

-- Module dependencies - pass Tacview to each module
local config = require("config")
config.SetTacview(Tacview)

local tcpServer = require("tcp_server")
tcpServer.SetTacview(Tacview)

local protocol = require("protocol")
protocol.SetTacview(Tacview)

local stateManager = require("state_manager")

local pilotExtractor = require("pilot_extractor")
pilotExtractor.SetTacview(Tacview)

local panManager = require("pan_manager")
panManager.SetTacview(Tacview)

local menuUI = require("menu_ui")
menuUI.SetTacview(Tacview)

local visualEffects = require("visual_effects")
visualEffects.SetTacview(Tacview)

local utils = require("utils")

Tacview.Log.Info("AeroDebrief Sync: All modules loaded successfully")

-- State
local isInitialized = false
local documentLoaded = false  -- Track if telemetry document is loaded

-- Selection detection state (polling workaround)
local lastSelectedPrimary = nil
local lastSelectedSecondary = nil
local selectionCheckInterval = 1.0  -- Check every 1 second
local lastSelectionCheckTime = 0

----------------------------------------------------------------
-- Initialize addon (called immediately on load)
----------------------------------------------------------------

local function Initialize()
    if isInitialized then
        return
    end
    
    Tacview.Log.Info("==============================================")
    Tacview.Log.Info("AeroDebrief Sync: Initializing...")
    Tacview.Log.Info("==============================================")
    
    -- Initialize configuration
    config.Initialize()
    
    -- Initialize state manager with update rate from config
    stateManager.SetUpdateRate(config.UpdateRate or 10)
    Tacview.Log.Info(string.format("State manager update rate: %d Hz", config.UpdateRate or 10))
    
    -- Initialize visual effects
    visualEffects.Initialize()
    
    -- Start TCP server
    local success, err = pcall(function()
        tcpServer.Start(config.Port, config.BindAddress)
    end)
    
    if not success then
        Tacview.Log.Error(string.format("Failed to start TCP server: %s", tostring(err)))
        -- Don't show message box during auto-init, just log
        return
    end
    
    Tacview.Log.Info(string.format("TCP server started on %s:%d", config.BindAddress, config.Port))
    
    -- Initialize pan manager
    panManager.Initialize()
    
    -- Register menu
    menuUI.RegisterMenu(AeroDebriefSync)
    
    -- Set message handler for incoming messages from AeroDebrief
    tcpServer.SetMessageHandler(function(message)
        AeroDebriefSync:OnMessageReceived(message)
    end)
    
    -- Register event listeners according to Tacview 1.9.0 API
    RegisterEventListeners()
    
    isInitialized = true
    
    Tacview.Log.Info("==============================================")
    Tacview.Log.Info("AeroDebrief Sync: Initialized successfully")
    Tacview.Log.Info(string.format("  Port: %d", config.Port))
    Tacview.Log.Info(string.format("  Update rate: %d Hz", config.UpdateRate))
    Tacview.Log.Info(string.format("  Visual effects: enabled"))
    Tacview.Log.Info("  Event listeners: registered")
    Tacview.Log.Info("  Selection detection: polling (1 Hz)")
    Tacview.Log.Info("  ⚠️  Waiting for telemetry file to be loaded...")
    Tacview.Log.Info("==============================================")
end

----------------------------------------------------------------
-- Register event listeners using proper Tacview 1.9.0 API
----------------------------------------------------------------

function RegisterEventListeners()
    Tacview.Log.Info("Registering event listeners...")
    
    -- Register Update listener (replaces OnUpdate callback)
    Tacview.Events.Update.RegisterListener(function(dt, absoluteTime)
        AeroDebriefSync:OnUpdate(dt, absoluteTime)
    end)
    
    -- Register DocumentLoaded listener
    Tacview.Events.DocumentLoaded.RegisterListener(function(fileNames)
        AeroDebriefSync:OnDocumentLoaded(fileNames)
    end)
    
    -- Register DocumentUnload listener
    Tacview.Events.DocumentUnload.RegisterListener(function()
        AeroDebriefSync:OnDocumentUnload()
    end)
    
    -- Register Shutdown listener
    Tacview.Events.Shutdown.RegisterListener(function()
        AeroDebriefSync:OnShutdown()
    end)
    
    Tacview.Log.Info("✅ Event listeners registered successfully")
end

-- Initialize immediately when the addon loads
Initialize()

----------------------------------------------------------------
-- Check for selection changes (polling workaround)
----------------------------------------------------------------

function AeroDebriefSync:CheckSelectionChange(currentTime)
    -- Only check periodically (1 Hz default)
    if currentTime - lastSelectionCheckTime < selectionCheckInterval then
        return
    end

    lastSelectionCheckTime = currentTime

    -- Try to get selected objects (documented API)
    local okPrimary, selectedPrimary = pcall(function() return Tacview.Context.GetSelectedObject(0) end)
    local okSecondary, selectedSecondary = pcall(function() return Tacview.Context.GetSelectedObject(1) end)

    if not okPrimary or not okSecondary then
        -- API doesn't exist or failed - only log once
        if lastSelectedPrimary ~= "api_not_available" or lastSelectedSecondary ~= "api_not_available" then
            Tacview.Log.Debug("Selection detection: GetSelectedObject(index) not available in this Tacview version")
            Tacview.Log.Debug("Use manual broadcast via menu: AeroDebrief Sync → 📡 Broadcast Selected Pilots")
            lastSelectedPrimary = "api_not_available"
            lastSelectedSecondary = "api_not_available"
        end
        return
    end

    -- Check primary selection change
    if selectedPrimary ~= lastSelectedPrimary then
        lastSelectedPrimary = selectedPrimary

        if selectedPrimary and selectedPrimary ~= 0 then
            Tacview.Log.Info(string.format("📌 Primary object selected: %s", tostring(selectedPrimary)))
            self:BroadcastSelectedPilot(selectedPrimary, 0)
        else
            Tacview.Log.Debug("Primary selection cleared")
        end
    end

    -- Check secondary selection change
    if selectedSecondary ~= lastSelectedSecondary then
        lastSelectedSecondary = selectedSecondary

        if selectedSecondary and selectedSecondary ~= 0 then
            Tacview.Log.Info(string.format("📌 Secondary object selected: %s", tostring(selectedSecondary)))
            self:BroadcastSelectedPilot(selectedSecondary, 1)
        else
            Tacview.Log.Debug("Secondary selection cleared")
        end
    end
end

----------------------------------------------------------------
-- Broadcast current pilot selection
----------------------------------------------------------------

function AeroDebriefSync:BroadcastPilotSelection()
    -- Extract all pilots from current telemetry
    local pilots = pilotExtractor.ExtractPilots()
    
    if not pilots or #pilots == 0 then
        Tacview.Log.Warning("No pilots found - cannot broadcast selection")
        return
    end
    
    -- Get pan mode and general frequencies
    local panMode = panManager.GetMode()
    local generalFreqs = panManager.GetGeneralEnabledFrequencies()
    
    -- Create and broadcast pilot selection message
    local message = protocol.CreatePilotSelection(pilots, panMode, generalFreqs)
    
    if message then
        local success = tcpServer.Broadcast(message)
        
        if success then
            Tacview.Log.Info(string.format(
                "📡 Auto-broadcast: %d pilots, %s pan, %d general frequencies",
                #pilots,
                panMode,
                #generalFreqs
            ))
        else
            Tacview.Log.Debug("No clients connected for auto-broadcast")
        end
    else
        Tacview.Log.Error("Failed to create pilot selection message")
    end
end

----------------------------------------------------------------
-- Broadcast a single selected pilot
----------------------------------------------------------------

function AeroDebriefSync:BroadcastSelectedPilot(objectHandle, selectionIndex)
    if not objectHandle or objectHandle == 0 then
        return
    end

    -- Extract pilot info for the selected object
    local pilot = pilotExtractor.ExtractPilotInfo(objectHandle)
    if not pilot then
        Tacview.Log.Debug("BroadcastSelectedPilot: No pilot info found for selected object")
        return
    end

    -- Build pilot list with single selected pilot
    local pilots = { pilot }
    local panMode = panManager.GetMode()
    local generalFreqs = panManager.GetGeneralEnabledFrequencies()

    local message = protocol.CreatePilotSelection(pilots, panMode, generalFreqs)
    if message then
        local success = tcpServer.Broadcast(message)
        if success then
            local idxText = selectionIndex and (selectionIndex == 1 and "secondary" or "primary") or "primary"
            Tacview.Log.Info(string.format("📡 Broadcast %s selected pilot: %s (%s)", idxText, pilot.pilot_name or "unknown", pilot.pilot_id))
        else
            Tacview.Log.Debug("No clients connected - skipping selected pilot broadcast")
        end
    else
        Tacview.Log.Warning("Failed to create pilot selection message for selected pilot")
    end
end

----------------------------------------------------------------
-- Update handler (called via Events.Update.RegisterListener)
----------------------------------------------------------------

function AeroDebriefSync:OnUpdate(dt, absoluteTime)
    if not isInitialized then
        return
    end
    
    -- Update TCP server to process connections
    tcpServer.Update()
    
    -- CRITICAL: Do not broadcast time updates until a telemetry document is loaded
    if not documentLoaded then
        -- Still log occasionally so user knows addon is running
        if not self._lastNoDocLog then
            self._lastNoDocLog = 0
        end
        
        local clockNow = os.clock()
        if clockNow - self._lastNoDocLog > 10.0 then
            Tacview.Log.Debug("[UPDATE] Waiting for telemetry file to be loaded (no broadcasts until then)")
            self._lastNoDocLog = clockNow
        end
        return
    end
    
    -- Check for selection changes (polling workaround for missing API)
    self:CheckSelectionChange(absoluteTime)
    
    -- Get current mission state
    local currentTime = Tacview.Context.GetAbsoluteTime()
    local isPlaying = Tacview.Context.Playback.IsPlaying()
    
    -- NOTE: Tacview 1.9.0 API does NOT expose playback speed to addons
    -- However, we CAN calculate it by comparing real-time vs mission-time deltas!
    -- The state manager calculates effective speed automatically
    local playbackSpeed = stateManager.GetCalculatedPlaybackSpeed()
    local playbackState = isPlaying and "playing" or "paused"
    
    -- DEBUG: Log state every 5 seconds
    if not self._lastDebugLog then
        self._lastDebugLog = 0
        self._updateCount = 0
    end
    self._updateCount = self._updateCount + 1
    
    local clockNow = os.clock()
    if clockNow - self._lastDebugLog > 5.0 then
        Tacview.Log.Info(string.format(
            "[UPDATE] stats - updates=%d, time=%.2f, state=%s, speed=%.2fx, clients=%d",
            self._updateCount, currentTime, playbackState, playbackSpeed, tcpServer.GetClientCount()
        ))
        self._lastDebugLog = clockNow
        self._updateCount = 0
    end
    
    -- Check if state has changed
    -- The state manager automatically detects speed changes based on time deltas
    local stateChanged = stateManager.HasStateChanged(
        currentTime,
        playbackState,
        playbackSpeed  -- Pass the calculated speed
    )
    
    -- Broadcast time updates at configured rate
    if stateChanged then
        local message = protocol.CreateTimeUpdate(currentTime, playbackState, playbackSpeed)
        
        if message then
            local broadcastSuccess = tcpServer.Broadcast(message)
            
            if not broadcastSuccess then
                Tacview.Log.Debug("[UPDATE] Broadcast returned false - no clients connected")
            end
            
            stateManager.UpdateState(currentTime, playbackState, playbackSpeed)
        else
            Tacview.Log.Error("[UPDATE] Failed to create time update message!")
        end
    end
    
    -- Update visual effects (transmission indicators)
    visualEffects.Update(dt, absoluteTime)
end

----------------------------------------------------------------
-- Document loaded handler (called when telemetry file is loaded)
----------------------------------------------------------------

function AeroDebriefSync:OnDocumentLoaded(fileNames)
    Tacview.Log.Info("╔════════════════════════════════════════════════════╗")
    Tacview.Log.Info("│  📄 DOCUMENT LOADED                               │")
    Tacview.Log.Info("│                                                    │")
    if fileNames and fileNames ~= "" then
        Tacview.Log.Info("│  Files: " .. fileNames)
    end
    Tacview.Log.Info("│  ✅ Time updates will now be broadcast            │")
    Tacview.Log.Info("╚════════════════════════════════════════════════════╝")
    
    -- Mark document as loaded - broadcasts can now start
    documentLoaded = true
    
    -- Reset state manager to start fresh with new document
    stateManager.Reset()
    
    -- Reset selection tracking
    lastSelectedObjectId = nil
    
    -- Broadcast document info to AeroDebrief clients
    local docInfoMessage = protocol.CreateDocumentInfo(fileNames)
    if docInfoMessage then
        local success = tcpServer.Broadcast(docInfoMessage)
        if success then
            Tacview.Log.Info("📡 Document info broadcast to clients")
        else
            Tacview.Log.Debug("No clients connected to receive document info")
        end
    else
        Tacview.Log.Warning("Failed to create document info message")
    end
    
    -- Broadcast pilot selection automatically after document loads
    Tacview.Log.Info("📡 Auto-broadcasting pilots on document load...")
    self:BroadcastPilotSelection()
end

----------------------------------------------------------------
-- Document unload handler
----------------------------------------------------------------

function AeroDebriefSync:OnDocumentUnload()
    Tacview.Log.Info("==> DOCUMENT UNLOADING")
    
    -- Stop broadcasts when document is unloaded
    documentLoaded = false
    
    -- Clear visual effects
    visualEffects.Clear()
    
    -- Reset state manager
    stateManager.Reset()
    
    -- Reset selection tracking
    lastSelectedObjectId = nil
    
    Tacview.Log.Info("⚠️  Time updates paused until next document is loaded")
end

----------------------------------------------------------------
-- Handle incoming messages from AeroDebrief
----------------------------------------------------------------

function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if not decoded then
        Tacview.Log.Warning("Failed to decode message from AeroDebrief")
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
        
        Tacview.Log.Debug("Frequency filter updated from AeroDebrief")
        
    elseif decoded.type == "pan_configuration" then
        -- Update pan configuration from AeroDebrief
        panManager.SetMode(decoded.pan_mode or "auto")
        
        if decoded.pilot_pan_settings then
            for pilotId, panValue in pairs(decoded.pilot_pan_settings) do
                panManager.SetPilotPan(pilotId, panValue)
            end
        end
        
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
    documentLoaded = false
    
    Tacview.Log.Info("AeroDebrief Sync: Shutdown complete")
end

----------------------------------------------------------------
-- Return addon table (Tacview auto-registers it)
----------------------------------------------------------------

Tacview.Log.Info("AeroDebrief Sync: Module loaded successfully")

return AeroDebriefSync
