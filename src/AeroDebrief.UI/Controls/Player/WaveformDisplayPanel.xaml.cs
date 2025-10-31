using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AeroDebrief.UI.Helpers;
using AeroDebrief.UI.Events;
using FontAwesome.WPF;

namespace AeroDebrief.UI.Controls.Player
{
    /// <summary>
    /// Independent waveform display panel with integrated zoom controls and GPU status.
    /// Wraps the WaveformWithMiniMap control and provides additional UI controls.
    /// </summary>
    public partial class WaveformDisplayPanel : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Gets or sets the waveform data to display.
        /// </summary>
        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(
                nameof(WaveformData),
                typeof(float[]),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the per-frequency waveform data.
        /// </summary>
        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(
                nameof(FrequencyWaveforms),
                typeof(Dictionary<double, FrequencyWaveformData>),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(null, OnFrequencyWaveformsPropertyChanged));

        /// <summary>
        /// Gets or sets the playhead position (0.0 to 1.0).
        /// </summary>
        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(
                nameof(PlayheadPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Gets or sets the zoom start time (0.0 to 1.0).
        /// </summary>
        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(
                nameof(ZoomStartTime),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0, OnZoomPropertyChanged));

        /// <summary>
        /// Gets or sets the zoom end time (0.0 to 1.0).
        /// </summary>
        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(
                nameof(ZoomEndTime),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(1.0, OnZoomPropertyChanged));

        /// <summary>
        /// Gets or sets the total duration of the audio.
        /// </summary>
        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(
                nameof(TotalDuration),
                typeof(TimeSpan),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(TimeSpan.Zero));

        /// <summary>
        /// Gets or sets whether the waveform is being loaded.
        /// </summary>
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the loading message.
        /// </summary>
        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register(
                nameof(LoadingMessage),
                typeof(string),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata("Generating waveform..."));

        /// <summary>
        /// Gets or sets the loading progress (0.0 to 100.0).
        /// </summary>
        public static readonly DependencyProperty LoadingProgressProperty =
            DependencyProperty.Register(
                nameof(LoadingProgress),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Gets or sets the waveform engine type ("GPU" or "CPU").
        /// </summary>
        public static readonly DependencyProperty EngineTypeProperty =
            DependencyProperty.Register(
                nameof(EngineType),
                typeof(string),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata("CPU", OnEngineTypeChanged));

        /// <summary>
        /// Gets or sets whether GPU acceleration is being used.
        /// </summary>
        public static readonly DependencyProperty IsUsingGpuProperty =
            DependencyProperty.Register(
                nameof(IsUsingGpu),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(false, OnIsUsingGpuChanged));

        /// <summary>
        /// Gets or sets whether to show the engine status badge.
        /// </summary>
        public static readonly DependencyProperty ShowEngineStatusProperty =
            DependencyProperty.Register(
                nameof(ShowEngineStatus),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets whether to show zoom controls.
        /// </summary>
        public static readonly DependencyProperty ShowZoomControlsProperty =
            DependencyProperty.Register(
                nameof(ShowZoomControls),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets whether to show the minimap.
        /// </summary>
        public static readonly DependencyProperty ShowMinimapProperty =
            DependencyProperty.Register(
                nameof(ShowMinimap),
                typeof(bool),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets the minimap height.
        /// </summary>
        public static readonly DependencyProperty MinimapHeightProperty =
            DependencyProperty.Register(
                nameof(MinimapHeight),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(80.0));

        /// <summary>
        /// Gets or sets the buffer start position (0.0 to 1.0).
        /// </summary>
        public static readonly DependencyProperty BufferStartPositionProperty =
            DependencyProperty.Register(
                nameof(BufferStartPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Gets or sets the buffer end position (0.0 to 1.0).
        /// </summary>
        public static readonly DependencyProperty BufferEndPositionProperty =
            DependencyProperty.Register(
                nameof(BufferEndPosition),
                typeof(double),
                typeof(WaveformDisplayPanel),
                new PropertyMetadata(0.0));

        #endregion

        #region Properties

        public float[]? WaveformData
        {
            get => (float[]?)GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public Dictionary<double, FrequencyWaveformData>? FrequencyWaveforms
        {
            get => (Dictionary<double, FrequencyWaveformData>?)GetValue(FrequencyWaveformsProperty);
            set => SetValue(FrequencyWaveformsProperty, value);
        }

        public double PlayheadPosition
        {
            get => (double)GetValue(PlayheadPositionProperty);
            set => SetValue(PlayheadPositionProperty, value);
        }

        public double ZoomStartTime
        {
            get => (double)GetValue(ZoomStartTimeProperty);
            set => SetValue(ZoomStartTimeProperty, value);
        }

        public double ZoomEndTime
        {
            get => (double)GetValue(ZoomEndTimeProperty);
            set => SetValue(ZoomEndTimeProperty, value);
        }

        public TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public string LoadingMessage
        {
            get => (string)GetValue(LoadingMessageProperty);
            set => SetValue(LoadingMessageProperty, value);
        }

        public double LoadingProgress
        {
            get => (double)GetValue(LoadingProgressProperty);
            set => SetValue(LoadingProgressProperty, value);
        }

        public string EngineType
        {
            get => (string)GetValue(EngineTypeProperty);
            set => SetValue(EngineTypeProperty, value);
        }

        public bool IsUsingGpu
        {
            get => (bool)GetValue(IsUsingGpuProperty);
            set => SetValue(IsUsingGpuProperty, value);
        }

        public bool ShowEngineStatus
        {
            get => (bool)GetValue(ShowEngineStatusProperty);
            set => SetValue(ShowEngineStatusProperty, value);
        }

        public bool ShowZoomControls
        {
            get => (bool)GetValue(ShowZoomControlsProperty);
            set => SetValue(ShowZoomControlsProperty, value);
        }

        public bool ShowMinimap
        {
            get => (bool)GetValue(ShowMinimapProperty);
            set => SetValue(ShowMinimapProperty, value);
        }

        public double MinimapHeight
        {
            get => (double)GetValue(MinimapHeightProperty);
            set => SetValue(MinimapHeightProperty, value);
        }

        public double BufferStartPosition
        {
            get => (double)GetValue(BufferStartPositionProperty);
            set => SetValue(BufferStartPositionProperty, value);
        }

        public double BufferEndPosition
        {
            get => (double)GetValue(BufferEndPositionProperty);
            set => SetValue(BufferEndPositionProperty, value);
        }

        /// <summary>
        /// Gets the engine status color based on whether GPU is being used.
        /// </summary>
        public Brush EngineStatusColor => IsUsingGpu
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Green for GPU
            : new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange for CPU

        /// <summary>
        /// Gets the engine status tooltip.
        /// </summary>
        public string EngineStatusTooltip => IsUsingGpu
            ? "GPU-accelerated waveform generation\n10-50x faster than CPU"
            : "CPU-based waveform generation\nGPU not available or disabled";

        #endregion

        #region Routed Events - Using Centralized PlayerEvents

        /// <summary>
        /// Raised when the user requests to seek to a specific position.
        /// </summary>
        public event Events.RoutedEventHandler<Events.SeekRequestedEventArgs> SeekRequested
        {
            add => this.AddSeekRequestedHandler(value);
            remove => this.RemoveSeekRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the zoom level changes.
        /// </summary>
        public event Events.RoutedEventHandler<Events.ZoomChangedEventArgs> ZoomChanged
        {
            add => this.AddZoomChangedHandler(value);
            remove => this.RemoveZoomChangedHandler(value);
        }

        /// <summary>
        /// Raised when the waveform control size changes (for GPU compositor).
        /// </summary>
        public event Events.RoutedEventHandler<Events.WaveformSizeChangedEventArgs> WaveformSizeChanged
        {
            add => this.AddWaveformSizeChangedHandler(value);
            remove => this.RemoveWaveformSizeChangedHandler(value);
        }

        #endregion

        #region Constructor

        public WaveformDisplayPanel()
        {
            InitializeComponent();
            
            // Wire up size change events for GPU compositor
            WaveformDisplay.SizeChanged += WaveformDisplay_SizeChanged;
            
            // Update engine icon when loaded
            this.Loaded += WaveformDisplayPanel_Loaded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the panel loaded event to set up initial state.
        /// </summary>
        private void WaveformDisplayPanel_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEngineIcon();
        }

        /// <summary>
        /// Handles waveform size changes to trigger GPU compositor updates.
        /// </summary>
        private void WaveformDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                this.RaiseWaveformSizeChanged(e.NewSize.Width, e.NewSize.Height);
            }
        }

        /// <summary>
        /// Handles seek requests from the waveform display.
        /// </summary>
        private void WaveformDisplay_SeekRequested(object? sender, double normalizedPosition)
        {
            this.RaiseSeekRequested(normalizedPosition);
        }

        /// <summary>
        /// Handles zoom region selection from the waveform display.
        /// </summary>
        private void WaveformDisplay_ZoomRegionSelected(object? sender, ZoomRegionSelectedEventArgs e)
        {
            ZoomToRegion(e.StartTime, e.EndTime);
        }

        /// <summary>
        /// Handles minimap click events.
        /// </summary>
        private void MiniMap_MinimapClicked(object? sender, MiniMapClickEventArgs e)
        {
            ZoomToRegion(e.StartTime, e.EndTime);
        }

        /// <summary>
        /// Handles minimap drag events.
        /// </summary>
        private void MiniMap_MinimapDragged(object? sender, MiniMapDragEventArgs e)
        {
            ZoomToRegion(e.StartTime, e.EndTime);
        }

        /// <summary>
        /// Handles zoom in button click.
        /// </summary>
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        /// <summary>
        /// Handles zoom out button click.
        /// </summary>
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        /// <summary>
        /// Handles zoom reset button click.
        /// </summary>
        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Zooms in by reducing the visible range by 50%.
        /// </summary>
        public void ZoomIn()
        {
            WaveformDisplay?.ZoomIn(0.5);
        }

        /// <summary>
        /// Zooms out by doubling the visible range.
        /// </summary>
        public void ZoomOut()
        {
            WaveformDisplay?.ZoomOut(2.0);
        }

        /// <summary>
        /// Resets zoom to show the full waveform.
        /// </summary>
        public void ResetZoom()
        {
            WaveformDisplay?.ResetZoom();
        }

        /// <summary>
        /// Zooms to a specific time region.
        /// </summary>
        /// <param name="startTime">Normalized start time (0.0 to 1.0).</param>
        /// <param name="endTime">Normalized end time (0.0 to 1.0).</param>
        public void ZoomToRegion(double startTime, double endTime)
        {
            WaveformDisplay?.ZoomToRegion(startTime, endTime);
        }

        #endregion

        #region Property Change Handlers

        private static void OnFrequencyWaveformsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateGpuCompositeIfNeeded();
            }
        }

        private static void OnZoomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.RaiseZoomChangedEvent();
            }
        }

        private static void OnEngineTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateEngineIcon();
                panel.OnPropertyChanged(nameof(EngineStatusColor));
                panel.OnPropertyChanged(nameof(EngineStatusTooltip));
            }
        }

        private static void OnIsUsingGpuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformDisplayPanel panel)
            {
                panel.UpdateEngineIcon();
                panel.OnPropertyChanged(nameof(EngineStatusColor));
                panel.OnPropertyChanged(nameof(EngineStatusTooltip));
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Updates the engine icon based on the engine type.
        /// </summary>
        private void UpdateEngineIcon()
        {
            if (EngineIconHost == null)
                return;

            try
            {
                var token = (EngineType ?? "CPU").Trim().ToLowerInvariant();
                FontAwesomeIcon faIcon = IsUsingGpu 
                    ? FontAwesomeIcon.Microchip  // GPU icon
                    : FontAwesomeIcon.Desktop;   // CPU icon

                var brush = new SolidColorBrush(Colors.White);
                var element = IconHelper.CreateFaIcon(faIcon, 14, brush);

                Dispatcher.Invoke(() =>
                {
                    EngineIconHost.Content = element;
                });
            }
            catch
            {
                // Ignore any errors updating the icon
            }
        }

        /// <summary>
        /// Updates GPU composite texture if GPU rendering is active.
        /// </summary>
        private void UpdateGpuCompositeIfNeeded()
        {
            // This is called when FrequencyWaveforms changes
            // The parent can listen to this event and update GPU compositor
        }

        /// <summary>
        /// Raises the ZoomChanged event using centralized event system.
        /// </summary>
        private void RaiseZoomChangedEvent()
        {
            this.RaiseZoomChanged(ZoomStartTime, ZoomEndTime, 1.0 / (ZoomEndTime - ZoomStartTime));
        }

        /// <summary>
        /// Notifies property change for dynamic properties.
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            // Trigger property changed notification
            GetBindingExpression(GetPropertyForName(propertyName))?.UpdateTarget();
        }

        /// <summary>
        /// Gets the dependency property for a given property name.
        /// </summary>
        private DependencyProperty? GetPropertyForName(string propertyName)
        {
            return propertyName switch
            {
                nameof(EngineStatusColor) => null, // Computed property
                nameof(EngineStatusTooltip) => null, // Computed property
                _ => null
            };
        }

        #endregion
    }
}
