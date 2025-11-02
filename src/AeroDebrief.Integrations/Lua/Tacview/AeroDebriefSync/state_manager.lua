-- State Manager for AeroDebrief Sync
-- Tracks state changes to avoid spamming updates

local StateManager = {}

-- Previous state
local lastMissionTime = nil
local lastIsPlaying = nil
local lastPlaybackSpeed = nil

-- Threshold for time drift to trigger update (in seconds)
local TIME_DRIFT_THRESHOLD = 0.5

----------------------------------------------------------------
-- Check if state has changed significantly
----------------------------------------------------------------

function StateManager.HasStateChanged(missionTime, isPlaying, playbackSpeed)
    -- First update - always send
    if lastMissionTime == nil then
        return true
    end
    
    -- Playback state changed
    if isPlaying ~= lastIsPlaying then
        return true
    end
    
    -- Playback speed changed
    if math.abs(playbackSpeed - lastPlaybackSpeed) > 0.01 then
        return true
    end
    
    -- Time drift detected (only check if playing)
    if isPlaying then
        local timeDrift = math.abs(missionTime - lastMissionTime)
        if timeDrift > TIME_DRIFT_THRESHOLD then
            return true
        end
    end
    
    return false
end

----------------------------------------------------------------
-- Update stored state
----------------------------------------------------------------

function StateManager.UpdateState(missionTime, isPlaying, playbackSpeed)
    lastMissionTime = missionTime
    lastIsPlaying = isPlaying
    lastPlaybackSpeed = playbackSpeed
end

----------------------------------------------------------------
-- Reset state (useful after reconnection)
----------------------------------------------------------------

function StateManager.Reset()
    lastMissionTime = nil
    lastIsPlaying = nil
    lastPlaybackSpeed = nil
end

return StateManager
