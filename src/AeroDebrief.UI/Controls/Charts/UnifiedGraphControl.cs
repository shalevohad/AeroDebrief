using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveChartsCore.SkiaSharpView.WPF;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Visualization.Graphs;

namespace AeroDebrief.UI.Controls.Charts
{
    /// <summary>
    /// Unified graph control using LiveChartsCore for amplitude visualization.
    /// Phase 5.1: Zoom/Pan UI integration with mouse and keyboard controls.
    /// Phase 7 Step 4: Now uses UnifiedGraphViewModel from DataContext (provided by UnifiedPlayerViewModel).
    /// Phase 9 Step 1: Loading spinner overlay for tile loading feedback.
    /// Phase 9 Step 2: Error banner overlay for error handling.
    /// Phase 9 Step 3: Performance stats overlay with F3 toggle.
    /// </summary>
    public partial class UnifiedGraphControl : UserControl
    {
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private ErrorHandlingService? _errorService;

        // Phase 5.1: Pan state tracking
        private bool _isPanning;
        private Point _panStartPoint;
        private DateTime _panStartViewportStart;
        private DateTime _panStartViewportEnd;

        public UnifiedGraphControl()
        {
            InitializeComponent();
            
            _logger.Info("UnifiedGraphControl initializing (Phase 5.1 - Zoom/Pan Integration)");
            
            // Log when DataContext changes
            DataContextChanged += OnDataContextChanged;
            
            // Phase 5.1: Handle keyboard shortcuts for zoom/pan
            Focusable = true;
            KeyDown += OnKeyDown;
            
            // Phase 5.1: Subscribe to viewport changes to update chart
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Wire up viewport synchronization
            if (ViewModel != null)
            {
                ViewModel.ViewportChanged += OnViewportChanged;
                _logger.Debug("Subscribed to ViewportChanged event");
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is UnifiedGraphViewModel oldVm)
            {
                oldVm.ViewportChanged -= OnViewportChanged;
            }

            // Subscribe to new ViewModel
            var vm = e.NewValue as UnifiedGraphViewModel;
            if (vm != null)
            {
                _logger.Info($"? UnifiedGraphControl received ViewModel with {vm.Series.Count} series");
                
                vm.ViewportChanged += OnViewportChanged;
                
                // Phase 9 Step 2: Wire up error handling service
                if (_errorService == null)
                {
                    _errorService = new ErrorHandlingService(_logger);
                    _logger.Debug("Error handling service initialized");
                }
                
                // Initial viewport sync
                UpdateChartViewport();
            }
        }

        /// <summary>
        /// Phase 5.1: Update chart axes when viewport changes in ViewModel.
        /// </summary>
        private void OnViewportChanged(object? sender, EventArgs e)
        {
            UpdateChartViewport();
        }

        /// <summary>
        /// Phase 5.1: Synchronize chart X-axis with ViewModel viewport properties.
        /// Converts DateTime to seconds offset from recording start.
        /// </summary>
        private void UpdateChartViewport()
        {
            if (ViewModel == null || MainChart?.XAxes == null) return;

            try
            {
                var xAxis = MainChart.XAxes.FirstOrDefault();
                if (xAxis == null) return;
                
                // CRITICAL FIX: Convert DateTime to seconds offset from recording start
                // The chart data uses seconds (double), not DateTime ticks
                var minSeconds = (ViewModel.ViewportStart - ViewModel.Start).TotalSeconds;
                var maxSeconds = (ViewModel.ViewportEnd - ViewModel.Start).TotalSeconds;
                
                xAxis.MinLimit = minSeconds;
                xAxis.MaxLimit = maxSeconds;
                
                _logger.Trace($"Chart viewport updated: {minSeconds:F1}s to {maxSeconds:F1}s ({ViewModel.ViewportStart:HH:mm:ss} to {ViewModel.ViewportEnd:HH:mm:ss})");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update chart viewport");
            }
        }

        /// <summary>
        /// Phase 5.1: Handle mouse wheel zoom.
        /// Zooms in/out at the cursor position.
        /// </summary>
        private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            try
            {
                // Zoom factor: 1.2x per scroll tick (positive = zoom in, negative = zoom out)
                var zoomFactor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
                
                // Get mouse position relative to chart (0.0 = left, 1.0 = right)
                var mousePos = e.GetPosition(MainChart);
                var mouseX = mousePos.X / MainChart.ActualWidth;
                mouseX = Math.Max(0, Math.Min(1, mouseX)); // Clamp to [0, 1]
                
                // Calculate current viewport duration
                var currentDuration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
                var newDuration = TimeSpan.FromTicks((long)(currentDuration.Ticks / zoomFactor));
                
                // Minimum zoom: 1 second, Maximum zoom: full recording
                var minDuration = TimeSpan.FromSeconds(1);
                var maxDuration = ViewModel.End - ViewModel.Start;
                newDuration = TimeSpan.FromTicks(Math.Max(minDuration.Ticks, Math.Min(maxDuration.Ticks, newDuration.Ticks)));
                
                // Calculate new viewport start/end, centering zoom at mouse position
                var currentMouseTime = ViewModel.ViewportStart + TimeSpan.FromTicks((long)(currentDuration.Ticks * mouseX));
                var newStart = currentMouseTime - TimeSpan.FromTicks((long)(newDuration.Ticks * mouseX));
                var newEnd = newStart + newDuration;
                
                // Clamp to recording bounds
                if (newStart < ViewModel.Start)
                {
                    newStart = ViewModel.Start;
                    newEnd = newStart + newDuration;
                }
                if (newEnd > ViewModel.End)
                {
                    newEnd = ViewModel.End;
                    newStart = newEnd - newDuration;
                }
                
                // Update viewport
                ViewModel.ViewportStart = newStart;
                ViewModel.ViewportEnd = newEnd;
                
                // Update zoom level for display
                var zoomLevel = (ViewModel.End - ViewModel.Start).TotalSeconds / (newEnd - newStart).TotalSeconds;
                ViewModel.ZoomLevel = zoomLevel;
                
                _logger.Debug($"Zoom: {zoomLevel:F1}x, Duration: {newDuration.TotalSeconds:F1}s");
                
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during mouse wheel zoom");
            }
        }

        /// <summary>
        /// Phase 5.1: Start pan operation on middle-click or Ctrl+Left-click.
        /// </summary>
        private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;

            // Middle button or Ctrl+Left button starts pan
            if (e.ChangedButton == MouseButton.Middle || 
                (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Control))
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(MainChart);
                _panStartViewportStart = ViewModel.ViewportStart;
                _panStartViewportEnd = ViewModel.ViewportEnd;
                
                MainChart.CaptureMouse();
                e.Handled = true;
                
                _logger.Debug("Pan started");
            }
        }

        /// <summary>
        /// Phase 5.1: Update pan during mouse move.
        /// </summary>
        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning || ViewModel == null) return;

            try
            {
                var currentPoint = e.GetPosition(MainChart);
                var deltaX = currentPoint.X - _panStartPoint.X;
                
                // Convert pixel delta to time delta
                var pixelsPerTick = MainChart.ActualWidth / (_panStartViewportEnd - _panStartViewportStart).Ticks;
                var timeDelta = TimeSpan.FromTicks((long)(-deltaX / pixelsPerTick)); // Negative for natural pan direction
                
                // Calculate new viewport
                var newStart = _panStartViewportStart + timeDelta;
                var newEnd = _panStartViewportEnd + timeDelta;
                var duration = newEnd - newStart;
                
                // Clamp to recording bounds
                if (newStart < ViewModel.Start)
                {
                    newStart = ViewModel.Start;
                    newEnd = newStart + duration;
                }
                if (newEnd > ViewModel.End)
                {
                    newEnd = ViewModel.End;
                    newStart = newEnd - duration;
                }
                
                // Update viewport
                ViewModel.ViewportStart = newStart;
                ViewModel.ViewportEnd = newEnd;
                
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during pan");
            }
        }

        /// <summary>
        /// Phase 5.1: End pan operation.
        /// </summary>
        private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
            {
                _isPanning = false;
                MainChart.ReleaseMouseCapture();
                e.Handled = true;
                
                _logger.Debug("Pan ended");
            }
        }

        /// <summary>
        /// Phase 5.1: Handle keyboard shortcuts for zoom/pan and performance stats.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            var handled = true;

            switch (e.Key)
            {
                // Phase 9 Step 3: F3 toggles performance stats
                case Key.F3:
                    ViewModel.ShowPerformanceStats = !ViewModel.ShowPerformanceStats;
                    _logger.Info($"Performance stats toggled: {ViewModel.ShowPerformanceStats}");
                    break;

                // Phase 5.1: Zoom controls
                case Key.OemPlus:
                case Key.Add:
                    ZoomIn();
                    break;

                case Key.OemMinus:
                case Key.Subtract:
                    ZoomOut();
                    break;

                case Key.R:
                    ResetZoom();
                    break;

                // Phase 5.1: Pan controls
                case Key.Home:
                    PanToStart();
                    break;

                case Key.End:
                    PanToEnd();
                    break;

                case Key.PageUp:
                    PanByViewport(-1.0);
                    break;

                case Key.PageDown:
                    PanByViewport(1.0);
                    break;

                case Key.Left:
                    var leftAmount = Keyboard.Modifiers == ModifierKeys.Shift ? -0.5 : -0.1;
                    PanByViewport(leftAmount);
                    break;

                case Key.Right:
                    var rightAmount = Keyboard.Modifiers == ModifierKeys.Shift ? 0.5 : 0.1;
                    PanByViewport(rightAmount);
                    break;

                default:
                    handled = false;
                    break;
            }

            e.Handled = handled;
        }

        /// <summary>
        /// Phase 5.1: Zoom in by 20% at center.
        /// </summary>
        private void ZoomIn()
        {
            if (ViewModel == null) return;

            var currentDuration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
            var newDuration = TimeSpan.FromTicks((long)(currentDuration.Ticks / 1.2));
            var minDuration = TimeSpan.FromSeconds(1);
            
            if (newDuration >= minDuration)
            {
                var center = ViewModel.ViewportStart + TimeSpan.FromTicks(currentDuration.Ticks / 2);
                ViewModel.ViewportStart = center - TimeSpan.FromTicks(newDuration.Ticks / 2);
                ViewModel.ViewportEnd = center + TimeSpan.FromTicks(newDuration.Ticks / 2);
                
                var zoomLevel = (ViewModel.End - ViewModel.Start).TotalSeconds / newDuration.TotalSeconds;
                ViewModel.ZoomLevel = zoomLevel;
                
                _logger.Debug($"Zoom in: {zoomLevel:F1}x");
            }
        }

        /// <summary>
        /// Phase 5.1: Zoom out by 20%.
        /// </summary>
        private void ZoomOut()
        {
            if (ViewModel == null) return;

            var currentDuration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
            var newDuration = TimeSpan.FromTicks((long)(currentDuration.Ticks * 1.2));
            var maxDuration = ViewModel.End - ViewModel.Start;
            
            if (newDuration <= maxDuration)
            {
                var center = ViewModel.ViewportStart + TimeSpan.FromTicks(currentDuration.Ticks / 2);
                var newStart = center - TimeSpan.FromTicks(newDuration.Ticks / 2);
                var newEnd = center + TimeSpan.FromTicks(newDuration.Ticks / 2);
                
                // Clamp to bounds
                if (newStart < ViewModel.Start)
                {
                    newStart = ViewModel.Start;
                    newEnd = newStart + newDuration;
                }
                if (newEnd > ViewModel.End)
                {
                    newEnd = ViewModel.End;
                    newStart = newEnd - newDuration;
                }
                
                ViewModel.ViewportStart = newStart;
                ViewModel.ViewportEnd = newEnd;
                
                var zoomLevel = (ViewModel.End - ViewModel.Start).TotalSeconds / newDuration.TotalSeconds;
                ViewModel.ZoomLevel = zoomLevel;
                
                _logger.Debug($"Zoom out: {zoomLevel:F1}x");
            }
        }

        /// <summary>
        /// Phase 5.1: Reset zoom to show full recording.
        /// </summary>
        private void ResetZoom()
        {
            if (ViewModel == null) return;

            ViewModel.ViewportStart = ViewModel.Start;
            ViewModel.ViewportEnd = ViewModel.End;
            ViewModel.ZoomLevel = 1.0;
            
            _logger.Info("Zoom reset to full view");
        }

        /// <summary>
        /// Phase 5.1: Pan to start of recording.
        /// </summary>
        private void PanToStart()
        {
            if (ViewModel == null) return;

            var duration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
            ViewModel.ViewportStart = ViewModel.Start;
            ViewModel.ViewportEnd = ViewModel.Start + duration;
            
            _logger.Debug("Panned to start");
        }

        /// <summary>
        /// Phase 5.1: Pan to end of recording.
        /// </summary>
        private void PanToEnd()
        {
            if (ViewModel == null) return;

            var duration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
            ViewModel.ViewportEnd = ViewModel.End;
            ViewModel.ViewportStart = ViewModel.End - duration;
            
            _logger.Debug("Panned to end");
        }

        /// <summary>
        /// Phase 5.1: Pan by a fraction of the viewport duration.
        /// </summary>
        /// <param name="fraction">Fraction of viewport to pan (negative = left, positive = right)</param>
        private void PanByViewport(double fraction)
        {
            if (ViewModel == null) return;

            var duration = ViewModel.ViewportEnd - ViewModel.ViewportStart;
            var panAmount = TimeSpan.FromTicks((long)(duration.Ticks * fraction));
            
            var newStart = ViewModel.ViewportStart + panAmount;
            var newEnd = ViewModel.ViewportEnd + panAmount;
            
            // Clamp to bounds
            if (newStart < ViewModel.Start)
            {
                newStart = ViewModel.Start;
                newEnd = newStart + duration;
            }
            if (newEnd > ViewModel.End)
            {
                newEnd = ViewModel.End;
                newStart = newEnd - duration;
            }
            
            ViewModel.ViewportStart = newStart;
            ViewModel.ViewportEnd = newEnd;
            
            _logger.Trace($"Panned by {fraction:F2} viewport");
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
