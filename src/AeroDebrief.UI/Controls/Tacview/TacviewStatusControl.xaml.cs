using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AeroDebrief.UI.Controls.Tacview
{
    /// <summary>
    /// Interaction logic for TacviewStatusControl.xaml
    /// Displays Tacview integration status, sync quality, pilot selection, and configuration
    /// Compact by default, showing only icon, status, and badges. Details popup opens on demand.
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
        
        private void OnDetailsPopupOpened(object? sender, EventArgs e)
        {
            // Rotate the button icon to indicate expanded state
            AnimateButtonIcon(90);
        }
        
        private void OnDetailsPopupClosed(object? sender, EventArgs e)
        {
            // Rotate the button icon back to indicate collapsed state
            AnimateButtonIcon(0);
        }
        
        private void AnimateButtonIcon(double targetAngle)
        {
            var rotateTransform = (DetailsButtonIcon.RenderTransform as System.Windows.Media.RotateTransform);
            if (rotateTransform != null)
            {
                var animation = new DoubleAnimation
                {
                    To = targetAngle,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                rotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
            }
        }
    }
}
