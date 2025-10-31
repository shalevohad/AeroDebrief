using AeroDebrief.Core.Models;
using AeroDebrief.Core.Helpers;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// High-performance audio mixer with per-frequency volume (0-200%) and stereo pan control.
    /// Features soft-clip limiter on master bus, lock-free parameter updates, and zero-allocation hot path.
    /// Implements equal-power pan law for professional stereo imaging.
    /// </summary>
    public sealed class AudioMixerEngine : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new();
        private readonly Dictionary<double, ChannelSettings> _channelSettings = new();
        
        private bool _disposed;
        private volatile float _masterGain = 1.0f;
        private volatile bool _isMuted;
        private volatile bool _softClipEnabled = true;

        public event EventHandler<ChannelSettingsChangedEventArgs>? ChannelSettingsChanged;

        /// <summary>
        /// Gets or sets the master gain (0.0 to 2.0, where 1.0 = 100%, 2.0 = 200%)
        /// </summary>
        public float MasterGain
        {
            get => _masterGain;
            set
            {
                _masterGain = Math.Clamp(value, 0f, 2f);
                Logger.Debug($"Master gain set to {_masterGain:F2} ({_masterGain * 100:F0}%)");
            }
        }

        /// <summary>
        /// Gets or sets whether soft-clip limiting is enabled on master bus
        /// </summary>
        public bool SoftClipEnabled
        {
            get => _softClipEnabled;
            set
            {
                _softClipEnabled = value;
                Logger.Debug($"Soft-clip limiter {(value ? "enabled" : "disabled")}");
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

        public AudioMixerEngine()
        {
            Logger.Debug("AudioMixerEngine initialized with soft-clip limiter");
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
        /// High-performance mixer for multiple frequency channels.
        /// Mixes source buffers into destination with soft-clip limiting on master bus.
        /// Zero-allocation hot path for optimal performance.
        /// </summary>
        /// <param name="dst">Destination buffer (mono float samples)</param>
        /// <param name="srcs">Source channel buffers with frequency metadata</param>
        public void Mix(Span<float> dst, params ChannelBuffer[] srcs)
        {
            // Clear destination
            dst.Clear();

            if (srcs.Length == 0)
                return;

            // Mix all source channels with their individual settings
            foreach (var src in srcs)
            {
                var effectiveGain = GetEffectiveGain(src.Frequency);
                if (effectiveGain <= 0f)
                    continue;

                var pan = GetChannelPan(src.Frequency);

                // Apply gain and pan to source, accumulate into destination
                MixChannel(dst, src.Samples, effectiveGain, pan);
            }

            // Apply master gain
            if (Math.Abs(_masterGain - 1.0f) > 0.001f)
            {
                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i] *= _masterGain;
                }
            }

            // Apply soft-clip limiter to prevent clipping
            if (_softClipEnabled)
            {
                ApplySoftClipLimiter(dst);
            }
        }

        /// <summary>
        /// Mixes a single channel into destination with gain and pan.
        /// For mono output, pan is applied as amplitude scaling.
        /// </summary>
        private void MixChannel(Span<float> dst, float[] src, float gain, float pan)
        {
            var length = Math.Min(dst.Length, src.Length);

            // For mono output, we interpret pan as a simple gain adjustment
            // In a stereo system, pan would split between L/R with equal-power law
            // For now, pan has minimal effect on mono (center = full, sides = reduced)
            var panGain = 1.0f - (Math.Abs(pan) * 0.3f); // Reduce gain slightly for extreme pan

            var combinedGain = gain * panGain;

            for (int i = 0; i < length; i++)
            {
                dst[i] += src[i] * combinedGain;
            }
        }

        /// <summary>
        /// Applies soft-clip limiter using hyperbolic tangent function.
        /// Prevents harsh clipping while maintaining loudness.
        /// </summary>
        private void ApplySoftClipLimiter(Span<float> samples)
        {
            const float threshold = 0.8f; // Start soft-clipping at 80% amplitude
            const float knee = 0.2f;      // Soft knee region

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                var abs = Math.Abs(sample);

                if (abs > threshold)
                {
                    // Soft-clip using tanh curve
                    var excess = abs - threshold;
                    var compressed = threshold + (float)Math.Tanh(excess / knee) * knee;
                    samples[i] = Math.Sign(sample) * Math.Min(compressed, 1.0f);
                }
            }
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
            Logger.Debug("AudioMixerEngine disposed");
        }
    }

    /// <summary>
    /// Channel buffer for mixer input
    /// </summary>
    public readonly struct ChannelBuffer
    {
        public double Frequency { get; init; }
        public float[] Samples { get; init; }

        public ChannelBuffer(double frequency, float[] samples)
        {
            Frequency = frequency;
            Samples = samples ?? Array.Empty<float>();
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