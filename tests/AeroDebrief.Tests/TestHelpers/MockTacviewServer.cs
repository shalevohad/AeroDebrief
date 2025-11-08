using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Integrations.Tacview.Protocol;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using NLog;

namespace AeroDebrief.Tests.TestHelpers;

/// <summary>
/// Mock Tacview TCP server for testing client connections and message handling
/// </summary>
public class MockTacviewServer : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private TcpListener? _listener;
    private readonly List<TcpClient> _connectedClients = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;
    private readonly int _port;
    private readonly object _lock = new();
    
    /// <summary>
    /// Messages received from clients
    /// </summary>
    public List<object> ReceivedMessages { get; } = new();
    
    /// <summary>
    /// Number of currently connected clients
    /// </summary>
    public int ConnectedClientCount
    {
        get
        {
            lock (_lock)
            {
                return _connectedClients.Count;
            }
        }
    }
    
    /// <summary>
    /// Waits until at least one client is connected, or timeout occurs
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 5000)</param>
    /// <returns>True if client connected, false if timeout</returns>
    public async Task<bool> WaitForClientAsync(int timeoutMs = 5000)
    {
        var startTime = DateTime.UtcNow;
        while (ConnectedClientCount == 0)
        {
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
            {
                return false;
            }
            await Task.Delay(50);
        }
        // Give extra time for client's receive loop to start
        await Task.Delay(100);
        return true;
    }
    
    /// <summary>
    /// Event fired when a client connects
    /// </summary>
    public event EventHandler? ClientConnected;
    
    /// <summary>
    /// Event fired when a client disconnects
    /// </summary>
    public event EventHandler? ClientDisconnected;
    
    /// <summary>
    /// Event fired when a message is received from a client
    /// </summary>
    public event EventHandler<object>? MessageReceived;
    
    public MockTacviewServer(int port)
    {
        _port = port;
    }
    
    /// <summary>
    /// Starts the mock server
    /// </summary>
    public void Start()
    {
        if (_listener != null)
        {
            Logger.Warn("MockTacviewServer already started");
            return;
        }
        
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        
        Logger.Info($"MockTacviewServer started on port {_port}");
        
        // Start accepting clients in background
        _acceptTask = Task.Run(AcceptClientsAsync, _cts.Token);
    }
    
    /// <summary>
    /// Stops the mock server
    /// </summary>
    public void Stop()
    {
        if (_listener == null)
        {
            return;
        }
        
        Logger.Info("Stopping MockTacviewServer...");
        
        _cts.Cancel();
        
        lock (_lock)
        {
            foreach (var client in _connectedClients)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }
            _connectedClients.Clear();
        }
        
        try
        {
            _listener.Stop();
        }
        catch { }
        
        _listener = null;
        
        Logger.Info("MockTacviewServer stopped");
    }
    
    /// <summary>
    /// Sends a message to all connected clients
    /// </summary>
    public async Task SendMessageToAllAsync(object message)
    {
        var json = TacviewProtocol.SerializeMessage(message);
        
        List<TcpClient> clientsCopy;
        lock (_lock)
        {
            clientsCopy = new List<TcpClient>(_connectedClients);
        }
        
        foreach (var client in clientsCopy)
        {
            try
            {
                var stream = client.GetStream();
                var bytes = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes, _cts.Token);
                Logger.Debug($"Sent message to client: {json.Trim()}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to send message to client");
            }
        }
    }
    
    /// <summary>
    /// Disconnects all clients
    /// </summary>
    public void DisconnectAllClients()
    {
        lock (_lock)
        {
            foreach (var client in _connectedClients)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }
            _connectedClients.Clear();
        }
    }
    
    private async Task AcceptClientsAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    
                    lock (_lock)
                    {
                        _connectedClients.Add(client);
                    }
                    
                    Logger.Info($"Client connected. Total clients: {ConnectedClientCount}");
                    ClientConnected?.Invoke(this, EventArgs.Empty);
                    
                    // Handle this client in background
                    _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error accepting client");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Accept loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Fatal error in accept loop");
        }
    }
    
    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            
            while (!_cts.Token.IsCancellationRequested && client.Connected)
            {
                string? line = null;
                
                try
                {
                    line = await reader.ReadLineAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                
                if (line == null)
                {
                    Logger.Debug("Client disconnected (ReadLine returned null)");
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                Logger.Debug($"Received from client: {line}");
                
                try
                {
                    var message = TacviewProtocol.ParseMessage(line);
                    if (message != null)
                    {
                        ReceivedMessages.Add(message);
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to parse message: {line}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error handling client");
        }
        finally
        {
            lock (_lock)
            {
                _connectedClients.Remove(client);
            }
            
            try
            {
                client.Close();
            }
            catch { }
            
            Logger.Info($"Client disconnected. Total clients: {ConnectedClientCount}");
            ClientDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
