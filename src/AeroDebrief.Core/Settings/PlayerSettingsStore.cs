using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using NLog;
using SharpConfig;

namespace AeroDebrief.Core.Settings
{
    public enum PlayerSettingKeys
    {
        // File Analysis Settings
        AudioActivityThreshold,
        AudioActivityMinDuration,
        LastAnalysisFile,
        
        // Player Settings
        MasterVolume,
        EnableDebugLogging,
        LastRecordingFile,
        EnableFrequencyFilterByDefault,
        ThemeFile,
        
        // Window Settings
        WindowWidth,
        WindowHeight,
        WindowX,
        WindowY,
        SelectedTab
    }

    public class PlayerSettingsStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string CFG_FILE_NAME = "player.cfg";
        private static readonly object _lock = new();
        private static PlayerSettingsStore? _instance;
        private Configuration _configuration;
        private readonly ConcurrentDictionary<string, object> _settingsCache = new();

        private readonly Dictionary<string, string> defaultPlayerSettings = new()
        {
            // File Analysis Settings
            { PlayerSettingKeys.AudioActivityThreshold.ToString(), "500" },
            { PlayerSettingKeys.AudioActivityMinDuration.ToString(), "300" },
            { PlayerSettingKeys.LastAnalysisFile.ToString(), "" },
            
            // Player Settings
            { PlayerSettingKeys.MasterVolume.ToString(), "100" },
            { PlayerSettingKeys.EnableDebugLogging.ToString(), "true" },
            { PlayerSettingKeys.LastRecordingFile.ToString(), "" },
            { PlayerSettingKeys.EnableFrequencyFilterByDefault.ToString(), "false" },
            { PlayerSettingKeys.ThemeFile.ToString(), "light.json" },
            
            // Window Settings
            { PlayerSettingKeys.WindowWidth.ToString(), "950" },
            { PlayerSettingKeys.WindowHeight.ToString(), "750" },
            { PlayerSettingKeys.WindowX.ToString(), "-1" }, // -1 means center
            { PlayerSettingKeys.WindowY.ToString(), "-1" }, // -1 means center
            { PlayerSettingKeys.SelectedTab.ToString(), "0" } // 0 = Player tab
        };

        public string ConfigFileName { get; } = CFG_FILE_NAME;
        public static string Path { get; set; } = "";

        public static PlayerSettingsStore Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PlayerSettingsStore();
                return _instance;
            }
        }

        private PlayerSettingsStore()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
                if (arg.Trim().StartsWith("-playercfg="))
                {
                    Path = arg.Trim().Replace("-playercfg=", "").Trim();
                    if (!Path.EndsWith("\\")) Path = Path + "\\";
                    Logger.Info($"Found -playercfg loading: {Path + ConfigFileName}");
                }

            try
            {
                var count = 0;
                while (IsFileLocked(new FileInfo(Path + ConfigFileName)) && count < 10)
                {
                    Logger.Warn($"Config file {Path + ConfigFileName} is locked. Waiting...");
                    Thread.Sleep(200);
                    count++;
                }

                _configuration = Configuration.LoadFromFile(Path + ConfigFileName);
                Logger.Info($"Loaded player config from {Path + ConfigFileName}");
                
                // Validate the loaded configuration
                ValidateConfiguration();
            }
            catch (FileNotFoundException)
            {
                Logger.Info($"Did not find player config file at path {Path}{ConfigFileName}, initializing with default config");
                CreateDefaultConfiguration();
            }
            catch (ParserException ex)
            {
                Logger.Error(ex, "Failed to parse player config, potentially corrupted. Creating backup and re-initializing with default config");
                HandleCorruptedConfig();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error loading player config. Re-initializing with default config");
                HandleCorruptedConfig();
            }
        }

        private void CreateDefaultConfiguration()
        {
            _configuration = new Configuration
            {
                new Section("Player Settings")
            };
            InitializeDefaultSettings();
            Save();
        }

        private void HandleCorruptedConfig()
        {
            try
            {
                File.Copy(Path + ConfigFileName, Path + ConfigFileName + ".bak", true);
                Logger.Info($"Backup of corrupted config file created at {Path + ConfigFileName}.bak");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
            }

            CreateDefaultConfiguration();
        }

        private void ValidateConfiguration()
        {
            try
            {
                // Check if the configuration has any obvious issues
                if (_configuration == null)
                {
                    Logger.Warn("Configuration is null, reinitializing");
                    CreateDefaultConfiguration();
                    return;
                }

                // Ensure the Player Settings section exists
                if (!_configuration.Contains("Player Settings"))
                {
                    Logger.Info("Player Settings section missing, adding it");
                    _configuration.Add(new Section("Player Settings"));
                }

                // Test access to a few key settings to detect corruption
                var testSection = _configuration["Player Settings"];
                if (testSection != null)
                {
                    // Try to access some settings to trigger any enum-related errors
                    foreach (var key in Enum.GetValues<PlayerSettingKeys>())
                    {
                        if (testSection.Contains(key.ToString()))
                        {
                            var setting = testSection[key.ToString()];
                            // Just accessing the RawValue should be safe
                            _ = setting.RawValue;
                        }
                    }
                }

                Logger.Debug("Configuration validation completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Configuration validation failed, reinitializing with defaults");
                HandleCorruptedConfig();
            }
        }

        private void InitializeDefaultSettings()
        {
            // File Analysis defaults
            SetPlayerSetting(PlayerSettingKeys.AudioActivityThreshold, int.Parse(defaultPlayerSettings[PlayerSettingKeys.AudioActivityThreshold.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.AudioActivityMinDuration, int.Parse(defaultPlayerSettings[PlayerSettingKeys.AudioActivityMinDuration.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.LastAnalysisFile, defaultPlayerSettings[PlayerSettingKeys.LastAnalysisFile.ToString()]);
            
            // Player defaults
            SetPlayerSetting(PlayerSettingKeys.MasterVolume, int.Parse(defaultPlayerSettings[PlayerSettingKeys.MasterVolume.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.EnableDebugLogging, bool.Parse(defaultPlayerSettings[PlayerSettingKeys.EnableDebugLogging.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.LastRecordingFile, defaultPlayerSettings[PlayerSettingKeys.LastRecordingFile.ToString()]);
            SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, bool.Parse(defaultPlayerSettings[PlayerSettingKeys.EnableFrequencyFilterByDefault.ToString()]));
            // Theme default
            SetPlayerSetting(PlayerSettingKeys.ThemeFile, defaultPlayerSettings[PlayerSettingKeys.ThemeFile.ToString()]);
            
            // Window defaults
            SetPlayerSetting(PlayerSettingKeys.WindowWidth, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowWidth.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowHeight, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowHeight.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowX, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowX.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowY, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowY.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.SelectedTab, int.Parse(defaultPlayerSettings[PlayerSettingKeys.SelectedTab.ToString()]));
        }

        public static bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists) return false;
            try
            {
                using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        private SharpConfig.Setting GetSetting(string section, string setting)
        {
            try
            {
                if (!_configuration.Contains(section)) _configuration.Add(section);

                if (!_configuration[section].Contains(setting))
                {
                    if (defaultPlayerSettings.ContainsKey(setting))
                    {
                        _configuration[section].Add(new SharpConfig.Setting(setting, defaultPlayerSettings[setting]));
                        Save();
                    }
                    else
                    {
                        _configuration[section].Add(new SharpConfig.Setting(setting, ""));
                        Save();
                    }
                }
                return _configuration[section][setting];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error getting setting '{setting}' from section '{section}'. Using default value.");
                // Return a temporary setting with default value
                var defaultValue = defaultPlayerSettings.ContainsKey(setting) ? defaultPlayerSettings[setting] : "";
                return new SharpConfig.Setting(setting, defaultValue);
            }
        }

        public int GetPlayerSettingInt(PlayerSettingKeys key)
        {
            try
            {
                if (!Enum.IsDefined(typeof(PlayerSettingKeys), key))
                {
                    Logger.Error($"Invalid PlayerSettingKeys enum value: {(int)key}. Using default value.");
                    return 0;
                }

                if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (int)val;
                var setting = GetSetting("Player Settings", key.ToString());
                if (setting.RawValue.Length == 0) return 0;
                _settingsCache[key.ToString()] = setting.IntValue;
                return setting.IntValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error getting integer setting for key '{key}'. Using default value.");
                return 0;
            }
        }

        public double GetPlayerSettingDouble(PlayerSettingKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (double)val;
            var setting = GetSetting("Player Settings", key.ToString());
            if (setting.RawValue.Length == 0) return 0D;
            _settingsCache[key.ToString()] = setting.DoubleValue;
            return setting.DoubleValue;
        }

        public bool GetPlayerSettingBool(PlayerSettingKeys key)
        {
            try
            {
                if (!Enum.IsDefined(typeof(PlayerSettingKeys), key))
                {
                    Logger.Error($"Invalid PlayerSettingKeys enum value: {(int)key}. Using default value.");
                    return false;
                }

                if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (bool)val;
                var setting = GetSetting("Player Settings", key.ToString());
                if (setting.RawValue.Length == 0) return false;
                _settingsCache[key.ToString()] = setting.BoolValue;
                return setting.BoolValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error getting boolean setting for key '{key}'. Using default value.");
                return false;
            }
        }

        public string GetPlayerSettingString(PlayerSettingKeys key)
        {
            try
            {
                if (!Enum.IsDefined(typeof(PlayerSettingKeys), key))
                {
                    Logger.Error($"Invalid PlayerSettingKeys enum value: {(int)key}. Using default value.");
                    return "";
                }

                if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (string)val;
                var setting = GetSetting("Player Settings", key.ToString());
                if (setting.RawValue.Length == 0) return "";
                _settingsCache[key.ToString()] = setting.StringValue;
                return setting.StringValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error getting string setting for key '{key}'. Using default value.");
                return "";
            }
        }

        public void SetPlayerSetting(PlayerSettingKeys key, string value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Player Settings", key.ToString(), value);
        }

        public void SetPlayerSetting(PlayerSettingKeys key, int value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Player Settings", key.ToString(), value);
        }

        public void SetPlayerSetting(PlayerSettingKeys key, double value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Player Settings", key.ToString(), value);
        }

        public void SetPlayerSetting(PlayerSettingKeys key, bool value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Player Settings", key.ToString(), value);
        }

        private void SetSetting(string section, string key, object setting)
        {
            if (setting == null) setting = "";
            if (!_configuration.Contains(section)) _configuration.Add(section);

            if (!_configuration[section].Contains(key))
                _configuration[section].Add(new SharpConfig.Setting(key, setting));
            else
            {
                if (setting is bool)
                    _configuration[section][key].BoolValue = (bool)setting;
                else if (setting is string)
                    _configuration[section][key].StringValue = (string)setting;
                else if (setting is int)
                    _configuration[section][key].IntValue = (int)setting;
                else if (setting is double)
                    _configuration[section][key].DoubleValue = (double)setting;
                else
                    Logger.Error("Unknown Setting Type - Not Saved ");
            }
            Save();
        }

        private void Save()
        {
            lock (_lock)
            {
                try
                {
                    _configuration.SaveToFile(Path + ConfigFileName);
                    Logger.Debug($"Player settings saved to {Path + ConfigFileName}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unable to save player settings!");
                }
            }
        }

        /// <summary>
        /// Save window position and size
        /// </summary>
        public void SaveWindowSettings(int x, int y, int width, int height)
        {
            SetPlayerSetting(PlayerSettingKeys.WindowX, x);
            SetPlayerSetting(PlayerSettingKeys.WindowY, y);
            SetPlayerSetting(PlayerSettingKeys.WindowWidth, width);
            SetPlayerSetting(PlayerSettingKeys.WindowHeight, height);
        }

        /// <summary>
        /// Save currently selected tab index
        /// </summary>
        public void SaveSelectedTab(int tabIndex)
        {
            SetPlayerSetting(PlayerSettingKeys.SelectedTab, tabIndex);
        }

        /// <summary>
        /// Save file analysis settings
        /// </summary>
        public void SaveAnalysisSettings(int threshold, int minDuration)
        {
            SetPlayerSetting(PlayerSettingKeys.AudioActivityThreshold, threshold);
            SetPlayerSetting(PlayerSettingKeys.AudioActivityMinDuration, minDuration);
        }

        /// <summary>
        /// Save the last file that was analyzed
        /// </summary>
        public void SaveLastAnalysisFile(string filePath)
        {
            SetPlayerSetting(PlayerSettingKeys.LastAnalysisFile, filePath);
        }

        /// <summary>
        /// Save the last recording file that was loaded
        /// </summary>
        public void SaveLastRecordingFile(string filePath)
        {
            SetPlayerSetting(PlayerSettingKeys.LastRecordingFile, filePath);
        }

        /// <summary>
        /// Get default audio activity threshold
        /// </summary>
        public int GetDefaultAudioActivityThreshold() => 
            GetPlayerSettingInt(PlayerSettingKeys.AudioActivityThreshold);

        /// <summary>
        /// Get default audio activity minimum duration in milliseconds
        /// </summary>
        public int GetDefaultAudioActivityMinDuration() => 
            GetPlayerSettingInt(PlayerSettingKeys.AudioActivityMinDuration);

        /// <summary>
        /// Get default master volume (0-200)
        /// </summary>
        public int GetDefaultMasterVolume() => 
            GetPlayerSettingInt(PlayerSettingKeys.MasterVolume);

        /// <summary>
        /// Get whether debug logging is enabled by default
        /// </summary>
        public bool GetDefaultDebugLogging() => 
            GetPlayerSettingBool(PlayerSettingKeys.EnableDebugLogging);
    }
}