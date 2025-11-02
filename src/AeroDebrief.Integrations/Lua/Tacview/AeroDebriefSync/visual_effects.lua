-- Visual Effects Module for AeroDebrief Sync
-- Displays real-time transmission indicators in Tacview 3D view

local VisualEffects = {}

-- Configuration
local Config = {
    -- Text label settings
    enableTextLabels = true,
    textLabelHeight = 50,          -- Height above aircraft in meters
    textLabelColor = 0xFF00FF00,   -- Green for active transmission
    textLabelSize = 1.5,            -- Text size multiplier
    
    -- Radio wave effect settings
    enableRadioWaves = true,
    radioWaveColor = 0x8000FF00,   -- Semi-transparent green
    radioWaveRadius = 100,          -- Radius in meters
    radioWaveDuration = 1.0,        -- Duration in seconds
    
    -- Frequency color mapping
    frequencyColors = {
        [251.0] = 0xFF00FF00,  -- Green
        [264.0] = 0xFF0080FF,  -- Light blue
        [305.0] = 0xFFFF8000,  -- Orange
        [127.5] = 0xFFFF00FF,  -- Magenta
        -- Add more as needed
    }
}

-- Active transmissions tracking
local activeTransmissions = {}

-- Transmission data structure:
-- {
--     objectId = number,
--     pilotName = string,
--     frequency = number (Hz),
--     startTime = number (mission time),
--     endTime = number (mission time),
--     color = number (ARGB)
-- }

----------------------------------------------------------------
-- Initialize visual effects system
----------------------------------------------------------------

function VisualEffects.Initialize()
    activeTransmissions = {}
    Tacview.Log.Info("AeroDebrief Sync: Visual effects initialized")
end

----------------------------------------------------------------
-- Update active transmissions from AeroDebrief
-- Called when receiving "speaking_status" messages
----------------------------------------------------------------

function VisualEffects.UpdateTransmission(pilotId, pilotName, frequency, isSpeaking)
    -- Find object ID from pilot ID
    local objectId = FindObjectIdByPilotId(pilotId)
    if not objectId then
        Tacview.Log.Debug(string.format("Cannot find object for pilot: %s", pilotName or pilotId))
        return
    end
    
    local currentTime = Tacview.Context.GetAbsoluteTime()
    
    if isSpeaking then
        -- Start transmission
        local color = GetColorForFrequency(frequency)
        
        activeTransmissions[pilotId] = {
            objectId = objectId,
            pilotName = pilotName,
            frequency = frequency,
            startTime = currentTime,
            endTime = nil,
            color = color
        }
        
        Tacview.Log.Debug(string.format("Transmission started: %s on %.3f MHz", 
            pilotName, frequency / 1000000.0))
    else
        -- End transmission
        local transmission = activeTransmissions[pilotId]
        if transmission then
            transmission.endTime = currentTime
            
            -- Keep transmission visible for a short time after it ends
            -- Remove it in Update() after fade-out period
            
            Tacview.Log.Debug(string.format("Transmission ended: %s on %.3f MHz", 
                pilotName, frequency / 1000000.0))
        end
    end
end

----------------------------------------------------------------
-- Update visual effects every frame
-- Called from main.lua OnUpdate()
----------------------------------------------------------------

function VisualEffects.Update(dt, absoluteTime)
    if not Config.enableTextLabels and not Config.enableRadioWaves then
        return
    end
    
    -- Clean up old transmissions (after 1 second fade-out)
    local toRemove = {}
    for pilotId, transmission in pairs(activeTransmissions) do
        if transmission.endTime and (absoluteTime - transmission.endTime) > 1.0 then
            table.insert(toRemove, pilotId)
        end
    end
    
    for _, pilotId in ipairs(toRemove) do
        activeTransmissions[pilotId] = nil
    end
    
    -- Draw visual effects for active transmissions
    for pilotId, transmission in pairs(activeTransmissions) do
        DrawTransmissionEffects(transmission, absoluteTime)
    end
end

----------------------------------------------------------------
-- Draw visual effects for a transmission
----------------------------------------------------------------

function DrawTransmissionEffects(transmission, absoluteTime)
    local objectId = transmission.objectId
    
    -- Get object transform (position, rotation)
    local transform = Tacview.Context.GetTransform(objectId)
    if not transform then
        return
    end
    
    -- Calculate fade factor if transmission has ended
    local fadeFactor = 1.0
    if transmission.endTime then
        local timeSinceEnd = absoluteTime - transmission.endTime
        fadeFactor = math.max(0, 1.0 - timeSinceEnd)
    end
    
    -- Draw text label
    if Config.enableTextLabels then
        DrawFrequencyLabel(transform, transmission, fadeFactor)
    end
    
    -- Draw radio wave effect
    if Config.enableRadioWaves and not transmission.endTime then
        DrawRadioWaveEffect(transform, transmission, absoluteTime)
    end
end

----------------------------------------------------------------
-- Draw frequency label above aircraft
----------------------------------------------------------------

function DrawFrequencyLabel(transform, transmission, fadeFactor)
    -- Calculate label position (above aircraft)
    local labelPos = {
        longitude = transform.longitude,
        latitude = transform.latitude,
        altitude = transform.altitude + Config.textLabelHeight
    }
    
    -- Format frequency text
    local freqMHz = transmission.frequency / 1000000.0
    local text = string.format("TX: %.3f MHz", freqMHz)
    
    -- Apply fade to color
    local color = ApplyFadeToColor(transmission.color, fadeFactor)
    
    -- Draw text using Tacview's text rendering
    Tacview.UI.Renderer.DrawText(
        labelPos,
        text,
        color,
        Config.textLabelSize
    )
    
    -- Optional: Draw pilot name below frequency
    if transmission.pilotName then
        local namePos = {
            longitude = transform.longitude,
            latitude = transform.latitude,
            altitude = transform.altitude + Config.textLabelHeight - 10
        }
        
        Tacview.UI.Renderer.DrawText(
            namePos,
            transmission.pilotName,
            ApplyFadeToColor(0xFFFFFFFF, fadeFactor * 0.8),
            Config.textLabelSize * 0.8
        )
    end
end

----------------------------------------------------------------
-- Draw expanding radio wave effect
----------------------------------------------------------------

function DrawRadioWaveEffect(transform, transmission, absoluteTime)
    local timeSinceStart = absoluteTime - transmission.startTime
    
    -- Calculate wave expansion (0 to max radius over duration)
    local progress = (timeSinceStart % Config.radioWaveDuration) / Config.radioWaveDuration
    local radius = Config.radioWaveRadius * progress
    
    -- Fade out as wave expands
    local alpha = (1.0 - progress) * 0.5
    local color = ApplyFadeToColor(transmission.color, alpha)
    
    -- Draw circle at aircraft position
    Tacview.UI.Renderer.DrawCircle(
        {
            longitude = transform.longitude,
            latitude = transform.latitude,
            altitude = transform.altitude
        },
        radius,
        color,
        2.0  -- Line width
    )
}

----------------------------------------------------------------
-- Helper: Find Tacview object ID from pilot ID (GUID)
----------------------------------------------------------------

function FindObjectIdByPilotId(pilotId)
    -- Iterate through all objects to find matching pilot
    local objectCount = Tacview.Context.GetObjectCount()
    
    for i = 0, objectCount - 1 do
        local objectId = Tacview.Context.GetObjectId(i)
        if objectId then
            -- Get object properties
            local pilot = Tacview.Context.GetObjectProperty(objectId, "Pilot")
            local guid = Tacview.Context.GetObjectProperty(objectId, "GUID")
            
            -- Match by GUID or pilot name
            if guid == pilotId or pilot == pilotId then
                return objectId
            end
        end
    end
    
    return nil
end

----------------------------------------------------------------
-- Helper: Get color for frequency
----------------------------------------------------------------

function GetColorForFrequency(frequency)
    -- Round frequency to nearest 0.1 MHz for lookup
    local freqMHz = math.floor(frequency / 100000.0 + 0.5) / 10.0
    
    -- Look up color in frequency table
    local color = Config.frequencyColors[freqMHz]
    if color then
        return color
    end
    
    -- Generate color based on frequency hash
    return GenerateColorFromFrequency(frequency)
end

----------------------------------------------------------------
-- Helper: Generate color from frequency (if not in table)
----------------------------------------------------------------

function GenerateColorFromFrequency(frequency)
    -- Use frequency as seed for color generation
    local hue = (frequency % 1000000) / 1000000.0
    
    -- Convert HSV to RGB (simplified)
    local r, g, b = HSVtoRGB(hue, 0.8, 1.0)
    
    -- Convert to ARGB hex
    return 0xFF000000 + 
           (math.floor(r * 255) * 0x10000) + 
           (math.floor(g * 255) * 0x100) + 
           math.floor(b * 255)
end

----------------------------------------------------------------
-- Helper: HSV to RGB conversion
----------------------------------------------------------------

function HSVtoRGB(h, s, v)
    local i = math.floor(h * 6)
    local f = h * 6 - i
    local p = v * (1 - s)
    local q = v * (1 - f * s)
    local t = v * (1 - (1 - f) * s)
    
    i = i % 6
    
    if i == 0 then return v, t, p
    elseif i == 1 then return q, v, p
    elseif i == 2 then return p, v, t
    elseif i == 3 then return p, q, v
    elseif i == 4 then return t, p, v
    else return v, p, q
    end
end

----------------------------------------------------------------
-- Helper: Apply fade factor to ARGB color
----------------------------------------------------------------

function ApplyFadeToColor(color, fadeFactor)
    fadeFactor = math.max(0, math.min(1, fadeFactor))
    
    -- Extract ARGB components
    local a = math.floor((color / 0x1000000) % 0x100)
    local r = math.floor((color / 0x10000) % 0x100)
    local g = math.floor((color / 0x100) % 0x100)
    local b = math.floor(color % 0x100)
    
    -- Apply fade to alpha
    a = math.floor(a * fadeFactor)
    
    -- Reconstruct color
    return (a * 0x1000000) + (r * 0x10000) + (g * 0x100) + b
end

----------------------------------------------------------------
-- Configuration setters (called from menu)
----------------------------------------------------------------

function VisualEffects.SetTextLabelsEnabled(enabled)
    Config.enableTextLabels = enabled
    Tacview.Log.Info(string.format("Text labels: %s", enabled and "enabled" or "disabled"))
end

function VisualEffects.SetRadioWavesEnabled(enabled)
    Config.enableRadioWaves = enabled
    Tacview.Log.Info(string.format("Radio waves: %s", enabled and "enabled" or "disabled"))
end

function VisualEffects.SetTextLabelHeight(height)
    Config.textLabelHeight = height
end

function VisualEffects.SetRadioWaveRadius(radius)
    Config.radioWaveRadius = radius
end

----------------------------------------------------------------
-- Clear all active transmissions (e.g., when seeking)
----------------------------------------------------------------

function VisualEffects.Clear()
    activeTransmissions = {}
    Tacview.Log.Debug("Visual effects cleared")
end

----------------------------------------------------------------
-- Get configuration (for saving)
----------------------------------------------------------------

function VisualEffects.GetConfig()
    return {
        enableTextLabels = Config.enableTextLabels,
        enableRadioWaves = Config.enableRadioWaves,
        textLabelHeight = Config.textLabelHeight,
        radioWaveRadius = Config.radioWaveRadius
    }
end

----------------------------------------------------------------
-- Set configuration (for loading)
----------------------------------------------------------------

function VisualEffects.SetConfig(config)
    if config.enableTextLabels ~= nil then
        Config.enableTextLabels = config.enableTextLabels
    end
    if config.enableRadioWaves ~= nil then
        Config.enableRadioWaves = config.enableRadioWaves
    end
    if config.textLabelHeight then
        Config.textLabelHeight = config.textLabelHeight
    end
    if config.radioWaveRadius then
        Config.radioWaveRadius = config.radioWaveRadius
    end
end

return VisualEffects
