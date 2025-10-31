using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using AeroDebrief.Core;
using AeroDebrief.Core.Settings;
using AeroDebrief.UI.Commands;
using NLog;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// View model for SRS server connection and recording.
    /// Manages server bookmarks, connection lifecycle, and recording state.
    /// </summary>
    public class ServerSourceViewModel : ViewModelBase, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private AudioPacketRecorder? _recorder;
        private string _serverIp = "127.0.0.1";
        private int _serverPort = 5002;
        private bool _isConnected;
        private bool _isRecording;
        private string _connectionStatus = "Disconnected";
        private string? _serverVersion;
        private ServerBookmark? _selectedBookmark;
        private ObservableCollection<ServerBookmark> _bookmarks = new();

        #region Properties

        /// <summary>Server IP address</summary>
        public string ServerIp
        {
            get => _serverIp;
            set
            {
                if (SetProperty(ref _serverIp, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                }
            }
        }

        /// <summary>Server port</summary>
        public int ServerPort
        {
            get => _serverPort;
            set
            {
                if (SetProperty(ref _serverPort, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                }
            }
        }

        /// <summary>Whether connected to server</summary>
        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(CanDisconnect));
                    OnPropertyChanged(nameof(CanRecord));
                    ConnectionStateChanged?.Invoke(value);
                }
            }
        }

        /// <summary>Whether currently recording</summary>
        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    OnPropertyChanged(nameof(CanRecord));
                    OnPropertyChanged(nameof(CanStopRecording));
                    RecordingStateChanged?.Invoke(value);
                }
            }
        }

        /// <summary>Connection status message</summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        /// <summary>Server version string</summary>
        public string? ServerVersion
        {
            get => _serverVersion;
            set => SetProperty(ref _serverVersion, value);
        }

        /// <summary>Server bookmarks collection</summary>
        public ObservableCollection<ServerBookmark> Bookmarks
        {
            get => _bookmarks;
            set => SetProperty(ref _bookmarks, value);
        }

        /// <summary>Currently selected bookmark</summary>
        public ServerBookmark? SelectedBookmark
        {
            get => _selectedBookmark;
            set
            {
                if (SetProperty(ref _selectedBookmark, value) && value != null)
                {
                    LoadBookmark(value);
                }
            }
        }

        // Command enablement properties
        public bool CanConnect => !IsConnected && !string.IsNullOrWhiteSpace(ServerIp) && ServerPort > 0;
        public bool CanDisconnect => IsConnected;
        public bool CanRecord => IsConnected && !IsRecording;
        public bool CanStopRecording => IsRecording;

        #endregion

        #region Events

        /// <summary>Raised when connection state changes</summary>
        public event Action<bool>? ConnectionStateChanged;

        /// <summary>Raised when recording state changes</summary>
        public event Action<bool>? RecordingStateChanged;

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RecordCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand AddBookmarkCommand { get; }
        public ICommand DeleteBookmarkCommand { get; }
        public ICommand MoveBookmarkUpCommand { get; }
        public ICommand MoveBookmarkDownCommand { get; }
        public ICommand ImportBookmarksCommand { get; }
        public ICommand ExportBookmarksCommand { get; }

        #endregion

        #region Constructor

        public ServerSourceViewModel()
        {
            // Initialize commands
            ConnectCommand = new RelayCommand(async () => await ExecuteConnectAsync(), () => CanConnect);
            DisconnectCommand = new RelayCommand(ExecuteDisconnect, () => CanDisconnect);
            RecordCommand = new RelayCommand(ExecuteRecord, () => CanRecord);
            StopRecordingCommand = new RelayCommand(ExecuteStopRecording, () => CanStopRecording);
            AddBookmarkCommand = new RelayCommand(ExecuteAddBookmark);
            DeleteBookmarkCommand = new RelayCommand<ServerBookmark>(ExecuteDeleteBookmark);
            MoveBookmarkUpCommand = new RelayCommand<ServerBookmark>(ExecuteMoveBookmarkUp);
            MoveBookmarkDownCommand = new RelayCommand<ServerBookmark>(ExecuteMoveBookmarkDown);
            ImportBookmarksCommand = new RelayCommand(async () => await ExecuteImportBookmarksAsync());
            ExportBookmarksCommand = new RelayCommand(async () => await ExecuteExportBookmarksAsync());

            // Load bookmarks from settings
            LoadBookmarksFromSettings();

            // Load last used server from settings
            LoadLastServerFromSettings();

            Logger.Info("ServerSourceViewModel initialized");
        }

        #endregion

        #region Command Implementations

        private async Task ExecuteConnectAsync()
        {
            try
            {
                ConnectionStatus = "Connecting...";
                Logger.Info($"Attempting to connect to {ServerIp}:{ServerPort}");

                _recorder = new AudioPacketRecorder();
                
                // Wire up connection events
                _recorder.ConnectionStatusChanged += OnConnectionStatusChanged;

                await _recorder.ConnectAsync(ServerIp, ServerPort);

                // Connection success is handled by the event handler
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to server");
                ConnectionStatus = $"Connection failed: {ex.Message}";
                IsConnected = false;
                
                _recorder = null;
            }
        }

        private void ExecuteDisconnect()
        {
            try
            {
                Logger.Info("Disconnecting from server");
                
                if (IsRecording)
                {
                    ExecuteStopRecording();
                }

                _recorder?.Disconnect();
                _recorder = null;

                IsConnected = false;
                ConnectionStatus = "Disconnected";
                ServerVersion = null;

                Logger.Info("Disconnected successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during disconnect");
                ConnectionStatus = $"Disconnect error: {ex.Message}";
            }
        }

        private void ExecuteRecord()
        {
            try
            {
                var settings = RecorderSettingsStore.Instance;
                var outputDir = settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);
                
                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var filename = $"SRS_Recording_{timestamp}.raw";
                var fullPath = Path.Combine(Path.GetDirectoryName(outputDir) ?? Environment.CurrentDirectory, filename);

                Logger.Info($"Starting recording to: {fullPath}");
                
                _recorder?.StartRecording(fullPath);
                IsRecording = true;
                ConnectionStatus = $"Recording to {filename}...";

                Logger.Info("Recording started successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start recording");
                ConnectionStatus = $"Recording error: {ex.Message}";
                IsRecording = false;
            }
        }

        private void ExecuteStopRecording()
        {
            try
            {
                Logger.Info("Stopping recording");
                
                _recorder?.StopRecording();
                IsRecording = false;
                ConnectionStatus = "Connected (recording stopped)";

                Logger.Info("Recording stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping recording");
                ConnectionStatus = $"Stop recording error: {ex.Message}";
            }
        }

        private void ExecuteAddBookmark()
        {
            var newBookmark = new ServerBookmark
            {
                Name = $"Server {Bookmarks.Count + 1}",
                Ip = ServerIp,
                Port = ServerPort
            };

            Bookmarks.Add(newBookmark);
            SaveBookmarksToSettings();
            
            Logger.Info($"Added bookmark: {newBookmark.Name}");
        }

        private void ExecuteDeleteBookmark(ServerBookmark? bookmark)
        {
            if (bookmark != null && Bookmarks.Contains(bookmark))
            {
                Bookmarks.Remove(bookmark);
                SaveBookmarksToSettings();
                Logger.Info($"Deleted bookmark: {bookmark.Name}");
            }
        }

        private void ExecuteMoveBookmarkUp(ServerBookmark? bookmark)
        {
            if (bookmark == null) return;

            var index = Bookmarks.IndexOf(bookmark);
            if (index > 0)
            {
                Bookmarks.Move(index, index - 1);
                SaveBookmarksToSettings();
                Logger.Debug($"Moved bookmark up: {bookmark.Name}");
            }
        }

        private void ExecuteMoveBookmarkDown(ServerBookmark? bookmark)
        {
            if (bookmark == null) return;

            var index = Bookmarks.IndexOf(bookmark);
            if (index >= 0 && index < Bookmarks.Count - 1)
            {
                Bookmarks.Move(index, index + 1);
                SaveBookmarksToSettings();
                Logger.Debug($"Moved bookmark down: {bookmark.Name}");
            }
        }

        private async Task ExecuteImportBookmarksAsync()
        {
            try
            {
                // TODO: Show file dialog to select JSON file
                // For now, use a hardcoded path for demonstration
                var filePath = "server_bookmarks.json";

                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Import file not found: {filePath}");
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var importedBookmarks = JsonSerializer.Deserialize<ServerBookmark[]>(json);

                if (importedBookmarks != null)
                {
                    foreach (var bookmark in importedBookmarks)
                    {
                        Bookmarks.Add(bookmark);
                    }

                    SaveBookmarksToSettings();
                    Logger.Info($"Imported {importedBookmarks.Length} bookmarks from {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to import bookmarks");
            }
        }

        private async Task ExecuteExportBookmarksAsync()
        {
            try
            {
                // TODO: Show save file dialog
                // For now, use a hardcoded path for demonstration
                var filePath = "server_bookmarks.json";

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Bookmarks.ToArray(), options);
                
                await File.WriteAllTextAsync(filePath, json);
                Logger.Info($"Exported {Bookmarks.Count} bookmarks to {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export bookmarks");
            }
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStatusChanged(Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages.TCPClientStatusMessage status)
        {
            if (status.Connected)
            {
                IsConnected = true;
                ConnectionStatus = "Connected to SRS server";
                ServerVersion = _recorder?.ServerVersion;
                
                Logger.Info($"Connected to SRS server (version: {ServerVersion ?? "unknown"})");
                
                // Save as last used server
                SaveLastServerToSettings();
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = $"Disconnected: {status.Error}";
                ServerVersion = null;
                
                Logger.Warn($"Disconnected from server: {status.Error}");
            }
        }

        #endregion

        #region Bookmark Management

        private void LoadBookmark(ServerBookmark bookmark)
        {
            ServerIp = bookmark.Ip;
            ServerPort = bookmark.Port;
            Logger.Debug($"Loaded bookmark: {bookmark.Name}");
        }

        private void LoadBookmarksFromSettings()
        {
            try
            {
                var settings = PlayerSettingsStore.Instance;
                // TODO: Load bookmarks from settings when settings store is extended
                // For now, add some default bookmarks
                Bookmarks.Add(new ServerBookmark { Name = "Local Server", Ip = "127.0.0.1", Port = 5002 });
                
                Logger.Debug($"Loaded {Bookmarks.Count} bookmarks from settings");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load bookmarks from settings");
            }
        }

        private void SaveBookmarksToSettings()
        {
            try
            {
                var settings = PlayerSettingsStore.Instance;
                // TODO: Save bookmarks to settings when settings store is extended
                Logger.Debug($"Saved {Bookmarks.Count} bookmarks to settings");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save bookmarks to settings");
            }
        }

        private void LoadLastServerFromSettings()
        {
            try
            {
                var settings = RecorderSettingsStore.Instance;
                ServerIp = settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
                ServerPort = settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);
                
                Logger.Debug($"Loaded last server from settings: {ServerIp}:{ServerPort}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load last server from settings");
            }
        }

        private void SaveLastServerToSettings()
        {
            try
            {
                var settings = RecorderSettingsStore.Instance;
                settings.SetRecorderSetting(RecorderSettingKeys.ServerIp, ServerIp);
                settings.SetRecorderSetting(RecorderSettingKeys.ServerPort, ServerPort);
                
                Logger.Debug($"Saved last server to settings: {ServerIp}:{ServerPort}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save last server to settings");
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            ExecuteDisconnect();
            Logger.Info("ServerSourceViewModel disposed");
        }

        #endregion
    }

    /// <summary>
    /// Server bookmark data model
    /// </summary>
    public class ServerBookmark
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
    }
}
