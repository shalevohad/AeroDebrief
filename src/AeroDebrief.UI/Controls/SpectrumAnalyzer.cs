using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Controls
{
    public class SpectrumAnalyzer : Canvas
    {
        public static readonly DependencyProperty SpectrumDataProperty =
            DependencyProperty.Register(nameof(SpectrumData), typeof(SpectrumData), typeof(SpectrumAnalyzer),
                new PropertyMetadata(null, OnSpectrumDataChanged));

        public static readonly DependencyProperty ShowOnlySelectedFrequenciesProperty =
            DependencyProperty.Register(nameof(ShowOnlySelectedFrequencies), typeof(bool), typeof(SpectrumAnalyzer),
                new PropertyMetadata(false, OnShowOnlySelectedFrequenciesChanged));

        public static readonly DependencyProperty SelectedFrequenciesProperty =
            DependencyProperty.Register(nameof(SelectedFrequencies), typeof(HashSet<double>), typeof(SpectrumAnalyzer),
                new PropertyMetadata(null, OnSelectedFrequenciesChanged));

        public SpectrumData? SpectrumData
        {
            get => (SpectrumData?)GetValue(SpectrumDataProperty);
            set => SetValue(SpectrumDataProperty, value);
        }

        public bool ShowOnlySelectedFrequencies
        {
            get => (bool)GetValue(ShowOnlySelectedFrequenciesProperty);
            set => SetValue(ShowOnlySelectedFrequenciesProperty, value);
        }

        public HashSet<double>? SelectedFrequencies
        {
            get => (HashSet<double>?)GetValue(SelectedFrequenciesProperty);
            set => SetValue(SelectedFrequenciesProperty, value);
        }

        private readonly SolidColorBrush _spectrumBrush = new(Color.FromRgb(56, 142, 60)); // Green
        private readonly SolidColorBrush _gridBrush = new(Color.FromRgb(224, 224, 224)); // Light gray

        public SpectrumAnalyzer()
        {
            Background = Brushes.White;
            ClipToBounds = true;
            SizeChanged += OnSizeChanged;
        }

        private static void OnSpectrumDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumAnalyzer analyzer)
            {
                analyzer.RedrawSpectrum();
            }
        }

        private static void OnShowOnlySelectedFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumAnalyzer analyzer)
            {
                analyzer.RedrawSpectrum();
            }
        }

        private static void OnSelectedFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumAnalyzer analyzer)
            {
                analyzer.RedrawSpectrum();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawSpectrum();
        }

        private void RedrawSpectrum()
        {
            Children.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            DrawGrid();

            if (SpectrumData?.Magnitudes == null || SpectrumData.Magnitudes.Length == 0)
            {
                DrawPlaceholder();
                return;
            }

            DrawSpectrumBars();
        }

        private void DrawGrid()
        {
            // Horizontal grid lines (dB levels)
            var dbLevels = new[] { -60, -40, -20, -10, -6, -3, 0 };

            foreach (var dbLevel in dbLevels)
            {
                var y = ActualHeight - ((dbLevel + 60) / 60.0 * ActualHeight);

                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = ActualWidth,
                    Y2 = y,
                    Stroke = _gridBrush,
                    StrokeThickness = dbLevel == 0 ? 1.5 : 0.5
                };

                Children.Add(line);

                // Add dB label
                var label = new TextBlock
                {
                    Text = $"{dbLevel} dB",
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    Background = Brushes.White
                };

                Canvas.SetLeft(label, 4);
                Canvas.SetTop(label, y - 8);
                Children.Add(label);
            }

            // Vertical grid lines (frequency markers)
            if (SpectrumData?.SampleRate > 0)
            {
                var nyquist = SpectrumData.SampleRate / 2;
                var freqMarkers = new[] { 0, nyquist / 4, nyquist / 2, 3 * nyquist / 4, nyquist };

                foreach (var freq in freqMarkers)
                {
                    var x = freq / nyquist * ActualWidth;

                    var line = new Line
                    {
                        X1 = x,
                        Y1 = 0,
                        X2 = x,
                        Y2 = ActualHeight,
                        Stroke = _gridBrush,
                        StrokeThickness = 0.5
                    };

                    Children.Add(line);

                    // Add frequency label
                    var label = new TextBlock
                    {
                        Text = freq >= 1000 ? $"{freq / 1000:F1}k" : $"{freq:F0}",
                        FontSize = 9,
                        Foreground = Brushes.Gray,
                        Background = Brushes.White
                    };

                    Canvas.SetLeft(label, x + 2);
                    Canvas.SetBottom(label, 2);
                    Children.Add(label);
                }
            }
        }

        private void DrawSpectrumBars()
        {
            if (SpectrumData?.Magnitudes == null)
                return;

            var barWidth = ActualWidth / SpectrumData.Magnitudes.Length;
            var maxMagnitude = SpectrumData.Magnitudes.Max();

            if (maxMagnitude <= 0)
                return;

            for (int i = 0; i < SpectrumData.Magnitudes.Length; i++)
            {
                var magnitude = SpectrumData.Magnitudes[i];
                
                // Apply frequency filtering if enabled
                if (ShowOnlySelectedFrequencies && SelectedFrequencies?.Count > 0)
                {
                    var frequency = i * SpectrumData.SampleRate / (SpectrumData.Magnitudes.Length * 2);
                    var isNearSelectedFreq = SelectedFrequencies.Any(f => Math.Abs(f - frequency) < 10000); // Within 10kHz
                    
                    if (!isNearSelectedFreq)
                    {
                        magnitude *= 0.1f; // Dim non-selected frequencies
                    }
                }

                var dbValue = magnitude > 0 ? 20 * Math.Log10(magnitude / maxMagnitude) : -60;
                var normalizedHeight = Math.Max(0, (dbValue + 60) / 60.0);

                var barHeight = normalizedHeight * ActualHeight;
                var x = i * barWidth;
                var y = ActualHeight - barHeight;

                // Use different colors for selected vs non-selected frequencies
                var brush = _spectrumBrush;
                if (ShowOnlySelectedFrequencies && SelectedFrequencies?.Count > 0)
                {
                    var frequency = i * SpectrumData.SampleRate / (SpectrumData.Magnitudes.Length * 2);
                    var isNearSelectedFreq = SelectedFrequencies.Any(f => Math.Abs(f - frequency) < 10000);
                    brush = isNearSelectedFreq ? _spectrumBrush : new SolidColorBrush(Color.FromRgb(180, 180, 180));
                }

                var bar = new Rectangle
                {
                    Width = Math.Max(1, barWidth - 0.5),
                    Height = barHeight,
                    Fill = brush
                };

                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                Children.Add(bar);
            }
        }

        private void DrawPlaceholder()
        {
            var placeholder = new TextBlock
            {
                Text = "No spectrum data available\nPlay audio to see real-time spectrum analysis",
                FontSize = 14,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(placeholder, (ActualWidth - 300) / 2);
            Canvas.SetTop(placeholder, (ActualHeight - 40) / 2);
            Children.Add(placeholder);
        }
    }
}