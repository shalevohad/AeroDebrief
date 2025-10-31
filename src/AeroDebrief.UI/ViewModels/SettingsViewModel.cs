using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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
