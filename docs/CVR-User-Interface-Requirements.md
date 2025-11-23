# CVR User Interface Requirements

## Overview
AeroDebrief uses an internal SQLite database for recording storage, but this implementation detail is **completely hidden from users**. Users interact exclusively with `.cvr` (Combat Voice Recording) archive files.

## Core Requirements

### 1. File Visibility
**ONLY `.cvr` files are exposed to users in all UI contexts:**
- Open File dialogs
- Save File dialogs
- Recent files lists
- File information displays
- Documentation and help text

### 2. Internal Database Isolation
The actual database file (`recording.db`) is:
- Stored only in temporary/hidden working directories
- Never exposed through file dialogs
- Never referenced in UI strings or paths
- Automatically cleaned up when sessions end
- Not visible in user-accessible locations

### 3. File Format Architecture

```
User Layer:           .cvr (Compressed Archive) - ONLY format users see
                           ?
Archive Codec:        IArchiveCodec (Zstd/Brotli compression)
                           ?
Internal Layer:       .db (SQLite Database) - Hidden in temp folders
```

## Implementation Details

### File Extensions by Context

| Extension | Visibility | Purpose | Location |
|-----------|-----------|---------|----------|
| `.cvr` | **User-facing** | Compressed recording archive | User-selected directories |
| `.adb` | **User-facing** | Legacy format (auto-converts) | User-selected directories |
| `.db` | Internal only | SQLite database | `%TEMP%\aerodebrief_*` only |
| `.cvr-debug` | Development only | Uncompressed testing format | Development builds only |

### File Dialog Filters

**Current Implementation (Correct):**
```csharp
"All Recording Files|*.cvr;*.adb|" +
"Combat Voice Recording (*.cvr)|*.cvr|" +
"Legacy Recording (*.adb)|*.adb|" +
"All Files|*.*"
```

**What NOT to do:**
```csharp
// ? WRONG - Exposes internal database files
"All Recording Files|*.cvr;*.adb;*.db|"
"Database (*.db)|*.db|"
```

### Recording Process Flow

1. **Start Recording:**
   - Create temporary database: `%TEMP%\aerodebrief_recording_<guid>.db`
   - Record packets directly to temporary database
   - If live playback enabled, allow concurrent reads from temp DB

2. **Stop Recording:**
   - Finalize database (rebuild stats, mark complete)
   - **Mandatory compression**: Compress temp DB ? `.cvr` archive
   - Delete temporary database
   - Save `.cvr` file to user-specified location
   - Clean up all temporary files

3. **Open Recording:**
   - User selects `.cvr` file from dialog
   - Extract to temporary location: `%TEMP%\AeroDebrief_<guid>\recording.db`
   - Open database from temporary location
   - User never sees the extracted `.db` file

### Constants and Configuration

**RecordingConstants.cs:**
```csharp
// MUST be true in all release builds
public const bool FORCE_CVR_COMPRESSION = true;

// Only true in DEBUG builds for testing
#if DEBUG
public const bool ALLOW_COMPRESSION_OVERRIDE = true;
#else
public const bool ALLOW_COMPRESSION_OVERRIDE = false;
#endif
```

### Debug Mode Considerations

**Development Testing:**
- When `FORCE_CVR_COMPRESSION = false` (development only)
- Files are saved as `.cvr-debug` (NOT `.db`)
- This maintains CVR branding even in testing
- `.cvr-debug` files are NOT shown in user file dialogs
- Production builds MUST have `FORCE_CVR_COMPRESSION = true`

## Compliance Checklist

? **File Dialogs:**
- Open dialog only shows `.cvr` and `.adb` (legacy)
- Save dialog (if implemented) only allows `.cvr` extension
- No `.db` or `.duckdb` extensions in user-facing filters

? **File Paths:**
- User-selected paths always use `.cvr` extension
- Internal database paths use temp directories only
- Temp directories use `AeroDebrief_*` or `aerodebrief_*` prefix

? **UI Strings:**
- All format names use "CVR" or "Combat Voice Recording"
- No references to "database", "SQLite", or "DB files"
- Error messages refer to "recording files" not "database files"

? **Documentation:**
- User-facing docs mention only `.cvr` format
- Technical/developer docs may explain internal architecture
- README focuses on `.cvr` as the recording format

? **Temporary File Management:**
- All temp files created in `%TEMP%` or `%LOCALAPPDATA%`
- Temp directories have clear prefixes for identification
- Cleanup on application exit and session closure

## Testing Verification

### Manual Tests

1. **Open File Dialog Test:**
   - Menu ? File ? Open Recording
   - Verify only `.cvr` and `.adb` files are visible by default
   - "All Files" filter may show `.db` but shouldn't be default

2. **Recording Test:**
   - Start a recording
   - Check output directory - should only see `.cvr` file
   - Verify no `.db` files in user directories
   - Check `%TEMP%` - temp `.db` should be deleted after recording

3. **Recent Files Test:**
   - Open several recordings
   - Check recent files list
   - Verify all entries end with `.cvr` or `.adb`

4. **File Info Display Test:**
   - Select various recordings
   - Check file info/properties display
   - Verify format shown as "CVR" not "Database"

### Automated Tests

```csharp
[Test]
public void FileDialog_ShouldNotExpose_DatabaseExtension()
{
    var filters = RecordingFileLoader.GetFileFilters();
    Assert.That(filters, Does.Not.Contain(".db"));
    Assert.That(filters, Does.Contain(".cvr"));
}

[Test]
public void Recording_MustCreateCvrFile_NotDbFile()
{
    // Record audio packets
    var outputPath = recorder.StopRecording();
    Assert.That(Path.GetExtension(outputPath), Is.EqualTo(".cvr"));
}

[Test]
public void TempDatabase_MustBeInTempDirectory()
{
    var tempPath = recorder.GetTempDatabasePath();
    Assert.That(tempPath, Does.Contain(Path.GetTempPath()));
}
```

## Migration Notes

### Legacy Code Updates

If you find code that violates these requirements:

1. **File Dialog Filters:**
   ```csharp
   // ? Before
   "Database (*.db)|*.db"
   
   // ? After - Remove from user-facing filters
   // .db support remains internal via GetSupportedExtensions()
   ```

2. **Output File Paths:**
   ```csharp
   // ? Before
   var output = Path.ChangeExtension(input, ".db");
   
   // ? After
   var output = Path.ChangeExtension(input, ".cvr");
   ```

3. **Format Display Names:**
   ```csharp
   // ? Before
   "SQLite Database"
   
   // ? After
   "Combat Voice Recording"
   ```

## References

- **RecordingFileLoader.cs** - File format detection and loading
- **CvrFormat.cs** - CVR compression/decompression
- **AudioPacketRecorder.cs** - Recording process with mandatory compression
- **RecordingConstants.cs** - Compression enforcement constants
- **FileSourceViewModel.cs** - UI file selection logic

## Contact

For questions about CVR format requirements or implementation:
- See: `docs/SQLite-Migration-Implementation-Plan.md`
- See: `docs/SQLite-Migration-Status.md`
