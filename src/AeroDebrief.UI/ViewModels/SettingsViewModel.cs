using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AeroDebrief.Core;
using AeroDebrief.Core.Settings;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings window
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private int _masterVolume;
        private bool _enableFrequencyFilterByDefault;
        private bool _enableDebugLogging;
        private int _audioActivityThreshold;
        private int _audioActivityMinDuration;
        private string _themeFile;
        
        // AGC Settings
        private bool _agcEnabled;
        private double _agcTargetDB;
        private double _agcMaxBoostDB;
        private double _agcMaxCutDB;
        
        // Visualization Settings
        private bool _useDbScale;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Default master volume (0-200)
        /// </summary>
        public int MasterVolume
        {
            get => _masterVolume;
            set => SetProperty(ref _masterVolume, value);
        }

        /// <summary>
        /// Enable frequency filter by default
        /// </summary>
        public bool EnableFrequencyFilterByDefault
        {
            get => _enableFrequencyFilterByDefault;
            set => SetProperty(ref _enableFrequencyFilterByDefault, value);
        }

        /// <summary>
        /// Enable debug logging
        /// </summary>
        public bool EnableDebugLogging
        {
            get => _enableDebugLogging;
            set => SetProperty(ref _enableDebugLogging, value);
        }

        /// <summary>
        /// Audio activity threshold (0-32767)
        /// </summary>
        public int AudioActivityThreshold
        {
            get => _audioActivityThreshold;
            set => SetProperty(ref _audioActivityThreshold, value);
        }

        /// <summary>
        /// Minimum activity duration in milliseconds
        /// </summary>
        public int AudioActivityMinDuration
        {
            get => _audioActivityMinDuration;
            set => SetProperty(ref _audioActivityMinDuration, value);
        }

        /// <summary>
        /// Theme file name
        /// </summary>
        public string ThemeFile
        {
            get => _themeFile;
            set => SetProperty(ref _themeFile, value);
        }
        
        /// <summary>
        /// Enable/disable Automatic Gain Control
        /// </summary>
        public bool AGCEnabled
        {
            get => _agcEnabled;
            set => SetProperty(ref _agcEnabled, value);
        }
        
        /// <summary>
        /// AGC target RMS level in dB (-30 to -10)
        /// </summary>
        public double AGCTargetDB
        {
            get => _agcTargetDB;
            set => SetProperty(ref _agcTargetDB, value);
        }
        
        /// <summary>
        /// AGC maximum boost in dB (0 to +30)
        /// </summary>
        public double AGCMaxBoostDB
        {
            get => _agcMaxBoostDB;
            set => SetProperty(ref _agcMaxBoostDB, value);
        }
        
        /// <summary>
        /// AGC maximum cut in dB (-20 to 0)
        /// </summary>
        public double AGCMaxCutDB
        {
            get => _agcMaxCutDB;
            set => SetProperty(ref _agcMaxCutDB, value);
        }
        
        /// <summary>
        /// Use dB scale for amplitude visualization (true) or linear amplitude scale 0-1 (false)
        /// </summary>
        public bool UseDbScale
        {
            get => _useDbScale;
            set => SetProperty(ref _useDbScale, value);
        }

        /// <summary>
        /// Path to configuration file (read-only)
        /// </summary>
        public string ConfigFilePath
        {
            get
            {
                var basePath = PlayerSettingsStore.Path;
                var fileName = PlayerSettingsStore.Instance.ConfigFileName;
                return Path.Combine(basePath, fileName);
            }
        }

        public SettingsViewModel()
        {
            _masterVolume = 100;
            _enableFrequencyFilterByDefault = false;
            _enableDebugLogging = true;
            _audioActivityThreshold = 500;
            _audioActivityMinDuration = 100;
            _themeFile = "light.json";
            
            // AGC Defaults
            _agcEnabled = true;
            _agcTargetDB = Constants.AGC_TARGET_DB;
            _agcMaxBoostDB = Constants.AGC_MAX_BOOST_DB;
            _agcMaxCutDB = Constants.AGC_MAX_CUT_DB;
            
            // Visualization Defaults
            _useDbScale = false; // Linear amplitude scale by default
        }

        /// <summary>
        /// Load settings from PlayerSettingsStore
        /// </summary>
        public void LoadSettings()
        {
            var store = PlayerSettingsStore.Instance;

            MasterVolume = store.GetDefaultMasterVolume();
            EnableFrequencyFilterByDefault = store.GetPlayerSettingBool(PlayerSettingKeys.EnableFrequencyFilterByDefault);
            EnableDebugLogging = store.GetDefaultDebugLogging();
            AudioActivityThreshold = store.GetDefaultAudioActivityThreshold();
            AudioActivityMinDuration = store.GetDefaultAudioActivityMinDuration();
            ThemeFile = store.GetPlayerSettingString(PlayerSettingKeys.ThemeFile);
            
            // Load AGC settings
            AGCEnabled = store.GetAGCEnabled();
            AGCTargetDB = store.GetAGCTargetDB();
            AGCMaxBoostDB = store.GetAGCMaxBoostDB();
            AGCMaxCutDB = store.GetAGCMaxCutDB();
            
            // Load Visualization settings
            UseDbScale = store.GetUseDbScale();
        }

        /// <summary>
        /// Save settings to PlayerSettingsStore
        /// </summary>
        public void SaveSettings()
        {
            var store = PlayerSettingsStore.Instance;

            store.SetPlayerSetting(PlayerSettingKeys.MasterVolume, MasterVolume);
            store.SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, EnableFrequencyFilterByDefault);
            store.SetPlayerSetting(PlayerSettingKeys.EnableDebugLogging, EnableDebugLogging);
            store.SetPlayerSetting(PlayerSettingKeys.AudioActivityThreshold, AudioActivityThreshold);
            store.SetPlayerSetting(PlayerSettingKeys.AudioActivityMinDuration, AudioActivityMinDuration);
            store.SetPlayerSetting(PlayerSettingKeys.ThemeFile, ThemeFile);
            
            // Save AGC settings
            store.SaveAGCSettings(AGCTargetDB, AGCMaxBoostDB, AGCMaxCutDB, AGCEnabled);
            
            // Save Visualization settings
            store.SetUseDbScale(UseDbScale);
        }

        /// <summary>
        /// Reset all settings to default values
        /// </summary>
        public void ResetToDefaults()
        {
            MasterVolume = 100;
            EnableFrequencyFilterByDefault = false;
            EnableDebugLogging = true;
            AudioActivityThreshold = 500;
            AudioActivityMinDuration = 100;
            ThemeFile = "light.json";
            
            // Reset AGC to defaults
            ResetAGCToDefaults();
            
            // Reset Visualization settings to defaults
            UseDbScale = false; // Linear amplitude scale by default
        }
        
        /// <summary>
        /// Reset AGC settings to default values
        /// </summary>
        public void ResetAGCToDefaults()
        {
            AGCEnabled = true;
            AGCTargetDB = Constants.AGC_TARGET_DB;
            AGCMaxBoostDB = Constants.AGC_MAX_BOOST_DB;
            AGCMaxCutDB = Constants.AGC_MAX_CUT_DB;
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
