using AeroDebrief.Core;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.Audio;
using AeroDebrief.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Core service that provides the essential APIs for the SRS Signal Analyzer UI
    /// All audio processing, DSP, FFT, filtering, and decoding is done through this service
    /// </summary>
    public class AudioSession : IDisposable
    {
        private AudioPacketReader? _reader;
        private List<AudioPacketMetadata> _allPackets = new();
        private float[]? _waveformData;
        private bool _disposed;
        
        // Enhanced analysis services
        private FrequencyAnalysisService? _analysisService;
        private FilteredSpectrumAnalyzer? _spectrumAnalyzer;
        private FilteredWaveformGenerator? _waveformGenerator;
        private FrequencyChannelMixer? _channelMixer;
        private readonly HashSet<double> _selectedFrequencies = new();
        
        // Store frequency colors for consistent visualization
        private readonly Dictionary<double, System.Windows.Media.Color> _frequencyColors = new();

        public event Action? PlaybackStarted;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused;
        public event Action? PlaybackResumed;
        public event Action<Exception>? PlaybackError;
        public event Action<double>? OnPlaybackProgress;
        public event Action? OnEndReached;
        
        // Enhanced events
        public event Action<FrequencyAnalysisUpdatedEventArgs>? FrequencyAnalysisUpdated;
        public event Action<SpectrumAnalysisEventArgs>? SpectrumUpdated;
        public event Action<WaveformUpdatedEventArgs>? WaveformUpdated;

        public bool IsPlaying => _reader?.IsPlaying ?? false;
        public bool IsPaused => _reader?.IsPaused ?? false;
        public TimeSpan CurrentPosition => _reader?.CurrentPosition ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _reader?.TotalDuration ?? TimeSpan.Zero;
        public string CurrentFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Loads an audio file for analysis and playback
        /// </summary>
        public async Task<bool> LoadFileAsync(string filePath)
        {
            try
            {
                if (_reader != null)
                {
                    await _reader.StopPlaybackAsync();
                    _reader.Dispose();
                }

                // Dispose existing analysis services
                _analysisService?.Dispose();
                _spectrumAnalyzer?.Dispose();
                _waveformGenerator?.Dispose();
                _channelMixer?.Dispose();

                // Store current file path
                CurrentFilePath = filePath;

                // Initialize new services - use null-safe implementations
                try
                {
                    _analysisService = new FrequencyAnalysisService();
                    _spectrumAnalyzer = new FilteredSpectrumAnalyzer(_analysisService);
                    _waveformGenerator = new FilteredWaveformGenerator(_analysisService);
                    _channelMixer = new FrequencyChannelMixer();

                    // Wire up events
                    _analysisService.AnalysisUpdated += (s, e) => FrequencyAnalysisUpdated?.Invoke(e);
                    _spectrumAnalyzer.SpectrumUpdated += (s, e) => SpectrumUpdated?.Invoke(e);
                    _waveformGenerator.WaveformUpdated += (s, e) => WaveformUpdated?.Invoke(e);
                }
                catch (Exception ex)
                {
                    var serviceLogger = NLog.LogManager.GetCurrentClassLogger();
                    serviceLogger.Warn(ex, "Failed to initialize some analysis services, using fallback implementations");
                    // Continue with null services - the rest of the code will handle this gracefully
                }

                _reader = new AudioPacketReader(filePath);
                
                // CRITICAL FIX: Set master volume immediately after creating the reader
                _reader.SetMasterVolume(1.0f);
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Info("?? AudioSession.LoadFileAsync(): Master volume set to 1.0 after loading file");
                
                WireUpEvents();

                // Start frequency analysis (only if service is available)
                if (_analysisService != null)
                {
                    try
                    {
                        await _analysisService.StartAnalysisAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        var analysisLogger = NLog.LogManager.GetCurrentClassLogger();
                        analysisLogger.Warn(ex, "Failed to start frequency analysis, continuing without it");
                    }
                }

                // Pre-load all packets for analysis
                _allPackets = _reader.ReadAllPackets().ToList();
                
                // Calculate total duration
                _reader.CalculateTotalDuration();

                // Generate initial waveform data from packets
                await GenerateWaveformDataAsync();

                return true;
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke(ex);
                CurrentFilePath = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Gets waveform data for visualization (amplitude envelope)
        /// </summary>
        public float[] GetWaveformData()
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            
            // CRITICAL FIX: When no frequencies selected, use the stored _waveformData
            // This ensures we return a consistent blank waveform instead of calling the generator
            if (_selectedFrequencies.Count == 0)
            {
                logger.Debug($"No frequencies selected, returning stored blank waveform with {_waveformData?.Length ?? 0} points");
                return _waveformData ?? Array.Empty<float>();
            }
            
            // For selected frequencies, use the generator which has the filtered channels
            if (_waveformGenerator != null)
            {
                var waveform = _waveformGenerator.GetCombinedWaveform();
                logger.Debug($"Returning generated waveform with {waveform.Length} points from {_selectedFrequencies.Count} selected frequencies");
                return waveform;
            }
            
            // Fallback to basic waveform data only if generator not initialized
            logger.Debug("Generator not available, returning fallback waveform data");
            return _waveformData ?? Array.Empty<float>();
        }

        /// <summary>
        /// Sets the color for a specific frequency to ensure consistent visualization
        /// </summary>
        public void SetFrequencyColor(double frequency, System.Windows.Media.Color color)
        {
            _frequencyColors[frequency] = color;
        }

        /// <summary>
        /// Gets per-frequency waveform data with colors for multi-colored visualization
        /// Uses the colors assigned to each frequency in the FrequencyViewModel
        /// </summary>
        public Dictionary<double, Controls.FrequencyWaveformData> GetFrequencyWaveformData()
        {
            var result = new Dictionary<double, Controls.FrequencyWaveformData>();
            
            if (_waveformGenerator == null || _selectedFrequencies.Count == 0)
                return result;

            foreach (var frequency in _selectedFrequencies.OrderBy(f => f))
            {
                var channelWaveform = _waveformGenerator.GetChannelWaveform(frequency);
                if (channelWaveform != null && channelWaveform.Length > 0)
                {
                    // Get color from stored frequency colors
                    // If not found, use a default color (this should not happen if colors are properly set)
                    var color = _frequencyColors.TryGetValue(frequency, out var storedColor) 
                        ? storedColor 
                        : System.Windows.Media.Color.FromRgb(128, 128, 128); // Gray fallback
                    
                    var freqInfo = GetAvailableFrequencies().FirstOrDefault(f => Math.Abs(f.Frequency - frequency) < 0.1);
                    
                    result[frequency] = new Controls.FrequencyWaveformData
                    {
                        Frequency = frequency,
                        WaveformData = channelWaveform,
                        Color = color,
                        DisplayName = freqInfo?.DisplayName ?? $"{frequency / 1_000_000.0:F3} MHz"
                    };
                }
            }

            return result;
        }

        /// <summary>
        /// Gets waveform data for a specific frequency channel
        /// </summary>
        public float[]? GetChannelWaveformData(double frequency)
        {
            return _waveformGenerator?.GetChannelWaveform(frequency);
        }

        /// <summary>
        /// Regenerates waveform data with current frequency selection
        /// </summary>
        public async Task RegenerateWaveformAsync(string filePath)
        {
            if (_waveformGenerator != null)
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Debug($"Regenerating waveform with {_selectedFrequencies.Count} selected frequencies");
                
                var waveformData = await _waveformGenerator.GenerateWaveformAsync(filePath, _selectedFrequencies);
                
                // CRITICAL FIX: Always update _waveformData to match what GetCombinedWaveform() will return
                // This ensures the ViewModel gets the correct data (blank for 0 frequencies, filtered for selected)
                if (_selectedFrequencies.Count == 0)
                {
                    // When no frequencies selected, ensure we store a blank waveform
                    _waveformData = new float[_waveformGenerator.MaxDataPoints];
                    logger.Debug($"Waveform cleared (0 frequencies selected), storing blank waveform with {_waveformData.Length} points");
                }
                else
                {
                    _waveformData = waveformData.CombinedWaveform;
                    logger.Debug($"Waveform regenerated: {waveformData.CombinedWaveform.Length} data points, {waveformData.Channels.Count} channels");
                }
            }
        }

        /// <summary>
        /// Gets current spectrum snapshot for real-time FFT visualization
        /// </summary>
        public ViewModels.SpectrumData GetSpectrumSnapshot()
        {
            if (_spectrumAnalyzer != null)
            {
                var coreSpectrum = _spectrumAnalyzer.GetCombinedSpectrum();
                return new ViewModels.SpectrumData
                {
                    Magnitudes = coreSpectrum.Magnitudes,
                    Frequencies = coreSpectrum.Frequencies,
                    Timestamp = coreSpectrum.Timestamp,
                    SampleRate = coreSpectrum.SampleRate
                };
            }

            // Fallback for when analyzer is not initialized
            return new ViewModels.SpectrumData
            {
                Magnitudes = new float[512],
                Frequencies = Enumerable.Range(0, 512).Select(i => i * 48000.0 / 1024).ToArray(),
                Timestamp = DateTime.UtcNow,
                SampleRate = 48000
            };
        }

        /// <summary>
        /// Gets all available frequencies from the loaded file
        /// </summary>
        public List<FrequencyInfo> GetAvailableFrequencies()
        {
            if (_reader == null)
                return new List<FrequencyInfo>();

            var frequencyModulations = _reader.GetAllFrequencyModulations();
            return frequencyModulations.Select(fm => new FrequencyInfo
            {
                Frequency = fm.Frequency,
                Modulation = fm.Modulation.ToString(),
                DisplayName = fm.GetDisplayText(),
                PacketCount = fm.Players.Sum(p => p.PacketCount),
                Players = fm.Players.ToList()
            }).ToList();
        }

        /// <summary>
        /// Sets whether a specific frequency should be active in playback
        /// </summary>
        public void SetChannelActive(double frequency, bool active)
        {
            if (active)
            {
                _selectedFrequencies.Add(frequency);
                
                // Setup the channel in the mixer if not already present
                if (_channelMixer != null)
                {
                    var freqInfo = GetAvailableFrequencies().FirstOrDefault(f => Math.Abs(f.Frequency - frequency) < 0.1);
                    var displayName = freqInfo?.DisplayName ?? $"{frequency / 1_000_000.0:F3} MHz";
                    _channelMixer.SetupChannel(frequency, displayName);
                }
            }
            else
            {
                _selectedFrequencies.Remove(frequency);
                
                // Remove channel from mixer
                _channelMixer?.RemoveChannel(frequency);
            }

            UpdateFrequencyFiltering();
        }

        /// <summary>
        /// Updates frequency filtering across all services
        /// </summary>
        public void UpdateFrequencyFiltering()
        {
            if (_reader == null || _analysisService == null)
                return;

            // Update the analysis service
            var selectedFreqMods = _reader.GetAllFrequencyModulations()
                .Where(fm => _selectedFrequencies.Contains(fm.Frequency))
                .Select(fm => (fm.Frequency, fm.Modulation))
                .ToList();

            _analysisService.UpdateSelectedFrequencies(selectedFreqMods);

            // Update reader filtering
            if (_selectedFrequencies.Count > 0)
            {
                var frequencyModulations = _reader.GetAllFrequencyModulations()
                    .Where(fm => _selectedFrequencies.Contains(fm.Frequency))
                    .ToList();
                _reader.SetFrequencyFilter(frequencyModulations);
            }
            else
            {
                _reader.ClearFrequencyFilter();
            }
        }

        /// <summary>
        /// Sets the gain for a specific frequency channel
        /// </summary>
        public void SetChannelGain(double frequency, float gain)
        {
            _channelMixer?.SetChannelGain(frequency, gain);
        }

        /// <summary>
        /// Sets the pan (left/right balance) for a specific frequency channel  
        /// </summary>
        public void SetChannelPan(double frequency, float pan)
        {
            _channelMixer?.SetChannelPan(frequency, pan);
        }

        /// <summary>
        /// Sets whether a channel is muted
        /// </summary>
        public void SetChannelMuted(double frequency, bool muted)
        {
            _channelMixer?.SetChannelMuted(frequency, muted);
        }

        /// <summary>
        /// Resets a channel to default settings
        /// </summary>
        public void ResetChannel(double frequency)
        {
            _channelMixer?.ResetChannel(frequency);
        }

        /// <summary>
        /// Starts audio playback
        /// </summary>
        public void Play()
        {
            if (_reader != null)
            {
                // CRITICAL FIX: Set master volume to 1.0 before every playback to ensure audio is audible
                _reader.SetMasterVolume(1.0f);
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Info("?? AudioSession.Play(): Master volume explicitly set to 1.0 before playback");
                
                _reader.StartPlayback();
            }
        }

        /// <summary>
        /// Pauses audio playback
        /// </summary>
        public void Pause()
        {
            _reader?.PausePlayback();
        }

        /// <summary>
        /// Stops audio playback
        /// </summary>
        public void Stop()
        {
            _reader?.StopPlayback();
        }

        /// <summary>
        /// Seeks to a specific position (0.0 to 1.0)
        /// </summary>
        public void SeekTo(double normalizedPosition)
        {
            if (_reader != null && TotalDuration.Ticks > 0)
            {
                var targetPosition = TimeSpan.FromTicks((long)(TotalDuration.Ticks * normalizedPosition));
                _reader.SeekTo(targetPosition);
            }
        }

        private void WireUpEvents()
        {
            if (_reader == null)
                return;

            _reader.PlaybackStarted += () => PlaybackStarted?.Invoke();
            _reader.PlaybackStopped += () => 
            {
                PlaybackStopped?.Invoke();
                OnEndReached?.Invoke();
            };
            _reader.PlaybackPaused += () => PlaybackPaused?.Invoke();
            _reader.PlaybackResumed += () => PlaybackResumed?.Invoke();
            _reader.PlaybackError += (ex) => PlaybackError?.Invoke(ex);
            _reader.PlaybackProgressChanged += (progress) => OnPlaybackProgress?.Invoke(progress);
        }

        private async Task GenerateWaveformDataAsync()
        {
            await Task.Run(() =>
            {
                if (_allPackets.Count == 0)
                {
                    _waveformData = Array.Empty<float>();
                    return;
                }

                // Create waveform data by analyzing audio packet amplitudes
                var waveformPoints = new List<float>();
                var timeStep = TimeSpan.FromMilliseconds(50); // 50ms resolution
                var startTime = _allPackets[0].Timestamp;
                var endTime = _allPackets[^1].Timestamp;
                var totalDuration = endTime - startTime;

                if (totalDuration.TotalMilliseconds <= 0)
                {
                    _waveformData = Array.Empty<float>();
                    return;
                }

                for (var time = startTime; time < endTime; time += timeStep)
                {
                    var packetsInWindow = _allPackets.Where(p => 
                        p.Timestamp >= time && p.Timestamp < time + timeStep).ToList();

                    if (packetsInWindow.Count == 0)
                    {
                        waveformPoints.Add(0);
                        continue;
                    }

                    // Calculate RMS amplitude for this time window
                    var totalAmplitude = 0.0;
                    var sampleCount = 0;

                    foreach (var packet in packetsInWindow)
                    {
                        if (packet.AudioPayload != null && packet.AudioPayload.Length > 0)
                        {
                            // Calculate RMS of audio samples (assuming 16-bit PCM)
                            for (int i = 0; i < packet.AudioPayload.Length - 1; i += 2)
                            {
                                var sample = BitConverter.ToInt16(packet.AudioPayload, i);
                                totalAmplitude += sample * sample;
                                sampleCount++;
                            }
                        }
                    }

                    var rms = sampleCount > 0 ? Math.Sqrt(totalAmplitude / sampleCount) : 0;
                    var normalizedAmplitude = (float)(rms / 32768.0); // Normalize to 0-1 range
                    waveformPoints.Add(normalizedAmplitude);
                }

                _waveformData = waveformPoints.ToArray();
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _reader?.Dispose();
            _analysisService?.Dispose();
            _spectrumAnalyzer?.Dispose();
            _waveformGenerator?.Dispose();
            _channelMixer?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Frequency analyzer for real-time spectrum analysis
    /// </summary>
    public class FrequencyAnalyzer
    {
        /// <summary>
        /// Gets current spectrum snapshot from the audio processing pipeline
        /// </summary>
        public ViewModels.SpectrumData GetSpectrumSnapshot()
        {
            // In a full implementation, this would perform FFT on the current audio buffer
            // For demonstration, return empty spectrum data
            return new ViewModels.SpectrumData
            {
                Magnitudes = new float[512],
                Frequencies = Enumerable.Range(0, 512).Select(i => i * 48000.0 / 1024).ToArray(),
                Timestamp = DateTime.UtcNow,
                SampleRate = 48000
            };
        }
    }

    /// <summary>
    /// Audio mixer for controlling frequency channel levels
    /// </summary>
    public class Mixer
    {
        private readonly Dictionary<double, float> _channelGains = new();
        private readonly Dictionary<double, float> _channelPans = new();

        /// <summary>
        /// Sets whether a frequency channel is active
        /// </summary>
        public void SetChannelActive(double frequency, bool active)
        {
            // Implementation would enable/disable frequency channel
        }

        /// <summary>
        /// Sets the gain for a frequency channel
        /// </summary>
        public void SetChannelGain(double frequency, float gain)
        {
            _channelGains[frequency] = Math.Clamp(gain, 0f, 2f);
        }

        /// <summary>
        /// Sets the pan for a frequency channel
        /// </summary>
        public void SetChannelPan(double frequency, float pan)
        {
            _channelPans[frequency] = Math.Clamp(pan, -1f, 1f);
        }

        /// <summary>
        /// Gets the current gain for a frequency
        /// </summary>
        public float GetChannelGain(double frequency)
        {
            return _channelGains.TryGetValue(frequency, out var gain) ? gain : 1.0f;
        }

        /// <summary>
        /// Gets the current pan for a frequency
        /// </summary>
        public float GetChannelPan(double frequency)
        {
            return _channelPans.TryGetValue(frequency, out var pan) ? pan : 0.0f;
        }
    }

    /// <summary>
    /// Frequency information for UI display
    /// </summary>
    public class FrequencyInfo
    {
        public double Frequency { get; set; }
        public string Modulation { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int PacketCount { get; set; }
        public List<PlayerFrequencyInfo> Players { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime LastActivity { get; set; }
    }
}