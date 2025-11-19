using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.UI.Models;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Manages loading and caching of data tiles for efficient viewport rendering.
    /// Phase 8: Core service for tile-based data system.
    /// 
    /// The tile manager is responsible for:
    /// - Loading only tiles visible in the current viewport
    /// - Preloading tiles for smooth scrolling (±1 viewport width)
    /// - Selecting appropriate resolution based on zoom level
    /// - Managing memory budget (max 500 MB for tiles)
    /// - Evicting least recently used tiles when over budget (LRU)
    /// - Providing cache statistics for monitoring
    /// </summary>
    public interface IDataTileManager
    {
        /// <summary>
        /// Loads tiles for the specified viewport and zoom level.
        /// 
        /// This method:
        /// 1. Calculates which tiles are needed for the viewport (including preload buffer)
        /// 2. Selects appropriate resolution based on zoom level
        /// 3. Checks cache for existing tiles
        /// 4. Loads missing tiles from TileCache (Phase 3)
        /// 5. Evicts old tiles if over memory budget (LRU)
        /// 6. Returns all tiles for the viewport
        /// 
        /// The preload buffer (±1 viewport width) ensures smooth scrolling by loading
        /// tiles before they become visible.
        /// </summary>
        /// <param name="viewportStart">Start of visible viewport</param>
        /// <param name="viewportEnd">End of visible viewport</param>
        /// <param name="zoomLevel">Current zoom level (1.0 = full range, 10.0 = zoomed 10x)</param>
        /// <param name="visibleFrequencies">Frequencies that should be loaded (only visible ones)</param>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>Collection of loaded tiles covering the viewport</returns>
        Task<IEnumerable<SeriesTile>> LoadTilesForViewportAsync(
            DateTime viewportStart,
            DateTime viewportEnd,
            double zoomLevel,
            IEnumerable<double> visibleFrequencies,
            CancellationToken ct = default);
        
        /// <summary>
        /// Unloads tiles that are outside the specified viewport to free memory.
        /// 
        /// This method keeps tiles within the preload buffer (±1 viewport width) and
        /// removes all others. This prevents memory accumulation as the user pans
        /// across the recording.
        /// 
        /// Call this method after viewport changes to maintain memory budget.
        /// </summary>
        /// <param name="viewportStart">Start of visible viewport</param>
        /// <param name="viewportEnd">End of visible viewport</param>
        void UnloadTilesOutsideViewport(DateTime viewportStart, DateTime viewportEnd);
        
        /// <summary>
        /// Gets current memory usage of loaded tiles in bytes.
        /// 
        /// This includes all tiles currently in memory, whether visible or in
        /// the preload buffer. Use this for monitoring and debugging memory usage.
        /// </summary>
        /// <returns>Total memory usage in bytes</returns>
        long GetMemoryUsage();
        
        /// <summary>
        /// Clears all loaded tiles and resets cache statistics.
        /// 
        /// Call this when:
        /// - Switching to a different recording
        /// - Memory needs to be freed immediately
        /// - Reset is required after error
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Gets statistics about tile cache performance.
        /// 
        /// Use these statistics to monitor:
        /// - Cache hit rate (should be > 70%)
        /// - Memory usage (should be < 500 MB)
        /// - Number of loaded tiles
        /// - Cache efficiency
        /// </summary>
        /// <returns>Current cache statistics</returns>
        TileCacheStats GetStats();
        
        /// <summary>
        /// Sets the recording start time for tile generation from amplitude data.
        /// Must be called before LoadTilesForViewportAsync if using amplitude provider.
        /// </summary>
        /// <param name="recordingStart">Start time of the recording</param>
        void SetRecordingStart(DateTime recordingStart);
    }
    
    /// <summary>
    /// Statistics for tile cache performance monitoring.
    /// Phase 8: Used for telemetry and debugging.
    /// </summary>
    public class TileCacheStats
    {
        /// <summary>
        /// Number of tiles currently loaded in memory.
        /// </summary>
        public int LoadedTileCount { get; set; }
        
        /// <summary>
        /// Total memory usage of loaded tiles in bytes.
        /// Should stay under 500 MB (500 * 1024 * 1024 bytes).
        /// </summary>
        public long TotalMemoryBytes { get; set; }
        
        /// <summary>
        /// Total memory usage in MB (for convenience).
        /// </summary>
        public double TotalMemoryMB => TotalMemoryBytes / (1024.0 * 1024.0);
        
        /// <summary>
        /// Number of times a tile was found in cache (hit).
        /// Higher is better - indicates efficient caching.
        /// </summary>
        public int CacheHitCount { get; set; }
        
        /// <summary>
        /// Number of times a tile was not in cache and had to be loaded (miss).
        /// Lower is better relative to hits.
        /// </summary>
        public int CacheMissCount { get; set; }
        
        /// <summary>
        /// Cache hit rate as a percentage (0.0 to 1.0).
        /// Target: > 0.70 (70% hit rate).
        /// Formula: hits / (hits + misses)
        /// </summary>
        public double CacheHitRate
        {
            get
            {
                int total = CacheHitCount + CacheMissCount;
                return total > 0 ? (double)CacheHitCount / total : 0.0;
            }
        }
        
        /// <summary>
        /// Number of tiles evicted due to memory budget constraints.
        /// Indicates LRU eviction activity.
        /// </summary>
        public int EvictionCount { get; set; }
        
        /// <summary>
        /// Number of tiles unloaded due to viewport changes.
        /// Normal during panning operations.
        /// </summary>
        public int UnloadCount { get; set; }
        
        /// <summary>
        /// Total number of tiles loaded since creation or last clear.
        /// </summary>
        public int TotalTilesLoaded { get; set; }
        
        public override string ToString()
        {
            return $"TileCache: {LoadedTileCount} tiles, " +
                   $"{TotalMemoryMB:F1} MB, " +
                   $"Hit Rate: {CacheHitRate:P1} " +
                   $"({CacheHitCount} hits / {CacheMissCount} misses)";
        }
    }
}
