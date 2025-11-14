using System.Windows.Controls;
using System.Windows.Input;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Panel for SRS server connection and recording controls
    /// </summary>
    public partial class ServerSourcePanel : UserControl
    {
        public ServerSourcePanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles double-click on bookmark to connect to server
        /// </summary>
        private void BookmarksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ServerSourceViewModel viewModel && viewModel.SelectedBookmark != null)
            {
                // Load the bookmark (which sets IP, Port, and Name)
                // This is already handled by SelectedBookmark setter in the ViewModel
                
                // Connect to the server if not already connected
                if (viewModel.ConnectCommand?.CanExecute(null) == true)
                {
                    viewModel.ConnectCommand.Execute(null);
                }
            }
        }
    }
}
