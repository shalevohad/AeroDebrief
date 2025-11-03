using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using NLog;

namespace AeroDebrief.Integrations.Tacview.Client;

/// <summary>
/// TCP client for connecting to Tacview addon server
/// Handles connection, message sending/receiving, and reconnection logic
/// </summary>
public class TacviewClient : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly TacviewConfiguration _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    
    private bool _isDisposed;
    private bool _isDisconnecting;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    
    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                _connectionState = value;
                ConnectionStateChanged?.Invoke(this, value);
                Logger.Info($"Tacview connection state changed: {value}");
            }
        }
    }
    
    /// <summary>
    /// True if connected to Tacview
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected == true && _connectionState == ConnectionState.Connected;
    
    /// <summary>
    /// Fired when connection state changes
    /// </summary>
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    /// <summary>
    /// Fired when a message is received from Tacview
    /// </summary>
    public event EventHandler<object>? MessageReceived;
    
    /// <summary>
    /// Fired when connected to Tacview
    /// </summary>
    public event EventHandler? Connected;
    
    /// <summary>
    /// Fired when disconnected from Tacview
    /// </summary>
    public event EventHandler<string>? Disconnected;
    
    public TacviewClient(TacviewConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (!_config.Validate(out var error))
        {
            throw new ArgumentException($"Invalid configuration: {error}", nameof(config));
        }
    }
    
    /// <summary>
    /// Connects to Tacview TCP server
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Use lock to prevent concurrent connect/disconnect operations
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                Logger.Warn("Already connected to Tacview");
                return;
            }
            
            // Wait for any pending disconnect to complete
            while (_isDisconnecting)
            {
                Logger.Debug("Waiting for pending disconnect to complete...");
                await Task.Delay(100, cancellationToken);
            }
            
            // Ensure clean state before connecting
            await CleanupConnectionInternalAsync();
            
            ConnectionState = ConnectionState.Connecting;
            
            Logger.Info($"Connecting to Tacview at {_config.Host}:{_config.Port}...");
            
            _tcpClient = new TcpClient
            {
                ReceiveTimeout = _config.ReceiveTimeoutMs,
                SendTimeout = _config.SendTimeoutMs,
                ReceiveBufferSize = _config.SocketBufferSize,
                SendBufferSize = _config.SocketBufferSize
            };
            
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_config.ConnectionTimeoutMs);
            
            try
            {
                await _tcpClient.ConnectAsync(_config.Host, _config.Port, connectCts.Token);
            }
            catch (SocketException sockEx)
            {
                Logger.Warn(sockEx, $"Socket error connecting to Tacview: {sockEx.Message}");
                ConnectionState = ConnectionState.Disconnected;
                await CleanupConnectionInternalAsync();
                throw;
            }
            
            _networkStream = _tcpClient.GetStream();
            _reader = new StreamReader(_networkStream, Encoding.UTF8);
            _writer = new StreamWriter(_networkStream, Encoding.UTF8) { AutoFlush = true };
            
            // Start receive loop
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveMessagesAsync(_receiveCts.Token), _receiveCts.Token);
            
            ConnectionState = ConnectionState.Connected;
            Connected?.Invoke(this, EventArgs.Empty);
            
            Logger.Info($"? Connected to Tacview at {_config.Host}:{_config.Port}");
        }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Connection to Tacview timed out after {_config.ConnectionTimeoutMs}ms");
            ConnectionState = ConnectionState.Disconnected;
            await CleanupConnectionInternalAsync();
            throw new TimeoutException($"Connection to Tacview timed out after {_config.ConnectionTimeoutMs}ms");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to connect to Tacview at {_config.Host}:{_config.Port}");
            ConnectionState = ConnectionState.Disconnected;
            await CleanupConnectionInternalAsync();
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    /// <summary>
    /// Disconnects from Tacview TCP server
    /// </summary>
    public async Task DisconnectAsync(string reason = "User requested")
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_isDisconnecting)
            {
                Logger.Debug($"Already disconnecting, skipping duplicate disconnect request: {reason}");
                return;
            }
            
            if (!IsConnected && _connectionState == ConnectionState.Disconnected)
            {
                Logger.Debug($"Already disconnected, skipping disconnect request: {reason}");
                return;
            }
            
            _isDisconnecting = true;
            Logger.Info($"Disconnecting from Tacview: {reason}");
            ConnectionState = ConnectionState.Disconnected;
            
            await CleanupConnectionInternalAsync();
            
            Disconnected?.Invoke(this, reason);
            Logger.Info("Disconnected from Tacview");
        }
        finally
        {
            _isDisconnecting = false;
            _connectionLock.Release();
        }
    }
    
    /// <summary>
    /// Sends a message to Tacview
    /// </summary>
    public async Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer == null)
        {
            throw new InvalidOperationException("Not connected to Tacview");
        }
        
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            
            if (_config.EnableDebugLogging)
            {
                Logger.Debug($"Sent to Tacview: {json}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send message to Tacview");
            await DisconnectAsync("Send failed");
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    /// <summary>
    /// Receive loop - runs continuously while connected
    /// </summary>
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        Logger.Info("Tacview receive loop started");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                string? line = null;
                
                try
                {
                    // Check if the connection is still alive before reading
                    if (_tcpClient?.Client != null && !_tcpClient.Connected)
                    {
                        Logger.Warn("TCP connection no longer active");
                        break;
                    }
                    
                    line = await _reader.ReadLineAsync(cancellationToken);
                }
                catch (IOException ioEx)
                {
                    Logger.Warn(ioEx, "IO error while reading from Tacview - connection may be lost");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Logger.Debug("Reader disposed during read - disconnect in progress");
                    break;
                }
                
                if (line == null)
                {
                    Logger.Warn("Tacview connection closed by remote (ReadLine returned null)");
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                if (_config.EnableDebugLogging)
                {
                    Logger.Debug($"Received from Tacview: {line}");
                }
                
                try
                {
                    var message = ParseMessage(line);
                    if (message != null)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to parse message: {line}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Tacview receive loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Tacview receive loop error");
        }
        finally
        {
            Logger.Info("Tacview receive loop stopped");
            
            // Only trigger disconnect if we're still supposed to be connected
            // and not already in the middle of disconnecting
            if (_connectionState == ConnectionState.Connected && !_isDisconnecting)
            {
                // Trigger disconnect in background to avoid deadlock
                _ = Task.Run(() => DisconnectAsync("Receive loop ended"));
            }
        }
    }
    
    /// <summary>
    /// Parses a JSON message from Tacview
    /// </summary>
    private object? ParseMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("type", out var typeElement))
        {
            Logger.Warn("Message missing 'type' field");
            return null;
        }
        
        var type = typeElement.GetString();
        
        return type switch
        {
            "time_update" => JsonSerializer.Deserialize<TimeUpdateMessage>(json),
            "pilot_selection" => JsonSerializer.Deserialize<PilotSelectionMessage>(json),
            "playback_command" => JsonSerializer.Deserialize<PlaybackCommandMessage>(json),
            "seek" => JsonSerializer.Deserialize<SeekMessage>(json),
            "ready" => JsonSerializer.Deserialize<ReadyMessage>(json),
            _ => throw new NotSupportedException($"Unknown message type: {type}")
        };
    }
    
    /// <summary>
    /// Cleans up connection resources (internal, assumes lock is held)
    /// </summary>
    private async Task CleanupConnectionInternalAsync()
    {
        Logger.Debug("Cleaning up connection resources...");
        
        try
        {
            _receiveCts?.Cancel();
            
            if (_receiveTask != null)
            {
                // Wait up to 2 seconds for receive task to complete
                var completed = await Task.WhenAny(_receiveTask, Task.Delay(2000));
                if (completed != _receiveTask)
                {
                    Logger.Warn("Receive task did not complete within timeout");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error stopping receive task");
        }
        
        try
        {
            _receiveCts?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error disposing receive cancellation token");
        }
        _receiveCts = null;
        _receiveTask = null;
        
        try
        {
            _reader?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error disposing reader");
        }
        _reader = null;
        
        try
        {
            _writer?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error disposing writer");
        }
        _writer = null;
        
        try
        {
            _networkStream?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error disposing network stream");
        }
        _networkStream = null;
        
        try
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error disposing TCP client");
        }
        _tcpClient = null;
        
        Logger.Debug("Connection cleanup complete");
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        DisconnectAsync("Dispose").GetAwaiter().GetResult();
        _sendLock.Dispose();
        _connectionLock.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
