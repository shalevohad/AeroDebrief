using System;
using System.Collections.Generic;
using System.Threading;
using LiveChartsCore.Defaults;

namespace AeroDebrief.UI.Interfaces.Visualization
{
    /// <summary>
    /// Amplitude series provider contract for waveform visualization data.
    /// Connects recording data (IPacketSource) to chart visualization (LiveCharts2).
    /// 
    /// Provides amplitude time series with:
    /// - Time offset in seconds from recording start (X-axis)
    /// - Amplitude in dBFS (Y-axis)
    /// - Grouped by frequency and pilot
    /// 
    /// Phase 2: Real implementation using PacketSource and AmplitudeExtractor.
    /// </summary>
    public interface IAmplitudeSeriesProvider
    {
        /// <summary>
        /// Get amplitude series for the specified time range.
        /// </summary>
        /// <param name="start">Start time</param>
        /// <param name="end">End time</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Series key (e.g., "F127.5-P1") and points (X = time offset in seconds, Y = dBFS)</returns>
        IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
            DateTime start, 
            DateTime end, 
            CancellationToken ct = default);
    }
}
