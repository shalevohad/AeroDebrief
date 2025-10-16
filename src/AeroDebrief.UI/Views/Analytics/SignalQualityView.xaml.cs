using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AeroDebrief.UI.Views.Analytics
{
    /// <summary>
    /// Line chart showing signal clarity metrics
    /// </summary>
    public partial class SignalQualityView : UserControl
    {
        public static readonly DependencyProperty HasDataProperty =
            DependencyProperty.Register(nameof(HasData), typeof(bool),
                typeof(SignalQualityView), new PropertyMetadata(false));

        public bool HasData
        {
            get => (bool)GetValue(HasDataProperty);
            set => SetValue(HasDataProperty, value);
        }

        public SignalQualityView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            DrawPlaceholder();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawSignalQuality();
        }

        private void RedrawSignalQuality()
        {
            SignalCanvas.Children.Clear();

            if (!HasData || SignalCanvas.ActualWidth <= 0 || SignalCanvas.ActualHeight <= 0)
            {
                DrawPlaceholder();
                return;
            }

            // Draw signal quality chart
            DrawQualityChart();
        }

        private void DrawPlaceholder()
        {
            SignalCanvas.Children.Clear();

            if (SignalCanvas.ActualWidth <= 0 || SignalCanvas.ActualHeight <= 0)
                return;

            var text = new TextBlock
            {
                Text = "Signal quality visualization\n(Coming soon)",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };

            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, (SignalCanvas.ActualWidth - text.DesiredSize.Width) / 2);
            Canvas.SetTop(text, (SignalCanvas.ActualHeight - text.DesiredSize.Height) / 2);

            SignalCanvas.Children.Add(text);
        }

        private void DrawQualityChart()
        {
            // Placeholder implementation
            // TODO: Implement actual signal quality chart based on audio metrics
        }
    }
}
