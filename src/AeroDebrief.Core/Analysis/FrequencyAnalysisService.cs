using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using AeroDebrief.Core.Models;
using NLog;
using System.Collections.Concurrent;

namespace AeroDebrief.Core.Analysis
{
    /// <summary>
    /// Service for real-time frequency analysis and player statistics tracking
    /// </summary>
    public sealed class FrequencyAnalysisService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly object _lockObject = new();
        private readonly ConcurrentDictionary<(double Frequency, Modulation Modulation), FrequencyChannelAnalysis> _channelAnalysis = new();
        private readonly HashSet<(double Frequency, Modulation Modulation)> _selectedFrequencies = new();
        private readonly Timer _analysisTimer;
        
        private bool _disposed;
        private string _currentFilePath = string.Empty;
        private DateTime _analysisStartTime;
        private volatile bool _isAnalyzing;

        public event EventHandler<FrequencyAnalysisUpdatedEventArgs>? AnalysisUpdated;
        public event EventHandler<PlayerActivityEventArgs>? PlayerActivityDetected;

        /// <summary>
        /// Gets the currently selected frequencies for filtering
        /// </summary>
        public IReadOnlySet<(double Frequency, Modulation Modulation)> SelectedFrequencies
        {
            get
            {
                lock (_lockObject)
                {
                    return new HashSet<(double, Modulation)>(_selectedFrequencies);
                }
            }
        }

        /// <summary>
        /// Gets all analyzed frequency channels
        /// </summary>
        public IReadOnlyDictionary<(double Frequency, Modulation Modulation), FrequencyChannelAnalysis> ChannelAnalysis
        {
            get
            {
                return new Dictionary<(double, Modulation), FrequencyChannelAnalysis>(_channelAnalysis);
            }
        }

        /// <summary>
        /// Indicates if analysis is currently running
        /// </summary>
        public bool IsAnalyzing => _isAnalyzing;

        public FrequencyAnalysisService()
        {
            // Update analysis every 500ms for smooth real-time updates
            _analysisTimer = new Timer(UpdateAnalysis, null, Timeout.Infinite, 500);
            Logger.Debug("FrequencyAnalysisService initialized");
        }

        /// <summary>
        /// Starts analysis of the specified audio file
        /// </summary>
        public async Task<List<FrequencyModulationInfo>> StartAnalysisAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            lock (_lockObject)
            {
                if (_isAnalyzing)
                {
                    Logger.Warn("Analysis already in progress, stopping current analysis");
                    StopAnalysis();
                }

                _currentFilePath = filePath;
                _analysisStartTime = DateTime.UtcNow;
                _channelAnalysis.Clear();
                _isAnalyzing = true;
            }

            Logger.Info($"Starting frequency analysis for: {filePath}");

            try
            {
                // Perform initial analysis to get all frequencies
                var frequencies = await Task.Run(() => FileAnalyzer.GetAllFrequencyModulations(filePath, cancellationToken));
                
                // Initialize channel analysis for each frequency
                foreach (var freq in frequencies)
                {
                    var key = (freq.Frequency, freq.Modulation);
                    var analysis = new FrequencyChannelAnalysis
                    {
                        Frequency = freq.Frequency,
                        Modulation = freq.Modulation,
                        DisplayName = freq.GetDisplayText(),
                        Players = freq.Players.Select(p => new PlayerAnalysis
                        {
                            Name = p.Name,
                            TransmitterGuid = p.TransmitterGuid,
                            Coalition = p.Coalition,
                            Aircraft = p.Aircraft,
                            PacketCount = p.PacketCount,
                            FirstSeen = p.FirstSeen,
                            LastSeen = p.LastSeen,
                            TotalTransmissionTime = p.LastSeen - p.FirstSeen,
                            IsActive = false
                        }).ToList(),
                        TotalPackets = freq.Players.Sum(p => p.PacketCount),
                        IsSelected = false,
                        LastActivity = freq.Players.Count > 0 ? freq.Players.Max(p => p.LastSeen) : DateTime.MinValue
                    };

                    _channelAnalysis[key] = analysis;
                }

                // Start real-time analysis timer
                _analysisTimer.Change(500, 500);

                Logger.Info($"Analysis initialized with {frequencies.Count} frequency channels");
                return frequencies;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start frequency analysis");
                StopAnalysis();
                throw;
            }
        }

        /// <summary>
        /// Updates the selected frequencies for filtering and analysis focus
        /// </summary>
        public void UpdateSelectedFrequencies(IEnumerable<(double Frequency, Modulation Modulation)> selectedFrequencies)
        {
            if (selectedFrequencies == null)
                throw new ArgumentNullException(nameof(selectedFrequencies));

            lock (_lockObject)
            {
                _selectedFrequencies.Clear();
                foreach (var freq in selectedFrequencies)
                {
                    _selectedFrequencies.Add(freq);
                }

                // Update analysis objects
                foreach (var (key, analysis) in _channelAnalysis)
                {
                    analysis.IsSelected = _selectedFrequencies.Contains(key);
                }
            }

            Logger.Debug($"Updated selected frequencies: {_selectedFrequencies.Count} channels selected");
            
            // Notify listeners of the update
            AnalysisUpdated?.Invoke(this, new FrequencyAnalysisUpdatedEventArgs(ChannelAnalysis));
        }

        /// <summary>
        /// Processes an audio packet for real-time analysis
        /// </summary>
        public void ProcessPacket(AudioPacketMetadata packet)
        {
            if (packet == null || !_isAnalyzing)
                return;

            var modulation = Enum.IsDefined(typeof(Modulation), (int)packet.Modulation) 
                ? (Modulation)packet.Modulation 
                : Modulation.DISABLED;

            var key = (packet.Frequency, modulation);

            if (_channelAnalysis.TryGetValue(key, out var analysis))
            {
                // Update activity
                analysis.LastActivity = packet.Timestamp;
                analysis.CurrentPackets++;

                // Find or update player
                var playerGuid = packet.TransmitterGuid;
                var player = analysis.Players.FirstOrDefault(p => p.TransmitterGuid == playerGuid);
                
                if (player != null)
                {
                    player.IsActive = true;
                    player.CurrentPackets++;
                    player.LastActivity = packet.Timestamp;
                    
                    // Update player info if we have better data
                    if (packet.PlayerData != null)
                    {
                        var displayName = packet.PlayerData.GetDisplayName();
                        if (!string.IsNullOrEmpty(displayName) && displayName != playerGuid)
                        {
                            player.Name = displayName;
                        }
                        
                        var coalition = packet.PlayerData.GetCoalitionName();
                        if (!string.IsNullOrEmpty(coalition))
                        {
                            player.Coalition = coalition;
                        }
                        
                        var aircraft = packet.PlayerData.AircraftInfo?.UnitType;
                        if (!string.IsNullOrEmpty(aircraft))
                        {
                            player.Aircraft = aircraft;
                        }
                    }

                    // Notify of player activity
                    PlayerActivityDetected?.Invoke(this, new PlayerActivityEventArgs(analysis, player, packet));
                }
            }
        }

        /// <summary>
        /// Gets filtered analysis data for only the selected frequencies
        /// </summary>
        public Dictionary<(double Frequency, Modulation Modulation), FrequencyChannelAnalysis> GetSelectedChannelAnalysis()
        {
            lock (_lockObject)
            {
                return _channelAnalysis
                    .Where(kvp => kvp.Value.IsSelected)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Gets analysis data grouped by coalition
        /// </summary>
        public Dictionary<string, List<FrequencyChannelAnalysis>> GetAnalysisByCoalition()
        {
            var coalitionGroups = new Dictionary<string, List<FrequencyChannelAnalysis>>();

            foreach (var analysis in _channelAnalysis.Values)
            {
                var coalitions = analysis.Players
                    .Where(p => !string.IsNullOrEmpty(p.Coalition))
                    .Select(p => p.Coalition)
                    .Distinct();

                foreach (var coalition in coalitions)
                {
                    if (!coalitionGroups.ContainsKey(coalition))
                        coalitionGroups[coalition] = new List<FrequencyChannelAnalysis>();
                    
                    if (!coalitionGroups[coalition].Contains(analysis))
                        coalitionGroups[coalition].Add(analysis);
                }

                // Add to "Unknown" if no coalition data
                if (!coalitions.Any())
                {
                    if (!coalitionGroups.ContainsKey("Unknown"))
                        coalitionGroups["Unknown"] = new List<FrequencyChannelAnalysis>();
                    
                    coalitionGroups["Unknown"].Add(analysis);
                }
            }

            return coalitionGroups;
        }

        /// <summary>
        /// Stops the current analysis
        /// </summary>
        public void StopAnalysis()
        {
            lock (_lockObject)
            {
                if (!_isAnalyzing)
                    return;

                _isAnalyzing = false;
                _analysisTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Mark all players as inactive
                foreach (var analysis in _channelAnalysis.Values)
                {
                    foreach (var player in analysis.Players)
                    {
                        player.IsActive = false;
                    }
                }
            }

            Logger.Info("Frequency analysis stopped");
        }

        private void UpdateAnalysis(object? state)
        {
            if (!_isAnalyzing)
                return;

            try
            {
                var now = DateTime.UtcNow;
                var hasChanges = false;

                // Update player activity status (consider inactive after 2 seconds)
                foreach (var analysis in _channelAnalysis.Values)
                {
                    foreach (var player in analysis.Players)
                    {
                        var wasActive = player.IsActive;
                        player.IsActive = (now - player.LastActivity).TotalSeconds < 2;
                        
                        if (wasActive != player.IsActive)
                            hasChanges = true;
                    }
                }

                // Notify if there were changes
                if (hasChanges)
                {
                    AnalysisUpdated?.Invoke(this, new FrequencyAnalysisUpdatedEventArgs(ChannelAnalysis));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during analysis update");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopAnalysis();
            _analysisTimer?.Dispose();
            _disposed = true;

            Logger.Debug("FrequencyAnalysisService disposed");
        }
    }

    /// <summary>
    /// Analysis data for a specific frequency channel
    /// </summary>
    public class FrequencyChannelAnalysis
    {
        public double Frequency { get; set; }
        public Modulation Modulation { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public List<PlayerAnalysis> Players { get; set; } = new();
        public int TotalPackets { get; set; }
        public int CurrentPackets { get; set; }
        public bool IsSelected { get; set; }
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets the most active player on this frequency
        /// </summary>
        public PlayerAnalysis? MostActivePlayer => Players.OrderByDescending(p => p.PacketCount).FirstOrDefault();

        /// <summary>
        /// Gets currently active players
        /// </summary>
        public IEnumerable<PlayerAnalysis> ActivePlayers => Players.Where(p => p.IsActive);

        /// <summary>
        /// Gets activity percentage (packets transmitted vs total recording time)
        /// </summary>
        public double ActivityPercentage
        {
            get
            {
                if (!Players.Any())
                    return 0;

                var totalRecordingTime = Players.Max(p => p.LastSeen) - Players.Min(p => p.FirstSeen);
                if (totalRecordingTime.TotalSeconds <= 0)
                    return 0;

                var totalTransmissionTime = Players.Sum(p => p.TotalTransmissionTime.TotalSeconds);
                return (totalTransmissionTime / totalRecordingTime.TotalSeconds) * 100;
            }
        }
    }

    /// <summary>
    /// Analysis data for a specific player on a frequency
    /// </summary>
    public class PlayerAnalysis
    {
        public string Name { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public string Coalition { get; set; } = string.Empty;
        public string Aircraft { get; set; } = string.Empty;
        public int PacketCount { get; set; }
        public int CurrentPackets { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan TotalTransmissionTime { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets transmission rate (packets per minute)
        /// </summary>
        public double TransmissionRate
        {
            get
            {
                var duration = LastSeen - FirstSeen;
                return duration.TotalMinutes > 0 ? PacketCount / duration.TotalMinutes : 0;
            }
        }
    }

    /// <summary>
    /// Event args for frequency analysis updates
    /// </summary>
    public class FrequencyAnalysisUpdatedEventArgs : EventArgs
    {
        public IReadOnlyDictionary<(double Frequency, Modulation Modulation), FrequencyChannelAnalysis> Analysis { get; }

        public FrequencyAnalysisUpdatedEventArgs(IReadOnlyDictionary<(double, Modulation), FrequencyChannelAnalysis> analysis)
        {
            Analysis = analysis;
        }
    }

    /// <summary>
    /// Event args for player activity detection
    /// </summary>
    public class PlayerActivityEventArgs : EventArgs
    {
        public FrequencyChannelAnalysis Channel { get; }
        public PlayerAnalysis Player { get; }
        public AudioPacketMetadata Packet { get; }

        public PlayerActivityEventArgs(FrequencyChannelAnalysis channel, PlayerAnalysis player, AudioPacketMetadata packet)
        {
            Channel = channel;
            Player = player;
            Packet = packet;
        }
    }
}