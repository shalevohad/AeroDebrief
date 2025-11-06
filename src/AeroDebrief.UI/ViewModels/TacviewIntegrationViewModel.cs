using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AeroDebrief.Integrations.Tacview;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.UI.Commands;
using NLog;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// View model for Tacview integration status and control
    /// Displays connection status, sync quality, pilot selection, and configuration
    /// </summary>
    public class TacviewIntegrationViewModel : ViewModelBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly TacviewIntegrationService _integrationService;
        
        // Connection state
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private string _statusMessage = "Not Connected";
        private Brush _statusColor = Brushes.Gray; // Gray = not running/disconnected
        private string _portDisplay = "Port: -";
        private bool _hasConnectionError = false; // Track if last connection attempt failed
        
        // Sync quality
        private int _syncDriftMs;
        private double _syncQualityPercent;
        private bool _isSynchronized;
        
        // Speed limiting warnings
        private bool _isSpeedClamped;
        private double _requestedSpeed;
        private double _actualSpeed;
        private string _speedClampWarning = string.Empty;
        
        // Selected pilots
        private ObservableCollection<TacviewPilotViewModel> _selectedPilots = new();
        
        // Frequency filtering
        private ObservableCollection<TacviewFrequencyViewModel> _allKnownFrequencies = new();
        
        // Pan configuration
        private string _panMode = "auto";
        private bool _isAutoPan = true;
        private bool _isManualPan;
        
        // Configuration
        private TacviewConfiguration _config;
        
        #region Properties
        
        public ConnectionState ConnectionState
        {
            get => _connectionState;
            set
            {
                if (SetProperty(ref _connectionState, value))
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsDisconnected));
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    UpdateStatusDisplay();
                }
            }
        }
        
        /// <summary>
        /// Connection status text for the status badge (e.g., "Connected", "Disconnected", "N/A")
        /// </summary>
        public string ConnectionStatusText
        {
            get
            {
                return ConnectionState switch
                {
                    ConnectionState.Disconnected => _hasConnectionError ? "Error" : "Disconnected",
                    ConnectionState.Connecting => "Connecting",
                    ConnectionState.Connected => "Connected",
                    ConnectionState.Synchronized => "Connected",
                    ConnectionState.Degraded => "Degraded",
                    _ => "N/A"
                };
            }
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }
        
        public string PortDisplay
        {
            get => _portDisplay;
            set => SetProperty(ref _portDisplay, value);
        }
        
        public int SyncDriftMs
        {
            get => _syncDriftMs;
            set => SetProperty(ref _syncDriftMs, value);
        }
        
        public double SyncQualityPercent
        {
            get => _syncQualityPercent;
            set => SetProperty(ref _syncQualityPercent, value);
        }
        
        public bool IsSynchronized
        {
            get => _isSynchronized;
            set => SetProperty(ref _isSynchronized, value);
        }
        
        public string SyncDriftDisplay => $"{SyncDriftMs}ms drift";
        
        /// <summary>
        /// True if playback speed is being clamped to supported range
        /// </summary>
        public bool IsSpeedClamped
        {
            get => _isSpeedClamped;
            set => SetProperty(ref _isSpeedClamped, value);
        }
        
        /// <summary>
        /// The speed requested by Tacview (before clamping)
        /// </summary>
        public double RequestedSpeed
        {
            get => _requestedSpeed;
            set
            {
                if (SetProperty(ref _requestedSpeed, value))
                {
                    OnPropertyChanged(nameof(RequestedSpeedDisplay));
                }
            }
        }
        
        /// <summary>
        /// The actual playback speed (after clamping)
        /// </summary>
        public double ActualSpeed
        {
            get => _actualSpeed;
            set
            {
                if (SetProperty(ref _actualSpeed, value))
                {
                    OnPropertyChanged(nameof(ActualSpeedDisplay));
                }
            }
        }
        
        /// <summary>
        /// Warning message explaining why speed was clamped
        /// </summary>
        public string SpeedClampWarning
        {
            get => _speedClampWarning;
            set => SetProperty(ref _speedClampWarning, value);
        }
        
        /// <summary>
        /// Formatted display of requested speed
        /// </summary>
        public string RequestedSpeedDisplay => $"{RequestedSpeed:F2}x";
        
        /// <summary>
        /// Formatted display of actual speed
        /// </summary>
        public string ActualSpeedDisplay => $"{ActualSpeed:F2}x";
        
        public ObservableCollection<TacviewPilotViewModel> SelectedPilots
        {
            get => _selectedPilots;
            set => SetProperty(ref _selectedPilots, value);
        }
        
        public ObservableCollection<TacviewFrequencyViewModel> AllKnownFrequencies
        {
            get => _allKnownFrequencies;
            set => SetProperty(ref _allKnownFrequencies, value);
        }
        
        public string PanMode
        {
            get => _panMode;
            set
            {
                if (SetProperty(ref _panMode, value))
                {
                    IsAutoPan = value == "auto";
                    IsManualPan = value == "manual";
                }
            }
        }
        
        public bool IsAutoPan
        {
            get => _isAutoPan;
            set => SetProperty(ref _isAutoPan, value);
        }
        
        public bool IsManualPan
        {
            get => _isManualPan;
            set => SetProperty(ref _isManualPan, value);
        }
        
        public TacviewConfiguration Config
        {
            get => _config;
            set => SetProperty(ref _config, value);
        }
        
        public bool IsConnected => ConnectionState != ConnectionState.Disconnected && ConnectionState != ConnectionState.Connecting;
        public bool IsDisconnected => !IsConnected;
        public bool HasSelectedPilots => SelectedPilots.Count > 0;
        
        /// <summary>
        /// Indicates whether Tacview integration is available and should be displayed in UI
        /// Always true - the control will show "Not Connected" state if disconnected
        /// </summary>
        public bool IsAvailable => true;
        
        #endregion
        
        #region Commands
        
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ReconnectCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        
        // Pan configuration commands
        public ICommand UpdatePanConfigurationCommand { get; }
        public ICommand SetAutoPanCommand { get; }
        public ICommand SetManualPanCommand { get; }
        
        // Frequency filtering commands
        public ICommand UpdatePilotFrequenciesCommand { get; }
        public ICommand UpdateGeneralFrequenciesCommand { get; }
        public ICommand ToggleFrequencyCommand { get; }
        public ICommand EnableAllFrequenciesCommand { get; }
        public ICommand DisableAllFrequenciesCommand { get; }
        public ICommand EnableAllGeneralFrequenciesCommand { get; }
        public ICommand DisableAllGeneralFrequenciesCommand { get; }
        
        #endregion
        
        public TacviewIntegrationViewModel(TacviewIntegrationService integrationService)
        {
            _integrationService = integrationService ?? throw new ArgumentNullException(nameof(integrationService));
            
            // Subscribe to events
            _integrationService.ConnectionStateChanged += OnConnectionStateChanged;
            _integrationService.SyncQualityChanged += OnSyncQualityChanged;
            _integrationService.PilotSelectionChanged += OnPilotSelectionChanged;
            _integrationService.SpeedClampedChanged += (s, e) => OnSpeedClamped(e.isClamped, e.requestedSpeed, e.actualSpeed);
            
            // Load configuration
            _config = integrationService.AudioFilter is null 
                ? TacviewConfiguration.LoadFromFile(GetDefaultConfigPath()) 
                : new TacviewConfiguration();
            
            PortDisplay = $"Port: {_config.Port}";
            
            // Initialize commands
            ConnectCommand = new RelayCommand(async () => await ExecuteConnectAsync(), () => IsDisconnected);
            DisconnectCommand = new RelayCommand(async () => await ExecuteDisconnectAsync(), () => IsConnected);
            ReconnectCommand = new RelayCommand(async () => await ExecuteReconnectAsync(), () => true);
            TestConnectionCommand = new RelayCommand(async () => await ExecuteTestConnectionAsync());
            SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            
            // Pan configuration commands
            UpdatePanConfigurationCommand = new RelayCommand(async () => await ExecuteUpdatePanConfigurationAsync(), () => IsConnected);
            SetAutoPanCommand = new RelayCommand(() => { PanMode = "auto"; _ = ExecuteUpdatePanConfigurationAsync(); }, () => IsConnected);
            SetManualPanCommand = new RelayCommand(() => { PanMode = "manual"; _ = ExecuteUpdatePanConfigurationAsync(); }, () => IsConnected);
            
            // Frequency filtering commands
            UpdatePilotFrequenciesCommand = new RelayCommand<TacviewPilotViewModel>(async pilot => await ExecuteUpdatePilotFrequenciesAsync(pilot), pilot => pilot != null && IsConnected);
            UpdateGeneralFrequenciesCommand = new RelayCommand(async () => await ExecuteUpdateGeneralFrequenciesAsync(), () => IsConnected);
            ToggleFrequencyCommand = new RelayCommand<(TacviewPilotViewModel pilot, double frequency)>(async param => await ExecuteToggleFrequencyAsync(param.pilot, param.frequency), param => IsConnected);
            EnableAllFrequenciesCommand = new RelayCommand<TacviewPilotViewModel>(pilot => ExecuteEnableAllFrequencies(pilot), pilot => pilot != null);
            DisableAllFrequenciesCommand = new RelayCommand<TacviewPilotViewModel>(pilot => ExecuteDisableAllFrequencies(pilot), pilot => pilot != null);
            EnableAllGeneralFrequenciesCommand = new RelayCommand(ExecuteEnableAllGeneralFrequencies);
            DisableAllGeneralFrequenciesCommand = new RelayCommand(ExecuteDisableAllGeneralFrequencies);
            
            Logger.Info("TacviewIntegrationViewModel initialized");
            
            // Auto-connect if configured
            if (_config.AutoConnect)
            {
                Logger.Info("Auto-connect enabled - attempting to connect to Tacview...");
                // Use Task.Run to avoid blocking the constructor
                _ = Task.Run(async () =>
                {
                    // Wait a moment for initialization to complete
                    await Task.Delay(500);
                    try
                    {
                        await ExecuteConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Auto-connect to Tacview failed - will remain disconnected");
                    }
                });
            }
        }
        
        /// <summary>
        /// Loads frequencies from the audio file into AllKnownFrequencies
        /// This should be called when a file is loaded in the main player
        /// </summary>
        public void LoadFrequenciesFromAudioFile(IEnumerable<double> frequencies)
        {
            Logger.Info($"?? LoadFrequenciesFromAudioFile called with {frequencies?.Count() ?? 0} frequencies");
            
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AllKnownFrequencies.Clear();
                
                if (frequencies == null)
                {
                    Logger.Info("No frequencies provided");
                    return;
                }
                
                var freqList = frequencies.OrderBy(f => f).ToList();
                Logger.Info($"Loading {freqList.Count} frequencies from audio file");
                
                foreach (var freq in freqList)
                {
                    var freqViewModel = new TacviewFrequencyViewModel
                    {
                        Frequency = freq,
                        DisplayName = $"{freq / 1_000_000.0:F3} MHz",
                        IsGeneralEnabled = false, // Default to disabled until user selects or Tacview sends selection
                        IsNewlyDiscovered = false // Not new for pre-analyzed files
                    };
                    
                    AllKnownFrequencies.Add(freqViewModel);
                    Logger.Debug($"   ? Added frequency from file: {freqViewModel.DisplayName}");
                }
                
                Logger.Info($"? Loaded {AllKnownFrequencies.Count} frequencies from audio file");
                OnPropertyChanged(nameof(AllKnownFrequencies));
            });
        }
        
        /// <summary>
        /// Adds a frequency discovered during live SRS recording
        /// Called when a new packet arrives with a previously unseen frequency
        /// The frequency will be marked as newly discovered for visual feedback (glow effect)
        /// </summary>
        public void AddLiveFrequency(double frequency)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Check if frequency already exists
                if (AllKnownFrequencies.Any(f => Math.Abs(f.Frequency - frequency) < 0.1))
                {
                    return; // Already exists
                }
                
                var freqViewModel = new TacviewFrequencyViewModel
                {
                    Frequency = frequency,
                    DisplayName = $"{frequency / 1_000_000.0:F3} MHz",
                    IsGeneralEnabled = true, // Enable by default for live recording
                    IsNewlyDiscovered = true // Mark as new for glow effect
                };
                
                // Insert in sorted order
                int insertIndex = 0;
                for (int i = 0; i < AllKnownFrequencies.Count; i++)
                {
                    if (AllKnownFrequencies[i].Frequency > frequency)
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }
                
                AllKnownFrequencies.Insert(insertIndex, freqViewModel);
                Logger.Info($"?? Discovered live frequency: {freqViewModel.DisplayName}");
                OnPropertyChanged(nameof(AllKnownFrequencies));
                
                // Clear the "newly discovered" flag after animation completes (e.g., 2 seconds)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        freqViewModel.IsNewlyDiscovered = false;
                    });
                });
            });
        }
        
        #region Command Implementations
        
        private async System.Threading.Tasks.Task ExecuteConnectAsync()
        {
            try
            {
                _hasConnectionError = false; // Reset error flag
                StatusMessage = "Connecting...";
                StatusColor = Brushes.Orange; // Connecting state
                await _integrationService.ConnectAsync();
                // Success - error flag stays false, status will be updated by event
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to Tacview");
                _hasConnectionError = true; // Mark error state
                // Fail silently: update status message and color, but do not show modal dialog
                StatusMessage = "Connection Error";
                StatusColor = Brushes.Red; // Red = error connecting
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteDisconnectAsync()
        {
            try
            {
                await _integrationService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to disconnect from Tacview");
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteReconnectAsync()
        {
            try
            {
                if (IsConnected)
                    await ExecuteDisconnectAsync();
                
                await ExecuteConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to reconnect to Tacview");
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteTestConnectionAsync()
        {
            try
            {
                StatusMessage = "Testing connection...";
                
                // Try to connect
                await _integrationService.ConnectAsync();
                
                // Wait a moment
                await System.Threading.Tasks.Task.Delay(1000);
                
                // Check if connected
                if (_integrationService.IsConnected)
                {
                    StatusMessage = "Connection test successful!";
                    System.Windows.MessageBox.Show("Successfully connected to Tacview!", 
                        "Connection Test", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    
                    // Disconnect after test
                    await ExecuteDisconnectAsync();
                }
                else
                {
                    StatusMessage = "Connection test failed";
                    System.Windows.MessageBox.Show("Failed to connect to Tacview. Check that Tacview is running with the AeroDebrief addon loaded.", 
                        "Connection Test", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Connection test failed");
                StatusMessage = "Connection test failed";
                System.Windows.MessageBox.Show($"Connection test failed: {ex.Message}", 
                    "Connection Test", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private void ExecuteSaveSettings()
        {
            try
            {
                _config.SaveToFile(GetDefaultConfigPath());
                StatusMessage = "Settings saved";
                Logger.Info("Tacview settings saved");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings");
                System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", 
                    "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private void ExecuteOpenSettings()
        {
            // TODO: Open settings dialog
            Logger.Info("Open Tacview settings (not yet implemented)");
        }
        
        private async System.Threading.Tasks.Task ExecuteUpdatePanConfigurationAsync()
        {
            if (!IsConnected)
                return;
            
            try
            {
                var pilotPanSettings = new Dictionary<string, double>();
                foreach (var pilot in SelectedPilots)
                {
                    pilotPanSettings[pilot.PilotId] = pilot.Pan;
                }
                
                await _integrationService.SendPanConfigurationAsync(PanMode, pilotPanSettings);
                
                Logger.Info($"Sent pan configuration to Tacview: {PanMode} mode");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update pan configuration");
                System.Windows.MessageBox.Show($"Failed to update pan configuration in Tacview: {ex.Message}", 
                    "Tacview Update Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteUpdatePilotFrequenciesAsync(TacviewPilotViewModel pilot)
        {
            if (!IsConnected || pilot == null)
                return;
            
            try
            {
                var enabledFrequencies = pilot.EnabledFrequencies.ToList();
                await _integrationService.SendFrequencyFilterUpdateAsync(pilot.PilotId, enabledFrequencies);
                
                Logger.Info($"Sent pilot frequency update to Tacview: {pilot.PilotName}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send pilot frequency update");
                System.Windows.MessageBox.Show($"Failed to update frequencies in Tacview: {ex.Message}", 
                    "Tacview Update Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteUpdateGeneralFrequenciesAsync()
        {
            if (!IsConnected)
                return;
            
            try
            {
                var enabledFrequencies = AllKnownFrequencies
                    .Where(f => f.IsGeneralEnabled)
                    .Select(f => f.Frequency)
                    .ToList();
                
                await _integrationService.SendFrequencyFilterUpdateAsync(null, enabledFrequencies);
                
                Logger.Info("Sent general frequency update to Tacview");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send general frequency update");
                System.Windows.MessageBox.Show($"Failed to update general frequencies in Tacview: {ex.Message}", 
                    "Tacview Update Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private async System.Threading.Tasks.Task ExecuteToggleFrequencyAsync(TacviewPilotViewModel pilot, double frequency)
        {
            if (pilot == null)
                return;
            
            if (pilot.EnabledFrequencies.Contains(frequency))
            {
                pilot.EnabledFrequencies.Remove(frequency);
            }
            else
            {
                pilot.EnabledFrequencies.Add(frequency);
            }
            
            // Send update to Tacview
            await ExecuteUpdatePilotFrequenciesAsync(pilot);
        }
        
        private void ExecuteEnableAllFrequencies(TacviewPilotViewModel pilot)
        {
            if (pilot == null)
                return;
            
            pilot.EnabledFrequencies.Clear();
            foreach (var freq in pilot.AllFrequencies)
            {
                pilot.EnabledFrequencies.Add(freq);
            }
            
            _ = ExecuteUpdatePilotFrequenciesAsync(pilot);
        }
        
        private void ExecuteDisableAllFrequencies(TacviewPilotViewModel pilot)
        {
            if (pilot == null)
                return;
            
            pilot.EnabledFrequencies.Clear();
            _ = ExecuteUpdatePilotFrequenciesAsync(pilot);
        }
        
        private void ExecuteEnableAllGeneralFrequencies()
        {
            foreach (var freq in AllKnownFrequencies)
            {
                freq.IsGeneralEnabled = true;
            }
            
            _ = ExecuteUpdateGeneralFrequenciesAsync();
        }
        
        private void ExecuteDisableAllGeneralFrequencies()
        {
            foreach (var freq in AllKnownFrequencies)
            {
                freq.IsGeneralEnabled = false;
            }
            
            _ = ExecuteUpdateGeneralFrequenciesAsync();
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnConnectionStateChanged(object? sender, ConnectionState state)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ConnectionState = state;
                UpdateStatusDisplay();
                
                // Notify that connection status text has changed
                OnPropertyChanged(nameof(ConnectionStatusText));
                
                // Update command can-execute state
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            });
        }
        
        private void OnSyncQualityChanged(object? sender, double quality)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                SyncQualityPercent = quality;
                
                // Get sync health
                var syncHealth = _integrationService.GetSyncHealth();
                IsSynchronized = syncHealth.IsHealthy;
                SyncDriftMs = Math.Abs(syncHealth.DriftMs);
                
                // Update drift display
                OnPropertyChanged(nameof(SyncDriftDisplay));
            });
        }
        
        /// <summary>
        /// Handles speed clamping notifications from PlaybackController
        /// </summary>
        private void OnSpeedClamped(bool isClamped, double requestedSpeed, double actualSpeed)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsSpeedClamped = isClamped;
                RequestedSpeed = requestedSpeed;
                ActualSpeed = actualSpeed;
                
                if (isClamped)
                {
                    if (requestedSpeed < AeroDebrief.Core.Constants.MIN_PLAYBACK_SPEED)
                    {
                        SpeedClampWarning = $"?? Tacview speed {requestedSpeed:F2}x is too slow. " +
                                          $"Limited to {AeroDebrief.Core.Constants.MIN_PLAYBACK_SPEED}x (minimum supported).";
                    }
                    else if (requestedSpeed > AeroDebrief.Core.Constants.MAX_PLAYBACK_SPEED)
                    {
                        SpeedClampWarning = $"?? Tacview speed {requestedSpeed:F2}x is too fast. " +
                                          $"Limited to {AeroDebrief.Core.Constants.MAX_PLAYBACK_SPEED}x (maximum supported).";
                    }
                    
                    Logger.Info($"Speed clamped: {requestedSpeed:F2}x ? {actualSpeed:F2}x");
                }
                else
                {
                    SpeedClampWarning = string.Empty;
                }
            });
        }
        
        private void OnPilotSelectionChanged(object? sender, PilotSelectionMessage? selection)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Logger.Info($"?? OnPilotSelectionChanged triggered. Selection is null: {selection == null}");
                
                // Clear existing pilots
                SelectedPilots.Clear();
                
                // Handle null selection (disconnected/cleared state)
                if (selection == null)
                {
                    PanMode = "auto";
                    
                    // DON'T clear AllKnownFrequencies - keep frequencies from audio file
                    // Just reset their enabled state
                    foreach (var freq in AllKnownFrequencies)
                    {
                        freq.IsGeneralEnabled = false;
                    }
                    
                    OnPropertyChanged(nameof(HasSelectedPilots));
                    Logger.Info("Pilot selection cleared - kept frequencies from audio file, reset enabled states");
                    return;
                }
                
                Logger.Info($"   ? Received selection with {selection.SelectedPilots.Count} pilots");
                
                // Add selected pilots
                foreach (var pilot in selection.SelectedPilots)
                {
                    Logger.Info($"      ?? Pilot: {pilot.PilotName}, Frequencies: {pilot.Frequencies.Count}");
                    
                    var pilotViewModel = new TacviewPilotViewModel
                    {
                        PilotId = pilot.PilotId,
                        PilotName = pilot.PilotName,
                        Coalition = pilot.Coalition ?? "Unknown",
                        UnitType = pilot.UnitType ?? "Unknown",
                        Pan = pilot.Pan,
                        AllFrequencies = pilot.Frequencies.ToList(),
                        EnabledFrequencies = new ObservableCollection<double>(pilot.EnabledFrequencies ?? pilot.Frequencies)
                    };
                    
                    SelectedPilots.Add(pilotViewModel);
                }
                
                // Update pan mode
                PanMode = selection.PanMode;
                
                // Update general frequencies from Tacview selection
                UpdateGeneralFrequenciesFromTacview(selection);
                
                Logger.Info($"   ?? AllKnownFrequencies now has {AllKnownFrequencies.Count} frequencies");
                Logger.Info($"   ?? SelectedPilots now has {SelectedPilots.Count} pilots");
                
                // Update hasSelectedPilots property
                OnPropertyChanged(nameof(HasSelectedPilots));
                
                Logger.Info($"Pilot selection updated: {SelectedPilots.Count} pilots selected, {AllKnownFrequencies.Count} total frequencies");
            });
        }
        
        /// <summary>
        /// Updates general frequency enabled states from Tacview pilot selection
        /// Merges with existing frequencies rather than replacing
        /// </summary>
        private void UpdateGeneralFrequenciesFromTacview(PilotSelectionMessage selection)
        {
            Logger.Info($"?? UpdateGeneralFrequenciesFromTacview called");
            
            // Collect all known frequencies from all pilots
            var pilotFrequencies = selection.SelectedPilots
                .SelectMany(p => p.Frequencies)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
            
            Logger.Info($"   ?? Found {pilotFrequencies.Count} unique frequencies from Tacview pilots");
            
            // If AllKnownFrequencies is empty (no audio file loaded), populate from Tacview
            if (AllKnownFrequencies.Count == 0)
            {
                Logger.Info("   ?? No frequencies from audio file, populating from Tacview");
                
                foreach (var freq in pilotFrequencies)
                {
                    var isGeneralEnabled = selection.GeneralEnabledFrequencies?.Contains(freq) ?? false;
                    
                    var freqViewModel = new TacviewFrequencyViewModel
                    {
                        Frequency = freq,
                        DisplayName = $"{freq / 1_000_000.0:F3} MHz",
                        IsGeneralEnabled = isGeneralEnabled,
                        IsNewlyDiscovered = true // Mark as new for glow effect
                    };
                    
                    AllKnownFrequencies.Add(freqViewModel);
                    Logger.Debug($"      ? Added frequency from Tacview: {freqViewModel.DisplayName}, Enabled: {isGeneralEnabled}");
                    
                    // Clear the "newly discovered" flag after animation completes (e.g., 2 seconds)
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            freqViewModel.IsNewlyDiscovered = false;
                        });
                    });
                }
            }
            else
            {
                // Update enabled states for frequencies that match Tacview pilot selections
                Logger.Info("   ?? Updating enabled states for existing frequencies");
                
                foreach (var freqViewModel in AllKnownFrequencies)
                {
                    if (pilotFrequencies.Contains(freqViewModel.Frequency))
                    {
                        var isGeneralEnabled = selection.GeneralEnabledFrequencies?.Contains(freqViewModel.Frequency) ?? false;
                        freqViewModel.IsGeneralEnabled = isGeneralEnabled;
                        Logger.Debug($"      ?? Updated frequency: {freqViewModel.DisplayName}, Enabled: {isGeneralEnabled}");
                    }
                }
                
                // Add any new frequencies from Tacview that weren't in the audio file
                foreach (var freq in pilotFrequencies)
                {
                    if (!AllKnownFrequencies.Any(f => Math.Abs(f.Frequency - freq) < 0.1))
                    {
                        var isGeneralEnabled = selection.GeneralEnabledFrequencies?.Contains(freq) ?? false;
                        
                        var freqViewModel = new TacviewFrequencyViewModel
                        {
                            Frequency = freq,
                            DisplayName = $"{freq / 1_000_000.0:F3} MHz",
                            IsGeneralEnabled = isGeneralEnabled,
                            IsNewlyDiscovered = true // Mark as new for glow effect
                        };
                        
                        // Insert in sorted order
                        int insertIndex = 0;
                        for (int i = 0; i < AllKnownFrequencies.Count; i++)
                        {
                            if (AllKnownFrequencies[i].Frequency > freq)
                            {
                                insertIndex = i;
                                break;
                            }
                            insertIndex = i + 1;
                        }
                        
                        AllKnownFrequencies.Insert(insertIndex, freqViewModel);
                        Logger.Debug($"      ? Added new frequency from Tacview: {freqViewModel.DisplayName}, Enabled: {isGeneralEnabled}");
                        
                        // Clear the "newly discovered" flag after animation completes (e.g., 2 seconds)
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(2000);
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                freqViewModel.IsNewlyDiscovered = false;
                            });
                        });
                    }
                }
            }
            
            Logger.Info($"   ? AllKnownFrequencies collection now has {AllKnownFrequencies.Count} items");
            OnPropertyChanged(nameof(AllKnownFrequencies));
        }
        
        private void UpdateStatusDisplay()
        {
            // Clear error flag on successful connection
            if (ConnectionState == ConnectionState.Connected || ConnectionState == ConnectionState.Synchronized)
            {
                _hasConnectionError = false;
            }
            
            // Determine status message and color
            if (_hasConnectionError && ConnectionState == ConnectionState.Disconnected)
            {
                // Red: Had a connection error
                StatusMessage = "Connection Error";
                StatusColor = Brushes.Red;
            }
            else
            {
                // Normal status based on connection state
                (StatusMessage, StatusColor) = ConnectionState switch
                {
                    // Gray: Normal disconnect (Tacview not running)
                    ConnectionState.Disconnected => ("Not Connected", Brushes.Gray),
                    
                    // Orange: Attempting to connect
                    ConnectionState.Connecting => ("Connecting...", Brushes.Orange),
                    
                    // Green: Successfully connected
                    ConnectionState.Connected => ("Connected", new SolidColorBrush(Color.FromRgb(76, 175, 80))), // #4CAF50
                    
                    // Green: Connected and synchronized (ideal state)
                    ConnectionState.Synchronized => ("Synchronized", new SolidColorBrush(Color.FromRgb(76, 175, 80))), // #4CAF50
                    
                    // Red: Connection exists but degraded/error state
                    ConnectionState.Degraded => ("Degraded", Brushes.Red),
                    
                    // Red: Unknown error state
                    _ => ("Unknown State", Brushes.Red)
                };
            }
            
            // Notify that connection status text has changed
            OnPropertyChanged(nameof(ConnectionStatusText));
        }
        
        #endregion
        
        private static string GetDefaultConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = System.IO.Path.Combine(appData, "AeroDebrief", "Tacview");
            
            if (!System.IO.Directory.Exists(configDir))
            {
                System.IO.Directory.CreateDirectory(configDir);
            }
            
            return System.IO.Path.Combine(configDir, "tacview-config.json");
        }
    }
    
    /// <summary>
    /// View model for a Tacview pilot
    /// </summary>
    public class TacviewPilotViewModel : ViewModelBase
    {
        private string _pilotId = string.Empty;
        private string _pilotName = string.Empty;
        private string _coalition = string.Empty;
        private string _unitType = string.Empty;
        private double _pan;
        
        public string PilotId
        {
            get => _pilotId;
            set => SetProperty(ref _pilotId, value);
        }
        
        public string PilotName
        {
            get => _pilotName;
            set => SetProperty(ref _pilotName, value);
        }
        
        public string Coalition
        {
            get => _coalition;
            set => SetProperty(ref _coalition, value);
        }
        
        public string UnitType
        {
            get => _unitType;
            set => SetProperty(ref _unitType, value);
        }
        
        public double Pan
        {
            get => _pan;
            set => SetProperty(ref _pan, Math.Clamp(value, -1.0, 1.0));
        }
        
        public string PanDisplay => Pan switch
        {
            < -0.01 => $"L{Math.Abs(Pan):F2}",
            > 0.01 => $"R{Pan:F2}",
            _ => "Center"
        };
        
        public List<double> AllFrequencies { get; set; } = new();
        public ObservableCollection<double> EnabledFrequencies { get; set; } = new();
        
        public string FrequencyDisplay
        {
            get
            {
                if (!EnabledFrequencies.Any())
                    return "No frequencies";
                
                if (EnabledFrequencies.Count == AllFrequencies.Count)
                    return "All frequencies";
                
                return $"{EnabledFrequencies.Count}/{AllFrequencies.Count} frequencies";
            }
        }
    }
    
    /// <summary>
    /// View model for a frequency item in Tacview integration
    /// </summary>
    public class TacviewFrequencyViewModel : ViewModelBase
    {
        private double _frequency;
        private string _displayName = string.Empty;
        private bool _isGeneralEnabled;
        private bool _isNewlyDiscovered;
        
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
        
        public bool IsGeneralEnabled
        {
            get => _isGeneralEnabled;
            set => SetProperty(ref _isGeneralEnabled, value);
        }
        
        /// <summary>
        /// Indicates if this frequency was just discovered during live recording
        /// Used for visual feedback (glow effect animation)
        /// </summary>
        public bool IsNewlyDiscovered
        {
            get => _isNewlyDiscovered;
            set => SetProperty(ref _isNewlyDiscovered, value);
        }
    }
}
