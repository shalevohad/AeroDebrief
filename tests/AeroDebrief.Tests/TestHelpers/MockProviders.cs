using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.UI.Services.Visualization.Graphs;
using LiveChartsCore.Defaults;

namespace AeroDebrief.Tests.TestHelpers
{
    /// <summary>
    /// Mock amplitude series provider for testing.
    /// Returns empty series immediately for fast tests.
    /// </summary>
    public class MockAmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        public async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
            DateTime start,
            DateTime end,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Return empty series for testing
            await Task.CompletedTask;
            yield break;
        }
    }

    /// <summary>
    /// Mock provider that simulates slow loading for testing cancellation.
    /// </summary>
    public class SlowMockAmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        public async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
            DateTime start,
            DateTime end,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Simulate slow loading
            await Task.Delay(2000, cancellationToken);
            yield break;
        }
    }

    /// <summary>
    /// Mock provider that always fails for testing error handling.
    /// </summary>
    public class FailingMockAmplitudeSeriesProvider : IAmplitudeSeriesProvider
    {
        public async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
            DateTime start,
            DateTime end,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Simulated load failure");
#pragma warning disable CS0162 // Unreachable code detected
            yield break;
#pragma warning restore CS0162
        }
    }
}
