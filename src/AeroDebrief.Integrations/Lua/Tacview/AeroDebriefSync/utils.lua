-- Utility Functions for AeroDebrief Sync

local Utils = {}

----------------------------------------------------------------
-- Check if a table contains a value
----------------------------------------------------------------

function Utils.Contains(table, value)
    for _, v in ipairs(table) do
        if v == value then
            return true
        end
    end
    return false
end

----------------------------------------------------------------
-- Deep copy a table
----------------------------------------------------------------

function Utils.DeepCopy(original)
    local copy
    if type(original) == 'table' then
        copy = {}
        for key, value in next, original, nil do
            copy[Utils.DeepCopy(key)] = Utils.DeepCopy(value)
        end
        setmetatable(copy, Utils.DeepCopy(getmetatable(original)))
    else
        copy = original
    end
    return copy
end

----------------------------------------------------------------
-- Format number with fixed decimal places
----------------------------------------------------------------

function Utils.FormatNumber(number, decimals)
    return string.format("%." .. decimals .. "f", number)
end

----------------------------------------------------------------
-- Convert Hz to MHz with formatting
----------------------------------------------------------------

function Utils.FormatFrequency(frequencyHz)
    local frequencyMHz = frequencyHz / 1000000.0
    return string.format("%.3f MHz", frequencyMHz)
end

----------------------------------------------------------------
-- Clamp value between min and max
----------------------------------------------------------------

function Utils.Clamp(value, min, max)
    if value < min then
        return min
    elseif value > max then
        return max
    else
        return value
    end
end

----------------------------------------------------------------
-- Linear interpolation
----------------------------------------------------------------

function Utils.Lerp(a, b, t)
    return a + (b - a) * t
end

----------------------------------------------------------------
-- Check if two numbers are approximately equal
----------------------------------------------------------------

function Utils.AlmostEqual(a, b, epsilon)
    epsilon = epsilon or 0.0001
    return math.abs(a - b) < epsilon
end

----------------------------------------------------------------
-- Get table size (for non-array tables)
----------------------------------------------------------------

function Utils.TableSize(table)
    local count = 0
    for _ in pairs(table) do
        count = count + 1
    end
    return count
end

----------------------------------------------------------------
-- Merge two tables (shallow copy)
----------------------------------------------------------------

function Utils.MergeTables(t1, t2)
    local result = {}
    
    for k, v in pairs(t1) do
        result[k] = v
    end
    
    for k, v in pairs(t2) do
        result[k] = v
    end
    
    return result
end

----------------------------------------------------------------
-- Convert table to string for debugging
----------------------------------------------------------------

function Utils.TableToString(table, indent)
    indent = indent or 0
    local spacing = string.rep("  ", indent)
    local result = "{\n"
    
    for key, value in pairs(table) do
        result = result .. spacing .. "  " .. tostring(key) .. " = "
        
        if type(value) == "table" then
            result = result .. Utils.TableToString(value, indent + 1)
        else
            result = result .. tostring(value)
        end
        
        result = result .. ",\n"
    end
    
    result = result .. spacing .. "}"
    return result
end

return Utils
