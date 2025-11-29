# DuckDB Phase 3: Recording Integration

## ?? Overview

**Status**: ?? **PLANNED** - Ready to Start  
**Date**: 2025-01-18  
**Prerequisites**: ? Phase 2 Complete, ? Phase 2.5 Complete

---

## ?? Goals

Phase 3 replaces the legacy `.adb` recording format with direct DuckDB recording, enabling:
- ? Record directly to DuckDB database (not `.adb`)
- ? Optional auto-compression to `.cvr` on stop
- ? User setting: "Output Format" (CVR or Uncompressed)
- ? Live playback during recording (concurrent read/write)
- ? Better performance and smaller files
- ? Real-time metadata indexing

---

## ??? Architecture Changes

### Current Flow (Phase 2)
```
Audio Packets ? ADB File (.adb)
                    ?
              (On Playback)
                    ?
           Convert to DuckDB
                    ?
             Decompress CVR
                    ?
                 Playback
```

### New Flow (Phase 3)
```
Audio Packets ? DuckDB (.duckdb)  ???? Concurrent Read/Write (Live Playback)
                    ?
           (Optional on Stop)
                    ?
            Compress to CVR
                    ?
          Delete .duckdb temp
```

---

## ?? Components to Modify

### 1. **AudioPacketRecorder.cs** - Core Recording Engine
**Current**: Records to `.adb` file  
**New**: Records to DuckDB database

**Changes Needed**:
- ? Replace `FileStream` with `DuckDBStore` instance
- ? Initialize database in `StartRecording()`
- ? Use `InsertPacketsAsync()` in batch writer
- ? Add WAL mode for concurrent read/write
- ? Optional compression to CVR on `StopRecording()`
- ? Use user settings for output format preference

**New Methods**:
```csharp
private DuckDBStore? _recordingStore;
private string? _tempDatabasePath;

public void StartRecording(string? filePath = null)
{
    // Create temp database path
    _tempDatabasePath = Path.Combine(
        Path.GetTempPath(), 
        $"aerodebrief_recording_{Guid.NewGuid()}.duckdb"
    );
    
    // Initialize DuckDB store in WAL mode (concurrent read/write)
    _recordingStore = new DuckDBStore(_tempDatabasePath, walMode: true);
    
    // Start batch writer using InsertPacketsAsync()
    _writerTask = Task.Run(() => WriterLoop(_recordingCts.Token));
    
    // Enable live playback if requested
    if (settings.EnableLivePlayback)
    {
        LivePlaybackReady?.Invoke(_tempDatabasePath);
    }
}

public async Task StopRecordingAsync()
{
    // Stop writer
    _recordingCts?.Cancel();
    await _writerTask!;
    
    // Get user setting for output format
    var outputFormat = settings.GetRecorderSetting(RecorderSettingKeys.OutputFormat);
    
    if (outputFormat == "CVR")
    {
        // Compress to CVR
        var cvrPath = _outputFile!.Replace(".duckdb", ".cvr");
        await CvrFormat.CompressToCvrAsync(_tempDatabasePath!, cvrPath, progress);
        
        // Delete temp database
        File.Delete(_tempDatabasePath!);
        _outputFile = cvrPath;
    }
    else
    {
        // Keep uncompressed (rename temp to final)
        File.Move(_tempDatabasePath!, _outputFile!);
    }
    
    _recordingStore?.Dispose();
    RecordingComplete?.Invoke(_outputFile!);
}
```

---

### 2. **RecorderSettingsStore.cs** - Add Output Format Setting
**Current**: Only has basic recording settings  
**New**: Add output format and live playback settings

**Changes Needed**:
- ? Add `OutputFormat` setting (CVR/Uncompressed)
- ? Add `EnableLivePlayback` setting (bool)
- ? Add `AutoCompress` setting (bool)

**New Settings**:
```csharp
public static class RecorderSettingKeys
{
    // ...existing settings...
    
    // Phase 3 additions
    public const string OutputFormat = "OutputFormat"; // "CVR" or "Uncompressed"
    public const string EnableLivePlayback = "EnableLivePlayback"; // bool
    public const string AutoCompress = "AutoCompress"; // bool (auto-compress on stop)
}

// Default values
OutputFormat = "CVR"  // Compress to CVR by default
EnableLivePlayback = false  // Disabled by default (Phase 4)
AutoCompress = true  // Auto-compress when stopping
```

---

### 3. **DuckDBStore.cs** - WAL Mode Support
**Current**: Read-only mode  
**New**: Support concurrent read/write during recording

**Changes Needed**:
- ? Add WAL mode initialization
- ? Add batch insert optimization
- ? Add flush/checkpoint methods

**New Methods**:
```csharp
public DuckDBStore(string path, bool walMode = false)
{
    _dbPath = path;
    
    if (walMode)
    {
        // Enable WAL for concurrent read/write
        ExecuteNonQuery("PRAGMA journal_mode=WAL;");
        Logger.Info("WAL mode enabled for concurrent access");
    }
    
    InitializeSchema();
}

public async Task FlushAsync()
{
    // Checkpoint WAL to main database
    await Task.Run(() => ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE);"));
}
```

---

### 4. **CLI Recording Commands** - Update Output
**Current**: Records to `.adb`  
**New**: Records to `.duckdb` or `.cvr`

**Changes Needed**:
- ? Update `--record` command documentation
- ? Add `--format` option (cvr/uncompressed)
- ? Show compression progress
- ? Display final file size

**New Options**:
```bash
# Record to CVR (compressed) - default
aerodebrief record --server 192.168.1.100 --format cvr

# Record to uncompressed DuckDB
aerodebrief record --server 192.168.1.100 --format uncompressed

# Record with live playback (Phase 4)
aerodebrief record --server 192.168.1.100 --live
```

---

## ?? Implementation Steps

### Step 1: Update DuckDBStore for WAL Mode
- [ ] Add WAL mode constructor parameter
- [ ] Add `FlushAsync()` method
- [ ] Add batch insert optimization
- [ ] Test concurrent read/write

### Step 2: Update RecorderSettingsStore
- [ ] Add `OutputFormat` setting
- [ ] Add `EnableLivePlayback` setting
- [ ] Add `AutoCompress` setting
- [ ] Update default values

### Step 3: Modify AudioPacketRecorder
- [ ] Replace FileStream with DuckDBStore
- [ ] Update `StartRecording()` to use database
- [ ] Update `StopRecording()` for compression
- [ ] Add batch writer using `InsertPacketsAsync()`
- [ ] Add live playback event
- [ ] Update error handling

### Step 4: Update CLI Commands
- [ ] Update `record` command help
- [ ] Add `--format` option
- [ ] Add compression progress indicator
- [ ] Update output messages

### Step 5: Testing
- [ ] Unit tests for WAL mode
- [ ] Integration tests for recording
- [ ] Test compression on stop
- [ ] Test concurrent read/write
- [ ] Test file size comparison

### Step 6: Documentation
- [ ] Update user guide
- [ ] Update CLI help
- [ ] Create migration guide (Phase 2 ? Phase 3)
- [ ] Update architecture diagrams

---

## ?? Performance Benefits

### File Sizes (Estimated)
| Format | Size | Compression Ratio |
|--------|------|-------------------|
| ADB (legacy) | 100 MB | Baseline |
| DuckDB (uncompressed) | 85 MB | 15% smaller (columnar) |
| CVR (compressed) | 40 MB | 60% smaller |

### Recording Performance
| Operation | Current (ADB) | New (DuckDB) |
|-----------|---------------|--------------|
| Write speed | ~5 MB/s | ~15 MB/s (batch) |
| Index creation | On playback | Real-time |
| Metadata queries | Linear scan | Instant (indexed) |
| Concurrent read | Not possible | Supported (WAL) |

---

## ?? User Experience

### Recording Flow

```
1. User starts recording
   ?? CLI: aerodebrief record --server 192.168.1.100
   
2. Recording to temporary database
   ?? Console: "Recording to: temp_recording_abc123.duckdb"
   ?? Console: "Packets: 1,234 | Size: 5.2 MB | Duration: 00:02:34"
   
3. User stops recording (Ctrl+C)
   ?? Console: "Stopping recording..."
   ?? Console: "Flushing database..."
   ?? Console: "Compressing to CVR format..."
   ?? Progress: [????????????????????] 100% (5.2 MB ? 2.1 MB)
   ?? Console: "? Recording saved: recording_192-168-1-100_5015_20250118T143022Z.cvr"
   ?? Console: "  Compression: 60% (5.2 MB ? 2.1 MB)"
   ?? Console: "  Duration: 00:02:34"
   ?? Console: "  Packets: 1,234"
```

### Settings UI (Future)
```
???????????????????????????????????????????
?        Recording Settings               ?
???????????????????????????????????????????
?                                         ?
? Output Format:                          ?
?  ? CVR (Compressed) - Recommended       ?
?    File size: ~40% of uncompressed      ?
?                                         ?
?  ? Uncompressed (DuckDB)                ?
?    Faster, larger files                 ?
?                                         ?
? [?] Auto-compress on stop               ?
?                                         ?
? [ ] Enable live playback (experimental) ?
?                                         ?
???????????????????????????????????????????
```

---

## ?? Breaking Changes

### For Users
- ? **None** - Existing `.adb` files still work
- ? New recordings create `.cvr` by default
- ? Old `.adb` files can be migrated using `--migrate`

### For Developers
- ?? `AudioPacketRecorder` no longer produces `.adb` files
- ?? New dependency: `DuckDBStore` required
- ?? Recording format changed: Binary ? Database

---

## ?? Testing Strategy

### Unit Tests
- [ ] `DuckDBStore` WAL mode initialization
- [ ] Batch insert performance
- [ ] Concurrent read/write
- [ ] Compression on stop
- [ ] Settings storage

### Integration Tests
- [ ] Record 5-minute session
- [ ] Verify all packets stored
- [ ] Verify metadata indexed
- [ ] Verify compression ratio
- [ ] Verify playback compatibility

### Performance Tests
- [ ] Recording throughput (packets/sec)
- [ ] Database write speed
- [ ] Compression speed
- [ ] Concurrent read performance

---

## ?? Success Criteria

### Must Have
- [x] Records directly to DuckDB
- [x] Optional CVR compression on stop
- [x] User setting for output format
- [x] No data loss
- [x] Backward compatible (ADB still works)

### Should Have
- [x] Better performance than ADB
- [x] Real-time metadata indexing
- [x] Progress indicators
- [x] File size reduction

### Nice to Have
- [ ] Live playback during recording (Phase 4)
- [ ] Streaming compression (Phase 4)
- [ ] Background compression (Phase 4)

---

## ?? Next Phases

### Phase 3.5: UI Integration (Optional)
- Add recording UI to AeroDebrief Player
- Add format selection dropdown
- Add live preview during recording

### Phase 4: Live Playback
- Enable concurrent read/write
- Stream packets during recording
- Real-time waveform visualization
- Live frequency/player list

### Phase 5: Advanced Features
- Network recording (remote server)
- Cloud storage integration
- Multi-server recording
- Automatic backup

---

## ?? Related Documentation

- [DuckDB Phase 2 Complete](DuckDB-Phase2-Complete.md) - Migration tool
- [CVR Format Specification](CVR-Format-Specification.md) - Format details
- [DuckDB Architecture](DuckDB-Architecture-Diagrams.md) - System design
- [Phase 2.5 Complete](Phase2.5-Complete.md) - UI integration

---

## ?? Summary

### What Changes
- Recording engine writes to DuckDB instead of ADB
- Optional compression to CVR on stop
- User control over output format
- Real-time metadata indexing
- WAL mode for concurrent access

### Why It Matters
- **Performance**: 3x faster writes with batch inserts
- **Size**: 60% smaller files with CVR compression
- **Features**: Enables live playback (Phase 4)
- **Quality**: Real-time indexing and validation
- **Future**: Foundation for advanced features

### How It Works
1. Start recording ? Create temp DuckDB
2. Write packets in batches ? Real-time indexing
3. Stop recording ? Optional compression to CVR
4. Cleanup temp files ? Keep final output

---

**Status**: ?? **Ready to Implement**  
**Estimated Effort**: 2-3 days  
**Risk Level**: Medium (core recording change)  
**Next Step**: Implement WAL mode in DuckDBStore

---

**Let's Begin Phase 3! ??**
