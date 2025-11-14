using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Windows.Media.Imaging;  // Phase 3.1: Re-enabled for GPU compositor
using Vortice.Direct3D11;  // Phase 3.1: Re-enabled for GPU compositor
using AeroDebrief.Core.Audio; // For BufferedRegion class

namespace AeroDebrief.UI.Controls
{
    public class WaveformViewer : Canvas
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
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

        public static readonly DependencyProperty GenerationProgressProperty =
            DependencyProperty.Register(nameof(GenerationProgress), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnGenerationProgressChanged));

        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(nameof(FrequencyWaveforms), typeof(Dictionary<double, FrequencyWaveformData>), typeof(WaveformViewer),
                new PropertyMetadata(null, OnFrequencyWaveformsChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(nameof(ZoomStartTime), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnZoomChanged));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(nameof(ZoomEndTime), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(1.0, OnZoomChanged));

        public static readonly DependencyProperty BufferStartPositionProperty =
            DependencyProperty.Register(nameof(BufferStartPosition), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnBufferPositionChanged));

        public static readonly DependencyProperty BufferEndPositionProperty =
            DependencyProperty.Register(nameof(BufferEndPosition), typeof(double), typeof(WaveformViewer),
                new PropertyMetadata(0.0, OnBufferPositionChanged));

        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(nameof(TotalDuration), typeof(TimeSpan), typeof(WaveformViewer),
                new PropertyMetadata(TimeSpan.Zero, OnTotalDurationChanged));

        public static readonly DependencyProperty BufferedRegionsProperty =
            DependencyProperty.Register(nameof(BufferedRegions), typeof(List<AeroDebrief.Core.Audio.BufferedRegion>), typeof(WaveformViewer),
                new PropertyMetadata(null, OnBufferedRegionsChanged));

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

        public double GenerationProgress
        {
            get => (double)GetValue(GenerationProgressProperty);
            set => SetValue(GenerationProgressProperty, value);
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

        public TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        public List<AeroDebrief.Core.Audio.BufferedRegion>? BufferedRegions
        {
            get => (List<AeroDebrief.Core.Audio.BufferedRegion>?)GetValue(BufferedRegionsProperty);
            set => SetValue(BufferedRegionsProperty, value);
        }

        public event EventHandler<double>? SeekRequested;
        public event EventHandler<ZoomRegionSelectedEventArgs>? ZoomRegionSelected;

        private Line? _playheadLine;
        private TextBlock? _playheadTimeText;
        private Rectangle? _selectionRectangle;
        private Rectangle? _bufferIndicator; // Legacy - replaced with buffered regions
        private List<Rectangle> _bufferedRegionRectangles = new(); // NEW: Multiple buffer regions
        private List<UIElement> _timelineElements = new();
        private Point? _selectionStartPoint;
        private bool _isSelecting;
        private bool _isDraggingPlayhead;
        private DateTime _lastSeekTime = DateTime.MinValue;
        private const double PlayheadDragThreshold = 8; // Pixels from playhead to consider as drag area
        private const int SeekThrottleMs = 50; // Throttle seek events to max 20 per second
        private readonly SolidColorBrush _waveformBrush = new(Color.FromRgb(25, 118, 210)); // Blue
        private readonly SolidColorBrush _filteredWaveformBrush = new(Color.FromRgb(76, 175, 80)); // Green for filtered
        private readonly SolidColorBrush _playheadBrush = new(Color.FromRgb(211, 47, 47)); // Red
        private readonly SolidColorBrush _selectionBrush = new(Color.FromArgb(60, 25, 118, 210)); // Semi-transparent blue
        private readonly SolidColorBrush _selectionBorderBrush = new(Color.FromRgb(255, 255, 255)); // White border
        private readonly SolidColorBrush _bufferBrush = new(Color.FromArgb(40, 76, 175, 80)); // Semi-transparent green for buffer
        private readonly SolidColorBrush _bufferBorderBrush = new(Color.FromArgb(120, 76, 175, 80)); // Green border for buffer

        // Phase 3.1: GPU output texture cache (Re-enabled)
        private ID3D11Texture2D? _gpuCompositeTexture;
        private WriteableBitmap? _compositeBitmap;

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

        private static void OnGenerationProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                // Update loading message with progress
                if (viewer.IsLoading)
                {
                    viewer.LoadingMessage = $"Generating waveform... {viewer.GenerationProgress:F0}%";
                }
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
                viewer.UpdateBufferIndicator(); // Legacy buffer indicator
                viewer.UpdateBufferedRegionsOverlay(); // NEW: Update buffered regions overlay
                viewer.UpdateTimeline();
            }
        }

        private static void OnBufferPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.UpdateBufferIndicator();
            }
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                // Update total duration display if needed
                viewer.UpdateTimeline();
            }
        }

        private static void OnBufferedRegionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformViewer viewer)
            {
                viewer.UpdateBufferedRegionsOverlay();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawWaveform();
            UpdatePlayhead();
            UpdateBufferIndicator();
            UpdateTimeline();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive || ActualWidth <= 0)
                return;

            var position = e.GetPosition(this);

            // Check if Ctrl is pressed for selection mode (zoom)
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
                // Start playhead dragging - click anywhere to scrub
                _isDraggingPlayhead = true;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                
                // Immediately seek to clicked position
                var normalizedPosition = Math.Clamp(position.X / ActualWidth, 0.0, 1.0);
                var visibleRange = ZoomEndTime - ZoomStartTime;
                var seekPosition = ZoomStartTime + (normalizedPosition * visibleRange);
                SeekRequested?.Invoke(this, Math.Clamp(seekPosition, 0.0, 1.0));
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var currentPosition = e.GetPosition(this);

            // Handle playhead dragging (scrubbing) with throttling
            if (_isDraggingPlayhead)
            {
                // Throttle seek events to prevent overwhelming the audio system
                var now = DateTime.UtcNow;
                if ((now - _lastSeekTime).TotalMilliseconds < SeekThrottleMs)
                {
                    return; // Skip this seek event
                }
                _lastSeekTime = now;

                var normalizedPosition = Math.Clamp(currentPosition.X / ActualWidth, 0.0, 1.0);
                
                // Convert to visible time range
                var visibleRange = ZoomEndTime - ZoomStartTime;
                var seekPosition = ZoomStartTime + (normalizedPosition * visibleRange);
                
                SeekRequested?.Invoke(this, Math.Clamp(seekPosition, 0.0, 1.0));
                return;
            }

            // Handle selection rectangle dragging
            if (_isSelecting && _selectionStartPoint != null && _selectionRectangle != null)
            {
                var startX = _selectionStartPoint.Value.X;
                var currentX = currentPosition.X;

                // Update selection rectangle
                var left = Math.Min(startX, currentX);
                var width = Math.Abs(currentX - startX);

                Canvas.SetLeft(_selectionRectangle, left);
                _selectionRectangle.Width = width;
                return;
            }

            // Default cursor when not dragging
            if (!_isDraggingPlayhead && !_isSelecting)
            {
                Cursor = Cursors.Hand;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            // Handle playhead drag end
            if (_isDraggingPlayhead)
            {
                _isDraggingPlayhead = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Hand;
                return;
            }

            // Handle selection end
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

                // Calculate new start and end times within the visible range
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
            // Cancel playhead dragging on mouse leave
            if (_isDraggingPlayhead)
            {
                _isDraggingPlayhead = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Hand;
            }

            // Cancel selection on mouse leave
            if (_isSelecting)
            {
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
                // Note: DrawMultiFrequencyWaveform now handles playhead re-adding internally
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
            // Defensive guards to avoid ArgumentOutOfRange from Math.Clamp when lengths are zero
            var dataLen = WaveformData.Length;
            if (dataLen <= 0) return;

            var startIndex = (int)(ZoomStartTime * dataLen);
            var endIndex = (int)(ZoomEndTime * dataLen);

            // Ensure clamp bounds are valid - CRITICAL FIX: use proper min/max values
            // Math.Clamp requires: min <= max, so we must ensure startIndex <= endIndex before clamping
            startIndex = Math.Clamp(startIndex, 0, dataLen - 1);
            
            // CRITICAL: Ensure endIndex is at least startIndex+1 but no more than dataLen
            // First clamp with proper bounds, then ensure minimum of startIndex+1
            endIndex = Math.Max(startIndex + 1, Math.Min(endIndex, dataLen));
            
            // Safeguard: if endIndex <= startIndex after clamping, we can't slice
            if (endIndex <= startIndex)
            {
                // Try to give at least 1 sample to render
                endIndex = Math.Min(startIndex + 1, dataLen);
                if (endIndex <= startIndex)
                {
                    // Still can't create a valid range - bail out
                    return;
                }
            }

            // CRITICAL FIX: Check slice bounds before creating slice
            if (startIndex < 0 || endIndex > dataLen || startIndex >= endIndex)
            {
                Logger.Error($"Invalid slice indices: start={startIndex}, end={endIndex}, dataLen={dataLen}");
                return;
            }

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

                    // CRITICAL FIX: Use max value instead of RMS
                    // The data is ALREADY RMS from GPU shader, so we just need the peak in this window
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(visibleData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var maxValue = 0.0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(visibleData[i]));
                    }

                    var normalizedAmplitude = maxValue / maxAmplitude;
                    var y = centerY - (normalizedAmplitude * scaleY);
                    points.Add(new Point(x, y));
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

            // Re-add playhead if it exists (for fallback single-color waveform)
            if (_playheadLine != null && !Children.Contains(_playheadLine))
            {
                Children.Add(_playheadLine);
            }
        }

        private void DrawMultiFrequencyWaveform()
        {
            if (FrequencyWaveforms == null || !FrequencyWaveforms.Any())
                return;

            // NEW: Check if we have GPU layers (each layer has its own texture)
            var allLayers = FrequencyWaveforms.Values
                .Where(f => f.LayerId != Guid.Empty)
                .ToList();

            if (allLayers.Any())
            {
                // Separate visible (ready) layers from pending ones
                var visibleLayers = allLayers.Where(l => l.IsVisible).ToList();
                var totalExpectedLayers = allLayers.Count;
                
                Logger.Debug($"[WaveformViewer] Rendering {visibleLayers.Count}/{totalExpectedLayers} GPU layers (progressive rendering)");
                
                // CRITICAL: Draw ALL available visible layers immediately, regardless of total count
                if (visibleLayers.Any())
                {
                    foreach (var layer in visibleLayers.OrderBy(l => l.Frequency))
                    {
                        DrawGpuLayer(layer);
                    }
                }
                
                // Show loading overlay if not all layers are ready yet
                if (visibleLayers.Count < totalExpectedLayers && ActualWidth > 0 && ActualHeight > 0)
                {
                    // Semi-transparent overlay
                    var overlay = new Rectangle
                    {
                        Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                        Width = ActualWidth,
                        Height = ActualHeight
                    };
                    Children.Add(overlay);
                    
                    // Loading text with progress
                    var loadingText = new TextBlock
                    {
                        Text = $"Loading waveform layers...\n{visibleLayers.Count} of {totalExpectedLayers} ready",
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };

                    loadingText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(loadingText, (ActualWidth - loadingText.DesiredSize.Width) / 2);
                    Canvas.SetTop(loadingText, (ActualHeight - loadingText.DesiredSize.Height) / 2);

                    Children.Add(loadingText);
                    
                    Logger.Debug($"[WaveformViewer] Showing loading overlay: {visibleLayers.Count}/{totalExpectedLayers} layers ready");
                }
                else if (visibleLayers.Count == totalExpectedLayers)
                {
                    Logger.Debug($"[WaveformViewer] All {totalExpectedLayers} GPU layers rendered - loading complete");
                }
                
                // Re-add playhead on top of everything (layers + overlay)
                // Remove first to avoid "already a child" exception
                if (_playheadLine != null)
                {
                    Children.Remove(_playheadLine);
                    Children.Add(_playheadLine);
                }
                
                if (_playheadTimeText != null)
                {
                    Children.Remove(_playheadTimeText);
                    Children.Add(_playheadTimeText);
                }
                
                return;
            }

            // Fallback: CPU rendering with overlaid waveforms
            var centerY = ActualHeight / 2;
            var scaleY = (ActualHeight * 0.8) / 2;

            // Find global max amplitude across all visible frequencies for consistent scaling
            var globalMaxAmplitude = 0.0f;
            foreach (var freqData in FrequencyWaveforms.Values.Where(f => f.IsVisible))
            {
                if (freqData.WaveformData != null && freqData.WaveformData.Length > 0)
                {
                    var localMax = freqData.WaveformData.Max(Math.Abs);
                    if (localMax > globalMaxAmplitude)
                        globalMaxAmplitude = localMax;
                }
            }

            if (globalMaxAmplitude == 0)
            {
                // All frequencies are silent or hidden
                if (ActualWidth > 0 && ActualHeight > 0)
                {
                    var silentText = new TextBlock
                    {
                        Text = "No visible frequencies\nSelect frequencies to view waveform",
                        FontSize = 14,
                        FontWeight = FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };

                    silentText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(silentText, (ActualWidth - silentText.DesiredSize.Width) / 2);
                    Canvas.SetTop(silentText, (ActualHeight - silentText.DesiredSize.Height) / 2);

                    Children.Add(silentText);
                }
                return;
            }

            // Draw each visible frequency with its own color (CPU fallback)
            foreach (var (frequency, freqData) in FrequencyWaveforms.OrderBy(kvp => kvp.Key))
            {
                if (!freqData.IsVisible)
                    continue;
                
                if (freqData.WaveformData == null || freqData.WaveformData.Length == 0)
                    continue;

                DrawFrequencyWaveform(freqData, centerY, scaleY, globalMaxAmplitude);
            }
        }

        /// <summary>
        /// Draws a single GPU layer texture (Phase 3: Individual layer rendering)
        /// Each layer is drawn separately for instant visibility toggling
        /// </summary>
        private void DrawGpuLayer(FrequencyWaveformData layerData)
        {
            if (layerData.WaveformData == null || layerData.WaveformData.Length == 0)
                return;

            Logger.Debug($"[WaveformViewer] Drawing GPU layer: {layerData.DisplayName} (LayerId: {layerData.LayerId})");

            // For now, draw using CPU data (cached from GPU)
            // TODO Phase 3.2: Direct GPU texture rendering via WriteableBitmap or D3DImage
            var centerY = ActualHeight / 2;
            var scaleY = (ActualHeight * 0.8) / 2;

            // Calculate visible data range based on zoom
            var waveformData = layerData.WaveformData;
            var dataLen = waveformData.Length;
            if (dataLen <= 0 || ActualWidth <= 0) return;

            var startIndex = (int)(ZoomStartTime * dataLen);
            var endIndex = (int)(ZoomEndTime * dataLen);

            var maxStart = Math.Max(0, dataLen - 1);
            startIndex = Math.Clamp(startIndex, 0, maxStart);

            endIndex = Math.Clamp(endIndex, startIndex + 1, dataLen);
            if (endIndex <= startIndex)
            {
                endIndex = Math.Min(startIndex + 1, dataLen);
                if (endIndex <= startIndex) return;
            }
            
            var visibleData = waveformData[startIndex..endIndex];
            var pointsPerPixel = Math.Max(1, (int)(visibleData.Length / ActualWidth));
            
            // Find max amplitude for this layer
            var maxAmplitude = visibleData.Max(Math.Abs);
            if (maxAmplitude == 0)
                return;

            // Create path with frequency-specific color
            var strokeBrush = new SolidColorBrush(layerData.Color);
            var fillColor = Color.FromArgb(60, layerData.Color.R, layerData.Color.G, layerData.Color.B);

            var path = new Path
            {
                Stroke = strokeBrush,
                StrokeThickness = 1.2,
                Fill = new SolidColorBrush(fillColor),
                Opacity = 0.7 // Transparency for overlaying multiple layers
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

                    // Find peak amplitude in this pixel's window
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(visibleData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var maxValue = 0.0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(visibleData[i]));
                    }

                    var normalizedAmplitude = maxValue / maxAmplitude;
                    var y = centerY - (normalizedAmplitude * scaleY);
                    points.Add(new Point(x, y));
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

        private void DrawFrequencyWaveform(FrequencyWaveformData freqData, double centerY, double scaleY, float globalMaxAmplitude)
        {
            var waveformData = freqData.WaveformData;
            if (waveformData == null || waveformData.Length == 0 || ActualWidth <= 0)
                return;

            // Calculate visible data range based on zoom
            var dataLen = waveformData.Length;
            if (dataLen <= 0) return;

            var startIndex = (int)(ZoomStartTime * dataLen);
            var endIndex = (int)(ZoomEndTime * dataLen);

            var maxStart = Math.Max(0, dataLen - 1);
            startIndex = Math.Clamp(startIndex, 0, maxStart);
            endIndex = Math.Clamp(endIndex, startIndex + 1, dataLen);
            if (endIndex <= startIndex)
            {
                endIndex = Math.Min(startIndex + 1, dataLen);
                if (endIndex <= startIndex) return;
            }

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

                    // Find peak amplitude in this pixel's window
                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(visibleData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var maxValue = 0.0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(visibleData[i]));
                    }

                    var normalizedAmplitude = maxValue / globalMaxAmplitude;
                    var y = centerY - (normalizedAmplitude * scaleY);
                    points.Add(new Point(x, y));
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
                _playheadLine = null;
            }

            if (_playheadTimeText != null)
            {
                Children.Remove(_playheadTimeText);
                _playheadTimeText = null;
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Convert from percentage (0-100) to normalized (0-1) if needed
            var normalizedPosition = PlayheadPosition;
            if (normalizedPosition > 1.0)
            {
                normalizedPosition = normalizedPosition / 100.0;
            }

            // Check if playhead is within visible zoom range
            if (normalizedPosition < ZoomStartTime || normalizedPosition > ZoomEndTime)
            {
                // Playhead is outside visible range
                return;
            }

            var visibleRange = ZoomEndTime - ZoomStartTime;
            var relativePosition = (normalizedPosition - ZoomStartTime) / visibleRange;
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

            // Add current playtime display at the top of the playhead line
            if (TotalDuration.TotalSeconds > 0)
            {
                // Calculate current time
                var currentTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * normalizedPosition));
                var timeString = currentTime.ToString(@"hh\:mm\:ss\.f");

                _playheadTimeText = new TextBlock
                {
                    Text = timeString,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Background = _playheadBrush,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                // Measure the text size
                _playheadTimeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = _playheadTimeText.DesiredSize.Width;

                // Position at the top of the playhead, centered on the line
                var textX = x - (textWidth / 2);
                
                // Keep text within bounds
                if (textX < 2)
                    textX = 2;
                else if (textX + textWidth > ActualWidth - 2)
                    textX = ActualWidth - textWidth - 2;

                Canvas.SetLeft(_playheadTimeText, textX);
                Canvas.SetTop(_playheadTimeText, 2);

                Children.Add(_playheadTimeText);
            }
        }

        private void UpdateBufferIndicator()
        {
            if (_bufferIndicator != null)
            {
                Children.Remove(_bufferIndicator);
                _bufferIndicator = null;
            }

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // Only show buffer if we have valid buffer positions
            if (BufferStartPosition <= 0 && BufferEndPosition <= 0)
                return;

            // Convert buffer positions (0-1 normalized) to screen coordinates considering zoom
            var bufferStart = Math.Max(BufferStartPosition, ZoomStartTime);
            var bufferEnd = Math.Min(BufferEndPosition, ZoomEndTime);

            // Only draw if buffer intersects with visible range
            if (bufferStart >= ZoomEndTime || bufferEnd <= ZoomStartTime)
                return;

            var visibleRange = ZoomEndTime - ZoomStartTime;
            var relativeStart = (bufferStart - ZoomStartTime) / visibleRange;
            var relativeEnd = (bufferEnd - ZoomStartTime) / visibleRange;

            var startX = relativeStart * ActualWidth;
            var endX = relativeEnd * ActualWidth;
            var width = endX - startX;

            if (width > 0)
            {
                _bufferIndicator = new Rectangle
                {
                    Fill = _bufferBrush,
                    Stroke = _bufferBorderBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Width = width,
                    Height = ActualHeight
                };

                Canvas.SetLeft(_bufferIndicator, startX);
                Canvas.SetTop(_bufferIndicator, 0);

                // Add buffer indicator before playhead (so playhead is on top)
                var playheadIndex = _playheadLine != null ? Children.IndexOf(_playheadLine) : Children.Count;
                if (playheadIndex >= 0)
                {
                    Children.Insert(Math.Max(0, playheadIndex), _bufferIndicator);
                }
                else
                {
                    Children.Add(_bufferIndicator);
                }
            }
        }

        private void UpdateTotalDuration()
        {
            // TODO: Implement total duration display logic (if required)
        }

        private void UpdateTimeline()
        {
            // Remove existing timeline elements
            foreach (var element in _timelineElements)
            {
                Children.Remove(element);
            }
            _timelineElements.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0 || TotalDuration.TotalSeconds == 0)
                return;

            var visibleRange = ZoomEndTime - ZoomStartTime;
            var visibleDuration = TimeSpan.FromTicks((long)(TotalDuration.Ticks * visibleRange));

            // Calculate appropriate time interval for markers
            var intervalSeconds = CalculateTimeInterval(visibleDuration.TotalSeconds, ActualWidth);
            if (intervalSeconds == 0)
                return;

            var startTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * ZoomStartTime));
            var endTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * ZoomEndTime));

            // Round start time to nearest interval
            var firstMarkerSeconds = Math.Ceiling(startTime.TotalSeconds / intervalSeconds) * intervalSeconds;
            
            // Create time markers
            var timeTextBrush = new SolidColorBrush(Color.FromRgb(96, 96, 96));
            var tickBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));

            for (double seconds = firstMarkerSeconds; seconds <= endTime.TotalSeconds; seconds += intervalSeconds)
            {
                var markerTime = TimeSpan.FromSeconds(seconds);
                var normalizedTime = markerTime.Ticks / (double)TotalDuration.Ticks;

                // Check if within visible range
                if (normalizedTime < ZoomStartTime || normalizedTime > ZoomEndTime)
                    continue;

                // Calculate x position
                var relativePosition = (normalizedTime - ZoomStartTime) / visibleRange;
                var x = relativePosition * ActualWidth;

                // Draw tick mark
                var tick = new Line
                {
                    X1 = x,
                    Y1 = 20,
                    X2 = x,
                    Y2 = ActualHeight,
                    Stroke = tickBrush,
                    StrokeThickness = 0.5,
                    Opacity = 0.3
                };
                Children.Add(tick);
                _timelineElements.Add(tick);

                // Draw time label
                var timeString = markerTime.ToString(@"hh\:mm\:ss");
                var timeText = new TextBlock
                {
                    Text = timeString,
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = timeTextBrush,
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    Padding = new Thickness(3, 1, 3, 1)
                };

                timeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = timeText.DesiredSize.Width;
                var textX = x - (textWidth / 2);

                // Keep text within bounds
                if (textX < 2)
                    textX = 2;
                else if (textX + textWidth > ActualWidth - 2)
                    textX = ActualWidth - textWidth - 2;

                Canvas.SetLeft(timeText, textX);
                Canvas.SetTop(timeText, 2);

                Children.Add(timeText);
                _timelineElements.Add(timeText);
            }
        }

        private double CalculateTimeInterval(double visibleSeconds, double pixelWidth)
        {
            // Calculate how many seconds per pixel
            var secondsPerPixel = visibleSeconds / pixelWidth;

            // Target: one marker every 100-150 pixels
            var targetSecondsPerMarker = secondsPerPixel * 120;

            // Choose appropriate interval (in seconds)
            double[] intervals = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };

            foreach (var interval in intervals)
            {
                if (interval >= targetSecondsPerMarker)
                    return interval;
            }

            // For very long durations, use hour intervals
            return Math.Ceiling(targetSecondsPerMarker / 3600) * 3600;
        }

        private void UpdateBufferedRegionsOverlay()
        {
            // Remove existing buffered region rectangles
            foreach (var rect in _bufferedRegionRectangles)
            {
                Children.Remove(rect);
            }
            _bufferedRegionRectangles.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0 || TotalDuration.TotalSeconds <= 0)
                return;

            if (BufferedRegions == null || BufferedRegions.Count == 0)
                return;

            // Draw each buffered region as a greenish overlay
            foreach (var region in BufferedRegions)
            {
                // Convert region to normalized positions
                var regionStart = region.GetNormalizedStart(TotalDuration);
                var regionEnd = region.GetNormalizedEnd(TotalDuration);

                // Check if region intersects with visible zoom range
                if (regionEnd < ZoomStartTime || regionStart > ZoomEndTime)
                    continue; // Region not visible

                // Clamp to visible range
                var visibleStart = Math.Max(regionStart, ZoomStartTime);
                var visibleEnd = Math.Min(regionEnd, ZoomEndTime);

                // Convert to screen coordinates
                var visibleRange = ZoomEndTime - ZoomStartTime;
                var relativeStart = (visibleStart - ZoomStartTime) / visibleRange;
                var relativeEnd = (visibleEnd - ZoomStartTime) / visibleRange;

                var startX = relativeStart * ActualWidth;
                var endX = relativeEnd * ActualWidth;
                var width = endX - startX;

                if (width > 0)
                {
                    var bufferRect = new Rectangle
                    {
                        Fill = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80)), // Semi-transparent green
                        Stroke = new SolidColorBrush(Color.FromArgb(100, 76, 175, 80)), // Green border
                        StrokeThickness = 0.5,
                        Width = width,
                        Height = ActualHeight
                    };

                    Canvas.SetLeft(bufferRect, startX);
                    Canvas.SetTop(bufferRect, 0);

                    // Add buffer indicator before playhead (so playhead stays on top)
                    var playheadIndex = _playheadLine != null ? Children.IndexOf(_playheadLine) : Children.Count;
                    if (playheadIndex >= 0)
                    {
                        Children.Insert(Math.Max(0, playheadIndex), bufferRect);
                    }
                    else
                    {
                        Children.Add(bufferRect);
                    }

                    _bufferedRegionRectangles.Add(bufferRect);
                }
            }
        }

        /// <summary>
        /// Sets GPU-composited waveform texture (Phase 3.1)
        /// </summary>
        public void SetGpuCompositeTexture(ID3D11Texture2D? texture)
        {
            _gpuCompositeTexture = texture;
            
            if (texture != null)
            {
                try
                {
                    // Convert GPU texture to WPF bitmap
                    _compositeBitmap = ConvertD3D11TextureToWpfBitmap(texture);
                    InvalidateVisual();
                }
                catch (Exception ex)
                {
                    var logger = NLog.LogManager.GetCurrentClassLogger();
                    logger.Warn(ex, "Failed to convert GPU texture to WPF bitmap");
                    _compositeBitmap = null;
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            
            // Phase 3.1: Render GPU composite if available
            if (_compositeBitmap != null && AeroDebrief.Core.Constants.USE_GPU_COMPOSITOR)
            {
                dc.DrawImage(_compositeBitmap, new Rect(0, 0, ActualWidth, ActualHeight));
                
                // Re-add playhead if it exists
                if (_playheadLine != null)
                {
                    // Playhead is drawn in separate rendering pass
                }
                return;
            }
            
            // Fallback: Phase 2 CPU rendering
            // ...existing rendering code follows...
        }

        private WriteableBitmap? ConvertD3D11TextureToWpfBitmap(ID3D11Texture2D texture)
        {
            try
            {
                // Get texture description
                var desc = texture.Description;
                
                // Get D3D11 device and context from texture
                var device = texture.Device;
                var context = device.ImmediateContext;
                
                // Create staging texture for CPU readback
                var stagingDesc = new Texture2DDescription
                {
                    Width = desc.Width,
                    Height = desc.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = desc.Format,
                    SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read,
                    MiscFlags = ResourceOptionFlags.None
                };
                
                using var stagingTexture = device.CreateTexture2D(stagingDesc);
                
                // Copy GPU texture to staging texture
                context.CopyResource(stagingTexture, texture);
                
                // Map staging texture and copy to WPF bitmap
                var mappedResource = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                
                var bitmap = new WriteableBitmap(
                    desc.Width, 
                    desc.Height, 
                    96, 96, 
                    PixelFormats.Bgra32, 
                    null);
                
                bitmap.Lock();
                
                try
                {
                    unsafe
                    {
                        var srcPtr = (byte*)mappedResource.DataPointer.ToPointer();
                        var dstPtr = (byte*)bitmap.BackBuffer.ToPointer();
                        var rowPitch = desc.Width * 4; // RGBA8 = 4 bytes per pixel
                        
                        for (int y = 0; y < desc.Height; y++)
                        {
                            Buffer.MemoryCopy(
                                srcPtr + y * mappedResource.RowPitch,
                                dstPtr + y * rowPitch,
                                rowPitch,
                                rowPitch);
                        }
                    }
                    
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, desc.Width, desc.Height));
                }
                finally
                {
                    bitmap.Unlock();
                }
                
                context.Unmap(stagingTexture, 0);
                
                return bitmap;
            }
            catch (Exception ex)
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Failed to convert D3D11 texture to WPF bitmap");
                return null;
            }
        }
    }

    /// <summary>
    /// Data structure for per-frequency waveform with GPU support (Phase 3.1)
    /// </summary>
    public class FrequencyWaveformData
    {
        public double Frequency { get; set; }
        public float[] WaveformData { get; set; } = Array.Empty<float>();
        public Color Color { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// GPU layer ID (Guid.Empty if not using GPU layers)
        /// </summary>
        public Guid LayerId { get; set; } = Guid.Empty;
        
        /// <summary>
        /// Whether this layer is visible
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        // Phase 3.1: GPU compositor support
        /// <summary>
        /// GPU composite texture (Phase 3.1)
        /// </summary>
        public ID3D11Texture2D? GpuCompositeTexture { get; set; }
        
        /// <summary>
        /// Whether this is a GPU-composited result (not individual frequency)
        /// </summary>
        public bool IsGpuComposite { get; set; }
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