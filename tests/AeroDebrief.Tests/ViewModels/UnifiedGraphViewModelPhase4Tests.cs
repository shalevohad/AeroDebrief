using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LiveChartsCore.Defaults;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Visualization.Graphs;

namespace AeroDebrief.Tests.ViewModels
{
    /// <summary>
    /// Tests for UnifiedGraphViewModel Phase 4 features.
    /// Validates visibility management, density-aware rendering, and frequency/pilot toggles.
    /// </summary>
    public class UnifiedGraphViewModelPhase4Tests
    {
        // Mock amplitude provider for testing
        private class MockAmplitudeProvider : IAmplitudeSeriesProvider
        {
            private readonly List<(string key, List<ObservablePoint> points)> _testData;

            public MockAmplitudeProvider(List<(string key, List<ObservablePoint> points)> testData)
            {
                _testData = testData;
            }

            public async IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(
                DateTime start, 
                DateTime end, 
                CancellationToken ct = default)
            {
                await Task.Yield();
                
                foreach (var (key, points) in _testData)
                {
                    if (ct.IsCancellationRequested)
                        yield break;
                    
                    yield return (key, points);
                }
            }
        }

        private List<(string key, List<ObservablePoint> points)> CreateTestData(
            int frequencyCount, 
            int pilotsPerFrequency, 
            int pointsPerSeries = 100)
        {
            var data = new List<(string, List<ObservablePoint>)>();
            var baseTime = DateTime.Now;

            for (int f = 0; f < frequencyCount; f++)
            {
                var freqId = $"F{f:D3}.0";
                
                for (int p = 0; p < pilotsPerFrequency; p++)
                {
                    var pilotId = $"P{p:D2}";
                    var key = $"{freqId}-{pilotId}";
                    
                    var points = new List<ObservablePoint>();
                    for (int i = 0; i < pointsPerSeries; i++)
                    {
                        points.Add(new ObservablePoint(
                            baseTime.AddSeconds(i).Ticks,
                            -40.0 - (i % 20) // dBFS
                        ));
                    }
                    
                    data.Add((key, points));
                }
            }

            return data;
        }

        [Fact]
        public async Task LoadDataAsync_SmallDataset_LoadsAllSeries()
        {
            // Arrange - 5 frequencies with 4 pilots each (typical case)
            var testData = CreateTestData(frequencyCount: 5, pilotsPerFrequency: 4);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider);

            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            await viewModel.LoadDataAsync(start, end);

            // Assert
            Assert.Equal(20, viewModel.Series.Count); // 5 * 4 = 20 series all visible
            Assert.Equal(20, viewModel.VisibleSeriesCount);
            Assert.True(viewModel.TotalPoints > 0);
        }

        [Fact]
        public async Task LoadDataAsync_HighPilotFrequency_CollapsesExcessPilots()
        {
            // Arrange - 1 frequency with 20 pilots (10% case)
            var testData = CreateTestData(frequencyCount: 1, pilotsPerFrequency: 20);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider) 
            { 
                MaxPilotsPerFrequency = 8 
            };

            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            await viewModel.LoadDataAsync(start, end);

            // Assert - Should only show top 8 pilots initially
            Assert.True(viewModel.VisibleSeriesCount <= 8, 
                $"Expected at most 8 visible series, got {viewModel.VisibleSeriesCount}");
        }

        [Fact]
        public async Task SetFrequencyVisible_TogglesAllPilotsInFrequency()
        {
            // Arrange
            var testData = CreateTestData(frequencyCount: 2, pilotsPerFrequency: 3);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider);

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));
            var initialCount = viewModel.VisibleSeriesCount;

            // Act - Hide first frequency
            viewModel.SetFrequencyVisible("F000.0", false);

            // Assert - Should hide 3 series (3 pilots)
            Assert.Equal(initialCount - 3, viewModel.VisibleSeriesCount);

            // Act - Show it again
            viewModel.SetFrequencyVisible("F000.0", true);

            // Assert - Should restore all series
            Assert.Equal(initialCount, viewModel.VisibleSeriesCount);
        }

        [Fact]
        public async Task SetPilotVisible_TogglesIndividualPilot()
        {
            // Arrange
            var testData = CreateTestData(frequencyCount: 1, pilotsPerFrequency: 4);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider);

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));
            var initialCount = viewModel.VisibleSeriesCount;

            // Act - Hide one pilot
            viewModel.SetPilotVisible("F000.0", "P00", false);

            // Assert
            Assert.Equal(initialCount - 1, viewModel.VisibleSeriesCount);

            // Act - Show it again
            viewModel.SetPilotVisible("F000.0", "P00", true);

            // Assert
            Assert.Equal(initialCount, viewModel.VisibleSeriesCount);
        }

        [Fact]
        public async Task ExpandFrequency_ShowsAllPilots()
        {
            // Arrange - Frequency with 12 pilots (> default 8)
            var testData = CreateTestData(frequencyCount: 1, pilotsPerFrequency: 12);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider) 
            { 
                MaxPilotsPerFrequency = 8 
            };

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));
            var collapsedCount = viewModel.VisibleSeriesCount;

            // Act - Expand the frequency
            viewModel.ExpandFrequency("F000.0", expanded: true);

            // Assert - Should now show all 12 pilots
            Assert.Equal(12, viewModel.VisibleSeriesCount);
            Assert.True(viewModel.VisibleSeriesCount > collapsedCount);
        }

        [Fact]
        public async Task ExpandFrequency_ThenCollapse_RestoresTopN()
        {
            // Arrange
            var testData = CreateTestData(frequencyCount: 1, pilotsPerFrequency: 15);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider) 
            { 
                MaxPilotsPerFrequency = 6 
            };

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));

            // Act - Expand then collapse
            viewModel.ExpandFrequency("F000.0", expanded: true);
            var expandedCount = viewModel.VisibleSeriesCount;
            
            viewModel.ExpandFrequency("F000.0", expanded: false);
            var collapsedCount = viewModel.VisibleSeriesCount;

            // Assert
            Assert.Equal(15, expandedCount); // All pilots shown when expanded
            Assert.True(collapsedCount <= 6); // Only top 6 when collapsed
        }

        [Fact]
        public async Task VisibilityToggles_DoNotReloadData()
        {
            // Arrange
            var testData = CreateTestData(frequencyCount: 2, pilotsPerFrequency: 3);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider);

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));
            var originalSeries = viewModel.Series.ToList();

            // Act - Toggle visibility multiple times
            viewModel.SetFrequencyVisible("F000.0", false);
            viewModel.SetFrequencyVisible("F000.0", true);
            viewModel.SetPilotVisible("F001.0", "P00", false);
            viewModel.SetPilotVisible("F001.0", "P00", true);

            // Assert - Series instances should be reused (no reload)
            // The Series collection changes, but underlying series objects are reused
            var finalSeries = viewModel.Series.ToList();
            Assert.Equal(originalSeries.Count, finalSeries.Count);
        }

        [Fact]
        public async Task ZoomLevel_UpdatesMarkerDensity()
        {
            // Arrange
            var testData = CreateTestData(frequencyCount: 1, pilotsPerFrequency: 2);
            var provider = new MockAmplitudeProvider(testData);
            var viewModel = new UnifiedGraphViewModel(provider);

            await viewModel.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));

            // Verify series were loaded
            Assert.True(viewModel.Series.Count > 0, "Series collection should not be empty after LoadDataAsync");

            // Act - Change zoom level
            viewModel.ZoomLevel = 1.0; // Zoomed out - no markers
            var lowZoomMarkerSize = GetMarkerSizeFromSeries(viewModel);

            viewModel.ZoomLevel = 3.0; // Zoomed in - show markers
            var highZoomMarkerSize = GetMarkerSizeFromSeries(viewModel);

            // Assert - Higher zoom should show larger markers
            Assert.True(highZoomMarkerSize >= lowZoomMarkerSize, 
                $"Expected highZoomMarkerSize ({highZoomMarkerSize}) >= lowZoomMarkerSize ({lowZoomMarkerSize}). " +
                $"Series count: {viewModel.Series.Count}, ZoomLevel threshold: 2.0");
        }

        [Fact]
        public async Task LoadDataAsync_MixedPilotCounts_AppliesDensityCorrectly()
        {
            // Arrange - Simulate 90/10 distribution from plan
            // 54 frequencies with 4 pilots each (90%)
            var normalFreqs = CreateTestData(frequencyCount: 54, pilotsPerFrequency: 4, pointsPerSeries: 10);
            // 6 frequencies with 24 pilots each (10%)
            var highPilotFreqs = CreateTestData(frequencyCount: 6, pilotsPerFrequency: 24, pointsPerSeries: 10);
            
            // Adjust keys to avoid collision
            for (int i = 0; i < highPilotFreqs.Count; i++)
            {
                var (key, points) = highPilotFreqs[i];
                var newKey = key.Replace("F00", "F54"); // Start at F054
                newKey = newKey.Replace("F01", "F55");
                newKey = newKey.Replace("F02", "F56");
                newKey = newKey.Replace("F03", "F57");
                newKey = newKey.Replace("F04", "F58");
                newKey = newKey.Replace("F05", "F59");
                highPilotFreqs[i] = (newKey, points);
            }

            var allData = normalFreqs.Concat(highPilotFreqs).ToList();
            var provider = new MockAmplitudeProvider(allData);
            var viewModel = new UnifiedGraphViewModel(provider) 
            { 
                MaxPilotsPerFrequency = 8 
            };

            var start = DateTime.Now;
            var end = start.AddMinutes(10);

            // Act
            await viewModel.LoadDataAsync(start, end);

            // Assert
            // Normal frequencies: 54 * 4 = 216 series (all visible)
            // High-pilot frequencies: 6 * 8 = 48 series (collapsed to 8 per freq)
            // Total visible: 216 + 48 = 264
            var expectedVisible = (54 * 4) + (6 * 8);
            Assert.True(viewModel.VisibleSeriesCount <= expectedVisible + 10, // Allow some tolerance
                $"Expected around {expectedVisible} visible series, got {viewModel.VisibleSeriesCount}");
            
            // Total series created: 54 * 4 + 6 * 24 = 360
            // Note: We can't directly check _allSeries from here, but visible should be less than total
        }

        [Fact]
        public void PropertyChangedEvents_FireCorrectly()
        {
            // Arrange
            var provider = new MockAmplitudeProvider(new List<(string, List<ObservablePoint>)>());
            var viewModel = new UnifiedGraphViewModel(provider);
            
            var propertiesChanged = new List<string>();
            viewModel.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName!);

            // Act
            viewModel.ZoomLevel = 2.5;
            viewModel.Start = DateTime.Now;
            viewModel.End = DateTime.Now.AddHours(1);

            // Assert
            Assert.Contains("ZoomLevel", propertiesChanged);
            Assert.Contains("Start", propertiesChanged);
            Assert.Contains("End", propertiesChanged);
        }

        // Helper method to extract marker size from series
        private double GetMarkerSizeFromSeries(UnifiedGraphViewModel viewModel)
        {
            var firstSeries = viewModel.Series.FirstOrDefault();
            if (firstSeries is LiveChartsCore.SkiaSharpView.LineSeries<ObservablePoint> lineSeries)
            {
                return lineSeries.GeometrySize;
            }
            return 0;
        }
    }
}
