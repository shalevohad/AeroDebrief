using Caliburn.Micro; // For IHandle<T>
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;
using System.Timers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using SRSTCPClientStatusMessage = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages.TCPClientStatusMessage;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using AeroDebrief.Core.Storage;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using AeroDebrief.Core.Storage.Sqlite;

namespace AeroDebrief.Core{
    public class AudioPacketRecorder : IHandle<SRSTCPClientStatusMessage>, IHandle<NetworkMessage>
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private TCPClientHandler? _tcpClientHandler;
        private UDPVoiceHandler? _udpVoiceHandler;
        
        // Repository Pattern: Use IUnitOfWork instead of direct store
        private IUnitOfWork? _recordingUnitOfWork;
        private readonly IRepositoryFactory _repositoryFactory = new SqliteRepositoryFactory();
        private string? _tempDatabasePath;
        private DateTime _recordingStartTime;
        
        private CancellationTokenSource? _recordingCts;
        private System.Timers.Timer? _keepAliveTimer;
        private string? _outputFile;
        private string? _clientGuid;
        private IPEndPoint? _serverEndpoint;
        private int _sampleRate = Ciribob.DCS.SimpleRadio.Standalone.Common.Constants.OUTPUT_SAMPLE_RATE; // Usually 48000
        private int _channelCount = 1; // Mono (default for SRS voice)

        private readonly ConcurrentQueue<AudioPacketMetadata> _writeQueue = new();
        private Task? _writerTask;
        private readonly object _fileWriteLock = new();
        private bool _writerRunning = false;

        // Add this field to track if we've checked the version
        private bool _serverVersionChecked = false;

        public bool IsConnected => _tcpClientHandler?.TCPConnected ?? false;

        public string? ServerVersion { get; private set; }
        
        // Events for recording lifecycle
        public event Action<string>? LivePlaybackReady;  // Fired when database is ready for concurrent read
        public event Action<string>? RecordingComplete;   // Fired when recording is finalized

        /// <summary>
        /// Connects to the SRS server using TCP for control and UDP for audio.
        /// Uses RecorderSettingsStore for default values if parameters are not provided.
        /// Uses RecordingClientState.Instance for client state and GUID.
        /// </summary>
        public async Task ConnectAsync(
            string? serverIp = null,
            int? port = null) // Only one port parameter
        {
            Logger.Info("Initializing connection to SRS server...");
            var settings = RecorderSettingsStore.Instance;
            var state = RecordingClientState.Instance;
            _clientGuid = state.ClientGuid;

            string ip = serverIp ?? settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
            int unifiedPort = port ?? settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);

            Logger.Info($"Connecting to {ip}:{unifiedPort} with client GUID {state.ClientGuid}");

            try
            {
                _serverEndpoint = new IPEndPoint(IPAddress.Parse(ip), unifiedPort);
                _tcpClientHandler = new TCPClientHandler(_clientGuid, state);
                _tcpClientHandler.TryConnect(_serverEndpoint);

                if (_udpVoiceHandler == null)
                {
                    _udpVoiceHandler = new UDPVoiceHandler(_clientGuid, _serverEndpoint);
                    _udpVoiceHandler.Connect();
                    Logger.Info("UDPVoiceHandler initialized and connected.");
                }

                // Subscribe to EventBus for SRSTCPClientStatusMessage events
                EventBus.Instance.SubscribeOnPublishedThread(this);
                Logger.Info("Subscribed to EventBus for status messages.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to SRS server.");
                throw;
            }
        }

        /// <summary>
        /// Disconnects from the SRS server and stops all activities.
        /// </summary>
        public void Disconnect()
        {
            Logger.Info("Disconnecting from SRS server.");

            _tcpClientHandler?.Disconnect();
            StopRecording(); // Synchronous wrapper
            _udpVoiceHandler?.RequestStop();
            _udpVoiceHandler = null;
            EventBus.Instance.Unsubscribe(this);
            Logger.Info("Disconnected and cleaned up resources.");

            ConnectionStatusChanged?.Invoke(
                new SRSTCPClientStatusMessage(
                    false,
                    SRSTCPClientStatusMessage.ErrorCode.USER_DISCONNECTED
                )
            );
        }

        /// <summary>
        /// Synchronous wrapper for StopRecordingAsync (for backward compatibility)
        /// </summary>
        public void StopRecording()
        {
            try
            {
                StopRecordingAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping recording (synchronous wrapper)");
            }
        }

        /// <summary>
        /// Synchronous wrapper for StartRecordingAsync (for backward compatibility)
        /// </summary>
        public void StartRecording(string? filePath = null)
        {
            try
            {
                StartRecordingAsync(filePath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting recording (synchronous wrapper)");
                throw;
            }
        }

        /// <summary>
        /// Starts recording incoming UDP audio packets to DuckDB database.
        /// Phase 3: Records directly to DuckDB instead of .adb format.
        /// Uses RecorderSettingsStore for default file path if not provided.
        /// </summary>
        public async Task StartRecordingAsync(string? filePath = null)
        {
            if (_recordingCts != null && !_recordingCts.IsCancellationRequested)
            {
                Logger.Warn("Attempted to start recording, but recording is already in progress.");
                return;
            }

            if (_udpVoiceHandler == null)
            {
                Logger.Error("UDPVoiceHandler not initialized. Cannot start recording.");
                throw new InvalidOperationException("UDPVoiceHandler not initialized.");
            }

            var settings = RecorderSettingsStore.Instance;
            var requestedPath = filePath ?? settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);

            // Build an output filename that includes server IP, port and start timestamp
            var ipForName = _serverEndpoint?.Address.ToString() ?? settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp) ?? string.Empty;
            var portForName = _serverEndpoint?.Port ?? settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);
            _recordingStartTime = DateTime.UtcNow;
            var timestampForName = _recordingStartTime.ToString("yyyyMMddTHHmmssZ");

            // Sanitize pieces for filename
            string Sanitize(string s)
            {
                if (string.IsNullOrEmpty(s)) return string.Empty;
                var invalid = Path.GetInvalidFileNameChars();
                var sb = new StringBuilder(s.Length);
                foreach (var c in s)
                {
                    if (invalid.Contains(c) || c == ':' || c == '\\' || c == '/')
                        sb.Append('-');
                    else
                        sb.Append(c);
                }
                return sb.ToString();
            }

            var dir = Path.GetDirectoryName(requestedPath) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(requestedPath) ?? "recording";
            
            // Use .db extension for SQLite database
            var sanitizedIp = Sanitize(ipForName);
            var sanitizedBase = Sanitize(baseName);

            var finalName = $"{sanitizedBase}_srv_{sanitizedIp}_{portForName}_t{timestampForName}.db";
            var finalPath = string.IsNullOrEmpty(dir) ? finalName : Path.Combine(dir, finalName);

            // Create temporary database for recording
            _tempDatabasePath = Path.Combine(Path.GetTempPath(), $"aerodebrief_recording_{Guid.NewGuid():N}.db");
            _outputFile = finalPath;

            Logger.Info($"? Starting recording with Repository Pattern");
            Logger.Info($"  Temp database: {_tempDatabasePath}");
            Logger.Info($"  Final output: {_outputFile}");

            try
            {
                // Create recording using Repository Pattern
                var metadata = new RecordingMetadata
                {
                    Version = Constants.RECORDING_FILE_MAGIC,
                    ServerIp = ipForName,
                    ServerPort = portForName,
                    StartTime = _recordingStartTime
                };

                _recordingUnitOfWork = _repositoryFactory.CreateRecording(_tempDatabasePath, metadata);
                await _recordingUnitOfWork.InitializeAsync(metadata);
                
                // Phase 2.1: Enable amplitude precomputation for faster playback loading
                Logger.Info("Enabling amplitude precomputation for recording...");
                _recordingUnitOfWork.Packets.EnableAmplitudePrecomputation();
                Logger.Info("? Amplitude precomputation enabled");

                Logger.Info($"? Recording database created successfully");
                Logger.Info($"   Server: {ipForName}:{portForName}");
                Logger.Info($"   Start: {_recordingStartTime:o}");

                _recordingCts = new CancellationTokenSource();
                _writerRunning = true;
                _writerTask = Task.Run(() => WriterLoop(_recordingCts.Token));
                Task.Run(() => RecordingLoop(_recordingCts.Token));

                // Enable live playback if requested
                if (settings.GetRecorderSettingBool(RecorderSettingKeys.EnableLivePlayback))
                {
                    Logger.Info("?? Live playback enabled - database ready for concurrent reads");
                    LivePlaybackReady?.Invoke(_tempDatabasePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start recording.");
                _recordingUnitOfWork?.Dispose();
                _recordingUnitOfWork = null;
                throw;
            }
        }

        /// <summary>
        /// Stops recording and finalizes the database.
        /// Phase 3: Optionally compresses to CVR format based on user settings.
        /// </summary>
        public async Task StopRecordingAsync()
        {
            Logger.Info("Stopping recording...");
            _recordingCts?.Cancel();
            _writerRunning = false;
            
            try
            {
                // Wait up to 10 seconds for graceful shutdown (increased for database flush)
                if (_writerTask != null && !await Task.Run(() => _writerTask.Wait(TimeSpan.FromSeconds(10))))
                {
                    Logger.Warn("Writer task did not complete within 10 seconds");
                }
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Logger.Error(ex, "Error during writer task shutdown.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during writer task shutdown.");
            }
            
            // Finalize and optionally compress
            try
            {
                if (_recordingUnitOfWork != null && !string.IsNullOrEmpty(_tempDatabasePath))
                {
                    Logger.Info("Finalizing recording database...");
                    
                    // Rebuild statistics and finalize using Repository Pattern
                    await _recordingUnitOfWork.Frequencies.RebuildStatsAsync();
                    await _recordingUnitOfWork.Players.RebuildStatsAsync();
                    await _recordingUnitOfWork.Recording.MarkFinalizedAsync();
                    await _recordingUnitOfWork.Packets.FinalizeAsync();
                    
                    _recordingUnitOfWork.Dispose();
                    _recordingUnitOfWork = null;

                    var settings = RecorderSettingsStore.Instance;

                    // Compression is mandatory (controlled by RecordingConstants)
                    bool shouldCompress = RecordingConstants.FORCE_CVR_COMPRESSION;
                    
                    if (shouldCompress)
                    {
                        Logger.Info("CVR compression MANDATORY (RecordingConstants.FORCE_CVR_COMPRESSION = true)");
                    }
                    else
                    {
                        Logger.Warn("CVR compression DISABLED (RecordingConstants.FORCE_CVR_COMPRESSION = false)");
                        Logger.Warn("This should ONLY happen in development/testing!");
                    }

                    if (shouldCompress)
                    {
                        // Compress to CVR format
                        var cvrPath = Path.ChangeExtension(_outputFile!, ".cvr");
                        Logger.Info($"Compressing to CVR format: {cvrPath}");

                        // Phase 3: Use IProgress<int> for percentage progress
                        var progress = new Progress<int>(percent => Logger.Info($"Compression: {percent}%"));
                        await CvrFormat.CompressToCvrAsync(_tempDatabasePath, cvrPath, progress);

                        // Delete temporary database
                        File.Delete(_tempDatabasePath);
                        Logger.Info($"? Recording compressed to CVR: {cvrPath}");
                        Logger.Info($"   Temporary database deleted: {_tempDatabasePath}");

                        _outputFile = cvrPath;
                    }
                    else
                    {
                        // Keep uncompressed - DEBUG ONLY
                        // IMPORTANT: Even in debug mode, never expose .db extension to users
                        // Use .cvr-debug extension to indicate uncompressed CVR for testing
                        var debugPath = Path.ChangeExtension(_outputFile!, ".cvr-debug");
                        
                        if (File.Exists(debugPath))
                        {
                            File.Delete(debugPath);
                        }
                        File.Move(_tempDatabasePath, debugPath);
                        
                        Logger.Warn($"??  Recording saved UNCOMPRESSED (debug mode): {debugPath}");
                        Logger.Warn($"??  Extension: .cvr-debug (internal database, testing only)");
                        Logger.Warn($"??  This should NEVER happen in production builds!");
                        
                        _outputFile = debugPath;
                    }

                    // Notify listeners
                    RecordingComplete?.Invoke(_outputFile!);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error finalizing recording");
                throw;
            }
            
            Logger.Info($"?? Recording stopped: {_outputFile}");
        }

        private async Task RecordingLoop(CancellationToken token)
        {
            if (_udpVoiceHandler == null)
            {
                Logger.Warn("RecordingLoop called but UDPVoiceHandler is null.");
                return;
            }

            // EncodedAudio is a BlockingCollection<byte[]> containing received UDP packets
            while (!token.IsCancellationRequested)
            {
                try
                {
                    byte[]? packet = null;
                    // Try to take a packet with a timeout to allow cancellation
                    if (_udpVoiceHandler.EncodedAudio.TryTake(out packet, 100))
                    {
                        if (packet != null && packet.Length > 0)
                        {
                            var meta = ExtractAudioMetadata(packet);
                            Logger.Debug($"Audio packet received: Freq={meta.Frequency}, TxGuid={meta.TransmitterGuid}, Size={meta.AudioPayload.Length}");
                            // Notify listeners (CLI) about the received packet
                            PacketReceived?.Invoke(meta);
                            _writeQueue.Enqueue(meta);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Recording cancelled.");
                    break;
                }
                catch (IOException ioEx)
                {
                    Logger.Error(ioEx, "IO error while writing audio packet.");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unexpected error during audio packet reception.");
                }
            }
            Logger.Info("RecordingLoop stopped.");
        }

        // Extract metadata from UDP packet with comprehensive player information
        private AudioPacketMetadata ExtractAudioMetadata(byte[] packet)
        {
            int offset = 0;
            ushort packetLength = BitConverter.ToUInt16(packet, offset); offset += 2;
            ushort audioPart1Length = BitConverter.ToUInt16(packet, offset); offset += 2;
            ushort freqPartLength = BitConverter.ToUInt16(packet, offset); offset += 2;

            byte[] audioPayload = new byte[audioPart1Length];
            Array.Copy(packet, offset, audioPayload, 0, audioPart1Length);
            offset += audioPart1Length;

            double frequency = BitConverter.ToDouble(packet, offset); offset += 8;
            byte modulation = packet[offset]; offset += 1;
            byte encryption = packet[offset]; offset += 1;

            uint transmitterUnitId = BitConverter.ToUInt32(packet, offset); offset += 4;
            ulong packetId = BitConverter.ToUInt64(packet, offset); offset += 8;
            byte hopCount = packet[offset]; offset += 1;

            string transmitterGuid = System.Text.Encoding.ASCII.GetString(packet, offset, 22).TrimEnd('\0'); offset += 22;

            int coalition = BitConverter.ToInt32(packet, offset); offset += 4;

            // Create comprehensive player information
            var playerInfo = new PlayerInfo
            {
                TransmitterGuid = transmitterGuid,
                Coalition = coalition,
                Name = transmitterGuid, // Default fallback
                Seat = -1,
                AllowRecord = true,
                Position = new Position(),
                AircraftInfo = new AircraftInfo()
            };

            // Look up comprehensive player data from connected clients
            var clients = ConnectedClientsSingleton.Instance;
            if (clients.TryGetValue(transmitterGuid, out var foundClient) && foundClient != null)
            {
                Logger.Debug($"Found client info for GUID {transmitterGuid}: Name='{foundClient.Name}', Coalition={foundClient.Coalition}");
                
                // Extract all available player information
                playerInfo.Name = !string.IsNullOrEmpty(foundClient.Name) ? foundClient.Name : transmitterGuid;
                playerInfo.Coalition = foundClient.Coalition;
                playerInfo.Seat = foundClient.Seat;
                playerInfo.AllowRecord = foundClient.AllowRecord;

                // Extract position information
                if (foundClient.LatLngPosition != null)
                {
                    playerInfo.Position = new Position
                    {
                        Latitude = foundClient.LatLngPosition.lat,
                        Longitude = foundClient.LatLngPosition.lng,
                        Altitude = foundClient.LatLngPosition.alt
                    };
                }

                // Extract aircraft/unit information
                if (foundClient.RadioInfo != null)
                {
                    playerInfo.AircraftInfo = new AircraftInfo
                    {
                        UnitType = foundClient.RadioInfo.unit ?? string.Empty,
                        UnitId = foundClient.RadioInfo.unitId
                    };
                }

                // Only add audioPayload if recording is allowed
                byte[] payloadToWrite = foundClient.AllowRecord ? audioPayload : Array.Empty<byte>();

                Logger.Debug($"Created player info: Name='{playerInfo.Name}', DisplayName='{playerInfo.GetDisplayName()}'");

                return new AudioPacketMetadata(
                    DateTime.UtcNow,
                    frequency,
                    modulation,
                    encryption,
                    transmitterUnitId,
                    packetId,
                    transmitterGuid,
                    playerInfo,
                    _sampleRate,
                    _channelCount,
                    coalition,
                    payloadToWrite
                );
            }
            else
            {
                Logger.Debug($"Client not found for GUID {transmitterGuid}, using GUID as fallback name");
            }

            // Fallback if client not found - still record but with minimal info
            return new AudioPacketMetadata(
                DateTime.UtcNow,
                frequency,
                modulation,
                encryption,
                transmitterUnitId,
                packetId,
                transmitterGuid,
                playerInfo,
                _sampleRate,
                _channelCount,
                coalition,
                audioPayload // Allow recording even if client not found
            );
        }

        public event Action<AudioPacketMetadata>? PacketReceived;
        public event Action<SRSTCPClientStatusMessage>? ConnectionStatusChanged;

        public async Task HandleAsync(SRSTCPClientStatusMessage status, CancellationToken cancellationToken)
        {
            Logger.Info($"[EventBus] Received TCPClientStatusMessage: Connected={status.Connected}, Error={status.Error}");
            if (!status.Connected)
            {
                Logger.Warn($"Disconnected from server. Reason: {status.Error}");
                // Stop keep-alive when disconnected
                StopKeepAliveTimer();
                // Stop recording and clean up UDP
                StopRecording();
                _udpVoiceHandler?.RequestStop();
                _udpVoiceHandler = null;

                // Notify listeners (CLI/UI)
                ConnectionStatusChanged?.Invoke(status);
            }
            else
            {
                // Connected: start UDP if not already started
                if (_udpVoiceHandler == null && _serverEndpoint != null)
                {
                    _udpVoiceHandler = new UDPVoiceHandler(_clientGuid, _serverEndpoint);
                    _udpVoiceHandler.Connect();
                }
                // Start periodic keep-alive to prevent server-side idle disconnects
                StartKeepAliveTimer();
                ConnectionStatusChanged?.Invoke(status);
            }
        }

        private void StartKeepAliveTimer()
        {
            try
            {
                if (_keepAliveTimer != null)
                    return;

                // Send a lightweight UnitUpdateMessage periodically to keep the TCP connection alive.
                _keepAliveTimer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds);
                _keepAliveTimer.AutoReset = true;
                _keepAliveTimer.Elapsed += (s, e) =>
                {
                    try
                    {
                        var state = RecordingClientState.Instance;
                        var msg = new UnitUpdateMessage { UnitUpdate = state, FullUpdate = false };
                        EventBus.Instance.PublishOnBackgroundThreadAsync(msg);
                        Logger.Debug("KeepAlive: published UnitUpdateMessage to EventBus");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "KeepAlive: failed to publish UnitUpdateMessage");
                    }
                };
                _keepAliveTimer.Enabled = true;
                _keepAliveTimer.Start();
                Logger.Info("Keep-alive timer started (2 minutes interval)");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to start keep-alive timer");
            }
        }

        private void StopKeepAliveTimer()
        {
            try
            {
                if (_keepAliveTimer == null) return;
                _keepAliveTimer.Enabled = false;
                _keepAliveTimer.Stop();
                _keepAliveTimer.Dispose();
                _keepAliveTimer = null;
                Logger.Info("Keep-alive timer stopped");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error while stopping keep-alive timer");
            }
        }

        /// <summary>
        /// Phase 3: Writer loop using DuckDB batch inserts instead of file writes
        /// </summary>
        private async Task WriterLoop(CancellationToken token)
        {
            const int BATCH_SIZE = RecordingConstants.RECORDING_BATCH_SIZE;
            var batch = new List<AudioPacketMetadata>(BATCH_SIZE);
            var lastFlushTime = DateTime.UtcNow;
            const int FLUSH_INTERVAL_MS = RecordingConstants.RECORDING_FLUSH_INTERVAL_MS;
            
            Logger.Info($"?? Phase 3 WriterLoop started (batch size: {BATCH_SIZE}, flush interval: {FLUSH_INTERVAL_MS}ms)");
            
            while (_writerRunning && !token.IsCancellationRequested)
            {
                try
                {
                    // Try to build a batch
                    var batchFilled = false;
                    for (int i = 0; i < BATCH_SIZE; i++)
                    {
                        if (_writeQueue.TryDequeue(out var meta))
                        {
                            batch.Add(meta);
                            
                            if (batch.Count >= BATCH_SIZE)
                            {
                                batchFilled = true;
                                break;
                            }
                        }
                        else
                        {
                            break; // No more packets available
                        }
                    }
                    
                    // Insert batch if we have packets AND either:
                    // 1. Batch is full, or
                    // 2. Enough time has passed since last flush
                    var timeSinceFlush = (DateTime.UtcNow - lastFlushTime).TotalMilliseconds;
                    if (batch.Count > 0 && (batchFilled || timeSinceFlush >= FLUSH_INTERVAL_MS))
                    {
                        if (_recordingUnitOfWork != null)
                        {
                            // Use Repository Pattern: Packets.InsertBatchAsync
                            await _recordingUnitOfWork.Packets.InsertBatchAsync(batch, token);
                            var count = await _recordingUnitOfWork.Packets.GetCountAsync(token);
                            Logger.Debug($"Inserted batch: {batch.Count} packets (total: {count:N0})");
                            batch.Clear();
                            lastFlushTime = DateTime.UtcNow;
                        }
                    }

                    // Small delay if queue is empty to avoid CPU spinning
                    if (_writeQueue.IsEmpty)
                    {
                        await Task.Delay(50, token); // 50ms delay when idle
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("WriterLoop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in WriterLoop batch processing");
                    await Task.Delay(100, token); // Delay on error to avoid tight loop
                }
            }
            
            // Final batch insert before shutting down
            if (batch.Count > 0 && _recordingUnitOfWork != null)
            {
                try
                {
                    await _recordingUnitOfWork.Packets.InsertBatchAsync(batch, CancellationToken.None);
                    var totalCount = await _recordingUnitOfWork.Packets.GetCountAsync(CancellationToken.None);
                    Logger.Info($"Final batch inserted: {batch.Count} packets (total: {totalCount:N0})");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error inserting final batch");
                }
            }
            
            var finalTotal = _recordingUnitOfWork != null 
                ? await _recordingUnitOfWork.Packets.GetCountAsync(CancellationToken.None)
                : 0;
            Logger.Info($"WriterLoop stopped - Total packets recorded: {finalTotal:N0}");
        }

        // Add this method to handle sync messages
        private void HandleSyncMessage(NetworkMessage networkMessage)
        {
            if (_serverVersionChecked)
                return; // Only check once at first connection

            _serverVersionChecked = true;

            //string serverVersion = networkMessage.Version ?? networkMessage.ServerSettings?["Version"];
            //ServerVersion = serverVersion;
            ServerVersion = networkMessage.Version ?? networkMessage.ServerSettings?["Version"];

            if (string.IsNullOrEmpty(ServerVersion))
            {
                Logger.Warn("Server version not found in sync message.");
                return;
            }

            if (IsVersionLower(ServerVersion, Constants.MINIMUM_SERVER_VERSION))
            {
                Logger.Error($"Server version {ServerVersion} is lower than required {Constants.MINIMUM_SERVER_VERSION}. Disconnecting.");
                ConnectionStatusChanged?.Invoke(
                    new SRSTCPClientStatusMessage(
                        false,
                        SRSTCPClientStatusMessage.ErrorCode.MISMATCHED_SERVER
                    )
                );
                Disconnect();
            }
        }

        // Utility method to compare semantic versions
        private static bool IsVersionLower(string serverVersion, string minVersion)
        {
            Version serverVer, minVer;
            if (Version.TryParse(serverVersion, out serverVer) && Version.TryParse(minVersion, out minVer))
            {
                return serverVer < minVer;
            }
            return false; // If parsing fails, don't block connection
        }

        // Implement the handler for NetworkMessage
        public async Task HandleAsync(NetworkMessage message, CancellationToken cancellationToken)
        {
            if (message.MsgType == NetworkMessage.MessageType.SYNC)
            {
                HandleSyncMessage(message);
            }
        }
    }
}