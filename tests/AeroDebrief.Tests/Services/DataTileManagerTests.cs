using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using AeroDebrief.UI.Models;
using AeroDebrief.UI.Services.Graphs;

namespace AeroDebrief.Tests.Services
{
    /// <summary>
    /// Unit tests for DataTileManager (Phase 8).
    /// Tests tile loading, caching, memory management, and LRU eviction.
    /// </summary>
    public class DataTileManagerTests
    {
        #region Test Infrastructure
        
        /// <summary>
        /// Mock implementation of IDataTileCache for testing.
        /// </summary>
        private class MockDataTileCache : IDataTileCache
        {
            private readonly Dictionary<string, DataTile<string>> _tiles = new();
            
            public int GetOrCreateCallCount { get; private set; }
            
            public DataTile<string>? GetOrCreateTile(
                string key,
                TimeSpan start,
                TimeSpan end,
                int level,
                Func<DataTile<string>> factory)
            {
                GetOrCreateCallCount++;
                
                var tileKey = $"{key}_{start.Ticks}_{end.Ticks}_{level}";
                
                if (_tiles.TryGetValue(tileKey, out var tile))
                    return tile;
                
                // Create a new tile with factory
                var newTile = factory();
                if (newTile != null)
                {
                    _tiles[tileKey] = newTile;
                }
                
                return newTile;
            }
            
            public void PrefetchTiles(string key, TimeSpan viewportStart, TimeSpan viewportEnd, int level)
            {
                // Not used in Phase 8 Step 2
            }
            
            public void Clear()
            {
                _tiles.Clear();
            }
            
            public CacheStatistics GetStatistics()
            {
                return new CacheStatistics
                {
                    TileCount = _tiles.Count,
                    TotalSizeBytes = _tiles.Values.Sum(t => t.EstimatedSizeBytes),
                    HitCount = 0,
                    MissCount = 0,
                    EvictionCount = 0
                };
            }
            
            public void SetBudget(double budgetMB)
            {
                // Not used in Phase 8 Step 2
            }
        }
        
        // Helper method to create test tile manager
        private IDataTileManager CreateTileManager()
        {
            var mockCache = new MockDataTileCache();
            return new DataTileManager(mockCache);
        }
        
        private IDataTileManager CreateTileManagerWithMockCache(out MockDataTileCache mockCache)
        {
            mockCache = new MockDataTileCache();
            return new DataTileManager(mockCache);
        }

        #endregion
        
        #region Step 1: Basic Structure Tests

        [Fact]
        public void SeriesTile_Key_IsUnique()
        {
            // Arrange
            var tile1 = new SeriesTile
            {
                Frequency = 251000000.0,
                PilotId = "VIPER-1",
                StartTime = new DateTime(2025, 1, 21, 10, 0, 0),
                EndTime = new DateTime(2025, 1, 21, 10, 5, 0),
                Resolution = Resolution.Layer1_50ms
            };

            var tile2 = new SeriesTile
            {
                Frequency = 251000000.0,
                PilotId = "VIPER-1",
                StartTime = new DateTime(2025, 1, 21, 10, 5, 0), // Different time
                EndTime = new DateTime(2025, 1, 21, 10, 10, 0),
                Resolution = Resolution.Layer1_50ms
            };

            // Act
            var key1 = tile1.Key;
            var key2 = tile2.Key;

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void SeriesTile_MemoryBytes_CalculatesCorrectly()
        {
            // Arrange
            var tile = new SeriesTile
            {
                Points = new List<AmplitudePoint>
                {
                    new AmplitudePoint(DateTime.Now, -40.0),
                    new AmplitudePoint(DateTime.Now.AddSeconds(1), -35.0),
                    new AmplitudePoint(DateTime.Now.AddSeconds(2), -30.0)
                }
            };

            // Act
            var memoryBytes = tile.MemoryBytes;

            // Assert
            Assert.Equal(48, memoryBytes); // 3 points * 16 bytes each
        }

        [Fact]
        public void SeriesTile_ContainsTime_ReturnsTrue_WhenTimeInRange()
        {
            // Arrange
            var tile = new SeriesTile
            {
                StartTime = new DateTime(2025, 1, 21, 10, 0, 0),
                EndTime = new DateTime(2025, 1, 21, 10, 5, 0)
            };

            var time = new DateTime(2025, 1, 21, 10, 2, 30);

            // Act
            var contains = tile.ContainsTime(time);

            // Assert
            Assert.True(contains);
        }

        [Fact]
        public void SeriesTile_ContainsTime_ReturnsFalse_WhenTimeOutOfRange()
        {
            // Arrange
            var tile = new SeriesTile
            {
                StartTime = new DateTime(2025, 1, 21, 10, 0, 0),
                EndTime = new DateTime(2025, 1, 21, 10, 5, 0)
            };

            var time = new DateTime(2025, 1, 21, 10, 6, 0); // After end

            // Act
            var contains = tile.ContainsTime(time);

            // Assert
            Assert.False(contains);
        }

        [Fact]
        public void SeriesTile_Overlaps_ReturnsTrue_WhenRangesOverlap()
        {
            // Arrange
            var tile = new SeriesTile
            {
                StartTime = new DateTime(2025, 1, 21, 10, 0, 0),
                EndTime = new DateTime(2025, 1, 21, 10, 5, 0)
            };

            var rangeStart = new DateTime(2025, 1, 21, 10, 3, 0);
            var rangeEnd = new DateTime(2025, 1, 21, 10, 8, 0);

            // Act
            var overlaps = tile.Overlaps(rangeStart, rangeEnd);

            // Assert
            Assert.True(overlaps);
        }

        [Fact]
        public void TileCacheStats_CacheHitRate_CalculatesCorrectly()
        {
            // Arrange
            var stats = new TileCacheStats
            {
                CacheHitCount = 70,
                CacheMissCount = 30
            };

            // Act
            var hitRate = stats.CacheHitRate;

            // Assert
            Assert.Equal(0.70, hitRate, 2); // 70% hit rate
        }

        #endregion

        #region Step 2: DataTileManager Implementation Tests

        [Fact]
        public void TileManager_Constructor_InitializesSuccessfully()
        {
            // Arrange
            var mockCache = new MockDataTileCache();
            
            // Act
            var manager = new DataTileManager(mockCache);
            
            // Assert
            Assert.NotNull(manager);
            var stats = manager.GetStats();
            Assert.Equal(0, stats.LoadedTileCount);
            Assert.Equal(0, stats.TotalMemoryBytes);
        }
        
        [Fact]
        public void TileManager_Constructor_ThrowsOnNullCache()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DataTileManager(null!));
        }
        
        [Fact]
        public async Task TileManager_LoadTilesForViewport_ThrowsOnInvalidRange()
        {
            // Arrange
            var manager = CreateTileManager();
            var start = new DateTime(2025, 1, 21, 10, 5, 0);
            var end = new DateTime(2025, 1, 21, 10, 0, 0); // End before start
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await manager.LoadTilesForViewportAsync(start, end, 1.0, new[] { 251000000.0 });
            });
        }
        
        [Fact]
        public void TileManager_GetMemoryUsage_ReturnsZeroInitially()
        {
            // Arrange
            var manager = CreateTileManager();
            
            // Act
            var memory = manager.GetMemoryUsage();
            
            // Assert
            Assert.Equal(0, memory);
        }
        
        [Fact]
        public void TileManager_GetStats_ReturnsInitialStats()
        {
            // Arrange
            var manager = CreateTileManager();
            
            // Act
            var stats = manager.GetStats();
            
            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.LoadedTileCount);
            Assert.Equal(0, stats.TotalMemoryBytes);
            Assert.Equal(0.0, stats.TotalMemoryMB);
            Assert.Equal(0, stats.CacheHitCount);
            Assert.Equal(0, stats.CacheMissCount);
            Assert.Equal(0.0, stats.CacheHitRate);
            Assert.Equal(0, stats.EvictionCount);
            Assert.Equal(0, stats.UnloadCount);
            Assert.Equal(0, stats.TotalTilesLoaded);
        }
        
        [Fact]
        public void TileManager_Clear_ResetsState()
        {
            // Arrange
            var manager = CreateTileManager();
            
            // Act
            manager.Clear();
            
            // Assert
            var stats = manager.GetStats();
            Assert.Equal(0, stats.LoadedTileCount);
            Assert.Equal(0, stats.TotalMemoryBytes);
        }
        
        [Fact]
        public void TileManager_UnloadTilesOutsideViewport_HandlesEmptyCache()
        {
            // Arrange
            var manager = CreateTileManager();
            var start = new DateTime(2025, 1, 21, 10, 0, 0);
            var end = new DateTime(2025, 1, 21, 10, 10, 0);
            
            // Act (should not throw)
            manager.UnloadTilesOutsideViewport(start, end);
            
            // Assert
            Assert.Equal(0, manager.GetStats().LoadedTileCount);
        }
        
        [Fact]
        public void TileManager_GetStats_CalculatesCacheHitRate()
        {
            // Arrange
            var manager = CreateTileManager();
            
            // Act
            var stats = manager.GetStats();
            
            // Assert - initial state should have 0 hit rate
            Assert.Equal(0.0, stats.CacheHitRate);
        }
        
        [Fact]
        public void TileCacheStats_TotalMemoryMB_ConvertsCorrectly()
        {
            // Arrange
            var stats = new TileCacheStats
            {
                TotalMemoryBytes = 1024 * 1024 * 50 // 50 MB
            };
            
            // Act
            var memoryMB = stats.TotalMemoryMB;
            
            // Assert
            Assert.Equal(50.0, memoryMB, 2);
        }
        
        [Fact]
        public void TileCacheStats_CacheHitRate_HandlesZeroRequests()
        {
            // Arrange
            var stats = new TileCacheStats
            {
                CacheHitCount = 0,
                CacheMissCount = 0
            };
            
            // Act
            var hitRate = stats.CacheHitRate;
            
            // Assert
            Assert.Equal(0.0, hitRate);
        }

        #endregion
    }
}
