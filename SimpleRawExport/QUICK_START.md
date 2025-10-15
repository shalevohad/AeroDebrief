# Quick Start Guide - SimpleRawExport

## Prerequisites
? .NET 9 Runtime installed
? Built the project (Debug or Release)

## Location
The executable is located at:
- **Debug**: `SimpleRawExport\bin\Debug\net9.0\SimpleRawExport.exe`
- **Release**: `SimpleRawExport\bin\Release\net9.0\SimpleRawExport.exe`

## Basic Usage

### 1. Navigate to the output directory
```bash
cd SimpleRawExport\bin\Debug\net9.0
```

### 2. Run the tool
```bash
# Basic usage (output will be recording.mp3)
SimpleRawExport.exe recording.srs

# Specify custom output file
SimpleRawExport.exe recording.srs output.mp3

# With full paths
SimpleRawExport.exe C:\Recordings\mission.srs C:\Export\mission.mp3
```

## Expected Output

### Success
```
Simple SRS Raw Export - Decode and Export to MP3
=================================================
Input file: recording.srs
Output file: recording.mp3

Reading packets from file...
Read 1500 packets    
Found 1500 packets
Decoding audio...
Decoded 1500/1500 packets    
Decoded 144000 audio samples
Normalizing audio (peak: 1.23)...
Exporting to MP3...
Written recording.mp3 (0.28 MB raw, 0.15 MB MP3)

Export completed successfully!
```

### No Packets
```
Reading packets from file...
Read 0 packets    
No packets found in file.
```

### Error
```
Error: Input file not found: recording.srs
```

## Troubleshooting

### "Unable to load DLL 'opus'"
**Problem**: Native Opus library is missing

**Solution**: 
1. Rebuild the project: `dotnet build SimpleRawExport\SimpleRawExport.csproj`
2. Verify `opus.dll` exists in the output directory
3. If missing, check that SharedAudio project is referenced

### "Exception occurred while creating decoder"
**Problem**: Invalid decoder parameters or corrupted native library

**Solution**:
1. Verify `opus.dll` is not corrupted (size should be ~454 KB)
2. Check that you're using the correct .NET runtime
3. Try rebuilding with `--force` flag

### "Decoding failed - InvalidPacket"
**Problem**: Corrupted audio data in the recording

**Solution**:
- This is expected for corrupted packets
- The tool will skip bad packets and continue
- Final output may have gaps if many packets are corrupted

### "No packets found in file"
**Problem**: File is empty or wrong format

**Solution**:
- Verify the file is a valid .srs recording
- Check file size (should be > 0 bytes)
- Try with a different recording file

## File Requirements

### Input File (.srs)
- Created by SRS Recording Client
- Contains Opus-encoded audio packets
- Binary format with metadata

### Output File (.mp3)
- Standard MP3 format
- 48 kHz sample rate
- Mono audio
- VBR encoding (~192 kbps)

## Performance Notes

- **Reading**: ~100 packets/second (depends on disk speed)
- **Decoding**: ~1000 packets/second (depends on CPU)
- **Encoding**: Depends on audio length and CPU

Typical 5-minute recording:
- ~7500 packets
- Processing time: 10-20 seconds
- Output size: ~5-8 MB

## Advanced Usage

### Batch Processing (PowerShell)
```powershell
# Convert all .srs files in a directory
Get-ChildItem "C:\Recordings\*.srs" | ForEach-Object {
    .\SimpleRawExport.exe $_.FullName "$($_.DirectoryName)\$($_.BaseName).mp3"
}
```

### Command Line Integration
```bash
# From anywhere (add to PATH)
SimpleRawExport C:\Recordings\mission.srs

# With dotnet run (from project directory)
dotnet run --project SimpleRawExport -- recording.srs output.mp3
```

## Quality Settings

Currently uses LAME Standard preset:
- VBR encoding
- Target bitrate: ~192 kbps
- Quality: High

To modify quality, edit `Program.cs`:
```csharp
// Change from:
new LameMP3FileWriter(outputFile, waveFormat, LAMEPreset.STANDARD);

// To one of:
LAMEPreset.ABR_128    // 128 kbps average
LAMEPreset.ABR_192    // 192 kbps average
LAMEPreset.ABR_256    // 256 kbps average
LAMEPreset.VBR_90     // Variable, highest quality
```

## Common Use Cases

### 1. Quick Export
```bash
SimpleRawExport recording.srs
```
? Creates `recording.mp3` in same directory

### 2. Organized Export
```bash
SimpleRawExport C:\Recordings\raw\mission.srs C:\Recordings\mp3\mission.mp3
```
? Keeps raw and processed files separate

### 3. Date-stamped Export
```powershell
$date = Get-Date -Format "yyyy-MM-dd_HH-mm"
.\SimpleRawExport.exe recording.srs "export_$date.mp3"
```
? Creates `export_2024-01-15_14-30.mp3`

## Getting Help

### Display Usage
```bash
SimpleRawExport
```
(Run without arguments to see usage information)

### Check Version
Built-in version info is shown in the header:
```
Simple SRS Raw Export - Decode and Export to MP3
```

### Report Issues
If you encounter problems:
1. Check the troubleshooting section above
2. Verify all native libraries are present
3. Try with a different recording file
4. Check the FIX_SUMMARY.md for known issues
