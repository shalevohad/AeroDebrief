# SimpleRawExport

A simple CLI tool to convert SRS raw recording files to MP3 format.

## Overview

This tool reads `.srs` recording files (created by the SRS Recording Client), decodes the Opus-encoded audio packets, and exports them as a single MP3 file.

## Features

- ? Reads SRS raw recording files
- ? Decodes Opus audio packets
- ? Handles both legacy and enhanced file formats
- ? Mixes all audio packets into a continuous timeline
- ? Automatic audio normalization
- ? Exports to high-quality MP3

## Dependencies

- **Common project**: For Opus decoder API
- **SharedAudio project**: For native Opus and LAME libraries (opus.dll, libmp3lame.dll)
- **NAudio**: For audio format conversion
- **NAudio.Lame**: For MP3 encoding

This tool does **not** depend on the Core project, making it lightweight and standalone.

### Native Libraries

The tool requires native libraries that are automatically copied from the SharedAudio project:
- `opus.dll` / `opus.so` - Opus audio codec
- `libmp3lame.dll` / `libmp3lame.so` - LAME MP3 encoder

## Usage

```bash
SimpleRawExport <input-file> [output-file]
```

### Arguments

- `input-file`: Path to the `.srs` recording file (required)
- `output-file`: Path for output MP3 file (optional)

If output file is not specified, it will be created in the same directory as the input file with `.mp3` extension.

### Examples

```bash
# Basic usage
SimpleRawExport recorded_audio.srs

# Specify output file
SimpleRawExport recorded_audio.srs output.mp3

# With full paths
SimpleRawExport "C:\Recordings\mission_20240101.srs" "C:\Export\mission.mp3"
```

## How It Works

1. **Read Packets**: Reads all audio packets from the raw recording file
2. **Decode Opus**: Decodes each Opus-encoded audio packet to PCM
3. **Mix Timeline**: Places decoded audio at the correct timestamp position
4. **Normalize**: Applies automatic gain normalization to prevent clipping
5. **Export MP3**: Converts to MP3 format using LAME encoder

## Technical Details

- **Sample Rate**: 48000 Hz (SRS standard)
- **Frame Size**: 40ms (1920 samples per frame)
- **Channels**: Mono (1 channel)
- **MP3 Quality**: LAME Standard preset (~192 kbps VBR)

## File Format Support

The tool supports both:
- **Legacy format**: Basic packet metadata
- **Enhanced format**: Extended player information (automatically detected)

## Audio Processing

- **Mixing**: All packets are mixed into a continuous timeline based on timestamps
- **Normalization**: Automatic gain adjustment if peak exceeds 1.0 (leaves 5% headroom)
- **Clipping Protection**: Audio is clamped to prevent distortion

## Performance

- Progress indicators for reading and decoding operations
- Handles large recordings efficiently
- Minimal memory footprint (processes packets sequentially)

## Error Handling

- Gracefully skips corrupted packets
- Continues processing even if individual packets fail
- Reports warnings for failed packets without stopping export

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run --project SimpleRawExport -- recorded_audio.srs
```
