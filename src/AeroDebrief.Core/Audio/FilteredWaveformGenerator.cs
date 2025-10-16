using AeroDebrief.Core.Models;
using AeroDebrief.Core.Analysis;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Enhanced waveform generator that supports frequency filtering and multi-channel visualization
    /// </summary>
    public sealed class FilteredWaveformGenerator : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new();
        private readonly Dictionary<double, WaveformChannel> _channels = new();
        private readonly FrequencyAnalysisService _analysisService;
        
        private bool _disposed;
        private TimeSpan _resolution = TimeSpan.FromMilliseconds(50); // 50ms resolution
        private int _maxDataPoints = 2000; // Maximum points in waveform

        public event EventHandler<WaveformUpdatedEventArgs>? WaveformUpdated;

        /// <summary>
        /// Gets or sets the time resolution for waveform generation
        /// </summary>
        public TimeSpan Resolution
        {
            get => _resolution;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Resolution must be positive", nameof(value));
                _resolution = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of data points in the waveform
        /// </summary>
        public int MaxDataPoints
        {
            get => _maxDataPoints;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Max data points must be positive", nameof(value));
                _maxDataPoints = value;
            }
        }

        /// <summary>
        /// Gets the waveform channels
        /// </summary>
        public IReadOnlyDictionary<double, WaveformChannel> Channels
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<double, WaveformChannel>(_channels);
                }
            }
        }

        public FilteredWaveformGenerator(FrequencyAnalysisService analysisService)
        {
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
            Logger.Debug("FilteredWaveformGenerator initialized");
        }

        /// <summary>
        /// Generates waveform data from audio packets with frequency filtering
        /// </summary>
        public async Task<WaveformData> GenerateWaveformAsync(
            string filePath, 
            HashSet<double> selectedFrequencies, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            Logger.Info($"Generating filtered waveform for {selectedFrequencies.Count} frequencies from: {filePath}");

            try
            {
                return await Task.Run(async () =>
                {
                    var waveformData = new WaveformData
                    {
                        SelectedFrequencies = new HashSet<double>(selectedFrequencies),
                        IsFiltered = selectedFrequencies.Count > 0
                    };

                    // Read and analyze all packets
                    var packets = await ReadAndFilterPacketsAsync(filePath, selectedFrequencies, cancellationToken);
                    
                    if (packets.Count == 0)
                    {
                        Logger.Warn("No packets found for waveform generation");
                        waveformData.CombinedWaveform = new float[_maxDataPoints];
                        return waveformData;
                    }

                    // Generate waveforms per frequency and combined
                    await GenerateChannelWaveformsAsync(waveformData, packets, cancellationToken);
                    GenerateCombinedWaveform(waveformData);

                    // CRITICAL FIX: Update the internal _channels dictionary with generated channels
                    // This ensures GetCombinedWaveform() can access the channels
                    lock (_lockObject)
                    {
                        _channels.Clear();
                        foreach (var (frequency, channel) in waveformData.Channels)
                        {
                            _channels[frequency] = channel;
                        }
                        Logger.Debug($"Updated internal channels dictionary with {_channels.Count} channels");
                    }

                    Logger.Info($"Generated waveform with {waveformData.CombinedWaveform.Length} points for {waveformData.Channels.Count} channels");
                    return waveformData;

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate filtered waveform");
                throw;
            }
        }

        /// <summary>
        /// Updates waveform data in real-time as new packets arrive
        /// </summary>
        public void UpdateRealTimeWaveform(AudioPacketMetadata packet, HashSet<double> activeFrequencies)
        {
            if (_disposed || packet?.AudioPayload == null)
                return;

            try
            {
                lock (_lockObject)
                {
                    var frequency = packet.Frequency;
                    
                    // Only process if frequency is active
                    if (!activeFrequencies.Contains(frequency))
                        return;

                    // Get or create channel
                    if (!_channels.ContainsKey(frequency))
                    {
                        _channels[frequency] = new WaveformChannel
                        {
                            Frequency = frequency,
                            Data = new float[_maxDataPoints],
                            TimeStamps = new DateTime[_maxDataPoints],
                            IsActive = true
                        };
                    }

                    var channel = _channels[frequency];
                    channel.IsActive = true;

                    // Calculate amplitude for this packet
                    var amplitude = CalculatePacketAmplitude(packet.AudioPayload);
                    
                    // Add to channel data (circular buffer)
                    var index = (int)(channel.CurrentIndex % _maxDataPoints);
                    channel.Data[index] = amplitude;
                    channel.TimeStamps[index] = packet.Timestamp;
                    channel.CurrentIndex++;
                    channel.LastUpdate = DateTime.UtcNow;

                    // Mark inactive channels
                    foreach (var ch in _channels.Values)
                    {
                        ch.IsActive = activeFrequencies.Contains(ch.Frequency);
                    }
                }

                // Notify listeners
                WaveformUpdated?.Invoke(this, new WaveformUpdatedEventArgs(Channels));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating real-time waveform");
            }
        }

        /// <summary>
        /// Gets combined waveform data for all selected frequencies
        /// </summary>
        public float[] GetCombinedWaveform()
        {
            lock (_lockObject)
            {
                // CRITICAL FIX: Return blank waveform if no channels exist
                // This properly handles the "Select None" scenario
                if (!_channels.Any())
                {
                    Logger.Debug("No channels available, returning blank waveform");
                    return new float[_maxDataPoints];
                }

                var combined = new float[_maxDataPoints];
                var activeChannels = _channels.Values.Where(c => c.IsActive).ToList();
                
                if (!activeChannels.Any())
                {
                    Logger.Debug("No active channels, returning blank waveform");
                    return new float[_maxDataPoints];
                }

                // Combine all active channel waveforms
                foreach (var channel in activeChannels)
                {
                    for (int i = 0; i < combined.Length && i < channel.Data.Length; i++)
                    {
                        combined[i] += channel.Data[i];
                    }
                }

                // Average the amplitudes
                for (int i = 0; i < combined.Length; i++)
                {
                    combined[i] /= activeChannels.Count;
                }

                Logger.Debug($"Generated combined waveform from {activeChannels.Count} active channels");
                return combined;
            }
        }

        /// <summary>
        /// Gets waveform data for a specific frequency channel
        /// </summary>
        public float[]? GetChannelWaveform(double frequency)
        {
            lock (_lockObject)
            {
                if (_channels.TryGetValue(frequency, out var channel))
                {
                    return (float[])channel.Data.Clone();
                }
            }
            return null;
        }

        /// <summary>
        /// Clears all waveform data
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _channels.Clear();
            }
            Logger.Debug("Waveform data cleared");
        }

        private async Task<List<AudioPacketMetadata>> ReadAndFilterPacketsAsync(
            string filePath, 
            HashSet<double> selectedFrequencies, 
            CancellationToken cancellationToken)
        {
            var packets = new List<AudioPacketMetadata>();

            // CRITICAL FIX: If no frequencies are selected, return empty list (blank waveform)
            // Previously, the logic was backwards - empty selection would include ALL packets
            if (selectedFrequencies.Count == 0)
            {
                Logger.Debug("No frequencies selected, returning empty packet list for blank waveform");
                return packets;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            var processedPackets = 0;
            while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
            {
                if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                {
                    // Only include packets for selected frequencies
                    if (selectedFrequencies.Contains(metadata.Frequency))
                    {
                        packets.Add(metadata);
                    }

                    processedPackets++;
                    if (processedPackets % 1000 == 0)
                    {
                        Logger.Debug($"Processed {processedPackets} packets, kept {packets.Count} for waveform");
                    }
                }
                else
                {
                    break;
                }
            }

            Logger.Debug($"Filtered {packets.Count} packets from {processedPackets} total packets for {selectedFrequencies.Count} selected frequencies");
            return packets.OrderBy(p => p.Timestamp).ToList();
        }

        private async Task GenerateChannelWaveformsAsync(
            WaveformData waveformData, 
            List<AudioPacketMetadata> packets, 
            CancellationToken cancellationToken)
        {
            if (packets.Count == 0)
                return;

            var startTime = packets[0].Timestamp;
            var endTime = packets[^1].Timestamp;
            var totalDuration = endTime - startTime;

            if (totalDuration.TotalMilliseconds <= 0)
            {
                Logger.Warn("Invalid time range for waveform generation");
                return;
            }

            // Group packets by frequency
            var frequencyGroups = packets.GroupBy(p => p.Frequency).ToDictionary(g => g.Key, g => g.ToList());

            // Generate waveform for each frequency
            foreach (var (frequency, frequencyPackets) in frequencyGroups)
            {
                var channelWaveform = new WaveformChannel
                {
                    Frequency = frequency,
                    Data = new float[_maxDataPoints],
                    TimeStamps = new DateTime[_maxDataPoints],
                    IsActive = true,
                    DisplayName = $"{frequency / 1_000_000.0:F3} MHz"
                };

                await GenerateChannelDataAsync(channelWaveform, frequencyPackets, startTime, totalDuration, cancellationToken);
                waveformData.Channels[frequency] = channelWaveform;
            }
        }

        private async Task GenerateChannelDataAsync(
            WaveformChannel channel, 
            List<AudioPacketMetadata> packets, 
            DateTime startTime, 
            TimeSpan totalDuration, 
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var timeStep = TimeSpan.FromTicks(totalDuration.Ticks / _maxDataPoints);
                
                for (int i = 0; i < _maxDataPoints && !cancellationToken.IsCancellationRequested; i++)
                {
                    var windowStart = startTime + TimeSpan.FromTicks(i * timeStep.Ticks);
                    var windowEnd = windowStart + timeStep;
                    
                    channel.TimeStamps[i] = windowStart;

                    // Find packets in this time window
                    var windowPackets = packets.Where(p => p.Timestamp >= windowStart && p.Timestamp < windowEnd).ToList();

                    if (windowPackets.Count == 0)
                    {
                        channel.Data[i] = 0f;
                        continue;
                    }

                    // Calculate RMS amplitude for this window
                    var totalAmplitude = 0.0;
                    var sampleCount = 0;

                    foreach (var packet in windowPackets)
                    {
                        if (packet.AudioPayload != null && packet.AudioPayload.Length > 0)
                        {
                            var amplitude = CalculatePacketAmplitude(packet.AudioPayload);
                            totalAmplitude += amplitude * amplitude; // RMS calculation
                            sampleCount++;
                        }
                    }

                    var rms = sampleCount > 0 ? Math.Sqrt(totalAmplitude / sampleCount) : 0;
                    channel.Data[i] = (float)rms;
                }

                channel.LastUpdate = DateTime.UtcNow;
            }, cancellationToken);
        }

        private void GenerateCombinedWaveform(WaveformData waveformData)
        {
            var combined = new float[_maxDataPoints];

            if (waveformData.Channels.Count == 0)
            {
                waveformData.CombinedWaveform = combined;
                return;
            }

            // Combine all channel waveforms
            foreach (var channel in waveformData.Channels.Values)
            {
                for (int i = 0; i < combined.Length && i < channel.Data.Length; i++)
                {
                    combined[i] += channel.Data[i];
                }
            }

            // Average the combined waveform
            for (int i = 0; i < combined.Length; i++)
            {
                combined[i] /= waveformData.Channels.Count;
            }

            waveformData.CombinedWaveform = combined;
        }

        // Made internal for unit testing access
        // Decodes Opus-encoded audio payload and computes normalized amplitude
        internal static float CalculatePacketAmplitude(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return 0f;

            // Use centralized AudioHelpers for decoding and amplitude calculation
            var pcmSamples = Helpers.AudioHelpers.DecodeAudioToPcm(audioData);
            return Helpers.AudioHelpers.CalculateNormalizedAmplitude(pcmSamples);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
            Logger.Debug("FilteredWaveformGenerator disposed");
        }
    }

    /// <summary>
    /// Waveform data for a specific frequency channel
    /// </summary>
    public class WaveformChannel
    {
        public double Frequency { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public float[] Data { get; set; } = Array.Empty<float>();
        public DateTime[] TimeStamps { get; set; } = Array.Empty<DateTime>();
        public bool IsActive { get; set; }
        public DateTime LastUpdate { get; set; }
        public long CurrentIndex { get; set; }
    }

    /// <summary>
    /// Complete waveform data with filtering information
    /// </summary>
    public class WaveformData
    {
        public float[] CombinedWaveform { get; set; } = Array.Empty<float>();
        public Dictionary<double, WaveformChannel> Channels { get; set; } = new();
        public HashSet<double> SelectedFrequencies { get; set; } = new();
        public bool IsFiltered { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalDuration { get; set; }
        public int DataPoints { get; set; }
    }

    /// <summary>
    /// Event args for waveform updates
    /// </summary>
    public class WaveformUpdatedEventArgs : EventArgs
    {
        public IReadOnlyDictionary<double, WaveformChannel> Channels { get; }

        public WaveformUpdatedEventArgs(IReadOnlyDictionary<double, WaveformChannel> channels)
        {
            Channels = channels;
        }
    }
}