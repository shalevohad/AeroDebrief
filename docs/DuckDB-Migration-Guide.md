# DuckDB Migration Guide

## Complete Guide to Migrating from ADB to CVR Format

**Target Audience**: End Users, Developers, System Administrators  
**Time Required**: 5-30 minutes (depending on collection size)  
**Status**: Production Ready

---

## Table of Contents

1. [Overview](#overview)
2. [Why Migrate?](#why-migrate)
3. [Migration Methods](#migration-methods)
4. [Step-by-Step Instructions](#step-by-step-instructions)
5. [Troubleshooting](#troubleshooting)
6. [FAQ](#faq)

---

## Overview

### What is Changing?

| Old System | New System |
|------------|------------|
| `.adb` files (binary format) | `.cvr` files (compressed database) |
| `.pkidx` index files | No separate index needed |
| Sequential access only | Fast queries and filtering |
| Manual index rebuilding | Automatic optimization |
| ~250 MB per hour | ~120 MB per hour (-52%) |

### Migration Timeline

**Phase 1** (Current): Automatic conversion on file open  
**Phase 2** (v1.1): CVR becomes default recording format  
**Phase 3** (v2.0): ADB support removed (converter remains)

---

## Why Migrate?

### Benefits

? **Smaller Files**: 40-60% size reduction (CVR vs ADB)  
? **Faster Loading**: Instant metadata (no index building)  
? **Better Filtering**: 10-100x faster frequency/player filtering  
? **No Index Files**: Single .cvr file contains everything  
? **Concurrent Access**: Read while recording (live playback)  
? **Query Support**: SQL-like filtering and search  
? **Future-Proof**: Modern database format  

### Comparison

| Feature | ADB | CVR |
|---------|-----|-----|
| File Size (1hr) | 250 MB | 120 MB |
| Load Time | 5-10s + index | <1s |
| Metadata Access | Scan all packets | <1ms |
| Frequency Filter | Full scan | Indexed (instant) |
| Player Filter | Full scan | Indexed (instant) |
| Concurrent Read | ? No | ? Yes |
| Live Playback | ? No | ? Yes |

---

## Migration Methods

### Method 1: Automatic (Recommended for Most Users)

**How it works:**
- Open any `.adb` file in AeroDebrief
- Automatically converts to `.duckdb` (cached)
- Conversion happens once (reused on future opens)

**Pros:**
- ? No manual steps
- ? Conversion cached for reuse
- ? Immediate use after conversion

**Cons:**
- ?? First open takes time
- ?? Doesn't create .cvr (compressed) file

**When to use:**
- Individual files
- One-time access
- Quick review

### Method 2: CLI Batch Conversion

**How it works:**
- Use CLI tool to convert multiple files
- Creates both .duckdb and .cvr (optional)
- Progress reporting and statistics

**Pros:**
- ? Batch processing
- ? Progress monitoring
- ? Can create compressed .cvr files
- ? Compression statistics

**Cons:**
- ?? Command-line required
- ?? Manual process

**When to use:**
- Large collections (50+ files)
- Archival preparation
- IT/admin tasks

### Method 3: Manual Conversion + Compression

**How it works:**
- Convert ADB ? DuckDB (CLI or UI)
- Compress DuckDB ? CVR (CLI)
- Delete intermediates

**Pros:**
- ? Maximum compression
- ? Single file output
- ? Best for distribution

**Cons:**
- ?? Most steps
- ?? Requires disk space

**When to use:**
- Sharing recordings
- Long-term archival
- Bandwidth-limited transfer

---

## Step-by-Step Instructions

### For End Users: Automatic Migration

1. **Open AeroDebrief**
   ```
   Launch AeroDebrief.UI.exe
   ```

2. **Open an ADB File**
   ```
   File ? Open Recording ? Select .adb file
   ```

3. **Wait for Conversion**
   ```
   Status bar shows: "Converting ADB to DuckDB... 45%"
   Typical time: 30s-2min depending on file size
   ```

4. **Recording Opens Automatically**
   ```
   Status bar shows: "? Loaded: 12,543 packets, 8 frequencies, 14 players"
   ```

5. **Cached for Future Use**
   ```
   Next time you open same .adb:
   - Detects cached .duckdb file
   - Opens instantly (no re-conversion)
   ```

### For Power Users: CLI Batch Conversion

1. **Open Command Prompt/PowerShell**
   ```powershell
   cd C:\Program Files\AeroDebrief
   ```

2. **Single File Conversion**
   ```powershell
   .\AeroDebrief.CLI.exe --migrate "C:\Recordings\recording.adb"
   ```

   **Output:**
   ```
   ?? ADB ? DuckDB Migration Tool
   ============================================================
   
   Migrating packets:    [100%] 12,543 packets
   Finalizing database... ?
   
   ============================================================
   ? Conversion complete!
      Packets: 12,543
      Duration: 2.3s
      Speed: 5,453 packets/sec
      Source: 45.2 MB
      Output: 32.1 MB
      Compression: 29.0% smaller
   
   ?? You can now delete the ADB file: recording.adb
   ```

3. **Batch Directory Conversion**
   ```powershell
   .\AeroDebrief.CLI.exe --migrate "C:\Recordings\"
   ```

   **Output:**
   ```
   Found 47 ADB file(s) to convert
   
   [12/47] recording_20250115.adb        100% - Complete
   [13/47] recording_20250116.adb         67% - Converting packets
   
   ============================================================
   ? Batch conversion complete:
      Successful: 47/47
      Total packets: 589,241
      Total source: 2.1 GB
      Total output: 1.5 GB
      Avg compression: 28.5%
   ```

4. **Optional: Compress to CVR**
   ```powershell
   # For each .duckdb file, compress to .cvr
   foreach ($file in Get-ChildItem "C:\Recordings\" -Filter "*.duckdb") {
       .\AeroDebrief.CLI.exe --compress $file.FullName
   }
   ```

### For Developers: Programmatic Migration

```csharp
using AeroDebrief.Core.Storage;

// Single file
var converter = new AdbToDuckDBConverter();
var result = await converter.ConvertAsync(
    "recording.adb", 
    "recording.duckdb", 
    progress: new Progress<ConversionProgress>(p => 
        Console.WriteLine($"{p.Stage}: {p.Percent}%")
    )
);

if (result.Success)
{
    Console.WriteLine($"Converted {result.TotalPackets:N0} packets");
    
    // Optional: Compress to CVR
    await CvrFormat.CompressToCvrAsync("recording.duckdb", "recording.cvr");
}

// Batch conversion
var adbFiles = Directory.GetFiles(@"C:\Recordings", "*.adb");
var results = await converter.ConvertBatchAsync(adbFiles, progress);
```

---

## Post-Migration Tasks

### Verify Conversion

```csharp
// Check packet count matches
var adbPackets = AdbReader.CountPackets("recording.adb");
var duckDbPackets = duckDbStore.TotalPackets;

if (adbPackets == duckDbPackets)
    Console.WriteLine("? Conversion verified");
```

### Clean Up Old Files

```powershell
# After verifying conversion success:

# Delete ADB files
Remove-Item "C:\Recordings\*.adb"

# Delete PKD index files (no longer needed)
Remove-Item "C:\Recordings\*.pkidx"

# Keep .duckdb for quick access, or compress to .cvr and delete
```

### Organize Files

```
Before Migration:
C:\Recordings\
??? recording1.adb (250 MB)
??? recording1.pkidx (2 MB)
??? recording2.adb (180 MB)
??? recording2.pkidx (1.5 MB)

After Migration (Option A - Uncompressed):
C:\Recordings\
??? recording1.duckdb (200 MB)
??? recording2.duckdb (144 MB)

After Migration (Option B - Compressed):
C:\Recordings\
??? recording1.cvr (120 MB)
??? recording2.cvr (86 MB)
```

---

## Troubleshooting

### "Conversion failed: File may be corrupted"

**Solution:**
1. Verify ADB file integrity:
   ```powershell
   .\AeroDebrief.CLI.exe --analyze recording.adb
   ```

2. Check for disk space:
   - Need 1.5x source file size for temp files
   - Example: 250 MB ADB needs ~375 MB free space

3. Try manual conversion with verbose logging:
   ```powershell
   .\AeroDebrief.CLI.exe --migrate recording.adb --verbose
   ```

### "Out of disk space"

**Solution:**
1. Free up space:
   - Delete temp files: `C:\Users\<user>\AppData\Local\Temp\AeroDebrief_*`
   - Clear old recordings

2. Convert to external drive:
   ```powershell
   .\AeroDebrief.CLI.exe --migrate recording.adb "D:\Output\recording.duckdb"
   ```

3. Convert in batches (instead of entire directory)

### "Conversion very slow"

**Typical speeds:**
- Fast: 50,000+ packets/sec (SSD)
- Normal: 10,000-20,000 packets/sec (HDD)
- Slow: <5,000 packets/sec (network drive, slow disk)

**Solutions:**
1. Copy ADB files to local SSD
2. Close other applications
3. Check antivirus isn't scanning files

### "Converted file larger than source"

**Explanation:**
- Uncompressed .duckdb might be slightly larger than ADB
- Database includes indexes and metadata
- Compress to .cvr for 40-60% reduction

**Solution:**
```powershell
.\AeroDebrief.CLI.exe --compress recording.duckdb
# Creates recording.cvr (much smaller)
```

---

## FAQ

### **Q: Do I have to migrate immediately?**
**A:** No. ADB files still work. Migration happens automatically when you open them.

### **Q: Will my old recordings still work?**
**A:** Yes. AeroDebrief automatically converts ADB files when you open them.

### **Q: Can I delete the ADB files after migration?**
**A:** Yes, once you've verified the .duckdb or .cvr files work correctly.

### **Q: What about my PKIDX index files?**
**A:** No longer needed. Delete them after migration.

### **Q: Should I use .duckdb or .cvr?**
**A:** 
- **Use .duckdb** for: Active recordings, frequent access, development
- **Use .cvr** for: Archival, sharing, distribution, saving space

### **Q: Can I convert back to ADB?**
**A:** No, but you don't need to. CVR format is better in every way.

### **Q: How long does migration take?**
**A:** Typically 30 seconds to 2 minutes per file, depending on size.

### **Q: Does migration affect quality?**
**A:** No. Audio quality is identical (lossless conversion).

### **Q: Can I continue using old AeroDebrief versions?**
**A:** Old versions can't read CVR files. Update to latest version.

### **Q: What if I need to share recordings with someone using old version?**
**A:** Keep ADB files for now, or ask them to update.

---

## Migration Checklist

### Pre-Migration
- [ ] Update AeroDebrief to latest version
- [ ] Verify disk space (1.5x collection size)
- [ ] Backup important recordings
- [ ] Test conversion with single file

### Migration
- [ ] Choose migration method (automatic/CLI)
- [ ] Convert files
- [ ] Verify packet counts match
- [ ] Test opening converted files

### Post-Migration
- [ ] Delete ADB files (after verification)
- [ ] Delete PKIDX files
- [ ] Compress to CVR for archival (optional)
- [ ] Update backup procedures

### Cleanup
- [ ] Delete temp files
- [ ] Organize recordings folder
- [ ] Update documentation/procedures
- [ ] Inform team about new format

---

## Support

### Get Help
- **Documentation**: `docs/` folder
- **Issues**: GitHub Issues
- **Logs**: `%APPDATA%\AeroDebrief\Logs\`

### Report Problems
Include:
1. AeroDebrief version
2. Source file size
3. Error message
4. Log file (if available)

---

## Summary

? **Automatic**: Open .adb files normally (auto-converts)  
? **Manual**: Use CLI for batch conversion  
? **Compress**: Create .cvr files for archival  
? **Safe**: Original files untouched until you delete them  
? **Fast**: 30s-2min per file typical  
? **Better**: Smaller, faster, more features  

**Ready to migrate? Just open your recordings!** ??
