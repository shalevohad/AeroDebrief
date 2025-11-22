# CVR-Only Architecture Implementation

## Overview
This document describes the complete implementation of the CVR-only architecture in AeroDebrief, ensuring that users **ONLY** see `.cvr` (Combat Voice Recording) files, with all internal database files hidden in system directories.

## Architecture Components

### 1. IRecordingDbLifecycle Interface
**File:** `src\AeroDebrief.Core\Interfaces\Storage\IRecordingDbLifecycle.cs`

**Purpose:** Manages the lifecycle of recording database files in hidden working directories.

**Key Methods:**
- `CreateAsync()` - Creates a new database in a hidden session directory
- `ExtractFromCvrAsync(cvrPath)` - Extracts a CVR to a hidden session directory
- `CompressToCvrAsync(cvrPath)` - Compresses the database to a user-visible CVR file
- `CleanupAsync()` - Removes the hidden session directory and all contents

**Hidden Directory Structure:**
```
%LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\
    ??? recording.db  (NEVER shown to users)
```

### 2. RecordingDbLifecycle Implementation
**File:** `src\AeroDebrief.Core\Storage\RecordingDbLifecycle.cs`

**Key Features:**
- Creates hidden directories in `%LOCALAPPDATA%\AeroDebrief\Sessions\`
- Each session gets a unique GUID-based directory
- Directories are marked as hidden on Windows
- Automatic cleanup with retry logic for file locks
- Uses IArchiveCodec for compression/decompression

**Session Isolation:**
Each recording session is completely isolated:
- Unique session ID (GUID)
- Separate hidden directory
- No cross-contamination between sessions
- Clean separation of user files (.cvr) from internal files (.db)

### 3. RecordingArchiveService
**File:** `src\AeroDebrief.Core\Storage\RecordingArchiveService.cs`

**Purpose:** Public-facing service that enforces CVR-only architecture. This is the **ONLY** entry point users should use.

**Key Methods:**

#### Creating New Recordings
```csharp
var service = new RecordingArchiveService();
var unitOfWork = await service.CreateNewRecordingAsync(metadata);
// Database is now in: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\recording.db
// Users NEVER see this path
```

#### Opening CVR Files
```csharp
var unitOfWork = await service.OpenCvrAsync("path/to/recording.cvr", progress);
// CVR is extracted to: %LOCALAPPDATA%\AeroDebrief\Sessions\{session-id}\recording.db
// Users NEVER see the extracted database
```

#### Saving as CVR
```csharp
await service.SaveAsCvrAsync("path/to/output.cvr", progress);
// Internal database is compressed to user-specified .cvr file
// Hidden database remains until CloseAsync()
```

#### Closing Sessions
```csharp
await service.CloseAsync();
// Hidden session directory is completely removed
// No traces of internal database left
```

## Integration Points

### 1. File Dialogs
Use `RecordingArchiveService.GetFileDialogFilter()` for all file open/save dialogs:

```csharp
var dialog = new OpenFileDialog
{
    Title = "Select Recording File",
    Filter = RecordingArchiveService.GetFileDialogFilter(),
    FilterIndex = 1
};
```

**Result:** Only `.cvr` and `.adb` (legacy) files are shown to users.

### 2. File Validation
Use `RecordingArchiveService.IsUserVisibleFormat()` to validate file paths:

```csharp
if (!RecordingArchiveService.IsUserVisibleFormat(filePath))
{
    // Reject internal formats (.db, .cvr-debug)
    throw new ArgumentException("Invalid file format");
}
```

### 3. Recent Files Lists
Store only `.cvr` and `.adb` paths in recent files:

```csharp
if (RecordingArchiveService.IsUserVisibleFormat(filePath))
{
    AddToRecentFiles(filePath);
}
```

## Migration from Existing Code

### Before (Direct Database Access)
```csharp
// ? OLD - Exposes database files to users
var dbPath = "C:\\Users\\...\\recording.db";
var store = new DuckDBStore(dbPath);
await store.InitializeAsync();
```

### After (CVR-Only Architecture)
```csharp
// ? NEW - Only CVR files visible to users
var service = new RecordingArchiveService();

// Option 1: Create new recording
var unitOfWork = await service.CreateNewRecordingAsync(metadata);

// Option 2: Open existing CVR
var unitOfWork = await service.OpenCvrAsync("recording.cvr");

// All database operations through IUnitOfWork
await unitOfWork.Packets.InsertBatchAsync(packets);

// Save and cleanup
await service.SaveAsCvrAsync("output.cvr");
await service.CloseAsync();
```

## File Flow Diagrams

### Recording Flow
```
User Action                     Internal Process                    Visible to User
???????????                     ?????????????????                   ???????????????
Start Recording
    ?
    ??> Create Session          %LOCALAPPDATA%\AeroDebrief\
    ?   (hidden)                Sessions\{guid}\recording.db        [HIDDEN]
    ?
    ??> Write Packets           Write to hidden database            [HIDDEN]
    ?
    ??> Stop Recording
        ?
        ??> Finalize DB         Update statistics                   [HIDDEN]
        ?
        ??> Compress to CVR     recording.cvr                       ? VISIBLE
        ?
        ??> Cleanup Session     Delete hidden directory             [HIDDEN]
```

### Playback Flow
```
User Action                     Internal Process                    Visible to User
???????????                     ?????????????????                   ???????????????
Select CVR File                 recording.cvr                       ? VISIBLE
    ?
    ??> Open Recording
        ?
        ??> Create Session      %LOCALAPPDATA%\AeroDebrief\
        ?   (hidden)            Sessions\{guid}\                    [HIDDEN]
        ?
        ??> Extract CVR         recording.db extracted              [HIDDEN]
        ?
        ??> Open Database       Read from hidden DB                 [HIDDEN]
        ?
        ??> Close Recording
            ?
            ??> Cleanup         Delete hidden directory             [HIDDEN]
```

## Testing

### Unit Tests
**File:** `tests\AeroDebrief.Tests\Storage\RecordingArchiveServiceTests.cs`

**Test Coverage:**
- ? Database stored in hidden directory
- ? CVR files created (not .db files)
- ? Hidden directories cleaned up
- ? CVR extraction to hidden directory
- ? File dialog filters don't expose .db
- ? Only CVR and ADB are user-visible formats
- ? CVR extension required for output
- ? Non-CVR files rejected
- ? Multiple recordings use different sessions

### Manual Verification Checklist

1. **File Dialog Test:**
   - Open recording file dialog
   - Verify only `.cvr` and `.adb` files shown by default
   - `.db` files should not appear in primary filters

2. **Recording Test:**
   - Start and stop a recording
   - Check output directory - should only see `.cvr` file
   - Verify no `.db` files in user directories
   - Check `%LOCALAPPDATA%\AeroDebrief\Sessions\` - should be empty after cleanup

3. **Playback Test:**
   - Open a `.cvr` file
   - Verify playback works correctly
   - Close the file
   - Check `%LOCALAPPDATA%\AeroDebrief\Sessions\` - should be empty after cleanup

4. **Recent Files Test:**
   - Open several recordings
   - Check recent files list
   - Verify all entries end with `.cvr` or `.adb`

## Configuration

### RecordingConstants
**File:** `src\AeroDebrief.Core\RecordingConstants.cs`

```csharp
// MUST be true in production builds
public const bool FORCE_CVR_COMPRESSION = true;

#if DEBUG
// Allow override in debug builds only
public const bool ALLOW_COMPRESSION_OVERRIDE = true;
#else
// Force compression in release builds
public const bool ALLOW_COMPRESSION_OVERRIDE = false;
#endif
```

**Important:** Never ship a build with `FORCE_CVR_COMPRESSION = false` to users.

## Security Considerations

### Hidden Directory Permissions
- Default: Uses standard user permissions via `Environment.SpecialFolder.LocalApplicationData`
- Windows: Directories are marked with `FileAttributes.Hidden`
- Cleanup: Automatic removal prevents data accumulation

### Data Isolation
- Each session has a unique directory
- No cross-session data leakage
- Clean separation between user files and internal files

### Temporary File Cleanup
- Automatic cleanup on session close
- Retry logic handles file locks
- Best-effort cleanup on application exit

## Performance Considerations

### Extraction Performance
- CVR files are decompressed to hidden directories (not temp)
- Zstandard compression provides fast decompression
- Extracted databases persist for session duration

### Storage Overhead
- Hidden databases are removed after session closes
- No long-term storage overhead
- Temporary spike during open session

### Cleanup Performance
- Async cleanup with exponential backoff
- Graceful handling of file locks
- Best-effort cleanup (doesn't block application exit)

## Troubleshooting

### Issue: Session Directory Not Cleaned Up
**Symptom:** Directories remain in `%LOCALAPPDATA%\AeroDebrief\Sessions\`

**Causes:**
- File locks from antivirus software
- Database connections not properly disposed
- Application crashed before cleanup

**Solution:**
- Directories will be cleaned up on next application start
- Manual cleanup: Delete old session directories

### Issue: Cannot Open CVR File
**Symptom:** Error when opening a CVR file

**Diagnosis:**
1. Check file extension is `.cvr`
2. Verify file is not corrupted
3. Check available disk space in `%LOCALAPPDATA%`
4. Review application logs

### Issue: CVR Compression Disabled
**Symptom:** Recordings saved as `.cvr-debug` instead of `.cvr`

**Cause:** `RecordingConstants.FORCE_CVR_COMPRESSION = false`

**Solution:** Set to `true` for production builds

## Best Practices

### DO ?
- Use `RecordingArchiveService` for all recording operations
- Always call `CloseAsync()` when done with a recording
- Use `GetFileDialogFilter()` for file dialogs
- Validate file paths with `IsUserVisibleFormat()`
- Store only `.cvr` paths in recent files

### DON'T ?
- Don't expose database paths to users
- Don't reference `recording.db` in UI strings
- Don't show session directories to users
- Don't skip `CloseAsync()` cleanup
- Don't allow users to select `.db` files directly

## Future Enhancements

### Potential Improvements
1. **Session Persistence:** Store active sessions in a registry for recovery
2. **Background Cleanup:** Periodic cleanup of orphaned session directories
3. **Compression Options:** Allow users to choose compression level
4. **Multi-format Support:** Add support for other archive formats
5. **Cloud Integration:** Direct upload to cloud storage

### Backwards Compatibility
The architecture maintains full backwards compatibility:
- `.adb` files automatically convert to CVR on first open
- `.db` files can be opened internally (not shown in dialogs)
- `.cvr-debug` files supported for development testing

## Summary

The CVR-only architecture successfully hides all internal database implementation details from users:

? Users see ONLY `.cvr` files  
? Database files stored in hidden system directories  
? Automatic cleanup prevents data accumulation  
? Clean separation of user files from internal files  
? Full test coverage  
? Backwards compatible with existing recordings  

All requirements from the original specification are fully implemented and tested.
