-- State Manager for AeroDebrief Sync
-- Tracks state changes to avoid spamming updates

local StateManager = {}

-- Previous state
local lastMissionTime = nil
local lastPlaybackState = nil
local lastPlaybackSpeed = nil
local lastUpdateTime = 0  -- Track when we last sent update
local lastRealTime = 0  -- Track real clock time for speed calculation

-- Update rate configuration
-- NOTE: This will be set by main.lua using SetUpdateRate()
-- Default is 10 Hz (100ms) but can be changed by user via Tacview UI
local UPDATE_INTERVAL = 0.1  -- Default: 10 Hz = 0.1 seconds = 100ms

-- Calculated playback speed (based on real-time vs mission-time delta)
local calculatedPlaybackSpeed = 1.0

----------------------------------------------------------------
-- Calculate effective playback speed by comparing deltas
----------------------------------------------------------------

local function CalculatePlaybackSpeed(currentMissionTime, currentRealTime, playbackState)
    -- Only calculate when playing
    if playbackState ~= "playing" then
        return 1.0
    end
    
    -- Need previous values to calculate
    if not lastMissionTime or not lastRealTime then
        return 1.0
    end
    
    -- Calculate time deltas
    local realTimeDelta = currentRealTime - lastRealTime
    local missionTimeDelta = currentMissionTime - lastMissionTime
    
    -- Avoid division by zero and very small intervals
    if realTimeDelta < 0.05 then  -- Less than 50ms, too small to be accurate
        return calculatedPlaybackSpeed  -- Return previous value
    end
    
    -- Calculate speed: mission time change / real time change
    -- Example: If 2 seconds of mission time passed in 1 second of real time = 2x speed
    local speed = missionTimeDelta / realTimeDelta
    
    -- Sanity check: reasonable speed range (0.1x to 16x)
    if speed < 0.1 or speed > 16.0 then
        return calculatedPlaybackSpeed  -- Return previous value if unreasonable
    end
    
    -- Smooth the speed calculation to avoid jitter (exponential moving average)
    calculatedPlaybackSpeed = calculatedPlaybackSpeed * 0.7 + speed * 0.3
    
    return calculatedPlaybackSpeed
end

----------------------------------------------------------------
-- Check if state has changed OR enough time has passed
----------------------------------------------------------------

function StateManager.HasStateChanged(missionTime, playbackState, playbackSpeed)
    local currentTime = os.clock()  -- Get current time for rate limiting
    
    -- Calculate effective playback speed based on time deltas
    local effectiveSpeed = CalculatePlaybackSpeed(missionTime, currentTime, playbackState)
    
    -- First update - always send
    if lastMissionTime == nil then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        calculatedPlaybackSpeed = 1.0
        return true
    end
    
    -- Playback state changed (playing <-> paused)
    -- CRITICAL: Always broadcast immediately on state change
    if playbackState ~= lastPlaybackState then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        return true
    end
    
    -- Playback speed changed significantly (>5% difference)
    -- NOTE: playbackSpeed parameter is always 1.0 (API limitation)
    -- But we calculate effective speed from time deltas
    if math.abs(effectiveSpeed - (lastPlaybackSpeed or 1.0)) > 0.05 then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        -- Update the stored speed for next comparison
        lastPlaybackSpeed = effectiveSpeed
        return true
    end
    
    -- Mission time changed significantly (seeking or time progressing)
    -- CRITICAL: Always broadcast immediately on seek, even when paused
    -- Detect seeks by checking if time jumped by more than expected
    local expectedTimeDelta = UPDATE_INTERVAL * (playbackState == "playing" and effectiveSpeed or 0.0)
    local actualTimeDelta = math.abs(missionTime - (lastMissionTime or missionTime))
    
    -- If time changed by more than 0.5 seconds, it's a seek
    if actualTimeDelta > 0.5 then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        calculatedPlaybackSpeed = 1.0  -- Reset speed calculation after seek
        return true
    end
    
    -- If time changed at all while paused, it's a seek
    if playbackState == "paused" and actualTimeDelta > 0.01 then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        calculatedPlaybackSpeed = 1.0  -- Reset speed calculation after seek
        return true
    end
    
    -- Time-based periodic update at configured rate
    -- Send at user-configured rate to keep client synchronized
    -- This ensures AeroDebrief always knows current position
    -- Rate is configurable via Tacview UI menu (default 10 Hz = 100ms)
    local timeSinceLastUpdate = currentTime - lastUpdateTime
    if timeSinceLastUpdate >= UPDATE_INTERVAL then
        lastUpdateTime = currentTime
        lastRealTime = currentTime
        return true
    end
    
    return false
end

----------------------------------------------------------------
-- Update stored state
----------------------------------------------------------------

function StateManager.UpdateState(missionTime, playbackState, playbackSpeed)
    lastMissionTime = missionTime
    lastPlaybackState = playbackState
    -- Store the calculated effective speed, not the parameter
    lastPlaybackSpeed = calculatedPlaybackSpeed
end

----------------------------------------------------------------
-- Get calculated playback speed (for use in protocol messages)
----------------------------------------------------------------

function StateManager.GetCalculatedPlaybackSpeed()
    return calculatedPlaybackSpeed
end

----------------------------------------------------------------
-- Set update rate (Hz) - Called by main.lua with user's configured rate
----------------------------------------------------------------

function StateManager.SetUpdateRate(hz)
    if hz and hz > 0 and hz <= 60 then
        UPDATE_INTERVAL = 1.0 / hz
    else
        -- Use default if invalid
        UPDATE_INTERVAL = 0.1  -- 10 Hz default
    end
end

----------------------------------------------------------------
-- Get current update interval (for diagnostics)
----------------------------------------------------------------

function StateManager.GetUpdateInterval()
    return UPDATE_INTERVAL
end

----------------------------------------------------------------
-- Get current update rate in Hz (for diagnostics)
----------------------------------------------------------------

function StateManager.GetUpdateRate()
    if UPDATE_INTERVAL > 0 then
        return 1.0 / UPDATE_INTERVAL
    end
    return 10  -- Default
end

----------------------------------------------------------------
-- Reset state (useful after reconnection)
----------------------------------------------------------------

function StateManager.Reset()
    lastMissionTime = nil
    lastPlaybackState = nil
    lastPlaybackSpeed = nil
    lastUpdateTime = 0
    lastRealTime = 0
    calculatedPlaybackSpeed = 1.0
end

return StateManager
