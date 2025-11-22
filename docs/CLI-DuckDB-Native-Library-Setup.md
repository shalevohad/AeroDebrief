# DuckDB Native Library Setup for CLI

## Problem

When running `dotnet run --project src\AeroDebrief.CLI -- --migrate file.adb`, you may encounter:

```
System.DllNotFoundException: Unable to load DLL 'duckdb' or one of its dependencies
```

## Root Cause

The **DuckDB.NET NuGet package** (v1.4.1) does not include the native `duckdb.dll` library in its package. This is a known issue with certain versions of the DuckDB.NET bindings.

## Solution

### Automated Setup (Recommended)

Run the provided PowerShell script to download and install the native DuckDB library:

```powershell
.\scripts\copy-duckdb-native.ps1
```

**What the script does:**
1. Downloads the DuckDB native library (libduckdb v1.1.3) from GitHub releases
2. Extracts `duckdb.dll` from the archive
3. Copies it to the CLI output directories:
   - `src\AeroDebrief.CLI\bin\Debug\net9.0\`
   - `src\AeroDebrief.CLI\bin\Release\net9.0\`

### Manual Setup

If you prefer to do it manually:

1. **Download DuckDB native library:**
   - Go to https://github.com/duckdb/duckdb/releases
   - Download `libduckdb-windows-amd64.zip` (version 1.1.3 or compatible)

2. **Extract and copy:**
   ```powershell
   # Extract the zip file
   Expand-Archive libduckdb-windows-amd64.zip -DestinationPath duckdb-temp
   
   # Copy duckdb.dll to CLI output directory
   Copy-Item duckdb-temp\duckdb.dll src\AeroDebrief.CLI\bin\Release\net9.0\
   ```

3. **Build the project** (if you haven't already):
   ```powershell
   dotnet build src\AeroDebrief.CLI --configuration Release
   ```

## Verification

After setup, verify the DLL exists:

```powershell
dir src\AeroDebrief.CLI\bin\Release\net9.0\duckdb.dll
```

Expected output:
```
Mode   LastWriteTime     Length   Name
----   -------------     ------   ----
-a---- XX/XX/XXXX       ~29MB    duckdb.dll
```

## Running the CLI

Once the native DLL is in place, you can use all CLI commands:

### Migrate ADB to DuckDB

```powershell
# Single file
dotnet run --project src\AeroDebrief.CLI -- --migrate recording.adb

# Custom output path
dotnet run --project src\AeroDebrief.CLI -- --migrate recording.adb output.duckdb

# Batch convert directory
dotnet run --project src\AeroDebrief.CLI -- --migrate "C:\Recordings\"
```

### Example Output

```
?? ADB ? DuckDB Migration Tool
============================================================
??  WARNING: This is a ONE-WAY migration!
??  ADB files will remain, but won't be used after migration.

Opening source file           [  5%] 0 packets
Creating DuckDB database      [ 10%] 0 packets
Converting packets            [ 80%] 1,234,567 packets
Finalizing database           [ 90%] 1,234,567 packets

============================================================
? Conversion successful!
   Output: recording.duckdb
   Packets: 1,234,567
   Duration: 45.2s
   Speed: 27,312 packets/sec
   Source: 1,234.5 MB
   Output: 456.7 MB
   Compression: 63.0% smaller

??? You can now delete the ADB file: recording.adb
```

## Troubleshooting

### Issue: Script execution policy error

**Error:**
```
cannot be loaded because running scripts is disabled on this system
```

**Solution:**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\copy-duckdb-native.ps1
```

### Issue: DLL still not found after running script

**Possible causes:**
1. You're running from Debug but the DLL was copied to Release
2. You need to rebuild after running the script

**Solutions:**
```powershell
# Build both configurations
dotnet build src\AeroDebrief.CLI --configuration Debug
dotnet build src\AeroDebrief.CLI --configuration Release

# Run the script again
.\scripts\copy-duckdb-native.ps1
```

### Issue: Version mismatch warnings

If you see warnings about DuckDB version mismatches, ensure you're using a compatible version. The script downloads v1.1.3 which is compatible with DuckDB.NET v1.4.1.

## Project Configuration

The CLI project file (`AeroDebrief.CLI.csproj`) includes:

```xml
<PropertyGroup>
  <!-- Ensure native dependencies are copied to output -->
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

This ensures that .NET wrapper DLLs are copied, but the native library still needs manual setup.

## Alternative: Using the UI Project

If you have the UI project built and working, you can copy the `duckdb.dll` from there:

```powershell
# Find duckdb.dll in UI output
Get-ChildItem src\AeroDebrief.UI\bin -Recurse -Filter "duckdb.dll"

# Copy to CLI
Copy-Item <path-to-ui-duckdb.dll> src\AeroDebrief.CLI\bin\Release\net9.0\
```

## Future Improvements

Consider these improvements for a permanent solution:

1. **MSBuild Target** - Add a custom MSBuild target to automatically download and copy the DLL during build
2. **NuGet Package** - Create a custom NuGet package that wraps the native DLL
3. **Build Script Integration** - Integrate the PowerShell script into the build process

## Related Files

- `scripts/copy-duckdb-native.ps1` - Automated setup script
- `src/AeroDebrief.CLI/AeroDebrief.CLI.csproj` - Project configuration
- `src/AeroDebrief.Core/Storage/AdbToDuckDBConverter.cs` - Migration implementation
- `src/AeroDebrief.Core/Storage/DuckDBStore.cs` - DuckDB wrapper

## Status

? **Setup complete** - Native DLL is now in place  
? **CLI ready** - You can run `--migrate` commands  
?? **Documentation** - This file serves as reference

---

**Created:** 2025-01-19  
**DuckDB Version:** v1.1.3  
**DuckDB.NET Version:** v1.4.1  
**Branch:** DuckDB-implementation
