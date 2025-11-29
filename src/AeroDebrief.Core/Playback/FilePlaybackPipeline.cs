using System.Diagnostics;
using System.Runtime.CompilerServices;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Interfaces.Audio;

namespace AeroDebrief.Core.Playback
{
    /// <summary>
    /// Complete playback pipeline using IPacketSource for memory-efficient streaming.
    /// Supports batched packet reading, instant filtering, and low-latency playback.
    /// Works with both FilePacketSource (.adb) and DuckDBPacketSource (.cvr/.duckdb).
    /// </summary>
    public sealed class FilePlaybackPipeline : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IPacketSource _packetSource;
        private PacketRouter? _packetRouter;
        private AudioOutputEngine? _audioOutput;
        private IAudioOutputEngine? _customAudioOutput; // NEW: For test injection
        private MasterMixer? _masterMixer;
        
        // Controllers for external integration (e.g., Tacview)
        private PlaybackController? _playbackController;
        private SeekController? _seekController;
        
        // FrequencyWorker mapping (FrequencyRoutingWorker -> FrequencyWorker)
        private readonly Dictionary<double, FrequencyWorker> _frequencyWorkers = new();
        
        // NEW: Track registered UserWorkers to avoid duplicate registration
        private readonly HashSet<(double Frequency, string PilotId)> _registeredUserWorkers = new();
        
        // NEW: Enable per-pilot mixing mode
        public bool EnablePerPilotMixing { get; set; } = true;

        private CancellationTokenSource? _playbackCts;
        private Task? _streamingTask;
        private Task? _positionUpdateTask;
        private bool _disposed;

        // Playback state
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public TimeSpan TotalDuration => _packetSource.TotalDuration;
        public DateTime RecordingStart => _packetSource.RecordingStart;

        // Events
        public event Action? PlaybackStarted;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused;
        public event Action? PlaybackResumed;
        public event Action<TimeSpan, TimeSpan>? PositionChanged;
        public event Action<Exception>? ErrorOccurred;

        /// <summary>
        /// Gets the playback controller for external time source integration
        /// </summary>
        public PlaybackController PlaybackController => _playbackController 
            ?? throw new InvalidOperationException("Pipeline not opened - call OpenAsync first");

        /// <summary>
        /// Gets the seek controller for external seek integration
        /// </summary>
        public SeekController SeekController => _seekController 
            ?? throw new InvalidOperationException("Pipeline not opened - call OpenAsync first");

        /// <summary>
        /// Gets the audio output engine for spatial audio integration
        /// </summary>
        public AudioOutputEngine AudioOutput => _audioOutput 
            ?? throw new InvalidOperationException("Pipeline not opened - call OpenAsync first");

        /// <summary>
        /// Gets the master mixer for packet filtering integration
        /// </summary>
        public MasterMixer MasterMixer => _masterMixer 
            ?? throw new InvalidOperationException("Pipeline not opened - call OpenAsync first");

        /// <summary>
        /// Creates a new FilePlaybackPipeline with an already-opened IPacketSource.
        /// This allows sharing the same packet source for waveform generation and playback.
        /// </summary>
        public FilePlaybackPipeline(IPacketSource packetSource)
        {
            _packetSource = packetSource ?? throw new ArgumentNullException(nameof(packetSource));
            Logger.Info($"FilePlaybackPipeline created with {packetSource.GetType().Name}");
        }

        /// <summary>
        /// Creates a new FilePlaybackPipeline with a custom audio output engine (for testing).
        /// This allows test code to inject TestAudioCapture instead of using production AudioOutputEngine.
        /// </summary>
        public FilePlaybackPipeline(IPacketSource packetSource, IAudioOutputEngine audioOutputEngine)
        {
            _packetSource = packetSource ?? throw new ArgumentNullException(nameof(packetSource));
            _customAudioOutput = audioOutputEngine ?? throw new ArgumentNullException(nameof(audioOutputEngine));
            Logger.Info($"FilePlaybackPipeline created with {packetSource.GetType().Name} and custom audio output engine (test mode)");
        }

        /// <summary>
        /// Initializes the complete playback pipeline components.
        /// IPacketSource must already be opened before calling this.
        /// </summary>
        public async Task OpenAsync()
        {
            try
            {
                Logger.Info("Initializing FilePlaybackPipeline components...");
                
                // Verify IPacketSource is opened
                if (_packetSource.TotalPackets == 0)
                    throw new InvalidOperationException("IPacketSource not opened. Call OpenAsync on it first.");
                
                // NEW: Create controllers for external integration (e.g., Tacview)
                Logger.Info("Creating PlaybackController and SeekController for external integration...");
                _playbackController = new PlaybackController();
                _playbackController.SetTotalDuration(_packetSource.TotalDuration);
                _playbackController.SetRecordingStart(_packetSource.RecordingStart);
                
                _seekController = new SeekController();
                Logger.Info("? Controllers created for external integration support");
                
                // Step 1: Initialize PacketRouter
                Logger.Info("Step 1: Initializing PacketRouter...");
                _packetRouter = new PacketRouter();
                Logger.Info("PacketRouter initialized");
                
                // Step 2: Get frequency metadata and pre-allocate routing workers
                Logger.Info("Step 2: Pre-allocating FrequencyRoutingWorkers...");
                var frequencies = _packetSource.GetFrequencyMetadata();
                _packetRouter.PreAllocateFrequencies(frequencies);
                Logger.Info($"Pre-allocated {frequencies.Count} FrequencyRoutingWorkers");
                
                // Step 3: Initialize AudioOutputEngine
                IAudioOutputEngine audioEngine;
                if (_customAudioOutput != null)
                {
                    // Use custom audio output (test mode)
                    Logger.Info("Step 3: Using custom audio output engine (test mode)...");
                    audioEngine = _customAudioOutput;
                    await audioEngine.InitializeAsync();
                    Logger.Info("Custom audio output engine initialized");
                }
                else
                {
                    // Use production audio output
                    Logger.Info("Step 3: Initializing AudioOutputEngine...");
                    _audioOutput = new AudioOutputEngine();
                    await _audioOutput.InitializeAsync();
                    audioEngine = _audioOutput;
                    Logger.Info("AudioOutputEngine initialized");
                }
                
                // Step 4: Initialize MasterMixer with AudioOutputEngine
                Logger.Info("Step 4: Initializing MasterMixer...");
                _masterMixer = new MasterMixer(audioEngine);
                Logger.Info("MasterMixer initialized");
                
                // Step 5: Create FrequencyWorkers and register with MasterMixer
                Logger.Info("Step 5: Creating FrequencyWorkers and registering with MasterMixer...");
                foreach (var (freq, metadata) in frequencies)
                {
                    // Create FrequencyWorker for this frequency
                    var frequencyWorker = new FrequencyWorker(freq);
                    _frequencyWorkers[freq] = frequencyWorker;
                    
                    // Register with MasterMixer
                    _masterMixer.RegisterFrequency(freq, frequencyWorker);
                }
                Logger.Info($"Registered {_frequencyWorkers.Count} FrequencyWorkers with MasterMixer");
                
                Logger.Info($"? Pipeline opened successfully with external integration support: {_packetSource.TotalPackets} packets, Duration: {TotalDuration}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize pipeline");
                throw;
            }
        }

        /// <summary>
        /// Starts audio playback using batched streaming for optimal performance.
        /// </summary>
        public async Task PlayAsync()
        {
            if (IsPlaying)
            {
                Logger.Warn("PlayAsync called but already playing");
                return;
            }

            if (_packetRouter == null || _masterMixer == null)
            {
                throw new InvalidOperationException("Pipeline not initialized. Call OpenAsync first.");
            }

            // Get the correct audio output (production or test)
            var audioOutput = _customAudioOutput ?? _audioOutput;
            if (audioOutput == null)
            {
                throw new InvalidOperationException("Audio output not initialized. Call OpenAsync first.");
            }

            try
            {
                Logger.Info("Starting playback with batched streaming...");
                
                _playbackCts = new CancellationTokenSource();
                IsPlaying = true;
                IsPaused = false;
                
                // Start audio output
                audioOutput.Start();
                
                // Start streaming task (IPacketSource ? PacketRouter ? FrequencyWorkers)
                // Uses BATCHED streaming for 100x less async overhead
                _streamingTask = Task.Run(async () => await StreamingTaskAsync(_playbackCts.Token), _playbackCts.Token);
                
                // Start position update task (for UI)
                _positionUpdateTask = Task.Run(async () => await PositionUpdateTaskAsync(_playbackCts.Token), _playbackCts.Token);
                
                PlaybackStarted?.Invoke();
                
                Logger.Info("? Playback started with batched streaming");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start playback");
                IsPlaying = false;
                throw;
            }
        }

        /// <summary>
        /// Pauses playback (can be resumed)
        /// </summary>
        public void Pause()
        {
            if (!IsPlaying || IsPaused)
                return;

            IsPaused = true;
            PlaybackPaused?.Invoke();
            Logger.Info("Playback paused");
        }

        /// <summary>
        /// Resumes playback after pause
        /// </summary>
        public void Resume()
        {
            if (!IsPlaying || !IsPaused)
                return;

            IsPaused = false;
            PlaybackResumed?.Invoke();
            Logger.Info("Playback resumed");
        }

        /// <summary>
        /// Stops playback completely
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsPlaying)
                return;

            try
            {
                Logger.Info("Stopping playback...");
                
                // Cancel tasks
                _playbackCts?.Cancel();
                
                // Wait for tasks to complete
                if (_streamingTask != null)
                    await _streamingTask;
                if (_positionUpdateTask != null)
                    await _positionUpdateTask;
                
                // Stop audio output (production or custom test)
                var audioOutput = _customAudioOutput ?? _audioOutput;
                audioOutput?.Stop();
                
                IsPlaying = false;
                IsPaused = false;
                CurrentPosition = TimeSpan.Zero;
                
                PlaybackStopped?.Invoke();
                
                Logger.Info("? Playback stopped");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping playback");
                throw;
            }
            finally
            {
                _playbackCts?.Dispose();
                _playbackCts = null;
            }
        }

        /// <summary>
        /// Seeks to a specific position in the recording
        /// </summary>
        public async Task SeekAsync(TimeSpan position)
        {
            if (position < TimeSpan.Zero || position > TotalDuration)
                throw new ArgumentOutOfRangeException(nameof(position));

            var wasPlaying = IsPlaying;
            var wasPaused = IsPaused;

            // Stop current playback
            if (IsPlaying)
                await StopAsync();

            // Update position
            CurrentPosition = position;

            // Restart if it was playing
            if (wasPlaying)
            {
                await PlayAsync();
                if (wasPaused)
                    Pause();
            }

            Logger.Info($"Seeked to {position}");
        }

        /// <summary>
        /// Streaming task: reads packets from IPacketSource using batched streaming
        /// and routes them. Uses batching to reduce async overhead by 100x.
        /// </summary>
        private async Task StreamingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info($"Streaming task started with batched reading (PerPilotMixing={EnablePerPilotMixing})");
                
                var packetsProcessed = 0;
                var batchesProcessed = 0;
                var startTime = DateTime.UtcNow;
                
                const int batchSize = 100; // Process 100 packets at a time
                
                // Use BATCHED streaming for optimal performance
                await foreach (var batch in _packetSource.ReadRangeBatched(CurrentPosition, batchSize, cancellationToken))
                {
                    // Wait if paused
                    while (IsPaused && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    // Process entire batch (no async overhead per packet!)
                    foreach (var packet in batch)
                    {
                        // Route packet to appropriate FrequencyRoutingWorker
                        _packetRouter!.RoutePacket(packet);
                        
                        // Forward to FrequencyWorker for processing
                        if (_frequencyWorkers.TryGetValue(packet.Frequency, out var freqWorker))
                        {
                            freqWorker.EnqueueForUser(packet.TransmitterGuid, packet);
                            
                            // NEW: Dynamically register UserWorkers for per-pilot mixing
                            if (EnablePerPilotMixing && _masterMixer != null)
                            {
                                RegisterUserWorkerIfNeeded(packet.Frequency, packet.TransmitterGuid, freqWorker);
                            }
                        }
                        
                        packetsProcessed++;
                    }
                    
                    batchesProcessed++;
                    
                    // Log progress every 10 batches (1000 packets)
                    if (batchesProcessed % 10 == 0)
                    {
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        var packetsPerSecond = packetsProcessed / elapsed;
                        Logger.Debug($"Streaming: {packetsProcessed} packets in {batchesProcessed} batches ({packetsPerSecond:F0} packets/sec)");
                    }
                }
                
                Logger.Info($"Streaming task completed: {packetsProcessed} packets in {batchesProcessed} batches");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Streaming task cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Streaming task failed");
                ErrorOccurred?.Invoke(ex);
            }
        }

        /// <summary>
        /// NEW: Dynamically registers UserWorkers with MasterMixer as they are created
        /// This enables per-pilot audio mixing with instant mute/solo
        /// </summary>
        private void RegisterUserWorkerIfNeeded(double frequency, string pilotId, FrequencyWorker freqWorker)
        {
            var key = (frequency, pilotId);
            
            // Check if already registered
            if (_registeredUserWorkers.Contains(key))
                return;
            
            // Get UserWorkers from FrequencyWorker
            foreach (var (workerId, userWorker) in freqWorker.GetUserWorkers())
            {
                if (workerId == pilotId)
                {
                    // Register with MasterMixer for per-pilot mixing
                    if (_masterMixer!.RegisterUserWorker(frequency, pilotId, userWorker))
                    {
                        _registeredUserWorkers.Add(key);
                        Logger.Debug($"Registered UserWorker for per-pilot mixing: Pilot={pilotId}, Frequency={frequency / 1_000_000.0:F3} MHz");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Position update task: periodically updates CurrentPosition for UI
        /// </summary>
        private async Task PositionUpdateTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startPosition = CurrentPosition;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!IsPaused)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        CurrentPosition = startPosition + elapsed;

                        // Clamp to duration
                        if (CurrentPosition > TotalDuration)
                            CurrentPosition = TotalDuration;

                        PositionChanged?.Invoke(CurrentPosition, TotalDuration);

                        // Stop when reached end
                        if (CurrentPosition >= TotalDuration)
                        {
                            await StopAsync();
                            break;
                        }
                    }

                    await Task.Delay(100, cancellationToken); // Update 10 times per second
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Position update task cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Position update task failed");
            }
        }

        /// <summary>
        /// Gets all available frequencies from the recording using index metadata (instant!).
        /// No packet reads required - extracted from FilePacketSource index with player information.
        /// </summary>
        public List<FrequencyInfo> GetAvailableFrequencies()
        {
            var metadata = _packetSource.GetFrequencyMetadata();
            
            return metadata.Values.Select(m => new FrequencyInfo
            {
                Frequency = m.Frequency,
                PacketCount = m.PacketCount,
                DisplayName = $"{m.Frequency / 1_000_000.0:F3} MHz",
                Modulation = GetModulationName(m.Modulation),
                Players = m.Players.Select(p => new Models.PlayerFrequencyInfo
                {
                    Name = p.PlayerName,
                    TransmitterGuid = p.TransmitterGuid,
                    Coalition = p.Coalition ?? "Unknown",
                    Aircraft = p.UnitType,
                    PacketCount = p.PacketCount,
                    FirstSeen = DateTime.MinValue, // Not tracked in index yet
                    LastSeen = DateTime.MinValue
                }).ToList(),
                IsActive = false,
                LastActivity = m.LastSeen
            }).OrderBy(f => f.Frequency).ToList();
        }
        
        private static string GetModulationName(byte modulation)
        {
            if (Enum.IsDefined(typeof(Modulation), (int)modulation))
            {
                return ((Modulation)modulation).ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// Sets the frequency gate mode for instant filtering.
        /// Changes take effect immediately with smooth fade (1.33ms).
        /// </summary>
        public void SetFrequencyGate(double frequency, Audio.FrequencyGateMode mode)
        {
            _masterMixer?.SetFrequencyGate(frequency, mode);
            Logger.Debug($"Frequency gate set: {frequency / 1_000_000.0:F3} MHz ? {mode}");
        }

        /// <summary>
        /// Sets the pilot gate mode for instant per-pilot filtering.
        /// Changes take effect immediately with smooth fade (1.33ms).
        /// </summary>
        public void SetPilotGate(string pilotId, double frequency, Audio.PilotGateMode mode)
        {
            _masterMixer?.SetPilotGate(pilotId, frequency, mode);
            Logger.Debug($"Pilot gate set: {pilotId} on {frequency / 1_000_000.0:F3} MHz ? {mode}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _playbackCts?.Cancel();
            _playbackCts?.Dispose();

            // Dispose controllers
            _playbackController?.Dispose();
            _seekController?.Dispose();

            // Dispose FrequencyWorkers
            foreach (var worker in _frequencyWorkers.Values)
            {
                _ = worker.DisposeAsync();
            }
            _frequencyWorkers.Clear();

            _masterMixer?.Dispose();
            _audioOutput?.Dispose();
            _packetRouter?.Dispose();
            
            // Note: Don't dispose _packetSource as it's shared and managed by the caller

            _disposed = true;
            Logger.Debug("FilePlaybackPipeline disposed");
        }
    }
    
    /// <summary>
    /// Frequency information for pipeline display
    /// </summary>
    public class FrequencyInfo
    {
        public double Frequency { get; set; }
        public string Modulation { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int PacketCount { get; set; }
        public List<Models.PlayerFrequencyInfo> Players { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
