using AeroDebrief.Core.Models;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.IO;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// CPU-based waveform generator that supports frequency filtering and multi-channel visualization
    /// </summary>
    public sealed class FilteredWaveformGenerator : WaveformGeneratorBase
    {
        private readonly FrequencyAnalysisService _analysisService;

        public FilteredWaveformGenerator(FrequencyAnalysisService analysisService)
        {
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
            Logger.Debug("FilteredWaveformGenerator initialized");
        }

        protected override async Task GenerateChannelWaveformsAsync(
            WaveformData waveformData,
            List<AudioPacketMetadata> packets,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var (startTime, endTime, totalDuration) = GetTimeRange(packets);

            if (totalDuration.TotalMilliseconds <= 0)
            {
                Logger.Warn("Invalid time range for waveform generation");
                return;
            }

            var frequencyGroups = GroupPacketsByFrequency(packets);
            
            Logger.Debug($"FilteredWaveformGenerator: Processing {frequencyGroups.Count} frequency groups");

            foreach (var (frequency, frequencyPackets) in frequencyGroups)
            {
                var channel = CreateChannelWaveform(frequency);
                await GenerateChannelDataAsync(channel, frequencyPackets, startTime, totalDuration, cancellationToken);
                waveformData.Channels[frequency] = channel;
            }
            
            progress?.Report(85);
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
                var timeStep = TimeSpan.FromTicks(totalDuration.Ticks / MaxDataPointsValue);

                for (int i = 0; i < MaxDataPointsValue && !cancellationToken.IsCancellationRequested; i++)
                {
                    var windowStart = startTime + TimeSpan.FromTicks(i * timeStep.Ticks);
                    var windowEnd = windowStart + timeStep;

                    channel.TimeStamps[i] = windowStart;

                    var windowPackets = packets.Where(p => p.Timestamp >= windowStart && p.Timestamp < windowEnd).ToList();

                    if (windowPackets.Count == 0)
                    {
                        channel.Data[i] = 0f;
                        continue;
                    }

                    // Find peak amplitude in this time window
                    var maxAmplitude = 0.0f;

                    foreach (var packet in windowPackets)
                    {
                        if (packet.AudioPayload != null && packet.AudioPayload.Length > 0)
                        {
                            var amplitude = CalculatePacketAmplitude(packet.AudioPayload);
                            maxAmplitude = Math.Max(maxAmplitude, amplitude);
                        }
                    }

                    channel.Data[i] = maxAmplitude;
                }

                channel.LastUpdate = DateTime.UtcNow;
            }, cancellationToken);
        }

        /// <summary>
        /// Updates waveform data in real-time as new packets arrive
        /// </summary>
        public void UpdateRealTimeWaveform(AudioPacketMetadata packet, HashSet<double> activeFrequencies)
        {
            if (Disposed || packet?.AudioPayload == null)
                return;

            try
            {
                lock (LockObject)
                {
                    var frequency = packet.Frequency;

                    if (!activeFrequencies.Contains(frequency))
                        return;

                    if (!Channels.ContainsKey(frequency))
                    {
                        Channels[frequency] = CreateChannelWaveform(frequency);
                    }

                    var channel = Channels[frequency];
                    channel.IsActive = true;

                    var amplitude = CalculatePacketAmplitude(packet.AudioPayload);

                    var index = (int)(channel.CurrentIndex % MaxDataPointsValue);
                    channel.Data[index] = amplitude;
                    channel.TimeStamps[index] = packet.Timestamp;
                    channel.CurrentIndex++;
                    channel.LastUpdate = DateTime.UtcNow;

                    foreach (var ch in Channels.Values)
                    {
                        ch.IsActive = activeFrequencies.Contains(ch.Frequency);
                    }
                }

                OnWaveformUpdated();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating real-time waveform");
            }
        }

        internal static float CalculatePacketAmplitude(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return 0f;

            var pcmSamples = Helpers.AudioHelpers.DecodeAudioToPcm(audioData);
            return Helpers.AudioHelpers.CalculateNormalizedAmplitude(pcmSamples);
        }

        public override void Dispose()
        {
            if (Disposed) return;

            Clear();
            Disposed = true;
            Logger.Debug("FilteredWaveformGenerator disposed");
        }
    }
}