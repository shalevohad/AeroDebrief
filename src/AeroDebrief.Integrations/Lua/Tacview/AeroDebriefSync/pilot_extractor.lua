-- Pilot Information Extractor
-- Extracts pilot data from Tacview objects

local PilotExtractor = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function PilotExtractor.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

----------------------------------------------------------------
-- Extract all pilots from current telemetry
----------------------------------------------------------------

function PilotExtractor.ExtractPilots()
    local pilots = {}
    
    if not Tacview then
        Tacview.Log.Error("ExtractPilots: Tacview API not initialized")
        return pilots
    end
    
    -- Get all active objects at current time
    local objectCount = Tacview.Telemetry.GetObjectCount()
    
    if objectCount == 0 then
        Tacview.Log.Debug("ExtractPilots: No objects found in telemetry")
        return pilots
    end
    
    Tacview.Log.Debug(string.format("ExtractPilots: Processing %d objects", objectCount))
    
    -- Iterate through all objects
    for i = 0, objectCount - 1 do
        local objectHandle = Tacview.Telemetry.GetObjectHandleByIndex(i)
        
        if objectHandle then
            local pilotInfo = PilotExtractor.ExtractPilotInfo(objectHandle)
            
            if pilotInfo then
                table.insert(pilots, pilotInfo)
            end
        end
    end
    
    Tacview.Log.Info(string.format("ExtractPilots: Extracted %d pilots", #pilots))
    
    return pilots
end

----------------------------------------------------------------
-- Extract pilot information from object handle
----------------------------------------------------------------

function PilotExtractor.ExtractPilotInfo(objectHandle)
    if not objectHandle or objectHandle == 0 then
        return nil
    end
    
    -- Get object tags to filter only aircraft/pilots
    local tags = Tacview.Telemetry.GetCurrentTags(objectHandle)
    
    -- Filter: Only extract aircraft (fixed-wing or rotorcraft)
    local isAircraft = (tags and (
        (tags & Tacview.Telemetry.Tags.FixedWing) ~= 0 or
        (tags & Tacview.Telemetry.Tags.Rotorcraft) ~= 0
    ))
    
    if not isAircraft then
        return nil
    end
    
    -- Get object ID (hex format for unique identification)
    local objectId = Tacview.Telemetry.GetObjectId(objectHandle)
    local pilotId = string.format("%X", objectId or 0)
    
    -- Get pilot name (use Pilot property, Name property, or short name as fallback)
    local pilotPropertyIndex = Tacview.Telemetry.GetObjectsTextPropertyIndex("Pilot", false)
    local namePropertyIndex = Tacview.Telemetry.GetObjectsTextPropertyIndex("Name", false)
    
    local pilotName = nil
    
    if pilotPropertyIndex and pilotPropertyIndex ~= Tacview.Telemetry.InvalidPropertyIndex then
        pilotName = Tacview.Telemetry.GetTextSample(objectHandle, Tacview.Context.GetAbsoluteTime(), pilotPropertyIndex)
    end
    
    if not pilotName or pilotName == "" then
        if namePropertyIndex and namePropertyIndex ~= Tacview.Telemetry.InvalidPropertyIndex then
            pilotName = Tacview.Telemetry.GetTextSample(objectHandle, Tacview.Context.GetAbsoluteTime(), namePropertyIndex)
        end
    end
    
    if not pilotName or pilotName == "" then
        pilotName = Tacview.Telemetry.GetCurrentShortName(objectHandle) or pilotId
    end
    
    -- Get coalition from tags
    local coalition = "Neutral"
    if tags then
        -- Coalition detection based on tags (simplified)
        if (tags & Tacview.Telemetry.Tags.Air) ~= 0 then
            -- Use object properties or heuristics to determine coalition
            -- For now, default to neutral unless we can determine otherwise
            coalition = "Blue" -- Default assumption for aircraft
        end
    end
    
    -- Get aircraft type
    local unitType = Tacview.Telemetry.GetCurrentShortName(objectHandle) or "Unknown Aircraft"
    
    -- Extract radio frequencies (use default frequencies for now)
    local frequencies = PilotExtractor.ExtractFrequencies(objectHandle, coalition)
    
    -- Create pilot info structure (matches protocol format)
    local pilotInfo = {
        pilot_id = pilotId,
        pilot_name = pilotName,
        coalition = coalition,
        unit_type = unitType,
        enabled_frequencies = frequencies -- All frequencies enabled by default
    }
    
    return pilotInfo
end

----------------------------------------------------------------
-- Get coalition for object (legacy function, kept for compatibility)
----------------------------------------------------------------

function PilotExtractor.GetCoalition(objectHandle)
    local tags = Tacview.Telemetry.GetCurrentTags(objectHandle)
    
    if not tags then
        return "Neutral"
    end
    
    -- Coalition detection is not directly exposed in Tacview 1.9.0 tags
    -- We'll use heuristics or default to Blue for now
    return "Blue" -- Simplified default
end

----------------------------------------------------------------
-- Extract radio frequencies from object
----------------------------------------------------------------

function PilotExtractor.ExtractFrequencies(objectHandle, coalition)
    local frequencies = {}
    
    -- Try to get radio frequencies from object properties
    local radio1Index = Tacview.Telemetry.GetObjectsNumericPropertyIndex("Radio1", false)
    local radio2Index = Tacview.Telemetry.GetObjectsNumericPropertyIndex("Radio2", false)
    
    if radio1Index and radio1Index ~= Tacview.Telemetry.InvalidPropertyIndex then
        local freq1 = Tacview.Telemetry.GetNumericSample(objectHandle, Tacview.Context.GetAbsoluteTime(), radio1Index)
        if freq1 and freq1 > 0 then
            table.insert(frequencies, freq1)
        end
    end
    
    if radio2Index and radio2Index ~= Tacview.Telemetry.InvalidPropertyIndex then
        local freq2 = Tacview.Telemetry.GetNumericSample(objectHandle, Tacview.Context.GetAbsoluteTime(), radio2Index)
        if freq2 and freq2 > 0 then
            table.insert(frequencies, freq2)
        end
    end
    
    -- If no frequencies found, use common DCS frequencies as fallback based on coalition
    if #frequencies == 0 then
        if coalition == "Blue" then
            frequencies = {251000000, 305000000} -- NATO frequencies (Hz)
        elseif coalition == "Red" then
            frequencies = {124000000, 264000000} -- Soviet/Russian frequencies (Hz)
        else
            frequencies = {243000000} -- Guard frequency (Hz)
        end
    end
    
    return frequencies
end

return PilotExtractor
