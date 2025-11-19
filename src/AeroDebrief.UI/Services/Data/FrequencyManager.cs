using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Playback;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Charts;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;

namespace AeroDebrief.UI.Services.Data
{
    /// <summary>
    /// Service responsible for managing frequency data and selection state.
    /// Implements Separation of Concerns by handling ONLY frequency-related operations.
    /// Phase 4 Update: Uses ChartColors for deterministic color assignment.
    /// </summary>
    public sealed class FrequencyManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ObservableCollection<FrequencyGroupViewModel> _frequencies = new();
        private readonly HashSet<double> _selectedFrequencies = new();
        private bool _disposed;

        /// <summary>
        /// Gets the read-only collection of frequency groups.
        /// </summary>
        public ReadOnlyObservableCollection<FrequencyGroupViewModel> Frequencies { get; }

        /// <summary>
        /// Gets the set of currently selected frequencies.
        /// </summary>
        public IReadOnlySet<double> SelectedFrequencies => _selectedFrequencies;

        /// <summary>
        /// Raised when a frequency selection changes.
        /// </summary>
        public event EventHandler<FrequencySelectionChangedEventArgs>? SelectionChanged;

        /// <summary>
        /// Raised when frequencies are loaded.
        /// </summary>
        public event EventHandler<FrequenciesLoadedEventArgs>? FrequenciesLoaded;

        public FrequencyManager()
        {
            Frequencies = new ReadOnlyObservableCollection<FrequencyGroupViewModel>(_frequencies);
            Logger.Debug("FrequencyManager initialized");
        }

        /// <summary>
        /// Loads frequencies from the packet source and groups them by coalition.
        /// This implements the race-condition fix by building complete groups before adding to observable collection.
        /// </summary>
        public async Task LoadFrequenciesAsync(FilePacketSource source, FilePlaybackPipeline pipeline)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            try
            {
                Logger.Info("FrequencyManager: Loading frequencies...");

                _frequencies.Clear();
                _selectedFrequencies.Clear();

                var frequencyInfos = pipeline.GetAvailableFrequencies();
                Logger.Info($"Found {frequencyInfos.Count} frequencies in file");

                if (frequencyInfos.Count == 0)
                {
                    Logger.Warn("No frequencies found in file");
                    return;
                }

                var coalitionGroups = frequencyInfos
                    .GroupBy(fi => GetCoalitionNameFromPlayers(fi.Players))
                    .OrderBy(g => GetCoalitionOrder(g.Key))
                    .ToList();

                Logger.Info($"Created {coalitionGroups.Count} coalition groups");

                // CRITICAL: Build ALL groups COMPLETELY before adding to observable collection
                // This prevents race conditions with UI binding
                var completeGroups = new List<(FrequencyGroupViewModel Group, List<FrequencyViewModel> Freqs)>();

                foreach (var coalitionGroup in coalitionGroups)
                {
                    var coalitionName = coalitionGroup.Key;
                    var group = new FrequencyGroupViewModel
                    {
                        Name = $"{coalitionName} ({coalitionGroup.Count()} frequencies)",
                        IsExpanded = true
                    };

                    Logger.Debug($"Building group: {group.Name}");

                    var groupFreqs = new List<FrequencyViewModel>();

                    foreach (var fi in coalitionGroup.OrderBy(f => f.Frequency))
                    {
                        var freqViewModel = CreateFrequencyViewModel(fi);
                        groupFreqs.Add(freqViewModel);
                    }

                    Logger.Debug($"   Group '{group.Name}' built with {groupFreqs.Count} frequencies");
                    completeGroups.Add((group, groupFreqs));
                }

                // Add to observable collection on UI thread (groups are already complete)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var (group, freqs) in completeGroups)
                    {
                        // Add all frequencies to group FIRST
                        foreach (var freq in freqs)
                        {
                            group.Frequencies.Add(freq);
                        }

                        // Then add group to observable collection (triggers binding with complete group)
                        _frequencies.Add(group);
                        Logger.Debug($"   Added complete group: {group.Name} with {group.Frequencies.Count} frequencies");
                    }
                    
                    // Now that UI has processed all groups, auto-select all frequencies
                    Logger.Info("Auto-selecting all frequencies after UI load...");
                    SelectAll();
                });

                var totalFrequencies = completeGroups.Sum(g => g.Freqs.Count);
                Logger.Info($"? Loaded {coalitionGroups.Count} groups with {totalFrequencies} total frequencies");

                FrequenciesLoaded?.Invoke(this, new FrequenciesLoadedEventArgs(totalFrequencies));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load frequencies");
                throw;
            }
        }

        /// <summary>
        /// Selects a frequency and raises the SelectionChanged event.
        /// </summary>
        public void SelectFrequency(double frequency)
        {
            if (_selectedFrequencies.Add(frequency))
            {
                Logger.Debug($"Frequency selected: {frequency:F1} Hz");
                SelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, true));
            }
        }

        /// <summary>
        /// Deselects a frequency and raises the SelectionChanged event.
        /// </summary>
        public void DeselectFrequency(double frequency)
        {
            if (_selectedFrequencies.Remove(frequency))
            {
                Logger.Debug($"Frequency deselected: {frequency:F1} Hz");
                SelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, false));
            }
        }

        /// <summary>
        /// Selects all frequencies.
        /// </summary>
        public void SelectAll()
        {
            Logger.Info("Selecting all frequencies...");
            var count = 0;

            foreach (var group in _frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    if (_selectedFrequencies.Add(freq.Frequency))
                    {
                        freq.IsSelected = true;
                        SelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(freq.Frequency, true));
                        count++;
                    }
                }
            }

            Logger.Info($"? Selected {count} frequencies");
        }

        /// <summary>
        /// Deselects all frequencies.
        /// </summary>
        public void DeselectAll()
        {
            Logger.Info("Deselecting all frequencies...");
            var toRemove = _selectedFrequencies.ToList();
            
            foreach (var frequency in toRemove)
            {
                _selectedFrequencies.Remove(frequency);
                
                // Update ViewModel
                var freqViewModel = _frequencies
                    .SelectMany(g => g.Frequencies)
                    .FirstOrDefault(f => Math.Abs(f.Frequency - frequency) < 0.1);
                
                if (freqViewModel != null)
                {
                    freqViewModel.IsSelected = false;
                }
                
                SelectionChanged?.Invoke(this, new FrequencySelectionChangedEventArgs(frequency, false));
            }

            Logger.Info($"? Deselected {toRemove.Count} frequencies");
        }

        /// <summary>
        /// Gets a frequency view model by frequency value.
        /// </summary>
        public FrequencyViewModel? GetFrequency(double frequency)
        {
            return _frequencies
                .SelectMany(g => g.Frequencies)
                .FirstOrDefault(f => Math.Abs(f.Frequency - frequency) < 0.1);
        }

        /// <summary>
        /// Clears all frequencies and selections.
        /// </summary>
        public void Clear()
        {
            Logger.Debug("Clearing all frequencies");
            _frequencies.Clear();
            _selectedFrequencies.Clear();
        }

        #region Private Helper Methods

        private FrequencyViewModel CreateFrequencyViewModel(FrequencyInfo fi)
        {
            // Parse modulation from string to enum
            var modulation = Enum.TryParse<Modulation>(fi.Modulation, out var mod) 
                ? mod 
                : Modulation.AM;

            // Create FrequencyModulationInfo with player data
            var sourceData = new FrequencyModulationInfo(fi.Frequency, modulation)
            {
                Players = fi.Players
            };

            // Phase 4: Use ChartColors for deterministic color assignment
            var frequencyId = GetFrequencyId(fi.Frequency, fi.Modulation);
            var skColor = ChartColors.GetColorForFrequency(frequencyId);
            
            // Convert SKColor to WPF Color
            var wpfColor = System.Windows.Media.Color.FromArgb(
                skColor.Alpha,
                skColor.Red,
                skColor.Green,
                skColor.Blue
            );

            Logger.Debug($"Assigned color to {frequencyId}: RGB({skColor.Red},{skColor.Green},{skColor.Blue})");

            return new FrequencyViewModel
            {
                Frequency = fi.Frequency,
                Modulation = fi.Modulation,
                DisplayName = fi.DisplayName,
                PacketCount = fi.PacketCount,
                IsSelected = false, // Default to NOT selected - user must explicitly select frequencies
                WaveformColor = wpfColor,
                SourceData = sourceData
            };
        }

        /// <summary>
        /// Generates a consistent frequency ID for color assignment.
        /// Format: "FrequencyMHz-Modulation" (e.g., "251.0-AM")
        /// </summary>
        private string GetFrequencyId(double frequencyHz, string modulation)
        {
            var frequencyMHz = frequencyHz / 1_000_000.0;
            return $"{frequencyMHz:F1}-{modulation}";
        }

        private string GetCoalitionNameFromPlayers(List<AeroDebrief.Core.Models.PlayerFrequencyInfo> players)
        {
            if (!players.Any()) 
                return "Unknown";

            var coalitionCounts = players
                .GroupBy(p => p.Coalition ?? "Unknown")
                .Select(g => new { Coalition = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            return coalitionCounts.FirstOrDefault()?.Coalition ?? "Unknown";
        }

        private int GetCoalitionOrder(string coalition)
        {
            return coalition.ToLower() switch
            {
                "red" => 0,
                "blue" => 1,
                "neutral" => 2,
                "spectator" => 3,
                _ => 4
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) 
                return;

            Logger.Debug("FrequencyManager disposing");
            _frequencies.Clear();
            _selectedFrequencies.Clear();
            _disposed = true;
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for frequency selection changes.
    /// </summary>
    public class FrequencySelectionChangedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public bool IsSelected { get; }

        public FrequencySelectionChangedEventArgs(double frequency, bool isSelected)
        {
            Frequency = frequency;
            IsSelected = isSelected;
        }
    }

    /// <summary>
    /// Event arguments for when frequencies are loaded.
    /// </summary>
    public class FrequenciesLoadedEventArgs : EventArgs
    {
        public int TotalFrequencies { get; }

        public FrequenciesLoadedEventArgs(int totalFrequencies)
        {
            TotalFrequencies = totalFrequencies;
        }
    }

    #endregion
}
