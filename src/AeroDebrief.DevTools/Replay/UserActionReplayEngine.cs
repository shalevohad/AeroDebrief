using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.DevTools.Replay
{
    /// <summary>
    /// Replays recorded user actions for bug reproduction.
    /// This is a developer-only tool that ships separately from the main application.
    /// </summary>
    public sealed class UserActionReplayEngine
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private List<UserAction> _actions = new();
        private int _currentActionIndex = 0;
        private DateTime _replayStartTime;
        private DateTime _originalStartTime;
        private bool _isReplaying = false;
        private double _playbackSpeed = 1.0;

        public event EventHandler<UserAction>? ActionReplayed;
        public event EventHandler? ReplayCompleted;
        public event EventHandler<string>? ReplayError;

        /// <summary>
        /// Current playback speed (1.0 = normal, 2.0 = 2x speed, etc.)
        /// </summary>
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Math.Max(0.1, Math.Min(10.0, value));
        }

        /// <summary>
        /// Whether replay is currently in progress
        /// </summary>
        public bool IsReplaying => _isReplaying;

        /// <summary>
        /// Current action index
        /// </summary>
        public int CurrentActionIndex => _currentActionIndex;

        /// <summary>
        /// Total number of actions
        /// </summary>
        public int TotalActions => _actions.Count;

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercent => _actions.Count > 0 ? (_currentActionIndex / (double)_actions.Count) * 100.0 : 0.0;

        /// <summary>
        /// Loads an action log file for replay
        /// </summary>
        public async Task<bool> LoadLogFileAsync(string filePath)
        {
            try
            {
                Logger.Info($"Loading action log: {Path.GetFileName(filePath)}");

                var json = await File.ReadAllTextAsync(filePath);
                _actions = JsonSerializer.Deserialize<List<UserAction>>(json) ?? new List<UserAction>();

                if (_actions.Count == 0)
                {
                    Logger.Warn("Action log file is empty");
                    return false;
                }

                _originalStartTime = _actions[0].Timestamp;
                _currentActionIndex = 0;

                Logger.Info($"Loaded {_actions.Count} actions from log (Duration: {GetTotalDuration()})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load action log file");
                ReplayError?.Invoke(this, $"Failed to load log: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets summary of loaded actions
        /// </summary>
        public ReplaySummary GetSummary()
        {
            if (_actions.Count == 0)
                return new ReplaySummary();

            return new ReplaySummary
            {
                TotalActions = _actions.Count,
                StartTime = _originalStartTime,
                EndTime = _actions[^1].Timestamp,
                Duration = _actions[^1].Timestamp - _originalStartTime,
                ActionsByType = _actions.GroupBy(a => a.ActionType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ErrorCount = _actions.Count(a => a.ActionType == UserActionType.Error),
                WarningCount = _actions.Count(a => a.ActionType == UserActionType.Warning)
            };
        }

        /// <summary>
        /// Gets all actions for display
        /// </summary>
        public List<UserAction> GetAllActions()
        {
            return new List<UserAction>(_actions);
        }

        /// <summary>
        /// Starts replay from the beginning
        /// </summary>
        public async Task StartReplayAsync()
        {
            if (_isReplaying)
            {
                Logger.Warn("Replay already in progress");
                return;
            }

            if (_actions.Count == 0)
            {
                Logger.Warn("No actions to replay");
                return;
            }

            _isReplaying = true;
            _currentActionIndex = 0;
            _replayStartTime = DateTime.UtcNow;

            Logger.Info($"Starting replay of {_actions.Count} actions at {_playbackSpeed}x speed");

            try
            {
                while (_isReplaying && _currentActionIndex < _actions.Count)
                {
                    var action = _actions[_currentActionIndex];

                    // Calculate delay based on original timing and playback speed
                    if (_currentActionIndex > 0)
                    {
                        var previousAction = _actions[_currentActionIndex - 1];
                        var originalDelay = (action.Timestamp - previousAction.Timestamp).TotalMilliseconds;
                        var adjustedDelay = (int)(originalDelay / _playbackSpeed);

                        if (adjustedDelay > 0)
                        {
                            await Task.Delay(adjustedDelay);
                        }
                    }

                    // Check if still replaying (user might have stopped during delay)
                    if (!_isReplaying)
                        break;

                    // Replay the action
                    Logger.Debug($"Replaying action {_currentActionIndex + 1}/{_actions.Count}: {action}");
                    ActionReplayed?.Invoke(this, action);

                    _currentActionIndex++;
                }

                if (_isReplaying)
                {
                    Logger.Info("Replay completed successfully");
                    ReplayCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during replay");
                ReplayError?.Invoke(this, $"Replay error: {ex.Message}");
            }
            finally
            {
                _isReplaying = false;
            }
        }

        /// <summary>
        /// Pauses replay
        /// </summary>
        public void PauseReplay()
        {
            if (!_isReplaying)
                return;

            _isReplaying = false;
            Logger.Info($"Replay paused at action {_currentActionIndex}/{_actions.Count}");
        }

        /// <summary>
        /// Resumes replay
        /// </summary>
        public async Task ResumeReplayAsync()
        {
            if (_isReplaying)
                return;

            Logger.Info($"Resuming replay from action {_currentActionIndex}/{_actions.Count}");
            await StartReplayAsync();
        }

        /// <summary>
        /// Stops replay
        /// </summary>
        public void StopReplay()
        {
            _isReplaying = false;
            _currentActionIndex = 0;
            Logger.Info("Replay stopped");
        }

        /// <summary>
        /// Seeks to specific action index
        /// </summary>
        public void SeekToAction(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= _actions.Count)
            {
                Logger.Warn($"Invalid action index: {actionIndex}");
                return;
            }

            _currentActionIndex = actionIndex;
            Logger.Info($"Seeked to action {actionIndex}/{_actions.Count}");
        }

        /// <summary>
        /// Gets actions of specific type
        /// </summary>
        public List<UserAction> GetActionsByType(UserActionType actionType)
        {
            return _actions.Where(a => a.ActionType == actionType).ToList();
        }

        /// <summary>
        /// Gets actions that resulted in errors
        /// </summary>
        public List<UserAction> GetErrorActions()
        {
            return _actions.Where(a => a.ActionType == UserActionType.Error).ToList();
        }

        /// <summary>
        /// Gets actions within time range
        /// </summary>
        public List<UserAction> GetActionsInTimeRange(TimeSpan startOffset, TimeSpan endOffset)
        {
            var startTime = _originalStartTime + startOffset;
            var endTime = _originalStartTime + endOffset;

            return _actions.Where(a => a.Timestamp >= startTime && a.Timestamp <= endTime).ToList();
        }

        /// <summary>
        /// Gets total duration of recording
        /// </summary>
        public TimeSpan GetTotalDuration()
        {
            if (_actions.Count < 2)
                return TimeSpan.Zero;

            return _actions[^1].Timestamp - _actions[0].Timestamp;
        }

        /// <summary>
        /// Exports filtered actions to new log file
        /// </summary>
        public async Task<bool> ExportFilteredActionsAsync(string outputPath, Func<UserAction, bool> filter)
        {
            try
            {
                var filteredActions = _actions.Where(filter).ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(filteredActions, options);
                await File.WriteAllTextAsync(outputPath, json);

                Logger.Info($"Exported {filteredActions.Count} filtered actions to {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export filtered actions");
                return false;
            }
        }
    }

    /// <summary>
    /// Summary of a replay session
    /// </summary>
    public class ReplaySummary
    {
        public int TotalActions { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<UserActionType, int> ActionsByType { get; set; } = new();
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        public override string ToString()
        {
            var summary = $"""
                Replay Summary
                ==============
                Total Actions: {TotalActions}
                Duration: {Duration}
                Errors: {ErrorCount}
                Warnings: {WarningCount}
                
                Actions by Type:
                """;

            foreach (var (type, count) in ActionsByType.OrderByDescending(kvp => kvp.Value))
            {
                summary += $"\n  - {type}: {count}";
            }

            return summary;
        }
    }

    /// <summary>
    /// Import UserAction and UserActionType from Core
    /// These are defined in AeroDebrief.Core.Diagnostics
    /// </summary>
    public class UserAction
    {
        public DateTime Timestamp { get; set; }
        public UserActionType ActionType { get; set; }
        public string? ControlName { get; set; }
        public string? Parameters { get; set; }
        public string? Result { get; set; }
        public int ThreadId { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {ActionType} on {ControlName ?? "N/A"}";
        }
    }

    public enum UserActionType
    {
        FileLoad, FileSave, FileClose,
        PlaybackStart, PlaybackStop, PlaybackPause, PlaybackResume, PlaybackSeek,
        FrequencySelect, FrequencyDeselect, FrequencySelectAll, FrequencyDeselectAll,
        MixerVolumeChange, MixerPanChange, MixerMuteToggle, MixerSoloToggle,
        WaveformZoomIn, WaveformZoomOut, WaveformZoomReset, WaveformScroll,
        SettingsOpen, SettingsChange, SettingsSave,
        ServerConnect, ServerDisconnect,
        WindowOpen, WindowClose, WindowResize,
        Error, Warning,
        ButtonClick, MenuItemClick, KeyPress, Other
    }
}
