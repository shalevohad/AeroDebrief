using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Views.Analytics
{
    /// <summary>
    /// Visualizes real-time presence of users connected to various military communication frequencies
    /// </summary>
    public partial class PresenceGraphView : UserControl
    {
        public static readonly DependencyProperty FrequencyPresenceDataProperty =
            DependencyProperty.Register(nameof(FrequencyPresenceData), typeof(ObservableCollection<FrequencyPresenceViewModel>),
                typeof(PresenceGraphView), new PropertyMetadata(null, OnFrequencyPresenceDataChanged));

        public static readonly DependencyProperty ShowGroupHighlightProperty =
            DependencyProperty.Register(nameof(ShowGroupHighlight), typeof(bool),
                typeof(PresenceGraphView), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowTooltipsProperty =
            DependencyProperty.Register(nameof(ShowTooltips), typeof(bool),
                typeof(PresenceGraphView), new PropertyMetadata(true));

        public static readonly DependencyProperty HasDataProperty =
            DependencyProperty.Register(nameof(HasData), typeof(bool),
                typeof(PresenceGraphView), new PropertyMetadata(false));

        public ObservableCollection<FrequencyPresenceViewModel>? FrequencyPresenceData
        {
            get => (ObservableCollection<FrequencyPresenceViewModel>?)GetValue(FrequencyPresenceDataProperty);
            set => SetValue(FrequencyPresenceDataProperty, value);
        }

        public bool ShowGroupHighlight
        {
            get => (bool)GetValue(ShowGroupHighlightProperty);
            set => SetValue(ShowGroupHighlightProperty, value);
        }

        public bool ShowTooltips
        {
            get => (bool)GetValue(ShowTooltipsProperty);
            set => SetValue(ShowTooltipsProperty, value);
        }

        public bool HasData
        {
            get => (bool)GetValue(HasDataProperty);
            set => SetValue(HasDataProperty, value);
        }

        private readonly Dictionary<string, UIElement> _nodeElements = new();
        private ToolTip? _currentTooltip;

        public PresenceGraphView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;

            // Start animation timer for smooth updates
            CompositionTarget.Rendering += OnRendering;
        }

        private static void OnFrequencyPresenceDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PresenceGraphView view)
            {
                view.HasData = e.NewValue != null && ((ObservableCollection<FrequencyPresenceViewModel>)e.NewValue).Count > 0;
                view.RedrawPresenceGraph();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawPresenceGraph();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            // Update animations and pulsing effects
            UpdateNodeAnimations();
        }

        private void RedrawPresenceGraph()
        {
            PresenceCanvas.Children.Clear();
            _nodeElements.Clear();

            if (FrequencyPresenceData == null || FrequencyPresenceData.Count == 0)
                return;

            var canvasWidth = PresenceCanvas.ActualWidth;
            var canvasHeight = PresenceCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            // Calculate spacing between frequencies
            var frequencyCount = FrequencyPresenceData.Count;
            var horizontalSpacing = canvasWidth / (frequencyCount + 1);

            for (int i = 0; i < frequencyCount; i++)
            {
                var frequency = FrequencyPresenceData[i];
                var centerX = horizontalSpacing * (i + 1);
                var centerY = canvasHeight / 2;

                // Draw frequency node
                DrawFrequencyNode(frequency, centerX, centerY);

                // Draw user nodes around frequency
                DrawUserNodes(frequency, centerX, centerY);
            }
        }

        private void DrawFrequencyNode(FrequencyPresenceViewModel frequency, double centerX, double centerY)
        {
            const double nodeRadius = 35;

            // Outer glow ellipse
            var glowEllipse = new Ellipse
            {
                Width = nodeRadius * 2.4,
                Height = nodeRadius * 2.4,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(60, 0, 122, 204), 0.0),
                        new GradientStop(Color.FromArgb(0, 0, 122, 204), 1.0)
                    }
                }
            };
            Canvas.SetLeft(glowEllipse, centerX - nodeRadius * 1.2);
            Canvas.SetTop(glowEllipse, centerY - nodeRadius * 1.2);
            PresenceCanvas.Children.Add(glowEllipse);

            // Main node circle
            var nodeEllipse = new Ellipse
            {
                Width = nodeRadius * 2,
                Height = nodeRadius * 2,
                Fill = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(nodeEllipse, centerX - nodeRadius);
            Canvas.SetTop(nodeEllipse, centerY - nodeRadius);
            PresenceCanvas.Children.Add(nodeEllipse);

            // Frequency label
            var label = new TextBlock
            {
                Text = $"{frequency.FrequencyMHz:F3} MHz",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, centerX - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centerY - label.DesiredSize.Height / 2);
            PresenceCanvas.Children.Add(label);
        }

        private void DrawUserNodes(FrequencyPresenceViewModel frequency, double centerX, double centerY)
        {
            if (frequency.Users == null || frequency.Users.Count == 0)
                return;

            const double orbitRadius = 100;
            const double userNodeRadius = 20;

            var userCount = frequency.Users.Count;
            var angleStep = 360.0 / userCount;

            for (int i = 0; i < userCount; i++)
            {
                var user = frequency.Users[i];
                var angle = angleStep * i * Math.PI / 180.0;
                var userX = centerX + orbitRadius * Math.Cos(angle);
                var userY = centerY + orbitRadius * Math.Sin(angle);

                // Draw connection line
                var line = new Line
                {
                    X1 = centerX,
                    Y1 = centerY,
                    X2 = userX,
                    Y2 = userY,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 64, 64, 64)),
                    StrokeThickness = 1.5
                };
                PresenceCanvas.Children.Add(line);

                // Draw user node
                DrawUserNode(user, userX, userY, userNodeRadius);
            }
        }

        private void DrawUserNode(UserNodeViewModel user, double x, double y, double radius)
        {
            var nodeKey = $"{user.UserName}_{x}_{y}";

            // Determine node color based on state
            var nodeColor = user.IsTalking
                ? Color.FromRgb(255, 255, 255) // White when talking
                : (user.IsInGroup && ShowGroupHighlight
                    ? ParseGroupColor(user.GroupColor)
                    : Color.FromRgb(180, 180, 180)); // Light gray inactive

            var scale = user.IsTalking ? 1.2 : 1.0;

            // Glow effect for talking users
            if (user.IsTalking)
            {
                var glowEllipse = new Ellipse
                {
                    Width = radius * 2 * scale * 1.5,
                    Height = radius * 2 * scale * 1.5,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(80, 255, 255, 255), 0.0),
                            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0)
                        }
                    }
                };
                Canvas.SetLeft(glowEllipse, x - radius * scale * 1.5);
                Canvas.SetTop(glowEllipse, y - radius * scale * 1.5);
                PresenceCanvas.Children.Add(glowEllipse);
            }

            // Main user node
            var userEllipse = new Ellipse
            {
                Width = radius * 2 * scale,
                Height = radius * 2 * scale,
                Fill = new SolidColorBrush(nodeColor),
                Stroke = user.IsTalking
                    ? new SolidColorBrush(Color.FromRgb(0, 255, 0))
                    : new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = user.IsTalking ? 3 : 1.5,
                Tag = user
            };

            Canvas.SetLeft(userEllipse, x - radius * scale);
            Canvas.SetTop(userEllipse, y - radius * scale);

            // Add tooltip
            if (ShowTooltips)
            {
                userEllipse.ToolTip = CreateUserTooltip(user);
            }

            // Handle join/leave animations
            if (user.IsJustJoined)
            {
                AnimateJoin(userEllipse);
            }
            else if (user.IsJustLeft)
            {
                AnimateLeave(userEllipse);
            }

            PresenceCanvas.Children.Add(userEllipse);
            _nodeElements[nodeKey] = userEllipse;

            // User name label (optional, for close zoom)
            if (radius > 15)
            {
                var nameLabel = new TextBlock
                {
                    Text = user.UserName.Length > 10 ? user.UserName.Substring(0, 10) + "..." : user.UserName,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 9,
                    FontWeight = FontWeights.Normal,
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(4, 2, 4, 2)
                };
                nameLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(nameLabel, x - nameLabel.DesiredSize.Width / 2);
                Canvas.SetTop(nameLabel, y + radius * scale + 5);
                PresenceCanvas.Children.Add(nameLabel);
            }
        }

        private ToolTip CreateUserTooltip(UserNodeViewModel user)
        {
            var tooltipGrid = new Grid();
            tooltipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tooltipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tooltipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = user.UserName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(nameText, 0);
            tooltipGrid.Children.Add(nameText);

            if (user.IsInGroup)
            {
                var groupText = new TextBlock
                {
                    Text = $"Group: {user.GroupColor}",
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                Grid.SetRow(groupText, 1);
                tooltipGrid.Children.Add(groupText);
            }

            var statusText = new TextBlock
            {
                Text = user.IsTalking ? "?? Speaking" : "Listening",
                FontSize = 10,
                Foreground = user.IsTalking
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
            Grid.SetRow(statusText, 2);
            tooltipGrid.Children.Add(statusText);

            return new ToolTip
            {
                Content = tooltipGrid,
                Background = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
        }

        private void AnimateJoin(Ellipse ellipse)
        {
            // Fade-in + scale-up animation
            ellipse.Opacity = 0;
            ellipse.RenderTransform = new ScaleTransform(0.1, 0.1, ellipse.Width / 2, ellipse.Height / 2);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.0));
            var scaleAnimation = new DoubleAnimation(0.1, 1.2, TimeSpan.FromSeconds(1.0))
            {
                EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 5 }
            };

            ellipse.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((ScaleTransform)ellipse.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            ((ScaleTransform)ellipse.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private void AnimateLeave(Ellipse ellipse)
        {
            // Red glow + fade-out animation
            var colorAnimation = new ColorAnimation
            {
                To = Color.FromRgb(255, 0, 0),
                Duration = TimeSpan.FromSeconds(0.5)
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.2));

            ((SolidColorBrush)ellipse.Fill).BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            ellipse.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void UpdateNodeAnimations()
        {
            // Update pulsing effects for talking users
            // This would be called on CompositionTarget.Rendering for smooth animation
        }

        private Color ParseGroupColor(string? colorString)
        {
            if (string.IsNullOrEmpty(colorString))
                return Color.FromRgb(180, 180, 180);

            try
            {
                return (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return Color.FromRgb(180, 180, 180);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Handle hover effects
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // Clear hover effects
        }
    }
}
