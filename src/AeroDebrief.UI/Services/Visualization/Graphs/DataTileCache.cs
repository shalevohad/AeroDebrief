using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Represents a data tile at a specific time range and resolution level.
    /// Used for multi-resolution caching of amplitude data.
    /// </summary>
    /// <typeparam name="TKey">Type of the series key (e.g., frequency+pilot)</typeparam>
    public sealed class DataTile<TKey> where TKey : notnull
    {
        public TKey Key { get; init; }
        public TimeSpan Start { get; init; }
        public TimeSpan End { get; init; }
        public int Level { get; init; }
        public ReadOnlyMemory<LiveChartsCore.Defaults.ObservablePoint> Samples { get; init; }
        
        public TimeSpan Span => End - Start;
        public int SampleCount => Samples.Length;
        public long EstimatedSizeBytes => 
            sizeof(double) * 2 * SampleCount + // X and Y values
            64; // Overhead estimate
    }
    
    /// <summary>
    /// Interface for multi-resolution data tile cache with memory budgeting.
    /// Implements LRU eviction when memory budget is exceeded.
    /// Phase 3: Memory-bounded caching system.
    /// </summary>
    public interface IDataTileCache
    {
        /// <summary>
        /// Get or create a tile for the specified key, time range, and level.
        /// </summary>
        DataTile<string>? GetOrCreateTile(
            string key,
            TimeSpan start,
            TimeSpan end,
            int level,
            Func<DataTile<string>> factory);
        
        /// <summary>
        /// Pre-fetch tiles for the specified viewport range.
        /// </summary>
        void PrefetchTiles(string key, TimeSpan viewportStart, TimeSpan viewportEnd, int level);
        
        /// <summary>
        /// Clear all cached tiles.
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Get current cache statistics.
        /// </summary>
        CacheStatistics GetStatistics();
        
        /// <summary>
        /// Set the memory budget in megabytes.
        /// </summary>
        void SetBudget(double budgetMB);
    }
    
    /// <summary>
    /// Cache statistics for monitoring and telemetry.
    /// </summary>
    public sealed record CacheStatistics
    {
        public int TileCount { get; init; }
        public long TotalSizeBytes { get; init; }
        public double SizeMB => TotalSizeBytes / (1024.0 * 1024.0);
        public int HitCount { get; init; }
        public int MissCount { get; init; }
        public int EvictionCount { get; init; }
        public double HitRate => HitCount + MissCount > 0 
            ? (double)HitCount / (HitCount + MissCount) 
            : 0.0;
    }
    
    /// <summary>
    /// LRU cache entry for data tiles.
    /// </summary>
    internal sealed class CacheEntry
    {
        public DataTile<string> Tile { get; init; }
        public DateTime LastAccessed { get; set; }
        public long AccessCount { get; set; }
        
        public CacheEntry(DataTile<string> tile)
        {
            Tile = tile ?? throw new ArgumentNullException(nameof(tile));
            LastAccessed = DateTime.UtcNow;
            AccessCount = 0;
        }
    }
    
    /// <summary>
    /// Memory-bounded LRU cache for multi-resolution amplitude data tiles.
    /// Implements Phase 3 requirements for viewport-driven caching.
    /// </summary>
    public sealed class DataTileCache : IDataTileCache
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Dictionary<string, CacheEntry> _cache = new();
        private readonly object _lock = new();
        
        private long _totalSizeBytes;
        private long _budgetBytes;
        private int _hitCount;
        private int _missCount;
        private int _evictionCount;
        
        /// <summary>
        /// Create a data tile cache with the specified memory budget.
        /// </summary>
        /// <param name="budgetMB">Memory budget in megabytes (default 300)</param>
        public DataTileCache(double budgetMB = 300.0)
        {
            SetBudget(budgetMB);
            Logger.Info($"DataTileCache initialized with {budgetMB:F1} MB budget");
        }
        
        /// <inheritdoc/>
        public void SetBudget(double budgetMB)
        {
            if (budgetMB <= 0)
                throw new ArgumentException("Budget must be positive", nameof(budgetMB));
            
            lock (_lock)
            {
                _budgetBytes = (long)(budgetMB * 1024 * 1024);
                Logger.Info($"DataTileCache budget set to {budgetMB:F1} MB ({_budgetBytes:N0} bytes)");
                
                // Enforce budget immediately
                EnforceBudget();
            }
        }
        
        /// <inheritdoc/>
        public DataTile<string>? GetOrCreateTile(
            string key,
            TimeSpan start,
            TimeSpan end,
            int level,
            Func<DataTile<string>> factory)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            
            var tileKey = MakeTileKey(key, start, end, level);
            
            lock (_lock)
            {
                // Check cache
                if (_cache.TryGetValue(tileKey, out var entry))
                {
                    entry.LastAccessed = DateTime.UtcNow;
                    entry.AccessCount++;
                    _hitCount++;
                    
                    Logger.Trace($"Cache HIT: {tileKey} (access #{entry.AccessCount})");
                    return entry.Tile;
                }
                
                // Cache miss - create tile
                _missCount++;
                Logger.Trace($"Cache MISS: {tileKey}");
                
                var tile = factory();
                if (tile == null)
                {
                    Logger.Warn($"Factory returned null tile for {tileKey}");
                    return null;
                }
                
                // Add to cache
                var newEntry = new CacheEntry(tile);
                _cache[tileKey] = newEntry;
                _totalSizeBytes += tile.EstimatedSizeBytes;
                
                Logger.Debug($"Cached tile: {tileKey}, size: {tile.EstimatedSizeBytes:N0} bytes, samples: {tile.SampleCount}");
                
                // Enforce budget
                EnforceBudget();
                
                return tile;
            }
        }
        
        /// <inheritdoc/>
        public void PrefetchTiles(string key, TimeSpan viewportStart, TimeSpan viewportEnd, int level)
        {
            // TODO: Implement prefetching logic
            // For now, this is a no-op
            Logger.Trace($"PrefetchTiles: {key}, {viewportStart}-{viewportEnd}, level {level}");
        }
        
        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lock)
            {
                var count = _cache.Count;
                var size = _totalSizeBytes;
                
                _cache.Clear();
                _totalSizeBytes = 0;
                
                Logger.Info($"Cache cleared: {count} tiles, {size / 1024.0 / 1024.0:F2} MB freed");
            }
        }
        
        /// <inheritdoc/>
        public CacheStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new CacheStatistics
                {
                    TileCount = _cache.Count,
                    TotalSizeBytes = _totalSizeBytes,
                    HitCount = _hitCount,
                    MissCount = _missCount,
                    EvictionCount = _evictionCount
                };
            }
        }
        
        /// <summary>
        /// Enforce memory budget by evicting least recently used tiles.
        /// </summary>
        private void EnforceBudget()
        {
            // Already holding lock
            
            if (_totalSizeBytes <= _budgetBytes)
                return; // Within budget
            
            Logger.Debug($"Cache over budget: {_totalSizeBytes / 1024.0 / 1024.0:F2} MB / {_budgetBytes / 1024.0 / 1024.0:F2} MB");
            
            // Sort by last accessed time (LRU)
            var sorted = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ToList();
            
            // Evict until we're under 80% of budget (hysteresis)
            var targetBytes = (long)(_budgetBytes * 0.8);
            var evicted = 0;
            var freedBytes = 0L;
            
            while (_totalSizeBytes > targetBytes && sorted.Count > 0)
            {
                var entry = sorted[0];
                sorted.RemoveAt(0);
                
                var tileSize = entry.Value.Tile.EstimatedSizeBytes;
                _cache.Remove(entry.Key);
                _totalSizeBytes -= tileSize;
                freedBytes += tileSize;
                evicted++;
                _evictionCount++;
                
                Logger.Trace($"Evicted tile: {entry.Key}, freed {tileSize:N0} bytes");
            }
            
            if (evicted > 0)
            {
                Logger.Info($"Evicted {evicted} tiles, freed {freedBytes / 1024.0 / 1024.0:F2} MB, " +
                           $"cache now: {_totalSizeBytes / 1024.0 / 1024.0:F2} MB ({_cache.Count} tiles)");
            }
        }
        
        /// <summary>
        /// Generate a unique cache key for a tile.
        /// </summary>
        private static string MakeTileKey(string seriesKey, TimeSpan start, TimeSpan end, int level)
        {
            return $"{seriesKey}|L{level}|{start.TotalSeconds:F3}-{end.TotalSeconds:F3}";
        }
    }
}
