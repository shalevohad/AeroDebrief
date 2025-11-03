-- TCP Server for AeroDebrief Sync
-- Handles TCP connections from AeroDebrief clients

local TcpServer = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function TcpServer.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

-- State variables (MUST be declared at module level)
local serverSocket = nil
local clients = {}
local isRunning = false
local messageHandler = nil

-- Ensure the addon's `lib` and `socket` directories are on package.path/package.cpath so bundled LuaSocket can be found.
local function setupPaths()
    -- Tacview 1.9.0+ includes LuaSocket by default
    -- No manual path setup needed for modern Tacview versions
    
    -- For older versions or custom installs, try to detect addon path
    if Tacview and Tacview.AddOns and Tacview.AddOns.Current then
        local addonPath = Tacview.AddOns.Current.GetPath()
        if addonPath then
            package.path = package.path .. ";" .. addonPath .. "/?.lua"
            package.cpath = package.cpath .. ";" .. addonPath .. "/?.dll"
            Tacview.Log.Debug("Updated package paths for addon: " .. addonPath)
        end
    end
end

----------------------------------------------------------------
-- Start TCP Server
----------------------------------------------------------------

function TcpServer.Start(port, bindAddress)
    if isRunning then
        Tacview.Log.Warning("TCP server already running")
        return false, "Server is already running"
    end
    
    -- Try to load LuaSocket
    setupPaths()
    
    local socketStatus, socket = pcall(require, "socket")
    if not socketStatus then
        local errorMsg = "Failed to load LuaSocket: " .. tostring(socket)
        Tacview.Log.Error("AeroDebrief Sync: " .. errorMsg)
        Tacview.Log.Error("Make sure you have Tacview 1.9.0 or later")
        return false, errorMsg
    end
    
    Tacview.Log.Info("LuaSocket loaded successfully (version: " .. tostring(socket._VERSION or "unknown") .. ")")
    
    -- Save socket reference for later use
    TcpServer.socket = socket
    
    -- Create TCP server socket
    local serverSock, err = socket.tcp()
    if not serverSock then
        local errorMsg = "Failed to create TCP socket: " .. tostring(err)
        Tacview.Log.Error("AeroDebrief Sync: " .. errorMsg)
        return false, errorMsg
    end
    
    serverSocket = serverSock
    
    -- Set socket options
    serverSocket:settimeout(0)  -- Non-blocking
    serverSocket:setoption("reuseaddr", true)  -- Allow quick restart
    
    -- Bind to address and port
    local success, bindErr = serverSocket:bind(bindAddress, port)
    if not success then
        serverSocket:close()
        serverSocket = nil
        local errorMsg = string.format("Failed to bind to %s:%d - %s", bindAddress, port, tostring(bindErr))
        Tacview.Log.Error("AeroDebrief Sync: " .. errorMsg)
        Tacview.Log.Error("Is another application using port " .. port .. "?")
        return false, errorMsg
    end
    
    -- Start listening
    success, err = serverSocket:listen(5)
    if not success then
        serverSocket:close()
        serverSocket = nil
        local errorMsg = "Failed to listen on socket: " .. tostring(err)
        Tacview.Log.Error("AeroDebrief Sync: " .. errorMsg)
        return false, errorMsg
    end
    
    isRunning = true
    clients = {}  -- Reset client list
    
    Tacview.Log.Info(string.format("? TCP server listening on %s:%d", bindAddress, port))
    
    return true
end

----------------------------------------------------------------
-- Stop TCP Server
----------------------------------------------------------------

function TcpServer.Stop()
    if not isRunning then
        return
    end
    
    -- Close all client connections
    for _, client in ipairs(clients) do
        if client.socket then
            client.socket:close()
        end
    end
    clients = {}
    
    -- Close server socket
    if serverSocket then
        serverSocket:close()
        serverSocket = nil
    end
    
    isRunning = false
    Tacview.Log.Info("AeroDebrief Sync: TCP server stopped")
end

----------------------------------------------------------------
-- Update (process connections and messages)
----------------------------------------------------------------

function TcpServer.Update()
    if not isRunning then
        return
    end
    
    -- Accept new connections
    local clientSocket = serverSocket:accept()
    if clientSocket then
        clientSocket:settimeout(0) -- non-blocking
        
        local client = {
            socket = clientSocket,
            buffer = ""
        }
        
        table.insert(clients, client)
        
        local peerIp, peerPort = clientSocket:getpeername()
        -- Use Info level to ensure visibility (DBG level might be filtered)
        Tacview.Log.Info(string.format("==> CLIENT CONNECTED from %s:%s (Total clients: %d)", 
            peerIp or "unknown", peerPort or "unknown", #clients))
    end
    
    -- Process existing clients
    local i = 1
    while i <= #clients do
        local client = clients[i]
        local keepClient = TcpServer.ProcessClient(client)
        
        if keepClient then
            i = i + 1
        else
            -- Remove disconnected client
            if client.socket then
                client.socket:close()
            end
            table.remove(clients, i)
            Tacview.Log.Info(string.format("==> CLIENT DISCONNECTED (Remaining clients: %d)", #clients))
        end
    end
end

----------------------------------------------------------------
-- Process individual client
----------------------------------------------------------------

function TcpServer.ProcessClient(client)
    -- Try to receive data
    local data, err, partial = client.socket:receive("*a")
    
    if err == "closed" then
        return false -- Client disconnected
    end
    
    -- Append received data to buffer
    if data then
        client.buffer = client.buffer .. data
    elseif partial then
        client.buffer = client.buffer .. partial
    end
    
    -- Process complete messages (newline-delimited)
    while true do
        local newlinePos = client.buffer:find("\n")
        if not newlinePos then
            break
        end
        
        local message = client.buffer:sub(1, newlinePos - 1)
        client.buffer = client.buffer:sub(newlinePos + 1)
        
        -- Trim whitespace
        message = message:match("^%s*(.-)%s*$")
        
        if message ~= "" then
            -- Call message handler
            if messageHandler then
                local success, err = pcall(messageHandler, message)
                if not success then
                    Tacview.Log.Error("AeroDebrief Sync: Error in message handler: " .. tostring(err))
                end
            end
        end
    end
    
    return true -- Keep client
end

----------------------------------------------------------------
-- Broadcast message to all connected clients
----------------------------------------------------------------

function TcpServer.Broadcast(message)
    if not isRunning then
        Tacview.Log.Debug("Cannot broadcast - server not running")
        return false
    end
    
    if #clients == 0 then
        -- This is normal if no clients connected yet
        Tacview.Log.Debug("No clients connected - skipping broadcast")
        return true
    end
    
    -- Ensure message ends with newline
    if not message:match("\n$") then
        message = message .. "\n"
    end
    
    -- Track broadcast count
    if not TcpServer.broadcastCount then
        TcpServer.broadcastCount = 0
    end
    TcpServer.broadcastCount = TcpServer.broadcastCount + 1
    
    -- Log first 10 broadcasts, then every 50th to verify messages are being sent
    if TcpServer.broadcastCount <= 10 or TcpServer.broadcastCount % 50 == 0 then
        -- Truncate message for logging (first 150 chars)
        local msgPreview = message:sub(1, 150):gsub("\n", "\\n")
        Tacview.Log.Info(string.format(
            "?? BROADCAST #%d to %d client(s): %s%s", 
            TcpServer.broadcastCount, 
            #clients, 
            msgPreview,
            #message > 150 and "..." or ""
        ))
    end
    
    -- Send to all clients
    local successCount = 0
    local failCount = 0
    
    for i, client in ipairs(clients) do
        local bytesSent, err = client.socket:send(message)
        if bytesSent then
            successCount = successCount + 1
        else
            failCount = failCount + 1
            Tacview.Log.Warning(string.format(
                "? Failed to send to client #%d: %s (error: %s)", 
                i, tostring(err), tostring(err)
            ))
        end
    end
    
    -- Log summary if there were failures
    if failCount > 0 then
        Tacview.Log.Warning(string.format(
            "Broadcast #%d: %d succeeded, %d failed", 
            TcpServer.broadcastCount, successCount, failCount
        ))
    end
    
    return successCount > 0
end

----------------------------------------------------------------
-- Set message handler callback
----------------------------------------------------------------

function TcpServer.SetMessageHandler(handler)
    messageHandler = handler
end

----------------------------------------------------------------
-- Get connection status
----------------------------------------------------------------

function TcpServer.IsRunning()
    return isRunning
end

function TcpServer.GetClientCount()
    return #clients
end

return TcpServer
