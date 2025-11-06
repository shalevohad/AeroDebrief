using System.Windows;
using System.Windows.Controls;

namespace AeroDebrief.UI.Controls.Tacview
{
    /// <summary>
    /// Interaction logic for TacviewStatusControl.xaml
    /// Displays Tacview integration status, sync quality, pilot selection, and configuration
    /// </summary>
    public partial class TacviewStatusControl : UserControl
    {
        public TacviewStatusControl()
        {
            InitializeComponent();
        }
        
        private void OnDetailsButtonClick(object sender, RoutedEventArgs e)
        {
            // Toggle the details popup
            DetailsPopup.IsOpen = !DetailsPopup.IsOpen;
        }
    }
}
