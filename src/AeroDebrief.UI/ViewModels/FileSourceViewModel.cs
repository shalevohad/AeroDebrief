using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AeroDebrief.Core.Settings;
using AeroDebrief.UI.Commands;
using Microsoft.Win32;
using NLog;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// View model for file-based playback source.
    /// Manages file selection, loading, and recent files.
    /// </summary>
    public class FileSourceViewModel : ViewModelBase, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private string? _selectedFilePath;
        private string _fileInfo = "No file selected";
        private bool _isLoading;
        private ObservableCollection<string> _recentFiles = new();

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
                var settings = PlayerSettingsStore.Instance;
                var showLegacy = true; // TODO: Load from settings when extended
                
                var dialog = new OpenFileDialog
                {
                    Title = "Select Recording File",
                    Filter = showLegacy 
                        ? "Recording Files (*.adb;*.raw)|*.adb;*.raw|All Files (*.*)|*.*"
                        : "ADB Files (*.adb)|*.adb|All Files (*.*)|*.*",
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
                FileInfo = "Loading...";
                
                Logger.Info($"Loading file: {SelectedFilePath}");

                // Validate file exists
                if (!File.Exists(SelectedFilePath))
                {
                    FileInfo = "File not found";
                    Logger.Warn($"File not found: {SelectedFilePath}");
                    return;
                }

                // Validate file format
                if (!IsValidRecordingFile(SelectedFilePath))
                {
                    FileInfo = "Invalid file format";
                    Logger.Warn($"Invalid file format: {SelectedFilePath}");
                    return;
                }

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
                settings.SetPlayerSetting(PlayerSettingKeys.LastAnalysisFile, string.Empty);

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
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return extension == ".adb" || extension == ".raw";
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
                var last = settings.GetPlayerSettingString(PlayerSettingKeys.LastRecordingFile);
                if (!string.IsNullOrEmpty(last) && File.Exists(last))
                {
                    SelectedFilePath = last;
                    AddToRecentFiles(last);
                }

                // Load recent files list
                var recentRaw = settings.GetPlayerSettingString(PlayerSettingKeys.LastAnalysisFile);
                if (!string.IsNullOrEmpty(recentRaw))
                {
                    try
                    {
                        var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(recentRaw);
                        if (arr != null)
                        {
                            foreach (var r in arr.Take(5))
                                AddToRecentFiles(r);
                        }
                    }
                    catch { }
                }

                Logger.Debug("Loaded last file from settings");
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

                // Save recent files list as JSON
                try
                {
                    var arr = RecentFiles.Take(5).ToArray();
                    var json = System.Text.Json.JsonSerializer.Serialize(arr);
                    settings.SetPlayerSetting(PlayerSettingKeys.LastAnalysisFile, json);
                }
                catch { }

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
                // Remove existing entry
                var existing = RecentFiles.FirstOrDefault(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) RecentFiles.Remove(existing);

                RecentFiles.Insert(0, path);

                // Trim to 5
                while (RecentFiles.Count > 5) RecentFiles.RemoveAt(RecentFiles.Count - 1);
                
                // Notify that HasRecentFiles may have changed
                OnPropertyChanged(nameof(HasRecentFiles));
                
                // Save to settings
                SaveLastFileToSettings();
            }
            catch { }
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
