using System;
using System.IO;
using System.Threading;
using System.Windows;
using AeroDebrief.Core.Storage;
using NLog;

namespace AeroDebrief.UI.Windows
{
    /// <summary>
    /// Modal progress dialog for ADB file conversion.
    /// Shows detailed progress with percentage, speed, and ETA.
    /// </summary>
    public partial class ConversionProgressWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _canCancel = true;

        public ConversionProgressWindow()
        {
            InitializeComponent();
            Logger.Debug("ConversionProgressWindow initialized");
        }

        /// <summary>
        /// Set the file being converted (displays filename only)
        /// </summary>
        public void SetFileName(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            FileNameText.Text = $"Converting: {fileName}";
            Logger.Debug($"Conversion dialog: {fileName}");
        }

        /// <summary>
        /// Update progress from ConversionProgress
        /// </summary>
        public void UpdateProgress(ConversionProgress progress)
        {
            try
            {
                // Main stage message
                StageText.Text = progress.Stage;
                
                // Detailed message with packet count
                if (!string.IsNullOrEmpty(progress.Message))
                {
                    DetailText.Text = progress.Message;
                }
                else if (progress.PacketsProcessed > 0)
                {
                    DetailText.Text = $"Processed: {progress.PacketsProcessed:N0} packets";
                }
                else
                {
                    DetailText.Text = progress.Stage;
                }
                
                // Progress bar
                ProgressBar.Value = progress.Percent;
                ProgressBar.IsIndeterminate = progress.Percent == 0 && string.IsNullOrEmpty(progress.Message);
                
                // Percentage text
                PercentText.Text = $"{progress.Percent}%";
                
                // Speed
                if (progress.PacketsPerSecond > 0)
                {
                    SpeedText.Text = $"Speed: {progress.PacketsPerSecond:N0} pkt/sec";
                }
                else
                {
                    SpeedText.Text = "Speed: --";
                }
                
                // ETA
                if (progress.EstimatedTimeRemaining.HasValue)
                {
                    var eta = progress.EstimatedTimeRemaining.Value;
                    if (eta.TotalHours >= 1)
                    {
                        EtaText.Text = $"ETA: {eta:hh\\:mm\\:ss}";
                    }
                    else
                    {
                        EtaText.Text = $"ETA: {eta:mm\\:ss}";
                    }
                }
                else
                {
                    EtaText.Text = "ETA: --:--";
                }
                
                // Disable cancel when finalizing
                if (progress.Percent >= 85)
                {
                    _canCancel = false;
                    CancelButton.IsEnabled = false;
                    CancelButton.Content = "Please wait...";
                }
                
                // Show info panel for long conversions
                if (progress.PacketsProcessed > 50000 && InfoPanel.Visibility == Visibility.Collapsed)
                {
                    InfoPanel.Visibility = Visibility.Visible;
                    InfoText.Text = "Large file detected. This may take 10-15 minutes with amplitude cache enabled.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating conversion progress UI");
            }
        }

        /// <summary>
        /// Set cancellation token source (allows cancellation)
        /// </summary>
        public void SetCancellationTokenSource(CancellationTokenSource cts)
        {
            _cancellationTokenSource = cts;
            CancelButton.IsEnabled = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_canCancel)
            {
                Logger.Debug("Cancel not allowed during finalization");
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to cancel the conversion?\n\nThe partially converted file will be deleted.",
                "Cancel Conversion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Logger.Info("User cancelled conversion");
                CancelButton.IsEnabled = false;
                CancelButton.Content = "Cancelling...";
                _cancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        /// Complete the conversion (auto-closes after brief delay)
        /// </summary>
        public async void Complete(bool success)
        {
            try
            {
                if (success)
                {
                    ProgressBar.Value = 100;
                    PercentText.Text = "100%";
                    StageText.Text = "Conversion Complete!";
                    DetailText.Text = "File ready for playback";
                    CancelButton.Content = "Close";
                    CancelButton.IsEnabled = true;
                    
                    // Auto-close after 1 second
                    await System.Threading.Tasks.Task.Delay(1000);
                    DialogResult = true;
                }
                else
                {
                    StageText.Text = "Conversion Failed";
                    DetailText.Text = "An error occurred during conversion";
                    CancelButton.Content = "Close";
                    CancelButton.IsEnabled = true;
                    DialogResult = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error completing conversion dialog");
                DialogResult = false;
            }
        }

        /// <summary>
        /// Show error message
        /// </summary>
        public void ShowError(string message)
        {
            StageText.Text = "Conversion Failed";
            DetailText.Text = message;
            InfoPanel.Visibility = Visibility.Visible;
            InfoText.Text = "Please check the log file for details.";
            CancelButton.Content = "Close";
            CancelButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
        }
    }
}
