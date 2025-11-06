-- Menu UI for AeroDebrief Sync

local MenuUI = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function MenuUI.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

local Config = require("config")
local TcpServer = require("tcp_server")
local PanManager = require("pan_manager")
local VisualEffects = require("visual_effects")

-- Menu item IDs
local MenuItems = {
    Root = nil,
    ConfigurePan = nil,
    AutoPan = nil,
    ManualPan = nil,
    ConfigurePilotFrequencies = nil,
    ConfigureGeneralFrequencies = nil,
    VisualEffects = nil,        -- NEW: Visual effects submenu
    ToggleTextLabels = nil,     -- NEW
    ToggleRadioWaves = nil,     -- NEW
    Settings = nil,
    ChangePort = nil,
    ChangeUpdateRate = nil,
    SaveSettings = nil,
    About = nil
}

----------------------------------------------------------------
-- Register menu in Tacview
----------------------------------------------------------------

function MenuUI.RegisterMenu(addon)
    -- Main menu
    MenuItems.Root = Tacview.UI.Menus.AddMenu(nil, "AeroDebrief Sync")
    
    -- Broadcast Pilots button (NEW)
    Tacview.UI.Menus.AddCommand(MenuItems.Root, "?? Broadcast Selected Pilots", function()
        MenuUI.BroadcastSelectedPilots()
    end)
    
    Tacview.UI.Menus.AddSeparator(MenuItems.Root)
    
    -- Pan configuration submenu
    MenuItems.ConfigurePan = Tacview.UI.Menus.AddCommand(MenuItems.Root, "Configure Audio Pan...", function()
        MenuUI.ShowPanConfiguration()
    end)
    
    -- Pan mode options (checkable)
    local panMode = PanManager.GetMode()
    MenuItems.AutoPan = Tacview.UI.Menus.AddOption(MenuItems.Root, "Auto Pan Mode", panMode == "auto", function()
        PanManager.SetMode("auto")
        MenuUI.UpdatePanModeMenu()
        Tacview.Log.Info("Pan mode set to Auto")
    end)
    
    MenuItems.ManualPan = Tacview.UI.Menus.AddOption(MenuItems.Root, "Manual Pan Mode", panMode == "manual", function()
        PanManager.SetMode("manual")
        MenuUI.UpdatePanModeMenu()
        Tacview.Log.Info("Pan mode set to Manual")
    end)
    
    Tacview.UI.Menus.AddSeparator(MenuItems.Root)
    
    -- Frequency configuration
    MenuItems.ConfigurePilotFrequencies = Tacview.UI.Menus.AddCommand(MenuItems.Root, "Configure Pilot Frequencies...", function()
        MenuUI.ShowPilotFrequencyConfiguration()
    end)
    
    MenuItems.ConfigureGeneralFrequencies = Tacview.UI.Menus.AddCommand(MenuItems.Root, "Configure General Frequencies...", function()
        MenuUI.ShowGeneralFrequencyConfiguration()
    end)
    
    Tacview.UI.Menus.AddSeparator(MenuItems.Root)
    
    -- Visual effects submenu
    MenuItems.VisualEffects = Tacview.UI.Menus.AddMenu(MenuItems.Root, "Visual Effects")
    
    local visualConfig = VisualEffects.GetConfig()
    MenuItems.ToggleTextLabels = Tacview.UI.Menus.AddOption(MenuItems.VisualEffects, "Show Frequency Labels", visualConfig.enableTextLabels, function()
        local currentConfig = VisualEffects.GetConfig()
        local newEnabled = not currentConfig.enableTextLabels
        VisualEffects.SetTextLabelsEnabled(newEnabled)
        MenuUI.UpdateVisualEffectsMenu()
        Tacview.Log.Info(string.format("Frequency labels: %s", newEnabled and "enabled" or "disabled"))
    end)
    
    MenuItems.ToggleRadioWaves = Tacview.UI.Menus.AddOption(MenuItems.VisualEffects, "Show Radio Waves", visualConfig.enableRadioWaves, function()
        local currentConfig = VisualEffects.GetConfig()
        local newEnabled = not currentConfig.enableRadioWaves
        VisualEffects.SetRadioWavesEnabled(newEnabled)
        MenuUI.UpdateVisualEffectsMenu()
        Tacview.Log.Info(string.format("Radio waves: %s", newEnabled and "enabled" or "disabled"))
    end)
    
    Tacview.UI.Menus.AddCommand(MenuItems.VisualEffects, "Configure Effect Settings...", function()
        MenuUI.ShowVisualEffectsDialog()
    end)
    
    Tacview.UI.Menus.AddSeparator(MenuItems.Root)
    
    -- Settings submenu
    MenuItems.Settings = Tacview.UI.Menus.AddMenu(MenuItems.Root, "Settings")
    
    MenuItems.ChangePort = Tacview.UI.Menus.AddCommand(MenuItems.Settings, "Change TCP Port...", function()
        MenuUI.ShowPortConfiguration()
    end)
    
    MenuItems.ChangeUpdateRate = Tacview.UI.Menus.AddCommand(MenuItems.Settings, "Change Update Rate...", function()
        MenuUI.ShowUpdateRateDialog()
    end)
    
    MenuItems.SaveSettings = Tacview.UI.Menus.AddCommand(MenuItems.Settings, "Save Settings", function()
        Config.Save()
        Tacview.UI.MessageBox.Info("Settings saved successfully.")
    end)
    
    Tacview.UI.Menus.AddSeparator(MenuItems.Root)
    
    -- About
    MenuItems.About = Tacview.UI.Menus.AddCommand(MenuItems.Root, "About", function()
        MenuUI.ShowAbout()
    end)
    
    Tacview.Log.Debug("AeroDebrief Sync: Menu registered")
end

----------------------------------------------------------------
-- Update pan mode menu checkmarks
----------------------------------------------------------------

function MenuUI.UpdatePanModeMenu()
    local mode = PanManager.GetMode()
    Tacview.UI.Menus.SetOption(MenuItems.AutoPan, mode == "auto")
    Tacview.UI.Menus.SetOption(MenuItems.ManualPan, mode == "manual")
end

----------------------------------------------------------------
-- Update visual effects menu checkmarks
----------------------------------------------------------------

function MenuUI.UpdateVisualEffectsMenu()
    local config = VisualEffects.GetConfig()
    Tacview.UI.Menus.SetOption(MenuItems.ToggleTextLabels, config.enableTextLabels)
    Tacview.UI.Menus.SetOption(MenuItems.ToggleRadioWaves, config.enableRadioWaves)
end

----------------------------------------------------------------
-- Show Pan Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowPanConfiguration()
    local message = "Pan Configuration\n\n"
    message = message .. "Current Mode: " .. PanManager.GetMode() .. "\n\n"
    message = message .. "Use the menu to switch between Auto and Manual pan modes.\n"
    message = message .. "In Manual mode, pan values can be configured from AeroDebrief UI."
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show Pilot Frequency Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowPilotFrequencyConfiguration()
    local message = "Pilot Frequency Configuration\n\n"
    message = message .. "Per-pilot frequency filtering can be configured from:\n"
    message = message .. "• Tacview menu (current)\n"
    message = message .. "• AeroDebrief UI (recommended)\n\n"
    message = message .. "Changes in either location will sync automatically."
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show General Frequency Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowGeneralFrequencyConfiguration()
    local message = "General Frequency Configuration\n\n"
    message = message .. "General frequencies control which transmissions from\n"
    message = message .. "NON-SELECTED pilots are played.\n\n"
    message = message .. "Default: All disabled (no audio from non-selected pilots)\n\n"
    message = message .. "Configure from AeroDebrief UI for best experience."
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show Visual Effects Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowVisualEffectsDialog()
    local currentConfig = VisualEffects.GetConfig()
    
    -- Show current settings
    local message = "Visual Effects Configuration\n\n"
    message = message .. string.format("Current Settings:\n")
    message = message .. string.format("• Label Height: %.0f meters\n", currentConfig.textLabelHeight)
    message = message .. string.format("• Radio Wave Radius: %.0f meters\n\n", currentConfig.radioWaveRadius)
    message = message .. "To change settings:\n"
    message = message .. "1. Close this dialog\n"
    message = message .. "2. Edit config in addon folder\n"
    message = message .. "3. Restart Tacview\n\n"
    message = message .. "Or use the checkboxes in the Visual Effects menu\n"
    message = message .. "to enable/disable effects."
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show Settings Dialog
----------------------------------------------------------------

function MenuUI.ShowSettings()
    local status = TcpServer.IsRunning() and "Running" or "Stopped"
    local clientCount = TcpServer.GetClientCount()
    
    local message = "AeroDebrief Sync Settings\n\n"
    message = message .. string.format("TCP Server: %s\n", status)
    message = message .. string.format("Port: %d\n", Config.Port)
    message = message .. string.format("Connected Clients: %d\n", clientCount)
    message = message .. string.format("Update Rate: %d Hz\n", Config.UpdateRate)
    message = message .. string.format("Auto-Reconnect: %s\n", tostring(Config.AutoReconnect))
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show Port Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowPortConfiguration()
    local message = string.format("Current TCP Port: %d\n\n", Config.Port)
    message = message .. "To change the port:\n"
    message = message .. "1. Stop Tacview\n"
    message = message .. "2. Edit config.txt in addon folder\n"
    message = message .. "3. Set Port=<new port number>\n"
    message = message .. "4. Restart Tacview\n\n"
    message = message .. "Note: Both Tacview and AeroDebrief must use the same port."
    
    Tacview.UI.MessageBox.Info(message)
end

----------------------------------------------------------------
-- Show Update Rate Configuration Dialog
----------------------------------------------------------------

function MenuUI.ShowUpdateRateDialog()
    local stateManager = require("state_manager")
    
    -- Show current update rate and interval
    local currentRate = stateManager.GetUpdateRate()
    local currentInterval = stateManager.GetUpdateInterval()
    
    local message = string.format(
        "Current update rate: %d Hz (%.0f ms interval)\n\n" ..
        "Enter new update rate (1-60 Hz):\n" ..
        "(Higher = more responsive, more network usage)\n\n" ..
        "Recommended: 10 Hz (100ms) for good balance",
        math.floor(currentRate),
        currentInterval * 1000
    )
    
    -- Use InputText for text input
    local newRateStr = Tacview.UI.MessageBox.InputText(
        "Change Update Rate",
        message,
        tostring(math.floor(currentRate))
    )
    
    if newRateStr then
        local newRate = tonumber(newRateStr)
        
        if newRate and newRate >= 1 and newRate <= 60 then
            -- Update configuration
            Config.UpdateRate = math.floor(newRate)
            
            -- CRITICAL: Immediately apply the new rate to state manager
            stateManager.SetUpdateRate(Config.UpdateRate)
            
            -- Save configuration
            Config.Save()
            
            -- Show confirmation with actual interval
            local newInterval = stateManager.GetUpdateInterval()
            Tacview.UI.MessageBox.Info(string.format(
                "Update rate changed to %d Hz\n" ..
                "Interval: %.0f ms\n\n" ..
                "New rate is active immediately!",
                Config.UpdateRate,
                newInterval * 1000
            ))
            
            Tacview.Log.Info(string.format(
                "Update rate changed: %d Hz (%.0f ms interval)",
                Config.UpdateRate,
                newInterval * 1000
            ))
        else
            Tacview.UI.MessageBox.Error("Invalid input. Please enter a number between 1 and 60.")
            Tacview.Log.Warning(string.format("Invalid update rate input: %s", tostring(newRateStr)))
        end
    end
end

----------------------------------------------------------------
-- Broadcast Selected Pilots (Manual Trigger)
----------------------------------------------------------------

function MenuUI.BroadcastSelectedPilots()
    -- Extract all pilots from current telemetry
    local pilots = pilotExtractor.ExtractPilots()
    
    if not pilots or #pilots == 0 then
        Tacview.UI.MessageBox.Warning("No pilots found in current telemetry.\n\nPlease load a telemetry file first.")
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
            Tacview.UI.MessageBox.Info(string.format(
                "Broadcast successful!\n\n" ..
                "Pilots: %d\n" ..
                "Pan Mode: %s\n" ..
                "General Frequencies: %d",
                #pilots,
                panMode,
                #generalFreqs
            ))
            
            Tacview.Log.Info(string.format(
                "Manually broadcast pilot selection: %d pilots, %s pan mode",
                #pilots,
                panMode
            ))
        else
            Tacview.UI.MessageBox.Warning("No AeroDebrief clients connected.\n\nPlease connect AeroDebrief first.")
        end
    else
        Tacview.UI.MessageBox.Error("Failed to create pilot selection message.")
    end
end

----------------------------------------------------------------
-- Show About Dialog
----------------------------------------------------------------

function MenuUI.ShowAbout()
    local message = "AeroDebrief Voice Sync\n"
    message = message .. "Version 1.0.0\n\n"
    message = message .. "Synchronizes AeroDebrief voice recordings with Tacview mission replay.\n\n"
    message = message .. "Features:\n"
    message = message .. "• Time synchronization (10 Hz)\n"
    message = message .. "• Pilot selection filtering\n"
    message = message .. "• Frequency-based filtering\n"
    message = message .. "• Spatial audio (pan)\n"
    message = message .. "• Bidirectional configuration\n\n"
    message = message .. "For more information, visit:\n"
    message = message .. "https://github.com/shalevohad/AeroDebrief"
    
    Tacview.UI.MessageBox.Info(message)
end

return MenuUI
