using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Playback;
using AeroDebrief.UI.Services;
using NLog;

namespace AeroDebrief.UI.ViewModels.Player
{
    /// <summary>
    /// Base class for player mode view models (Recording and Playback).
    /// Contains shared functionality and services.
    /// </summary>
    public abstract class PlayerModeBase : ViewModelBase, IDisposable
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Shared services
        protected readonly FrequencyManager _frequencyManager;
        protected readonly MixerController _mixerController;
        protected readonly UnifiedGraphViewModel _graphViewModel;

        // Shared state
        protected PlaybackState _playbackState = PlaybackState.Stopped;
        protected TimeSpan _currentPosition = TimeSpan.Zero;
        protected TimeSpan _totalDuration = TimeSpan.Zero;
        protected string _statusMessage = "Ready";
        protected bool _isBuffering = false;

        // Shared collections
        public ObservableCollection<FrequencyGroupViewModel> Frequencies { get; } = new();
        public ObservableCollection<MixerChannelViewModel> MixerChannels { get; } = new();

        #region Shared Properties

        public PlaybackState PlaybackState
        {
            get => _playbackState;
            set
            {
                if (SetProperty(ref _playbackState, value))
                {
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(IsStopped));
                }
            }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (SetProperty(ref _currentPosition, value))
                {
                    OnPropertyChanged(nameof(CurrentPositionDisplay));
                }
            }
        }

        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (SetProperty(ref _totalDuration, value))
                {
                    OnPropertyChanged(nameof(TotalDurationDisplay));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBuffering
        {
            get => _isBuffering;
            set => SetProperty(ref _isBuffering, value);
        }

        // Computed properties
        public bool IsPlaying => PlaybackState == PlaybackState.Playing;
        public bool IsPaused => PlaybackState == PlaybackState.Paused;
        public bool IsStopped => PlaybackState == PlaybackState.Stopped;
        public string CurrentPositionDisplay => CurrentPosition.ToString(@"hh\:mm\:ss");
        public string TotalDurationDisplay => TotalDuration.ToString(@"hh\:mm\:ss");

        #endregion

        #region Shared Commands

        public ICommand PlayCommand { get; protected set; }
        public ICommand PauseCommand { get; protected set; }
        public ICommand StopCommand { get; protected set; }
        public ICommand SeekCommand { get; protected set; }

        #endregion

        protected PlayerModeBase(
            FrequencyManager frequencyManager,
            MixerController mixerController,
            UnifiedGraphViewModel graphViewModel)
        {
            _frequencyManager = frequencyManager ?? throw new ArgumentNullException(nameof(frequencyManager));
            _mixerController = mixerController ?? throw new ArgumentNullException(nameof(mixerController));
            _graphViewModel = graphViewModel ?? throw new ArgumentNullException(nameof(graphViewModel));

            // Wire up shared events
            WireSharedEvents();
        }

        protected virtual void WireSharedEvents()
        {
            // Frequency events
            _frequencyManager.SelectionChanged += OnFrequencySelectionChanged;
            _frequencyManager.FrequenciesLoaded += OnFrequenciesLoaded;

            // Mixer events
            _mixerController.ChannelAdded += OnMixerChannelAdded;
            _mixerController.ChannelRemoved += OnMixerChannelRemoved;
            _mixerController.ChannelChanged += OnMixerChannelChanged;
        }

        #region Shared Event Handlers

        protected virtual void OnFrequencySelectionChanged(object? sender, FrequencySelectionChangedEventArgs e)
        {
            Logger.Debug($"Frequency selection changed: {e.Frequency:F1} Hz = {e.IsSelected}");
        }

        protected virtual void OnFrequenciesLoaded(object? sender, FrequenciesLoadedEventArgs e)
        {
            Logger.Info($"Frequencies loaded: {e.TotalFrequencies} found");
        }

        protected virtual void OnMixerChannelAdded(object? sender, ChannelAddedEventArgs e)
        {
            var channel = _mixerController.GetChannel(e.Frequency);
            if (channel != null && !MixerChannels.Contains(channel))
            {
                MixerChannels.Add(channel);
            }
        }

        protected virtual void OnMixerChannelRemoved(object? sender, ChannelRemovedEventArgs e)
        {
            var channel = MixerChannels.FirstOrDefault(ch => Math.Abs(ch.Frequency - e.Frequency) < 0.1);
            if (channel != null)
            {
                MixerChannels.Remove(channel);
            }
        }

        protected virtual void OnMixerChannelChanged(object? sender, ChannelChangedEventArgs e)
        {
            Logger.Debug($"Mixer channel changed: {e.Frequency:F1} Hz");
        }

        #endregion

        #region Shared Methods

        /// <summary>
        /// Updates channel gain (volume) for a specific frequency.
        /// </summary>
        public void UpdateChannelGain(double frequency, float gain)
        {
            _mixerController.SetChannelGain(frequency, gain);
        }

        /// <summary>
        /// Updates channel mute state for a specific frequency.
        /// </summary>
        public void UpdateChannelMute(double frequency, bool muted)
        {
            _mixerController.SetChannelMuted(frequency, muted);

            // Sync with graph visualization
            var freqId = $"{frequency:F0}";
            _graphViewModel.SetFrequencyVisible(freqId, !muted);
        }

        /// <summary>
        /// Updates channel solo state for a specific frequency.
        /// </summary>
        public void UpdateChannelSolo(double frequency, bool solo)
        {
            _mixerController.SetChannelSolo(frequency, solo);
        }

        #endregion

        #region Abstract Methods (Must be implemented by derived classes)

        /// <summary>
        /// Activates this player mode.
        /// </summary>
        public abstract void Activate();

        /// <summary>
        /// Deactivates this player mode and cleans up resources.
        /// </summary>
        public abstract void Deactivate();

        #endregion

        public virtual void Dispose()
        {
            // Unwire events
            _frequencyManager.SelectionChanged -= OnFrequencySelectionChanged;
            _frequencyManager.FrequenciesLoaded -= OnFrequenciesLoaded;
            _mixerController.ChannelAdded -= OnMixerChannelAdded;
            _mixerController.ChannelRemoved -= OnMixerChannelRemoved;
            _mixerController.ChannelChanged -= OnMixerChannelChanged;

            Logger.Debug($"{GetType().Name} disposed");
        }
    }
}
