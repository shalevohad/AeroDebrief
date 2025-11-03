using AeroDebrief.Core.Playback;
using AeroDebrief.Integrations.Tacview.Client;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Pilot;
using AeroDebrief.Integrations.Tacview.Protocol;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.Integrations.Tacview.Sync;
using NLog;

namespace AeroDebrief.Integrations.Tacview;

/// <summary>
/// Main orchestrator for Tacview integration
/// Manages connection, synchronization, filtering, and bidirectional communication
/// </summary>
public class TacviewIntegrationService : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly TacviewClient _client;
    private readonly TacviewSyncService _syncService;
    private readonly TacviewAudioFilter _audioFilter;
    private readonly TacviewConfiguration _config;
    private readonly TacviewReconnectionStrategy _reconnectionStrategy;
    private readonly ScrubbingManager _scrubbingManager;
    
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private bool _isDisposed;
    
    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState ConnectionState => _client.ConnectionState;
    
    /// <summary>
    /// True if connected to Tacview
    /// </summary>
    public bool IsConnected => _client.IsConnected;
    
    /// <summary>
    /// Current sync quality (0-100%)
    /// </summary>
    public double SyncQuality => _syncService.SyncQuality;
    
    /// <summary>
    /// Audio filter for frequency-based filtering
    /// </summary>
    public TacviewAudioFilter AudioFilter => _audioFilter;
    
    /// <summary>
    /// Fired when connection state changes
    /// </summary>
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    /// <summary>
    /// Fired when sync quality changes
    /// </summary>
    public event EventHandler<double>? SyncQualityChanged;
    
    /// <summary>
    /// Fired when pilot selection changes
    /// </summary>
    public event EventHandler<PilotSelectionMessage>? PilotSelectionChanged;
    
    public TacviewIntegrationService(
        PlaybackController playbackController,
        SeekController seekController,
        TacviewConfiguration? config = null)
    {
        if (playbackController == null)
            throw new ArgumentNullException(nameof(playbackController));
        
        if (seekController == null)
            throw new ArgumentNullException(nameof(seekController));
        
        _config = config ?? TacviewConfiguration.LoadFromFile(GetDefaultConfigPath());
        _scrubbingManager = new ScrubbingManager();
        _client = new TacviewClient(_config);
        _syncService = new TacviewSyncService(playbackController, seekController, _config, _scrubbingManager);
        _audioFilter = new TacviewAudioFilter();
        _reconnectionStrategy = new TacviewReconnectionStrategy();
        
        // Wire up events
        _client.ConnectionStateChanged += OnConnectionStateChanged;
        _client.MessageReceived += OnMessageReceived;
        _client.Connected += OnConnected;
        _client.Disconnected += OnDisconnected;
        
        _syncService.SyncQualityChanged += (s, quality) => SyncQualityChanged?.Invoke(this, quality);
        _audioFilter.SelectionUpdated += (s, selection) => PilotSelectionChanged?.Invoke(this, selection);
        
        Logger.Info("Tacview integration service created");
    }
    
    /// <summary>
    /// Starts the integration service
    /// </summary>
    public async Task StartAsync(DateTime recordingStartUtc, CancellationToken cancellationToken = default)
    {
        Logger.Info("Starting Tacview integration service...");
        
        // Initialize sync service with recording start time
        _syncService.Initialize(recordingStartUtc);
        
        // Auto-connect if enabled
        if (_config.AutoConnect)
        {
            Logger.Info("Auto-connect enabled, connecting to Tacview...");
            await ConnectAsync(cancellationToken);
        }
        else
        {
            Logger.Info("Auto-connect disabled, waiting for manual connection");
        }
    }
    
    /// <summary>
    /// Stops the integration service
    /// </summary>
    public async Task StopAsync()
    {
        Logger.Info("Stopping Tacview integration service...");
        
        // Cancel reconnection if in progress
        _reconnectCts?.Cancel();
        
        if (_reconnectTask != null)
        {
            try
            {
                await _reconnectTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        
        // Disconnect from Tacview
        await _client.DisconnectAsync("Service stopped");
        
        // Clear filter
        _audioFilter.ClearSelection();
        
        // Reset sync
        _syncService.Reset();
        
        Logger.Info("Tacview integration service stopped");
    }
    
    /// <summary>
    /// Connects to Tacview
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info($"Connecting to Tacview at {_config.Host}:{_config.Port}...");
            await _client.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect to Tacview");
            throw;
        }
    }
    
    /// <summary>
    /// Disconnects from Tacview
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync("User requested");
    }
    
    /// <summary>
    /// Sends a message to Tacview
    /// </summary>
    public async Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to Tacview");
        }
        
        // Validate message before sending
        if (!TacviewProtocol.ValidateMessage(message, out var error))
        {
            throw new ArgumentException($"Invalid message: {error}", nameof(message));
        }
        
        await _client.SendMessageAsync(message, cancellationToken);
    }
    
    /// <summary>
    /// Sends a speaking status update to Tacview (for visual effects)
    /// </summary>
    public async Task SendSpeakingStatusAsync(
        string pilotId,
        string? pilotName,
        double frequency,
        bool isSpeaking,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return; // Silently ignore if not connected
        
        try
        {
            var message = TacviewProtocol.CreateSpeakingStatus(pilotId, pilotName, frequency, isSpeaking);
            await SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to send speaking status to Tacview");
        }
    }
    
    /// <summary>
    /// Sends a frequency filter update to Tacview
    /// </summary>
    public async Task SendFrequencyFilterUpdateAsync(
        string? pilotId,
        List<double> enabledFrequencies,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Tacview");
        
        var message = TacviewProtocol.CreateFrequencyFilterUpdate(pilotId, enabledFrequencies);
        await SendMessageAsync(message, cancellationToken);
        
        Logger.Info($"Sent frequency filter update to Tacview: {(pilotId != null ? $"pilot {pilotId}" : "general")}");
    }
    
    /// <summary>
    /// Sends a pan configuration update to Tacview
    /// </summary>
    public async Task SendPanConfigurationAsync(
        string panMode,
        Dictionary<string, double>? pilotPanSettings = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Tacview");
        
        var message = TacviewProtocol.CreatePanConfiguration(panMode, pilotPanSettings);
        await SendMessageAsync(message, cancellationToken);
        
        Logger.Info($"Sent pan configuration update to Tacview: {panMode} mode");
    }
    
    /// <summary>
    /// Gets sync health statistics
    /// </summary>
    public SyncQuality GetSyncHealth()
    {
        return _syncService.GetSyncHealth();
    }
    
    /// <summary>
    /// Gets filtering statistics
    /// </summary>
    public FilterStatistics GetFilterStatistics()
    {
        return _audioFilter.GetStatistics();
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        Logger.Info($"Connection state changed: {state}");
        ConnectionStateChanged?.Invoke(this, state);
    }
    
    private void OnConnected(object? sender, EventArgs e)
    {
        Logger.Info("? Connected to Tacview successfully");
    }
    
    private async void OnDisconnected(object? sender, string reason)
    {
        Logger.Warn($"Disconnected from Tacview: {reason}");
        
        // Clear filter and reset sync on disconnect
        _audioFilter.ClearSelection();
        _syncService.Reset();
        
        // Attempt auto-reconnect if enabled
        if (_config.AutoReconnect && !_isDisposed)
        {
            Logger.Info("Auto-reconnect enabled, attempting to reconnect...");
            
            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            
            _reconnectTask = Task.Run(async () =>
            {
                try
                {
                    var success = await _reconnectionStrategy.TryReconnectAsync(
                        _client,
                        _config,
                        _reconnectCts.Token);
                    
                    if (!success)
                    {
                        Logger.Error("Auto-reconnect failed after maximum attempts");
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Auto-reconnect cancelled");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Auto-reconnect error");
                }
            });
        }
    }
    
    private async void OnMessageReceived(object? sender, object message)
    {
        try
        {
            switch (message)
            {
                case TimeUpdateMessage timeUpdate:
                    await _syncService.HandleTimeUpdateAsync(timeUpdate);
                    break;
                
                case PilotSelectionMessage pilotSelection:
                    _audioFilter.UpdateSelection(pilotSelection);
                    break;
                
                case PlaybackCommandMessage playbackCommand:
                    await _syncService.HandlePlaybackCommandAsync(playbackCommand);
                    break;
                
                case SeekMessage seek:
                    await _syncService.HandleSeekAsync(seek);
                    break;
                
                case ReadyMessage ready:
                    Logger.Info($"Tacview ready: {ready.ServerVersion}");
                    break;
                
                default:
                    Logger.Warn($"Received unknown message type: {message.GetType().Name}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error handling message: {message.GetType().Name}");
        }
    }
    
    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "AeroDebrief", "Tacview");
        
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        
        return Path.Combine(configDir, "tacview-config.json");
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        _isDisposed = true;
        
        StopAsync().GetAwaiter().GetResult();
        
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        
        _client.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
