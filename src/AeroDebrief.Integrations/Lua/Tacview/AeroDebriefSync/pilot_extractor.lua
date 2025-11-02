-- Pilot Information Extractor
-- Extracts pilot data from Tacview objects

local PilotExtractor = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function PilotExtractor.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

----------------------------------------------------------------
-- Extract pilot information from object
----------------------------------------------------------------

function PilotExtractor.ExtractPilotInfo(objectId)
    if not objectId then
        return nil
    end
    
    -- Get object handle
    local object = Tacview.Context.GetObject(objectId)
    if not object then
        return nil
    end
    
    -- Extract basic information
    local pilotName = Tacview.Database.GetText(objectId, Tacview.Database.Property.Pilot) or
                      Tacview.Database.GetText(objectId, Tacview.Database.Property.Name) or
                      string.format("Object-%X", objectId)
    
    local coalition = PilotExtractor.GetCoalition(objectId)
    local unitType = Tacview.Database.GetText(objectId, Tacview.Database.Property.Type) or "Unknown"
    
    -- Extract radio frequencies
    local frequencies = PilotExtractor.ExtractFrequencies(objectId)
    
    -- Create pilot info structure
    local pilotInfo = {
        pilot_id = string.format("%X", objectId), -- Use hex object ID as unique identifier
        pilot_name = pilotName,
        coalition = coalition,
        unit_type = unitType,
        frequencies = frequencies,
        enabled_frequencies = nil, -- Will be set by pan manager
        pan = 0.0 -- Will be set by pan manager
    }
    
    return pilotInfo
end

----------------------------------------------------------------
-- Get coalition for object
----------------------------------------------------------------

function PilotExtractor.GetCoalition(objectId)
    local tags = Tacview.Database.GetTags(objectId)
    
    if tags then
        if tags:find("Allies") then
            return "Blue"
        elseif tags:find("Enemies") then
            return "Red"
        end
    end
    
    return "Neutral"
end

----------------------------------------------------------------
-- Extract radio frequencies from object
----------------------------------------------------------------

function PilotExtractor.ExtractFrequencies(objectId)
    local frequencies = {}
    
    -- Try to get radio frequencies from object properties
    -- Note: This is a simplified implementation - actual frequency extraction
    -- may require parsing specific DCS properties or using custom data
    
    -- Check for Radio1, Radio2, etc. properties
    for i = 1, 4 do
        local freqProp = Tacview.Database.GetText(objectId, "Radio" .. i)
        if freqProp then
            local freq = tonumber(freqProp)
            if freq and freq > 0 then
                table.insert(frequencies, freq)
            end
        end
    end
    
    -- If no frequencies found, use common DCS frequencies as fallback
    if #frequencies == 0 then
        -- Default frequencies based on coalition
        local coalition = PilotExtractor.GetCoalition(objectId)
        
        if coalition == "Blue" then
            frequencies = {251.0, 305.0} -- Common NATO frequencies
        elseif coalition == "Red" then
            frequencies = {124.0, 264.0} -- Common Soviet/Russian frequencies
        else
            frequencies = {243.0} -- Guard frequency
        end
    end
    
    return frequencies
end

return PilotExtractor
