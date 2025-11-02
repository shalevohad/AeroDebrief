-- TCP Server for AeroDebrief Sync
-- Handles TCP connections from AeroDebrief clients

local TcpServer = {}

-- Tacview API (will be set by main.lua)
local Tacview = nil

function TcpServer.SetTacview(tacviewInstance)
    Tacview = tacviewInstance
end

-- Ensure the addon's `lib` and `socket` directories are on package.path/package.cpath so bundled LuaSocket can be found.
local function setupPaths()
    -- Addon's root directory
    local addonPath = "D:/Games/DCS World OpenBeta/Game/DCS-BIOS/" --@TODO: Make this dynamic?

    package.path = package.path .. ";" .. addonPath .. "Lua/?.lua"
    package.cpath = package.cpath .. ";" .. addonPath .. "Lua/?.dll"

    -- Log the updated paths for debugging
    Tacview.Log.Info("Updated package.path: " .. package.path)
    Tacview.Log.Info("Updated package.cpath: " .. package.cpath)
end

----------------------------------------------------------------
-- Start TCP Server
----------------------------------------------------------------

function TcpServer.Start(port, bindAddress)
    if isRunning then
        return false, "Server is already running"
    end
    
    setupPaths() -- Ensure paths are set before loading LuaSocket
    
    -- Load LuaSocket
    local socketStatus, socket = pcall(require, "socket")
    if not socketStatus then
        return false, "Failed to load LuaSocket: " .. tostring(socket)
    end

    -- Save the socket reference
    TcpServer.socket = socket
    
    -- Create TCP server socket
    serverSocket = socket.tcp()
    if not serverSocket then
        return false, "Failed to create TCP socket"
    end
    
    -- Set socket to non-blocking mode
    serverSocket:settimeout(0)
    
    -- Bind to address and port
    local success, err = serverSocket:bind(bindAddress, port)
    if not success then
        serverSocket:close()
        serverSocket = nil
        return false, "Failed to bind to " .. bindAddress .. ":" .. port .. " - " .. tostring(err)
    end
    
    -- Start listening for connections
    success, err = serverSocket:listen(5)
    if not success then
        serverSocket:close()
        serverSocket = nil
        return false, "Failed to listen on socket: " .. tostring(err)
    end
    
    isRunning = true
    Tacview.Log.Info(string.format("AeroDebrief Sync: TCP server listening on %s:%d", bindAddress, port))
    
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
        Tacview.Log.Info(string.format("AeroDebrief Sync: Client connected from %s:%s", peerIp or "unknown", peerPort or "unknown"))
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
            Tacview.Log.Info("AeroDebrief Sync: Client disconnected")
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
                local success, err = pcall(messageHandler, client, message)
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
    if not isRunning or #clients == 0 then
        return
    end
    
    -- Ensure message ends with newline
    if not message:match("\n$") then
        message = message .. "\n"
    end
    
    -- Send to all clients
    for _, client in ipairs(clients) do
        local success, err = client.socket:send(message)
        if not success then
            Tacview.Log.Warning("AeroDebrief Sync: Failed to send to client: " .. tostring(err))
        end
    end
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
