using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AeroDebrief.Core.Storage;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.IO;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Data;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Phase 4: Manages live playback from a recording in progress.
    /// Monitors the temp DuckDB database for new packets, frequencies, and players.
    /// Updates UI in real-time while recording is active.
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// - 500ms polling for smooth UI updates
    /// - 100ms audio packet streaming for low-latency playback
    /// - Separate threads for metadata polling vs audio streaming
    /// - Batch processing to minimize database queries
    /// 
    /// DUAL PLAYHEAD SUPPORT:
    /// - Tracks recording position (where packets are being written)
    /// - Provides LiveRecordingPlaybackPipeline for audio playback
    /// - Syncs both playheads for UI visualization
    /// </summary>
    public sealed class LivePlaybackManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly FrequencyManager _frequencyManager;
        
        // Polling intervals optimized for smooth experience
        private readonly TimeSpan _metadataUpdateInterval = TimeSpan.FromMilliseconds(500);  // UI updates: 500ms (2 FPS)
        private readonly TimeSpan _audioPacketInterval = TimeSpan.FromMilliseconds(100);     // Audio streaming: 100ms (10 FPS)
        
        private DuckDBStore? _liveStore;
        private string? _liveDbPath;
        private CancellationTokenSource? _monitorCts;
        private Task? _metadataMonitorTask;
        private Task? _audioStreamTask;
        private bool _disposed;
        
        // Phase 4 Enhanced: Playback pipeline for audio output
        private LiveRecordingPlaybackPipeline? _playbackPipeline;
        
        // Tracking state
        private HashSet<double> _knownFrequencies = new();
        private HashSet<string> _knownPlayers = new();
        private long _lastPacketCount;
        private long _lastAudioPacketId;
        private DateTime _recordingStartTime;
        private TimeSpan _lastDuration;
        
        /// <summary>
        /// Gets whether live playback is currently active.
        /// </summary>
        public bool IsLivePlaybackActive => _liveStore != null && _monitorCts != null;
        
        /// <summary>
        /// Gets the current live recording duration (recording playhead position).
        /// </summary>
        public TimeSpan CurrentDuration => _lastDuration;
        
        /// <summary>
        /// Gets the live recording playback pipeline for audio output.
        /// </summary>
        public LiveRecordingPlaybackPipeline? PlaybackPipeline => _playbackPipeline;
        
        /// <summary>
        /// Raised when a new frequency is detected during live recording.
        /// </summary>
        public event EventHandler<FrequencyDetectedEventArgs>? FrequencyDetected;
        
        /// <summary>
        /// Raised when a new player is detected during live recording.
        /// </summary>
        public event EventHandler<PlayerDetectedEventArgs>? PlayerDetected;
        
        /// <summary>
        /// Raised when new packets are available for waveform update.
        /// </summary>
        public event EventHandler<LivePacketsEventArgs>? PacketsAvailable;
        
        /// <summary>
        /// Raised when the live recording duration updates.
        /// </summary>
        public event EventHandler<TimeSpan>? DurationUpdated;
        
        /// <summary>
        /// Raised when new audio packets are available for playback (high frequency - 10 FPS).
        /// </summary>
        public event EventHandler<LiveAudioPacketsEventArgs>? AudioPacketsAvailable;
        
        public LivePlaybackManager(FrequencyManager frequencyManager)
        {
            _frequencyManager = frequencyManager ?? throw new ArgumentNullException(nameof(frequencyManager));
            Logger.Debug("LivePlaybackManager initialized with optimized polling (Metadata: 500ms, Audio: 100ms)");
        }
        
        /// <summary>
        /// Starts live playback monitoring for the specified recording database.
        /// Spawns two monitoring threads: metadata updates (500ms) and audio streaming (100ms).
        /// </summary>
        public async Task StartLivePlaybackAsync(string liveDatabasePath)
        {
            if (string.IsNullOrEmpty(liveDatabasePath))
                throw new ArgumentException("Live database path cannot be empty", nameof(liveDatabasePath));
            
            if (!System.IO.File.Exists(liveDatabasePath))
                throw new System.IO.FileNotFoundException("Live database not found", liveDatabasePath);
            
            if (IsLivePlaybackActive)
            {
                Logger.Warn("Live playback is already active");
                return;
            }
            
            try
            {
                Logger.Info($"?? Starting live playback monitoring: {liveDatabasePath}");
                
                // Open database for concurrent read (WAL mode allows this)
                _liveDbPath = liveDatabasePath;
                _liveStore = new DuckDBStore(_liveDbPath);
                await _liveStore.OpenAsync();
                
                // Get initial recording metadata
                var metadata = await _liveStore.GetMetadataAsync();
                _recordingStartTime = metadata.StartTime;
                _lastPacketCount = 0;
                _lastAudioPacketId = 0;
                _lastDuration = TimeSpan.Zero;
                
                // Reset tracking state
                _knownFrequencies.Clear();
                _knownPlayers.Clear();
                
                // Phase 4 Enhanced: Initialize playback pipeline
                Logger.Info("   Initializing live recording playback pipeline...");
                _playbackPipeline = new LiveRecordingPlaybackPipeline(_liveStore, _liveDbPath);
                await _playbackPipeline.InitializeAsync();
                
                // Wire up playhead events
                _playbackPipeline.RecordingPositionChanged += OnRecordingPositionChanged;
                _playbackPipeline.PlaybackPositionChanged += OnPlaybackPositionChanged;
                
                Logger.Info("   ? Playback pipeline ready for dual-playhead tracking");
                
                // Start monitoring tasks (two threads for different update rates)
                _monitorCts = new CancellationTokenSource();
                _metadataMonitorTask = Task.Run(() => MetadataMonitorLoopAsync(_monitorCts.Token), _monitorCts.Token);
                _audioStreamTask = Task.Run(() => AudioStreamLoopAsync(_monitorCts.Token), _monitorCts.Token);
                
                Logger.Info($"? Live playback started: Recording from {_recordingStartTime:o}");
                Logger.Info($"   Metadata updates: {_metadataUpdateInterval.TotalMilliseconds}ms");
                Logger.Info($"   Audio streaming: {_audioPacketInterval.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start live playback");
                await StopLivePlaybackAsync();
                throw;
            }
        }
        
        /// <summary>
        /// Stops live playback monitoring.
        /// </summary>
        public async Task StopLivePlaybackAsync()
        {
            if (!IsLivePlaybackActive)
                return;
            
            Logger.Info("?? Stopping live playback monitoring...");
            
            try
            {
                _monitorCts?.Cancel();
                
                // Wait for both tasks to complete
                if (_metadataMonitorTask != null)
                    await _metadataMonitorTask.ConfigureAwait(false);
                
                if (_audioStreamTask != null)
                    await _audioStreamTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping live playback monitor");
            }
            finally
            {
                _monitorCts?.Dispose();
                _monitorCts = null;
                _metadataMonitorTask = null;
                _audioStreamTask = null;
                
                // Dispose playback pipeline
                if (_playbackPipeline != null)
                {
                    _playbackPipeline.RecordingPositionChanged -= OnRecordingPositionChanged;
                    _playbackPipeline.PlaybackPositionChanged -= OnPlaybackPositionChanged;
                    _playbackPipeline.Dispose();
                    _playbackPipeline = null;
                }
                
                _liveStore?.Dispose();
                _liveStore = null;
                _liveDbPath = null;
                
                _knownFrequencies.Clear();
                _knownPlayers.Clear();
                
                Logger.Info("? Live playback stopped");
            }
        }
        
        /// <summary>
        /// Metadata monitoring loop - checks for new frequencies/players/duration (500ms interval).
        /// </summary>
        private async Task MetadataMonitorLoopAsync(CancellationToken ct)
        {
            Logger.Info($"Metadata monitoring started (interval: {_metadataUpdateInterval.TotalMilliseconds}ms)");
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_metadataUpdateInterval, ct);
                    
                    // Check for new metadata (frequencies, players, duration)
                    await UpdateMetadataAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in metadata monitoring loop");
                    // Continue monitoring despite errors
                }
            }
            
            Logger.Info("Metadata monitoring loop exited");
        }
        
        /// <summary>
        /// Audio streaming loop - fetches new packets for playback (100ms interval).
        /// High frequency for smooth audio playback.
        /// </summary>
        private async Task AudioStreamLoopAsync(CancellationToken ct)
        {
            Logger.Info($"Audio streaming started (interval: {_audioPacketInterval.TotalMilliseconds}ms)");
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_audioPacketInterval, ct);
                    
                    // Stream new audio packets for playback
                    await StreamNewAudioPacketsAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in audio streaming loop");
                    // Continue streaming despite errors
                }
            }
            
            Logger.Info("Audio streaming loop exited");
        }
        
        /// <summary>
        /// Updates metadata from the database (frequencies, players, duration).
        /// Called every 500ms for UI responsiveness.
        /// </summary>
        private async Task UpdateMetadataAsync(CancellationToken ct)
        {
            if (_liveStore == null)
                return;
            
            try
            {
                // Get current packet count and duration
                var stats = await _liveStore.GetRecordingStatsAsync(ct);
                var newPacketCount = stats.TotalPackets;
                var currentDuration = stats.Duration;
                
                if (newPacketCount == _lastPacketCount)
                {
                    // No new packets since last check
                    return;
                }
                
                Logger.Debug($"?? Metadata update: {newPacketCount - _lastPacketCount} new packets (total: {newPacketCount:N0})");
                
                // Check for new frequencies
                await CheckNewFrequenciesAsync(ct);
                
                // Check for new players
                await CheckNewPlayersAsync(ct);
                
                // Notify about new packets (for waveform updates)
                var newPackets = newPacketCount - _lastPacketCount;
                PacketsAvailable?.Invoke(this, new LivePacketsEventArgs(newPackets, currentDuration));
                
                // Update duration
                if (currentDuration != _lastDuration)
                {
                    _lastDuration = currentDuration;
                    DurationUpdated?.Invoke(this, currentDuration);
                    
                    // Update playback pipeline recording position
                    _playbackPipeline?.UpdateRecordingPosition(currentDuration);
                }
                
                _lastPacketCount = newPacketCount;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating metadata");
            }
        }
        
        /// <summary>
        /// Streams new audio packets for live playback.
        /// Called every 100ms for smooth audio with minimal latency.
        /// </summary>
        private async Task StreamNewAudioPacketsAsync(CancellationToken ct)
        {
            if (_liveStore == null)
                return;
            
            try
            {
                // Query packets since last fetch (using packet ID for efficiency)
                var newPackets = new List<Core.Storage.RadioPacket>();
                
                await foreach (var packet in _liveStore.StreamPacketsAsync(
                    fromTime: TimeSpan.Zero,
                    toTime: null,
                    frequencies: null,
                    players: null,
                    coalition: null,
                    ct: ct))
                {
                    // Only get packets we haven't seen yet
                    if (packet.PacketId > (ulong)_lastAudioPacketId)
                    {
                        newPackets.Add(packet);
                        _lastAudioPacketId = (long)packet.PacketId;
                    }
                }
                
                if (newPackets.Count > 0)
                {
                    Logger.Debug($"?? Audio stream: {newPackets.Count} new packets (last ID: {_lastAudioPacketId})");
                    
                    // Fire event for audio playback subsystem
                    AudioPacketsAvailable?.Invoke(this, new LiveAudioPacketsEventArgs(newPackets));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error streaming audio packets");
            }
        }
        
        /// <summary>
        /// Checks for newly discovered frequencies.
        /// </summary>
        private async Task CheckNewFrequenciesAsync(CancellationToken ct)
        {
            try
            {
                var frequencies = await _liveStore!.GetUniqueFrequenciesAsync(ct);
                
                foreach (var freq in frequencies)
                {
                    if (_knownFrequencies.Add(freq.Frequency))
                    {
                        Logger.Info($"?? New frequency detected: {freq.Frequency:F1} MHz");
                        
                        // Fire event on UI thread
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            FrequencyDetected?.Invoke(this, new FrequencyDetectedEventArgs(freq));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking new frequencies");
            }
        }
        
        /// <summary>
        /// Checks for newly joined players.
        /// </summary>
        private async Task CheckNewPlayersAsync(CancellationToken ct)
        {
            try
            {
                var players = await _liveStore!.GetUniquePlayersAsync(ct);
                
                foreach (var player in players)
                {
                    var playerId = $"{player.PlayerName}_{player.TransmitterGuid}";
                    
                    if (_knownPlayers.Add(playerId))
                    {
                        Logger.Info($"?? New player detected: {player.PlayerName} ({player.Coalition})");
                        
                        // Fire event on UI thread
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            PlayerDetected?.Invoke(this, new PlayerDetectedEventArgs(player));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking new players");
            }
        }
        
        /// <summary>
        /// Gets the live database store for external access (e.g., waveform generation).
        /// </summary>
        public DuckDBStore? GetLiveStore() => _liveStore;
        
        /// <summary>
        /// Handles recording position updates from playback pipeline.
        /// </summary>
        private void OnRecordingPositionChanged(object? sender, TimeSpan position)
        {
            Logger.Debug($"?? Recording playhead: {position}");
            // Recording position is already tracked via _lastDuration
            // This event is for external consumers (UI)
        }
        
        /// <summary>
        /// Handles playback position updates from playback pipeline.
        /// </summary>
        private void OnPlaybackPositionChanged(object? sender, TimeSpan position)
        {
            Logger.Debug($"?? Playback playhead: {position}");
            // Playback position is tracked by PlaybackController
            // This event is for external consumers (UI)
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            StopLivePlaybackAsync().GetAwaiter().GetResult();
            _disposed = true;
            
            Logger.Debug("LivePlaybackManager disposed");
        }
    }
    
    #region Event Args
    
    public class FrequencyDetectedEventArgs : EventArgs
    {
        public FrequencyInfo Frequency { get; }
        
        public FrequencyDetectedEventArgs(FrequencyInfo frequency)
        {
            Frequency = frequency;
        }
    }
    
    public class PlayerDetectedEventArgs : EventArgs
    {
        public PlayerInfo Player { get; }
        
        public PlayerDetectedEventArgs(PlayerInfo player)
        {
            Player = player;
        }
    }
    
    public class LivePacketsEventArgs : EventArgs
    {
        public long NewPacketCount { get; }
        public TimeSpan CurrentDuration { get; }
        
        public LivePacketsEventArgs(long newPacketCount, TimeSpan currentDuration)
        {
            NewPacketCount = newPacketCount;
            CurrentDuration = currentDuration;
        }
    }
    
    /// <summary>
    /// Phase 4 Enhanced: Event args for live audio streaming.
    /// Contains actual packet data for immediate playback.
    /// </summary>
    public class LiveAudioPacketsEventArgs : EventArgs
    {
        public IReadOnlyList<Core.Storage.RadioPacket> Packets { get; }
        
        public LiveAudioPacketsEventArgs(IReadOnlyList<Core.Storage.RadioPacket> packets)
        {
            Packets = packets;
        }
    }
    
    #endregion
}
