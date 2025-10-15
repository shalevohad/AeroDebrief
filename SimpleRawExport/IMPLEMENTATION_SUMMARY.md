# SimpleRawExport Implementation Summary

## Changes Made

### 1. Program.cs - Complete Rewrite
- **Removed**: Dependencies on Core project (AudioProcessingEngine, AudioPacketMetadata)
- **Added**: Direct Opus decoding using Common project's OpusDecoder
- **Simplified**: Custom packet reading logic instead of using Core's metadata classes

### 2. SimpleRawExport.csproj - Updated Dependencies
- **Removed**: `<ProjectReference Include="..\Core\Core.csproj" />`
- **Added**: 
  - `<ProjectReference Include="..\External\SRS\Common\Common.csproj" />` - For OpusDecoder API
  - `<ProjectReference Include="..\External\SRS\SharedAudio\SharedAudio.csproj" />` - For native libraries (opus.dll, libmp3lame.dll)
- **Kept**: NAudio and NAudio.Lame packages for MP3 export

## Implementation Details

### Packet Reading
- Reads raw binary format directly from .srs files
- Supports both legacy and enhanced file formats (auto-detection)
- Skips corrupted packets gracefully
- Simple `PacketData` struct holds only essential data (Timestamp, Frequency, AudioPayload)

### Audio Decoding
- Uses `OpusDecoder` from `Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core`
- Decodes at 48kHz mono (SRS standard)
- 40ms frames (1920 samples per frame)
- Direct float output for high-quality processing

### Audio Mixing
- Builds complete timeline based on packet timestamps
- Mixes overlapping audio packets additively
- Automatic normalization with 5% headroom to prevent clipping

### MP3 Export
- Converts float samples to PCM16
- Uses NAudio's LameMP3FileWriter
- LAME Standard preset for good quality/size balance

## Benefits

? **Lightweight**: No dependency on heavy Core project
? **Simple**: Plain C# code, easy to understand and maintain  
? **Standalone**: Only needs Common project for Opus decoder API and SharedAudio for native libraries
? **Efficient**: Direct binary reading, minimal memory allocations
? **Robust**: Handles corrupted data and missing packets

## Native Libraries

The tool requires native libraries that are automatically copied from SharedAudio:
- **opus.dll** / **opus.so**: Native Opus codec library
- **libmp3lame.dll** / **libmp3lame.so**: Native LAME MP3 encoder

These are automatically included in the output directory when building the project.

## Usage

```bash
# Build
dotnet build SimpleRawExport

# Run
dotnet run --project SimpleRawExport -- input.srs output.mp3

# Or after build
SimpleRawExport.exe input.srs output.mp3
```

## Code Structure

```
SimpleRawExport/
??? Program.cs           # Main implementation
??? SimpleRawExport.csproj  # Project file
??? README.md           # User documentation
```

### Key Functions

1. `Main()` - Entry point, command-line parsing
2. `ProcessFile()` - Orchestrates read ? decode ? export pipeline
3. `ReadPackets()` - Reads and parses binary packet data
4. `TryReadPacket()` - Parses individual packet with format detection
5. `DecodeAllPackets()` - Opus decoding and timeline mixing
6. `ExportToMp3()` - PCM to MP3 conversion

## Technical Notes

- **Constants**:
  - `SAMPLE_RATE = 48000` (SRS standard)
  - `FRAME_SIZE_MS = 40` (Opus frame duration)
  - `SAMPLES_PER_FRAME = 1920` (calculated)

- **File Format Detection**: 
  - Tries to read enhanced format first
  - Falls back to legacy format if detection fails
  - Handles both formats transparently

- **Error Recovery**:
  - Try-catch around individual packet processing
  - Continues on failure, just logs warning
  - Final audio is best-effort from available data

## Testing Recommendations

1. Test with legacy format recordings
2. Test with enhanced format recordings  
3. Test with corrupted/truncated files
4. Test with very large files (memory usage)
5. Test with empty or zero-length files
6. Verify MP3 quality and duration matches source

## Future Enhancements (Optional)

- Add filtering by frequency/coalition
- Add player name extraction and display
- Support multiple output formats (WAV, FLAC)
- Add volume control parameter
- Add audio effects (EQ, compression)
- Multi-threaded decoding for large files
