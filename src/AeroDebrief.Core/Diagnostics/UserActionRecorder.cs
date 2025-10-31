using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace AeroDebrief.Core.Diagnostics
{
    /// <summary>
    /// Records user actions for bug reproduction and diagnostics.
    /// Designed for minimal performance impact in release builds.
    /// </summary>
    public sealed class UserActionRecorder : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<UserActionRecorder> _instance = new(() => new UserActionRecorder());
        
        public static UserActionRecorder Instance => _instance.Value;

        private readonly ConcurrentQueue<UserAction> _actionQueue = new();
        private readonly string _sessionId;
        private readonly string _logDirectory;
        private readonly Timer _flushTimer;
        private bool _isEnabled = true;
        private bool _disposed;
        
        // Configuration
        private const int MaxQueueSize = 1000; // Prevent memory issues
        private const int FlushIntervalMs = 30000; // Flush every 30 seconds
        private const int MaxActionLogFiles = 10; // Keep last 10 sessions

        private UserActionRecorder()
        {
            _sessionId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AeroDebrief",
                "ActionLogs"
            );
            
            Directory.CreateDirectory(_logDirectory);
            
            // Clean old logs
            CleanOldLogs();
            
            // Flush timer (low priority to avoid affecting performance)
            _flushTimer = new Timer(_ => FlushToFile(), null, FlushIntervalMs, FlushIntervalMs);
            
            Logger.Info($"User action recording started - Session: {_sessionId}");
        }

        /// <summary>
        /// Enable or disable action recording (e.g., based on user preference)
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                Logger.Info($"User action recording {(value ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Records a user action with minimal performance impact
        /// </summary>
        public void RecordAction(UserActionType actionType, string? controlName = null, object? parameters = null, string? result = null)
        {
            if (!_isEnabled || _disposed)
                return;

            // Prevent queue overflow
            if (_actionQueue.Count >= MaxQueueSize)
            {
#if DEBUG
                Logger.Warn($"Action queue full ({MaxQueueSize}), dropping oldest actions");
#endif
                _actionQueue.TryDequeue(out _);
            }

            var action = new UserAction
            {
                Timestamp = DateTime.UtcNow,
                ActionType = actionType,
                ControlName = controlName,
                Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
                Result = result,
                ThreadId = Environment.CurrentManagedThreadId
            };

            _actionQueue.Enqueue(action);

#if DEBUG
            Logger.Debug($"Action recorded: {actionType} on {controlName ?? "N/A"}");
#endif
        }

        /// <summary>
        /// Records an error that occurred during user interaction
        /// </summary>
        public void RecordError(Exception ex, string? context = null)
        {
            if (!_isEnabled || _disposed)
                return;

            RecordAction(
                UserActionType.Error,
                context,
                new { ExceptionType = ex.GetType().Name, Message = ex.Message },
                ex.StackTrace
            );

            // Immediately flush on error to ensure we have the log
            FlushToFile();
        }

        /// <summary>
        /// Flushes queued actions to file
        /// </summary>
        public void FlushToFile()
        {
            if (_actionQueue.IsEmpty)
                return;

            try
            {
                var actions = new List<UserAction>();
                while (_actionQueue.TryDequeue(out var action))
                {
                    actions.Add(action);
                }

                if (actions.Count == 0)
                    return;

                var logFile = Path.Combine(_logDirectory, $"session_{_sessionId}.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                // Append to existing file or create new
                var existingActions = new List<UserAction>();
                if (File.Exists(logFile))
                {
                    try
                    {
                        var json = File.ReadAllText(logFile);
                        existingActions = JsonSerializer.Deserialize<List<UserAction>>(json) ?? new List<UserAction>();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to read existing action log, creating new file");
                    }
                }

                existingActions.AddRange(actions);

                var serialized = JsonSerializer.Serialize(existingActions, options);
                File.WriteAllText(logFile, serialized);

#if DEBUG
                Logger.Debug($"Flushed {actions.Count} actions to {logFile}");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to flush action log to file");
            }
        }

        /// <summary>
        /// Gets the current session's log file path
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            return Path.Combine(_logDirectory, $"session_{_sessionId}.json");
        }

        /// <summary>
        /// Gets all action log files
        /// </summary>
        public string[] GetAllLogFiles()
        {
            try
            {
                return Directory.GetFiles(_logDirectory, "session_*.json");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get action log files");
                return Array.Empty<string>();
            }
        }

        private void CleanOldLogs()
        {
            try
            {
                var files = Directory.GetFiles(_logDirectory, "session_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(MaxActionLogFiles)
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
#if DEBUG
                        Logger.Debug($"Deleted old action log: {file.Name}");
#endif
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Failed to delete old action log: {file.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clean old action logs");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _flushTimer?.Dispose();
            
            // Final flush
            FlushToFile();
            
            Logger.Info($"User action recording stopped - Session: {_sessionId}");
        }
    }

    /// <summary>
    /// Represents a single user action
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

    /// <summary>
    /// Types of user actions we track
    /// </summary>
    public enum UserActionType
    {
        // File operations
        FileLoad,
        FileSave,
        FileClose,
        
        // Playback controls
        PlaybackStart,
        PlaybackStop,
        PlaybackPause,
        PlaybackResume,
        PlaybackSeek,
        
        // Frequency selection
        FrequencySelect,
        FrequencyDeselect,
        FrequencySelectAll,
        FrequencyDeselectAll,
        
        // Mixer controls
        MixerVolumeChange,
        MixerPanChange,
        MixerMuteToggle,
        MixerSoloToggle,
        
        // Waveform interactions
        WaveformZoomIn,
        WaveformZoomOut,
        WaveformZoomReset,
        WaveformScroll,
        
        // Settings
        SettingsOpen,
        SettingsChange,
        SettingsSave,
        
        // Connection
        ServerConnect,
        ServerDisconnect,
        
        // Window operations
        WindowOpen,
        WindowClose,
        WindowResize,
        
        // Errors
        Error,
        Warning,
        
        // General
        ButtonClick,
        MenuItemClick,
        KeyPress,
        Other
    }
}
