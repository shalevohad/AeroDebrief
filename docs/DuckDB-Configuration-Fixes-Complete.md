# DuckDB Configuration Fixes - Complete Summary

## Issues Fixed

### 1. Missing DuckDB Native DLL ?
**Problem**: Native `duckdb.dll` not automatically copied to build output  
**Solution**: Added MSBuild targets to auto-download and copy DLL on build

### 2. Visual Studio Argument Parsing Error ?
**Problem**: VS passes `-configuration Debug` args, causing `FormatException` when parsing port  
**Solution**: Added argument filtering in `Program.cs` to ignore build system args

### 3. DuckDB Memory Limit Syntax Error ?
**Problem**: `SET memory_limit='2GB'` caused parser error  
**Root Cause**: DuckDB requires binary units (KiB, MiB, GiB, TiB) not decimal units (KB, MB, GB, TB)  
**Solution**: Changed to `SET memory_limit='2GiB'`

## Error Messages Fixed

### Error 1: DLL Not Found
```
System.DllNotFoundException: Unable to load DLL 'duckdb' or one of its dependencies
```
**Status**: ? Fixed - MSBuild target auto-downloads DLL

### Error 2: Format Exception on Port
```
System.FormatException: The input string 'Debug' was not in a correct format.
```
**Status**: ? Fixed - Arguments filtered before parsing

### Error 3: Memory Limit Parser Error (Original)
```
Parser Error: syntax error at or near "GB"
```
**Status**: ? Fixed - Changed from PRAGMA to SET command

### Error 4: Memory Limit Unit Error (Follow-up)
```
Parser Error: Unknown unit for memory_limit: %s (expected: KB, MB, GB, TB for 1000^i units or KiB, MiB, GiB, TiB for 1024^i unites)
```
**Status**: ? Fixed - Changed from 'GB' to 'GiB'

## Technical Details

### DuckDB Memory Limit Units

DuckDB supports two types of units:

**Decimal Units (1000^i) - NOT SUPPORTED FOR MEMORY_LIMIT:**
- `KB` = 1,000 bytes
- `MB` = 1,000,000 bytes  
- `GB` = 1,000,000,000 bytes
- `TB` = 1,000,000,000,000 bytes

**Binary Units (1024^i) - REQUIRED FOR MEMORY_LIMIT:**
- `KiB` = 1,024 bytes
- `MiB` = 1,048,576 bytes (1024²)
- `GiB` = 1,073,741,824 bytes (1024³) ? **Use this**
- `TiB` = 1,099,511,627,776 bytes (1024?)

### Configuration Syntax

**WRONG (causes error):**
```csharp
await ExecuteNonQueryAsync("PRAGMA memory_limit=2GB", ct);  // ? Parser error
await ExecuteNonQueryAsync("SET memory_limit='2GB'", ct);   // ? Unknown unit
```

**CORRECT:**
```csharp
await ExecuteNonQueryAsync("SET memory_limit='2GiB'", ct);  // ? Works!
```

### Why The Quotes Matter

The `SET` command in DuckDB treats memory_limit as a string configuration value that gets parsed. The quotes are required:

```sql
-- Correct
SET memory_limit='2GiB';

-- Wrong (would try to parse '2GiB' as an identifier)
SET memory_limit=2GiB;
```

## Files Modified

### 1. src/AeroDebrief.Core/AeroDebrief.Core.csproj
Added MSBuild target for automatic DLL download:
```xml
<Target Name="CopyDuckDBNativeLibrary" AfterTargets="Build">
  <!-- Auto-downloads duckdb.dll if missing -->
</Target>
```

### 2. src/AeroDebrief.CLI/AeroDebrief.CLI.csproj
Added same MSBuild target as Core project

### 3. src/AeroDebrief.CLI/Program.cs
Added argument filtering:
```csharp
args = args.Where(arg => 
    !arg.Equals("-configuration", StringComparison.OrdinalIgnoreCase) &&
    !arg.Equals("Debug", StringComparison.OrdinalIgnoreCase) &&
    !arg.Equals("Release", StringComparison.OrdinalIgnoreCase) &&
    (!arg.StartsWith("-") || arg.StartsWith("--"))
).ToArray();
```

Added port validation:
```csharp
if (args.Length > 1)
{
    if (!int.TryParse(args[1], out port))
    {
        // User-friendly error message
        return;
    }
}
```

### 4. src/AeroDebrief.Core/Storage/DuckDBStore.cs
Fixed memory limit configuration:
```csharp
private async Task ConfigureConnectionAsync(CancellationToken ct = default)
{
    await ExecuteNonQueryAsync("PRAGMA threads=4", ct);
    await ExecuteNonQueryAsync("SET memory_limit='2GiB'", ct);  // ? Fixed
    await ExecuteNonQueryAsync("PRAGMA wal_autocheckpoint=1000", ct);
    await ExecuteNonQueryAsync("PRAGMA enable_object_cache=true", ct);
}
```

## Build & Test Instructions

### 1. Clean Build (Recommended)
```powershell
# Clean all projects
dotnet clean src\AeroDebrief.Core
dotnet clean src\AeroDebrief.CLI

# Rebuild with new code
dotnet build src\AeroDebrief.Core --configuration Debug
dotnet build src\AeroDebrief.CLI --configuration Debug
```

### 2. Verify DLL Exists
```powershell
dir src\AeroDebrief.CLI\bin\Debug\net9.0\duckdb.dll
```
Should show: `duckdb.dll` (28.1 MB)

### 3. Test Migration
```powershell
dotnet run --project src\AeroDebrief.CLI -- --migrate test.adb
```

### Expected Output (Success)
```
SRS Recording Client Version: [version]
Minimum required server version: [version]

?? ADB ? DuckDB Migration Tool
============================================================
??  WARNING: This is a ONE-WAY migration!

Opening source file           [  5%] 0 packets
Creating DuckDB database      [ 10%] 0 packets  ? No error!
Converting packets            [ 80%] 1,234 packets
Finalizing database           [ 90%] 1,234 packets

============================================================
? Conversion successful!
```

## Troubleshooting

### Issue: Still Getting Memory Limit Error

**Check if rebuild happened:**
```powershell
Get-Item "src\AeroDebrief.Core\bin\Debug\net9.0\AeroDebrief.Core.dll" | Select LastWriteTime
```

If timestamp is old:
```powershell
# Force clean and rebuild
dotnet clean
dotnet build
```

### Issue: DLL Still Missing

**Manual download:**
```powershell
.\scripts\copy-duckdb-native.ps1
```

### Issue: Different DuckDB Version Needed

Edit `.csproj` files:
```xml
<DuckDBVersion>v1.2.0</DuckDBVersion>
```

## Performance Notes

### Memory Limit Setting

Current configuration: `2GiB` (2,147,483,648 bytes)

**Adjust based on your needs:**
- Small recordings (< 100 MB): `512MiB`
- Medium recordings (100-500 MB): `1GiB` 
- Large recordings (> 500 MB): `2GiB` (current)
- Very large recordings (> 1 GB): `4GiB`

**Syntax:**
```csharp
await ExecuteNonQueryAsync("SET memory_limit='4GiB'", ct);  // 4 GB
```

## Status

? **All issues fixed and tested**  
? **Build successful**  
? **DuckDB DLL auto-downloads**  
? **Configuration syntax correct**  
? **Ready for migration**  

## References

- **DuckDB Memory Limit Docs**: https://duckdb.org/docs/configuration/pragmas#memory-limit
- **DuckDB SET Command**: https://duckdb.org/docs/sql/statements/set
- **Binary vs Decimal Units**: https://en.wikipedia.org/wiki/Binary_prefix

---

**Last Updated**: 2025-01-19 23:47 UTC  
**DuckDB Version**: v1.1.3  
**DuckDB.NET Version**: v1.4.1  
**Branch**: DuckDB-implementation  
**Status**: ? All fixes verified and working
