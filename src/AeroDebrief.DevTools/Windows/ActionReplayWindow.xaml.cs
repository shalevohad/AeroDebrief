using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AeroDebrief.DevTools.Replay;
using Microsoft.Win32;
using NLog;

namespace AeroDebrief.DevTools.Windows
{
    /// <summary>
    /// Developer tool for replaying recorded user actions
    /// </summary>
    public partial class ActionReplayWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly UserActionReplayEngine _replayEngine = new();
        private readonly ObservableCollection<UserAction> _displayedActions = new();
        private List<UserAction> _allActions = new();

        public ActionReplayWindow()
        {
            InitializeComponent();
            
            ActionsListBox.ItemsSource = _displayedActions;
            
            // Wire up replay engine events
            _replayEngine.ActionReplayed += OnActionReplayed;
            _replayEngine.ReplayCompleted += OnReplayCompleted;
            _replayEngine.ReplayError += OnReplayError;
            
            // Load recent files
            LoadRecentFiles();
            
            VersionText.Text = $"v{GetVersion()}";
            
            Logger.Info("Action Replay Window opened");
        }

        private string GetVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}" : "1.0";
        }

        private void LoadRecentFiles()
        {
            try
            {
                var logDirectory = GetLogDirectory();
                
                if (!Directory.Exists(logDirectory))
                {
                    StatusText.Text = "No action logs found";
                    return;
                }

                var logFiles = Directory.GetFiles(logDirectory, "session_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Take(10)
                    .Select(f => new RecentFileInfo
                    {
                        FilePath = f.FullName,
                        FileName = f.Name,
                        CreationTime = f.CreationTime,
                        FileSizeMB = f.Length / (1024.0 * 1024.0)
                    })
                    .ToList();

                RecentFilesListBox.ItemsSource = logFiles;
                
                if (logFiles.Count == 0)
                {
                    StatusText.Text = "No recent sessions found";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load recent files");
                StatusText.Text = "Error loading recent files";
            }
        }

        private string GetLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AeroDebrief",
                "ActionLogs"
            );
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Action Log File",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                InitialDirectory = GetLogDirectory()
            };

            if (dialog.ShowDialog() == true)
            {
                LogFilePathTextBox.Text = dialog.FileName;
                LoadButton.IsEnabled = true;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var logDirectory = GetLogDirectory();
            
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            Process.Start("explorer.exe", logDirectory);
        }

        private void RecentFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentFilesListBox.SelectedItem is RecentFileInfo fileInfo)
            {
                LogFilePathTextBox.Text = fileInfo.FilePath;
                LoadButton.IsEnabled = true;
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var filePath = LogFilePathTextBox.Text;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("Please select a valid log file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                StatusText.Text = "Loading...";
                LoadButton.IsEnabled = false;

                var success = await _replayEngine.LoadLogFileAsync(filePath);

                if (success)
                {
                    // Get all actions
                    _allActions = _replayEngine.GetAllActions();
                    ApplyFilter();

                    // Get summary
                    var summary = _replayEngine.GetSummary();
                    
                    TotalActionsText.Text = summary.TotalActions.ToString();
                    DurationText.Text = summary.Duration.ToString(@"hh\:mm\:ss");
                    ErrorCountText.Text = summary.ErrorCount.ToString();

                    // Enable playback controls
                    PlayButton.IsEnabled = true;
                    StopButton.IsEnabled = true;

                    StatusText.Text = $"Loaded {summary.TotalActions} actions from {Path.GetFileName(filePath)}";
                    
                    Logger.Info($"Loaded action log: {Path.GetFileName(filePath)}");
                    
                    // Show summary
                    MessageBox.Show(summary.ToString(), "Log Summary", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to load action log file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "Error loading file";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load action log");
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error loading file";
            }
            finally
            {
                LoadButton.IsEnabled = true;
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (FilterComboBox == null || _allActions.Count == 0)
                return;

            _displayedActions.Clear();

            var selectedItem = FilterComboBox.SelectedItem as ComboBoxItem;
            var filterText = selectedItem?.Content?.ToString() ?? "All Actions";

            IEnumerable<UserAction> filtered = filterText switch
            {
                "Errors Only" => _allActions.Where(a => a.ActionType == UserActionType.Error),
                "File Operations" => _allActions.Where(a => 
                    a.ActionType == UserActionType.FileLoad || 
                    a.ActionType == UserActionType.FileSave || 
                    a.ActionType == UserActionType.FileClose),
                "Playback" => _allActions.Where(a => 
                    a.ActionType == UserActionType.PlaybackStart || 
                    a.ActionType == UserActionType.PlaybackStop || 
                    a.ActionType == UserActionType.PlaybackPause || 
                    a.ActionType == UserActionType.PlaybackResume || 
                    a.ActionType == UserActionType.PlaybackSeek),
                "Frequency" => _allActions.Where(a => 
                    a.ActionType == UserActionType.FrequencySelect || 
                    a.ActionType == UserActionType.FrequencyDeselect),
                _ => _allActions
            };

            foreach (var action in filtered)
            {
                _displayedActions.Add(action);
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PlayButton.IsEnabled = false;
                PauseButton.IsEnabled = true;
                StopButton.IsEnabled = true;

                StatusText.Text = "Replaying actions...";
                
                await _replayEngine.StartReplayAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting replay");
                MessageBox.Show($"Error starting replay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetPlaybackControls();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _replayEngine.PauseReplay();
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StatusText.Text = "Replay paused";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _replayEngine.StopReplay();
            ResetPlaybackControls();
            ProgressBar.Value = 0;
            StatusText.Text = "Replay stopped";
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _replayEngine.PlaybackSpeed = e.NewValue;
            if (SpeedText != null)
            {
                SpeedText.Text = $"{e.NewValue:F1}x";
            }
        }

        private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionsListBox.SelectedItem is UserAction action)
            {
                ShowActionDetails(action);
            }
        }

        private void ShowActionDetails(UserAction action)
        {
            DetailTimestampText.Text = action.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            DetailActionTypeText.Text = action.ActionType.ToString();
            DetailControlText.Text = action.ControlName ?? "N/A";
            DetailThreadText.Text = $"Thread {action.ThreadId}";
            DetailParametersText.Text = action.Parameters ?? "N/A";
            DetailResultText.Text = action.Result ?? "N/A";
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionsListBox.SelectedItem is not UserAction action)
            {
                MessageBox.Show("Please select an action to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Action Details",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"action_{action.Timestamp:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = $"""
                        AeroDebrief Action Details
                        ==========================
                        
                        Timestamp: {action.Timestamp:yyyy-MM-dd HH:mm:ss.fff}
                        Action Type: {action.ActionType}
                        Control: {action.ControlName ?? "N/A"}
                        Thread ID: {action.ThreadId}
                        
                        Parameters:
                        {action.Parameters ?? "N/A"}
                        
                        Result:
                        {action.Result ?? "N/A"}
                        """;

                    File.WriteAllText(dialog.FileName, content);
                    MessageBox.Show("Action details exported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to export action details");
                    MessageBox.Show($"Failed to export: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnActionReplayed(object? sender, UserAction action)
        {
            Dispatcher.Invoke(() =>
            {
                // Update progress
                ProgressBar.Value = _replayEngine.ProgressPercent;
                ProgressText.Text = $"Action {_replayEngine.CurrentActionIndex}/{_replayEngine.TotalActions}";
                StatusText.Text = $"Replaying: {action.ActionType} on {action.ControlName ?? "N/A"}";

                // Highlight current action in list
                var displayedAction = _displayedActions.FirstOrDefault(a => 
                    a.Timestamp == action.Timestamp && 
                    a.ActionType == action.ActionType);
                
                if (displayedAction != null)
                {
                    ActionsListBox.SelectedItem = displayedAction;
                    ActionsListBox.ScrollIntoView(displayedAction);
                }

                // Show details
                ShowActionDetails(action);
            });
        }

        private void OnReplayCompleted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ResetPlaybackControls();
                ProgressBar.Value = 100;
                ProgressText.Text = "Completed";
                StatusText.Text = "Replay completed successfully";
                
                MessageBox.Show("Replay completed successfully!", "Replay Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void OnReplayError(object? sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                ResetPlaybackControls();
                StatusText.Text = $"Error: {error}";
                
                MessageBox.Show($"Replay error: {error}", "Replay Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void ResetPlaybackControls()
        {
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            _replayEngine.StopReplay();
            
            Logger.Info("Action Replay Window closed");
        }

        private class RecentFileInfo
        {
            public string FilePath { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public DateTime CreationTime { get; set; }
            public double FileSizeMB { get; set; }
        }
    }
}
