using System.Collections.ObjectModel;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using AeroDebrief.UI.Controls;

namespace AeroDebrief.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string _statusText = "Ready to load recording file";
        private string _currentFileName = string.Empty;
        private string _windowTitle = "SRS Signal Analyzer";
        private bool _isFileLoaded;
        private bool _isPlaying;
        private bool _isPaused;
        private TimeSpan _currentTime;
        private TimeSpan _totalTime;
        private double _progress;
        private double _playheadPosition;
        private float[]? _waveformData;
        private Dictionary<double, Controls.FrequencyWaveformData>? _frequencyWaveforms;
        private SpectrumData? _spectrumData;
        private int _selectedFrequencyCount;
        private bool _showOnlySelectedFrequencies = true;
        private HashSet<double> _selectedFrequencies = new();
        private bool _isWaveformLoading = false;
        private string _waveformLoadingMessage = string.Empty;
        private double _zoomLevel = 1.0;
        private double _zoomStartTime = 0.0;
        private double _zoomEndTime = 1.0;
        private bool _syncSpectrumWithWaveform = false;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string CurrentFileName
        {
            get => _currentFileName;
            set
            {
                if (SetProperty(ref _currentFileName, value))
                {
                    // Update window title when filename changes
                    WindowTitle = string.IsNullOrEmpty(value) 
                        ? "SRS Signal Analyzer" 
                        : $"SRS Signal Analyzer - {value}";
                }
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            set => SetProperty(ref _isFileLoaded, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                if (SetProperty(ref _totalTime, value))
                {
                    OnPropertyChanged(nameof(TimeRangeDisplay));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public double PlayheadPosition
        {
            get => _playheadPosition;
            set => SetProperty(ref _playheadPosition, value);
        }

        public float[]? WaveformData
        {
            get => _waveformData;
            set => SetProperty(ref _waveformData, value);
        }

        public Dictionary<double, Controls.FrequencyWaveformData>? FrequencyWaveforms
        {
            get => _frequencyWaveforms;
            set => SetProperty(ref _frequencyWaveforms, value);
        }

        public SpectrumData? SpectrumData
        {
            get => _spectrumData;
            set => SetProperty(ref _spectrumData, value);
        }

        public int SelectedFrequencyCount
        {
            get => _selectedFrequencyCount;
            set => SetProperty(ref _selectedFrequencyCount, value);
        }

        public bool ShowOnlySelectedFrequencies
        {
            get => _showOnlySelectedFrequencies;
            set => SetProperty(ref _showOnlySelectedFrequencies, value);
        }

        public HashSet<double> SelectedFrequencies
        {
            get => _selectedFrequencies;
            set => SetProperty(ref _selectedFrequencies, value);
        }

        public bool IsWaveformLoading
        {
            get => _isWaveformLoading;
            set => SetProperty(ref _isWaveformLoading, value);
        }

        public string WaveformLoadingMessage
        {
            get => _waveformLoadingMessage;
            set => SetProperty(ref _waveformLoadingMessage, value);
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (SetProperty(ref _zoomLevel, value))
                {
                    OnPropertyChanged(nameof(TimeRangeDisplay));
                }
            }
        }

        public double ZoomStartTime
        {
            get => _zoomStartTime;
            set
            {
                if (SetProperty(ref _zoomStartTime, value))
                {
                    OnPropertyChanged(nameof(TimeRangeDisplay));
                }
            }
        }

        public double ZoomEndTime
        {
            get => _zoomEndTime;
            set
            {
                if (SetProperty(ref _zoomEndTime, value))
                {
                    OnPropertyChanged(nameof(TimeRangeDisplay));
                }
            }
        }

        public bool SyncSpectrumWithWaveform
        {
            get => _syncSpectrumWithWaveform;
            set => SetProperty(ref _syncSpectrumWithWaveform, value);
        }

        /// <summary>
        /// Gets the formatted time range display string
        /// </summary>
        public string TimeRangeDisplay
        {
            get
            {
                if (TotalTime.TotalSeconds == 0)
                    return "Time Range: 00:00.000 - 00:00.000";

                var startTime = TimeSpan.FromSeconds(ZoomStartTime * TotalTime.TotalSeconds);
                var endTime = TimeSpan.FromSeconds(ZoomEndTime * TotalTime.TotalSeconds);
                
                var zoomText = ZoomLevel > 1.0 ? $" (Zoom x{ZoomLevel:F1})" : "";
                
                return $"Time Range: {startTime:mm\\:ss\\.fff} – {endTime:mm\\:ss\\.fff}{zoomText}";
            }
        }

        public ObservableCollection<FrequencyGroupViewModel> AvailableFrequencies { get; } = new();
        public ObservableCollection<ActiveFrequencyViewModel> ActiveFrequencies { get; } = new();

        /// <summary>
        /// Updates the selected frequencies and active frequency mixer
        /// </summary>
        public void UpdateSelectedFrequencies()
        {
            var selectedFreqs = new HashSet<double>();
            var activeFreqs = new List<ActiveFrequencyViewModel>();

            foreach (var group in AvailableFrequencies)
            {
                foreach (var freq in group.Frequencies.Where(f => f.IsSelected))
                {
                    selectedFreqs.Add(freq.Frequency);
                    
                    // Add to active frequencies if not already there
                    if (!ActiveFrequencies.Any(af => Math.Abs(af.Frequency - freq.Frequency) < 0.1))
                    {
                        activeFreqs.Add(new ActiveFrequencyViewModel
                        {
                            Frequency = freq.Frequency,
                            DisplayName = freq.DisplayName,
                            Gain = 1.0f,
                            Pan = 0.0f
                        });
                    }
                }
            }

            // Remove deselected frequencies from active list
            var toRemove = ActiveFrequencies
                .Where(af => !selectedFreqs.Any(sf => Math.Abs(sf - af.Frequency) < 0.1))
                .ToList();

            foreach (var freq in toRemove)
            {
                ActiveFrequencies.Remove(freq);
            }

            // Add new active frequencies
            foreach (var freq in activeFreqs)
            {
                ActiveFrequencies.Add(freq);
            }

            SelectedFrequencies = selectedFreqs;
            SelectedFrequencyCount = selectedFreqs.Count;
        }
    }

    public class FrequencyGroupViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ObservableCollection<FrequencyViewModel> Frequencies { get; } = new();
    }

    public class FrequencyViewModel : ViewModelBase
    {
        private double _frequency;
        private string _modulation = string.Empty;
        private string _displayName = string.Empty;
        private bool _isSelected;
        private int _packetCount;
        private bool _isActive;
        private DateTime _lastActivity;
        private System.Windows.Media.Color _waveformColor;

        public double Frequency
        {
            get => _frequency;
            set => SetProperty(ref _frequency, value);
        }

        public string Modulation
        {
            get => _modulation;
            set => SetProperty(ref _modulation, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int PacketCount
        {
            get => _packetCount;
            set => SetProperty(ref _packetCount, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public DateTime LastActivity
        {
            get => _lastActivity;
            set => SetProperty(ref _lastActivity, value);
        }

        public System.Windows.Media.Color WaveformColor
        {
            get => _waveformColor;
            set => SetProperty(ref _waveformColor, value);
        }

        public FrequencyModulationInfo? SourceData { get; set; }

        /// <summary>
        /// Gets the most active player on this frequency
        /// </summary>
        public string MostActivePlayer => 
            SourceData?.Players.OrderByDescending(p => p.PacketCount).FirstOrDefault()?.Name ?? "Unknown";

        /// <summary>
        /// Gets the activity percentage for this frequency
        /// </summary>
        public double ActivityPercentage
        {
            get
            {
                if (SourceData?.Players.Count == 0)
                    return 0;

                var players = SourceData.Players;
                var totalRecordingTime = players.Max(p => p.LastSeen) - players.Min(p => p.FirstSeen);
                if (totalRecordingTime.TotalSeconds <= 0)
                    return 0;

                var totalTransmissionTime = players.Sum(p => (p.LastSeen - p.FirstSeen).TotalSeconds);
                return (totalTransmissionTime / totalRecordingTime.TotalSeconds) * 100;
            }
        }
    }

    public class ActiveFrequencyViewModel : ViewModelBase
    {
        private double _frequency;
        private string _displayName = string.Empty;
        private float _gain = 1.0f;
        private float _pan = 0.0f;

        public double Frequency
        {
            get => _frequency;
            set => SetProperty(ref _frequency, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public float Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        public float Pan
        {
            get => _pan;
            set => SetProperty(ref _pan, value);
        }
    }

    // Spectrum data model for FFT visualization
    public class SpectrumData
    {
        public float[] Magnitudes { get; set; } = Array.Empty<float>();
        public double[] Frequencies { get; set; } = Array.Empty<double>();
        public DateTime Timestamp { get; set; }
        public double SampleRate { get; set; }
    }
}