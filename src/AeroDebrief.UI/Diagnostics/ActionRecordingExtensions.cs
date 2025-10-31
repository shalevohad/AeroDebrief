using System;
using System.Runtime.CompilerServices;
using AeroDebrief.Core.Diagnostics;
using NLog;

namespace AeroDebrief.UI.Diagnostics
{
    /// <summary>
    /// Extension methods for easy action recording in UI components
    /// </summary>
    public static class ActionRecordingExtensions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Records a button click action
        /// </summary>
        public static void RecordButtonClick(this object sender, string? buttonName = null, [CallerMemberName] string? callerName = null)
        {
            UserActionRecorder.Instance.RecordAction(
                UserActionType.ButtonClick,
                buttonName ?? callerName,
                new { SenderType = sender.GetType().Name }
            );
        }

        /// <summary>
        /// Records a playback action
        /// </summary>
        public static void RecordPlaybackAction(UserActionType actionType, object? parameters = null)
        {
            if (actionType != UserActionType.PlaybackStart &&
                actionType != UserActionType.PlaybackStop &&
                actionType != UserActionType.PlaybackPause &&
                actionType != UserActionType.PlaybackResume &&
                actionType != UserActionType.PlaybackSeek)
            {
                throw new ArgumentException($"Invalid playback action type: {actionType}");
            }

            UserActionRecorder.Instance.RecordAction(actionType, "PlaybackController", parameters);
        }

        /// <summary>
        /// Records a file operation
        /// </summary>
        public static void RecordFileOperation(UserActionType actionType, string filePath)
        {
            if (actionType != UserActionType.FileLoad &&
                actionType != UserActionType.FileSave &&
                actionType != UserActionType.FileClose)
            {
                throw new ArgumentException($"Invalid file operation type: {actionType}");
            }

            UserActionRecorder.Instance.RecordAction(
                actionType,
                "FileOperations",
                new { FileName = System.IO.Path.GetFileName(filePath), FileSize = GetFileSizeIfExists(filePath) }
            );
        }

        /// <summary>
        /// Records a frequency selection change
        /// </summary>
        public static void RecordFrequencyAction(UserActionType actionType, double frequency, bool isSelected)
        {
            UserActionRecorder.Instance.RecordAction(
                actionType,
                "FrequencySelector",
                new { Frequency = frequency, IsSelected = isSelected }
            );
        }

        /// <summary>
        /// Records a mixer control change
        /// </summary>
        public static void RecordMixerAction(UserActionType actionType, double frequency, object value)
        {
            UserActionRecorder.Instance.RecordAction(
                actionType,
                "MixerControl",
                new { Frequency = frequency, Value = value }
            );
        }

        /// <summary>
        /// Records a waveform interaction
        /// </summary>
        public static void RecordWaveformAction(UserActionType actionType, object? parameters = null)
        {
            UserActionRecorder.Instance.RecordAction(
                actionType,
                "WaveformViewer",
                parameters
            );
        }

        /// <summary>
        /// Records a settings change
        /// </summary>
        public static void RecordSettingsAction(string settingName, object? oldValue, object? newValue)
        {
            UserActionRecorder.Instance.RecordAction(
                UserActionType.SettingsChange,
                settingName,
                new { OldValue = oldValue, NewValue = newValue }
            );
        }

        /// <summary>
        /// Records an error with context
        /// </summary>
        public static void RecordError(Exception ex, string? context = null)
        {
            UserActionRecorder.Instance.RecordError(ex, context);
        }

        /// <summary>
        /// Records a window operation
        /// </summary>
        public static void RecordWindowAction(UserActionType actionType, string windowName, object? parameters = null)
        {
            UserActionRecorder.Instance.RecordAction(actionType, windowName, parameters);
        }

        private static long? GetFileSizeIfExists(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    return new System.IO.FileInfo(filePath).Length;
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }
    }

    /// <summary>
    /// Helper for recording actions in ViewModels
    /// </summary>
    public static class ViewModelActionRecorder
    {
        /// <summary>
        /// Records a command execution
        /// </summary>
        public static void RecordCommandExecution(string commandName, object? parameters = null, string? result = null)
        {
            UserActionRecorder.Instance.RecordAction(
                UserActionType.Other,
                $"Command:{commandName}",
                parameters,
                result
            );
        }

        /// <summary>
        /// Records a property change that affects user state
        /// </summary>
        public static void RecordPropertyChange(string propertyName, object? oldValue, object? newValue)
        {
            UserActionRecorder.Instance.RecordAction(
                UserActionType.Other,
                $"Property:{propertyName}",
                new { OldValue = oldValue, NewValue = newValue }
            );
        }

        /// <summary>
        /// Records a state transition
        /// </summary>
        public static void RecordStateTransition(string stateName, object? fromState, object? toState)
        {
            UserActionRecorder.Instance.RecordAction(
                UserActionType.Other,
                $"State:{stateName}",
                new { From = fromState, To = toState }
            );
        }
    }
}
