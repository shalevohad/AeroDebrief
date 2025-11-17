using System;
using System.Collections.Generic;
using LiveChartsCore;

namespace AeroDebrief.UI.Charts
{
    // Rendering abstraction for unified chart. Minimal API for phases 0-2.
    public interface IUnifiedChartRenderer : IDisposable
    {
        void Initialize(object chartHost);
        void SetSeries(IEnumerable<ISeries> series);
        void SetViewport(DateTime min, DateTime max);
        void UpdatePlayhead(DateTime time);
    }
}
