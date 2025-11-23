using System;
using System.Linq;
using AeroDebrief.UI.Services.Visualization.Graphs;
using LiveChartsCore.Defaults;
using NLog;
using Xunit;

namespace AeroDebrief.Tests.Graphs
{
    /// <summary>
    /// Tests for DataTileCache LRU behavior and memory budgeting.
    /// Phase 3: Multi-resolution tiling validation.
    /// </summary>
    public class DataTileCacheTests
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        [Fact]
        public void Constructor_SetsBudget()
        {
            // Arrange & Act
            var cache = new DataTileCache(budgetMB: 100.0);
            var stats = cache.GetStatistics();
            
            // Assert
            Assert.Equal(0, stats.TileCount);
            Assert.Equal(0, stats.TotalSizeBytes);
        }
        
        [Fact]
        public void GetOrCreateTile_CachesMisses()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 10.0);
            var key = "F251.0-P0";
            var start = TimeSpan.Zero;
            var end = TimeSpan.FromSeconds(5);
            var level = 0;
            
            // Act
            var tile1 = cache.GetOrCreateTile(key, start, end, level, () => CreateTestTile(key, start, end, level, 100));
            var tile2 = cache.GetOrCreateTile(key, start, end, level, () => CreateTestTile(key, start, end, level, 100));
            
            // Assert
            Assert.NotNull(tile1);
            Assert.NotNull(tile2);
            Assert.Same(tile1, tile2); // Should return cached tile
            
            var stats = cache.GetStatistics();
            Assert.Equal(1, stats.TileCount);
            Assert.Equal(1, stats.HitCount);
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(0.5, stats.HitRate, precision: 2);
        }
        
        [Fact]
        public void GetOrCreateTile_DifferentKeys_CreatesMultipleTiles()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 10.0);
            
            // Act
            var tile1 = cache.GetOrCreateTile("F251.0-P0", TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => CreateTestTile("F251.0-P0", TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            
            var tile2 = cache.GetOrCreateTile("F243.0-P1", TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => CreateTestTile("F243.0-P1", TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            
            // Assert
            Assert.NotNull(tile1);
            Assert.NotNull(tile2);
            Assert.NotSame(tile1, tile2);
            
            var stats = cache.GetStatistics();
            Assert.Equal(2, stats.TileCount);
            Assert.Equal(2, stats.MissCount);
        }
        
        [Fact]
        public void EnforceBudget_EvictsLeastRecentlyUsed()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 0.01); // Very small budget to force eviction
            
            // Act - Create tiles until budget exceeded
            for (int i = 0; i < 10; i++)
            {
                var key = $"F251.{i}-P0";
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 1000));
            }
            
            // Assert
            var stats = cache.GetStatistics();
            Assert.True(stats.TileCount < 10, "Should have evicted some tiles");
            Assert.True(stats.EvictionCount > 0, "Should have performed evictions");
            Assert.True(stats.SizeMB < 0.01, "Should be under budget");
            
            Logger.Info($"Cache stats: {stats.TileCount} tiles, {stats.SizeMB:F3} MB, {stats.EvictionCount} evictions");
        }
        
        [Fact]
        public void EnforceBudget_PreservesRecentlyAccessed()
        {
            // Arrange
            // Budget: 0.1 MB = 102,400 bytes
            // Each tile with 100 samples: 8 * 2 * 100 + 64 = 1,664 bytes
            // Target: ~80% of budget = 81,920 bytes / 1,664 = ~49 tiles
            var cache = new DataTileCache(budgetMB: 0.1);
            var key1 = "F251.0-P0";
            var key2 = "F251.1-P0";
            
            // Act - Create 50 tiles to fill cache to ~80% budget
            var tile1 = cache.GetOrCreateTile(key1, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => CreateTestTile(key1, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            
            var tile2 = cache.GetOrCreateTile(key2, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => CreateTestTile(key2, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            
            for (int i = 3; i < 50; i++)
            {
                var key = $"F251.{i}-P0";
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            }
            
            // Access tile1 multiple times to make it "hot" (most recently used)
            // tile2 is the least recently used (created early, never accessed again)
            for (int i = 0; i < 5; i++)
            {
                cache.GetOrCreateTile(key1, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => throw new Exception("tile1 should be cached"));
            }
            
            // Add enough new tiles to trigger eviction (need to exceed budget)
            // This should evict tile2 and other old tiles, NOT tile1 which is hot
            for (int i = 50; i < 65; i++)
            {
                var key = $"F251.{i}-P0";
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            }
            
            // Assert - tile1 should still be cached (was recently accessed)
            var tile1Again = cache.GetOrCreateTile(key1, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => throw new Exception("tile1 should not be evicted"));
            Assert.Same(tile1, tile1Again);
            
            // tile2 should have been evicted (was least recently used)
            var tile2WasEvicted = false;
            var tile2Again = cache.GetOrCreateTile(key2, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => { tile2WasEvicted = true; return CreateTestTile(key2, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100); });
            
            Assert.True(tile2WasEvicted, "tile2 should have been evicted as it was least recently used");
        }
        
        [Fact]
        public void Clear_RemovesAllTiles()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 10.0);
            
            for (int i = 0; i < 5; i++)
            {
                var key = $"F251.{i}-P0";
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            }
            
            var statsBefore = cache.GetStatistics();
            Assert.Equal(5, statsBefore.TileCount);
            
            // Act
            cache.Clear();
            
            // Assert
            var statsAfter = cache.GetStatistics();
            Assert.Equal(0, statsAfter.TileCount);
            Assert.Equal(0, statsAfter.TotalSizeBytes);
        }
        
        [Fact]
        public void SetBudget_EnforcesBudgetImmediately()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 10.0);
            
            // Fill cache
            for (int i = 0; i < 100; i++)
            {
                var key = $"F251.{i}-P0";
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 1000));
            }
            
            var statsBefore = cache.GetStatistics();
            var countBefore = statsBefore.TileCount;
            
            // Act - Reduce budget significantly
            cache.SetBudget(0.1); // 100 KB
            
            // Assert
            var statsAfter = cache.GetStatistics();
            Assert.True(statsAfter.TileCount < countBefore, "Should have evicted tiles");
            Assert.True(statsAfter.SizeMB <= 0.1, "Should be under new budget");
            
            Logger.Info($"Budget reduction: {countBefore} -> {statsAfter.TileCount} tiles");
        }
        
        [Fact]
        public void GetStatistics_ReturnsAccurateHitRate()
        {
            // Arrange
            var cache = new DataTileCache(budgetMB: 10.0);
            var key = "F251.0-P0";
            
            // Act
            // 1 miss
            cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                () => CreateTestTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0, 100));
            
            // 9 hits
            for (int i = 0; i < 9; i++)
            {
                cache.GetOrCreateTile(key, TimeSpan.Zero, TimeSpan.FromSeconds(5), 0,
                    () => throw new Exception("Should not create"));
            }
            
            // Assert
            var stats = cache.GetStatistics();
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(9, stats.HitCount);
            Assert.Equal(0.9, stats.HitRate, precision: 2);
        }
        
        /// <summary>
        /// Helper to create a test data tile.
        /// </summary>
        private static DataTile<string> CreateTestTile(string key, TimeSpan start, TimeSpan end, int level, int sampleCount)
        {
            var samples = new ObservablePoint[sampleCount];
            var duration = end - start;
            var step = duration.TotalSeconds / sampleCount;
            
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = new ObservablePoint(
                    start.TotalSeconds + i * step,
                    -60.0 + Math.Sin(i * 0.1) * 20.0 // Mock dBFS values
                );
            }
            
            return new DataTile<string>
            {
                Key = key,
                Start = start,
                End = end,
                Level = level,
                Samples = samples
            };
        }
    }
}
