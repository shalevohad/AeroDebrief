using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using AeroDebrief.UI.Charts;
using AeroDebrief.Core.Models;
using SkiaSharp;

namespace AeroDebrief.UI.Helpers
{
    /// <summary>
    /// Helper class for integrating pilot markers into WPF UI.
    /// Phase 4: Bridges PilotMarkers (SkiaSharp) with WPF visualization.
    /// </summary>
    public static class PilotMarkerHelper
    {
        /// <summary>
        /// Gets a WPF Path element for a pilot's marker.
        /// Converts SKPath to WPF PathGeometry for UI rendering.
        /// </summary>
        /// <param name="player">Player to get marker for</param>
        /// <param name="size">Size in pixels (default 12)</param>
        /// <param name="color">Fill color (optional, uses frequency color if null)</param>
        /// <returns>WPF Path element ready for display</returns>
        public static Path GetPilotMarkerPath(PlayerFrequencyInfo player, double size = 12.0, Brush? color = null)
        {
            var pilotId = player.PilotId;
            var skPath = PilotMarkers.GetMarkerForPilot(pilotId, (float)size);
            
            // Convert SKPath to WPF PathGeometry
            var pathGeometry = ConvertSkPathToWpf(skPath);
            
            return new Path
            {
                Data = pathGeometry,
                Fill = color ?? Brushes.Gray,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5,
                Width = size * 2,
                Height = size * 2,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Gets a small marker icon for display in lists/legends (8px).
        /// </summary>
        public static Path GetSmallMarkerIcon(PlayerFrequencyInfo player, Brush? color = null)
        {
            return GetPilotMarkerPath(player, size: 8.0, color: color);
        }

        /// <summary>
        /// Gets a medium marker icon for frequency tree display (12px).
        /// </summary>
        public static Path GetMediumMarkerIcon(PlayerFrequencyInfo player, Brush? color = null)
        {
            return GetPilotMarkerPath(player, size: 12.0, color: color);
        }

        /// <summary>
        /// Converts SkiaSharp SKPath to WPF PathGeometry.
        /// Simplified conversion for basic shapes.
        /// </summary>
        private static PathGeometry ConvertSkPathToWpf(SKPath skPath)
        {
            var pathGeometry = new PathGeometry();
            
            // Get path bounds for scaling
            var bounds = skPath.Bounds;
            
            // Use path description string for simple conversion
            // This is a simplified approach that works for basic geometries
            var pathFigure = new PathFigure();
            
            using (var iter = skPath.CreateRawIterator())
            {
                SKPoint[] points = new SKPoint[4];
                SKPathVerb verb;
                bool firstPoint = true;

                while ((verb = iter.Next(points)) != SKPathVerb.Done)
                {
                    switch (verb)
                    {
                        case SKPathVerb.Move:
                            pathFigure = new PathFigure
                            {
                                StartPoint = new Point(points[0].X, points[0].Y),
                                IsClosed = false
                            };
                            pathGeometry.Figures.Add(pathFigure);
                            firstPoint = false;
                            break;

                        case SKPathVerb.Line:
                            if (!firstPoint)
                            {
                                pathFigure.Segments.Add(new LineSegment(
                                    new Point(points[1].X, points[1].Y), true));
                            }
                            break;

                        case SKPathVerb.Quad:
                            if (!firstPoint)
                            {
                                pathFigure.Segments.Add(new QuadraticBezierSegment(
                                    new Point(points[1].X, points[1].Y),
                                    new Point(points[2].X, points[2].Y),
                                    true));
                            }
                            break;

                        case SKPathVerb.Cubic:
                            if (!firstPoint)
                            {
                                pathFigure.Segments.Add(new BezierSegment(
                                    new Point(points[1].X, points[1].Y),
                                    new Point(points[2].X, points[2].Y),
                                    new Point(points[3].X, points[3].Y),
                                    true));
                            }
                            break;

                        case SKPathVerb.Close:
                            if (pathFigure != null)
                            {
                                pathFigure.IsClosed = true;
                            }
                            break;
                    }
                }
            }

            return pathGeometry;
        }

        /// <summary>
        /// Creates a simple circle marker as fallback if SKPath conversion fails.
        /// </summary>
        private static PathGeometry CreateCircleFallback(double radius)
        {
            var geometry = new EllipseGeometry(new Point(0, 0), radius, radius);
            return PathGeometry.CreateFromGeometry(geometry);
        }

        /// <summary>
        /// Gets the marker type count (32+) for UI display.
        /// </summary>
        public static int AvailableMarkerTypes => PilotMarkers.MarkerTypeCount;

        /// <summary>
        /// Gets a frequency color as WPF Brush.
        /// </summary>
        public static SolidColorBrush GetFrequencyColorBrush(double frequencyHz, string modulation)
        {
            var frequencyMHz = frequencyHz / 1_000_000.0;
            var frequencyId = $"{frequencyMHz:F1}-{modulation}";
            var skColor = ChartColors.GetColorForFrequency(frequencyId);
            
            return new SolidColorBrush(Color.FromArgb(
                skColor.Alpha,
                skColor.Red,
                skColor.Green,
                skColor.Blue
            ));
        }

        /// <summary>
        /// Gets a pilot marker with frequency color applied.
        /// </summary>
        public static Path GetColoredPilotMarker(PlayerFrequencyInfo player, double frequencyHz, string modulation, double size = 12.0)
        {
            var colorBrush = GetFrequencyColorBrush(frequencyHz, modulation);
            return GetPilotMarkerPath(player, size, colorBrush);
        }
    }
}
