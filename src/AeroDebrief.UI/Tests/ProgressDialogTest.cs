using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AeroDebrief.Core.Storage;
using AeroDebrief.UI.Windows;

namespace AeroDebrief.UI.Tests
{
    /// <summary>
    /// Simple test to verify ConversionProgressWindow shows correctly
    /// </summary>
    public class ProgressDialogTest
    {
        /// <summary>
        /// Test method - call this from a button click or menu item
        /// </summary>
        public static async Task TestProgressDialog()
        {
            var window = new ConversionProgressWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true
            };
            
            window.SetFileName("test_recording.adb");
            
            var cts = new CancellationTokenSource();
            window.SetCancellationTokenSource(cts);
            
            // Simulate conversion progress in background
            _ = Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    await Task.Delay(200); // Simulate work
                    
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        window.UpdateProgress(new ConversionProgress
                        {
                            Stage = "Converting packets",
                            Percent = i,
                            PacketsProcessed = i * 1000,
                            PacketsPerSecond = 2500,
                            EstimatedTimeRemaining = TimeSpan.FromSeconds((100 - i) / 5),
                            Message = $"Processing: {i * 1000:N0} packets (2,500 pkt/sec)"
                        });
                    });
                    
                    if (cts.Token.IsCancellationRequested)
                        break;
                }
                
                // Close dialog
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    window.Complete(true);
                });
            });
            
            // Show dialog (blocks until closed)
            var result = window.ShowDialog();
            
            MessageBox.Show($"Dialog closed with result: {result}", "Test Result");
        }
    }
}
