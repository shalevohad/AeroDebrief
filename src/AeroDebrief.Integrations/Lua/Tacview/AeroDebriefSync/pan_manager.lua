-- Pan Manager for Spatial Audio
-- Manages pan settings and frequency filtering

local PanManager = {}

-- Pan mode: "auto" or "manual"
local panMode = "auto"

-- Per-pilot pan settings (pilot_id -> pan value)
local pilotPanSettings = {}

-- Per-pilot enabled frequencies (pilot_id -> {freq1, freq2, ...})
local pilotEnabledFrequencies = {}

-- General enabled frequencies for non-selected pilots
-- Default: empty (all disabled)
local generalEnabledFrequencies = {}

----------------------------------------------------------------
-- Initialize Pan Manager
----------------------------------------------------------------

function PanManager.Initialize()
    panMode = "auto"
    pilotPanSettings = {}
    pilotEnabledFrequencies = {}
    generalEnabledFrequencies = {}
    
    Tacview.Log.Debug("AeroDebrief Sync: Pan Manager initialized")
end

----------------------------------------------------------------
-- Get/Set Pan Mode
----------------------------------------------------------------

function PanManager.GetMode()
    return panMode
end

function PanManager.SetMode(mode)
    if mode == "auto" or mode == "manual" then
        panMode = mode
        Tacview.Log.Debug("AeroDebrief Sync: Pan mode set to " .. mode)
    else
        Tacview.Log.Warning("AeroDebrief Sync: Invalid pan mode: " .. tostring(mode))
    end
end

----------------------------------------------------------------
-- Get Pan Value for Pilot
----------------------------------------------------------------

function PanManager.GetPilotPan(pilotId)
    if panMode == "manual" then
        -- Return stored pan value or default center
        return pilotPanSettings[pilotId] or 0.0
    else
        -- Auto mode - calculate based on pilot index
        -- This is a simple implementation - could be enhanced
        return 0.0 -- Center for now
    end
end

----------------------------------------------------------------
-- Set Pan Value for Pilot
----------------------------------------------------------------

function PanManager.SetPilotPan(pilotId, panValue)
    -- Clamp pan value to [-1.0, 1.0]
    panValue = math.max(-1.0, math.min(1.0, panValue))
    pilotPanSettings[pilotId] = panValue
    
    Tacview.Log.Debug(string.format("AeroDebrief Sync: Set pan for pilot %s: %.2f", pilotId, panValue))
end

----------------------------------------------------------------
-- Frequency Filtering - Per-Pilot
----------------------------------------------------------------

function PanManager.GetPilotEnabledFrequencies(pilotId)
    return pilotEnabledFrequencies[pilotId]
end

function PanManager.SetPilotFrequencyEnabled(pilotId, frequency, enabled)
    if not pilotEnabledFrequencies[pilotId] then
        pilotEnabledFrequencies[pilotId] = {}
    end
    
    if enabled then
        -- Add frequency if not already present
        local found = false
        for _, freq in ipairs(pilotEnabledFrequencies[pilotId]) do
            if freq == frequency then
                found = true
                break
            end
        end
        if not found then
            table.insert(pilotEnabledFrequencies[pilotId], frequency)
        end
    else
        -- Remove frequency
        for i, freq in ipairs(pilotEnabledFrequencies[pilotId]) do
            if freq == frequency then
                table.remove(pilotEnabledFrequencies[pilotId], i)
                break
            end
        end
    end
end

function PanManager.ClearPilotFrequencies(pilotId)
    pilotEnabledFrequencies[pilotId] = {}
end

----------------------------------------------------------------
-- Frequency Filtering - General (Non-Selected Pilots)
----------------------------------------------------------------

function PanManager.GetGeneralEnabledFrequencies()
    return generalEnabledFrequencies
end

function PanManager.SetGeneralFrequencyEnabled(frequency, enabled)
    if enabled then
        -- Add frequency if not already present
        local found = false
        for _, freq in ipairs(generalEnabledFrequencies) do
            if freq == frequency then
                found = true
                break
            end
        end
        if not found then
            table.insert(generalEnabledFrequencies, frequency)
        end
    else
        -- Remove frequency
        for i, freq in ipairs(generalEnabledFrequencies) do
            if freq == frequency then
                table.remove(generalEnabledFrequencies, i)
                break
            end
        end
    end
end

function PanManager.ClearGeneralFrequencies()
    generalEnabledFrequencies = {}
end

----------------------------------------------------------------
-- Get All Known Frequencies
----------------------------------------------------------------

function PanManager.GetAllFrequencies()
    -- Collect all unique frequencies from all pilots
    local allFreqs = {}
    local seenFreqs = {}
    
    for _, freqs in pairs(pilotEnabledFrequencies) do
        for _, freq in ipairs(freqs) do
            if not seenFreqs[freq] then
                table.insert(allFreqs, freq)
                seenFreqs[freq] = true
            end
        end
    end
    
    return allFreqs
end

return PanManager
