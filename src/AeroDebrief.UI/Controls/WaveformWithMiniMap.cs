using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Reusable composite control that combines WaveformViewer with WaveformMiniMap.
    /// Provides a complete waveform visualization solution with zoom, pan, and navigation capabilities.
    /// </summary>
    public class WaveformWithMiniMap : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(WaveformWithMiniMap),
                new PropertyMetadata(null, OnWaveformDataChanged));

        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(nameof(FrequencyWaveforms), typeof(Dictionary<double, FrequencyWaveformData>), typeof(WaveformWithMiniMap),
                new PropertyMetadata(null, OnFrequencyWaveformsChanged));

        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(WaveformWithMiniMap),
                new PropertyMetadata(0.0, OnPlayheadPositionChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(nameof(ZoomStartTime), typeof(double), typeof(WaveformWithMiniMap),
                new PropertyMetadata(0.0, OnZoomStartTimeChanged));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(nameof(ZoomEndTime), typeof(double), typeof(WaveformWithMiniMap),
                new PropertyMetadata(1.0, OnZoomEndTimeChanged));

        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(nameof(TotalDuration), typeof(TimeSpan), typeof(WaveformWithMiniMap),
                new PropertyMetadata(TimeSpan.Zero, OnTotalDurationChanged));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(WaveformWithMiniMap),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(WaveformWithMiniMap),
                new PropertyMetadata("Generating waveform...", OnLoadingMessageChanged));

        public static readonly DependencyProperty IsInteractiveProperty =
            DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(WaveformWithMiniMap),
                new PropertyMetadata(true, OnIsInteractiveChanged));

        public static readonly DependencyProperty ShowMiniMapProperty =
            DependencyProperty.Register(nameof(ShowMiniMap), typeof(bool), typeof(WaveformWithMiniMap),
                new PropertyMetadata(true, OnShowMiniMapChanged));

        public static readonly DependencyProperty MiniMapHeightProperty =
            DependencyProperty.Register(nameof(MiniMapHeight), typeof(double), typeof(WaveformWithMiniMap),
                new PropertyMetadata(60.0, OnMiniMapHeightChanged));

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

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public bool ShowMiniMap
        {
            get => (bool)GetValue(ShowMiniMapProperty);
            set => SetValue(ShowMiniMapProperty, value);
        }

        public double MiniMapHeight
        {
            get => (double)GetValue(MiniMapHeightProperty);
            set => SetValue(MiniMapHeightProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? SeekRequested;
        public event EventHandler<ZoomRegionSelectedEventArgs>? ZoomRegionSelected;
        public event EventHandler<MiniMapClickEventArgs>? MinimapClicked;
        public event EventHandler<MiniMapDragEventArgs>? MinimapDragged;

        #endregion

        #region Private Fields

        private Grid _rootGrid;
        private WaveformViewer _waveformViewer;
        private WaveformMiniMap _miniMap;

        #endregion

        #region Constructor

        public WaveformWithMiniMap()
        {
            InitializeComponent();
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            // Create root grid
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) }); // Spacer
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // MiniMap

            // Create waveform viewer
            _waveformViewer = new WaveformViewer
            {
                IsInteractive = true,
                Background = System.Windows.Media.Brushes.White
            };
            Grid.SetRow(_waveformViewer, 0);

            // Create minimap
            _miniMap = new WaveformMiniMap();
            Grid.SetRow(_miniMap, 2);

            // Add controls to grid
            _rootGrid.Children.Add(_waveformViewer);
            _rootGrid.Children.Add(_miniMap);

            // Set as content
            Content = _rootGrid;

            // Wire up events
            _waveformViewer.SeekRequested += OnWaveformSeekRequested;
            _waveformViewer.ZoomRegionSelected += OnWaveformZoomRegionSelected;
            _miniMap.MinimapClicked += OnMinimapClickedInternal;
            _miniMap.MinimapDragged += OnMinimapDraggedInternal;
        }

        #endregion

        #region Property Changed Handlers

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.WaveformData = e.NewValue as float[];
                control._miniMap.WaveformData = e.NewValue as float[];
            }
        }

        private static void OnFrequencyWaveformsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.FrequencyWaveforms = e.NewValue as Dictionary<double, FrequencyWaveformData>;
                control._miniMap.FrequencyWaveforms = e.NewValue as Dictionary<double, FrequencyWaveformData>;
            }
        }

        private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.PlayheadPosition = (double)e.NewValue;
                control._miniMap.PlayheadPosition = (double)e.NewValue;
            }
        }

        private static void OnZoomStartTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.ZoomStartTime = (double)e.NewValue;
                control._miniMap.ZoomStartTime = (double)e.NewValue;
            }
        }

        private static void OnZoomEndTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.ZoomEndTime = (double)e.NewValue;
                control._miniMap.ZoomEndTime = (double)e.NewValue;
            }
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._miniMap.TotalDuration = (TimeSpan)e.NewValue;
            }
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.IsLoading = (bool)e.NewValue;
            }
        }

        private static void OnLoadingMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.LoadingMessage = (string)e.NewValue;
            }
        }

        private static void OnIsInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._waveformViewer.IsInteractive = (bool)e.NewValue;
            }
        }

        private static void OnShowMiniMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control)
            {
                control._miniMap.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
                // Update row height
                control._rootGrid.RowDefinitions[1].Height = (bool)e.NewValue ? new GridLength(8) : new GridLength(0);
                control._rootGrid.RowDefinitions[2].Height = (bool)e.NewValue ? new GridLength(control.MiniMapHeight) : new GridLength(0);
            }
        }

        private static void OnMiniMapHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformWithMiniMap control && control.ShowMiniMap)
            {
                control._rootGrid.RowDefinitions[2].Height = new GridLength((double)e.NewValue);
            }
        }

        #endregion

        #region Event Handlers

        private void OnWaveformSeekRequested(object? sender, double position)
        {
            SeekRequested?.Invoke(this, position);
        }

        private void OnWaveformZoomRegionSelected(object? sender, ZoomRegionSelectedEventArgs e)
        {
            ZoomRegionSelected?.Invoke(this, e);
        }

        private void OnMinimapClickedInternal(object? sender, MiniMapClickEventArgs e)
        {
            MinimapClicked?.Invoke(this, e);
        }

        private void OnMinimapDraggedInternal(object? sender, MiniMapDragEventArgs e)
        {
            MinimapDragged?.Invoke(this, e);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resets zoom to show the full waveform
        /// </summary>
        public void ResetZoom()
        {
            ZoomStartTime = 0.0;
            ZoomEndTime = 1.0;
        }

        /// <summary>
        /// Zooms in by a specific factor
        /// </summary>
        public void ZoomIn(double factor = 0.5)
        {
            var currentRange = ZoomEndTime - ZoomStartTime;
            var newRange = currentRange * factor;
            var center = (ZoomStartTime + ZoomEndTime) / 2.0;
            
            var newStart = Math.Max(0.0, center - newRange / 2.0);
            var newEnd = Math.Min(1.0, center + newRange / 2.0);
            
            // Ensure minimum zoom level (1% of total)
            if (newEnd - newStart < 0.01)
            {
                newRange = 0.01;
                newStart = Math.Max(0.0, center - newRange / 2.0);
                newEnd = newStart + newRange;
            }
            
            ZoomStartTime = newStart;
            ZoomEndTime = newEnd;
        }

        /// <summary>
        /// Zooms out by a specific factor
        /// </summary>
        public void ZoomOut(double factor = 2.0)
        {
            var currentRange = ZoomEndTime - ZoomStartTime;
            var newRange = Math.Min(1.0, currentRange * factor);
            var center = (ZoomStartTime + ZoomEndTime) / 2.0;
            
            var newStart = Math.Max(0.0, center - newRange / 2.0);
            var newEnd = Math.Min(1.0, center + newRange / 2.0);
            
            // Adjust if we hit boundaries
            if (newEnd > 1.0)
            {
                newEnd = 1.0;
                newStart = Math.Max(0.0, newEnd - newRange);
            }
            if (newStart < 0.0)
            {
                newStart = 0.0;
                newEnd = Math.Min(1.0, newStart + newRange);
            }
            
            ZoomStartTime = newStart;
            ZoomEndTime = newEnd;
        }

        /// <summary>
        /// Zooms to a specific region
        /// </summary>
        public void ZoomToRegion(double startTime, double endTime)
        {
            startTime = Math.Clamp(startTime, 0.0, 1.0);
            endTime = Math.Clamp(endTime, 0.0, 1.0);
            
            if (startTime >= endTime)
                return;
            
            ZoomStartTime = startTime;
            ZoomEndTime = endTime;
        }

        /// <summary>
        /// Pans the view by a specific amount (normalized)
        /// </summary>
        public void Pan(double delta)
        {
            var currentRange = ZoomEndTime - ZoomStartTime;
            var newStart = ZoomStartTime + delta;
            var newEnd = ZoomEndTime + delta;
            
            // Keep within bounds
            if (newStart < 0.0)
            {
                newStart = 0.0;
                newEnd = currentRange;
            }
            else if (newEnd > 1.0)
            {
                newEnd = 1.0;
                newStart = 1.0 - currentRange;
            }
            
            ZoomStartTime = newStart;
            ZoomEndTime = newEnd;
        }

        #endregion
    }
}
