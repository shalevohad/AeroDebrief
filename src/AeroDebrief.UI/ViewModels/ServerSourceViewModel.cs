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
        private string _serverName = string.Empty;
        private bool _isConnected;
        private bool _isRecording;
        private string _connectionStatus = "Disconnected";
        private string? _serverVersion;
        private ServerBookmark? _selectedBookmark;
        private ObservableCollection<ServerBookmark> _bookmarks = new();
        private bool _isTestingConnection;

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
                    OnPropertyChanged(nameof(CanTestConnection));
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
                    OnPropertyChanged(nameof(CanTestConnection));
                }
            }
        }

        /// <summary>Server name (required for bookmarks)</summary>
        public string ServerName
        {
            get => _serverName;
            set
            {
                if (SetProperty(ref _serverName, value))
                {
                    OnPropertyChanged(nameof(CanAddBookmark));
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
                    OnPropertyChanged(nameof(CanTestConnection));
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

        /// <summary>Whether currently testing the connection to the server</summary>
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            private set
            {
                if (SetProperty(ref _isTestingConnection, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(CanTestConnection));
                }
            }
        }

        // Command enablement properties
        public bool CanConnect => !IsConnected && !IsTestingConnection && !string.IsNullOrWhiteSpace(ServerIp) && ServerPort > 0;
        public bool CanDisconnect => IsConnected;
        public bool CanRecord => IsConnected && !IsRecording;
        public bool CanStopRecording => IsRecording;
        public bool CanAddBookmark => !string.IsNullOrWhiteSpace(ServerName) && !string.IsNullOrWhiteSpace(ServerIp) && ServerPort > 0;
        public bool CanTestConnection => !IsConnected && !IsTestingConnection && !string.IsNullOrWhiteSpace(ServerIp) && ServerPort > 0;

        #endregion

        #region Events

        /// <summary>Raised when connection state changes</summary>
        public event Action<bool>? ConnectionStateChanged;

        /// <summary>Raised when recording state changes</summary>
        public event Action<bool>? RecordingStateChanged;

        /// <summary>Phase 4: Raised when live playback is ready (recording has started)</summary>
        public event Action<string>? LivePlaybackReady;

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
        public ICommand TestConnectionCommand { get; }

        #endregion

        #region Constructor

        public ServerSourceViewModel()
        {
            // Initialize commands
            ConnectCommand = new RelayCommand(async () => await ExecuteConnectAsync(), () => CanConnect);
            DisconnectCommand = new RelayCommand(ExecuteDisconnect, () => CanDisconnect);
            RecordCommand = new RelayCommand(ExecuteRecord, () => CanRecord);
            StopRecordingCommand = new RelayCommand(ExecuteStopRecording, () => CanStopRecording);
            AddBookmarkCommand = new RelayCommand(ExecuteAddBookmark, () => CanAddBookmark);
            DeleteBookmarkCommand = new RelayCommand<ServerBookmark>(ExecuteDeleteBookmark);
            MoveBookmarkUpCommand = new RelayCommand<ServerBookmark>(ExecuteMoveBookmarkUp);
            MoveBookmarkDownCommand = new RelayCommand<ServerBookmark>(ExecuteMoveBookmarkDown);
            ImportBookmarksCommand = new RelayCommand(async () => await ExecuteImportBookmarksAsync());
            ExportBookmarksCommand = new RelayCommand(async () => await ExecuteExportBookmarksAsync());
            TestConnectionCommand = new RelayCommand(async () => await ExecuteTestConnectionAsync(), () => CanTestConnection);

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
                
                // Phase 4: Wire up live playback event
                _recorder.LivePlaybackReady += OnLivePlaybackReady;

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
            // Check if a bookmark with the same IP and Port already exists
            var existingBookmark = Bookmarks.FirstOrDefault(b => 
                b.Ip == ServerIp && b.Port == ServerPort);
            
            if (existingBookmark != null)
            {
                // Update existing bookmark name
                existingBookmark.Name = ServerName;
                SaveBookmarksToSettings();
                Logger.Info($"Updated bookmark: {existingBookmark.Name}");
            }
            else
            {
                // Add new bookmark
                var newBookmark = new ServerBookmark
                {
                    Name = ServerName,
                    Ip = ServerIp,
                    Port = ServerPort
                };

                Bookmarks.Add(newBookmark);
                SaveBookmarksToSettings();
                Logger.Info($"Added bookmark: {newBookmark.Name}");
            }
            
            // Don't clear the server name - keep it for the current session
            // ServerName = string.Empty;
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
                // Use Microsoft.Win32.OpenFileDialog for file selection
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Server Bookmarks",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    var filePath = dialog.FileName;
                    var json = await File.ReadAllTextAsync(filePath);
                    var importedBookmarks = JsonSerializer.Deserialize<ServerBookmark[]>(json);

                    if (importedBookmarks != null && importedBookmarks.Length > 0)
                    {
                        // Merge with existing bookmarks (avoid duplicates based on IP:Port)
                        int addedCount = 0;
                        foreach (var importedBookmark in importedBookmarks)
                        {
                            var existing = Bookmarks.FirstOrDefault(b => 
                                b.Ip == importedBookmark.Ip && b.Port == importedBookmark.Port);
                            
                            if (existing == null)
                            {
                                Bookmarks.Add(importedBookmark);
                                addedCount++;
                            }
                            else
                            {
                                // Update existing bookmark name
                                existing.Name = importedBookmark.Name;
                            }
                        }

                        SaveBookmarksToSettings();
                        ConnectionStatus = $"Imported {addedCount} new bookmarks, updated {importedBookmarks.Length - addedCount} existing";
                        Logger.Info($"Imported {addedCount} new bookmarks from {filePath}, updated {importedBookmarks.Length - addedCount}");
                    }
                    else
                    {
                        ConnectionStatus = "No bookmarks found in file";
                        Logger.Warn($"No valid bookmarks found in {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to import bookmarks");
                ConnectionStatus = $"Import failed: {ex.Message}";
            }
        }

        private async Task ExecuteExportBookmarksAsync()
        {
            try
            {
                if (Bookmarks.Count == 0)
                {
                    ConnectionStatus = "No bookmarks to export";
                    Logger.Warn("Attempted to export but no bookmarks exist");
                    return;
                }

                // Use Microsoft.Win32.SaveFileDialog for file selection
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Server Bookmarks",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = "server_bookmarks.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    var filePath = dialog.FileName;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(Bookmarks.ToArray(), options);
                    
                    await File.WriteAllTextAsync(filePath, json);
                    ConnectionStatus = $"Exported {Bookmarks.Count} bookmarks";
                    Logger.Info($"Exported {Bookmarks.Count} bookmarks to {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export bookmarks");
                ConnectionStatus = $"Export failed: {ex.Message}";
            }
        }

        private async Task ExecuteTestConnectionAsync()
        {
            AudioPacketRecorder? testRecorder = null;
            bool testPassed = false;
            string? testServerVersion = null;
            
            try
            {
                IsTestingConnection = true;
                ConnectionStatus = "Testing connection...";
                Logger.Info($"Testing connection to {ServerIp}:{ServerPort}");

                testRecorder = new AudioPacketRecorder();
                
                // Wire up connection events for test
                testRecorder.ConnectionStatusChanged += (status) =>
                {
                    if (status.Connected)
                    {
                        testPassed = true;
                        testServerVersion = testRecorder.ServerVersion;
                        Logger.Info($"Connection test successful - Server version: {testServerVersion}");
                    }
                    else
                    {
                        // Only show error if we haven't already passed the test
                        // (ignore disconnect errors after successful test)
                        if (!testPassed)
                        {
                            ConnectionStatus = $"? Test failed - {status.Error}";
                            Logger.Warn($"Connection test failed: {status.Error}");
                        }
                    }
                };

                await testRecorder.ConnectAsync(ServerIp, ServerPort);

                // Wait a moment for connection status
                await Task.Delay(2000);

                if (testPassed)
                {
                    // Test passed - show success message
                    ConnectionStatus = $"? Test passed - Server reachable (v{testServerVersion})";
                    
                    // Auto-disconnect after successful test
                    testRecorder.Disconnect();
                    Logger.Info("Auto-disconnecting after successful test");
                }
                else
                {
                    // Test failed - disconnect and status already set by event handler
                    testRecorder.Disconnect();
                    
                    if (string.IsNullOrEmpty(ConnectionStatus) || ConnectionStatus == "Testing connection...")
                    {
                        ConnectionStatus = "? Test failed - Could not reach server";
                    }
                }
                
                testRecorder = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Connection test threw exception");
                ConnectionStatus = $"? Test failed - {ex.Message}";
            }
            finally
            {
                testRecorder?.Disconnect();
                IsTestingConnection = false;
            }
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStatusChanged(Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages.TCPClientStatusMessage status)
        {
            if (status.Connected)
            {
                IsConnected = true;
                ServerVersion = _recorder?.ServerVersion;
                ConnectionStatus = $"Connected to SRS server (v{ServerVersion ?? "unknown"})";
                
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

        /// <summary>
        /// Phase 4: Handle live playback ready event from recorder
        /// </summary>
        private void OnLivePlaybackReady(string liveDatabasePath)
        {
            Logger.Info($"?? Live playback ready: {liveDatabasePath}");
            
            // Forward event to UnifiedPlayerViewModel
            LivePlaybackReady?.Invoke(liveDatabasePath);
        }

        #endregion

        #region Bookmark Management

        private void LoadBookmark(ServerBookmark bookmark)
        {
            ServerIp = bookmark.Ip;
            ServerPort = bookmark.Port;
            ServerName = bookmark.Name;
            Logger.Debug($"Loaded bookmark: {bookmark.Name}");
        }

        private void LoadBookmarksFromSettings()
        {
            try
            {
                // Store bookmarks in user profile AppData\Roaming
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var aeroDebriefPath = System.IO.Path.Combine(appDataPath, "AeroDebrief");
                var bookmarksPath = System.IO.Path.Combine(aeroDebriefPath, "server_bookmarks.json");
                
                // Ensure directory exists
                if (!Directory.Exists(aeroDebriefPath))
                {
                    Directory.CreateDirectory(aeroDebriefPath);
                    Logger.Info($"Created AeroDebrief user data directory: {aeroDebriefPath}");
                }
                
                if (File.Exists(bookmarksPath))
                {
                    var json = File.ReadAllText(bookmarksPath);
                    var loadedBookmarks = JsonSerializer.Deserialize<ServerBookmark[]>(json);
                    
                    if (loadedBookmarks != null)
                    {
                        // Sort by Name descending (Z-A)
                        var sortedBookmarks = loadedBookmarks.OrderByDescending(b => b.Name);
                        
                        Bookmarks.Clear();
                        foreach (var bookmark in sortedBookmarks)
                        {
                            Bookmarks.Add(bookmark);
                        }
                        
                        Logger.Debug($"Loaded {Bookmarks.Count} bookmarks from {bookmarksPath}");
                    }
                }
                else
                {
                    Logger.Debug($"No bookmarks file found at {bookmarksPath}, starting with empty list");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load bookmarks from user profile");
            }
        }

        private void SaveBookmarksToSettings()
        {
            try
            {
                // Store bookmarks in user profile AppData\Roaming
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var aeroDebriefPath = System.IO.Path.Combine(appDataPath, "AeroDebrief");
                var bookmarksPath = System.IO.Path.Combine(aeroDebriefPath, "server_bookmarks.json");
                
                // Ensure directory exists
                if (!Directory.Exists(aeroDebriefPath))
                {
                    Directory.CreateDirectory(aeroDebriefPath);
                }
                
                // Sort by Name descending before saving
                var sortedBookmarks = Bookmarks.OrderByDescending(b => b.Name).ToArray();
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(sortedBookmarks, options);
                
                File.WriteAllText(bookmarksPath, json);
                Logger.Debug($"Saved {Bookmarks.Count} bookmarks to {bookmarksPath}");
                
                // Update the observable collection with sorted order
                Bookmarks.Clear();
                foreach (var bookmark in sortedBookmarks)
                {
                    Bookmarks.Add(bookmark);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save bookmarks to user profile");
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
    public class ServerBookmark : ViewModelBase
    {
        private string _name = string.Empty;
        private string _ip = string.Empty;
        private int _port;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Ip
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }
    }
}
