using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.UI.Models;
using NLog;

namespace AeroDebrief.UI.Services.Visualization.Graphs
{
    /// <summary>
    /// Manages loading and caching of data tiles for efficient viewport rendering.
    /// Phase 8: Core service for tile-based data system.
    /// 
    /// This implementation:
    /// - Loads only tiles visible in viewport (+ preload buffer)
    /// - Selects appropriate resolution based on zoom level
    /// - Caches tiles in memory with LRU eviction
    /// - Maintains memory budget (default 500 MB)
    /// - Provides statistics for monitoring
    /// </summary>
    public class DataTileManager : IDataTileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // Dependencies
        private readonly IDataTileCache _tileCache;
        private readonly IAmplitudeSeriesProvider? _amplitudeProvider;
        
        // State
        private readonly Dictionary<string, CachedTile> _loadedTiles = new();
        private readonly object _lock = new();
        private long _memoryBudgetBytes = Constants.TILE_CACHE_MEMORY_BUDGET_BYTES;
        private TimeSpan _tileSize = TimeSpan.FromMinutes(Constants.TILE_SIZE_MINUTES);
        private DateTime _recordingStart = DateTime.MinValue;
        
        // Statistics
        private int _cacheHitCount;
        private int _cacheMissCount;
        private int _evictionCount;
        private int _unloadCount;
        private int _totalTilesLoaded;
        
        /// <summary>
        /// Cached tile with access tracking for LRU eviction.
        /// </summary>
        private class CachedTile
        {
            public SeriesTile Tile { get; }
            public DateTime LastAccessed { get; set; }
            public int AccessCount { get; set; }
            
            public CachedTile(SeriesTile tile)
            {
                Tile = tile ?? throw new ArgumentNullException(nameof(tile));
                LastAccessed = DateTime.UtcNow;
                AccessCount = 1;
            }
            
            public void Touch()
            {
                LastAccessed = DateTime.UtcNow;
                AccessCount++;
            }
        }
        
        /// <summary>
        /// Creates a new DataTileManager.
        /// </summary>
        /// <param name="tileCache">Phase 3 tile cache for loading data from database</param>
        /// <param name="amplitudeProvider">Optional amplitude provider for generating tiles on-demand</param>
        public DataTileManager(IDataTileCache tileCache, IAmplitudeSeriesProvider? amplitudeProvider = null)
        {
            _tileCache = tileCache ?? throw new ArgumentNullException(nameof(tileCache));
            _amplitudeProvider = amplitudeProvider;
            Logger.Info($"DataTileManager initialized (Budget: {_memoryBudgetBytes / (1024.0 * 1024.0):F1} MB, TileSize: {_tileSize.TotalMinutes:F1} min)");
        }
        
        /// <summary>
        /// Sets the recording start time for tile generation.
        /// Must be called before loading tiles if using amplitude provider.
        /// </summary>
        public void SetRecordingStart(DateTime recordingStart)
        {
            _recordingStart = recordingStart;
            Logger.Debug($"Recording start time set: {recordingStart:yyyy-MM-dd HH:mm:ss}");
        }
        
        /// <inheritdoc/>
        public async Task<IEnumerable<SeriesTile>> LoadTilesForViewportAsync(
            DateTime viewportStart,
            DateTime viewportEnd,
            double zoomLevel,
            IEnumerable<double> visibleFrequencies,
            CancellationToken ct = default)
        {
            if (viewportEnd <= viewportStart)
                throw new ArgumentException("Viewport end must be after start");
            
            var sw = Stopwatch.StartNew();
            var frequencies = visibleFrequencies.ToList();
            
            Logger.Debug($"LoadTilesForViewport: {viewportStart:HH:mm:ss}-{viewportEnd:HH:mm:ss}, zoom={zoomLevel:F2}, freqs={frequencies.Count}");
            
            // Step 1: Calculate preload buffer
            var viewportDuration = viewportEnd - viewportStart;
            var preloadBuffer = TimeSpan.FromTicks((long)(viewportDuration.Ticks * Constants.TILE_PRELOAD_BUFFER_MULTIPLIER));
            var loadStart = viewportStart - preloadBuffer;
            var loadEnd = viewportEnd + preloadBuffer;
            
            // Step 2: Select resolution based on zoom level
            var resolution = SelectResolution(zoomLevel);
            Logger.Debug($"Selected resolution: {resolution} for zoom {zoomLevel:F2}");
            
            // Step 3: Calculate required tiles
            var requiredTiles = CalculateRequiredTiles(loadStart, loadEnd, frequencies, resolution);
            Logger.Debug($"Need {requiredTiles.Count} tiles for viewport");
            
            // Step 4: Load tiles (check cache, load missing, evict if needed)
            var loadedTiles = await LoadTilesAsync(requiredTiles, ct);
            
            // Step 5: Filter to viewport only (preload buffer stays in cache but isn't returned)
            var viewportTiles = loadedTiles.Where(t => t.Overlaps(viewportStart, viewportEnd)).ToList();
            
            sw.Stop();
            Logger.Info($"LoadTilesForViewport completed in {sw.ElapsedMilliseconds}ms: {viewportTiles.Count} tiles, {GetMemoryUsage() / (1024.0 * 1024.0):F1} MB");
            
            return viewportTiles;
        }
        
        /// <inheritdoc/>
        public void UnloadTilesOutsideViewport(DateTime viewportStart, DateTime viewportEnd)
        {
            lock (_lock)
            {
                // Calculate preload buffer
                var viewportDuration = viewportEnd - viewportStart;
                var preloadBuffer = TimeSpan.FromTicks((long)(viewportDuration.Ticks * Constants.TILE_PRELOAD_BUFFER_MULTIPLIER));
                var keepStart = viewportStart - preloadBuffer;
                var keepEnd = viewportEnd + preloadBuffer;
                
                // Find tiles outside the keep range
                var tilesToUnload = _loadedTiles
                    .Where(kvp => !kvp.Value.Tile.Overlaps(keepStart, keepEnd))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                if (tilesToUnload.Count == 0)
                {
                    Logger.Debug("UnloadTilesOutsideViewport: No tiles to unload");
                    return;
                }
                
                // Unload tiles
                foreach (var key in tilesToUnload)
                {
                    _loadedTiles.Remove(key);
                    _unloadCount++;
                }
                
                Logger.Debug($"UnloadTilesOutsideViewport: Unloaded {tilesToUnload.Count} tiles, {_loadedTiles.Count} remain");
            }
        }
        
        /// <inheritdoc/>
        public long GetMemoryUsage()
        {
            lock (_lock)
            {
                return _loadedTiles.Values.Sum(ct => ct.Tile.MemoryBytes);
            }
        }
        
        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lock)
            {
                var tileCount = _loadedTiles.Count;
                var memoryMB = GetMemoryUsage() / (1024.0 * 1024.0);
                
                _loadedTiles.Clear();
                
                // Reset statistics
                _cacheHitCount = 0;
                _cacheMissCount = 0;
                _evictionCount = 0;
                _unloadCount = 0;
                _totalTilesLoaded = 0;
                
                Logger.Info($"DataTileManager cleared: {tileCount} tiles, {memoryMB:F1} MB freed");
            }
        }
        
        /// <inheritdoc/>
        public TileCacheStats GetStats()
        {
            lock (_lock)
            {
                return new TileCacheStats
                {
                    LoadedTileCount = _loadedTiles.Count,
                    TotalMemoryBytes = GetMemoryUsage(),
                    CacheHitCount = _cacheHitCount,
                    CacheMissCount = _cacheMissCount,
                    EvictionCount = _evictionCount,
                    UnloadCount = _unloadCount,
                    TotalTilesLoaded = _totalTilesLoaded
                };
            }
        }
        
        /// <summary>
        /// Selects appropriate resolution based on zoom level.
        /// Phase 8: Maps zoom levels to Phase 5 resolution layers.
        /// </summary>
        private Resolution SelectResolution(double zoomLevel)
        {
            // Zoom level mapping (from Phase 5 logic and Constants):
            // >= 10.0: Layer0 (10ms resolution)
            // >= 5.0: Layer1 (50ms resolution)
            // >= 2.0: Layer2 (250ms resolution)
            // < 2.0: Layer3 (1s resolution)
            
            if (zoomLevel >= Constants.ZOOM_THRESHOLD_LAYER0)
                return Resolution.Layer0_10ms;
            else if (zoomLevel >= Constants.ZOOM_THRESHOLD_LAYER1)
                return Resolution.Layer1_50ms;
            else if (zoomLevel >= Constants.ZOOM_THRESHOLD_LAYER2)
                return Resolution.Layer2_250ms;
            else
                return Resolution.Layer3_1s;
        }
        
        /// <summary>
        /// Calculates which tiles are needed for the specified time range.
        /// </summary>
        private List<TileRequest> CalculateRequiredTiles(
            DateTime startTime,
            DateTime endTime,
            List<double> frequencies,
            Resolution resolution)
        {
            var tiles = new List<TileRequest>();
            
            foreach (var frequency in frequencies)
            {
                // Calculate tile boundaries aligned to tile size
                var tileStart = AlignToTileBoundary(startTime);
                
                while (tileStart < endTime)
                {
                    var tileEnd = tileStart + _tileSize;
                    
                    tiles.Add(new TileRequest
                    {
                        Frequency = frequency,
                        PilotId = string.Empty, // TODO: Support pilot-level tiles in future
                        StartTime = tileStart,
                        EndTime = tileEnd,
                        Resolution = resolution
                    });
                    
                    tileStart = tileEnd;
                }
            }
            
            return tiles;
        }
        
        /// <summary>
        /// Aligns a time to the nearest tile boundary (floor).
        /// Example: If tiles are 5 minutes and time is 10:02:30, returns 10:00:00.
        /// </summary>
        private DateTime AlignToTileBoundary(DateTime time)
        {
            var tileTicks = _tileSize.Ticks;
            var alignedTicks = (time.Ticks / tileTicks) * tileTicks;
            return new DateTime(alignedTicks);
        }
        
        /// <summary>
        /// Loads requested tiles, checking cache first, then loading from database.
        /// Enforces memory budget with LRU eviction.
        /// </summary>
        private async Task<List<SeriesTile>> LoadTilesAsync(
            List<TileRequest> requests,
            CancellationToken ct)
        {
            var result = new List<SeriesTile>();
            var tilesToLoad = new List<TileRequest>();
            
            // Step 1: Check cache for existing tiles
            lock (_lock)
            {
                foreach (var request in requests)
                {
                    var key = GetTileKey(request);
                    
                    if (_loadedTiles.TryGetValue(key, out var cachedTile))
                    {
                        // Cache hit
                        cachedTile.Touch();
                        result.Add(cachedTile.Tile);
                        _cacheHitCount++;
                    }
                    else
                    {
                        // Cache miss - need to load
                        tilesToLoad.Add(request);
                        _cacheMissCount++;
                    }
                }
            }
            
            // Step 2: Load missing tiles from database
            if (tilesToLoad.Count > 0)
            {
                Logger.Debug($"Loading {tilesToLoad.Count} tiles from database");
                
                foreach (var request in tilesToLoad)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    var tile = await LoadTileFromDatabaseAsync(request, ct);
                    if (tile != null)
                    {
                        result.Add(tile);
                        
                        // Add to cache
                        lock (_lock)
                        {
                            var key = GetTileKey(request);
                            _loadedTiles[key] = new CachedTile(tile);
                            _totalTilesLoaded++;
                        }
                    }
                }
                
                // Step 3: Enforce memory budget
                EnforceMemoryBudget();
            }
            
            return result;
        }
        
        /// <summary>
        /// Loads a single tile from the Phase 3 database cache or generates it from amplitude provider.
        /// </summary>
        private async Task<SeriesTile?> LoadTileFromDatabaseAsync(
            TileRequest request,
            CancellationToken ct)
        {
            try
            {
                // If we have an amplitude provider, generate tiles on-demand
                if (_amplitudeProvider != null && _recordingStart != DateTime.MinValue)
                {
                    return await GenerateTileFromAmplitudeDataAsync(request, ct);
                }
                
                // Fallback to Phase 3 cache (for legacy support)
                return await LoadTileFromCacheAsync(request, ct);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load tile: {request.Frequency:F0} [{request.StartTime:HH:mm:ss}-{request.EndTime:HH:mm:ss}]");
                return null;
            }
        }
        
        /// <summary>
        /// Generates a tile on-demand from the amplitude provider.
        /// </summary>
        private async Task<SeriesTile?> GenerateTileFromAmplitudeDataAsync(
            TileRequest request,
            CancellationToken ct)
        {
            var points = new List<AmplitudePoint>();
            
            // Query amplitude data for this tile's time range
            await foreach (var (key, seriesPoints) in _amplitudeProvider!.GetSeriesAsync(request.StartTime, request.EndTime, ct))
            {
                // Parse the series key to get frequency and pilot
                var (freqStr, pilotId) = ParseSeriesKey(key);
                
                if (freqStr == null)
                    continue;
                
                // Check if this series matches our request
                if (double.TryParse(freqStr, out var freq) && Math.Abs(freq - request.Frequency) < 0.1)
                {
                    // Convert ObservablePoints to AmplitudePoints
                    foreach (var point in seriesPoints)
                    {
                        points.Add(new AmplitudePoint
                        {
                            Time = request.StartTime + TimeSpan.FromSeconds(point.X ?? 0),
                            Amplitude = point.Y ?? 0
                        });
                    }
                }
            }
            
            if (points.Count == 0)
            {
                Logger.Debug($"No amplitude data found for tile: {request.Frequency:F0} [{request.StartTime:HH:mm:ss}-{request.EndTime:HH:mm:ss}]");
                return null;
            }
            
            // Create tile (MemoryBytes is computed automatically from Points.Count)
            var tile = new SeriesTile
            {
                Frequency = request.Frequency,
                PilotId = request.PilotId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Resolution = request.Resolution,
                Points = points
            };
            
            Logger.Debug($"Generated tile: {request.Frequency:F0} [{request.StartTime:HH:mm:ss}-{request.EndTime:HH:mm:ss}], {points.Count} points");
            return tile;
        }
        
        /// <summary>
        /// Parse series key from amplitude provider format.
        /// Format: "F251.0-P1" or "251000000-PILOT123"
        /// </summary>
        private (string? frequencyId, string? pilotId) ParseSeriesKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return (null, null);
            
            // Check for new format: "F251.0-P1"
            if (key.StartsWith("F") && key.Contains("-P"))
            {
                var parts = key.Split('-');
                if (parts.Length >= 2)
                {
                    var freqStr = parts[0].Substring(1); // Remove "F" prefix
                    var pilotStr = parts.Length > 1 ? parts[1] : null;
                    
                    // Convert MHz to Hz for internal storage
                    if (double.TryParse(freqStr, out var freqMHz))
                    {
                        var freqHz = freqMHz * 1_000_000.0;
                        return ($"{freqHz:F0}", pilotStr);
                    }
                }
            }
            
            // Legacy format: "251000000-PILOT123" or "251000000"
            var legacyParts = key.Split('-');
            if (legacyParts.Length == 0)
                return (null, null);
            
            if (legacyParts.Length == 1)
                return (legacyParts[0], null);
            
            return (legacyParts[0], legacyParts[1]);
        }
        
        /// <summary>
        /// Loads a tile from the Phase 3 cache (legacy support).
        /// </summary>
        private async Task<SeriesTile?> LoadTileFromCacheAsync(
            TileRequest request,
            CancellationToken ct)
        {
            try
            {
                // Generate cache key for Phase 3 TileCache
                var cacheKey = $"{request.Frequency:F0}_{request.PilotId}";
                var level = ResolutionToLevel(request.Resolution);
                
                // Load tile from Phase 3 cache
                var tileStartSpan = request.StartTime - _recordingStart;
                var tileEndSpan = request.EndTime - _recordingStart;
                
                DataTile<string>? dataTile = null;
                
                // Use GetOrCreateTile with a factory that returns null if data doesn't exist
                await Task.Run(() =>
                {
                    dataTile = _tileCache.GetOrCreateTile(
                        cacheKey,
                        tileStartSpan,
                        tileEndSpan,
                        level,
                        () => null!); // Return null if tile doesn't exist
                }, ct);
                
                if (dataTile == null)
                {
                    Logger.Debug($"Tile not found in cache: {cacheKey} [{request.StartTime:HH:mm:ss}-{request.EndTime:HH:mm:ss}] @ L{level}");
                    return null;
                }
                
                // Convert Phase 3 DataTile to Phase 8 SeriesTile
                var seriesTile = new SeriesTile
                {
                    Frequency = request.Frequency,
                    PilotId = request.PilotId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Resolution = request.Resolution,
                    Points = new List<AmplitudePoint>(dataTile.SampleCount)
                };
                
                // Convert ObservablePoint to AmplitudePoint
                var samples = dataTile.Samples.Span;
                for (int i = 0; i < samples.Length; i++)
                {
                    var point = samples[i];
                    var time = _recordingStart.AddSeconds(point.X ?? 0); // X is time in seconds
                    var amplitude = point.Y ?? 0; // Y is amplitude in dB
                    
                    seriesTile.Points.Add(new AmplitudePoint(time, amplitude));
                }
                
                Logger.Debug($"Loaded tile: {seriesTile}");
                return seriesTile;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load tile: {request.Frequency} [{request.StartTime:HH:mm:ss}-{request.EndTime:HH:mm:ss}]");
                return null;
            }
        }
        
        /// <summary>
        /// Enforces memory budget by evicting least recently used tiles.
        /// </summary>
        private void EnforceMemoryBudget()
        {
            lock (_lock)
            {
                var currentMemory = GetMemoryUsage();
                
                if (currentMemory <= _memoryBudgetBytes)
                    return;
                
                Logger.Debug($"Memory budget exceeded: {currentMemory / (1024.0 * 1024.0):F1} MB > {_memoryBudgetBytes / (1024.0 * 1024.0):F1} MB");
                
                // Sort tiles by last accessed time (oldest first)
                var sortedTiles = _loadedTiles
                    .OrderBy(kvp => kvp.Value.LastAccessed)
                    .ToList();
                
                // Evict tiles until under budget
                var evictedCount = 0;
                foreach (var kvp in sortedTiles)
                {
                    if (currentMemory <= _memoryBudgetBytes)
                        break;
                    
                    currentMemory -= kvp.Value.Tile.MemoryBytes;
                    _loadedTiles.Remove(kvp.Key);
                    _evictionCount++;
                    evictedCount++;
                }
                
                Logger.Info($"Evicted {evictedCount} tiles, memory now: {currentMemory / (1024.0 * 1024.0):F1} MB");
            }
        }
        
        /// <summary>
        /// Converts Resolution enum to Phase 3 level integer.
        /// </summary>
        private int ResolutionToLevel(Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Layer0_10ms => 0,
                Resolution.Layer1_50ms => 1,
                Resolution.Layer2_250ms => 2,
                Resolution.Layer3_1s => 3,
                _ => throw new ArgumentException($"Unknown resolution: {resolution}")
            };
        }
        
        /// <summary>
        /// Generates cache key for a tile request.
        /// </summary>
        private string GetTileKey(TileRequest request)
        {
            return $"freq_{request.Frequency:F0}_pilot_{request.PilotId}_{request.StartTime.Ticks}_{request.Resolution}";
        }
        
        /// <summary>
        /// Request for a specific tile.
        /// </summary>
        private class TileRequest
        {
            public double Frequency { get; set; }
            public string PilotId { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public Resolution Resolution { get; set; }
        }
    }
}
