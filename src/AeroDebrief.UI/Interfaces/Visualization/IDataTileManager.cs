using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.UI.Models;

namespace AeroDebrief.UI.Interfaces.Visualization
{
    /// <summary>
    /// Data tile manager contract for efficient viewport-based data loading.
    /// Phase 8: Core service for tile-based visualization system.
    /// 
    /// Responsibilities:
    /// - Load only tiles visible in viewport (+ preload buffer)
    /// - Select appropriate resolution based on zoom level
    /// - Manage memory budget (max 500 MB)
    /// - LRU cache eviction
    /// - Performance monitoring
    /// 
    /// Benefits:
    /// - Instant zoom/pan (no lag)
    /// - Low memory usage (only visible data loaded)
    /// - Smooth scrolling (preload buffer)
    /// - Scale to huge recordings (hours of data)
    /// </summary>
    public interface IDataTileManager
    {
        /// <summary>
        /// Loads tiles for the specified viewport and zoom level.
        /// 
        /// Process:
        /// 1. Calculate needed tiles (viewport + preload buffer ±1 viewport width)
        /// 2. Select resolution based on zoom level
        /// 3. Check cache for existing tiles
        /// 4. Load missing tiles from amplitude provider
        /// 5. Evict old tiles if over memory budget (LRU)
        /// 6. Return all tiles for viewport
        /// </summary>
        Task<IEnumerable<SeriesTile>> LoadTilesForViewportAsync(
            DateTime viewportStart,
            DateTime viewportEnd,
            double zoomLevel,
            IEnumerable<double> visibleFrequencies,
            CancellationToken ct = default);
        
        /// <summary>
        /// Unloads tiles outside viewport to free memory.
        /// Keeps tiles within preload buffer (±1 viewport width).
        /// </summary>
        void UnloadTilesOutsideViewport(DateTime viewportStart, DateTime viewportEnd);
        
        /// <summary>
        /// Gets current memory usage of loaded tiles in bytes.
        /// </summary>
        long GetMemoryUsage();
        
        /// <summary>
        /// Clears all loaded tiles and resets cache.
        /// Call when switching recordings or need immediate memory release.
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Gets tile cache performance statistics.
        /// Monitor: cache hit rate (>70%), memory usage (<500MB), efficiency.
        /// </summary>
        TileCacheStats GetStats();
        
        /// <summary>
        /// Sets recording start time for tile generation.
        /// Must be called before LoadTilesForViewportAsync.
        /// </summary>
        void SetRecordingStart(DateTime recordingStart);
    }

    /// <summary>
    /// Tile cache statistics for monitoring performance.
    /// </summary>
    public class TileCacheStats
    {
        public int LoadedTileCount { get; set; }
        public long TotalMemoryBytes { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public int EvictionCount { get; set; }
        public int UnloadCount { get; set; }
        public int TotalTilesLoaded { get; set; }
        
        public double TotalMemoryMB => TotalMemoryBytes / (1024.0 * 1024.0);
        
        public double CacheHitRate => 
            CacheHitCount + CacheMissCount > 0 
                ? (double)CacheHitCount / (CacheHitCount + CacheMissCount) 
                : 0.0;
    }
}
