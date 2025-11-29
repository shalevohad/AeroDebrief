using System.Windows.Controls;

namespace AeroDebrief.UI.Controls.Charts
{
    /// <summary>
    /// Phase 9 Step 1: Loading spinner overlay for tile-based data loading.
    /// Shows visual feedback when tiles are being loaded, with status text and cancel option.
    /// </summary>
    public partial class LoadingSpinnerOverlay : UserControl
    {
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public LoadingSpinnerOverlay()
        {
            InitializeComponent();
            _logger.Debug("LoadingSpinnerOverlay initialized");
        }
    }
}
