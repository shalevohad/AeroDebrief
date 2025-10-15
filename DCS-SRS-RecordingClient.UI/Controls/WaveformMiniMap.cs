using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI.Controls
{
    /// <summary>
    /// Miniature overview of the entire waveform with visible region indicator,
    /// markers, activity heatmap, and zoom history navigation
    /// </summary>
    public class WaveformMiniMap : Canvas
    {
        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(WaveformMiniMap),
                new PropertyMetadata(null, OnWaveformDataChanged));

        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(nameof(FrequencyWaveforms), typeof(Dictionary<double, FrequencyWaveformData>), typeof(WaveformMiniMap),
                new PropertyMetadata(null, OnFrequencyWaveformsChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(nameof(ZoomStartTime), typeof(double), typeof(WaveformMiniMap),
                new PropertyMetadata(0.0, OnZoomRangeChanged, CoerceZoomStartTime));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(nameof(ZoomEndTime), typeof(double), typeof(WaveformMiniMap),
                new PropertyMetadata(1.0, OnZoomRangeChanged, CoerceZoomEndTime));

        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(WaveformMiniMap),
                new PropertyMetadata(0.0, OnPlayheadPositionChanged));

        public static readonly DependencyProperty ShowActivityHeatmapProperty =
            DependencyProperty.Register(nameof(ShowActivityHeatmap), typeof(bool), typeof(WaveformMiniMap),
                new PropertyMetadata(false, OnShowActivityHeatmapChanged));

        public static readonly DependencyProperty MarkersProperty =
            DependencyProperty.Register(nameof(Markers), typeof(ObservableCollection<WaveformMarker>), typeof(WaveformMiniMap),
                new PropertyMetadata(null, OnMarkersChanged));

        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(nameof(TotalDuration), typeof(TimeSpan), typeof(WaveformMiniMap),
                new PropertyMetadata(TimeSpan.Zero, OnTotalDurationChanged));

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

        public double PlayheadPosition
        {
            get => (double)GetValue(PlayheadPositionProperty);
            set => SetValue(PlayheadPositionProperty, value);
        }

        public bool ShowActivityHeatmap
        {
            get => (bool)GetValue(ShowActivityHeatmapProperty);
            set => SetValue(ShowActivityHeatmapProperty, value);
        }

        public ObservableCollection<WaveformMarker>? Markers
        {
            get => (ObservableCollection<WaveformMarker>?)GetValue(MarkersProperty);
            set => SetValue(MarkersProperty, value);
        }

        public TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        public event EventHandler<MiniMapClickEventArgs>? MinimapClicked;
        public event EventHandler<MiniMapDragEventArgs>? MinimapDragged;
        public event EventHandler<MarkerEventArgs>? MarkerAdded;
        public event EventHandler<MarkerEventArgs>? MarkerRemoved;
        public event EventHandler<MarkerEventArgs>? MarkerClicked;

        private Rectangle? _viewportIndicator;
        private Line? _playheadLine;
        private bool _isDraggingViewport;
        private Point _dragStartPoint;
        private double _dragStartZoomStart;
        private double _dragStartZoomEnd;

        // Zoom history management
        private readonly Stack<ZoomHistoryEntry> _zoomHistory = new();
        private readonly Stack<ZoomHistoryEntry> _zoomForwardHistory = new();
        private const int MaxZoomHistorySize = 20;

        // Marker management
        private readonly List<UIElement> _markerElements = new();

        // Activity heatmap cache
        private double[]? _activityIntensityCache;

        private readonly SolidColorBrush _waveformBrush = new(Color.FromRgb(25, 118, 210)); // Blue
        private readonly SolidColorBrush _viewportBrush = new(Color.FromArgb(80, 25, 118, 210)); // Semi-transparent blue
        private readonly SolidColorBrush _viewportBorderBrush = new(Color.FromRgb(25, 118, 210)); // Blue border
        private readonly SolidColorBrush _playheadBrush = new(Color.FromRgb(211, 47, 47)); // Red
        private readonly SolidColorBrush _backgroundBrush = new(Color.FromRgb(245, 245, 245)); // Light gray
        private readonly SolidColorBrush _markerBrush = new(Color.FromRgb(255, 152, 0)); // Orange
        private readonly SolidColorBrush _markerTextBrush = new(Color.FromRgb(33, 33, 33)); // Dark gray
        private readonly SolidColorBrush _timeTextBrush = new(Color.FromRgb(96, 96, 96)); // Gray for time display

        private TextBlock? _endingTimeText;

        public WaveformMiniMap()
        {
            Background = _backgroundBrush;
            ClipToBounds = true;
            Height = 60; // Fixed height for minimap
            Cursor = Cursors.Hand;

            SizeChanged += OnSizeChanged;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
            MouseRightButtonDown += OnMouseRightButtonDown;

            // Initialize markers collection
            Markers = new ObservableCollection<WaveformMarker>();
            Markers.CollectionChanged += OnMarkersCollectionChanged;
        }

        private void OnMarkersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RedrawMinimap();
        }

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.RedrawMinimap();
            }
        }

        private static void OnFrequencyWaveformsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.RedrawMinimap();
            }
        }

        private static void OnZoomRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.UpdateViewportIndicator();
            }
        }

        private static object CoerceZoomStartTime(DependencyObject d, object baseValue)
        {
            if (d is WaveformMiniMap minimap && baseValue is double value)
            {
                // Clamp to valid range [0, 1]
                value = Math.Clamp(value, 0.0, 1.0);
                
                // Ensure start time is less than end time
                if (value >= minimap.ZoomEndTime)
                {
                    System.Diagnostics.Debug.WriteLine($"Coercing ZoomStartTime: {value} >= ZoomEndTime: {minimap.ZoomEndTime}");
                    return Math.Max(0.0, minimap.ZoomEndTime - 0.01); // Leave at least 1% gap
                }
                
                return value;
            }
            return baseValue;
        }

        private static object CoerceZoomEndTime(DependencyObject d, object baseValue)
        {
            if (d is WaveformMiniMap minimap && baseValue is double value)
            {
                // Clamp to valid range [0, 1]
                value = Math.Clamp(value, 0.0, 1.0);
                
                // Ensure end time is greater than start time
                if (value <= minimap.ZoomStartTime)
                {
                    System.Diagnostics.Debug.WriteLine($"Coercing ZoomEndTime: {value} <= ZoomStartTime: {minimap.ZoomStartTime}");
                    return Math.Min(1.0, minimap.ZoomStartTime + 0.01); // Leave at least 1% gap
                }
                
                return value;
            }
            return baseValue;
        }

        private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.UpdatePlayhead();
            }
        }

        private static void OnShowActivityHeatmapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.RedrawMinimap();
            }
        }

        private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                if (e.OldValue is ObservableCollection<WaveformMarker> oldMarkers)
                {
                    oldMarkers.CollectionChanged -= minimap.OnMarkersCollectionChanged;
                }
                if (e.NewValue is ObservableCollection<WaveformMarker> newMarkers)
                {
                    newMarkers.CollectionChanged += minimap.OnMarkersCollectionChanged;
                }
                minimap.RedrawMinimap();
            }
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMap minimap)
            {
                minimap.UpdateEndingTimeDisplay();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawMinimap();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ActualWidth <= 0)
                return;

            var position = e.GetPosition(this);
            var normalizedX = position.X / ActualWidth;

            // Check if clicking on a marker
            if (Markers != null)
            {
                foreach (var marker in Markers)
                {
                    var markerX = marker.Position * ActualWidth;
                    if (Math.Abs(position.X - markerX) < 8) // 8 pixel tolerance
                    {
                        MarkerClicked?.Invoke(this, new MarkerEventArgs(marker));
                        return;
                    }
                }
            }

            // Check if clicking on viewport indicator
            if (_viewportIndicator != null)
            {
                var viewportLeft = Canvas.GetLeft(_viewportIndicator);
                var viewportRight = viewportLeft + _viewportIndicator.Width;

                if (position.X >= viewportLeft && position.X <= viewportRight)
                {
                    // Start dragging viewport
                    _isDraggingViewport = true;
                    _dragStartPoint = position;
                    _dragStartZoomStart = ZoomStartTime;
                    _dragStartZoomEnd = ZoomEndTime;
                    CaptureMouse();
                    Cursor = Cursors.SizeAll;
                    return;
                }
            }

            // Click outside viewport - center viewport on click position
            // Record zoom history before changing
            RecordZoomHistory();

            var zoomRange = ZoomEndTime - ZoomStartTime;
            var newStartTime = Math.Clamp(normalizedX - zoomRange / 2.0, 0.0, 1.0 - zoomRange);
            var newEndTime = newStartTime + zoomRange;

            MinimapClicked?.Invoke(this, new MiniMapClickEventArgs(newStartTime, newEndTime));
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ActualWidth <= 0)
                return;

            var position = e.GetPosition(this);
            var normalizedX = position.X / ActualWidth;

            // Check if right-clicking on an existing marker to remove it
            if (Markers != null)
            {
                foreach (var marker in Markers.ToList())
                {
                    var markerX = marker.Position * ActualWidth;
                    if (Math.Abs(position.X - markerX) < 8) // 8 pixel tolerance
                    {
                        Markers.Remove(marker);
                        MarkerRemoved?.Invoke(this, new MarkerEventArgs(marker));
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Create new marker at this position
            var newMarker = new WaveformMarker
            {
                Position = normalizedX,
                Label = $"Marker {(Markers?.Count ?? 0) + 1}",
                Color = Color.FromRgb(255, 152, 0) // Orange
            };

            Markers?.Add(newMarker);
            MarkerAdded?.Invoke(this, new MarkerEventArgs(newMarker));
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingViewport)
                return;

            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _dragStartPoint.X;
            var deltaNormalized = deltaX / ActualWidth;

            var newStartTime = Math.Clamp(_dragStartZoomStart + deltaNormalized, 0.0, 1.0);
            var newEndTime = Math.Clamp(_dragStartZoomEnd + deltaNormalized, 0.0, 1.0);

            // Ensure we don't exceed boundaries
            var zoomRange = ZoomEndTime - ZoomStartTime;
            if (newEndTime > 1.0)
            {
                newEndTime = 1.0;
                newStartTime = 1.0 - zoomRange;
            }
            if (newStartTime < 0.0)
            {
                newStartTime = 0.0;
                newEndTime = zoomRange;
            }

            MinimapDragged?.Invoke(this, new MiniMapDragEventArgs(newStartTime, newEndTime));
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingViewport)
            {
                _isDraggingViewport = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Hand;
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingViewport)
            {
                _isDraggingViewport = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Hand;
            }
        }

        private void RedrawMinimap()
        {
            Children.Clear();
            _markerElements.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Draw activity heatmap background if enabled
            if (ShowActivityHeatmap)
            {
                DrawActivityHeatmap();
            }

            // Draw waveform overview
            if (FrequencyWaveforms != null && FrequencyWaveforms.Any())
            {
                DrawMultiFrequencyOverview();
            }
            else if (WaveformData != null && WaveformData.Length > 0)
            {
                DrawSingleWaveformOverview();
            }

            // Draw markers
            DrawMarkers();

            // Draw viewport indicator and playhead on top
            UpdateViewportIndicator();
            UpdatePlayhead();

            // Draw ending time display
            UpdateEndingTimeDisplay();
        }

        private void DrawSingleWaveformOverview()
        {
            if (WaveformData == null || WaveformData.Length == 0)
                return;

            var centerY = ActualHeight / 2;
            var maxAmplitude = WaveformData.Max(Math.Abs);

            if (maxAmplitude == 0)
                return;

            var scaleY = (ActualHeight * 0.7) / 2; // Use 70% of height for compact view
            var pointsPerPixel = Math.Max(1, (int)(WaveformData.Length / ActualWidth));

            var path = new Path
            {
                Stroke = _waveformBrush,
                StrokeThickness = 0.8,
                Fill = new SolidColorBrush(Color.FromArgb(40, 25, 118, 210))
            };

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var points = new List<Point>();

                for (int x = 0; x < (int)ActualWidth; x++)
                {
                    var dataIndex = (int)(x * WaveformData.Length / ActualWidth);
                    if (dataIndex >= WaveformData.Length)
                        dataIndex = WaveformData.Length - 1;

                    // Calculate RMS
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(WaveformData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var rms = 0.0;
                    var count = 0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        rms += WaveformData[i] * WaveformData[i];
                        count++;
                    }

                    if (count > 0)
                    {
                        rms = Math.Sqrt(rms / count);
                        var normalizedAmplitude = rms / maxAmplitude;
                        var y = centerY - (normalizedAmplitude * scaleY);
                        points.Add(new Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    context.BeginFigure(points[0], true, true);

                    for (int i = 1; i < points.Count; i++)
                    {
                        context.LineTo(points[i], true, false);
                    }

                    for (int i = points.Count - 1; i >= 0; i--)
                    {
                        var mirroredPoint = new Point(points[i].X, centerY + (centerY - points[i].Y));
                        context.LineTo(mirroredPoint, true, false);
                    }
                }
            }

            geometry.Freeze();
            path.Data = geometry;
            Children.Add(path);
        }

        private void DrawMultiFrequencyOverview()
        {
            if (FrequencyWaveforms == null || !FrequencyWaveforms.Any())
                return;

            var centerY = ActualHeight / 2;
            var scaleY = (ActualHeight * 0.7) / 2;

            // Find global max amplitude
            var globalMaxAmplitude = 0.0f;
            foreach (var freqData in FrequencyWaveforms.Values)
            {
                if (freqData.WaveformData != null && freqData.WaveformData.Length > 0)
                {
                    var localMax = freqData.WaveformData.Max(Math.Abs);
                    if (localMax > globalMaxAmplitude)
                        globalMaxAmplitude = localMax;
                }
            }

            if (globalMaxAmplitude == 0)
                return;

            // Draw each frequency
            foreach (var (_, freqData) in FrequencyWaveforms.OrderBy(kvp => kvp.Key))
            {
                if (freqData.WaveformData == null || freqData.WaveformData.Length == 0)
                    continue;

                DrawFrequencyOverview(freqData, centerY, scaleY, globalMaxAmplitude);
            }
        }

        private void DrawFrequencyOverview(FrequencyWaveformData freqData, double centerY, double scaleY, float globalMaxAmplitude)
        {
            var waveformData = freqData.WaveformData;
            if (waveformData == null || waveformData.Length == 0)
                return;

            var pointsPerPixel = Math.Max(1, (int)(waveformData.Length / ActualWidth));

            var strokeBrush = new SolidColorBrush(freqData.Color);
            var fillColor = Color.FromArgb(50, freqData.Color.R, freqData.Color.G, freqData.Color.B);

            var path = new Path
            {
                Stroke = strokeBrush,
                StrokeThickness = 0.8,
                Fill = new SolidColorBrush(fillColor),
                Opacity = 0.6
            };

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var points = new List<Point>();

                for (int x = 0; x < (int)ActualWidth; x++)
                {
                    var dataIndex = (int)(x * waveformData.Length / ActualWidth);
                    if (dataIndex >= waveformData.Length)
                        dataIndex = waveformData.Length - 1;

                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(waveformData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var rms = 0.0;
                    var count = 0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        rms += waveformData[i] * waveformData[i];
                        count++;
                    }

                    if (count > 0)
                    {
                        rms = Math.Sqrt(rms / count);
                        var normalizedAmplitude = rms / globalMaxAmplitude;
                        var y = centerY - (normalizedAmplitude * scaleY);
                        points.Add(new Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    context.BeginFigure(points[0], true, true);

                    for (int i = 1; i < points.Count; i++)
                    {
                        context.LineTo(points[i], true, false);
                    }

                    for (int i = points.Count - 1; i >= 0; i--)
                    {
                        var mirroredPoint = new Point(points[i].X, centerY + (centerY - points[i].Y));
                        context.LineTo(mirroredPoint, true, false);
                    }
                }
            }

            geometry.Freeze();
            path.Data = geometry;
            Children.Add(path);
        }

        private void UpdateViewportIndicator()
        {
            if (_viewportIndicator != null)
            {
                Children.Remove(_viewportIndicator);
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Only show viewport if zoomed in
            if (ZoomStartTime <= 0.0 && ZoomEndTime >= 1.0)
                return;

            // Validate that ZoomEndTime is greater than ZoomStartTime
            if (ZoomEndTime <= ZoomStartTime)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid zoom range: StartTime={ZoomStartTime}, EndTime={ZoomEndTime}. Viewport indicator not shown.");
                return;
            }

            var leftX = ZoomStartTime * ActualWidth;
            var rightX = ZoomEndTime * ActualWidth;
            var width = rightX - leftX;

            // Additional safety check for positive width
            if (width <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"Calculated width is non-positive: {width}. Viewport indicator not shown.");
                return;
            }

            _viewportIndicator = new Rectangle
            {
                Fill = _viewportBrush,
                Stroke = _viewportBorderBrush,
                StrokeThickness = 2,
                Width = width,
                Height = ActualHeight,
                Cursor = Cursors.SizeAll
            };

            Canvas.SetLeft(_viewportIndicator, leftX);
            Canvas.SetTop(_viewportIndicator, 0);

            Children.Add(_viewportIndicator);
        }

        private void UpdatePlayhead()
        {
            if (_playheadLine != null)
            {
                Children.Remove(_playheadLine);
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            var x = PlayheadPosition * ActualWidth;

            _playheadLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = ActualHeight,
                Stroke = _playheadBrush,
                StrokeThickness = 1.5
            };

            Children.Add(_playheadLine);
        }

        #region Activity Heatmap

        private void DrawActivityHeatmap()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Calculate or use cached activity intensity
            if (_activityIntensityCache == null || _activityIntensityCache.Length != (int)ActualWidth)
            {
                CalculateActivityIntensity();
            }

            if (_activityIntensityCache == null)
                return;

            // Draw heatmap as rectangles
            for (int x = 0; x < _activityIntensityCache.Length; x++)
            {
                var intensity = _activityIntensityCache[x];
                if (intensity <= 0)
                    continue;

                var color = GetHeatmapColor(intensity);
                var rect = new Rectangle
                {
                    Width = 1,
                    Height = ActualHeight,
                    Fill = new SolidColorBrush(color)
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 0);
                Children.Add(rect);
            }
        }

        private void CalculateActivityIntensity()
        {
            if (ActualWidth <= 0)
                return;

            var width = (int)ActualWidth;
            _activityIntensityCache = new double[width];

            // Calculate intensity based on waveform data
            if (FrequencyWaveforms != null && FrequencyWaveforms.Any())
            {
                foreach (var freqData in FrequencyWaveforms.Values)
                {
                    if (freqData.WaveformData == null || freqData.WaveformData.Length == 0)
                        continue;

                    for (int x = 0; x < width; x++)
                    {
                        var dataIndex = (int)(x * freqData.WaveformData.Length / width);
                        if (dataIndex >= freqData.WaveformData.Length)
                            dataIndex = freqData.WaveformData.Length - 1;

                        // Calculate RMS in small window
                        var windowSize = Math.Max(1, freqData.WaveformData.Length / width);
                        var startIdx = Math.Max(0, dataIndex - windowSize / 2);
                        var endIdx = Math.Min(freqData.WaveformData.Length - 1, dataIndex + windowSize / 2);

                        double rms = 0;
                        var count = 0;
                        for (int i = startIdx; i <= endIdx; i++)
                        {
                            rms += freqData.WaveformData[i] * freqData.WaveformData[i];
                            count++;
                        }

                        if (count > 0)
                        {
                            rms = Math.Sqrt(rms / count);
                            _activityIntensityCache[x] = Math.Max(_activityIntensityCache[x], rms);
                        }
                    }
                }
            }
            else if (WaveformData != null && WaveformData.Length > 0)
            {
                for (int x = 0; x < width; x++)
                {
                    var dataIndex = (int)(x * WaveformData.Length / width);
                    if (dataIndex >= WaveformData.Length)
                        dataIndex = WaveformData.Length - 1;

                    var windowSize = Math.Max(1, WaveformData.Length / width);
                    var startIdx = Math.Max(0, dataIndex - windowSize / 2);
                    var endIdx = Math.Min(WaveformData.Length - 1, dataIndex + windowSize / 2);

                    double rms = 0;
                    var count = 0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        rms += WaveformData[i] * WaveformData[i];
                        count++;
                    }

                    if (count > 0)
                    {
                        rms = Math.Sqrt(rms / count);
                        _activityIntensityCache[x] = rms;
                    }
                }
            }

            // Normalize to 0-1 range
            var maxIntensity = _activityIntensityCache.Max();
            if (maxIntensity > 0)
            {
                for (int i = 0; i < _activityIntensityCache.Length; i++)
                {
                    _activityIntensityCache[i] /= maxIntensity;
                }
            }
        }

        private Color GetHeatmapColor(double intensity)
        {
            // Color gradient: Blue (low) -> Green (medium) -> Yellow -> Red (high)
            intensity = Math.Clamp(intensity, 0, 1);

            if (intensity < 0.25)
            {
                // Blue to Cyan
                var t = intensity / 0.25;
                return Color.FromArgb(
                    80,
                    (byte)(0 * (1 - t) + 0 * t),
                    (byte)(0 * (1 - t) + 255 * t),
                    (byte)(255 * (1 - t) + 255 * t)
                );
            }
            else if (intensity < 0.5)
            {
                // Cyan to Green
                var t = (intensity - 0.25) / 0.25;
                return Color.FromArgb(
                    80,
                    (byte)(0 * (1 - t) + 0 * t),
                    (byte)(255 * (1 - t) + 255 * t),
                    (byte)(255 * (1 - t) + 0 * t)
                );
            }
            else if (intensity < 0.75)
            {
                // Green to Yellow
                var t = (intensity - 0.5) / 0.25;
                return Color.FromArgb(
                    80,
                    (byte)(0 * (1 - t) + 255 * t),
                    (byte)(255 * (1 - t) + 255 * t),
                    (byte)(0 * (1 - t) + 0 * t)
                );
            }
            else
            {
                // Yellow to Red
                var t = (intensity - 0.75) / 0.25;
                return Color.FromArgb(
                    80,
                    (byte)(255 * (1 - t) + 255 * t),
                    (byte)(255 * (1 - t) + 0 * t),
                    (byte)(0 * (1 - t) + 0 * t)
                );
            }
        }

        #endregion

        #region Markers

        private void DrawMarkers()
        {
            if (Markers == null || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            foreach (var marker in Markers)
            {
                DrawMarker(marker);
            }
        }

        private void DrawMarker(WaveformMarker marker)
        {
            var x = marker.Position * ActualWidth;

            // Draw marker line
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = ActualHeight,
                Stroke = new SolidColorBrush(marker.Color),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };

            // Draw marker flag/triangle at top
            var triangle = new Polygon
            {
                Fill = new SolidColorBrush(marker.Color),
                Points = new PointCollection
                {
                    new Point(x, 0),
                    new Point(x + 8, 6),
                    new Point(x, 12)
                }
            };

            // Add tooltip
            var tooltip = new ToolTip
            {
                Content = $"{marker.Label}\nPosition: {marker.Position:P1}"
            };
            line.ToolTip = tooltip;
            triangle.ToolTip = tooltip;

            Children.Add(line);
            Children.Add(triangle);

            _markerElements.Add(line);
            _markerElements.Add(triangle);
        }

        #endregion

        #region Zoom History

        private void RecordZoomHistory()
        {
            // Don't record if at full view
            if (ZoomStartTime <= 0.0 && ZoomEndTime >= 1.0)
                return;

            var entry = new ZoomHistoryEntry(ZoomStartTime, ZoomEndTime);

            // Don't record duplicate entries
            if (_zoomHistory.Count > 0 && _zoomHistory.Peek().Equals(entry))
                return;

            _zoomHistory.Push(entry);

            // Limit history size
            if (_zoomHistory.Count > MaxZoomHistorySize)
            {
                var tempStack = new Stack<ZoomHistoryEntry>();
                for (int i = 0; i < MaxZoomHistorySize; i++)
                {
                    tempStack.Push(_zoomHistory.Pop());
                }
                _zoomHistory.Clear();
                while (tempStack.Count > 0)
                {
                    _zoomHistory.Push(tempStack.Pop());
                }
            }

            // Clear forward history when new action is taken
            _zoomForwardHistory.Clear();
        }

        public bool CanGoBackInHistory => _zoomHistory.Count > 0;
        public bool CanGoForwardInHistory => _zoomForwardHistory.Count > 0;

        public void GoBackInZoomHistory()
        {
            if (!CanGoBackInHistory)
                return;

            // Save current state to forward history
            var current = new ZoomHistoryEntry(ZoomStartTime, ZoomEndTime);
            _zoomForwardHistory.Push(current);

            // Restore previous state
            var previous = _zoomHistory.Pop();
            MinimapClicked?.Invoke(this, new MiniMapClickEventArgs(previous.StartTime, previous.EndTime));
        }

        public void GoForwardInZoomHistory()
        {
            if (!CanGoForwardInHistory)
                return;

            // Save current state to back history
            var current = new ZoomHistoryEntry(ZoomStartTime, ZoomEndTime);
            _zoomHistory.Push(current);

            // Restore next state
            var next = _zoomForwardHistory.Pop();
            MinimapClicked?.Invoke(this, new MiniMapClickEventArgs(next.StartTime, next.EndTime));
        }

        public void ClearZoomHistory()
        {
            _zoomHistory.Clear();
            _zoomForwardHistory.Clear();
        }

        #endregion

        #region Ending Time Display

        private void UpdateEndingTimeDisplay()
        {
            // Remove existing time display
            if (_endingTimeText != null)
            {
                Children.Remove(_endingTimeText);
                _endingTimeText = null;
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            if (TotalDuration.TotalSeconds == 0)
                return;

            // Create time text
            var timeString = TotalDuration.ToString(@"mm\:ss\.fff");
            
            _endingTimeText = new TextBlock
            {
                Text = timeString,
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Foreground = _timeTextBrush,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), // Semi-transparent white background
                Padding = new Thickness(4, 2, 4, 2)
            };

            // Measure the text size
            _endingTimeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = _endingTimeText.DesiredSize.Width;
            var textHeight = _endingTimeText.DesiredSize.Height;

            // Position at bottom right with small margin
            Canvas.SetRight(_endingTimeText, 4);
            Canvas.SetBottom(_endingTimeText, 2);

            Children.Add(_endingTimeText);
        }

        #endregion
    }

    /// <summary>
    /// Event args for minimap click
    /// </summary>
    public class MiniMapClickEventArgs : EventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public MiniMapClickEventArgs(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    /// <summary>
    /// Event args for minimap drag
    /// </summary>
    public class MiniMapDragEventArgs : EventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public MiniMapDragEventArgs(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    /// <summary>
    /// Event args for marker events
    /// </summary>
    public class MarkerEventArgs : EventArgs
    {
        public WaveformMarker Marker { get; }

        public MarkerEventArgs(WaveformMarker marker)
        {
            Marker = marker;
        }
    }

    /// <summary>
    /// Represents a marker/bookmark on the waveform timeline
    /// </summary>
    public class WaveformMarker
    {
        public double Position { get; set; } // 0.0 to 1.0
        public string Label { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.FromRgb(255, 152, 0);
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Represents a zoom history entry
    /// </summary>
    internal class ZoomHistoryEntry
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public ZoomHistoryEntry(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public bool Equals(ZoomHistoryEntry other)
        {
            return Math.Abs(StartTime - other.StartTime) < 0.001 &&
                   Math.Abs(EndTime - other.EndTime) < 0.001;
        }
    }
}
