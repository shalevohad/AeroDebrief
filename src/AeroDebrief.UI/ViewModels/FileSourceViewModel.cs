using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AeroDebrief.Core.Settings;
using AeroDebrief.Core.Storage;
using AeroDebrief.UI.Commands;
using Microsoft.Win32;
using NLog;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// View model for file-based playback source.
    /// Manages file selection, loading, and recent files.
    /// Supports CVR, ADB (legacy), and DuckDB formats.
    /// </summary>
    public class FileSourceViewModel : ViewModelBase, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private string? _selectedFilePath;
        private string _fileInfo = "No file selected";
        private bool _isLoading;
        private ObservableCollection<string> _recentFiles = new();
        
        // Phase 2.5: Loading status tracking
        private string _loadingStatus = string.Empty;
        private double _loadingProgress = 0.0;
        private bool _isIndeterminate = false;

        #region Properties

        /// <summary>Currently selected file path</summary>
        public string? SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (SetProperty(ref _selectedFilePath, value))
                {
                    OnPropertyChanged(nameof(HasFile));
                    OnPropertyChanged(nameof(CanLoad));
                    UpdateFileInfo();
                }
            }
        }

        /// <summary>File information display</summary>
        public string FileInfo
        {
            get => _fileInfo;
            set => SetProperty(ref _fileInfo, value);
        }

        /// <summary>Whether file loading is in progress</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        /// <summary>
        /// Phase 2.5: Current loading status message (e.g., "Opening file...", "Analyzing frequencies...")
        /// </summary>
        public string LoadingStatus
        {
            get => _loadingStatus;
            set => SetProperty(ref _loadingStatus, value);
        }
        
        /// <summary>
        /// Phase 2.5: Loading progress percentage (0.0 to 100.0)
        /// </summary>
        public double LoadingProgress
        {
            get => _loadingProgress;
            set => SetProperty(ref _loadingProgress, value);
        }
        
        /// <summary>
        /// Phase 2.5: Whether the progress bar should be indeterminate
        /// </summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        /// <summary>Recent files (most recent first)</summary>
        public ObservableCollection<string> RecentFiles
        {
            get => _recentFiles;
            set
            {
                if (SetProperty(ref _recentFiles, value))
                {
                    OnPropertyChanged(nameof(HasRecentFiles));
                }
            }
        }

        /// <summary>Whether there are any recent files</summary>
        public bool HasRecentFiles => RecentFiles?.Count > 0;

        // Command enablement properties
        public bool HasFile => !string.IsNullOrEmpty(SelectedFilePath);
        public bool CanLoad => HasFile && !IsLoading;

        #endregion

        #region Events

        /// <summary>Raised when a file is successfully loaded</summary>
        public event Action<string>? FileLoaded;

        /// <summary>Raised when a file is unloaded</summary>
        public event Action? FileUnloaded;

        #endregion

        #region Commands

        public ICommand BrowseCommand { get; }
        public ICommand LoadFileCommand { get; }
        public ICommand UnloadFileCommand { get; }
        public ICommand OpenRecentFileCommand { get; }
        public ICommand ClearRecentFilesCommand { get; }

        #endregion

        #region Constructor

        public FileSourceViewModel()
        {
            // Initialize commands
            BrowseCommand = new RelayCommand(async () => ExecuteBrowse());
            LoadFileCommand = new RelayCommand(async () => await ExecuteLoadFileAsync(), () => CanLoad);
            UnloadFileCommand = new RelayCommand(ExecuteUnloadFile, () => HasFile);
            OpenRecentFileCommand = new RelayCommand<string>(path => ExecuteOpenRecent(path));
            ClearRecentFilesCommand = new RelayCommand(ExecuteClearRecentFiles);

            // Load last used file path from settings
            LoadLastFileFromSettings();

            Logger.Info("FileSourceViewModel initialized");
        }

        #endregion

        #region Command Implementations

        private async void ExecuteBrowse()
        {
            try
            {
                // Use RecordingFileLoader to get the updated file filters (Phase 2.5)
                // This hides DuckDB from users but still allows all formats
                var fileFilters = RecordingFileLoader.GetFileFilters();
                
                var dialog = new OpenFileDialog
                {
                    Title = "Select Recording File",
                    Filter = fileFilters,
                    FilterIndex = 1
                };

                // Set initial directory to last used path
                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(SelectedFilePath);
                }

                if (dialog.ShowDialog() == true)
                {
                    SelectedFilePath = dialog.FileName;
                    Logger.Info($"File selected: {SelectedFilePath}");
                    
                    // Automatically load the selected file
                    await ExecuteLoadFileAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during file browse");
            }
        }

        private async Task ExecuteLoadFileAsync()
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
                return;

            try
            {
                IsLoading = true;
                IsIndeterminate = true;
                LoadingStatus = "Initializing...";
                LoadingProgress = 0;
                FileInfo = "Loading...";
                
                Logger.Info($"Loading file: {SelectedFilePath}");

                // Validate file exists
                if (!File.Exists(SelectedFilePath))
                {
                    FileInfo = "File not found";
                    LoadingStatus = "File not found";
                    IsLoading = false;
                    Logger.Warn($"File not found: {SelectedFilePath}");
                    return;
                }

                // Validate file format
                if (!IsValidRecordingFile(SelectedFilePath))
                {
                    FileInfo = "Invalid file format";
                    LoadingStatus = "Invalid file format";
                    IsLoading = false;
                    Logger.Warn($"Invalid file format: {SelectedFilePath}");
                    return;
                }
                
                // Update status to show file is being prepared
                LoadingStatus = "Preparing file...";

                // Save as last used file
                SaveLastFileToSettings();

                // Add to recent files
                AddToRecentFiles(SelectedFilePath);

                // Notify that file is loaded
                FileLoaded?.Invoke(SelectedFilePath);

                FileInfo = $"Loaded: {Path.GetFileName(SelectedFilePath)}";
                Logger.Info($"File loaded successfully: {SelectedFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load file");
                FileInfo = $"Load error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteUnloadFile()
        {
            try
            {
                Logger.Info("Unloading file");

                SelectedFilePath = null;
                FileInfo = "No file selected";

                FileUnloaded?.Invoke();

                Logger.Info("File unloaded");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error unloading file");
            }
        }

        private void ExecuteOpenRecent(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            SelectedFilePath = path;
            _ = ExecuteLoadFileAsync();
        }

        private void ExecuteClearRecentFiles()
        {
            try
            {
                RecentFiles.Clear();
                OnPropertyChanged(nameof(HasRecentFiles));
                
                var settings = PlayerSettingsStore.Instance;
                settings.SetPlayerSetting(PlayerSettingKeys.RecentRecordingFiles, string.Empty);

                Logger.Info("Cleared recent files");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clear recent files");
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateFileInfo()
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                FileInfo = "No file selected";
                return;
            }

            try
            {
                if (!File.Exists(SelectedFilePath))
                {
                    FileInfo = "File not found";
                    return;
                }

                var fileInfo = new FileInfo(SelectedFilePath);
                var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                FileInfo = $"{Path.GetFileName(SelectedFilePath)} ({sizeMB:F2} MB)";
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to get file info");
                FileInfo = Path.GetFileName(SelectedFilePath) ?? "Unknown";
            }
        }

        private bool IsValidRecordingFile(string filePath)
        {
            try
            {
                // Check if file extension is supported
                var supportedExtensions = RecordingFileLoader.GetSupportedExtensions();
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return supportedExtensions.Contains(extension);
            }
            catch
            {
                return false;
            }
        }

        private void LoadLastFileFromSettings()
        {
            try
            {
                var settings = PlayerSettingsStore.Instance;
                
                // First, load the recent files list from JSON
                var recentRaw = settings.GetPlayerSettingString(PlayerSettingKeys.RecentRecordingFiles);
                if (!string.IsNullOrEmpty(recentRaw))
                {
                    try
                    {
                        var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(recentRaw);
                        if (arr != null)
                        {
                            // Clear and reload all recent files
                            RecentFiles.Clear();
                            foreach (var r in arr.Take(5))
                            {
                                // Only add files that actually exist
                                if (!string.IsNullOrEmpty(r) && File.Exists(r))
                                {
                                    RecentFiles.Add(r);
                                }
                            }
                            OnPropertyChanged(nameof(HasRecentFiles));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to deserialize recent files list");
                    }
                }
                
                // Then, load the last opened file
                var last = settings.GetPlayerSettingString(PlayerSettingKeys.LastRecordingFile);
                if (!string.IsNullOrEmpty(last) && File.Exists(last))
                {
                    SelectedFilePath = last;
                    
                    // Make sure the last file is at the top of recent files
                    // Remove it if it exists, then add to beginning
                    var existing = RecentFiles.FirstOrDefault(r => string.Equals(r, last, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        RecentFiles.Remove(existing);
                    }
                    RecentFiles.Insert(0, last);
                    
                    // Trim to 5 max
                    while (RecentFiles.Count > 5)
                    {
                        RecentFiles.RemoveAt(RecentFiles.Count - 1);
                    }
                    
                    OnPropertyChanged(nameof(HasRecentFiles));
                }

                Logger.Debug($"Loaded recent files from settings: {RecentFiles.Count} files");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load last file from settings");
            }
        }

        private void SaveLastFileToSettings()
        {
            try
            {
                var settings = PlayerSettingsStore.Instance;
                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    settings.SaveLastRecordingFile(SelectedFilePath);
                }

                // Save recent files list as JSON using the new RecentRecordingFiles key
                try
                {
                    var arr = RecentFiles.Take(5).ToArray();
                    var json = System.Text.Json.JsonSerializer.Serialize(arr);
                    settings.SetPlayerSetting(PlayerSettingKeys.RecentRecordingFiles, json);
                    
                    Logger.Debug($"Saved {arr.Length} recent files to settings");
                    for (int i = 0; i < arr.Length; i++)
                    {
                        Logger.Debug($"  [{i}] {Path.GetFileName(arr[i])}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to save recent files list");
                }

                Logger.Debug($"Saved last file to settings: {SelectedFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save last file to settings");
            }
        }

        private void AddToRecentFiles(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                
                // Don't add non-existent files
                if (!File.Exists(path))
                {
                    Logger.Debug($"Skipping non-existent file from recent list: {path}");
                    return;
                }
                
                // Remove existing entry (case-insensitive)
                var existing = RecentFiles.FirstOrDefault(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    RecentFiles.Remove(existing);
                }

                // Add to beginning (most recent first)
                RecentFiles.Insert(0, path);

                // Trim to 5 maximum
                while (RecentFiles.Count > 5)
                {
                    RecentFiles.RemoveAt(RecentFiles.Count - 1);
                }
                
                // Notify that HasRecentFiles may have changed
                OnPropertyChanged(nameof(HasRecentFiles));
                
                // Save to settings
                SaveLastFileToSettings();
                
                Logger.Debug($"Added to recent files: {Path.GetFileName(path)} (Total: {RecentFiles.Count})");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to add file to recent list: {path}");
            }
        }

        #endregion

        #region Public Methods for External Updates

        /// <summary>
        /// Phase 2.5: Updates the loading status and progress from external code (e.g., UnifiedPlayerViewModel)
        /// </summary>
        public void UpdateLoadingProgress(string status, double progress, bool isIndeterminate = false)
        {
            LoadingStatus = status;
            LoadingProgress = progress;
            IsIndeterminate = isIndeterminate;
            FileInfo = status; // Also update FileInfo for backward compatibility
        }

        /// <summary>
        /// Phase 2.5: Completes the loading process and updates the file info display
        /// </summary>
        public void CompleteLoading(bool success, string? message = null)
        {
            IsLoading = false;
            IsIndeterminate = false;
            LoadingProgress = success ? 100.0 : 0.0;
            
            if (success)
            {
                LoadingStatus = message ?? "File loaded successfully";
                FileInfo = !string.IsNullOrEmpty(SelectedFilePath) 
                    ? $"Loaded: {Path.GetFileName(SelectedFilePath)}" 
                    : "File loaded";
            }
            else
            {
                LoadingStatus = message ?? "Load failed";
                FileInfo = message ?? "Load failed";
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            ExecuteUnloadFile();
            Logger.Info("FileSourceViewModel disposed");
        }

        #endregion
    }
}
