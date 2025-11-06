-- Configuration Manager for AeroDebrief Sync

local Config = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function Config.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

-- Default configuration values
local DEFAULT_PORT = 52001
local DEFAULT_BIND_ADDRESS = "127.0.0.1"
local DEFAULT_UPDATE_RATE = 10
local DEFAULT_AUTO_RECONNECT = true

-- Configuration file path
local CONFIG_FILE = "config.txt"

-- Current configuration
Config.Port = DEFAULT_PORT
Config.BindAddress = DEFAULT_BIND_ADDRESS
Config.UpdateRate = DEFAULT_UPDATE_RATE
Config.AutoReconnect = DEFAULT_AUTO_RECONNECT

----------------------------------------------------------------
-- Initialize configuration (load from file if exists)
----------------------------------------------------------------

function Config.Initialize()
    -- Try to load configuration from file
    local success, config = pcall(Config.LoadFromFile)
    if success and config then
        Config.Port = config.Port or DEFAULT_PORT
        Config.BindAddress = config.BindAddress or DEFAULT_BIND_ADDRESS
        Config.UpdateRate = config.UpdateRate or DEFAULT_UPDATE_RATE
        Config.AutoReconnect = config.AutoReconnect ~= nil and config.AutoReconnect or DEFAULT_AUTO_RECONNECT
        
        Tacview.Log.Info(string.format("AeroDebrief Sync: Configuration loaded (port=%d)", Config.Port))
    else
        -- Use defaults
        Tacview.Log.Info("AeroDebrief Sync: Using default configuration")
    end
end

----------------------------------------------------------------
-- Load configuration from file
----------------------------------------------------------------

function Config.LoadFromFile()
    local addonPath = Tacview.AddOns.Current.GetPath()
    local configPath = addonPath .. CONFIG_FILE
    
    local file = io.open(configPath, "r")
    if not file then
        return nil
    end
    
    local config = {}
    for line in file:lines() do
        local key, value = line:match("^([^=]+)=(.+)$")
        if key and value then
            key = key:match("^%s*(.-)%s*$") -- trim whitespace
            value = value:match("^%s*(.-)%s*$")
            
            if key == "Port" then
                config.Port = tonumber(value)
            elseif key == "BindAddress" then
                config.BindAddress = value
            elseif key == "UpdateRate" then
                config.UpdateRate = tonumber(value)
            elseif key == "AutoReconnect" then
                config.AutoReconnect = value == "true"
            end
        end
    end
    
    file:close()
    return config
end

----------------------------------------------------------------
-- Save configuration to file
----------------------------------------------------------------

function Config.Save()
    local addonPath = Tacview.AddOns.Current.GetPath()
    local configPath = addonPath .. CONFIG_FILE
    
    local file = io.open(configPath, "w")
    if not file then
        Tacview.Log.Error("AeroDebrief Sync: Failed to save configuration")
        return false
    end
    
    file:write(string.format("Port=%d\n", Config.Port))
    file:write(string.format("BindAddress=%s\n", Config.BindAddress))
    file:write(string.format("UpdateRate=%d\n", Config.UpdateRate))
    file:write(string.format("AutoReconnect=%s\n", tostring(Config.AutoReconnect)))
    
    file:close()
    
    Tacview.Log.Info("AeroDebrief Sync: Configuration saved")
    return true
end

----------------------------------------------------------------
-- Validate configuration values
----------------------------------------------------------------

function Config.Validate()
    if Config.Port < 1 or Config.Port > 65535 then
        return false, "Port must be between 1 and 65535"
    end
    
    if Config.UpdateRate < 1 or Config.UpdateRate > 100 then
        return false, "Update rate must be between 1 and 100 Hz"
    end
    
    return true
end

return Config
