using System;
using System.Collections.ObjectModel;
using System.Linq;
using AeroDebrief.Core.Audio;
using AeroDebrief.UI.ViewModels;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Service responsible for managing audio mixer channels and settings.
    /// Implements Separation of Concerns by handling ONLY mixer-related operations.
    /// </summary>
    public sealed class MixerController : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private AudioMixerEngine? _mixer;
        private readonly ObservableCollection<MixerChannelViewModel> _channels = new();
        private bool _disposed;

        /// <summary>
        /// Gets the read-only collection of mixer channels.
        /// </summary>
        public ReadOnlyObservableCollection<MixerChannelViewModel> Channels { get; }

        /// <summary>
        /// Gets whether the mixer is initialized.
        /// </summary>
        public bool IsInitialized => _mixer != null;

        /// <summary>
        /// Raised when a channel is added.
        /// </summary>
        public event EventHandler<ChannelAddedEventArgs>? ChannelAdded;

        /// <summary>
        /// Raised when a channel is removed.
        /// </summary>
        public event EventHandler<ChannelRemovedEventArgs>? ChannelRemoved;

        /// <summary>
        /// Raised when a channel's settings are changed.
        /// </summary>
        public event EventHandler<ChannelChangedEventArgs>? ChannelChanged;

        public MixerController()
        {
            Channels = new ReadOnlyObservableCollection<MixerChannelViewModel>(_channels);
            Logger.Debug("MixerController initialized");
        }

        /// <summary>
        /// Initializes the mixer engine.
        /// </summary>
        public void Initialize()
        {
            if (_mixer != null)
            {
                Logger.Warn("MixerController already initialized");
                return;
            }

            try
            {
                Logger.Info("Initializing audio mixer engine...");
                _mixer = new AudioMixerEngine();
                Logger.Info("? Audio mixer engine initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize audio mixer");
                throw;
            }
        }

        /// <summary>
        /// Sets up a mixer channel for a specific frequency.
        /// Includes defensive auto-initialization to prevent race conditions.
        /// </summary>
        public void SetupChannel(double frequency, string displayName)
        {
            // DEFENSIVE PROGRAMMING: Auto-initialize if not already initialized
            // This prevents race conditions and provides better user experience
            if (_mixer == null)
            {
                Logger.Warn("?? MixerController.SetupChannel() called before Initialize() - auto-initializing to prevent crash");
                Logger.Warn($"   Channel: {displayName} ({frequency:F1} Hz)");
                Logger.Warn("   This indicates an initialization order issue that should be fixed");
                
                try
                {
                    Initialize();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to auto-initialize mixer - cannot setup channel");
                    throw new InvalidOperationException(
                        $"MixerController not initialized and auto-initialization failed for channel '{displayName}'. " +
                        "Call Initialize() explicitly before using mixer channels.", ex);
                }
            }

            try
            {
                // Check if channel already exists
                var existingChannel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (existingChannel != null)
                {
                    Logger.Debug($"Channel already exists: {displayName}");
                    return;
                }

                Logger.Debug($"Setting up mixer channel: {displayName} ({frequency:F1} Hz)");

                // Setup in mixer engine
                _mixer.SetupChannel(frequency, displayName);

                // Create view model
                var channelViewModel = new MixerChannelViewModel
                {
                    Frequency = frequency,
                    DisplayName = displayName,
                    Volume = 1.0f,
                    Pan = 0.0f,
                    IsMuted = false,
                    IsSolo = false
                };

                _channels.Add(channelViewModel);

                Logger.Info($"? Mixer channel added: {displayName}");

                ChannelAdded?.Invoke(this, new ChannelAddedEventArgs(frequency, displayName));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to setup mixer channel: {displayName}");
                throw;
            }
        }

        /// <summary>
        /// Removes a mixer channel for a specific frequency.
        /// </summary>
        public void RemoveChannel(double frequency)
        {
            if (_mixer == null)
                return;

            try
            {
                var channel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel == null)
                {
                    Logger.Debug($"Channel not found: {frequency:F1} Hz");
                    return;
                }

                Logger.Debug($"Removing mixer channel: {channel.DisplayName}");

                _mixer.RemoveChannel(frequency);
                _channels.Remove(channel);

                Logger.Info($"? Mixer channel removed: {channel.DisplayName}");

                ChannelRemoved?.Invoke(this, new ChannelRemovedEventArgs(frequency, channel.DisplayName));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to remove mixer channel: {frequency:F1} Hz");
            }
        }

        /// <summary>
        /// Sets the gain (volume) for a specific channel.
        /// </summary>
        public void SetChannelGain(double frequency, float gain)
        {
            if (_mixer == null)
            {
                Logger.Error($"?? MixerController.SetChannelGain() called before Initialize() - Frequency: {frequency:F1} Hz, Gain: {gain:F2}");
                throw new InvalidOperationException(
                    $"MixerController not initialized. Cannot set gain for frequency {frequency:F1} Hz. " +
                    "Call Initialize() first.");
            }

            try
            {
                _mixer.SetChannelGain(frequency, gain);

                var channel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel != null)
                {
                    channel.Volume = gain;
                }

                Logger.Debug($"Channel gain set: {frequency:F1} Hz = {gain:F2}");

                ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(frequency, ChannelProperty.Gain, gain));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set channel gain: {frequency:F1} Hz");
                throw;
            }
        }

        /// <summary>
        /// Sets the pan for a specific channel.
        /// </summary>
        public void SetChannelPan(double frequency, float pan)
        {
            if (_mixer == null)
            {
                Logger.Error($"?? MixerController.SetChannelPan() called before Initialize() - Frequency: {frequency:F1} Hz, Pan: {pan:F2}");
                throw new InvalidOperationException(
                    $"MixerController not initialized. Cannot set pan for frequency {frequency:F1} Hz. " +
                    "Call Initialize() first.");
            }

            try
            {
                _mixer.SetChannelPan(frequency, pan);

                var channel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel != null)
                {
                    channel.Pan = pan;
                }

                Logger.Debug($"Channel pan set: {frequency:F1} Hz = {pan:F2}");

                ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(frequency, ChannelProperty.Pan, pan));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set channel pan: {frequency:F1} Hz");
                throw;
            }
        }

        /// <summary>
        /// Sets the mute state for a specific channel.
        /// </summary>
        public void SetChannelMuted(double frequency, bool muted)
        {
            if (_mixer == null)
            {
                Logger.Error($"?? MixerController.SetChannelMuted() called before Initialize() - Frequency: {frequency:F1} Hz, Muted: {muted}");
                throw new InvalidOperationException(
                    $"MixerController not initialized. Cannot set mute for frequency {frequency:F1} Hz. " +
                    "Call Initialize() first.");
            }

            try
            {
                _mixer.SetChannelMuted(frequency, muted);

                var channel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel != null)
                {
                    channel.IsMuted = muted;
                }

                Logger.Debug($"Channel mute set: {frequency:F1} Hz = {muted}");

                ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(frequency, ChannelProperty.Muted, muted));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set channel mute: {frequency:F1} Hz");
                throw;
            }
        }

        /// <summary>
        /// Sets the solo state for a specific channel.
        /// </summary>
        public void SetChannelSolo(double frequency, bool solo)
        {
            if (_mixer == null)
            {
                Logger.Error($"?? MixerController.SetChannelSolo() called before Initialize() - Frequency: {frequency:F1} Hz, Solo: {solo}");
                throw new InvalidOperationException(
                    $"MixerController not initialized. Cannot set solo for frequency {frequency:F1} Hz. " +
                    "Call Initialize() first.");
            }

            try
            {
                _mixer.SetChannelSolo(frequency, solo);

                var channel = _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel != null)
                {
                    channel.IsSolo = solo;
                }

                Logger.Debug($"Channel solo set: {frequency:F1} Hz = {solo}");

                ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(frequency, ChannelProperty.Solo, solo));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set channel solo: {frequency:F1} Hz");
                throw;
            }
        }

        /// <summary>
        /// Clears all mixer channels.
        /// </summary>
        public void ClearChannels()
        {
            Logger.Debug($"Clearing {_channels.Count} mixer channels");

            foreach (var channel in _channels.ToList())
            {
                RemoveChannel(channel.Frequency);
            }

            _mixer?.ClearChannels();
            _channels.Clear();

            Logger.Info("? All mixer channels cleared");
        }

        /// <summary>
        /// Gets a channel by frequency.
        /// </summary>
        public MixerChannelViewModel? GetChannel(double frequency)
        {
            return _channels.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
        }

        /// <summary>
        /// Gets mixer statistics.
        /// </summary>
        public MixerStatistics GetStatistics()
        {
            return new MixerStatistics
            {
                IsInitialized = IsInitialized,
                ChannelCount = _channels.Count,
                MutedCount = _channels.Count(ch => ch.IsMuted),
                SoloCount = _channels.Count(ch => ch.IsSolo)
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Logger.Debug("MixerController disposing");

            ClearChannels();
            _mixer?.Dispose();
            _mixer = null;

            _disposed = true;
        }
    }

    #region Event Args

    public class ChannelAddedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public string DisplayName { get; }

        public ChannelAddedEventArgs(double frequency, string displayName)
        {
            Frequency = frequency;
            DisplayName = displayName;
        }
    }

    public class ChannelRemovedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public string DisplayName { get; }

        public ChannelRemovedEventArgs(double frequency, string displayName)
        {
            Frequency = frequency;
            DisplayName = displayName;
        }
    }

    public class ChannelChangedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public ChannelProperty Property { get; }
        public object Value { get; }

        public ChannelChangedEventArgs(double frequency, ChannelProperty property, object value)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
        }
    }

    public enum ChannelProperty
    {
        Gain,
        Pan,
        Muted,
        Solo
    }

    public class MixerStatistics
    {
        public bool IsInitialized { get; init; }
        public int ChannelCount { get; init; }
        public int MutedCount { get; init; }
        public int SoloCount { get; init; }
    }

    #endregion
}
