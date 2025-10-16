using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AeroDebrief.UI.Views.Analytics
{
    /// <summary>
    /// Visualization of audio intensity over time
    /// </summary>
    public partial class PowerDistributionView : UserControl
    {
        public static readonly DependencyProperty HasDataProperty =
            DependencyProperty.Register(nameof(HasData), typeof(bool),
                typeof(PowerDistributionView), new PropertyMetadata(false));

        public bool HasData
        {
            get => (bool)GetValue(HasDataProperty);
            set => SetValue(HasDataProperty, value);
        }

        public PowerDistributionView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            DrawPlaceholder();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawPowerDistribution();
        }

        private void RedrawPowerDistribution()
        {
            PowerCanvas.Children.Clear();

            if (!HasData || PowerCanvas.ActualWidth <= 0 || PowerCanvas.ActualHeight <= 0)
            {
                DrawPlaceholder();
                return;
            }

            // Draw power distribution visualization
            DrawPowerBars();
        }

        private void DrawPlaceholder()
        {
            PowerCanvas.Children.Clear();

            if (PowerCanvas.ActualWidth <= 0 || PowerCanvas.ActualHeight <= 0)
                return;

            var text = new TextBlock
            {
                Text = "Power distribution visualization\n(Coming soon)",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };

            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, (PowerCanvas.ActualWidth - text.DesiredSize.Width) / 2);
            Canvas.SetTop(text, (PowerCanvas.ActualHeight - text.DesiredSize.Height) / 2);

            PowerCanvas.Children.Add(text);
        }

        private void DrawPowerBars()
        {
            // Placeholder implementation
            // TODO: Implement actual power distribution visualization based on audio data
        }
    }
}
