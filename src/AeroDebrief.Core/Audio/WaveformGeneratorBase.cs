using NLog;
using AeroDebrief.Core.IO; // NEW: Add FilePacketSource support

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Abstract base class for waveform generators that contains all common logic
    /// Eliminates code duplication between CPU and GPU implementations
    /// </summary>
    public abstract class WaveformGeneratorBase : IWaveformGenerator
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        protected readonly object LockObject = new();
        protected readonly Dictionary<double, WaveformChannel> Channels = new();
        
        protected bool Disposed;
        protected TimeSpan ResolutionValue = TimeSpan.FromMilliseconds(50);
        protected int MaxDataPointsValue = 2000;

        public event EventHandler<WaveformUpdatedEventArgs>? WaveformUpdated;

        #region Properties

        public TimeSpan Resolution
        {
            get => ResolutionValue;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Resolution must be positive", nameof(value));
                ResolutionValue = value;
            }
        }

        public int MaxDataPoints
        {
            get => MaxDataPointsValue;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Max data points must be positive", nameof(value));
                MaxDataPointsValue = value;
            }
        }

        IReadOnlyDictionary<double, WaveformChannel> IWaveformGenerator.Channels
        {
            get
            {
                lock (LockObject)
                {
                    return new Dictionary<double, WaveformChannel>(this.Channels);
                }
            }
        }

        #endregion

        #region Public Methods

        public async Task<WaveformData> GenerateWaveformAsync(
            string filePath,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            Logger.Info($"Generating waveform for {selectedFrequencies.Count} frequencies");
            progress?.Report(0);

            try
            {
                return await Task.Run(async () =>
                {
                    var waveformData = new WaveformData
                    {
                        SelectedFrequencies = new HashSet<double>(selectedFrequencies),
                        IsFiltered = selectedFrequencies.Count > 0
                    };

                    progress?.Report(10);

                    var packets = await ReadAndFilterPacketsAsync(filePath, selectedFrequencies, cancellationToken);
                    
                    progress?.Report(30);

                    if (packets.Count == 0)
                    {
                        Logger.Warn("No packets found for waveform generation");
                        waveformData.CombinedWaveform = new float[MaxDataPointsValue];
                        OnWaveformUpdated();
                        progress?.Report(100);
                        return waveformData;
                    }

                    await GenerateChannelWaveformsAsync(waveformData, packets, progress, cancellationToken);
                    
                    progress?.Report(90);
                    
                    GenerateCombinedWaveform(waveformData);
                    UpdateInternalChannels(waveformData);

                    Logger.Info($"Generated waveform with {waveformData.CombinedWaveform.Length} points for {waveformData.Channels.Count} channels");
                    
                    OnWaveformUpdated();
                    Logger.Debug("WaveformUpdated event fired after generation complete");
                    
                    progress?.Report(100);
                    
                    return waveformData;

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate waveform");
                throw;
            }
        }

        public async Task<WaveformData> GenerateWaveformFromPacketsAsync(
            List<AudioPacketMetadata> packets,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating waveform from {packets.Count} packets");
            progress?.Report(0);

            try
            {
                return await Task.Run(async () =>
                {
                    var waveformData = new WaveformData
                    {
                        SelectedFrequencies = new HashSet<double>(selectedFrequencies),
                        IsFiltered = selectedFrequencies.Count > 0
                    };

                    progress?.Report(20);

                    if (packets.Count == 0)
                    {
                        waveformData.CombinedWaveform = new float[MaxDataPointsValue];
                        OnWaveformUpdated();
                        progress?.Report(100);
                        return waveformData;
                    }

                    await GenerateChannelWaveformsAsync(waveformData, packets, progress, cancellationToken);
                    
                    progress?.Report(90);
                    
                    GenerateCombinedWaveform(waveformData);
                    UpdateInternalChannels(waveformData);

                    OnWaveformUpdated();
                    Logger.Debug("WaveformUpdated event fired after generation from packets complete");

                    progress?.Report(100);

                    return waveformData;

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate waveform from packets");
                throw;
            }
        }

        /// <summary>
        /// Generates waveform from IPacketSource (memory-mapped, efficient)
        /// Default implementation - subclasses should override for optimal performance
        /// </summary>
        public virtual async Task<WaveformData> GenerateWaveformFromSourceAsync(
            IPacketSource source,
            TimeSpan from,
            TimeSpan to,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"GenerateWaveformFromSourceAsync (base): from={from}, to={to}, frequencies={selectedFrequencies.Count}");
            
            // For now, if it's a FilePacketSource, use the existing implementation
            if (source is FilePacketSource fileSource)
            {
                return await GenerateWaveformFromFilePacketSourceAsync(fileSource, from, to, selectedFrequencies, progress, cancellationToken);
            }
            
            // For DuckDBPacketSource or other sources, we'll need to implement later
            // For now, return empty waveform
            Logger.Warn($"Waveform generation not yet implemented for {source.GetType().Name}");
            return new WaveformData
            {
                SelectedFrequencies = selectedFrequencies,
                CombinedWaveform = new float[MaxDataPointsValue],
                IsFiltered = selectedFrequencies.Count > 0
            };
        }

        /// <summary>
        /// Helper method for FilePacketSource-based generation
        /// </summary>
        protected virtual async Task<WaveformData> GenerateWaveformFromFilePacketSourceAsync(
            FilePacketSource source,
            TimeSpan from,
            TimeSpan to,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Default empty implementation - subclasses override this
            throw new NotImplementedException("Subclass must implement GenerateWaveformFromFilePacketSourceAsync");
        }
       

        public float[] GetCombinedWaveform()
        {
            lock (LockObject)
            {
                if (!Channels.Any())
                {
                    return new float[MaxDataPointsValue];
                }

                var combined = new float[MaxDataPointsValue];
                var activeChannels = Channels.Values.Where(c => c.IsActive).ToList();

                if (!activeChannels.Any())
                {
                    return new float[MaxDataPointsValue];
                }

                foreach (var channel in activeChannels)
                {
                    for (int i = 0; i < combined.Length && i < channel.Data.Length; i++)
                    {
                        combined[i] += channel.Data[i];
                    }
                }

                for (int i = 0; i < combined.Length; i++)
                {
                    combined[i] /= activeChannels.Count;
                }

                return combined;
            }
        }

        public float[]? GetChannelWaveform(double frequency)
        {
            lock (LockObject)
            {
                if (Channels.TryGetValue(frequency, out var channel))
                {
                    return (float[])channel.Data.Clone();
                }
            }
            return null;
        }

        public void Clear()
        {
            lock (LockObject)
            {
                Channels.Clear();
            }

            OnWaveformUpdated();
        }

        public abstract void Dispose();

        #endregion

        #region Protected Methods - Event Helpers

        /// <summary>
        /// Raises the WaveformUpdated event (accessible from derived classes)
        /// </summary>
        protected void OnWaveformUpdated()
        {
            WaveformUpdated?.Invoke(this, new WaveformUpdatedEventArgs(((IWaveformGenerator)this).Channels));
        }

        #endregion

        #region Protected Methods - Common Utilities

        protected async Task<List<AudioPacketMetadata>> ReadAndFilterPacketsAsync(
            string filePath,
            HashSet<double> selectedFrequencies,
            CancellationToken cancellationToken)
        {
            if (selectedFrequencies.Count == 0)
            {
                Logger.Debug("No frequencies selected, returning empty packet list");
                return new List<AudioPacketMetadata>();
            }

            return await Task.Run(() =>
            {
                var packets = new List<AudioPacketMetadata>();

                foreach (var metadata in IO.RecordingFileReader.EnumeratePackets(filePath, cancellationToken))
                {
                    if (selectedFrequencies.Contains(metadata.Frequency))
                    {
                        packets.Add(metadata);
                    }
                }

                return packets.OrderBy(p => p.Timestamp).ToList();
            }, cancellationToken);
        }

        protected void GenerateCombinedWaveform(WaveformData waveformData)
        {
            var combined = new float[MaxDataPointsValue];

            if (waveformData.Channels.Count == 0)
            {
                waveformData.CombinedWaveform = combined;
                return;
            }

            foreach (var channel in waveformData.Channels.Values)
            {
                for (int i = 0; i < combined.Length && i < channel.Data.Length; i++)
                {
                    combined[i] += channel.Data[i];
                }
            }

            for (int i = 0; i < combined.Length; i++)
            {
                combined[i] /= waveformData.Channels.Count;
            }

            waveformData.CombinedWaveform = combined;
        }

        protected void UpdateInternalChannels(WaveformData waveformData)
        {
            lock (LockObject)
            {
                Channels.Clear();
                foreach (var (frequency, channel) in waveformData.Channels)
                {
                    Channels[frequency] = channel;
                }
            }
        }

        protected WaveformChannel CreateChannelWaveform(double frequency)
        {
            return new WaveformChannel
            {
                Frequency = frequency,
                Data = new float[MaxDataPointsValue],
                TimeStamps = new DateTime[MaxDataPointsValue],
                IsActive = true,
                DisplayName = $"{frequency / 1_000_000.0:F3} MHz"
            };
        }

        protected void FillTimestamps(WaveformChannel channel, DateTime startTime, long timeStepTicks)
        {
            for (int i = 0; i < MaxDataPointsValue; i++)
            {
                channel.TimeStamps[i] = startTime + TimeSpan.FromTicks(i * timeStepTicks);
            }
        }

        protected (DateTime StartTime, DateTime EndTime, TimeSpan TotalDuration) GetTimeRange(List<AudioPacketMetadata> packets)
        {
            var startTime = packets[0].Timestamp;
            var endTime = packets[^1].Timestamp;
            var totalDuration = endTime - startTime;
            return (startTime, endTime, totalDuration);
        }

        protected Dictionary<double, List<AudioPacketMetadata>> GroupPacketsByFrequency(List<AudioPacketMetadata> packets)
        {
            return packets.GroupBy(p => p.Frequency).ToDictionary(g => g.Key, g => g.ToList());
        }

        #endregion

        #region Abstract Methods - Implementation Specific

        /// <summary>
        /// Generates waveforms for all frequency channels
        /// </summary>
        /// <param name="waveformData">Waveform data container</param>
        /// <param name="packets">Audio packets to process</param>
        /// <param name="progress">Optional progress reporter (reports 30-85% range)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected abstract Task GenerateChannelWaveformsAsync(
            WaveformData waveformData,
            List<AudioPacketMetadata> packets,
            IProgress<double>? progress,
            CancellationToken cancellationToken);

        #endregion
    }

    #region Shared Model Classes

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

    #endregion
}


