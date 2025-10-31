using System.Windows;

namespace AeroDebrief.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Always show MainWindow (production UI) in both Debug and Release
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}