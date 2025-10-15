using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Audio;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Analysis
{
    /// <summary>File analysis utilities</summary>
    public static class FileAnalyzer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Analyzes an audio file to detect periods of audio activity (non-silence)
        /// </summary>
        /// <param name="filePath">Path to the audio file</param>
        /// <param name="silenceThreshold">Amplitude threshold below which audio is considered silence (0-32767)</param>
        /// <param name="minimumActivityDuration">Minimum duration for an activity period to be reported</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio activity analysis result</returns>
        public static AudioActivityAnalysis AnalyzeAudioActivity(
            string filePath, 
            int silenceThreshold = 500, 
            TimeSpan minimumActivityDuration = default,
            CancellationToken cancellationToken = default)
        {
            if (minimumActivityDuration == default)
                minimumActivityDuration = TimeSpan.FromMilliseconds(100); // Default 100ms minimum activity
                
            var activityPeriods = new List<AudioActivityPeriod>();
            var playerActivity = new Dictionary<string, List<AudioActivityPeriod>>();
            var frequencyActivity = new Dictionary<double, List<AudioActivityPeriod>>();
            
            DateTime? recordingStart = null;
            DateTime? recordingEnd = null;
            var totalPackets = 0;
            var packetsWithAudio = 0;
            var totalAudioBytes = 0L;
            var activeAudioBytes = 0L;
            
            AudioActivityPeriod? currentActivity = null;
            
            try
            {
                Logger.Info($"Starting audio activity analysis for: {filePath}");
                Logger.Info($"Silence threshold: {silenceThreshold}/32767, Minimum activity duration: {minimumActivityDuration.TotalMilliseconds}ms");
                
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                    {
                        totalPackets++;
                        recordingStart ??= metadata.Timestamp;
                        recordingEnd = metadata.Timestamp;
                        
                        if (metadata.AudioPayload == null || metadata.AudioPayload.Length == 0)
                        {
                            // No audio data - end any current activity
                            currentActivity = EndCurrentActivity(currentActivity, activityPeriods, minimumActivityDuration);
                            continue;
                        }
                        
                        totalAudioBytes += metadata.AudioPayload.Length;
                        
                        // Analyze audio data for activity
                        var audioAnalysis = AnalyzeAudioData(metadata.AudioPayload, silenceThreshold);
                        
                        if (audioAnalysis.HasAudio)
                        {
                            packetsWithAudio++;
                            activeAudioBytes += metadata.AudioPayload.Length;
                            
                            // Start or continue activity period
                            if (currentActivity == null)
                            {
                                currentActivity = new AudioActivityPeriod
                                {
                                    StartTime = metadata.Timestamp,
                                    EndTime = metadata.Timestamp,
                                    PrimaryPlayer = metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid,
                                    PrimaryFrequency = metadata.Frequency,
                                    MaxAmplitude = audioAnalysis.MaxAmplitude,
                                    AverageAmplitude = audioAnalysis.AverageAmplitude,
                                    PacketCount = 1,
                                    Players = new HashSet<string> { metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid },
                                    Frequencies = new HashSet<double> { metadata.Frequency }
                                };
                            }
                            else
                            {
                                // Extend current activity
                                currentActivity.EndTime = metadata.Timestamp;
                                currentActivity.PacketCount++;
                                currentActivity.MaxAmplitude = Math.Max(currentActivity.MaxAmplitude, audioAnalysis.MaxAmplitude);
                                currentActivity.AverageAmplitude = (currentActivity.AverageAmplitude + audioAnalysis.AverageAmplitude) / 2;
                                currentActivity.Players.Add(metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid);
                                currentActivity.Frequencies.Add(metadata.Frequency);
                            }
                            
                            // Track per-player activity
                            var playerName = metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid;
                            if (!playerActivity.ContainsKey(playerName))
                                playerActivity[playerName] = new List<AudioActivityPeriod>();
                            
                            // Track per-frequency activity
                            if (!frequencyActivity.ContainsKey(metadata.Frequency))
                                frequencyActivity[metadata.Frequency] = new List<AudioActivityPeriod>();
                        }
                        else
                        {
                            // No significant audio - end current activity if it exists
                            currentActivity = EndCurrentActivity(currentActivity, activityPeriods, minimumActivityDuration);
                        }
                        
                        // Progress logging
                        if (totalPackets % 10000 == 0)
                        {
                            Logger.Debug($"Analyzed {totalPackets} packets, found {activityPeriods.Count} activity periods");
                        }
                    }
                    else
                    {
                        break; // End of readable data
                    }
                }
                
                // End any remaining activity
                EndCurrentActivity(currentActivity, activityPeriods, minimumActivityDuration);
                
                // Group activities by player and frequency
                foreach (var activity in activityPeriods)
                {
                    // Add to player activities
                    foreach (var player in activity.Players)
                    {
                        if (playerActivity.ContainsKey(player))
                            playerActivity[player].Add(activity);
                    }
                    
                    // Add to frequency activities
                    foreach (var frequency in activity.Frequencies)
                    {
                        if (frequencyActivity.ContainsKey(frequency))
                            frequencyActivity[frequency].Add(activity);
                    }
                }
                
                // Calculate statistics
                var totalDuration = recordingEnd.HasValue && recordingStart.HasValue 
                    ? recordingEnd.Value - recordingStart.Value 
                    : TimeSpan.Zero;
                    
                var totalActivityDuration = activityPeriods.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds);
                var activityPercentage = totalDuration.TotalMilliseconds > 0 
                    ? (totalActivityDuration / totalDuration.TotalMilliseconds) * 100 
                    : 0;
                
                var result = new AudioActivityAnalysis
                {
                    FilePath = filePath,
                    RecordingStart = recordingStart ?? DateTime.MinValue,
                    RecordingEnd = recordingEnd ?? DateTime.MinValue,
                    TotalDuration = totalDuration,
                    TotalPackets = totalPackets,
                    PacketsWithAudio = packetsWithAudio,
                    TotalAudioBytes = totalAudioBytes,
                    ActiveAudioBytes = activeAudioBytes,
                    ActivityPeriods = activityPeriods.OrderBy(a => a.StartTime).ToList(),
                    PlayerActivities = playerActivity.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value.OrderBy(a => a.StartTime).ToList()
                    ),
                    FrequencyActivities = frequencyActivity.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value.OrderBy(a => a.StartTime).ToList()
                    ),
                    ActivityPercentage = activityPercentage,
                    SilenceThreshold = silenceThreshold,
                    MinimumActivityDuration = minimumActivityDuration
                };
                
                Logger.Info($"Audio activity analysis completed:");
                Logger.Info($"  Total packets: {totalPackets:N0}");
                Logger.Info($"  Packets with audio: {packetsWithAudio:N0} ({(double)packetsWithAudio/totalPackets*100:F1}%)");
                Logger.Info($"  Activity periods: {activityPeriods.Count:N0}");
                Logger.Info($"  Total activity duration: {TimeSpan.FromMilliseconds(totalActivityDuration)}");
                Logger.Info($"  Activity percentage: {activityPercentage:F1}%");
                Logger.Info($"  Unique players: {playerActivity.Count}");
                Logger.Info($"  Unique frequencies: {frequencyActivity.Count}");
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to analyze audio activity for file: {filePath}");
                throw;
            }
        }
        
        private static AudioActivityPeriod? EndCurrentActivity(
            AudioActivityPeriod? currentActivity, 
            List<AudioActivityPeriod> activityPeriods, 
            TimeSpan minimumDuration)
        {
            if (currentActivity != null)
            {
                var duration = currentActivity.EndTime - currentActivity.StartTime;
                if (duration >= minimumDuration)
                {
                    activityPeriods.Add(currentActivity);
                }
            }
            return null;
        }
        
        private static AudioDataAnalysis AnalyzeAudioData(byte[] audioData, int silenceThreshold)
        {
            if (audioData == null || audioData.Length == 0)
                return new AudioDataAnalysis { HasAudio = false };
                
            var samples = audioData.Length / 2; // 16-bit samples
            var maxAmplitude = 0;
            var totalAmplitude = 0L;
            var activeSamples = 0;
            
            for (int i = 0; i < audioData.Length - 1; i += 2)
            {
                var rawSample = BitConverter.ToInt16(audioData, i);
                var sample = rawSample == Int16.MinValue ? Int16.MaxValue : Math.Abs(rawSample);
                maxAmplitude = Math.Max(maxAmplitude, sample);
                totalAmplitude += sample;
                
                if (sample > silenceThreshold)
                    activeSamples++;
            }
            
            var averageAmplitude = samples > 0 ? (int)(totalAmplitude / samples) : 0;
            var hasAudio = maxAmplitude > silenceThreshold && activeSamples > (samples * 0.05); // At least 5% of samples above threshold
            
            return new AudioDataAnalysis
            {
                HasAudio = hasAudio,
                MaxAmplitude = maxAmplitude,
                AverageAmplitude = averageAmplitude,
                ActiveSamplePercentage = samples > 0 ? (double)activeSamples / samples * 100 : 0
            };
        }

        public static List<FrequencyModulationInfo> GetAllFrequencyModulations(string filePath, CancellationToken cancellationToken = default)
        {
            var combinations = new Dictionary<(double Frequency, Modulation Modulation), FrequencyModulationInfo>();
            var playerStats = new Dictionary<(double, Modulation, string), PlayerStatsCollector>();
            var processedPackets = 0;
            
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                    {
                        var modulation = Enum.IsDefined(typeof(Modulation), (int)metadata.Modulation) 
                            ? (Modulation)metadata.Modulation 
                            : Modulation.DISABLED;
                        
                        var key = (metadata.Frequency, modulation);
                        
                        // Create or get frequency-modulation combination
                        if (!combinations.ContainsKey(key))
                        {
                            combinations[key] = new FrequencyModulationInfo(metadata.Frequency, modulation);
                        }

                        // Track player statistics for this frequency-modulation-transmitter combination
                        var playerKey = (metadata.Frequency, modulation, metadata.TransmitterGuid);
                        if (!playerStats.ContainsKey(playerKey))
                        {
                            playerStats[playerKey] = new PlayerStatsCollector
                            {
                                TransmitterGuid = metadata.TransmitterGuid,
                                Name = metadata.PlayerData?.GetDisplayName() ?? metadata.TransmitterGuid,
                                Coalition = metadata.PlayerData?.GetCoalitionName() ?? "Unknown",
                                Aircraft = metadata.PlayerData?.AircraftInfo?.UnitType ?? "Unknown",
                                FirstSeen = metadata.Timestamp,
                                LastSeen = metadata.Timestamp,
                                PacketCount = 1
                            };
                        }
                        else
                        {
                            var stats = playerStats[playerKey];
                            stats.PacketCount++;
                            stats.LastSeen = metadata.Timestamp;
                            
                            // Update info if we got better data
                            if (metadata.PlayerData != null)
                            {
                                var displayName = metadata.PlayerData.GetDisplayName();
                                if (!string.IsNullOrEmpty(displayName) && displayName != metadata.TransmitterGuid)
                                {
                                    stats.Name = displayName;
                                }
                                
                                var coalition = metadata.PlayerData.GetCoalitionName();
                                if (!string.IsNullOrEmpty(coalition) && coalition != "Unknown")
                                {
                                    stats.Coalition = coalition;
                                }
                                
                                var aircraft = metadata.PlayerData.AircraftInfo?.UnitType;
                                if (!string.IsNullOrEmpty(aircraft) && aircraft != "Unknown")
                                {
                                    stats.Aircraft = aircraft;
                                }
                            }
                        }
                        
                        processedPackets++;
                    }
                    else break;
                }

                // Convert player statistics to FrequencyModulationInfo players
                foreach (var ((freq, mod, guid), stats) in playerStats)
                {
                    var key = (freq, mod);
                    if (combinations.ContainsKey(key))
                    {
                        var playerInfo = new PlayerFrequencyInfo
                        {
                            Name = stats.Name,
                            TransmitterGuid = stats.TransmitterGuid,
                            Coalition = stats.Coalition,
                            Aircraft = stats.Aircraft,
                            PacketCount = stats.PacketCount,
                            FirstSeen = stats.FirstSeen,
                            LastSeen = stats.LastSeen
                        };
                        
                        // Create a new record with updated players list
                        var existingInfo = combinations[key];
                        var updatedPlayers = new List<PlayerFrequencyInfo>(existingInfo.Players) { playerInfo };
                        combinations[key] = existingInfo with { Players = updatedPlayers };
                    }
                }

                Logger.Info($"Analyzed {processedPackets} packets, found {combinations.Count} unique frequency-modulation combinations with {playerStats.Count} unique transmitters");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to analyze file: {filePath}");
                throw;
            }

            return combinations.Values
                .OrderBy(c => c.Frequency)
                .ThenBy(c => c.Modulation)
                .ToList();
        }

        public static TimeSpan CalculateTotalDuration(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                DateTime? first = null, last = null;
                TimeSpan lastDuration = TimeSpan.Zero;
                var processedPackets = 0;

                while (fs.Position < fs.Length)
                {
                    if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                    {
                        first ??= metadata.Timestamp;
                        last = metadata.Timestamp;
                        
                        if (metadata.AudioPayload?.Length > 0)
                        {
                            int samples = metadata.AudioPayload.Length / 2;
                            lastDuration = TimeSpan.FromSeconds((double)samples / metadata.SampleRate);
                        }
                        processedPackets++;
                    }
                    else break;
                }

                var totalDuration = first.HasValue && last.HasValue 
                    ? (last.Value - first.Value) + lastDuration 
                    : TimeSpan.Zero;

                Logger.Info($"Calculated total duration: {totalDuration} from {processedPackets} packets");
                return totalDuration;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to calculate duration for file: {filePath}");
                throw;
            }
        }

        public static IEnumerable<AudioPacketMetadata> ReadAllPackets(string filePath, CancellationToken cancellationToken = default)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            var processedPackets = 0;
            while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
            {
                if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                {
                    processedPackets++;
                    yield return metadata;
                }
                else 
                {
                    Logger.Debug($"Finished reading packets after processing {processedPackets} packets");
                    yield break;
                }
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Info($"Packet reading cancelled after processing {processedPackets} packets");
            }
        }

        /// <summary>
        /// Helper class to collect player statistics during analysis
        /// </summary>
        private class PlayerStatsCollector
        {
            public string TransmitterGuid { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Coalition { get; set; } = string.Empty;
            public string Aircraft { get; set; } = string.Empty;
            public int PacketCount { get; set; }
            public DateTime FirstSeen { get; set; }
            public DateTime LastSeen { get; set; }
        }
    }
    
    /// <summary>
    /// Results of audio activity analysis
    /// </summary>
    public class AudioActivityAnalysis
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime RecordingStart { get; set; }
        public DateTime RecordingEnd { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int TotalPackets { get; set; }
        public int PacketsWithAudio { get; set; }
        public long TotalAudioBytes { get; set; }
        public long ActiveAudioBytes { get; set; }
        public double ActivityPercentage { get; set; }
        public int SilenceThreshold { get; set; }
        public TimeSpan MinimumActivityDuration { get; set; }
        
        public List<AudioActivityPeriod> ActivityPeriods { get; set; } = new();
        public Dictionary<string, List<AudioActivityPeriod>> PlayerActivities { get; set; } = new();
        public Dictionary<double, List<AudioActivityPeriod>> FrequencyActivities { get; set; } = new();
        
        /// <summary>
        /// Gets total duration of all activity periods
        /// </summary>
        public TimeSpan TotalActivityDuration => 
            TimeSpan.FromMilliseconds(ActivityPeriods.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds));
            
        /// <summary>
        /// Gets the most active player by total activity duration
        /// </summary>
        public string? MostActivePlayer => 
            PlayerActivities
                .OrderByDescending(kvp => kvp.Value.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds))
                .FirstOrDefault().Key;
                
        /// <summary>
        /// Gets the most active frequency by total activity duration
        /// </summary>
        public double? MostActiveFrequency =>
            FrequencyActivities.Count > 0 ? 
                FrequencyActivities
                    .OrderByDescending(kvp => kvp.Value.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds))
                    .FirstOrDefault().Key 
                : null;
                
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Audio Activity Analysis ===");
            sb.AppendLine($"File: {FilePath}");
            sb.AppendLine($"Recording Period: {RecordingStart:yyyy-MM-dd HH:mm:ss} - {RecordingEnd:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Duration: {TotalDuration}");
            sb.AppendLine($"Activity Duration: {TotalActivityDuration} ({ActivityPercentage:F1}%)");
            sb.AppendLine($"");
            sb.AppendLine($"Statistics:");
            sb.AppendLine($"  Total Packets: {TotalPackets:N0}");
            sb.AppendLine($"  Packets with Audio: {PacketsWithAudio:N0} ({(double)PacketsWithAudio/TotalPackets*100:F1}%)");
            sb.AppendLine($"  Activity Periods: {ActivityPeriods.Count:N0}");
            sb.AppendLine($"  Unique Players: {PlayerActivities.Count}");
            sb.AppendLine($"  Unique Frequencies: {FrequencyActivities.Count}");
            sb.AppendLine($"  Silence Threshold: {SilenceThreshold}/32767");
            sb.AppendLine($"");
            
            if (ActivityPeriods.Count > 0)
            {
                sb.AppendLine($"Activity Periods (showing first 20):");
                var periodsToShow = ActivityPeriods.Take(20);
                foreach (var period in periodsToShow)
                {
                    var duration = period.EndTime - period.StartTime;
                    sb.AppendLine($"  {period.StartTime:HH:mm:ss.fff} - {period.EndTime:HH:mm:ss.fff} " +
                                 $"({duration.TotalSeconds:F1}s) {period.PrimaryPlayer} @ {period.PrimaryFrequency:F1}MHz " +
                                 $"[Max: {period.MaxAmplitude}, Avg: {period.AverageAmplitude}]");
                }
                
                if (ActivityPeriods.Count > 20)
                {
                    sb.AppendLine($"  ... and {ActivityPeriods.Count - 20} more periods");
                }
                sb.AppendLine($"");
            }
            
            if (PlayerActivities.Count > 0)
            {
                sb.AppendLine($"Player Activity Summary:");
                foreach (var (player, activities) in PlayerActivities.OrderByDescending(kvp => kvp.Value.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds))
)
                {
                    var totalDuration = TimeSpan.FromMilliseconds(activities.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds));
                    sb.AppendLine($"  {player}: {activities.Count} periods, {totalDuration.TotalSeconds:F1}s total");
                }
                sb.AppendLine($"");
            }
            
            if (FrequencyActivities.Count > 0)
            {
                sb.AppendLine($"Frequency Activity Summary:");
                foreach (var (freq, activities) in FrequencyActivities.OrderByDescending(kvp => kvp.Value.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds)))
                {
                    var totalDuration = TimeSpan.FromMilliseconds(activities.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds));
                    sb.AppendLine($"  {freq:F1}MHz: {activities.Count} periods, {totalDuration.TotalSeconds:F1}s total");
                }
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Represents a period of audio activity
    /// </summary>
    public class AudioActivityPeriod
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string PrimaryPlayer { get; set; } = string.Empty;
        public double PrimaryFrequency { get; set; }
        public int MaxAmplitude { get; set; }
        public int AverageAmplitude { get; set; }
        public int PacketCount { get; set; }
        public HashSet<string> Players { get; set; } = new();
        public HashSet<double> Frequencies { get; set; } = new();
        
        public TimeSpan Duration => EndTime - StartTime;
        
        public override string ToString()
        {
            return $"{StartTime:HH:mm:ss.fff}-{EndTime:HH:mm:ss.fff} ({Duration.TotalSeconds:F1}s) " +
                   $"{PrimaryPlayer} @ {PrimaryFrequency:F1}MHz";
        }
    }
    
    /// <summary>
    /// Analysis results for a single audio data chunk
    /// </summary>
    internal class AudioDataAnalysis
    {
        public bool HasAudio { get; set; }
        public int MaxAmplitude { get; set; }
        public int AverageAmplitude { get; set; }
        public double ActiveSamplePercentage { get; set; }
    }
}