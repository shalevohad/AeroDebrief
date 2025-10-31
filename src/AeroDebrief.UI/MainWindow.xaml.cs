using System.Windows;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize the ViewModel for the UnifiedPlayerControl
            var viewModel = new UnifiedPlayerViewModel();
            UnifiedPlayer.DataContext = viewModel;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
