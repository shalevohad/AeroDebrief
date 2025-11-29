using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using NLog;
using SharpConfig;
using AeroDebrief.Core;
using AeroDebrief.Core.Helpers;

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
        RecentRecordingFiles,  // Phase 2.5: JSON array of recent recording files
        EnableFrequencyFilterByDefault,
        ThemeFile,
        
        // Audio Mixing Settings (AGC)
        AGC_TargetDB,
        AGC_MaxBoostDB,
        AGC_MaxCutDB,
        AGC_Enabled,
        
        // Visualization Settings
        UseDbScale,  // Use dB scale (true) or linear amplitude scale (false) for graphs
        
        // Cache Settings
        TempCacheExpirationDays,  // Number of days before cached temp files are automatically deleted
        
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
            { PlayerSettingKeys.RecentRecordingFiles.ToString(), "[]" }, // Default to empty JSON array
            { PlayerSettingKeys.EnableFrequencyFilterByDefault.ToString(), "false" },
            { PlayerSettingKeys.ThemeFile.ToString(), "light.json" },
            
            // Audio Mixing Settings (AGC) - Default values from Constants
            { PlayerSettingKeys.AGC_TargetDB.ToString(), "-20.0" },      // Target RMS level in dB
            { PlayerSettingKeys.AGC_MaxBoostDB.ToString(), "20.0" },     // Max boost in dB
            { PlayerSettingKeys.AGC_MaxCutDB.ToString(), "-10.0" },      // Max cut in dB
            { PlayerSettingKeys.AGC_Enabled.ToString(), "true" },        // AGC enabled by default
            
            // Visualization Settings
            { PlayerSettingKeys.UseDbScale.ToString(), "false" },        // Use linear amplitude scale by default
            
            // Cache Settings
            { PlayerSettingKeys.TempCacheExpirationDays.ToString(), "30" }, // Keep cached files for 30 days by default
            
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
            // Check for command-line override first
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
                if (arg.Trim().StartsWith("-playercfg="))
                {
                    Path = arg.Trim().Replace("-playercfg=", "").Trim();
                    if (!Path.EndsWith("\\")) Path = Path + "\\";
                    Logger.Info($"Found -playercfg loading: {Path + ConfigFileName}");
                }

            // If no command-line override, use configs folder in application directory
            if (string.IsNullOrEmpty(Path))
            {
                Path = System.IO.Path.Combine(AppContext.BaseDirectory, Constants.CONFIG_FOLDER);
                if (!Directory.Exists(Path))
                {
                    Directory.CreateDirectory(Path);
                    Logger.Info($"Created configs directory: {Path}");
                }
                Path = Path + System.IO.Path.DirectorySeparatorChar;
            }

            try
            {
                var configPath = Path + ConfigFileName;
                
                // Use centralized file locking helper
                if (!FileHelpers.WaitForFileUnlock(configPath, maxWaitMs: 2000, checkIntervalMs: 200))
                {
                    Logger.Warn($"Config file {configPath} remained locked after waiting");
                }

                _configuration = Configuration.LoadFromFile(configPath);
                Logger.Info($"Loaded player config from {configPath}");
                
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
            SetPlayerSetting(PlayerSettingKeys.RecentRecordingFiles, defaultPlayerSettings[PlayerSettingKeys.RecentRecordingFiles.ToString()]); // Parse JSON array
            SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, bool.Parse(defaultPlayerSettings[PlayerSettingKeys.EnableFrequencyFilterByDefault.ToString()]));
            // Theme default
            SetPlayerSetting(PlayerSettingKeys.ThemeFile, defaultPlayerSettings[PlayerSettingKeys.ThemeFile.ToString()]);
            
            // Audio Mixing defaults (AGC)
            SetPlayerSetting(PlayerSettingKeys.AGC_TargetDB, double.Parse(defaultPlayerSettings[PlayerSettingKeys.AGC_TargetDB.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.AGC_MaxBoostDB, double.Parse(defaultPlayerSettings[PlayerSettingKeys.AGC_MaxBoostDB.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.AGC_MaxCutDB, double.Parse(defaultPlayerSettings[PlayerSettingKeys.AGC_MaxCutDB.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.AGC_Enabled, bool.Parse(defaultPlayerSettings[PlayerSettingKeys.AGC_Enabled.ToString()]));
            
            // Visualization defaults
            SetPlayerSetting(PlayerSettingKeys.UseDbScale, bool.Parse(defaultPlayerSettings[PlayerSettingKeys.UseDbScale.ToString()]));
            
            // Window defaults
            SetPlayerSetting(PlayerSettingKeys.WindowWidth, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowWidth.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowHeight, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowHeight.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowX, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowX.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.WindowY, int.Parse(defaultPlayerSettings[PlayerSettingKeys.WindowY.ToString()]));
            SetPlayerSetting(PlayerSettingKeys.SelectedTab, int.Parse(defaultPlayerSettings[PlayerSettingKeys.SelectedTab.ToString()]));
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
        
        /// <summary>
        /// Get AGC target RMS level in dB
        /// </summary>
        public double GetAGCTargetDB() => 
            GetPlayerSettingDouble(PlayerSettingKeys.AGC_TargetDB);
        
        /// <summary>
        /// Get AGC maximum boost in dB
        /// </summary>
        public double GetAGCMaxBoostDB() => 
            GetPlayerSettingDouble(PlayerSettingKeys.AGC_MaxBoostDB);
        
        /// <summary>
        /// Get AGC maximum cut in dB
        /// </summary>
        public double GetAGCMaxCutDB() => 
            GetPlayerSettingDouble(PlayerSettingKeys.AGC_MaxCutDB);
        
        /// <summary>
        /// Get whether AGC is enabled
        /// </summary>
        public bool GetAGCEnabled() => 
            GetPlayerSettingBool(PlayerSettingKeys.AGC_Enabled);
        
        /// <summary>
        /// Save AGC settings
        /// </summary>
        public void SaveAGCSettings(double targetDB, double maxBoostDB, double maxCutDB, bool enabled)
        {
            SetPlayerSetting(PlayerSettingKeys.AGC_TargetDB, targetDB);
            SetPlayerSetting(PlayerSettingKeys.AGC_MaxBoostDB, maxBoostDB);
            SetPlayerSetting(PlayerSettingKeys.AGC_MaxCutDB, maxCutDB);
            SetPlayerSetting(PlayerSettingKeys.AGC_Enabled, enabled);
        }
        
        /// <summary>
        /// Get whether to use dB scale for amplitude visualization (true) or linear amplitude 0-1 (false)
        /// </summary>
        public bool GetUseDbScale() => 
            GetPlayerSettingBool(PlayerSettingKeys.UseDbScale);
        
        /// <summary>
        /// Set whether to use dB scale for amplitude visualization
        /// </summary>
        public void SetUseDbScale(bool useDbScale)
        {
            SetPlayerSetting(PlayerSettingKeys.UseDbScale, useDbScale);
        }
    }
}