using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI.Controls
{
    public class WaveformViewer : Canvas
    {
        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(WaveformViewer),
                new PropertyMetadata(null, OnWaveformDataChanged));

        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnPlayheadPositionChanged));

        public static readonly DependencyProperty IsInteractiveProperty =
            DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(WaveformViewer),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IsFilteredProperty =
            DependencyProperty.Register(nameof(IsFiltered), typeof(bool), typeof(WaveformViewer),
                new PropertyMetadata(false, OnIsFilteredChanged));

        public static readonly DependencyProperty FilteredFrequenciesProperty =
            DependencyProperty.Register(nameof(FilteredFrequencies), typeof(HashSet<double>), typeof(WaveformViewer),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(WaveformViewer),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(WaveformViewer),
                new PropertyMetadata("Generating waveform...", OnIsLoadingChanged));

        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(nameof(FrequencyWaveforms), typeof(Dictionary<double, FrequencyWaveformData>), typeof(WaveformViewer),
                new PropertyMetadata(null, OnFrequencyWaveformsChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(nameof(ZoomStartTime), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnZoomChanged));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(nameof(ZoomEndTime), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(1.0, OnZoomChanged));

        public float[]? WaveformData
        {
            get => (float[]?)GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public double PlayheadPosition
        {
            get => (double)GetValue(PlayheadPositionProperty);
            set => SetValue(PlayheadPositionProperty, value);
        }

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public bool IsFiltered
        {
            get => (bool)GetValue(IsFilteredProperty);
            set => SetValue(IsFilteredProperty, value);
        }

        public HashSet<double>? FilteredFrequencies
        {
            get => (HashSet<double>?)GetValue(FilteredFrequenciesProperty);
            set => SetValue(FilteredFrequenciesProperty, value);
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

        public event EventHandler<double>? SeekRequested;
        public event EventHandler<ZoomRegionSelectedEventArgs>? ZoomRegionSelected;

        private Line? _playheadLine;
        private Rectangle? _selectionRectangle;
        private Point? _selectionStartPoint;
        private bool _isSelecting;
        private readonly SolidColorBrush _waveformBrush = new(Color.FromRgb(25, 118, 210)); // Blue
        private readonly SolidColorBrush _filteredWaveformBrush = new(Color.FromRgb(76, 175, 80)); // Green for filtered
        private readonly SolidColorBrush _playheadBrush = new(Color.FromRgb(211, 47, 47)); // Red
        private readonly SolidColorBrush _selectionBrush = new(Color.FromArgb(60, 25, 118, 210)); // Semi-transparent blue
        private readonly SolidColorBrush _selectionBorderBrush = new(Color.FromRgb(255, 255, 255)); // White border

        public WaveformViewer()
        {
            Background = Brushes.White;
            ClipToBounds = true;

            SizeChanged += OnSizeChanged;

            // mouse handlers for both seeking and selection
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
            Cursor = Cursors.Hand;
        }

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.RedrawWaveform();
            }
        }

        private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.UpdatePlayhead();
            }
        }

        private static void OnIsFilteredChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.RedrawWaveform();
            }
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.RedrawWaveform();
            }
        }

        private static void OnFrequencyWaveformsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.RedrawWaveform();
            }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.RedrawWaveform();
                viewer.UpdatePlayhead();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawWaveform();
            UpdatePlayhead();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive || ActualWidth <= 0)
                return;

            var position = e.GetPosition(this);

            // Check if Ctrl is pressed for selection mode
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Start selection
                _isSelecting = true;
                _selectionStartPoint = position;
                
                // Create selection rectangle
                _selectionRectangle = new Rectangle
                {
                    Fill = _selectionBrush,
                    Stroke = _selectionBorderBrush,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                
                Canvas.SetLeft(_selectionRectangle, position.X);
                Canvas.SetTop(_selectionRectangle, 0);
                _selectionRectangle.Width = 0;
                _selectionRectangle.Height = ActualHeight;
                
                Children.Add(_selectionRectangle);
                CaptureMouse();
                Cursor = Cursors.Cross;
            }
            else
            {
                // Regular seek behavior
                var normalizedPosition = position.X / ActualWidth;
                
                // Convert to visible time range
                var visibleRange = ZoomEndTime - ZoomStartTime;
                var seekPosition = ZoomStartTime + (normalizedPosition * visibleRange);
                
                SeekRequested?.Invoke(this, Math.Clamp(seekPosition, 0.0, 1.0));
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting || _selectionStartPoint == null || _selectionRectangle == null)
                return;

            var currentPosition = e.GetPosition(this);
            var startX = _selectionStartPoint.Value.X;
            var currentX = currentPosition.X;

            // Update selection rectangle
            var left = Math.Min(startX, currentX);
            var width = Math.Abs(currentX - startX);

            Canvas.SetLeft(_selectionRectangle, left);
            _selectionRectangle.Width = width;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting || _selectionStartPoint == null || _selectionRectangle == null)
                return;

            ReleaseMouseCapture();
            Cursor = Cursors.Hand;

            var endPosition = e.GetPosition(this);
            var startX = _selectionStartPoint.Value.X;
            var endX = endPosition.X;

            // Ensure we have a meaningful selection (at least 10 pixels)
            if (Math.Abs(endX - startX) > 10)
            {
                var left = Math.Min(startX, endX);
                var right = Math.Max(startX, endX);

                // Convert to normalized positions within the current zoom range
                var leftNormalized = left / ActualWidth;
                var rightNormalized = right / ActualWidth;

                // Calculate new zoom range within the visible range
                var visibleRange = ZoomEndTime - ZoomStartTime;
                var newStartTime = ZoomStartTime + (leftNormalized * visibleRange);
                var newEndTime = ZoomStartTime + (rightNormalized * visibleRange);

                // Fire zoom event
                ZoomRegionSelected?.Invoke(this, new ZoomRegionSelectedEventArgs(newStartTime, newEndTime));
            }

            // Clean up selection
            Children.Remove(_selectionRectangle);
            _selectionRectangle = null;
            _selectionStartPoint = null;
            _isSelecting = false;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                // Cancel selection on mouse leave
                ReleaseMouseCapture();
                if (_selectionRectangle != null)
                {
                    Children.Remove(_selectionRectangle);
                }
                _selectionRectangle = null;
                _selectionStartPoint = null;
                _isSelecting = false;
                Cursor = Cursors.Hand;
            }
        }

        private void RedrawWaveform()
        {
            Children.Clear();

            // Show loading message if waveform is being generated
            if (IsLoading)
            {
                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    var loadingText = new TextBlock
                    {
                        Text = LoadingMessage,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    loadingText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(loadingText, (ActualWidth - loadingText.DesiredSize.Width) / 2);
                    Canvas.SetTop(loadingText, (ActualHeight - loadingText.DesiredSize.Height) / 2);

                    Children.Add(loadingText);
                }
                return;
            }

            // Multi-frequency colored waveform rendering
            if (FrequencyWaveforms != null && FrequencyWaveforms.Any() && ActualWidth > 0 && ActualHeight > 0)
            {
                DrawMultiFrequencyWaveform();
                
                // Re-add playhead if it exists
                if (_playheadLine != null)
                {
                    Children.Add(_playheadLine);
                }
                return;
            }

            // Check if we have no frequencies selected (empty waveform data)
            if ((WaveformData == null || WaveformData.Length == 0 || WaveformData.All(v => v == 0)) && 
                (FrequencyWaveforms == null || !FrequencyWaveforms.Any()))
            {
                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    // Display message when no frequencies are selected
                    var emptyText = new TextBlock
                    {
                        Text = "No frequencies selected\nSelect frequencies from the list to view waveform",
                        FontSize = 14,
                        FontWeight = FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };

                    emptyText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(emptyText, (ActualWidth - emptyText.DesiredSize.Width) / 2);
                    Canvas.SetTop(emptyText, (ActualHeight - emptyText.DesiredSize.Height) / 2);

                    Children.Add(emptyText);
                }
                return;
            }

            // Fallback to single-color waveform
            if (WaveformData == null || WaveformData.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            var centerY = ActualHeight / 2;
            
            // Calculate visible data range based on zoom
            var startIndex = (int)(ZoomStartTime * WaveformData.Length);
            var endIndex = (int)(ZoomEndTime * WaveformData.Length);
            startIndex = Math.Clamp(startIndex, 0, WaveformData.Length - 1);
            endIndex = Math.Clamp(endIndex, startIndex + 1, WaveformData.Length);
            
            var visibleData = WaveformData[startIndex..endIndex];
            var maxAmplitude = visibleData.Max(Math.Abs);

            if (maxAmplitude == 0)
                return;

            var scaleY = (ActualHeight * 0.8) / 2; // Use 80% of height
            var pointsPerPixel = Math.Max(1, (int)(visibleData.Length / ActualWidth));

            // Use different colors based on filtering state
            var strokeBrush = IsFiltered ? _filteredWaveformBrush : _waveformBrush;
            var fillColor = IsFiltered ? Color.FromArgb(50, 76, 175, 80) : Color.FromArgb(50, 25, 118, 210);

            var path = new Path
            {
                Stroke = strokeBrush,
                StrokeThickness = IsFiltered ? 1.5 : 1,
                Fill = new SolidColorBrush(fillColor)
            };

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var points = new List<Point>();

                for (int x = 0; x < (int)ActualWidth; x++)
                {
                    var dataIndex = (int)(x * visibleData.Length / ActualWidth);
                    if (dataIndex >= visibleData.Length)
                        dataIndex = visibleData.Length - 1;

                    // Calculate RMS for smoother visualization
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(visibleData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var rms = 0.0;
                    var count = 0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        rms += visibleData[i] * visibleData[i];
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

                    // Draw top half
                    for (int i = 1; i < points.Count; i++)
                    {
                        context.LineTo(points[i], true, false);
                    }

                    // Draw bottom half (mirrored)
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

            // Re-add playhead if it exists
            if (_playheadLine != null)
            {
                Children.Add(_playheadLine);
            }
        }

        private void DrawMultiFrequencyWaveform()
        {
            if (FrequencyWaveforms == null || !FrequencyWaveforms.Any())
                return;

            var centerY = ActualHeight / 2;
            var scaleY = (ActualHeight * 0.8) / 2; // Use 80% of height

            // Find global max amplitude across all frequencies for consistent scaling
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

            // Draw each frequency with its own color
            foreach (var (frequency, freqData) in FrequencyWaveforms.OrderBy(kvp => kvp.Key))
            {
                if (freqData.WaveformData == null || freqData.WaveformData.Length == 0)
                    continue;

                DrawFrequencyWaveform(freqData, centerY, scaleY, globalMaxAmplitude);
            }
        }

        private void DrawFrequencyWaveform(FrequencyWaveformData freqData, double centerY, double scaleY, float globalMaxAmplitude)
        {
            var waveformData = freqData.WaveformData;
            if (waveformData == null || waveformData.Length == 0)
                return;

            // Calculate visible data range based on zoom
            var startIndex = (int)(ZoomStartTime * waveformData.Length);
            var endIndex = (int)(ZoomEndTime * waveformData.Length);
            startIndex = Math.Clamp(startIndex, 0, waveformData.Length - 1);
            endIndex = Math.Clamp(endIndex, startIndex + 1, waveformData.Length);
            
            var visibleData = waveformData[startIndex..endIndex];
            var pointsPerPixel = Math.Max(1, (int)(visibleData.Length / ActualWidth));
            
            // Create path with frequency-specific color
            var strokeBrush = new SolidColorBrush(freqData.Color);
            var fillColor = Color.FromArgb(60, freqData.Color.R, freqData.Color.G, freqData.Color.B);

            var path = new Path
            {
                Stroke = strokeBrush,
                StrokeThickness = 1.2,
                Fill = new SolidColorBrush(fillColor),
                Opacity = 0.7 // Slight transparency for overlapping frequencies
            };

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var points = new List<Point>();

                for (int x = 0; x < (int)ActualWidth; x++)
                {
                    var dataIndex = (int)(x * visibleData.Length / ActualWidth);
                    if (dataIndex >= visibleData.Length)
                        dataIndex = visibleData.Length - 1;

                    // Calculate RMS for smoother visualization
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(visibleData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var rms = 0.0;
                    var count = 0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        rms += visibleData[i] * visibleData[i];
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

                    // Draw top half
                    for (int i = 1; i < points.Count; i++)
                    {
                        context.LineTo(points[i], true, false);
                    }

                    // Draw bottom half (mirrored)
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

        private void UpdatePlayhead()
        {
            if (_playheadLine != null)
            {
                Children.Remove(_playheadLine);
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Convert playhead position to visible range
            if (PlayheadPosition < ZoomStartTime || PlayheadPosition > ZoomEndTime)
            {
                // Playhead is outside visible range
                return;
            }

            var visibleRange = ZoomEndTime - ZoomStartTime;
            var relativePosition = (PlayheadPosition - ZoomStartTime) / visibleRange;
            var x = relativePosition * ActualWidth;

            _playheadLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = ActualHeight,
                Stroke = _playheadBrush,
                StrokeThickness = 2
            };

            Children.Add(_playheadLine);
        }
    }

    /// <summary>
    /// Data structure for per-frequency waveform with color information
    /// </summary>
    public class FrequencyWaveformData
    {
        public double Frequency { get; set; }
        public float[] WaveformData { get; set; } = Array.Empty<float>();
        public Color Color { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event args for zoom region selection
    /// </summary>
    public class ZoomRegionSelectedEventArgs : EventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public ZoomRegionSelectedEventArgs(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }
}