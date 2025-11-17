using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AeroDebrief.UI.Helpers;
using AeroDebrief.UI.Models;
using FontAwesome.WPF;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Enhanced minimap that combines waveform overview with transport controls.
    /// Provides comprehensive navigation, playback control, and timeline visualization.
    /// </summary>
    public class WaveformMiniMapWithTransport : Canvas
    {
        #region Dependency Properties

        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(null, OnWaveformDataChanged));

        public static readonly DependencyProperty FrequencyWaveformsProperty =
            DependencyProperty.Register(nameof(FrequencyWaveforms), typeof(Dictionary<double, FrequencyWaveformData>), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(null, OnFrequencyWaveformsChanged));

        public static readonly DependencyProperty ZoomStartTimeProperty =
            DependencyProperty.Register(nameof(ZoomStartTime), typeof(double), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(0.0, OnZoomRangeChanged, CoerceZoomStartTime));

        public static readonly DependencyProperty ZoomEndTimeProperty =
            DependencyProperty.Register(nameof(ZoomEndTime), typeof(double), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(1.0, OnZoomRangeChanged, CoerceZoomEndTime));

        public static readonly DependencyProperty PlayheadPositionProperty =
            DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(0.0, OnPlayheadPositionChanged));

        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register(nameof(TotalDuration), typeof(TimeSpan), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(TimeSpan.Zero, OnTotalDurationChanged));

        public static readonly DependencyProperty CurrentTimeProperty =
            DependencyProperty.Register(nameof(CurrentTime), typeof(TimeSpan), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(TimeSpan.Zero, OnCurrentTimeChanged));

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(false, OnPlayingStateChanged));

        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(WaveformMiniMapWithTransport),
                new PropertyMetadata(false, OnPlayingStateChanged));

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

        public TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        public TimeSpan CurrentTime
        {
            get => (TimeSpan)GetValue(CurrentTimeProperty);
            set => SetValue(CurrentTimeProperty, value);
        }

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<double>? SeekRequested;
        public event EventHandler<MiniMapClickEventArgs>? MinimapClicked;
        public event EventHandler<MiniMapDragEventArgs>? MinimapDragged;
        public event EventHandler? PlayRequested;
        public event EventHandler? PauseRequested;
        public event EventHandler? StopRequested;

        #endregion

        #region Private Fields

        private Grid? _rootGrid;
        private Canvas? _waveformCanvas;
        private Rectangle? _viewportIndicator;
        private Line? _playheadLine;
        private Button? _playPauseButton;
        private Button? _stopButton;
        private TextBlock? _currentTimeText;
        private TextBlock? _totalTimeText;
        private TextBlock? _viewportStartTime;
        private TextBlock? _viewportEndTime;

        private bool _isDraggingViewport;
        private bool _isResizingLeft;
        private bool _isResizingRight;
        private bool _isDraggingPlayhead;
        private Point _dragStartPoint;
        private double _dragStartZoomStart;
        private double _dragStartZoomEnd;
        private DateTime _lastDragEventTime = DateTime.MinValue;
        private const double ResizeEdgeThreshold = 8;
        private const int DragThrottleMs = 50;

        private readonly SolidColorBrush _waveformBrush = new(Color.FromRgb(25, 118, 210));
        private readonly SolidColorBrush _viewportBrush = new(Color.FromArgb(80, 25, 118, 210));
        private readonly SolidColorBrush _viewportBorderBrush = new(Color.FromRgb(25, 118, 210));
        private readonly SolidColorBrush _playheadBrush = new(Color.FromRgb(211, 47, 47));
        private readonly SolidColorBrush _backgroundBrush = new(Color.FromRgb(245, 245, 245));

        #endregion

        #region Constructor

        public WaveformMiniMapWithTransport()
        {
            InitializeComponent();
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            Background = _backgroundBrush;
            ClipToBounds = true;
            Height = 100; // Increased height to accommodate transport controls

            _rootGrid = new Grid();
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Transport buttons
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Waveform

            // Transport controls (centered on left side)
            var transportPanel = CreateTransportPanel();
            Grid.SetColumn(transportPanel, 0);
            _rootGrid.Children.Add(transportPanel);

            // Waveform canvas (takes right side)
            _waveformCanvas = new Canvas
            {
                Background = _backgroundBrush,
                ClipToBounds = true
            };
            Grid.SetColumn(_waveformCanvas, 1);
            _rootGrid.Children.Add(_waveformCanvas);

            Children.Add(_rootGrid);

            // Wire up events
            _waveformCanvas.SizeChanged += OnWaveformCanvasSizeChanged;
            _waveformCanvas.MouseDown += OnWaveformMouseDown;
            _waveformCanvas.MouseMove += OnWaveformMouseMove;
            _waveformCanvas.MouseUp += OnWaveformMouseUp;
            _waveformCanvas.MouseLeave += OnWaveformMouseLeave;

            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateTransportButtons();
            UpdateTotalTimeDisplay();
        }

        private StackPanel CreateTransportPanel()
        {
            // Panel with buttons centered vertically on the left
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Margin = new Thickness(8, 0, 8, 0)
            };

            // Play/Pause Button - Icon only style
            _playPauseButton = new Button
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Play/Pause"
            };

            // Create a style for icon-only buttons
            var iconButtonStyle = new Style(typeof(Button));
            iconButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            iconButtonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            iconButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(25, 118, 210)))); // Blue

            // Add hover effect
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 25, 118, 210)))); // Very subtle background
            hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243)))); // Lighter blue
            iconButtonStyle.Triggers.Add(hoverTrigger);

            // Add pressed effect
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 25, 118, 210)))); // Slightly more visible
            pressedTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(13, 71, 161)))); // Darker blue
            iconButtonStyle.Triggers.Add(pressedTrigger);

            _playPauseButton.Style = iconButtonStyle;

            // Set initial icon (will be updated in OnLoaded)
            _playPauseButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.Play, 24, new SolidColorBrush(Color.FromRgb(25, 118, 210)));

            _playPauseButton.Click += OnPlayPauseClick;
            panel.Children.Add(_playPauseButton);

            // Stop Button - Icon only style
            _stopButton = new Button
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Stop"
            };

            // Create style for stop button (same transparent style as play/pause)
            var stopButtonStyle = new Style(typeof(Button));
            stopButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            stopButtonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            stopButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            stopButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(211, 47, 47)))); // Red

            // Hover effect for stop button
            var stopHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            stopHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 211, 47, 47)))); // Very subtle red background
            stopHoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(229, 57, 53)))); // Lighter red
            stopButtonStyle.Triggers.Add(stopHoverTrigger);

            // Pressed effect for stop button
            var stopPressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            stopPressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 211, 47, 47)))); // Slightly more visible
            stopPressedTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(183, 28, 28)))); // Darker red
            stopButtonStyle.Triggers.Add(stopPressedTrigger);

            // Disabled effect for stop button
            var stopDisabledTrigger = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            stopDisabledTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(189, 189, 189)))); // Gray
            stopDisabledTrigger.Setters.Add(new Setter(Button.OpacityProperty, 0.5));
            stopButtonStyle.Triggers.Add(stopDisabledTrigger);

            _stopButton.Style = stopButtonStyle;

            // Set stop icon
            _stopButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.Stop, 24, new SolidColorBrush(Color.FromRgb(211, 47, 47)));

            _stopButton.Click += OnStopClick;
            panel.Children.Add(_stopButton);

            return panel;
        }

        #endregion

        #region Property Changed Handlers

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.RedrawWaveform();
            }
        }

        private static void OnFrequencyWaveformsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.RedrawWaveform();
            }
        }

        private static void OnZoomRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.UpdateViewportIndicator();
            }
        }

        private static object CoerceZoomStartTime(DependencyObject d, object baseValue)
        {
            if (d is WaveformMiniMapWithTransport control && baseValue is double value)
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (value >= control.ZoomEndTime)
                    return Math.Max(0.0, control.ZoomEndTime - 0.01);
                return value;
            }
            return baseValue;
        }

        private static object CoerceZoomEndTime(DependencyObject d, object baseValue)
        {
            if (d is WaveformMiniMapWithTransport control && baseValue is double value)
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (value <= control.ZoomStartTime)
                    return Math.Min(1.0, control.ZoomStartTime + 0.01);
                return value;
            }
            return baseValue;
        }

        private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.UpdatePlayhead();
            }
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.UpdateTotalTimeDisplay();
                control.UpdateViewportIndicator();
            }
        }

        private static void OnCurrentTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                // Current time is shown with playhead, so update it
                if (control._currentTimeText != null && control._waveformCanvas != null)
                {
                    control._currentTimeText.Text = control.FormatTime(control.CurrentTime);
                }
            }
        }

        private static void OnPlayingStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformMiniMapWithTransport control)
            {
                control.UpdateTransportButtons();
            }
        }

        #endregion

        #region Mouse Event Handlers

        private void OnWaveformMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0)
                return;

            var position = e.GetPosition(_waveformCanvas);
            var normalizedX = position.X / _waveformCanvas.ActualWidth;

            // Check if clicking on playhead
            if (_playheadLine != null)
            {
                var playheadX = PlayheadPosition * _waveformCanvas.ActualWidth;
                if (Math.Abs(position.X - playheadX) < 8)
                {
                    _isDraggingPlayhead = true;
                    _waveformCanvas.CaptureMouse();
                    _waveformCanvas.Cursor = Cursors.Hand;
                    return;
                }
            }

            // Check if clicking on viewport indicator
            if (_viewportIndicator != null)
            {
                var viewportLeft = Canvas.GetLeft(_viewportIndicator);
                var viewportRight = viewportLeft + _viewportIndicator.Width;

                if (Math.Abs(position.X - viewportLeft) < ResizeEdgeThreshold)
                {
                    _isResizingLeft = true;
                    _dragStartPoint = position;
                    _dragStartZoomStart = ZoomStartTime;
                    _dragStartZoomEnd = ZoomEndTime;
                    _waveformCanvas.CaptureMouse();
                    _waveformCanvas.Cursor = Cursors.SizeWE;
                    return;
                }
                else if (Math.Abs(position.X - viewportRight) < ResizeEdgeThreshold)
                {
                    _isResizingRight = true;
                    _dragStartPoint = position;
                    _dragStartZoomStart = ZoomStartTime;
                    _dragStartZoomEnd = ZoomEndTime;
                    _waveformCanvas.CaptureMouse();
                    _waveformCanvas.Cursor = Cursors.SizeWE;
                    return;
                }
                else if (position.X >= viewportLeft && position.X <= viewportRight)
                {
                    _isDraggingViewport = true;
                    _dragStartPoint = position;
                    _dragStartZoomStart = ZoomStartTime;
                    _dragStartZoomEnd = ZoomEndTime;
                    _waveformCanvas.CaptureMouse();
                    _waveformCanvas.Cursor = Cursors.SizeAll;
                    return;
                }
            }

            // Click outside viewport - center viewport on click position
            var zoomRange = ZoomEndTime - ZoomStartTime;
            var newStartTime = Math.Clamp(normalizedX - zoomRange / 2.0, 0.0, 1.0 - zoomRange);
            var newEndTime = newStartTime + zoomRange;

            MinimapClicked?.Invoke(this, new MiniMapClickEventArgs(newStartTime, newEndTime));
        }

        private void OnWaveformMouseMove(object sender, MouseEventArgs e)
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0)
                return;

            var currentPosition = e.GetPosition(_waveformCanvas);
            var now = DateTime.UtcNow;
            var shouldThrottle = (now - _lastDragEventTime).TotalMilliseconds < DragThrottleMs;

            // Handle playhead dragging
            if (_isDraggingPlayhead)
            {
                if (!shouldThrottle)
                {
                    _lastDragEventTime = now;
                    var normalizedPosition = Math.Clamp(currentPosition.X / _waveformCanvas.ActualWidth, 0.0, 1.0);
                    SeekRequested?.Invoke(this, normalizedPosition);
                }
                return;
            }

            // Handle viewport resizing
            if (_isResizingLeft)
            {
                if (!shouldThrottle)
                {
                    _lastDragEventTime = now;
                    var deltaX = currentPosition.X - _dragStartPoint.X;
                    var deltaNormalized = deltaX / _waveformCanvas.ActualWidth;
                    var newStartTime = Math.Clamp(_dragStartZoomStart + deltaNormalized, 0.0, _dragStartZoomEnd - 0.01);
                    MinimapDragged?.Invoke(this, new MiniMapDragEventArgs(newStartTime, _dragStartZoomEnd));
                }
                return;
            }

            if (_isResizingRight)
            {
                if (!shouldThrottle)
                {
                    _lastDragEventTime = now;
                    var deltaX = currentPosition.X - _dragStartPoint.X;
                    var deltaNormalized = deltaX / _waveformCanvas.ActualWidth;
                    var newEndTime = Math.Clamp(_dragStartZoomEnd + deltaNormalized, _dragStartZoomStart + 0.01, 1.0);
                    MinimapDragged?.Invoke(this, new MiniMapDragEventArgs(_dragStartZoomStart, newEndTime));
                }
                return;
            }

            // Handle viewport dragging
            if (_isDraggingViewport)
            {
                if (!shouldThrottle)
                {
                    _lastDragEventTime = now;
                    var deltaX = currentPosition.X - _dragStartPoint.X;
                    var deltaNormalized = deltaX / _waveformCanvas.ActualWidth;
                    var newStartTime = Math.Clamp(_dragStartZoomStart + deltaNormalized, 0.0, 1.0);
                    var newEndTime = Math.Clamp(_dragStartZoomEnd + deltaNormalized, 0.0, 1.0);

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
                return;
            }

            // Update cursor based on position
            if (_viewportIndicator != null)
            {
                var viewportLeft = Canvas.GetLeft(_viewportIndicator);
                var viewportRight = viewportLeft + _viewportIndicator.Width;

                if (Math.Abs(currentPosition.X - viewportLeft) < ResizeEdgeThreshold ||
                    Math.Abs(currentPosition.X - viewportRight) < ResizeEdgeThreshold)
                {
                    _waveformCanvas.Cursor = Cursors.SizeWE;
                }
                else if (currentPosition.X >= viewportLeft && currentPosition.X <= viewportRight)
                {
                    _waveformCanvas.Cursor = Cursors.SizeAll;
                }
                else if (_playheadLine != null && Math.Abs(currentPosition.X - PlayheadPosition * _waveformCanvas.ActualWidth) < 8)
                {
                    _waveformCanvas.Cursor = Cursors.Hand;
                }
                else
                {
                    _waveformCanvas.Cursor = Cursors.Arrow;
                }
            }
        }

        private void OnWaveformMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingViewport || _isResizingLeft || _isResizingRight || _isDraggingPlayhead)
            {
                _isDraggingViewport = false;
                _isResizingLeft = false;
                _isResizingRight = false;
                _isDraggingPlayhead = false;
                _waveformCanvas?.ReleaseMouseCapture();
                if (_waveformCanvas != null)
                    _waveformCanvas.Cursor = Cursors.Arrow;
            }
        }

        private void OnWaveformMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingViewport || _isResizingLeft || _isResizingRight || _isDraggingPlayhead)
            {
                _isDraggingViewport = false;
                _isResizingLeft = false;
                _isResizingRight = false;
                _isDraggingPlayhead = false;
                _waveformCanvas?.ReleaseMouseCapture();
                if (_waveformCanvas != null)
                    _waveformCanvas.Cursor = Cursors.Arrow;
            }
        }

        #endregion

        #region Transport Control Handlers

        private void OnPlayPauseClick(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
            {
                PauseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                PlayRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Size Changed Handlers

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Width = ActualWidth;
                _rootGrid.Height = ActualHeight;
            }
        }

        private void OnWaveformCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawWaveform();
        }

        #endregion

        #region Drawing Methods

        private void RedrawWaveform()
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0 || _waveformCanvas.ActualHeight <= 0)
                return;

            _waveformCanvas.Children.Clear();

            if (FrequencyWaveforms != null && FrequencyWaveforms.Any())
            {
                DrawMultiFrequencyOverview();
            }
            else if (WaveformData != null && WaveformData.Length > 0)
            {
                DrawSingleWaveformOverview();
            }

            UpdateViewportIndicator();
            UpdatePlayhead();
            UpdateTotalTimeDisplay();
        }

        private void DrawSingleWaveformOverview()
        {
            if (WaveformData == null || WaveformData.Length == 0 || _waveformCanvas == null)
                return;

            var centerY = _waveformCanvas.ActualHeight / 2;
            var maxAmplitude = WaveformData.Max(Math.Abs);

            if (maxAmplitude == 0)
                return;

            var scaleY = (_waveformCanvas.ActualHeight * 0.7) / 2;
            var pointsPerPixel = Math.Max(1, (int)(WaveformData.Length / _waveformCanvas.ActualWidth));

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

                for (int x = 0; x < (int)_waveformCanvas.ActualWidth; x++)
                {
                    var dataIndex = (int)(x * WaveformData.Length / _waveformCanvas.ActualWidth);
                    if (dataIndex >= WaveformData.Length)
                        dataIndex = WaveformData.Length - 1;

                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(WaveformData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var maxValue = 0.0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(WaveformData[i]));
                    }

                    var normalizedAmplitude = maxValue / maxAmplitude;
                    var y = centerY - (normalizedAmplitude * scaleY);
                    points.Add(new Point(x, y));
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
            _waveformCanvas.Children.Add(path);
        }

        private void DrawMultiFrequencyOverview()
        {
            if (FrequencyWaveforms == null || !FrequencyWaveforms.Any() || _waveformCanvas == null)
                return;

            var centerY = _waveformCanvas.ActualHeight / 2;
            var scaleY = (_waveformCanvas.ActualHeight * 0.7) / 2;

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
            if (waveformData == null || waveformData.Length == 0 || _waveformCanvas == null)
                return;

            var pointsPerPixel = Math.Max(1, (int)(waveformData.Length / _waveformCanvas.ActualWidth));

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

                for (int x = 0; x < (int)_waveformCanvas.ActualWidth; x++)
                {
                    var dataIndex = (int)(x * waveformData.Length / _waveformCanvas.ActualWidth);
                    if (dataIndex >= waveformData.Length)
                        dataIndex = waveformData.Length - 1;

                    var startIdx = Math.Max(0, dataIndex - pointsPerPixel / 2);
                    var endIdx = Math.Min(waveformData.Length - 1, dataIndex + pointsPerPixel / 2);

                    var maxValue = 0.0;
                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(waveformData[i]));
                    }

                    var normalizedAmplitude = maxValue / globalMaxAmplitude;
                    var y = centerY - (normalizedAmplitude * scaleY);
                    points.Add(new Point(x, y));
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
            _waveformCanvas.Children.Add(path);
        }

        private void UpdateViewportIndicator()
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0 || _waveformCanvas.ActualHeight <= 0)
                return;

            // Remove existing viewport elements
            if (_viewportIndicator != null)
            {
                _waveformCanvas.Children.Remove(_viewportIndicator);
                _viewportIndicator = null;
            }
            if (_viewportStartTime != null)
            {
                _waveformCanvas.Children.Remove(_viewportStartTime);
                _viewportStartTime = null;
            }
            if (_viewportEndTime != null)
            {
                _waveformCanvas.Children.Remove(_viewportEndTime);
                _viewportEndTime = null;
            }

            // Only show viewport if zoomed in
            if (ZoomStartTime <= 0.0 && ZoomEndTime >= 1.0)
                return;

            if (ZoomEndTime <= ZoomStartTime)
                return;

            var leftX = ZoomStartTime * _waveformCanvas.ActualWidth;
            var rightX = ZoomEndTime * _waveformCanvas.ActualWidth;
            var width = rightX - leftX;

            if (width <= 0)
                return;

            // Create viewport rectangle
            _viewportIndicator = new Rectangle
            {
                Fill = _viewportBrush,
                Stroke = _viewportBorderBrush,
                StrokeThickness = 2,
                Width = width,
                Height = _waveformCanvas.ActualHeight,
                Cursor = Cursors.SizeAll
            };

            Canvas.SetLeft(_viewportIndicator, leftX);
            Canvas.SetTop(_viewportIndicator, 0);
            _waveformCanvas.Children.Add(_viewportIndicator);

            // Add time labels
            if (TotalDuration.TotalSeconds > 0)
            {
                var startTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * ZoomStartTime));
                var endTime = TimeSpan.FromTicks((long)(TotalDuration.Ticks * ZoomEndTime));

                _viewportStartTime = new TextBlock
                {
                    Text = startTime.ToString(@"mm\:ss\.f"),
                    FontSize = 8,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Colors.White),
                    Background = _viewportBorderBrush,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                Canvas.SetLeft(_viewportStartTime, leftX + 2);
                Canvas.SetTop(_viewportStartTime, 2);
                _waveformCanvas.Children.Add(_viewportStartTime);

                _viewportEndTime = new TextBlock
                {
                    Text = endTime.ToString(@"mm\:ss\.f"),
                    FontSize = 8,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Colors.White),
                    Background = _viewportBorderBrush,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                _viewportEndTime.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var endTimeWidth = _viewportEndTime.DesiredSize.Width;
                Canvas.SetLeft(_viewportEndTime, rightX - endTimeWidth - 2);
                Canvas.SetTop(_viewportEndTime, 2);
                _waveformCanvas.Children.Add(_viewportEndTime);
            }
        }

        private void UpdatePlayhead()
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0 || _waveformCanvas.ActualHeight <= 0)
                return;

            // Remove existing playhead and current time
            if (_playheadLine != null)
            {
                _waveformCanvas.Children.Remove(_playheadLine);
                _playheadLine = null;
            }
            if (_currentTimeText != null)
            {
                _waveformCanvas.Children.Remove(_currentTimeText);
                _currentTimeText = null;
            }

            var normalizedPosition = PlayheadPosition;
            if (normalizedPosition > 1.0)
                normalizedPosition = normalizedPosition / 100.0;

            var x = normalizedPosition * _waveformCanvas.ActualWidth;

            // Draw playhead line
            _playheadLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = _waveformCanvas.ActualHeight,
                Stroke = _playheadBrush,
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            _waveformCanvas.Children.Add(_playheadLine);

            // Add current time text at bottom of playhead
            _currentTimeText = new TextBlock
            {
                Text = FormatTime(CurrentTime),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = _playheadBrush,
                Padding = new Thickness(4, 2, 4, 2)
            };

            // Measure text to center it on the playhead
            _currentTimeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = _currentTimeText.DesiredSize.Width;
            var textHeight = _currentTimeText.DesiredSize.Height;

            // Position at bottom of canvas, centered on playhead
            var textLeft = x - (textWidth / 2);
            
            // Keep text within canvas bounds
            textLeft = Math.Max(2, Math.Min(textLeft, _waveformCanvas.ActualWidth - textWidth - 2));

            Canvas.SetLeft(_currentTimeText, textLeft);
            Canvas.SetBottom(_currentTimeText, 2);
            _waveformCanvas.Children.Add(_currentTimeText);
        }

        private void UpdateTimeDisplay()
        {
            // Update current time (attached to playhead)
            UpdatePlayhead();
            
            // Update total time (bottom right)
            UpdateTotalTimeDisplay();
        }

        private void UpdateTotalTimeDisplay()
        {
            if (_waveformCanvas == null || _waveformCanvas.ActualWidth <= 0 || _waveformCanvas.ActualHeight <= 0)
                return;

            // Remove existing total time
            if (_totalTimeText != null)
            {
                _waveformCanvas.Children.Remove(_totalTimeText);
                _totalTimeText = null;
            }

            // Create total time text at bottom right
            _totalTimeText = new TextBlock
            {
                Text = FormatTime(TotalDuration),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(4, 2, 4, 2)
            };

            _totalTimeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = _totalTimeText.DesiredSize.Width;

            Canvas.SetRight(_totalTimeText, 4);
            Canvas.SetBottom(_totalTimeText, 2);
            _waveformCanvas.Children.Add(_totalTimeText);
        }

        private void UpdateTransportButtons()
        {
            if (_playPauseButton == null || _stopButton == null)
                return;

            Dispatcher.Invoke(() =>
            {
                // Update play/pause icon with appropriate color
                var playPauseColor = new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Blue
                if (_playPauseButton.IsMouseOver)
                {
                    playPauseColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Lighter blue
                }

                _playPauseButton.Content = IsPlaying
                    ? IconHelper.CreateFaIcon(FontAwesomeIcon.Pause, 24, playPauseColor)
                    : IconHelper.CreateFaIcon(FontAwesomeIcon.Play, 24, playPauseColor);

                // Update stop button icon with appropriate color
                var stopColor = _stopButton.IsEnabled 
                    ? new SolidColorBrush(Color.FromRgb(211, 47, 47))  // Red
                    : new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Gray when disabled

                _stopButton.Content = IconHelper.CreateFaIcon(FontAwesomeIcon.Stop, 24, stopColor);
                _stopButton.IsEnabled = IsPlaying || IsPaused;
            });
        }

        private string FormatTime(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        #endregion
    }
}
