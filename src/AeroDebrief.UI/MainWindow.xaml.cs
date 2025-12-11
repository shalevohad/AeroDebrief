using System.Windows;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI
{
    public partial class MainWindow : Window
    {
        private UnifiedPlayerViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize the ViewModel for the UnifiedPlayerControl
            _viewModel = new UnifiedPlayerViewModel();
            UnifiedPlayer.DataContext = _viewModel;
        }

        private void OpenRecording_Click(object sender, RoutedEventArgs e)
        {
            // Trigger the FileSourceViewModel's Browse command
            // This will show the file dialog with updated CVR filters
            _viewModel?.FileSource?.BrowseCommand?.Execute(null);
        }
        
        private async void TestProgressDialog_Click(object sender, RoutedEventArgs e)
        {
            // Test the progress dialog with simulated progress
            await Tests.ProgressDialogTest.TestProgressDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
