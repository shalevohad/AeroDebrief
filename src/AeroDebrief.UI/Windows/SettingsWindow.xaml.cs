using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AeroDebrief.Core.Settings;
using AeroDebrief.Core.Storage;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Windows
{
    /// <summary>
    /// Settings window for configuring AeroDebrief Player preferences
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private bool _hasUnsavedChanges = false;

        public SettingsWindow()
        {
            InitializeComponent();
            
            _viewModel = new SettingsViewModel();
            DataContext = _viewModel;
            
            // Load settings
            _viewModel.LoadSettings();
            
            // Track changes
            _viewModel.PropertyChanged += (s, e) => _hasUnsavedChanges = true;
        }

        private void CategoryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Ignore event if controls haven't been initialized yet
            if (GeneralPanel == null) return;
            
            if (CategoryList.SelectedIndex < 0) return;

            // Hide all panels
            GeneralPanel.Visibility = Visibility.Collapsed;
            AudioPanel.Visibility = Visibility.Collapsed;
            AppearancePanel.Visibility = Visibility.Collapsed;
            AdvancedPanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            switch (CategoryList.SelectedIndex)
            {
                case 0:
                    GeneralPanel.Visibility = Visibility.Visible;
                    break;
                case 1:
                    AudioPanel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    AppearancePanel.Visibility = Visibility.Visible;
                    break;
                case 3:
                    AdvancedPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveSettings();
            _hasUnsavedChanges = false;
            MessageBox.Show("Settings saved successfully!", "Settings", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveSettings();
            _hasUnsavedChanges = false;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            DialogResult = false;
            Close();
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configPath = _viewModel.ConfigFilePath;
                var directory = Path.GetDirectoryName(configPath);
                
                if (Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", directory);
                }
                else
                {
                    MessageBox.Show($"Configuration directory not found: {directory}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open configuration folder: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?\n\n" +
                "This action cannot be undone.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ResetToDefaults();
                _viewModel.SaveSettings();
                _hasUnsavedChanges = false;
                
                MessageBox.Show(
                    "Settings have been reset to defaults.\n\n" +
                    "Please restart the application for all changes to take effect.",
                    "Settings Reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        
        private void ResetAGC_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset Automatic Gain Control settings to default values?\n\n" +
                $"Target Level: {Core.Constants.AGC_TARGET_DB:F1} dB\n" +
                $"Max Boost: +{Core.Constants.AGC_MAX_BOOST_DB:F1} dB\n" +
                $"Max Cut: {Core.Constants.AGC_MAX_CUT_DB:F1} dB",
                "Reset AGC Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ResetAGCToDefaults();
                _hasUnsavedChanges = true;
                
                MessageBox.Show(
                    "AGC settings have been reset to defaults.\n\n" +
                    "Click 'Apply' or 'OK' to save changes.",
                    "AGC Reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will delete all cached decompressed/converted recording files from the temp directory.\n\n" +
                "These files will be recreated automatically when you open recordings again.\n\n" +
                "Do you want to continue?",
                "Clear Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    RecordingFileLoader.ClearCache();
                    MessageBox.Show(
                        "Cache has been cleared successfully.",
                        "Cache Cleared",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to clear cache:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges && DialogResult != true)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
    }
}
