# CVR Format Quick Reference Card

## ?? One-Page Summary

---

## File Formats

| Extension | Name | Use Case | Shown to Users |
|-----------|------|----------|----------------|
| `.cvr` | Combat Voice Recording | **PRIMARY** - Distribution, archival | ? Yes |
| `.adb` | Legacy format | Backward compatibility | ? Yes (Legacy) |
| `.duckdb` | Uncompressed database | Developer testing | ? No |

---

## Quick Commands

### Open Any Recording
```csharp
var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath);
// Works for .cvr, .adb, or .duckdb
```

### Migrate Legacy File
```bash
AeroDebrief.CLI.exe --migrate recording.adb
```

### Compress to CVR
```csharp
await CvrFormat.CompressToCvrAsync("recording.duckdb", "recording.cvr");
```

### Stream Packets
```csharp
await foreach (var packet in store.StreamPacketsAsync(
    fromTime: TimeSpan.FromMinutes(5),
    frequencies: new[] { 251.0 }))
{
    // Process packet
}
```

---

## File Dialog Filters

```csharp
// User-facing (DuckDB hidden)
"Combat Voice Recordings|*.cvr;*.adb|" +
"CVR Files (*.cvr)|*.cvr|" +
"Legacy ADB Files (*.adb)|*.adb|" +
"All Files (*.*)|*.*"
```

---

## Format Names (Display)

```csharp
CvrFormat.GetFormatName(filePath);
// Returns:
// "CVR (Combat Voice Recording)"  - for .cvr
// "ADB (Legacy Format)"            - for .adb  
// "CVR (Uncompressed)"             - for .duckdb (hidden)
```

---

## Database Schema

### Main Tables
- `packets` - Audio packets (columnar)
- `recording_info` - Metadata (single row)
- `frequency_stats` - Pre-computed frequency data
- `player_stats` - Pre-computed player data

### Key Indexes
- `idx_time` - On `relative_ms`
- `idx_frequency` - On `frequency`
- `idx_player` - On `player_name`
- `idx_freq_time` - Composite

---

## Performance

| Metric | Value |
|--------|-------|
| File size (1hr) | ~120 MB (vs 250 MB ADB) |
| Compression ratio | 40-60% smaller |
| Load time | <1s |
| Metadata access | <1ms |
| Query speed | 10-100x faster |

---

## Common Queries

### Get All Frequencies
```sql
SELECT * FROM frequency_stats ORDER BY frequency;
```

### Filter by Frequency
```sql
SELECT * FROM packets 
WHERE frequency = 251000000 
  AND relative_ms BETWEEN 60000 AND 120000;
```

### Get Player Activity
```sql
SELECT * FROM packets 
WHERE player_name = 'Viper1-1'
ORDER BY timestamp_utc;
```

---

## Migration Checklist

- [ ] Update to latest AeroDebrief
- [ ] Choose method (auto/CLI)
- [ ] Convert files
- [ ] Verify conversion
- [ ] Delete old ADB/PKIDX files
- [ ] Compress to CVR (optional)

---

## User Terminology

? **Use These Terms:**
- "Combat Voice Recording"
- "CVR file"
- "Legacy format" (for ADB)
- "Uncompressed" (if user opens .duckdb)

? **Don't Use:**
- "DuckDB"
- "Database file"
- "Columnar storage"

---

## Error Messages

| Error | Solution |
|-------|----------|
| File corrupted | Verify with `--analyze` |
| Out of disk space | Need 1.5x source size |
| Conversion slow | Check disk speed, antivirus |
| Cannot open | Update AeroDebrief |

---

## File Naming Convention

```
recording_srv_<ip>_<port>_t<timestamp>.cvr
```

**Example:**
```
recording_srv_192-168-1-100_5002_t20250118T143022Z.cvr
```

---

## API Cheat Sheet

```csharp
// Open file (any format)
var (store, tempPath) = await RecordingFileLoader.OpenAsync(path);

// Get metadata (instant)
var frequencies = await store.GetFrequenciesAsync();
var players = await store.GetPlayersAsync();

// Stream packets
await foreach (var p in store.StreamPacketsAsync(TimeSpan.Zero)) { }

// Cleanup
store.Dispose();
RecordingFileLoader.Cleanup(tempPath);
```

---

## Package Requirements

- **DuckDB.NET.Data** v1.4.1
- **SharpCompress** v0.41.0

---

## Documentation Links

- [Phase 1](DuckDB-Phase1-Complete.md) - Implementation
- [Phase 2](DuckDB-Phase2-Complete.md) - Migration
- [CVR Spec](CVR-Format-Specification.md) - Format details
- [Migration Guide](DuckDB-Migration-Guide.md) - How-to

---

**Print this page for quick reference!** ??
