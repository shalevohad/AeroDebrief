using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using SkiaSharp;

namespace AeroDebrief.UI.Charts
{
    /// <summary>
    /// LiveCharts2 implementation of the unified chart renderer.
    /// Phase 1: Basic series management with optimized performance settings.
    /// </summary>
    public sealed class LiveChartsUnifiedChartRenderer : IUnifiedChartRenderer
    {
        private CartesianChart? _chart;
        private readonly List<ISeries> _series = new();
        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public void Initialize(object chartHost)
        {
            _chart = chartHost as CartesianChart;
            
            if (_chart != null)
            {
                // Apply performance optimizations
                _chart.AnimationsSpeed = TimeSpan.Zero;
                _chart.EasingFunction = null;
                _chart.TooltipPosition = TooltipPosition.Hidden;
                _chart.LegendPosition = LegendPosition.Hidden;
                
                // Configure axes
                ConfigureAxes();
                
                _logger.Info("LiveChartsUnifiedChartRenderer initialized");
            }
            else
            {
                _logger.Warn("Failed to initialize LiveChartsUnifiedChartRenderer: chart host is not CartesianChart");
            }
        }

        private void ConfigureAxes()
        {
            if (_chart == null) return;

            // Set a default time range to prevent invalid tick calculations during initialization
            var now = DateTime.Now;
            var defaultMin = now.AddMinutes(-1).Ticks;
            var defaultMax = now.Ticks;

            // X Axis: DateTime
            _chart.XAxes = new[]
            {
                new Axis
                {
                    Name = "Time",
                    LabelsRotation = 0,
                    TextSize = 12,
                    Labeler = value => 
                    {
                        // Validate the value is within valid DateTime range
                        long ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                        {
                            return string.Empty;
                        }
                        return new DateTime(ticks).ToString("HH:mm:ss");
                    },
                    MinStep = TimeSpan.FromSeconds(1).Ticks,
                    MinLimit = defaultMin,
                    MaxLimit = defaultMax
                }
            };

            // Y Axis: Amplitude in dB
            _chart.YAxes = new[]
            {
                new Axis
                {
                    Name = "Amplitude (dBFS)",
                    TextSize = 12,
                    MinLimit = -120,
                    MaxLimit = 0
                }
            };
        }

        public void SetSeries(IEnumerable<ISeries> series)
        {
            if (_chart == null) return;

            _series.Clear();
            _series.AddRange(series);
            _chart.Series = _series;
            
            _logger.Debug($"Set {_series.Count} series in renderer");
        }

        public void SetViewport(DateTime min, DateTime max)
        {
            if (_chart?.XAxes == null || !_chart.XAxes.Any()) return;

            var xAxis = _chart.XAxes.First();
            xAxis.MinLimit = min.Ticks;
            xAxis.MaxLimit = max.Ticks;
            
            _logger.Debug($"Set viewport: {min:HH:mm:ss} to {max:HH:mm:ss}");
        }

        public void UpdatePlayhead(DateTime time)
        {
            // Phase 1: Stub - will be implemented in Phase 6
            // TODO: Render vertical line at time position
        }

        public void Dispose()
        {
            _series.Clear();
            _chart = null;
            _logger.Debug("LiveChartsUnifiedChartRenderer disposed");
        }
    }
}
