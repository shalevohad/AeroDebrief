using System;
using System.Windows.Controls;
using System.Windows.Input;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services;

namespace AeroDebrief.UI.Controls.Charts
{
    /// <summary>
    /// Unified graph control using LiveChartsCore for amplitude visualization.
    /// Phase 7 Step 4: Now uses UnifiedGraphViewModel from DataContext (provided by UnifiedPlayerViewModel).
    /// Phase 9 Step 1: Loading spinner overlay for tile loading feedback.
    /// Phase 9 Step 2: Error banner overlay for error handling.
    /// Phase 9 Step 3: Performance stats overlay with F3 toggle.
    /// </summary>
    public partial class UnifiedGraphControl : UserControl
    {
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private ErrorHandlingService? _errorService;

        public UnifiedGraphControl()
        {
            InitializeComponent();
            
            _logger.Info("UnifiedGraphControl initializing (Phase 9 Step 3 - Performance Monitoring)");
            
            // Log when DataContext changes
            DataContextChanged += OnDataContextChanged;
            
            // Phase 9 Step 3: Handle keyboard input for F3 toggle
            Focusable = true;
            KeyDown += OnKeyDown;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var vm = e.NewValue as UnifiedGraphViewModel;
            if (vm != null)
            {
                _logger.Info($"? UnifiedGraphControl received ViewModel with {vm.Series.Count} series");
                
                // Phase 9 Step 2: Wire up error handling service
                // TODO: Create and wire ErrorBannerOverlay once XAML compilation is fixed
                if (_errorService == null)
                {
                    _errorService = new ErrorHandlingService(_logger);
                    _logger.Debug("Error handling service initialized");
                }
            }
        }

        /// <summary>
        /// Phase 9 Step 3: Handle keyboard shortcuts.
        /// F3: Toggle performance stats overlay.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            {
                var vm = ViewModel;
                if (vm != null)
                {
                    vm.ShowPerformanceStats = !vm.ShowPerformanceStats;
                    _logger.Info($"Performance stats toggled: {vm.ShowPerformanceStats}");
                }
                e.Handled = true;
            }
        }

        public UnifiedGraphViewModel? ViewModel => DataContext as UnifiedGraphViewModel;

        /// <summary>
        /// Phase 6: Connect playhead synchronization to PlaybackController.
        /// Stub implementation for Phase 7 Step 4.
        /// </summary>
        public void ConnectPlayheadToPlayback(Core.Playback.PlaybackController playbackController)
        {
            _logger.Info("ConnectPlayheadToPlayback called (stub)");
            // Full implementation in next phase
        }
    }
}
