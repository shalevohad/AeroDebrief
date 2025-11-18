using System;
using System.Threading.Tasks;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Playback;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Service responsible for managing playback session lifecycle (file loading, pipeline management).
    /// Implements Separation of Concerns by handling ONLY session-related operations.
    /// </summary>
    public sealed class PlaybackSessionManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private FilePacketSource? _packetSource;
        private FilePlaybackPipeline? _pipeline;
        private bool _disposed;
        private string _currentFilePath = string.Empty;

        /// <summary>
        /// Gets the current packet source.
        /// </summary>
        public FilePacketSource? PacketSource => _packetSource;

        /// <summary>
        /// Gets the current playback pipeline.
        /// </summary>
        public FilePlaybackPipeline? Pipeline => _pipeline;

        /// <summary>
        /// Gets whether a session is currently loaded.
        /// </summary>
        public bool IsSessionLoaded => _packetSource != null && _pipeline != null;

        /// <summary>
        /// Gets the current file path.
        /// </summary>
        public string CurrentFilePath => _currentFilePath;

        /// <summary>
        /// Gets the total duration of the loaded session.
        /// </summary>
        public TimeSpan TotalDuration => _packetSource?.TotalDuration ?? TimeSpan.Zero;

        /// <summary>
        /// Gets the total number of packets in the loaded session.
        /// </summary>
        public int TotalPackets => _packetSource != null ? (int)_packetSource.TotalPackets : 0;

        /// <summary>
        /// Raised when a session is successfully loaded.
        /// </summary>
        public event EventHandler<SessionLoadedEventArgs>? SessionLoaded;

        /// <summary>
        /// Raised when a session is unloaded.
        /// </summary>
        public event EventHandler? SessionUnloaded;

        /// <summary>
        /// Raised when session loading fails.
        /// </summary>
        public event EventHandler<SessionErrorEventArgs>? SessionError;

        public PlaybackSessionManager()
        {
            Logger.Debug("PlaybackSessionManager initialized");
        }

        /// <summary>
        /// Loads a file and creates a playback session.
        /// </summary>
        public async Task LoadFileAsync(string filePath, IProgress<string>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("File not found", filePath);

            try
            {
                Logger.Info("======== LOADING FILE (Pure FilePacketSource Architecture) ========");
                Logger.Info($"File: {System.IO.Path.GetFileName(filePath)}");
                progress?.Report("Initializing...");

                // Unload existing session if any
                if (IsSessionLoaded)
                {
                    Logger.Info("Unloading existing session...");
                    progress?.Report("Unloading previous session...");
                    UnloadSession();
                }

                // STEP 1: Open FilePacketSource (memory-mapped, indexed)
                Logger.Info("Step 1: Opening FilePacketSource (memory-mapped, shared)...");
                progress?.Report("Opening file...");
                _packetSource = new FilePacketSource(filePath);
                await _packetSource.OpenAsync(progress);
                Logger.Info($"FilePacketSource ready: {_packetSource.TotalPackets} packets, {_packetSource.TotalDuration}");
                progress?.Report($"File ready: {_packetSource.TotalPackets:N0} packets");

                // STEP 2: Create FilePlaybackPipeline (shares packet source)
                Logger.Info("Step 2: Creating FilePlaybackPipeline...");
                progress?.Report("Initializing playback engine...");
                _pipeline = new FilePlaybackPipeline(_packetSource);
                await _pipeline.OpenAsync();
                Logger.Info($"? FilePlaybackPipeline initialized");
                progress?.Report("Playback engine ready");

                _currentFilePath = filePath;

                Logger.Info($"- Session loaded successfully");
                Logger.Info($"-- Memory-mapped packets: {_packetSource.TotalPackets}");
                Logger.Info($"-- RAM usage: ~10MB (Pure FilePacketSource Architecture)");
                progress?.Report("Session loaded successfully");

                // Raise event
                SessionLoaded?.Invoke(this, new SessionLoadedEventArgs(
                    _packetSource,
                    _pipeline,
                    filePath,
                    _packetSource.TotalDuration,
                    (int)_packetSource.TotalPackets));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load file: {filePath}");
                progress?.Report($"Error: {ex.Message}");

                // Clean up on failure
                _pipeline?.Dispose();
                _pipeline = null;
                _packetSource?.Dispose();
                _packetSource = null;
                _currentFilePath = string.Empty;

                SessionError?.Invoke(this, new SessionErrorEventArgs(ex, filePath));

                throw;
            }
        }

        /// <summary>
        /// Unloads the current session and cleans up resources.
        /// </summary>
        public void UnloadSession()
        {
            if (!IsSessionLoaded)
            {
                Logger.Debug("No session to unload");
                return;
            }

            Logger.Info("Unloading session...");

            try
            {
                // Stop playback first
                if (_pipeline != null)
                {
                    _ = _pipeline.StopAsync();
                    _pipeline.Dispose();
                    _pipeline = null;
                }

                // Dispose packet source
                _packetSource?.Dispose();
                _packetSource = null;

                _currentFilePath = string.Empty;

                Logger.Info("? Session unloaded");

                SessionUnloaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during session unload");
            }
        }

        /// <summary>
        /// Plays the current session.
        /// </summary>
        public async Task PlayAsync()
        {
            if (_pipeline == null)
                throw new InvalidOperationException("No session loaded");

            try
            {
                Logger.Debug("Starting playback");
                await _pipeline.PlayAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start playback");
                throw;
            }
        }

        /// <summary>
        /// Pauses the current session.
        /// </summary>
        public void Pause()
        {
            if (_pipeline == null)
                throw new InvalidOperationException("No session loaded");

            try
            {
                Logger.Debug("Pausing playback");
                _pipeline.Pause();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to pause playback");
                throw;
            }
        }

        /// <summary>
        /// Stops the current session.
        /// </summary>
        public async Task StopAsync()
        {
            if (_pipeline == null)
                throw new InvalidOperationException("No session loaded");

            try
            {
                Logger.Debug("Stopping playback");
                await _pipeline.StopAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to stop playback");
                throw;
            }
        }

        /// <summary>
        /// Seeks to a specific time in the current session.
        /// </summary>
        public async Task SeekAsync(TimeSpan targetTime)
        {
            if (_pipeline == null)
                throw new InvalidOperationException("No session loaded");

            try
            {
                Logger.Debug($"Seeking to: {targetTime}");
                await _pipeline.SeekAsync(targetTime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to seek");
                throw;
            }
        }

        /// <summary>
        /// Sets the frequency gate mode for a specific frequency.
        /// </summary>
        public void SetFrequencyGate(double frequency, FrequencyGateMode mode)
        {
            if (_pipeline == null)
                throw new InvalidOperationException("No session loaded");

            try
            {
                _pipeline.SetFrequencyGate(frequency, mode);
                Logger.Debug($"Frequency gate set: {frequency:F1} Hz = {mode}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set frequency gate for {frequency:F1} Hz");
                throw;
            }
        }

        /// <summary>
        /// Gets session statistics.
        /// </summary>
        public SessionStatistics GetStatistics()
        {
            return new SessionStatistics
            {
                IsLoaded = IsSessionLoaded,
                FilePath = _currentFilePath,
                TotalDuration = TotalDuration,
                TotalPackets = TotalPackets,
                RamUsageMB = IsSessionLoaded ? 10 : 0 // Approximate
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Logger.Debug("PlaybackSessionManager disposing");

            UnloadSession();

            _disposed = true;
        }
    }

    #region Event Args

    public class SessionLoadedEventArgs : EventArgs
    {
        public FilePacketSource PacketSource { get; }
        public FilePlaybackPipeline Pipeline { get; }
        public string FilePath { get; }
        public TimeSpan TotalDuration { get; }
        public int TotalPackets { get; }

        public SessionLoadedEventArgs(
            FilePacketSource packetSource,
            FilePlaybackPipeline pipeline,
            string filePath,
            TimeSpan totalDuration,
            int totalPackets)
        {
            PacketSource = packetSource;
            Pipeline = pipeline;
            FilePath = filePath;
            TotalDuration = totalDuration;
            TotalPackets = totalPackets;
        }
    }

    public class SessionErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string FilePath { get; }

        public SessionErrorEventArgs(Exception exception, string filePath)
        {
            Exception = exception;
            FilePath = filePath;
        }
    }

    public class SessionStatistics
    {
        public bool IsLoaded { get; init; }
        public string FilePath { get; init; } = string.Empty;
        public TimeSpan TotalDuration { get; init; }
        public int TotalPackets { get; init; }
        public int RamUsageMB { get; init; }
    }

    #endregion
}
