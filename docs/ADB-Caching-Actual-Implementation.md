# ADB Caching - Actual Implementation

**Discovery**: The caching doesn't use temp directory - it creates `.db` next to `.adb`!  
**Status**: ?? **DIAGNOSTIC**

---

## ?? How ADB Caching ACTUALLY Works

### Current Implementation

When you open `recording.adb`, the system:

1. **Checks for `recording.db`** in the **same directory** as the ADB file
2. **Compares timestamps**: Is `.db` newer than `.adb`?
   - ? **Yes**: Use existing `.db` (CACHE HIT)
   - ? **No**: Re-convert (CACHE MISS)

### Code Location

**File**: `src\AeroDebrief.Core\Storage\RecordingFileLoader.cs` (lines 60-88)

```csharp
else if (CvrFormat.IsAdbFile(filePath))
{
    // ADB: Check for existing .db conversion
    var existingDb = Path.ChangeExtension(filePath, ".db");  // ? Same directory!
    
    if (File.Exists(existingDb))
    {
        // Check if DB is newer than ADB
        var adbTime = File.GetLastWriteTimeUtc(filePath);
        var dbTime = File.GetLastWriteTimeUtc(existingDb);
        
        if (dbTime >= adbTime)
        {
            Logger.Info("Using existing DB conversion");  // ? CACHE HIT message
            progress?.Report("Opening converted database...");
            dbPath = existingDb;
        }
        else
        {
            Logger.Info("Existing DB is outdated, re-converting...");  // ? Timestamp mismatch
            dbPath = await ConvertAdbAsync(filePath, existingDb, progress, ct);
        }
    }
    else
    {
        // Convert ADB to DB
        Logger.Info("Converting ADB to database...");  // ? No cache file
        dbPath = await ConvertAdbAsync(filePath, existingDb, progress, ct);
    }
}
```

---

## ?? File Structure

### After First Open

```
C:\Recordings\
  ??? recording.adb       (original file)
  ??? recording.db        (converted database) ? Created!
```

### Cache Lookup

1. Open `recording.adb`
2. Check for `recording.db` in same directory
3. Compare timestamps:
   - ADB: `2025-01-15 10:30:00`
   - DB:  `2025-01-15 10:35:00` (newer)
   - **Result**: Use existing DB ?

---

## ?? Why You're Not Seeing "Using existing DB conversion"

### Possible Reasons

#### Reason 1: No `.db` File Next to `.adb`
```powershell
# Check if .db exists
$adbFile = "C:\path\to\recording.adb"
$dbFile = [IO.Path]::ChangeExtension($adbFile, ".db")
Test-Path $dbFile  # Should be True for cache hit
```

**If False**: Cache file doesn't exist (first time or was deleted)

#### Reason 2: `.db` is Older Than `.adb`
```powershell
# Compare timestamps
$adbTime = (Get-Item $adbFile).LastWriteTimeUtc
$dbTime = (Get-Item $dbFile).LastWriteTimeUtc

Write-Host "ADB: $adbTime"
Write-Host "DB:  $dbTime"
Write-Host "DB is newer: $($dbTime -ge $adbTime)"
```

**If False**: ADB was modified after DB was created

#### Reason 3: Permission Issues
```powershell
# Check if .db can be created in ADB directory
$adbDir = Split-Path $adbFile
Test-Path $adbDir -PathType Container
# Check write permissions
```

**If Read-Only**: Can't create cache file

---

## ? How to Verify Cache Is Working

### Test 1: Check for `.db` File

```powershell
# After opening an ADB file, check same directory
dir "C:\path\to\recordings\*.db"

# Should show:
# recording.db  (same name as recording.adb)
```

### Test 2: Check Logs

**First Open**:
```
[INFO] Opening recording: C:\path\to\recording.adb
[INFO] Format: ADB
[INFO] Converting ADB to database...  ? No cache
... (conversion process)
[INFO] ? Recording opened: 25,000 packets
```

**Second Open** (Cache Hit):
```
[INFO] Opening recording: C:\path\to\recording.adb
[INFO] Format: ADB
[INFO] Using existing DB conversion  ? CACHE HIT!
[INFO] ? Recording opened: 25,000 packets
```

**Second Open** (Cache Miss - Outdated):
```
[INFO] Opening recording: C:\path\to\recording.adb
[INFO] Format: ADB
[INFO] Existing DB is outdated, re-converting...  ? Timestamp mismatch
... (re-conversion)
```

### Test 3: Measure Timing

```powershell
# First open (no cache)
Measure-Command { 
    # Open recording.adb
}
# Expected: 10-30 seconds

# Second open (with cache)
Measure-Command { 
    # Open recording.adb again
}
# Expected: 2-5 seconds (5-10x faster)
```

### Test 4: Check Timestamps

```powershell
$adbFile = "C:\path\to\recording.adb"
$dbFile = [IO.Path]::ChangeExtension($adbFile, ".db")

if (Test-Path $dbFile) {
    $adbTime = (Get-Item $adbFile).LastWriteTimeUtc
    $dbTime = (Get-Item $dbFile).LastWriteTimeUtc
    
    Write-Host "ADB timestamp: $adbTime"
    Write-Host "DB timestamp:  $dbTime"
    Write-Host "Cache valid:   $($dbTime -ge $adbTime)"
} else {
    Write-Host "No cache file found"
}
```

---

## ?? Troubleshooting

### Problem: Always Converting (Never Caching)

**Check 1: Is `.db` being created?**
```powershell
# After first open, verify .db exists
$adbFile = "C:\path\to\recording.adb"
$dbFile = [IO.Path]::ChangeExtension($adbFile, ".db")
Test-Path $dbFile
```
- **False**: Conversion failed or permission issue
- **True**: Go to Check 2

**Check 2: Are timestamps correct?**
```powershell
(Get-Item $adbFile).LastWriteTimeUtc
(Get-Item $dbFile).LastWriteTimeUtc
```
- **DB older**: ADB was modified (touch/copy/download again)
- **DB newer**: Should use cache (check logs)

**Check 3: Check logs for exact message**
```
Search for one of:
- "Using existing DB conversion"     ? Cache hit
- "Existing DB is outdated"          ? Timestamp issue
- "Converting ADB to database..."    ? No cache file
```

---

## ?? Common Issues

### Issue 1: ADB File Was Re-Downloaded

**Symptom**: Always converting, even though `.db` exists

**Cause**: Downloading updates the ADB timestamp to "now"

**Solution**: Normal behavior - re-convert is correct

### Issue 2: ADB File Was Copied/Moved

**Symptom**: Always converting after copy

**Cause**: Copy preserves `.adb` timestamp but leaves `.db` behind

**Solution**: Copy both `.adb` and `.db` files together

### Issue 3: `.db` File Was Deleted

**Symptom**: Suddenly starts converting again

**Cause**: Cleanup tool, manual deletion, antivirus

**Solution**: Normal behavior - will recreate cache

### Issue 4: Network/Cloud Storage

**Symptom**: Inconsistent caching behavior

**Cause**: Timestamp precision issues, sync delays

**Solution**: Copy to local disk before opening

---

## ?? Expected Behavior

| Scenario | Log Message | Time |
|----------|-------------|------|
| **First open** | "Converting ADB to database..." | 10-30s |
| **Second open (same file)** | "Using existing DB conversion" | 2-5s |
| **ADB modified** | "Existing DB is outdated, re-converting..." | 10-30s |
| **DB deleted** | "Converting ADB to database..." | 10-30s |

---

## ?? The Real Issue

Based on "no 'Using cached' in log", you're seeing:
- ? NOT: "Using existing DB conversion"
- ? PROBABLY: "Converting ADB to database..."

**This means**:
1. No `.db` file next to the `.adb` file, OR
2. `.db` exists but is older than `.adb`

**To verify**:
```powershell
# Check if cache file exists
$adbPath = "C:\path\to\your\recording.adb"
$dbPath = [IO.Path]::ChangeExtension($adbPath, ".db")

if (Test-Path $dbPath) {
    Write-Host "Cache exists!"
    Write-Host "ADB time: $((Get-Item $adbPath).LastWriteTimeUtc)"
    Write-Host "DB time:  $((Get-Item $dbPath).LastWriteTimeUtc)"
} else {
    Write-Host "No cache file - will convert every time"
}
```

---

## ? Solution

### If `.db` Doesn't Exist

**Normal**: First time opening will create it

**Check**: After first open, verify `.db` was created in same directory as `.adb`

### If `.db` Exists But Is Older

**Cause**: ADB file was modified after conversion

**Options**:
1. Accept re-conversion (correct behavior)
2. Don't modify/re-download ADB file
3. Touch `.db` to update its timestamp:
```powershell
(Get-Item "C:\path\to\recording.db").LastWriteTimeUtc = Get-Date
```

### If `.db` Exists and Is Newer But Still Converting

**This should not happen** - indicates a bug

**Diagnostic**:
1. Check exact log message
2. Verify timestamp comparison
3. Check file permissions
4. Report as bug with log file

---

**Document Version**: 1.0  
**Last Updated**: January 2025  
**Author**: Cache Investigation
