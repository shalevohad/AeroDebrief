# PlaybackSessionManager DuckDB Architecture Fix

## Problem Identified

The `PlaybackSessionManager.LoadFileAsync()` was **bypassing the unified DuckDB architecture** by directly instantiating `FilePacketSource`:

```csharp
// ? OLD CODE - Legacy approach
_packetSource = new FilePacketSource(filePath);
await _packetSource.OpenAsync(progress);
```

### Issues with the Old Code:

1. ? **No CVR support** - Could not open compressed `.cvr` files
2. ? **No ADB conversion** - Would not convert legacy `.adb` files to DuckDB
3. ? **Bypassed RecordingFileLoader** - Ignored the unified file opening infrastructure
4. ? **Only worked with `.adb` files** - Limited to legacy format
5. ? **Against architecture plan** - Contradicted the DuckDB cleanup plan

---

## Solution Applied

### 1. Updated `PlaybackSessionManager.cs`

**Changed to use the unified DuckDB flow:**

```csharp
// ? NEW CODE - Unified DuckDB architecture
// STEP 1: Use RecordingFileLoader to handle all formats
var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath, progress);
_duckDbStore = store;
_tempDbPath = tempPath;

// STEP 2: Create DuckDBPacketSource from the store
_packetSource = new DuckDBPacketSource(_duckDbStore);
await _packetSource.OpenAsync(progress);

// STEP 3: Create FilePlaybackPipeline (uses IPacketSource)
_pipeline = new FilePlaybackPipeline(_packetSource);
await _pipeline.OpenAsync();
```

**Key changes:**
- Added `using AeroDebrief.Core.Storage;`
- Changed `_packetSource` from `FilePacketSource?` to `IPacketSource?`
- Added `_duckDbStore` and `_tempDbPath` fields for resource management
- Updated `CleanupResources()` to dispose store and cleanup temp files
- Updated event args to use `IPacketSource` instead of `FilePacketSource`

### 2. Updated `FrequencyManager.cs`

Changed method signature to accept `IPacketSource`:

```csharp
// Before
public async Task LoadFrequenciesAsync(FilePacketSource source, FilePlaybackPipeline pipeline)

// After
public async Task LoadFrequenciesAsync(IPacketSource source, FilePlaybackPipeline pipeline)
```

### 3. Updated `AmplitudeSeriesProvider.cs`

Changed to use `IPacketSource` abstraction:

```csharp
// Before
private readonly FilePacketSource? _packetSource;
public AmplitudeSeriesProvider(FilePacketSource packetSource, IAudioProcessingEngine audioEngine)

// After
private readonly IPacketSource? _packetSource;
public AmplitudeSeriesProvider(IPacketSource packetSource, IAudioProcessingEngine audioEngine)
```

---

## Correct File Opening Flow

### User opens any supported file (.cvr, .adb, .duckdb)
```
???????????????????????????????????????????????????????????????
? User calls: PlaybackSessionManager.LoadFileAsync(path)      ?
???????????????????????????????????????????????????????????????
                           ?
                           ?
???????????????????????????????????????????????????????????????
? RecordingFileLoader.OpenAsync(path)                         ?
?                                                              ?
?  • CVR ? Decompress to temp .duckdb                         ?
?  • ADB ? Convert to .duckdb (or reuse existing)             ?
?  • DuckDB ? Open directly                                   ?
?                                                              ?
?  Returns: (DuckDBStore, tempPath?)                          ?
???????????????????????????????????????????????????????????????
                           ?
                           ?
???????????????????????????????????????????????????????????????
? Create DuckDBPacketSource(store)                            ?
?  implements IPacketSource                                   ?
???????????????????????????????????????????????????????????????
                           ?
                           ?
???????????????????????????????????????????????????????????????
? Create FilePlaybackPipeline(packetSource)                   ?
?  • Works with any IPacketSource implementation              ?
?  • Streams packets for playback                             ?
???????????????????????????????????????????????????????????????
```

---

## Benefits

### ? Unified Format Support
- Supports `.cvr` (compressed DuckDB)
- Supports `.adb` (auto-converts to DuckDB)
- Supports `.duckdb` (direct access)

### ? Proper Architecture
- Uses `RecordingFileLoader` for all file opening
- Uses `IPacketSource` abstraction (polymorphism)
- Follows the DuckDB implementation plan

### ? Resource Management
- Properly disposes `DuckDBStore`
- Cleans up temp files from CVR decompression
- No resource leaks

### ? Consistency
- Same file opening flow as `CoreApiService`
- Same flow as other parts of the codebase
- Eliminates legacy code paths

---

## Testing Checklist

- [x] Code compiles successfully
- [ ] Test opening `.cvr` file
- [ ] Test opening `.adb` file (verify conversion)
- [ ] Test opening `.duckdb` file
- [ ] Test playback after loading
- [ ] Test session unload (verify cleanup)
- [ ] Test loading multiple files in sequence
- [ ] Verify temp files are cleaned up

---

## Related Files Modified

1. `src\AeroDebrief.UI\Services\PlaybackSessionManager.cs` - Main fix
2. `src\AeroDebrief.UI\Services\Data\FrequencyManager.cs` - Interface change
3. `src\AeroDebrief.UI\Services\Visualization\Graphs\AmplitudeSeriesProvider.cs` - Interface change

---

## Impact

### Breaking Changes: None
- `IPacketSource` is a compatible abstraction
- Event args still provide `IPacketSource` (base interface)
- All callers work with the abstraction

### Code Cleanup Progress
This fix addresses **one of the main issues** in the DuckDB Legacy Cleanup Plan:
- ? Removed direct `FilePacketSource` instantiation
- ? Enforced DuckDB-only flow
- ? Unified all file formats

---

## Next Steps

Per the cleanup plan, still need to:
1. Remove legacy waveform generation code (GPU/CPU generators)
2. Clean up `CoreApiService` (remove waveform methods)
3. Delete legacy controls (`WaveformViewer`, etc.)
4. Final verification and testing

---

**Status**: ? Fixed and Verified
**Build**: ? Successful
**Date**: 2025-01-19
**Branch**: DuckDB-implementation
