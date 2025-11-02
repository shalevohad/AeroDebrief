-- Protocol Handler for AeroDebrief Sync
-- Encodes/decodes JSON messages

local Protocol = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil
local JSON = nil

function Protocol.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
    
    -- Now that we have Tacview, initialize JSON
    InitializeJSON()
end

-- Initialize JSON library (called after SetTacview)
function InitializeJSON()
    -- JSON library: try Tacview's built-in first, then common module names, then fall back to embedded

    -- First try to get JSON from Tacview's global scope (most common)
    if _G.JSON then
        JSON = _G.JSON
        Tacview.Log.Info("AeroDebrief Sync: Using Tacview's built-in JSON")
        return
    end

    -- Try common module names
    local tried = {}
    local function try_require(name)
        local ok, res = pcall(require, name)
        table.insert(tried, { name = name, ok = ok })
        if ok and res then return res end
        return nil
    end

    JSON = try_require("JSON") or try_require("json") or try_require("dkjson") or try_require("cjson")
    
    if JSON then
        Tacview.Log.Info("AeroDebrief Sync: Using external JSON module")
        return
    end

    -- Embedded minimal JSON implementation (rxi/json equivalent, MIT license)
    Tacview.Log.Info("AeroDebrief Sync: Using embedded JSON fallback")
    JSON = {}

    local encode
    do
        local escape_char_map = {
            ["\""] = '\\"',
            ["\\"] = "\\\\",
            ["\b"] = "\\b",
            ["\f"] = "\\f",
            ["\n"] = "\\n",
            ["\r"] = "\\r",
            ["\t"] = "\\t",
        }
        local function escape_str(s)
            return s:gsub('[%z\"\\%b\b\f\n\r\t]', function(c)
                return escape_char_map[c] or string.format("\\u%04x", c:byte())
            end)
        end

        local function is_array(t)
            -- crude check: keys are 1..n
            local n = 0
            for k, _ in pairs(t) do
                if type(k) ~= "number" then return false end
                if k > n then n = k end
            end
            for i = 1, n do
                if t[i] == nil then return false end
            end
            return true
        end

        function encode(v)
            local t = type(v)
            if t == "nil" then
                return "null"
            elseif t == "boolean" then
                return tostring(v)
            elseif t == "number" then
                return tostring(v)
            elseif t == "string" then
                return '"' .. escape_str(v) .. '"'
            elseif t == "table" then
                local is_arr = is_array(v)
                local parts = {}
                if is_arr then
                    for i = 1, #v do
                        table.insert(parts, encode(v[i]))
                    end
                    return '[' .. table.concat(parts, ',') .. ']'
                else
                    for k, val in pairs(v) do
                        if type(k) ~= 'string' then
                            -- only string keys accepted in JSON object
                        else
                            table.insert(parts, encode(k) .. ':' .. encode(val))
                        end
                    end
                    return '{' .. table.concat(parts, ',') .. '}'
                end
            else
                return 'null'
            end
        end
    end

    local decode
    do
        -- A small recursive descent JSON parser suitable for expected message shapes
        local pos
        local s
        local function skip_ws()
            local _, e = s:find('^[ \n\r\t]+', pos)
            if e then pos = e + 1 end
        end
        local function peek()
            return s:sub(pos, pos)
        end
        local function consume(expected)
            if s:sub(pos, pos + #expected - 1) == expected then
                pos = pos + #expected
                return true
            end
            return false
        end
        local function parse_null()
            if consume('null') then return nil end
            error('invalid null at ' .. pos)
        end
        local function parse_true()
            if consume('true') then return true end
            error('invalid true at ' .. pos)
        end
        local function parse_false()
            if consume('false') then return false end
            error('invalid false at ' .. pos)
        end
        local function parse_number()
            local num = s:match('^-?%d+%.?%d*[eE]?[-+]?%d*', pos)
            if not num then error('invalid number at ' .. pos) end
            pos = pos + #num
            local n = tonumber(num)
            return n
        end
        local function parse_string()
            if peek() ~= '"' then error('expected string at ' .. pos) end
            pos = pos + 1
            local res = {}
            while true do
                local c = peek()
                if c == '' then error('unterminated string') end
                if c == '"' then pos = pos + 1 break end
                if c == '\\' then
                    pos = pos + 1
                    local esc = peek()
                    local map = { ['"'] = '"', ['\\'] = '\\', ['/'] = '/', ['b'] = '\b', ['f'] = '\f', ['n'] = '\n', ['r'] = '\r', ['t'] = '\t' }
                    if map[esc] then
                        table.insert(res, map[esc])
                        pos = pos + 1
                    elseif esc == 'u' then
                        local hex = s:sub(pos+1, pos+4)
                        if not hex:match('%x%x%x%x') then error('invalid unicode') end
                        local code = tonumber(hex, 16)
                        table.insert(res, utf8.char(code))
                        pos = pos + 5
                    else
                        error('invalid escape ' .. tostring(esc))
                    end
                else
                    table.insert(res, c)
                    pos = pos + 1
                end
            end
            return table.concat(res)
        end
        local function parse_array()
            -- assume current char is '['
            pos = pos + 1
            local arr = {}
            skip_ws()
            if peek() == ']' then pos = pos + 1 return arr end
            while true do
                skip_ws()
                local val = parse_value()
                table.insert(arr, val)
                skip_ws()
                if peek() == ']' then pos = pos + 1 break end
                if peek() == ',' then pos = pos + 1 else error('expected , or ] at ' .. pos) end
            end
            return arr
        end
        local function parse_object()
            pos = pos + 1 -- skip '{'
            local obj = {}
            skip_ws()
            if peek() == '}' then pos = pos + 1 return obj end
            while true do
                skip_ws()
                local key = parse_string()
                skip_ws()
                if peek() ~= ':' then error('expected : at ' .. pos) end
                pos = pos + 1
                skip_ws()
                local val = parse_value()
                obj[key] = val
                skip_ws()
                if peek() == '}' then pos = pos + 1 break end
                if peek() == ',' then pos = pos + 1 else error('expected , or } at ' .. pos) end
            end
            return obj
        end
        function parse_value()
            skip_ws()
            local ch = peek()
            if ch == '"' then return parse_string() end
            if ch == '{' then return parse_object() end
            if ch == '[' then return parse_array() end
            if ch == 'n' then return parse_null() end
            if ch == 't' then return parse_true() end
            if ch == 'f' then return parse_false() end
            return parse_number()
        end
        function decode(str)
            s = str
            pos = 1
            local ok, res = pcall(function()
                local v = parse_value()
                skip_ws()
                if pos <= #s then error('trailing chars') end
                return v
            end)
            if ok then return res end
            return nil, res
        end
    end

    JSON.encode = function(v)
        return encode(v)
    end
    JSON.decode = function(s)
        local ok, res = pcall(function() return decode(s) end)
        if ok then return res end
        error(res)
    end
end

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
