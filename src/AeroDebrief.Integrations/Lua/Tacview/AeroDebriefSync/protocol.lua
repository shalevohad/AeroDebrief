-- Protocol Handler for AeroDebrief Sync
-- Encodes/decodes JSON messages

local Protocol = {}

-- JSON library (Tacview provides this)
local JSON = require("JSON")

----------------------------------------------------------------
-- Encode message to JSON string
----------------------------------------------------------------

function Protocol.Encode(message)
    local success, json = pcall(JSON.encode, message)
    if success then
        return json
    else
        Tacview.Log.Error("AeroDebrief Sync: Failed to encode JSON: " .. tostring(json))
        return nil
    end
end

----------------------------------------------------------------
-- Decode JSON string to message table
----------------------------------------------------------------

function Protocol.Decode(jsonString)
    local success, message = pcall(JSON.decode, jsonString)
    if success then
        return message
    else
        Tacview.Log.Error("AeroDebrief Sync: Failed to decode JSON: " .. tostring(message))
        return nil
    end
end

----------------------------------------------------------------
-- Create time update message
----------------------------------------------------------------

function Protocol.CreateTimeUpdate(missionTime, playbackState, playbackSpeed)
    -- Convert mission time to UTC ISO 8601 format
    local timeUtc = os.date("!%Y-%m-%dT%H:%M:%SZ", missionTime)
    
    local message = {
        type = "time_update",
        mission_time_utc = timeUtc,
        playback_state = playbackState,
        playback_speed = playbackSpeed
    }
    
    return Protocol.Encode(message)
end

----------------------------------------------------------------
-- Create pilot selection message
----------------------------------------------------------------

function Protocol.CreatePilotSelection(pilots, panMode, generalEnabledFrequencies)
    local message = {
        type = "pilot_selection",
        selected_pilots = pilots,
        pan_mode = panMode or "auto",
        general_enabled_frequencies = generalEnabledFrequencies or {}
    }
    
    return Protocol.Encode(message)
end

----------------------------------------------------------------
-- Create playback command message
----------------------------------------------------------------

function Protocol.CreatePlaybackCommand(command)
    local message = {
        type = "playback_command",
        command = command
    }
    
    return Protocol.Encode(message)
end

----------------------------------------------------------------
-- Create seek message
----------------------------------------------------------------

function Protocol.CreateSeek(targetTime)
    -- Convert target time to UTC ISO 8601 format
    local timeUtc = os.date("!%Y-%m-%dT%H:%M:%SZ", targetTime)
    
    local message = {
        type = "seek",
        target_time_utc = timeUtc
    }
    
    return Protocol.Encode(message)
end

return Protocol
