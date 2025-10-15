using Caliburn.Micro; // For IHandle<T>
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;
using System.Net;
using System.Collections.Concurrent;
using SRSTCPClientStatusMessage = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages.TCPClientStatusMessage;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public class AudioPacketRecorder : IHandle<SRSTCPClientStatusMessage>, IHandle<NetworkMessage>
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private TCPClientHandler? _tcpClientHandler;
        private UDPVoiceHandler? _udpVoiceHandler;
        private FileStream? _fileStream;
        private CancellationTokenSource? _recordingCts;
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
            StopRecording();
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
        /// Starts recording incoming UDP audio packets to a raw file.
        /// Uses RecorderSettingsStore for default file path if not provided.
        /// </summary>
        public void StartRecording(string? filePath = null)
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
            _outputFile = filePath ?? settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);
            Logger.Info($"Starting recording to file: {_outputFile}");

            try
            {
                _fileStream = new FileStream(_outputFile, FileMode.Create, FileAccess.Write);
                _recordingCts = new CancellationTokenSource();

                _writerRunning = true;
                _writerTask = Task.Run(() => WriterLoop(_recordingCts.Token));
                Task.Run(() => RecordingLoop(_recordingCts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start recording.");
                throw;
            }
        }

        public void StopRecording()
        {
            Logger.Info("Stopping recording...");
            _recordingCts?.Cancel();
            _writerRunning = false;
            try
            {
                _writerTask?.Wait();
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
            _fileStream?.Dispose();
            _fileStream = null;
            Logger.Info("Recording stopped and file stream disposed.");
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
                ConnectionStatusChanged?.Invoke(status);
            }
        }

        private async Task WriterLoop(CancellationToken token)
        {
            while (_writerRunning && !token.IsCancellationRequested)
            {
                if (_writeQueue.TryDequeue(out var meta))
                {
                    try
                    {
                        lock (_fileWriteLock)
                        {
                            using var bw = new BinaryWriter(_fileStream!, System.Text.Encoding.UTF8, leaveOpen: true);
                            if (!meta.TryWriteMetadata(bw))
                                Logger.Warn("Failed to write audio packet metadata.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error writing audio packet metadata from queue.");
                    }
                }
                else
                {
                    await Task.Delay(10, token); // Avoid busy wait
                }
            }
            Logger.Info("WriterLoop stopped.");
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