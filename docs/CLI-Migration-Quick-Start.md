# ?? CLI Migration Quick Start

## First Time Setup (5 seconds)

```powershell
# Run this ONCE per development machine
.\scripts\copy-duckdb-native.ps1
```

Expected output:
```
? DuckDB native library installed successfully!
```

---

## Migrate Files

### Using the Wrapper Script (Recommended)

```powershell
# Single file
.\scripts\run-cli.ps1 --migrate recording.adb

# Custom output path
.\scripts\run-cli.ps1 --migrate recording.adb my-converted.duckdb

# Batch convert directory
.\scripts\run-cli.ps1 --migrate "C:\Recordings\"
```

### Using dotnet run (Alternative)

```powershell
# Single file
dotnet run --project src\AeroDebrief.CLI --configuration Debug -- --migrate recording.adb

# Custom output path
dotnet run --project src\AeroDebrief.CLI --configuration Debug -- --migrate recording.adb output.duckdb
```

### Using Compiled Exe (Direct)

```powershell
cd src\AeroDebrief.CLI\bin\Debug\net9.0
.\AeroDebrief.CLI.exe --migrate "C:\Users\Ohad\source\repos\AeroDebrief\Records\recorded_audio_srv_88.99.165.102_5072_t20251115T195042Z.adb"
```

---

## Troubleshooting

### ? "Unable to load DLL 'duckdb'"
**Solution:** Run the setup script
```powershell
.\scripts\copy-duckdb-native.ps1
```

### ? "DuckDB native library not found"
**Solution:** Build and run setup
```powershell
dotnet build src\AeroDebrief.CLI --configuration Debug
.\scripts\copy-duckdb-native.ps1
```

### ? Script execution policy error
**Solution:** Bypass temporarily
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\copy-duckdb-native.ps1
```

---

## What You Get

```
?? ADB ? DuckDB Migration Tool
============================================================
? Conversion successful!
   Output: recording.duckdb
   Packets: 1,234,567
   Duration: 45.2s
   Speed: 27,312 packets/sec
   Source: 1,234.5 MB  
   Output: 456.7 MB
   Compression: 63.0% smaller
```

---

## Full Documentation

?? See `docs/CLI-DuckDB-Native-Library-Setup.md` for complete guide

---

**Status:** ? Ready to use  
**Setup Time:** ~5 seconds  
**Recommended:** Use `.\scripts\run-cli.ps1` wrapper
