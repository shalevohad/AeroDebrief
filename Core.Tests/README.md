# Core.Tests - Unit Test Project

This project contains unit tests for the Core library components.

## Test Coverage

### FilteredWaveformGeneratorTests

Tests for the `FilteredWaveformGenerator.CalculatePacketAmplitude` method, covering:

1. **Empty/Null Payloads**: Validates graceful handling of empty or null audio data
2. **Raw PCM Handling**: Tests direct Int16 PCM sample processing including:
   - Edge cases (`Int16.MinValue`, `Int16.MaxValue`) 
   - Average amplitude calculation
   - Odd-length buffer handling
3. **Opus-Encoded Audio**: Tests automatic detection and decoding of Opus-compressed packets
4. **Format Detection**: Validates heuristic-based detection between Opus and raw PCM formats

## Important Notes

### Centralized Audio Processing

The implementation now uses centralized helpers from `Core.Helpers.AudioHelpers`:

- **`DecodeAudioToPcm(byte[])`**: Automatically detects and decodes Opus or raw PCM
- **`CalculateNormalizedAmplitude(short[])`**: Computes amplitude safely without overflow
- **`IsOpusEncodedByteArray(byte[])`**: Detects audio format using size heuristics

This ensures consistent audio processing across the entire codebase.

### Opus vs PCM Detection

Audio data is automatically detected as:
- **Opus-encoded** (typical SRS recording format, < 400 bytes for 20ms frame)
- **Raw PCM Int16** (larger payloads, ~1920 bytes for 20ms @ 48kHz mono)

### Int16.MinValue Overflow Fix

The original implementation used `Math.Abs(short)` which throws `OverflowException` when called with `short.MinValue` (-32768) because negating it cannot be represented as a positive Int16.

**Solution**: Cast to `int` before calling `Math.Abs`:
```csharp
totalAmplitude += Math.Abs((int)sample);  // Safe for all Int16 values
```

This fix is now centralized in `AudioHelpers.CalculateNormalizedAmplitude()`.

### Opus Decoding

The implementation uses SRS's `OpusDecoder` from `Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core` to decode compressed audio packets before calculating amplitude.

Centralized in `AudioHelpers.DecodeOpusToPcm()` for reuse across:
- Waveform generation
- Spectrum analysis  
- WAV export
- Any future audio processing features

## Running Tests

```bash
dotnet test Core.Tests/Core.Tests.csproj
```

## Dependencies

- **xUnit** 2.9.2: Test framework
- **Microsoft.NET.Test.Sdk** 17.11.1: Test SDK
- **Core**: Main library (InternalsVisibleTo configured for test access)
