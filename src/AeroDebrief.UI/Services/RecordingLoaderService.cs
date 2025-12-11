using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage;
using AeroDebrief.UI.Windows;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Service for loading recording files with progress dialog.
    /// Handles ADB conversion, CVR decompression, and direct DB opening.
    /// </summary>
    public class RecordingLoaderService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Load a recording file with progress dialog.
        /// Shows modal progress window during ADB conversion.
        /// </summary>
        /// <param name="filePath">Path to the recording file (.cvr, .adb, .db)</param>
        /// <param name="owner">Owner window for modal dialog</param>
        /// <returns>Tuple of (UnitOfWork, TempPath) or null if cancelled/failed</returns>
        public static async Task<(IUnitOfWork? UnitOfWork, string? TempPath)?> LoadWithProgressAsync(
            string filePath, 
            Window? owner = null)
        {
            try
            {
                Logger.Info("=== LoadWithProgressAsync START ===");
                Logger.Info($"File: {filePath}");
                
                // Check if this is an ADB file (needs conversion with progress)
                var isAdb = CvrFormat.IsAdbFile(filePath);
                var isCvr = CvrFormat.IsCvrFile(filePath);
                
                Logger.Info($"IsAdb: {isAdb}, IsCvr: {isCvr}");
                
                if (!isAdb && !isCvr)
                {
                    // Direct DB file - no progress needed
                    Logger.Info($"Opening DB file directly: {filePath}");
                    return await RecordingFileLoader.OpenAsync(filePath);
                }
                
                Logger.Info("Creating progress window...");
                
                // For ADB or CVR, show progress dialog
                ConversionProgressWindow? progressWindow = null;
                CancellationTokenSource? cts = null;
                IUnitOfWork? uow = null;
                string? tempPath = null;
                Exception? loadException = null;
                
                // Create progress window on UI thread
                progressWindow = new ConversionProgressWindow
                {
                    Owner = owner ?? Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                Logger.Info("Progress window created");
                
                progressWindow.SetFileName(filePath);
                
                cts = new CancellationTokenSource();
                progressWindow.SetCancellationTokenSource(cts);
                
                // CRITICAL: Show the window first and force it to render
                Logger.Info("Showing progress window (non-blocking first)...");
                progressWindow.Show();
                progressWindow.Activate();
                
                // Force the window to render by processing pending UI messages
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(50); // Small delay to ensure window is visible
                
                Logger.Info("Starting background load task...");
                
                // Start loading in background task AFTER showing dialog
                var loadTask = Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info("Background task started");
                        
                        // Create progress reporter that updates UI
                        var progress = new Progress<ConversionProgress>(p =>
                        {
                            Logger.Debug($"Progress update: {p.Stage} - {p.Percent}%");
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                progressWindow?.UpdateProgress(p);
                            });
                        });
                        
                        // Load the file with progress
                        var result = await RecordingFileLoader.OpenAsync(
                            filePath,
                            progress: null,
                            detailedProgress: progress,
                            ct: cts.Token);
                        
                        uow = result.UnitOfWork;
                        tempPath = result.TempPath;
                        
                        Logger.Info($"Recording loaded successfully: {filePath}");
                        
                        // Close the dialog on success
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Logger.Info("Calling Complete(true)");
                            progressWindow?.Complete(true);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info("Recording load cancelled by user");
                        
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow?.Close();
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to load recording");
                        loadException = ex;
                        
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow?.ShowError(ex.Message);
                        });
                    }
                }, cts.Token);
                
                Logger.Info("Waiting for load task to complete...");
                
                // Wait for the task to complete (window is already visible)
                await loadTask;
                
                Logger.Info($"Load task completed");
                
                // Close the window if still open
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (progressWindow?.IsLoaded == true && progressWindow.IsVisible)
                    {
                        progressWindow.Close();
                    }
                });
                
                cts?.Dispose();
                
                // Handle result
                if (loadException != null)
                {
                    MessageBox.Show(
                        $"Failed to load recording:\n\n{loadException.Message}",
                        "Load Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    return null;
                }
                
                if (uow == null)
                {
                    Logger.Warn("Load completed but UnitOfWork is null");
                    return null;
                }
                
                return (uow, tempPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error in RecordingLoaderService");
                
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                return null;
            }
        }
        
        /// <summary>
        /// Simple overload that returns just the UnitOfWork (temp path handled internally)
        /// </summary>
        public static async Task<IUnitOfWork?> LoadAsync(string filePath, Window? owner = null)
        {
            var result = await LoadWithProgressAsync(filePath, owner);
            return result?.UnitOfWork;
        }
    }
}
