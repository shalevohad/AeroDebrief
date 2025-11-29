using System;
using System.Collections.Generic;
using LiveChartsCore;

namespace AeroDebrief.UI.Interfaces.Visualization
{
    /// <summary>
    /// Chart rendering contract for unified waveform visualization.
    /// Abstracts the underlying charting library (LiveCharts2).
    /// 
    /// Minimal API for phases 0-2 of visualization pipeline:
    /// - Initialize chart host
    /// - Set series data
    /// - Control viewport
    /// - Update playhead position
    /// </summary>
    public interface IUnifiedChartRenderer : IDisposable
    {
        void Initialize(object chartHost);
        void SetSeries(IEnumerable<ISeries> series);
        void SetViewport(DateTime min, DateTime max);
        void UpdatePlayhead(DateTime time);
    }
}
