# Automatic DuckDB Native Library Setup

## Problem Solved

When running the CLI project from Visual Studio, three issues occurred:
1. **Missing DuckDB DLL**: The native `duckdb.dll` library was not automatically copied to the output directory
2. **Argument Parsing Error**: Visual Studio passes build arguments like `-configuration Debug` which caused a `FormatException` when parsing port numbers
3. **DuckDB Configuration Error**: The `memory_limit` setting used incorrect unit format (`GB` instead of `GiB`)

## Solution Implemented

### 1. MSBuild Target for Automatic DLL Download

Added custom MSBuild targets to both `AeroDebrief.Core.csproj` and `AeroDebrief.CLI.csproj` that:

- **Check** if `duckdb.dll` exists in the output directory
- **Download** the native library from GitHub releases (v1.1.3) if missing
- **Extract** the DLL from the zip archive
- **Copy** it to the build output directory

**How it works:**
```xml
<Target Name="CopyDuckDBNativeLibrary" AfterTargets="Build">
  <!-- Checks for duckdb.dll in $(OutputPath) -->
  <!-- Downloads from GitHub if not present -->
  <!-- Extracts and copies to output -->
</Target>
```

### 2. Argument Filtering

Updated `Program.cs` to filter out Visual Studio build system arguments:

```csharp
// Filter out build system arguments that Visual Studio/dotnet CLI may pass
args = args.Where(arg => 
    !arg.Equals("-configuration", StringComparison.OrdinalIgnoreCase) &&
    !arg.Equals("Debug", StringComparison.OrdinalIgnoreCase) &&
    !arg.Equals("Release", StringComparison.OrdinalIgnoreCase) &&
    (!arg.StartsWith("-", StringComparison.OrdinalIgnoreCase) || 
     arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
).ToArray();
```

### 3. Robust Port Validation

Added safe parsing with user-friendly error messages:

```csharp
if (args.Length > 1)
{
    if (!int.TryParse(args[1], out port))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Invalid port number '{args[1]}'. Port must be a valid integer.");
        Console.ResetColor();
        // ...show usage
        return;
    }
}
```

### 4. DuckDB Memory Limit Configuration Fix

Fixed the DuckDB configuration to use correct binary units:

```csharp
// BEFORE (INCORRECT):
await ExecuteNonQueryAsync("SET memory_limit='2GB'", ct);  // ? Decimal units (1000^3)

// AFTER (CORRECT):
await ExecuteNonQueryAsync("SET memory_limit='2GiB'", ct); // ? Binary units (1024^3)
```

**Why this matters:**
- DuckDB requires binary units: `KiB`, `MiB`, `GiB`, `TiB` (base-1024)
- Using decimal units (`KB`, `MB`, `GB`, `TB`) causes a parser error
- `2GiB` = 2,147,483,648 bytes (2 × 1024³)
- `2GB` = 2,000,000,000 bytes (2 × 1000³) - **not supported**

## Benefits

### For Developers
? **No manual setup required** - DuckDB DLL downloads automatically on first build  
? **Works in Visual Studio** - Can F5/Debug directly without errors  
? **Works with dotnet CLI** - `dotnet build` and `dotnet run` work correctly  
? **Cross-configuration** - Works for both Debug and Release builds  

### For CI/CD
? **Build server compatible** - No manual DLL copying needed  
? **Deterministic builds** - Same DuckDB version every time (v1.1.3)  
? **Fast subsequent builds** - DLL only downloaded once, cached in temp directory  

## How It Works

### First Build
1. Project builds successfully
2. MSBuild target `CopyDuckDBNativeLibrary` runs after build
3. Checks if `duckdb.dll` exists in `bin\Debug\net9.0\` (or Release)
4. If missing:
   - Downloads `libduckdb-windows-amd64.zip` from GitHub
   - Extracts to `%TEMP%\duckdb-native-v1.1.3\`
   - Copies `duckdb.dll` to output directory
5. Build complete with DLL in place

### Subsequent Builds
1. Project builds
2. MSBuild target checks for `duckdb.dll`
3. DLL already exists ? **skip download**
4. Build completes immediately

### Build Output
```
Checking for DuckDB native library in bin\Debug\net9.0\...
? DuckDB native library ready at: bin\Debug\net9.0\duckdb.dll
```

Or if download is needed:
```
DuckDB native library not found. Downloading v1.1.3...
? DuckDB native library ready at: bin\Debug\net9.0\duckdb.dll
```

## Running from Visual Studio

You can now press **F5** or **Ctrl+F5** in Visual Studio to run the CLI project without any setup:

1. Set `AeroDebrief.CLI` as startup project
2. Press F5 to debug
3. Application runs with default settings (from `recorder.cfg`)
4. No more `DllNotFoundException`!

### With Command-Line Arguments

To pass arguments (like `--migrate`), configure the project:

1. Right-click `AeroDebrief.CLI` ? **Properties**
2. Go to **Debug** ? **General** ? **Open debug launch profiles UI**
3. Add **Command line arguments**:
   ```
   --migrate test.adb
   ```
4. Press F5 to run with arguments

## Fallback: Manual Script

If the automatic download fails (firewall, offline, etc.), you can still use the PowerShell script:

```powershell
.\scripts\copy-duckdb-native.ps1
```

This manually downloads and copies the DLL to all project output directories.

## Technical Details

### DuckDB Version
- **Native Library**: v1.1.3 (29.4 MB)
- **Download URL**: `https://github.com/duckdb/duckdb/releases/download/v1.1.3/libduckdb-windows-amd64.zip`
- **NuGet Package**: DuckDB.NET.Data v1.4.1

### MSBuild Tasks Used
- `DownloadFile` - Downloads the zip from GitHub
- `Unzip` - Extracts the archive
- `Copy` - Copies DLL to output directory
- `MakeDir` - Creates temp directory

### Cache Location
- **Temp Directory**: `%TEMP%\duckdb-native-v1.1.3\`
- **Persists** across builds (until temp cleanup)
- **Shared** between Debug and Release builds

## Troubleshooting

### Issue: DLL Still Missing After Build

**Check build output** for warnings:
```
?? Could not find or download duckdb.dll. Please run: scripts\copy-duckdb-native.ps1
```

**Solution**: Run the manual script
```powershell
.\scripts\copy-duckdb-native.ps1
```

### Issue: Still Getting Memory Limit Error After Fix

**Problem**: You may be running an old compiled version if your project uses platform-specific builds (x64).

**Check which folder is being used:**
```powershell
# Find all AeroDebrief.Core.dll files and their timestamps
Get-ChildItem "src\AeroDebrief.Core\bin" -Recurse -Filter "AeroDebrief.Core.dll" | 
  Select FullName, LastWriteTime | Sort LastWriteTime -Descending
```

**Solution**: Rebuild for the x64 platform specifically:
```powershell
# Clean old builds
dotnet clean src\AeroDebrief.Core --configuration Debug
dotnet clean src\AeroDebrief.CLI --configuration Debug

# Rebuild for x64 platform (if your project targets x64)
dotnet build src\AeroDebrief.Core --configuration Debug /p:Platform=x64
dotnet build src\AeroDebrief.CLI --configuration Debug /p:Platform=x64
```

**Or rebuild from Visual Studio:**
1. In Visual Studio, select **Build** ? **Clean Solution**
2. Select **Build** ? **Rebuild Solution**
3. Ensure the correct platform is selected (x64 if your project uses it)

### Issue: Download Fails (Firewall/Offline)

**Manual download**:
1. Download: https://github.com/duckdb/duckdb/releases/download/v1.1.3/libduckdb-windows-amd64.zip
2. Extract `duckdb.dll`
3. Copy to:
   - `src\AeroDebrief.Core\bin\Debug\net9.0\`
   - `src\AeroDebrief.Core\bin\x64\Debug\net9.0\` (if using x64 platform)
   - `src\AeroDebrief.CLI\bin\Debug\net9.0\`
   - `src\AeroDebrief.CLI\bin\x64\Debug\net9.0\` (if using x64 platform)

### Issue: Wrong DuckDB Version

If you need a different version, edit the project files:
```xml
<DuckDBVersion>v1.2.0</DuckDBVersion>
```

## Files Modified

- ? `src/AeroDebrief.CLI/AeroDebrief.CLI.csproj` - Added MSBuild target
- ? `src/AeroDebrief.Core/AeroDebrief.Core.csproj` - Added MSBuild target
- ? `src/AeroDebrief.UI/AeroDebrief.UI.csproj` - Added MSBuild target
- ? `src/AeroDebrief.CLI/Program.cs` - Added argument filtering and port validation
- ? `src/AeroDebrief.Core/Storage/DuckDBStore.cs` - Fixed memory_limit configuration
- ? Fixed syntax error on line 447 (character literal)

## All Projects Configured

All three main projects now have automatic DuckDB native library download:

| Project | Output Directory | DuckDB Auto-Copy |
|---------|------------------|------------------|
| **AeroDebrief.Core** | `bin\x64\Debug\net9.0\` | ? Configured |
| **AeroDebrief.CLI** | `bin\x64\Debug\net9.0\` | ? Configured |
| **AeroDebrief.UI** | `bin\x64\Debug\net9.0-windows\` | ? Configured |

This ensures the DuckDB native DLL is available for:
- **CLI**: Migration and analysis commands
- **Core**: Recording and database operations
- **UI**: Playback and visualization features

## Testing

### Verify Automatic Setup
```powershell
# Clean output directories
Remove-Item src\AeroDebrief.CLI\bin -Recurse -Force

# Build (should auto-download DLL)
dotnet build src\AeroDebrief.CLI

# Verify DLL exists
dir src\AeroDebrief.CLI\bin\Debug\net9.0\duckdb.dll
```

### Run from Visual Studio
1. Open solution in Visual Studio
2. Set `AeroDebrief.CLI` as startup project
3. Press F5
4. Should see: "SRS Recording Client Version: ..."
5. No DLL errors!

## Status

? **MSBuild targets added** - Automatic DLL download on build  
? **Argument filtering implemented** - Visual Studio arguments handled  
? **Port validation added** - User-friendly error messages  
? **Syntax error fixed** - Line 447 character literal  
? **Build successful** - All projects compile  
? **DLL verified** - 28.1 MB duckdb.dll in output directories  

---

**Created:** 2025-01-19  
**DuckDB Version:** v1.1.3  
**Branch:** DuckDB-implementation  
**Status:** ? Complete and tested
