# Phase 8: Data Tile System - Implementation Plan

## ?? Overview

**Phase**: 8 of 11  
**Status**: ?? In Progress  
**Prerequisites**: ? All met (Phases 0-7 complete)  
**Estimated Duration**: 2-3 days  
**Complexity**: Medium-High

**Progress Tracking**:
- ? Step 1: Interface & Data Structures (Not Started)
- ? Step 2: DataTileManager Implementation (Pending)
- ? Step 3: ViewModel Integration (Pending)
- ? Step 4: Performance & Polish (Pending)

---

## ?? Objectives

Implement efficient tile-based data loading system that:
1. Loads only visible viewport data
2. Caches recently accessed tiles
3. Unloads tiles outside viewport
4. Supports multi-resolution rendering
5. Maintains < 1 GB memory constraint
6. Provides smooth pan/zoom experience

### Key Goals
- **Scalability**: Handle 2+ hour recordings without slowdown
- **Memory Efficiency**: Use < 500 MB for tile cache
- **Performance**: < 200ms viewport load time
- **Smoothness**: No visual artifacts during pan/zoom
- **Reliability**: No memory leaks or cache thrashing

---

## ??? Architecture

### Current State (Phase 7)
```
UnifiedGraphViewModel
    ? Loads ALL data
Series[] (in memory)
    ? 2-hour recording = ~500 MB
Limited by total recording size
```

**Problem**: Cannot scale beyond ~2 hours without exceeding 1 GB limit.

### Target State (Phase 8)
```
UnifiedGraphViewModel
    ? Requests viewport
DataTileManager
    ? Loads only visible tiles
TileCache (Phase 3)
    ? SQLite + memory cache
Only ~50-100 MB in memory at once
```

**Benefit**: Scale to 10+ hour recordings within memory constraint.

---

## ?? Design

### Component Hierarchy

```
??????????????????????????????????????????????????
?         UnifiedGraphViewModel                  ?
?                                                ?
?  • ViewportStart, ViewportEnd                 ?
?  • ZoomLevel                                  ?
?  • VisibleFrequencies                         ?
?  • Series (Observable)                        ?
?                                                ?
?  NEW:                                         ?
?  • LoadDataForViewportAsync()                 ?
?  • IsLoadingTiles property                    ?
?  • TileLoadProgress property                  ?
??????????????????????????????????????????????????
                 ?
                 ? Requests tiles for viewport
                 ?
??????????????????????????????????????????????????
?            IDataTileManager                    ?
?                                                ?
?  • LoadTilesForViewportAsync(...)             ?
?  • UnloadTilesOutsideViewport(...)            ?
?  • GetMemoryUsage()                           ?
?  • Clear()                                    ?
??????????????????????????????????????????????????
                 ?
                 ? Implementation
                 ?
??????????????????????????????????????????????????
?           DataTileManager                      ?
?                                                ?
?  • _tileCache (Phase 3)                       ?
?  • _loadedTiles (Dictionary)                  ?
?  • _memoryBudget (500 MB)                     ?
?  • _tileSize (5 minutes)                      ?
?                                                ?
?  Logic:                                       ?
?  1. Calculate tiles needed for viewport       ?
?  2. Check cache for existing tiles            ?
?  3. Load missing tiles from TileCache         ?
?  4. Evict old tiles if over budget (LRU)      ?
?  5. Return tiles to ViewModel                 ?
??????????????????????????????????????????????????
                 ?
                 ? Uses
                 ?
??????????????????????????????????????????????????
?         IDataTileCache (Phase 3)               ?
?                                                ?
?  • GetTile(key, resolution)                   ?
?  • SQLite database                            ?
?  • Multi-resolution support                   ?
??????????????????????????????????????????????????
```

---

## ?? Data Structures

### 1. SeriesTile

Represents a single tile of data for one pilot on one frequency.

```csharp
namespace AeroDebrief.UI.Models
{
    /// <summary>
    /// Represents a tile of amplitude data for a frequency/pilot combination.
    /// Phase 8: Core data structure for tile-based loading.
    /// </summary>
    public class SeriesTile
    {
        /// <summary>
        /// Frequency in Hz (e.g., 251000000.0 for UHF 251.0 MHz).
        /// </summary>
        public double Frequency { get; set; }
        
        /// <summary>
        /// Pilot identifier (e.g., "VIPER-1").
        /// </summary>
        public string PilotId { get; set; } = string.Empty;
        
        /// <summary>
        /// Start time of this tile (inclusive).
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// End time of this tile (exclusive).
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// Resolution/layer of this tile.
        /// </summary>
        public Resolution Resolution { get; set; }
        
        /// <summary>
        /// Data points in this tile.
        /// </summary>
        public List<DataPoint> Points { get; set; } = new();
        
        /// <summary>
        /// Number of points in this tile.
        /// </summary>
        public int PointCount => Points.Count;
        
        /// <summary>
        /// Estimated memory usage in bytes.
        /// Approximately 16 bytes per DataPoint (8 bytes DateTime + 8 bytes double).
        /// </summary>
        public long MemoryBytes => Points.Count * 16;
        
        /// <summary>
        /// Unique key for this tile.
        /// Format: "freq_{frequency}_pilot_{pilotId}_{startTicks}_{resolution}"
        /// </summary>
        public string Key => $"freq_{Frequency}_pilot_{PilotId}_{StartTime.Ticks}_{Resolution}";
    }
    
    /// <summary>
    /// Simple data point with time and amplitude.
    /// </summary>
    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Amplitude { get; set; }
    }
}
```

### 2. IDataTileManager Interface

```csharp
namespace AeroDebrief.UI.Services.Graphs
{
    /// <summary>
    /// Manages loading and caching of data tiles for efficient viewport rendering.
    /// Phase 8: Core service for tile-based data system.
    /// </summary>
    public interface IDataTileManager
    {
        /// <summary>
        /// Loads tiles for the specified viewport and zoom level.
        /// Automatically selects appropriate resolution based on zoom level.
        /// Includes preload buffer (±1 viewport width).
        /// </summary>
        /// <param name="viewportStart">Start of visible viewport</param>
        /// <param name="viewportEnd">End of visible viewport</param>
        /// <param name="zoomLevel">Current zoom level (1.0 = full range)</param>
        /// <param name="visibleFrequencies">Frequencies that should be loaded</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Collection of loaded tiles</returns>
        Task<IEnumerable<SeriesTile>> LoadTilesForViewportAsync(
            DateTime viewportStart,
            DateTime viewportEnd,
            double zoomLevel,
            IEnumerable<double> visibleFrequencies,
            CancellationToken ct = default);
        
        /// <summary>
        /// Unloads tiles that are outside the specified viewport to free memory.
        /// Keeps tiles within ±1 viewport width for smooth scrolling.
        /// </summary>
        /// <param name="viewportStart">Start of visible viewport</param>
        /// <param name="viewportEnd">End of visible viewport</param>
        void UnloadTilesOutsideViewport(DateTime viewportStart, DateTime viewportEnd);
        
        /// <summary>
        /// Gets current memory usage of loaded tiles in bytes.
        /// </summary>
        long GetMemoryUsage();
        
        /// <summary>
        /// Clears all loaded tiles and resets cache.
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Gets statistics about tile cache performance.
        /// </summary>
        TileCacheStats GetStats();
    }
    
    /// <summary>
    /// Statistics for tile cache performance monitoring.
    /// </summary>
    public class TileCacheStats
    {
        public int LoadedTileCount { get; set; }
        public long TotalMemoryBytes { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public double CacheHitRate => CacheHitCount + CacheMissCount > 0
            ? (double)CacheHitCount / (CacheHitCount + CacheMissCount)
            : 0.0;
    }
}
```

---

## ?? Implementation Steps

### Step 1: Interface & Data Structures (4 hours)

#### Tasks
1. Create `SeriesTile` model class
2. Create `DataPoint` struct
3. Create `IDataTileManager` interface
4. Create `TileCacheStats` class
5. Add `Resolution` enum if needed
6. Create unit test file structure

#### Files to Create
- `src/AeroDebrief.UI/Models/SeriesTile.cs` (~80 lines)
- `src/AeroDebrief.UI/Services/Graphs/IDataTileManager.cs` (~120 lines)
- `tests/AeroDebrief.Tests/Services/DataTileManagerTests.cs` (skeleton)

#### Success Criteria
- [ ] All classes compile
- [ ] XML documentation complete
- [ ] No breaking changes
- [ ] Test file structure ready

---

### Step 2: DataTileManager Implementation (6 hours)

#### Core Logic

**Tile Size Calculation**:
```csharp
// Tile size: 5 minutes
private static readonly TimeSpan TileSize = TimeSpan.FromMinutes(5);

private DateTime GetTileStart(DateTime time)
{
    // Round down to nearest 5-minute boundary
    var ticks = time.Ticks;
    var tileTicks = TileSize.Ticks;
    return new DateTime((ticks / tileTicks) * tileTicks);
}
```

**Resolution Selection** (from Phase 5):
```csharp
private Resolution SelectResolution(double zoomLevel)
{
    if (zoomLevel >= 20.0)
        return Resolution.Layer0_10ms; // Highest detail
    else if (zoomLevel >= 4.0)
        return Resolution.Layer1_50ms; // High detail
    else if (zoomLevel >= 1.5)
        return Resolution.Layer2_250ms; // Medium detail
    else
        return Resolution.Layer3_1s; // Overview
}
```

**Preload Buffer**:
```csharp
private (DateTime, DateTime) CalculateLoadRange(DateTime viewportStart, DateTime viewportEnd)
{
    var viewportDuration = viewportEnd - viewportStart;
    
    // Load ±1 viewport width for smooth scrolling
    var bufferStart = viewportStart - viewportDuration;
    var bufferEnd = viewportEnd + viewportDuration;
    
    // Clamp to recording bounds
    bufferStart = bufferStart < _recordingStart ? _recordingStart : bufferStart;
    bufferEnd = bufferEnd > _recordingEnd ? _recordingEnd : bufferEnd;
    
    return (bufferStart, bufferEnd);
}
```

**Memory Management (LRU Eviction)**:
```csharp
private const long MaxMemoryBytes = 500 * 1024 * 1024; // 500 MB

private void EvictTilesIfNeeded()
{
    while (GetMemoryUsage() > MaxMemoryBytes && _loadedTiles.Count > 0)
    {
        // Find least recently used tile
        var lruTile = _loadedTiles
            .OrderBy(kvp => kvp.Value.LastAccessTime)
            .First();
        
        _loadedTiles.Remove(lruTile.Key);
        _logger.Debug($"Evicted tile: {lruTile.Key}, freed {lruTile.Value.Tile.MemoryBytes} bytes");
    }
}
```

#### Tasks
1. Implement `DataTileManager` class skeleton
2. Implement tile size and boundary logic
3. Implement resolution selection
4. Implement `LoadTilesForViewportAsync()` method
5. Implement preload buffer logic
6. Implement memory tracking
7. Implement LRU eviction
8. Implement `UnloadTilesOutsideViewport()` method
9. Implement `GetMemoryUsage()` and `GetStats()` methods
10. Add comprehensive logging

#### Files to Create
- `src/AeroDebrief.UI/Services/Graphs/DataTileManager.cs` (~400 lines)

#### Unit Tests (10-12 tests)
- `TileManager_CalculatesTileBoundariesCorrectly`
- `TileManager_SelectsAppropriateResolution`
- `TileManager_LoadsTilesForViewport`
- `TileManager_IncludesPreloadBuffer`
- `TileManager_UnloadsTilesOutsideViewport`
- `TileManager_EvictsTilesWhenOverBudget`
- `TileManager_TracksMemoryUsage`
- `TileManager_CalculatesCacheHitRate`
- `TileManager_HandlesEmptyCache`
- `TileManager_HandlesCancellation`
- `TileManager_Clear_RemovesAllTiles`
- `TileManager_GetStats_ReturnsAccurateStats`

#### Success Criteria
- [ ] All core methods implemented
- [ ] Memory management working
- [ ] LRU eviction tested
- [ ] 10+ unit tests passing
- [ ] No memory leaks

---

### Step 3: ViewModel Integration (4 hours)

#### UnifiedGraphViewModel Changes

**New Fields**:
```csharp
private readonly IDataTileManager? _tileManager;
private bool _isLoadingTiles = false;
private double _tileLoadProgress = 0.0;
private CancellationTokenSource? _loadCancellation;
```

**New Properties**:
```csharp
public bool IsLoadingTiles
{
    get => _isLoadingTiles;
    private set => SetProperty(ref _isLoadingTiles, value);
}

public double TileLoadProgress
{
    get => _tileLoadProgress;
    private set => SetProperty(ref _tileLoadProgress, value);
}
```

**New Method**:
```csharp
/// <summary>
/// Phase 8: Loads data tiles for the current viewport.
/// Automatically selects resolution based on zoom level.
/// </summary>
public async Task LoadDataForViewportAsync()
{
    if (_tileManager == null) return;
    
    try
    {
        IsLoadingTiles = true;
        TileLoadProgress = 0.0;
        
        // Cancel any existing load
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        
        // Get visible frequencies (only those not hidden)
        var visibleFreqs = _frequencyPilots.Keys
            .Where(key => _seriesVisibility.ContainsKey(key) && _seriesVisibility[key])
            .Select(key => double.Parse(key.Split('_')[1]))
            .Distinct();
        
        // Load tiles
        var tiles = await _tileManager.LoadTilesForViewportAsync(
            ViewportStart,
            ViewportEnd,
            ZoomLevel,
            visibleFreqs,
            _loadCancellation.Token);
        
        // Update series from tiles
        UpdateSeriesFromTiles(tiles);
        
        TileLoadProgress = 1.0;
    }
    catch (OperationCanceledException)
    {
        _logger.Debug("Tile load cancelled");
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Error loading tiles for viewport");
    }
    finally
    {
        IsLoadingTiles = false;
    }
}

private void UpdateSeriesFromTiles(IEnumerable<SeriesTile> tiles)
{
    foreach (var tile in tiles)
    {
        var seriesKey = $"freq_{tile.Frequency}_pilot_{tile.PilotId}";
        
        if (!_allSeries.TryGetValue(seriesKey, out var series))
        {
            // Create new series
            series = CreateSeriesForTile(tile);
            _allSeries[seriesKey] = series;
            Series.Add(series);
        }
        
        // Update series values
        UpdateSeriesValues(series, tile.Points);
    }
}
```

**Viewport Event Wiring**:
```csharp
protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    base.OnPropertyChanged(propertyName);
    
    if (propertyName == nameof(ViewportStart) || propertyName == nameof(ViewportEnd))
    {
        // Phase 8: Unload old tiles, load new tiles
        _tileManager?.UnloadTilesOutsideViewport(ViewportStart, ViewportEnd);
        _ = LoadDataForViewportAsync();
    }
}
```

#### Tasks
1. Add `_tileManager` field and pass in constructor
2. Add `IsLoadingTiles` and `TileLoadProgress` properties
3. Implement `LoadDataForViewportAsync()` method
4. Implement `UpdateSeriesFromTiles()` method
5. Wire viewport changed events to tile loading
6. Add cancellation support
7. Update constructor to accept `IDataTileManager`
8. Add progress reporting

#### Integration Tests (6-8 tests)
- `ViewModel_LoadsTilesForViewport`
- `ViewModel_UnloadsTilesOnViewportChange`
- `ViewModel_UpdatesSeriesFromTiles`
- `ViewModel_CancelsLoadOnNewViewport`
- `ViewModel_HandlesLoadErrors`
- `ViewModel_ReportsLoadProgress`
- `ViewModel_WorksWithoutTileManager` (null check)
- `ViewModel_LoadsOnlyVisibleFrequencies`

#### Success Criteria
- [ ] Tiles load on viewport change
- [ ] Old tiles unload automatically
- [ ] Series update from tiles
- [ ] Progress indicator works
- [ ] 6+ integration tests passing

---

### Step 4: Performance & Polish (4 hours)

#### Tasks
1. **Memory Profiling**
   - Profile with 2-hour recording
   - Verify < 500 MB for tiles
   - Check for memory leaks
   - Validate LRU eviction

2. **Performance Testing**
   - Measure viewport load time (target < 200ms)
   - Test pan/zoom smoothness
   - Validate cache hit rate (target > 70%)
   - Test with 10+ hour recording

3. **Telemetry**
   - Add performance counters
   - Log tile load/unload operations
   - Track cache statistics
   - Monitor memory usage

4. **Documentation**
   - Update architecture diagrams
   - Document tile size rationale
   - Explain memory budget
   - Create usage examples

5. **UI Integration**
   - Add loading indicator
   - Show progress during load
   - Handle errors gracefully
   - Test user experience

#### Performance Tests
- `Performance_LoadTime_Under200ms`
- `Performance_MemoryUsage_Under500MB`
- `Performance_CacheHitRate_Above70Percent`
- `Performance_SmoothPan_NoVisualGaps`
- `Performance_LargeFile_10Hours`

#### Success Criteria
- [ ] Load time < 200ms
- [ ] Memory < 500 MB for tiles
- [ ] Cache hit rate > 70%
- [ ] No visual artifacts
- [ ] Smooth pan/zoom
- [ ] All tests passing
- [ ] Documentation complete

---

## ?? Success Criteria

### Functional Requirements
- [ ] Tiles load only for visible viewport
- [ ] Tiles unload when outside viewport
- [ ] Resolution adjusts with zoom level
- [ ] Preload buffer provides smooth scrolling
- [ ] Memory stays under budget
- [ ] No visual gaps during pan/zoom

### Performance Requirements
- [ ] Viewport load time: < 200ms
- [ ] Memory usage: < 500 MB for tiles
- [ ] Cache hit rate: > 70%
- [ ] Smooth pan/zoom (no lag)
- [ ] Scales to 10+ hour recordings

### Quality Requirements
- [ ] 20+ tests passing (unit + integration)
- [ ] Clean interface design
- [ ] Comprehensive logging
- [ ] XML documentation complete
- [ ] No memory leaks
- [ ] Error handling robust

---

## ?? Files to Create/Modify

### New Files (7)
1. `src/AeroDebrief.UI/Models/SeriesTile.cs`
2. `src/AeroDebrief.UI/Services/Graphs/IDataTileManager.cs`
3. `src/AeroDebrief.UI/Services/Graphs/DataTileManager.cs`
4. `tests/AeroDebrief.Tests/Services/DataTileManagerTests.cs`
5. `tests/AeroDebrief.Tests/ViewModels/UnifiedGraphViewModelPhase8Tests.cs`
6. `docs/Phase8-Step1-Complete.md`
7. `docs/Phase8-Complete.md`

### Modified Files (2)
8. `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
9. `docs/AeroDebrief-Rewrite-Plan-LiveCharts2.md`

---

## ?? Estimated Effort

| Step | Duration | Files | Tests | LOC |
|------|----------|-------|-------|-----|
| Step 1: Interface & Models | 4h | 3 | 5-7 | ~200 |
| Step 2: Implementation | 6h | 1 | 10-12 | ~400 |
| Step 3: Integration | 4h | 1 | 6-8 | ~200 |
| Step 4: Performance | 4h | 2 | 3-5 | ~100 |
| **Total** | **18h** | **7** | **24-32** | **~900** |

---

## ?? Risks & Mitigations

### Risk 1: Memory Leaks
**Impact**: High  
**Probability**: Medium  
**Mitigation**:
- Implement IDisposable pattern
- Use weak references where appropriate
- Profiling after each step
- Automated memory tests

### Risk 2: Slow Tile Loading
**Impact**: Medium  
**Probability**: Low  
**Mitigation**:
- Async loading with cancellation
- Preload buffer for smooth scrolling
- Aggressive caching strategy
- Performance benchmarks

### Risk 3: Visual Gaps During Load
**Impact**: Medium  
**Probability**: Medium  
**Mitigation**:
- Progressive rendering
- Placeholder data while loading
- Larger preload buffer
- Fast tile loading (< 200ms)

### Risk 4: Cache Thrashing
**Impact**: Medium  
**Probability**: Low  
**Mitigation**:
- Smart LRU eviction
- Larger memory budget (500 MB)
- Preload buffer prevents constant eviction
- Cache hit rate monitoring

---

## ?? Expected Impact

### Before Phase 8
- Limited to ~2 hour recordings
- All data in memory (500+ MB)
- Memory grows with recording length
- Cannot scale beyond RAM constraint

### After Phase 8
- Support 10+ hour recordings
- Only ~50-100 MB in memory
- Constant memory usage regardless of length
- Scales indefinitely within constraint

---

## ?? Dependencies

### Required (Phase 3)
- ? `IDataTileCache` interface
- ? `DataTileCache` implementation
- ? Multi-resolution support
- ? SQLite backend

### Required (Phase 5)
- ? Viewport management
- ? Zoom level tracking
- ? Pan/zoom events

### Required (Phase 7)
- ? Visibility toggles
- ? Frequency tracking
- ? Series management

---

## ? Definition of Done

Phase 8 is complete when:
- [x] All interfaces defined and documented
- [x] DataTileManager fully implemented
- [x] ViewModel integration complete
- [x] 20+ tests passing (unit + integration + performance)
- [x] Memory stays under 500 MB for tiles
- [x] Load time under 200ms per viewport
- [x] Cache hit rate above 70%
- [x] No memory leaks detected
- [x] Documentation complete
- [x] Build successful
- [x] Code review passed
- [x] User testing successful

---

**Status**: ? Ready to Start  
**Next Step**: Step 1 - Interface & Data Structures  
**Estimated Completion**: 2-3 days  

---

**Last Updated**: January 21, 2025  
**Phase**: 8 of 11  
**Branch**: `livechart2-integration`
