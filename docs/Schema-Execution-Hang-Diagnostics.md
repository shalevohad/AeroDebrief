# Schema Execution Hang Diagnostics

**Date**: 2025-01-20  
**Issue**: Application hangs at "Reading schema from: ...Schema.sqlite.sql"  
**Status**: ?? **ENHANCED DIAGNOSTICS**

---

## ?? Current Issue

The application is now progressing further but hangs at:
```
2025-11-22 22:05:45.8367 | DEBUG | AeroDebrief.Core.Storage.Sqlite.SqliteUnitOfWork | 
Reading schema from: C:\Users\Ohad\source\repos\AeroDebrief\src\AeroDebrief.UI\bin\x64\Debug\net9.0-windows\Storage\Schema.sqlite.sql
```

This means:
- ? Schema file found successfully
- ? Path resolution working
- ? Hanging during file read or SQL execution

---

## ?? Potential Causes

### 1. File Access Issues
- File might be locked by another process
- Antivirus scanning the file
- File permissions issue
- File encoding issue

### 2. SQLite Execution Issues
- Large batch execution causing timeout
- Specific SQL statement causing hang
- Lock contention on database file
- WAL mode not properly initialized

### 3. Async/Threading Issues
- Deadlock in async operation
- UI thread blocking
- CancellationToken not being honored

---

## ? Improvements Made

### Enhanced Logging in CreateSchemaAsync

Added detailed logging to pinpoint exactly where the hang occurs:

```csharp
1. Log file size and accessibility
2. Log successful file read
3. Split schema into individual statements
4. Execute statements one-by-one
5. Log progress every 5 statements
6. Catch and log errors for specific statements
```

**Benefits**:
- Know exactly which SQL statement hangs
- See progress through schema creation
- Better error messages for debugging

### Example Enhanced Logging Output

```
DEBUG | Reading schema from: C:\...\Schema.sqlite.sql
DEBUG | Schema file size: 4253 bytes
DEBUG | Schema read successfully, length: 4253 characters
DEBUG | Executing 15 SQL statements...
DEBUG | Executed 5/15 statements...
DEBUG | Executed 10/15 statements...
DEBUG | ? Database schema created successfully (15 statements executed)
```

If it hangs, you'll see exactly where:
```
DEBUG | Reading schema from: C:\...\Schema.sqlite.sql
DEBUG | Schema file size: 4253 bytes
DEBUG | Schema read successfully, length: 4253 characters
DEBUG | Executing 15 SQL statements...
DEBUG | Executed 5/15 statements...
[HANGS HERE - knows it's around statement 6-10]
```

---

## ?? Steps to Test

### 1. Stop Current Application
- Force close the hung application
- Check Task Manager for any lingering processes
- Delete any `.db-wal` or `.db-shm` files in temp directory

### 2. Rebuild Solution
```powershell
# Clean and rebuild
dotnet clean
dotnet build
```

### 3. Run with Enhanced Logging
- Start the application
- Watch the debug output carefully
- Note which log message appears last before hang

### 4. Analyze the Hang Point

Based on last log message:

| Last Log Message | What's Hanging | Next Step |
|------------------|----------------|-----------|
| "Reading schema from..." | File read | Check file access |
| "Schema file size: X bytes" | File read | Check file encoding |
| "Schema read successfully..." | Statement parsing | Check schema syntax |
| "Executing X SQL statements..." | SQL execution | Check which statement |
| "Executed 5/15 statements..." | Specific SQL statement | Check schema file |

---

## ?? Quick Fixes to Try

### Fix 1: Check File Access
```powershell
# Check if file is readable
Get-Content "C:\Users\Ohad\source\repos\AeroDebrief\src\AeroDebrief.UI\bin\x64\Debug\net9.0-windows\Storage\Schema.sqlite.sql"
```

### Fix 2: Delete Database Files
```powershell
# Delete all temp database files and WAL files
Remove-Item "$env:TEMP\temp*.db*" -Force
Remove-Item "$env:TEMP\*.db-wal" -Force
Remove-Item "$env:TEMP\*.db-shm" -Force
```

### Fix 3: Check for Locks
```powershell
# Check for file locks on database files
Get-Process | Where-Object {$_.Modules.FileName -like "*sqlite*"}
```

### Fix 4: Disable Antivirus (Temporary)
If using Windows Defender or other antivirus:
1. Temporarily disable real-time protection
2. Add exclusion for `*.db` files in temp folder
3. Test again

### Fix 5: Run as Administrator
- Right-click Visual Studio
- Run as Administrator
- Try again

---

## ?? Expected Behavior (After Fix)

### Complete Success Log
```
2025-01-20 22:10:00.000 | DEBUG | Opening SQLite connection...
2025-01-20 22:10:00.001 | DEBUG | Connection opened successfully
2025-01-20 22:10:00.002 | DEBUG | Configuring connection...
2025-01-20 22:10:00.003 | DEBUG | Configuring SQLite for optimal performance...
2025-01-20 22:10:00.150 | DEBUG | ? SQLite configured with WAL mode and performance optimizations
2025-01-20 22:10:00.151 | DEBUG | Creating database schema...
2025-01-20 22:10:00.152 | DEBUG | Reading schema from: C:\...\Schema.sqlite.sql
2025-01-20 22:10:00.153 | DEBUG | Schema file size: 4253 bytes
2025-01-20 22:10:00.154 | DEBUG | Schema read successfully, length: 4253 characters
2025-01-20 22:10:00.155 | DEBUG | Executing 15 SQL statements...
2025-01-20 22:10:00.200 | DEBUG | Executed 5/15 statements...
2025-01-20 22:10:00.250 | DEBUG | Executed 10/15 statements...
2025-01-20 22:10:00.300 | DEBUG | Executed 15/15 statements...
2025-01-20 22:10:00.301 | DEBUG | ? Database schema created successfully (15 statements executed)
2025-01-20 22:10:00.302 | DEBUG | Updating recording metadata...
2025-01-20 22:10:00.310 | DEBUG | Initializing packet repository...
2025-01-20 22:10:00.320 | INFO  | ? SQLite Unit of Work initialized: C:\...\temp.db
```

**Total Time**: < 1 second

---

## ?? If Still Hanging

### Collect Diagnostic Info

1. **Note exact log output**:
   ```
   Last message before hang:
   [paste here]
   ```

2. **Check schema file manually**:
   ```powershell
   # View schema file
   Get-Content "src\AeroDebrief.Core\Storage\Schema.sqlite.sql"
   ```

3. **Try minimal schema**:
   Create a test with just one table:
   ```sql
   CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY);
   ```

4. **Check SQLite version**:
   Add this to code:
   ```csharp
   var version = await _connection.ExecuteScalarAsync<string>("SELECT sqlite_version()");
   Logger.Info($"SQLite version: {version}");
   ```

### Alternative: Use Inline Schema

If file reading is the issue, embed schema directly in code:

```csharp
private const string SCHEMA = @"
CREATE TABLE IF NOT EXISTS packets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ...
);
";

private async Task CreateSchemaAsync(CancellationToken ct = default)
{
    await _connection.ExecuteAsync(SCHEMA);
}
```

---

## ?? What Changed

### Files Modified

1. **SqliteUnitOfWork.cs**
   - Enhanced `CreateSchemaAsync()` with:
     - File size logging
     - Statement-by-statement execution
     - Progress logging every 5 statements
     - Better error handling
   - Enhanced `InitializeAsync()` with:
     - Step-by-step logging
     - Try-catch with detailed error logging

### Why These Changes Help

1. **File Size Check**: Confirms file is accessible
2. **Character Count**: Confirms file read succeeded
3. **Statement-by-Statement**: Identifies exact problematic SQL
4. **Progress Logging**: Shows execution is progressing
5. **Error Logging**: Captures specific SQL errors

---

## ? Action Plan

### Immediate Actions

1. ? Stop hung application
2. ? Delete temp database files
3. ? Rebuild solution
4. ? Run and capture full log output
5. ? Report which statement hangs (based on logs)

### Based on Results

#### If hangs at file read:
- Check file permissions
- Check antivirus
- Try running as admin

#### If hangs at specific SQL statement:
- Check that statement in schema file
- Try executing that statement manually
- Simplify the statement

#### If hangs at random points:
- Possible threading issue
- Try synchronous execution
- Check for deadlocks

---

## ?? Success Criteria

- [ ] All 15+ SQL statements execute
- [ ] Total time < 1 second
- [ ] No hanging or timeouts
- [ ] Clear progress logs visible
- [ ] ADB file loads successfully

---

**Created**: 2025-01-20  
**Status**: Enhanced diagnostics deployed  
**Next**: Rebuild and test with new logging  
**Expected**: Pinpoint exact hang location
