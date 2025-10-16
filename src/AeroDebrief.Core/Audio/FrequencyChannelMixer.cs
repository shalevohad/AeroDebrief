using AeroDebrief.Core.Models;
using AeroDebrief.Core.Helpers;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Enhanced audio mixer with per-frequency channel control and real-time processing
    /// </summary>
    public sealed class FrequencyChannelMixer : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new();
        private readonly Dictionary<double, ChannelSettings> _channelSettings = new();
        
        private bool _disposed;
        private float _masterGain = 1.0f;
        private bool _isMuted;

        public event EventHandler<ChannelSettingsChangedEventArgs>? ChannelSettingsChanged;

        /// <summary>
        /// Gets or sets the master gain (0.0 to 2.0)
        /// </summary>
        public float MasterGain
        {
            get => _masterGain;
            set
            {
                _masterGain = Math.Clamp(value, 0f, 2f);
                Logger.Debug($"Master gain set to {_masterGain:F2}");
            }
        }

        /// <summary>
        /// Gets or sets whether the entire mixer is muted
        /// </summary>
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                Logger.Debug($"Master mute set to {_isMuted}");
            }
        }

        /// <summary>
        /// Gets all channel settings
        /// </summary>
        public IReadOnlyDictionary<double, ChannelSettings> ChannelSettings
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<double, ChannelSettings>(_channelSettings);
                }
            }
        }

        public FrequencyChannelMixer()
        {
            Logger.Debug("FrequencyChannelMixer initialized");
        }

        /// <summary>
        /// Sets up a frequency channel with default settings
        /// </summary>
        public void SetupChannel(double frequency, string displayName)
        {
            lock (_lockObject)
            {
                if (!_channelSettings.ContainsKey(frequency))
                {
                    _channelSettings[frequency] = new ChannelSettings
                    {
                        Frequency = frequency,
                        DisplayName = displayName,
                        Gain = 1.0f,
                        Pan = 0.0f,
                        IsMuted = false,
                        IsSolo = false,
                        IsActive = false
                    };

                    Logger.Debug($"Setup channel for {displayName} ({frequency:F1} Hz)");
                }
            }
        }

        /// <summary>
        /// Sets the gain for a specific frequency channel
        /// </summary>
        public void SetChannelGain(double frequency, float gain)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    var oldGain = settings.Gain;
                    settings.Gain = Math.Clamp(gain, 0f, 2f);
                    
                    if (Math.Abs(oldGain - settings.Gain) > 0.001f)
                    {
                        Logger.Debug($"Channel gain changed: {frequency:F1} Hz = {settings.Gain:F2}");
                        ChannelSettingsChanged?.Invoke(this, new ChannelSettingsChangedEventArgs(frequency, settings));
                    }
                }
            }
        }

        /// <summary>
        /// Sets the pan for a specific frequency channel
        /// </summary>
        public void SetChannelPan(double frequency, float pan)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    var oldPan = settings.Pan;
                    settings.Pan = Math.Clamp(pan, -1f, 1f);
                    
                    if (Math.Abs(oldPan - settings.Pan) > 0.001f)
                    {
                        Logger.Debug($"Channel pan changed: {frequency:F1} Hz = {settings.Pan:F2}");
                        ChannelSettingsChanged?.Invoke(this, new ChannelSettingsChangedEventArgs(frequency, settings));
                    }
                }
            }
        }

        /// <summary>
        /// Sets whether a channel is muted
        /// </summary>
        public void SetChannelMuted(double frequency, bool muted)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    if (settings.IsMuted != muted)
                    {
                        settings.IsMuted = muted;
                        Logger.Debug($"Channel mute changed: {frequency:F1} Hz = {muted}");
                        ChannelSettingsChanged?.Invoke(this, new ChannelSettingsChangedEventArgs(frequency, settings));
                    }
                }
            }
        }

        /// <summary>
        /// Sets whether a channel is soloed (only solo channels will be heard)
        /// </summary>
        public void SetChannelSolo(double frequency, bool solo)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    if (settings.IsSolo != solo)
                    {
                        settings.IsSolo = solo;
                        Logger.Debug($"Channel solo changed: {frequency:F1} Hz = {solo}");
                        ChannelSettingsChanged?.Invoke(this, new ChannelSettingsChangedEventArgs(frequency, settings));
                    }
                }
            }
        }

        /// <summary>
        /// Sets whether a channel is currently active (receiving audio)
        /// </summary>
        public void SetChannelActive(double frequency, bool active)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    if (settings.IsActive != active)
                    {
                        settings.IsActive = active;
                        settings.LastActivity = active ? DateTime.UtcNow : settings.LastActivity;
                        // Don't log this as it happens frequently
                    }
                }
            }
        }

        /// <summary>
        /// Resets a channel to default settings
        /// </summary>
        public void ResetChannel(double frequency)
        {
            lock (_lockObject)
            {
                if (_channelSettings.TryGetValue(frequency, out var settings))
                {
                    settings.Gain = 1.0f;
                    settings.Pan = 0.0f;
                    settings.IsMuted = false;
                    settings.IsSolo = false;
                    
                    Logger.Debug($"Channel reset: {frequency:F1} Hz");
                    ChannelSettingsChanged?.Invoke(this, new ChannelSettingsChangedEventArgs(frequency, settings));
                }
            }
        }

        /// <summary>
        /// Gets the effective gain for a frequency channel (considering master gain, mute, solo)
        /// </summary>
        public float GetEffectiveGain(double frequency)
        {
            lock (_lockObject)
            {
                if (!_channelSettings.TryGetValue(frequency, out var settings))
                    return 0f;

                // Check if any channels are soloed
                var hasSoloChannels = _channelSettings.Values.Any(s => s.IsSolo);
                
                // If channels are soloed and this isn't one of them, return 0
                if (hasSoloChannels && !settings.IsSolo)
                    return 0f;

                // If master or channel is muted, return 0
                if (_isMuted || settings.IsMuted)
                    return 0f;

                // Return effective gain
                return settings.Gain * _masterGain;
            }
        }

        /// <summary>
        /// Gets the pan setting for a frequency channel
        /// </summary>
        public float GetChannelPan(double frequency)
        {
            lock (_lockObject)
            {
                return _channelSettings.TryGetValue(frequency, out var settings) ? settings.Pan : 0f;
            }
        }

        /// <summary>
        /// Processes audio data for a specific frequency channel
        /// </summary>
        public byte[] ProcessChannelAudio(double frequency, byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            var effectiveGain = GetEffectiveGain(frequency);
            if (effectiveGain <= 0f)
                return new byte[audioData.Length]; // Return silence

            var pan = GetChannelPan(frequency);

            // Process audio with gain and pan
            return ApplyGainAndPan(audioData, effectiveGain, pan);
        }

        /// <summary>
        /// Removes a channel from the mixer
        /// </summary>
        public void RemoveChannel(double frequency)
        {
            lock (_lockObject)
            {
                if (_channelSettings.Remove(frequency))
                {
                    Logger.Debug($"Channel removed: {frequency:F1} Hz");
                }
            }
        }

        /// <summary>
        /// Clears all channels
        /// </summary>
        public void ClearChannels()
        {
            lock (_lockObject)
            {
                var count = _channelSettings.Count;
                _channelSettings.Clear();
                Logger.Debug($"Cleared {count} channels");
            }
        }

        private static byte[] ApplyGainAndPan(byte[] audioData, float gain, float pan)
        {
            if (Math.Abs(gain - 1.0f) < 0.001f && Math.Abs(pan) < 0.001f)
                return audioData; // No processing needed

            try
            {
                // Decode audio using AudioHelpers (handles both Opus and PCM)
                var pcmSamples = AudioHelpers.DecodeAudioToPcm(audioData);
                
                // SRS audio is mono (1 channel), but we can simulate stereo panning
                // by creating a stereo output where pan affects left/right balance
                
                // For mono input, apply gain directly (pan has no effect on mono)
                // If we want to support stereo output in the future, we'd need to:
                // 1. Duplicate mono to stereo
                // 2. Apply different gains to left/right based on pan
                
                // For now, apply gain only (mono audio)
                if (Math.Abs(pan) > 0.001f)
                {
                    Logger.Warn($"Pan value {pan:F2} specified but SRS audio is mono - pan will be ignored");
                }
                
                for (int i = 0; i < pcmSamples.Length; i++)
                {
                    // Apply gain with safe clamping
                    var processedSample = pcmSamples[i] * gain;
                    pcmSamples[i] = (short)Math.Clamp(processedSample, short.MinValue, short.MaxValue);
                }

                // Convert back to bytes
                return AudioHelpers.ConvertPcm16ToBytes(pcmSamples);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to apply gain and pan to audio data ({audioData.Length} bytes)");
                // Return original data on error
                return audioData;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ClearChannels();
            _disposed = true;
            Logger.Debug("FrequencyChannelMixer disposed");
        }
    }

    /// <summary>
    /// Settings for a frequency channel in the mixer
    /// </summary>
    public class ChannelSettings
    {
        public double Frequency { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public float Gain { get; set; } = 1.0f;
        public float Pan { get; set; } = 0.0f;
        public bool IsMuted { get; set; }
        public bool IsSolo { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets a formatted display string for the pan value
        /// </summary>
        public string GetPanDisplayText()
        {
            if (Math.Abs(Pan) < 0.01f)
                return "Center";
            
            return Pan > 0 ? $"R{Pan:F2}" : $"L{Math.Abs(Pan):F2}";
        }
    }

    /// <summary>
    /// Event args for channel settings changes
    /// </summary>
    public class ChannelSettingsChangedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public ChannelSettings Settings { get; }

        public ChannelSettingsChangedEventArgs(double frequency, ChannelSettings settings)
        {
            Frequency = frequency;
            Settings = settings;
        }
    }
}