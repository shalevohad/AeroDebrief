using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LiveChartsCore.Defaults;

namespace AeroDebrief.UI.Services.Graphs
{
    /// <summary>
    /// Interface for providing amplitude time series data for visualization.
    /// Uses ObservablePoint with time offset in seconds from recording start.
    /// Phase 2: Real implementation.
    /// </summary>
    public interface IAmplitudeSeriesProvider
    {
        /// <summary>
        /// Get amplitude series for the specified time range.
        /// </summary>
        /// <param name="start">Start time</param>
        /// <param name="end">End time</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Series key and points (X = time offset in seconds, Y = dBFS)</returns>
        IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(DateTime start, DateTime end, CancellationToken ct = default);
    }
}
