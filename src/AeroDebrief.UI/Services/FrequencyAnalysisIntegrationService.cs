using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Models;
using AeroDebrief.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using NLog;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Service that orchestrates the integration between UI ViewModels and Core analysis services
    /// </summary>
    public sealed class FrequencyAnalysisIntegrationService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly MainViewModel _mainViewModel;
        private readonly AudioSession _audioSession;
        private bool _disposed;

        public FrequencyAnalysisIntegrationService(MainViewModel mainViewModel, AudioSession audioSession)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _audioSession = audioSession ?? throw new ArgumentNullException(nameof(audioSession));

            // Wire up events
            _audioSession.FrequencyAnalysisUpdated += OnFrequencyAnalysisUpdated;
            _audioSession.SpectrumUpdated += OnSpectrumUpdated;
            _audioSession.WaveformUpdated += OnWaveformUpdated;

            Logger.Debug("FrequencyAnalysisIntegrationService initialized");
        }

        /// <summary>
        /// Loads a file and initializes the frequency analysis
        /// </summary>
        public async Task<bool> LoadFileAsync(string filePath)
        {
            try
            {
                Logger.Info($"Loading file for frequency analysis: {filePath}");
                _mainViewModel.StatusText = "Loading file and analyzing frequencies...";

                // Load the file through AudioSession
                var success = await _audioSession.LoadFileAsync(filePath);
                
                if (success)
                {
                    // Get initial frequency data and populate the UI
                    await PopulateFrequencyTreeAsync();
                    
                    _mainViewModel.StatusText = $"File loaded successfully. Found {_mainViewModel.AvailableFrequencies.Count} frequency groups.";
                    _mainViewModel.IsFileLoaded = true;
                }
                else
                {
                    _mainViewModel.StatusText = "Failed to load file.";
                    _mainViewModel.IsFileLoaded = false;
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading file for frequency analysis");
                _mainViewModel.StatusText = $"Error loading file: {ex.Message}";
                _mainViewModel.IsFileLoaded = false;
                return false;
            }
        }

        /// <summary>
        /// Handles frequency selection changes from the UI
        /// </summary>
        public void OnFrequencySelectionChanged(FrequencyViewModel frequency, bool isSelected)
        {
            try
            {
                Logger.Debug($"Frequency selection changed: {frequency.DisplayName} = {isSelected}");
                
                // Update the core service
                _audioSession.SetChannelActive(frequency.Frequency, isSelected);

                // Update the main view model
                _mainViewModel.UpdateSelectedFrequencies();

                // Update waveform data immediately
                _mainViewModel.WaveformData = _audioSession.GetWaveformData();
                _mainViewModel.StatusText = $"Updated selection: {_mainViewModel.SelectedFrequencyCount} frequencies selected.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling frequency selection change");
            }
        }

        /// <summary>
        /// Handles gain changes from the frequency mixer
        /// </summary>
        public void OnFrequencyGainChanged(double frequency, float gain)
        {
            try
            {
                Logger.Debug($"Frequency gain changed: {frequency} Hz = {gain:F2}");
                _audioSession.SetChannelGain(frequency, gain);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling frequency gain change");
            }
        }

        /// <summary>
        /// Handles pan changes from the frequency mixer
        /// </summary>
        public void OnFrequencyPanChanged(double frequency, float pan)
        {
            try
            {
                Logger.Debug($"Frequency pan changed: {frequency} Hz = {pan:F2}");
                _audioSession.SetChannelPan(frequency, pan);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling frequency pan change");
            }
        }

        private async Task PopulateFrequencyTreeAsync()
        {
            try
            {
                var frequencies = _audioSession.GetAvailableFrequencies();
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mainViewModel.AvailableFrequencies.Clear();

                    // Group frequencies by coalition
                    var coalitionGroups = frequencies
                        .GroupBy(f => GetPrimaryCoalition(f.Players))
                        .OrderBy(g => g.Key);

                    foreach (var coalitionGroup in coalitionGroups)
                    {
                        var groupViewModel = new FrequencyGroupViewModel
                        {
                            Name = $"{coalitionGroup.Key} Coalition ({coalitionGroup.Count()} frequencies)",
                            IsExpanded = true
                        };

                        foreach (var freq in coalitionGroup.OrderBy(f => f.Frequency))
                        {
                            var freqViewModel = new FrequencyViewModel
                            {
                                Frequency = freq.Frequency,
                                Modulation = freq.Modulation,
                                DisplayName = freq.DisplayName,
                                PacketCount = freq.PacketCount,
                                IsSelected = false,
                                IsActive = freq.IsActive,
                                LastActivity = freq.LastActivity,
                                SourceData = CreateFrequencyModulationInfo(freq)
                            };

                            groupViewModel.Frequencies.Add(freqViewModel);
                        }

                        _mainViewModel.AvailableFrequencies.Add(groupViewModel);
                    }

                    Logger.Info($"Populated frequency tree with {frequencies.Count} frequencies in {_mainViewModel.AvailableFrequencies.Count} coalition groups");
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error populating frequency tree");
            }
        }

        private FrequencyModulationInfo CreateFrequencyModulationInfo(FrequencyInfo freq)
        {
            var modulation = Enum.TryParse<Modulation>(freq.Modulation, out var mod) ? mod : Modulation.DISABLED;
            
            return new FrequencyModulationInfo(freq.Frequency, modulation)
            {
                Players = freq.Players
            };
        }

        private string GetPrimaryCoalition(List<PlayerFrequencyInfo> players)
        {
            if (!players.Any())
                return "Unknown";

            // Find the coalition with the most activity
            var coalitionActivity = players
                .Where(p => !string.IsNullOrEmpty(p.Coalition))
                .GroupBy(p => p.Coalition)
                .Select(g => new { Coalition = g.Key, TotalPackets = g.Sum(p => p.PacketCount) })
                .OrderByDescending(x => x.TotalPackets)
                .FirstOrDefault();

            return coalitionActivity?.Coalition ?? "Unknown";
        }

        private void OnFrequencyAnalysisUpdated(FrequencyAnalysisUpdatedEventArgs e)
        {
            try
            {
                // Update frequency activity indicators in UI
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    foreach (var group in _mainViewModel.AvailableFrequencies)
                    {
                        foreach (var freq in group.Frequencies)
                        {
                            var key = (freq.Frequency, Enum.Parse<Modulation>(freq.Modulation));
                            if (e.Analysis.TryGetValue(key, out var analysis))
                            {
                                freq.IsActive = analysis.ActivePlayers.Any();
                                freq.LastActivity = analysis.LastActivity;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating frequency analysis in UI");
            }
        }

        private void OnSpectrumUpdated(SpectrumAnalysisEventArgs e)
        {
            try
            {
                // Update spectrum display
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var spectrumData = _audioSession.GetSpectrumSnapshot();
                    _mainViewModel.SpectrumData = spectrumData;
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating spectrum in UI");
            }
        }

        private void OnWaveformUpdated(WaveformUpdatedEventArgs e)
        {
            try
            {
                // Update waveform display
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    _mainViewModel.WaveformData = _audioSession.GetWaveformData();
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating waveform in UI");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Unwire events
            _audioSession.FrequencyAnalysisUpdated -= OnFrequencyAnalysisUpdated;
            _audioSession.SpectrumUpdated -= OnSpectrumUpdated;
            _audioSession.WaveformUpdated -= OnWaveformUpdated;

            _disposed = true;
            Logger.Debug("FrequencyAnalysisIntegrationService disposed");
        }
    }
}