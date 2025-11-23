# DuckDB Auto-Copy Added to UI Project

## Change Summary

Added automatic DuckDB native library download and copy to the **AeroDebrief.UI** project, matching the existing setup in CLI and Core projects.

## What Was Added

### File Modified: `src/AeroDebrief.UI/AeroDebrief.UI.csproj`

#### 1. Property Group Configuration
```xml
<!-- Ensure native dependencies (like duckdb.dll) are copied to output -->
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>

<!-- DuckDB native library configuration -->
<DuckDBVersion>v1.1.3</DuckDBVersion>
<DuckDBDownloadUrl>https://github.com/duckdb/duckdb/releases/download/$(DuckDBVersion)/libduckdb-windows-amd64.zip</DuckDBDownloadUrl>
```

#### 2. MSBuild Target
```xml
<Target Name="CopyDuckDBNativeLibrary" AfterTargets="Build">
  <!-- Checks for duckdb.dll in $(OutputPath) -->
  <!-- Downloads from GitHub if not present -->
  <!-- Extracts and copies to output -->
</Target>
```

## How It Works

### First Build
1. UI project builds successfully
2. MSBuild target `CopyDuckDBNativeLibrary` runs after build
3. Checks if `duckdb.dll` exists in output directory
4. If missing:
   - Downloads `libduckdb-windows-amd64.zip` (v1.1.3) from GitHub
   - Extracts to `%TEMP%\duckdb-native-v1.1.3\`
   - Copies `duckdb.dll` to output directory

### Subsequent Builds
- DLL already exists ? **skip download**
- Build completes immediately

## Verification

After the change, `duckdb.dll` (28.1 MB) is now present in:
- ? `src\AeroDebrief.UI\bin\Debug\net9.0-windows\duckdb.dll`
- ? `src\AeroDebrief.UI\bin\x64\Debug\net9.0-windows\duckdb.dll`

## Benefits

### For UI Development
- ? **No manual DLL copying** - Auto-downloads on first build
- ? **Works from Visual Studio** - F5 debugging works without setup
- ? **Consistent with other projects** - CLI, Core, and UI all use the same approach
- ? **Platform support** - Works for both Debug and x64 builds

### For CI/CD
- ? **Build server compatible** - No manual intervention needed
- ? **Deterministic** - Same DuckDB version (v1.1.3) every time
- ? **Fast** - DLL cached in temp directory, only downloaded once

## All Projects Now Configured

| Project | DuckDB Auto-Copy | Status |
|---------|------------------|--------|
| **AeroDebrief.Core** | ? | Configured |
| **AeroDebrief.CLI** | ? | Configured |
| **AeroDebrief.UI** | ? | **Just Added** |

## Testing

### Test UI Build
```powershell
# Clean and rebuild UI
dotnet clean src\AeroDebrief.UI
dotnet build src\AeroDebrief.UI --configuration Debug /p:Platform=x64

# Verify DLL exists
dir src\AeroDebrief.UI\bin\x64\Debug\net9.0-windows\duckdb.dll
```

### Run UI from Visual Studio
1. Set `AeroDebrief.UI` as startup project
2. Press F5 to run
3. No `DllNotFoundException` for DuckDB!

## Build Output

When building the UI project, you'll see:
```
Checking for DuckDB native library...
? DuckDB native library ready at: bin\x64\Debug\net9.0-windows\duckdb.dll
```

Or on first build:
```
DuckDB native library not found. Downloading...
? DuckDB native library ready at: bin\x64\Debug\net9.0-windows\duckdb.dll
```

## Technical Details

- **DuckDB Version**: v1.1.3
- **Download URL**: `https://github.com/duckdb/duckdb/releases/download/v1.1.3/libduckdb-windows-amd64.zip`
- **Cache Location**: `%TEMP%\duckdb-native-v1.1.3\`
- **DLL Size**: 28.1 MB (29,437,440 bytes)

## Consistency Across Projects

All three main projects now have identical DuckDB setup:

```xml
<!-- Same in Core, CLI, and UI -->
<DuckDBVersion>v1.1.3</DuckDBVersion>
<Target Name="CopyDuckDBNativeLibrary" AfterTargets="Build">
  <!-- Identical implementation -->
</Target>
```

This ensures:
- Same DuckDB version across all projects
- Same auto-download behavior
- Easier maintenance and troubleshooting

## Troubleshooting

### If DLL is missing after build:

**Manual copy:**
```powershell
.\scripts\copy-duckdb-native.ps1
```

This script will copy to all projects including UI.

### Platform-specific builds:

If using x64 platform (which UI does), ensure you build with:
```powershell
dotnet build src\AeroDebrief.UI --configuration Debug /p:Platform=x64
```

## Status

? **Complete** - DuckDB auto-copy now configured for all projects  
? **Tested** - Build successful, DLL verified in output  
? **Consistent** - All projects use same approach  
? **Ready** - UI can now be built and run without manual DLL setup  

---

**Updated**: 2025-01-20  
**DuckDB Version**: v1.1.3  
**Projects Configured**: Core, CLI, UI  
**Status**: ? Production ready
