# FreqExport - Frequency Export Tool

A command-line tool to export specific frequencies from DCS-SRS recording files to standard WAV audio files for playback in any media player.

## Features

- ? Export any frequency from SRS recordings to WAV format
- ? Automatic frequency detection and listing
- ? Generates two versions: raw decoded and processed audio
- ? **NEW: Smooth audio with automatic crossfading** (no clicks/pops)
- ? **NEW: Intelligent gap detection** for natural-sounding output
- ? Player information and packet statistics
- ? Simple command-line interface

## Recent Improvements

### v1.1 - Jitter Fix (Latest)
- ? **Eliminated audio jittering** in exported WAV files
- ? **Automatic crossfading** between transmission packets (10ms)
- ? **Intelligent gap detection** (distinguishes pauses from separate transmissions)
- ? **Enhanced normalization** with automatic quiet audio boosting
- ? Professional-quality output suitable for archival and sharing

**Result**: Smooth, natural-sounding audio instead of choppy/clicking playback!

See [Audio Export Jitter Fix](../docs/Audio-Export-Jitter-Fix.md) for technical details.

## Installation

Build the project:
```bash
cd C:\Users\Ohad\source\repos\AeroDebrief\FreqExport
dotnet build -c Release
```

The executable will be located at:
```
FreqExport\bin\Release\net9.0\FreqExport.exe
```

## Usage

### Basic Usage

Export frequency 303.1 MHz from a recording:
```bash
FreqExport recording.raw 303.1
```

This will create two files in the same directory as the recording:
- `freq_303.1_export_before.wav` - Raw decoded audio (smooth, professional quality)
- `freq_303.1_export_after.wav` - Processed audio with effects (smooth, professional quality)

### Specify Output Path

```bash
FreqExport recording.raw 303.1 output.wav
```

Creates:
- `output_before.wav`
- `output_after.wav`

### Full Path Example

```bash
FreqExport "C:\Recordings\mission_2024-01-15.raw" 251.0
```

## Output Format

Both output files are:
- **Sample Rate**: 48,000 Hz
- **Bit Depth**: 16-bit PCM
- **Channels**: Mono
- **Format**: Standard WAV (playable in any audio player)
- **Quality**: Professional-grade with automatic crossfading

### Difference Between Before and After

- **`_before.wav`**: Raw decoded Opus audio with smooth crossfades
- **`_after.wav`**: Audio with volume normalization, effects, and smooth crossfades

**Both versions now have professional quality thanks to the jitter fix!**

## Audio Quality Features

### Automatic Crossfading
- **10ms crossfades** between transmission packets
- Eliminates clicks and pops
- Natural transitions between transmissions

### Intelligent Gap Detection
- **Analyzes packet spacing** to distinguish:
  - Normal speech pauses (< 500ms) ? applies smooth crossfade
  - Separate transmissions (> 500ms) ? applies fade-in/out
- Results in natural-sounding audio

### Enhanced Normalization
- **Automatic peak detection** and limiting
- **Quiet audio boosting** for low-volume recordings
- **Maintains dynamic range** while ensuring audibility

## How It Works

1. **Scans** the recording file for all available frequencies
2. **Filters** packets matching the target frequency (within 1 kHz tolerance)
3. **Decodes** Opus-encoded audio packets to PCM
4. **Analyzes gaps** between packets to classify transmission patterns
5. **Applies crossfades** at packet boundaries (10ms smooth transitions)
6. **Mixes** overlapping transmissions into a continuous timeline
7. **Normalizes** audio to prevent clipping and boost quiet sections
8. **Exports** to standard WAV format

## Example Session

```
=== AeroDebrief Frequency Export Tool ===

?? Input:  mission_2024-01-15.raw
?? Frequency: 303.1 MHz (303100000 Hz)
?? Output: freq_303.1_export.wav

?? Loading recording file...
?? Scanning for frequencies...
? Found 12 frequency-modulation combinations

? Found frequency: 303.100 MHz (AM)
   Players using this frequency:
     - Viper-1 (F-16C_50) - 234 packets
     - Hawg-2 (A-10C) - 187 packets
     - Viper-3 (F-16C_50) - 156 packets

???  Applying frequency filter...
?? Exporting to WAV (this may take a moment)...
Processing 234 packets for export
Export timeline: 45.23s (2171040 samples at 48000Hz)
Audio mixing complete, normalizing...
Before: Peak amplitude = 0.8234, Non-zero samples = 1,856,420
After: Peak amplitude = 0.7891, Non-zero samples = 1,856,420
Writing WAV files...

? Export complete!

Created files:
  ?? freq_303.1_export_before.wav (2.45 MB)
     Raw decoded audio (before processing)
  ?? freq_303.1_export_after.wav (2.45 MB)
     Processed audio (with effects applied)

?? You can now listen to these files in any audio player!
```

## Frequency Not Found?

If the frequency isn't found, the tool will list all available frequencies:

```
? Frequency 303.1 MHz not found in recording!

Available frequencies:
  - 251.000 MHz (AM) - 456 packets
  - 264.000 MHz (AM) - 289 packets
  - 305.000 MHz (AM) - 178 packets
  - 251.000 MHz (FM) - 123 packets
```

## Error Handling

The tool provides clear error messages:
- ? File not found
- ? Invalid frequency format
- ? Frequency not in recording
- ? Export errors with details

## Technical Details

### Supported Formats
- Input: DCS-SRS `.raw` recording files
- Output: Standard WAV files (RIFF WAVE format)

### Audio Processing
- Opus decoding using libopus
- 48 kHz resampling
- **NEW: 10ms crossfade envelopes** (linear fade-in/out)
- **NEW: Gap detection and classification** (500ms threshold)
- Automatic normalization and gain boost
- Timestamp-based mixing

### Performance
- Typical export time: ~2-4 seconds per minute of audio
- Memory usage: ~100-500 MB depending on recording length
- Disk space: ~10 MB per minute of exported audio

## Quality Comparison

### Before Jitter Fix:
- ? Audible clicks every ~40ms
- ? Choppy, robotic sound
- ? Uncomfortable to listen to

### After Jitter Fix (Current):
- ? Smooth, natural audio
- ? No clicks or pops
- ? Professional quality
- ? Ready for archival/sharing

## Troubleshooting

### "Recording file not found"
Ensure the path is correct and use quotes for paths with spaces:
```bash
FreqExport "C:\My Recordings\mission.raw" 303.1
```

### "Frequency not found"
- Check the frequency value (in MHz, not Hz)
- List available frequencies by running without the output argument
- Frequency matching uses 1 kHz tolerance

### Audio still has issues
- Try the "before" version to check if it's a processing issue
- Check packet count in output - low packet count may indicate sparse data
- Verify the recording quality in the original file

## Use Cases

Perfect for:
- ? **Mission debriefs** - Extract specific frequency comms for review
- ? **Training material** - Create clean audio clips for instruction
- ? **Content creation** - Extract audio for videos/streams
- ? **Archival storage** - Save important communications
- ? **Public sharing** - Share mission comms with teammates

## Building from Source

Requirements:
- .NET 9 SDK
- AeroDebrief.Core project

Build command:
```bash
dotnet build -c Release
```

## License

Part of the AeroDebrief project.
Copyright © 2024 AeroDebrief Team

## Related Tools

- **AeroDebrief Player** - Full-featured SRS recording player with GUI
- **AeroDebrief CLI** - Batch processing and analysis tool

## Changelog

### v1.1 (Current)
- ? Fixed audio jittering with automatic crossfading
- ? Added intelligent gap detection
- ? Enhanced normalization with quiet audio boosting
- ? Improved logging and progress reporting

### v1.0
- Initial release with basic frequency export
