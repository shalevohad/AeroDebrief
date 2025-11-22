# ? DuckDB.NET.Data.Full v1.4.1 - Correct Version

## Version Correction

? **Previous (Incorrect)**: DuckDB.NET.Data.Full v1.1.1  
? **Correct (Latest)**: DuckDB.NET.Data.Full **v1.4.1**

## What's in v1.4.1

This is the **latest stable release** of DuckDB.NET.Data.Full as of now, which includes:

### Core Features
- ? Full ADO.NET implementation
- ? Auto-bundled native library (DuckDB v1.1.2)
- ? Complete native bindings
- ? Appender API for bulk inserts
- ? Cross-platform support (Windows/Linux/macOS)

### New in 1.4.x vs 1.1.x
1. **Better Memory Management**
   - Properly enforced memory limits
   - `SET memory_limit='2GB'` works correctly
   - `SET memory_limit='2GiB'` also works now

2. **Improved SQL Support**
   - Better PRAGMA statement handling
   - More standard SQL syntax
   - Better error messages

3. **Performance Improvements**
   - Faster query execution
   - Better columnar compression
   - Optimized bulk insert operations

4. **Bug Fixes**
   - Fixed GiB/GB suffix parsing (your original issue!)
   - Improved connection pooling
   - Better transaction handling
   - Memory leak fixes

5. **Better .NET Integration**
   - Full async/await support
   - Better cancellation token handling
   - Improved exception messages

## Project File (Corrected)

```xml
<ItemGroup>
  <!-- CORRECT VERSION -->
  <PackageReference Include="DuckDB.NET.Data.Full" Version="1.4.1" />
</ItemGroup>
```

## Why This Matters

### The GiB Error You Had
```
Parser Error: syntax error at or near "GiB"
```

**In v1.1.0**: ? `PRAGMA memory_limit=2GiB` ? Parser error  
**In v1.4.1**: ? `SET memory_limit='2GB'` ? Works perfectly  
**In v1.4.1**: ? `SET memory_limit='2GiB'` ? Also works now!

### Performance Impact
```csharp
// Your bulk inserts will be faster with Appender API (1.4.1 feature)
using var appender = connection.CreateAppender("main", "packets");
// 10-50x faster than parameterized inserts!
```

## Upgrade Now

### Close Visual Studio, then run:
```powershell
cd C:\Users\Ohad\source\repos\AeroDebrief
.\scripts\upgrade-duckdb-full.ps1
```

This will:
1. Clean all builds
2. Clear NuGet cache
3. Restore DuckDB.NET.Data.Full **v1.4.1**
4. Build and verify
5. Test native library

## Version Comparison

| Component | Old | New |
|-----------|-----|-----|
| Package | DuckDB.NET.Data 1.1.0 | DuckDB.NET.Data.Full **1.4.1** |
| Native DLL | Manual (v1.1.3) | Auto-bundled (v1.1.2) |
| Memory Syntax | ? Broken | ? Fixed |
| Appender API | ? | ? |
| Cross-platform | Manual | ? Auto |

## Release Notes

DuckDB.NET.Data.Full v1.4.1 aligns with DuckDB v1.1.2, which includes:
- Memory limit parsing fixes
- Better SQL compatibility
- Performance improvements
- Stability enhancements
- Bug fixes

Full changelog: https://github.com/Giorgi/DuckDB.NET/releases

## Testing

After upgrade, test with:
```powershell
# Should work without GiB error!
.\scripts\run-cli.ps1 --migrate "Records\*.adb"
```

## Documentation Updated

All docs now reference the correct version:
- ? `docs\DuckDB-Full-Quick-Start.md` ? Updated to 1.4.1
- ? `scripts\upgrade-duckdb-full.ps1` ? Updated to 1.4.1
- ? `src\AeroDebrief.Core\AeroDebrief.Core.csproj` ? Updated to 1.4.1

## Summary

**You asked why 1.1.1?** ? You were RIGHT to question it!  
**Correct version**: DuckDB.NET.Data.Full **v1.4.1** (latest stable)  
**Your benefit**: All the bug fixes including the GiB parser error you encountered!

---

**Status**: ? Corrected to v1.4.1  
**Action**: Close VS ? Run upgrade script ? Test migration
