# Integration Tests - Testing Production Code

## Purpose

These tests use the **EXACT SAME CODE** as the production application. There are NO separate test implementations, NO mocks of the core services, NO test doubles.

## What Makes These Different

### ? Traditional Unit Tests
```csharp
// WRONG - Testing a mock, not the real code
var mockReader = new Mock<IAudioPacketReader>();
mockReader.Setup(x => x.LoadFile()).Returns(...);
```

### ? Our Integration Tests
```csharp
// RIGHT - Testing the ACTUAL production code
using var audioSession = new AudioSession(); // Real UI service
var result = await audioSession.LoadFileAsync(file); // Real implementation
```

## How It Works

1. **Direct Project References**: The test project references `AeroDebrief.UI` and `AeroDebrief.Core` projects
2. **Same Assemblies**: Tests run against the actual compiled DLLs in `bin\Debug` or `bin\Release`
3. **Same Code Path**: Tests execute the exact same code that runs in production
4. **Real Performance**: Measurements reflect actual production performance

## Running the Tests

### From Command Line

```bash
# Build everything first (ensures tests use latest code)
dotnet build AeroDebrief.sln --configuration Debug

# Run all tests
dotnet test AeroDebrief.sln

# Run only integration tests
dotnet test AeroDebrief.sln --filter "TestCategory=Integration"

# Run only performance tests
dotnet test AeroDebrief.sln --filter "TestCategory=Performance"
```

### From Visual Studio

1. Build Solution (Ctrl+Shift+B)
2. Open Test Explorer (Test ? Test Explorer)
3. Click "Run All Tests"

**CRITICAL**: Always **rebuild** before running tests to ensure you're testing the latest code!

## Test Categories

### Integration Tests
- Test the full integration between UI and Core layers
- Use real file I/O
- Execute actual production code paths

### Performance Tests
- Measure actual load times
- Verify caching is working
- Ensure optimization targets are met

### Diagnostic Tests
- Verify we're testing the correct assemblies
- Check assembly ages (prevent testing old code)
- Validate build configuration

## Test Data

Tests look for test files in `tests\AeroDebrief.Tests\TestData\`.

To run the tests:
1. Create the `TestData` directory
2. Place a test `.adb` recording file named `test_recording.adb` in that directory
3. Run the tests

## What the Tests Verify

### FileLoadingPerformanceTests

#### `LoadFile_ShouldUsePacketCache_SingleFileRead`
- ? File loads successfully
- ? Load time is reasonable (< 10 seconds)
- ? Cache is populated
- ? Subsequent operations use cache (< 100ms)

#### `LoadFile_MultipleCalls_ShouldNotReloadFile`
- ? Cache persists across multiple calls
- ? Cache hits are 10x+ faster than initial load
- ? Cache returns consistent data

#### `LoadFile_CheckOptimizationLogs`
- ? Optimization messages appear in logs
- ? Only one file read occurs
- ? Cache is used for all subsequent operations

#### `VerifyTestingCorrectAssembly`
- ? Tests are using actual production assemblies
- ? Assemblies are recent (not cached old versions)
- ? No mock or test double assemblies are being used

## Expected Test Output

```
========================================
INTEGRATION TEST - USING PRODUCTION CODE
========================================
Testing Assembly: AeroDebrief.UI
Assembly Version: 1.0.0
Assembly Location: C:\...\bin\Debug\net9.0-windows\AeroDebrief.UI.dll
Assembly Build Date: 2024-01-15 14:32:10
Assembly Age: 2.3 minutes
========================================

Test: LoadFile_ShouldUsePacketCache_SingleFileRead
? File loaded in 2,543ms
   Total Duration: 01:23:45
   File Path: C:\...\test_recording.adb
? Frequency retrieval (from cache) took 8ms
   Found 12 frequencies

PASSED
```

## Troubleshooting

### Tests Are Using Old Code

**Problem**: Assembly age shows > 1 hour

**Solution**:
```bash
# Clean and rebuild
dotnet clean AeroDebrief.sln
dotnet build AeroDebrief.sln --configuration Debug
dotnet test AeroDebrief.sln
```

### Tests Fail with "File Not Found"

**Problem**: No test data file

**Solution**:
1. Create `tests\AeroDebrief.Tests\TestData\` directory
2. Copy a test `.adb` file to `test_recording.adb`
3. Re-run tests

### Performance Tests Fail (Too Slow)

**Problem**: File loading is slower than expected

**Solution**:
1. Check if optimization fix is present (see logs)
2. Verify you're testing the latest code (check assembly age)
3. Check test machine performance

### Logs Show No Optimization Messages

**Problem**: Fix is not being executed

**Solution**:
1. Rebuild: `dotnet build AeroDebrief.sln`
2. Verify fix is in source: Check `CoreApiService.cs`
3. Run: `.\verify_fix.ps1` to clean and rebuild everything

## Why This Approach?

### Traditional Approach (?)
```
Application Code ? Interface ? Mock Implementation ? Tests
                                ? Tests don't verify real code!
```

### Our Approach (?)
```
Application Code ? Tests directly
? Tests verify ACTUAL production code!
```

## Benefits

1. **High Confidence**: Tests prove the actual production code works
2. **Real Performance**: Measurements reflect actual runtime performance
3. **No Duplication**: No need to maintain separate test implementations
4. **Catch Real Bugs**: Tests find issues that would occur in production
5. **Same Code Path**: Tests execute the exact path users will take

## Limitations

1. **Slower**: Integration tests are slower than unit tests (but more valuable)
2. **Requires Test Data**: Need actual `.adb` files for testing
3. **Environment Dependent**: Tests may behave differently on different machines

## When to Use

- ? Testing critical performance optimizations (like our cache fix)
- ? Verifying end-to-end workflows
- ? Confirming bug fixes work in the real system
- ? Performance regression testing

## When NOT to Use

- ? Testing pure algorithms (use unit tests)
- ? Testing edge cases with specific inputs (use unit tests)
- ? Testing UI rendering (use UI tests)

## Continuous Integration

These tests can run in CI/CD:

```yaml
# Example GitHub Actions
- name: Run Integration Tests
  run: |
    dotnet build AeroDebrief.sln --configuration Release
    dotnet test AeroDebrief.sln --filter "TestCategory=Integration" --configuration Release
```

## Summary

**TL;DR**: These tests use the **EXACT SAME CODE** as production. No mocks, no fakes, no test doubles. They verify the actual performance fix works in the real codebase.

To run them:
```bash
dotnet build AeroDebrief.sln
dotnet test AeroDebrief.sln --filter "TestCategory=Integration"
```

---

# End-to-End Pipeline Integration Tests

## Overview

This test suite provides comprehensive end-to-end validation of the entire AeroDebrief pipeline, from synthetic recording generation through file I/O, analysis, processing, and playback. All tests use dummy data generated by the system and are designed to run on multiple platforms (Windows, Linux, macOS) in both Visual Studio IDE and GitHub Actions CI/CD.

## Test Coverage

### Test 1: Generate Recording - File Structure Validation
**Category**: Recording  
**Purpose**: Validates synthetic recording file generation and header structure  
**Validates**:
- File creation and size
- Magic header (`AERO_REC_V1`)
- Server metadata (IP, port, timestamp)
- Binary format correctness

### Test 2: Read Recording - Packet Structure Validation
**Category**: Reading  
**Purpose**: Validates packet reading and deserialization  
**Validates**:
- Packet enumeration (`RecordingFileReader`)
- Packet structure integrity
- Timestamp validity
- Frequency and player data extraction
- Audio payload presence

### Test 3: Analyze Recording - Analysis Results Validation
**Category**: Analysis  
**Purpose**: Validates comprehensive file analysis capabilities  
**Validates**:
- Frequency detection (`FileAnalyzer.GetAllFrequencyModulations`)
- Duration calculation (`FileAnalyzer.CalculateTotalDuration`)
- Activity analysis (`FileAnalyzer.AnalyzeAudioActivity`)
- Player enumeration
- Activity period detection

### Test 4: Decode Audio - Quality Validation
**Category**: AudioDecoding  
**Purpose**: Validates audio decoding and quality metrics  
**Validates**:
- OPUS/PCM decoding (`AudioHelpers.DecodeAudioToPcm`)
- Sample range validation (16-bit PCM)
- Amplitude calculation
- Signal presence detection
- Decoding success rate

### Test 5: Load FilePacketSource - Indexing Validation
**Category**: Indexing  
**Purpose**: Validates memory-mapped file indexing  
**Validates**:
- `FilePacketSource` initialization
- Packet indexing performance
- Duration calculation
- Random access functionality
- Memory efficiency

### Test 6: Playback Pipeline - Audio Quality Validation
**Category**: Playback  
**Purpose**: Validates complete playback pipeline with audio capture  
**Validates**:
- `FilePlaybackPipeline` initialization
- Frequency gate filtering
- Audio output capture (`TestAudioCapture`)
- Audio quality metrics (RMS, range, clipping)
- Real-time mixing (`MasterMixer`)

### Test 7: Playback Speed - Time Accuracy Validation
**Category**: PlaybackSpeed  
**Purpose**: Validates playback speed control and time accuracy  
**Validates**:
- Playback speed control (0.5x, 1.0x, 2.0x)
- Time advancement accuracy
- Position tracking
- Speed clamping (0.25x - 4.0x range)

### Test 8: Multi-Frequency Mixing - Filtering Validation
**Category**: Mixing  
**Purpose**: Validates multi-frequency mixing and filtering  
**Validates**:
- Frequency gate filtering
- Multiple frequency mixing
- Audio isolation per frequency
- Filter effectiveness

### Test 9: Seek Accuracy - Position Tracking Validation
**Category**: Seeking  
**Purpose**: Validates seek functionality and position accuracy  
**Validates**:
- Seek to arbitrary positions
- Position tracking accuracy (±1 second tolerance)
- Edge case handling (start, middle, end)

### Test 10: Complete Pipeline Stress Test
**Category**: StressTest  
**Purpose**: End-to-end stress test of entire pipeline  
**Validates**:
- 10MB recording generation
- Full file analysis
- Complete indexing
- Playback at 4x speed
- Audio quality under load
- Performance metrics

## Running End-to-End Tests

### Visual Studio IDE

#### Run All End-to-End Tests
```
Test ? Run Tests ? Run Tests for Test Category ? EndToEnd
```

Or in Test Explorer, filter by: `TestCategory=EndToEnd`

#### Run Specific Test Categories
- **Recording**: `TestCategory=EndToEnd&TestCategory=Recording`
- **Reading**: `TestCategory=EndToEnd&TestCategory=Reading`
- **Analysis**: `TestCategory=EndToEnd&TestCategory=Analysis`
- **AudioDecoding**: `TestCategory=EndToEnd&TestCategory=AudioDecoding`
- **Indexing**: `TestCategory=EndToEnd&TestCategory=Indexing`
- **Playback**: `TestCategory=EndToEnd&TestCategory=Playback`
- **PlaybackSpeed**: `TestCategory=EndToEnd&TestCategory=PlaybackSpeed`
- **Mixing**: `TestCategory=EndToEnd&TestCategory=Mixing`
- **Seeking**: `TestCategory=EndToEnd&TestCategory=Seeking`
- **StressTest**: `TestCategory=EndToEnd&TestCategory=StressTest`

### Command Line (dotnet CLI)

#### Run All End-to-End Tests
```bash
dotnet test --filter "TestCategory=EndToEnd"
```

#### Run with Detailed Logging
```bash
dotnet test --filter "TestCategory=EndToEnd" --logger "console;verbosity=detailed"
```

#### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~GenerateRecording_CreatesValidFile"
```

#### Run with Coverage
```bash
dotnet test --filter "TestCategory=EndToEnd" --collect:"XPlat Code Coverage"
```

### GitHub Actions CI/CD

Tests run automatically on:
- Push to `main`, `develop`, or `Unified-player-with-settings` branches
- Pull requests to `main` or `develop`
- Manual workflow dispatch

**Workflow File**: `.github/workflows/end-to-end-tests.yml`

**Platforms Tested**:
- Windows (windows-latest)
- Linux (ubuntu-latest)
- macOS (macos-latest)

**View Results**:
1. Go to repository ? Actions tab
2. Select "End-to-End Pipeline Tests" workflow
3. View test results per platform
4. Download test artifacts (TRX files, coverage reports)

## Test Data - Synthetic Recording Generator

### Synthetic Recording Generator

**Location**: `tests\AeroDebrief.Tests\IO\SyntheticRecordingGenerator.cs`

**Capabilities**:
- Generates `.adb` recording files of any size
- Simulates multiple frequencies (VHF AM: 251.0, 243.0, 305.0 MHz)
- Simulates multiple players (Viper-1, Enfield-2-1, Uzi-1, Hawg-1-1)
- Generates synthetic audio (440Hz sine wave)
- Includes realistic metadata (timestamps, positions, coalitions, aircraft)
- Configurable packet interval (default 40ms)

**Usage Example**:
```csharp
var file = await SyntheticRecordingGenerator.GenerateAsync(
    targetSizeMB: 10,
    packetIntervalMs: 40
);
```

**Benefits**:
- **No external dependencies** - Tests generate their own data
- **Consistent results** - Same data every time
- **Configurable** - Adjust size, frequency, players as needed
- **Fast** - Generates 10MB in < 5 seconds
- **Cross-platform** - Works on Windows, Linux, macOS

## Performance Benchmarks

Based on 10MB synthetic recording (Test 10):

| Operation | Target Time | Notes |
|-----------|-------------|-------|
| Generate Recording | < 5s | 10MB file with audio |
| Analyze File | < 3s | Frequency + activity analysis |
| Index File | < 1s | Memory-mapped indexing |
| Playback (4x speed) | Duration/4 + 10s | Full file playback |

## Architecture Tested

### Recording Pipeline
1. **Synthetic Generation** ? `SyntheticRecordingGenerator`
2. **File Writing** ? Binary format with header
3. **Packet Serialization** ? `AudioPacketMetadata.TryWriteMetadata`

### Analysis Pipeline
1. **File Reading** ? `RecordingFileReader.EnumeratePackets`
2. **Frequency Analysis** ? `FileAnalyzer.GetAllFrequencyModulations`
3. **Duration Calculation** ? `FileAnalyzer.CalculateTotalDuration`
4. **Activity Detection** ? `FileAnalyzer.AnalyzeAudioActivity`

### Playback Pipeline
1. **File Indexing** ? `FilePacketSource` (memory-mapped)
2. **Packet Routing** ? `FilePlaybackPipeline`
3. **Audio Processing** ? `UserWorker` + `JitterBuffer`
4. **Audio Mixing** ? `MasterMixer` + `AudioMixerEngine`
5. **Audio Output** ? `TestAudioCapture` (mock output)
6. **Quality Analysis** ? `AudioAnalyzer` metrics

### Quality Metrics Validated
- **RMS (Root Mean Square)** - Signal strength
- **Peak Amplitude** - Maximum signal level
- **Clipping Detection** - Samples at ±1.0
- **Range Validation** - All samples in [-1.0, 1.0]
- **Clicks/Pops Detection** - Sudden amplitude changes
- **SNR (Signal-to-Noise Ratio)** - Signal quality
- **Crest Factor** - Dynamic range (Peak/RMS)

## Timeout Configuration

All tests have appropriate timeouts:
- Standard tests: 30 seconds
- Stress test: 60 seconds

Configured via `[Timeout(milliseconds)]` attribute.

## Troubleshooting

### Test Fails in CI but Passes Locally
- Check platform-specific code paths (Windows vs Linux vs macOS)
- Verify file path separators (`Path.DirectorySeparatorChar`)
- Check timing-sensitive tests (may need increased tolerance)

### Test Timeout
- Increase `[Timeout]` value if legitimate (large files, slow CI runners)
- Check for deadlocks or infinite loops
- Verify cancellation token handling

### File Access Errors
- Ensure test cleanup disposes resources (`using` statements)
- Check for file locking issues
- Verify temp file permissions

### Audio Quality Assertion Failures
- Synthetic audio may have variable characteristics
- Adjust quality thresholds if consistently failing on specific platforms
- Check for audio buffer overflow/underrun

## Dependencies

**Core Components**:
- `AeroDebrief.Core` - Recording, analysis, playback
- `AeroDebrief.Tests.Audio` - `TestAudioCapture`, `AudioAnalyzer`, `TestAudioSource`
- `AeroDebrief.Tests.IO` - `SyntheticRecordingGenerator`

**NuGet Packages**:
- `Microsoft.VisualStudio.TestTools.UnitTesting` - Test framework
- `NLog` - Logging
- NAudio, Opus encoders (via SRS Common)

## Future Enhancements

### Planned Additions
1. **Network jitter simulation** - Test with `JitteredAudioSource`
2. **Corrupted file handling** - Validate error recovery
3. **Large file stress tests** - 100MB+ recordings
4. **Tacview export validation** - End-to-end integration with Tacview
5. **Real-time recording simulation** - Test `AudioPacketRecorder`
6. **Memory leak detection** - Long-running stress tests
7. **Concurrent playback** - Multiple pipelines simultaneously

### Performance Profiling
- Add BenchmarkDotNet integration
- Profile memory usage patterns
- Measure disk I/O efficiency
- Track CPU utilization

## Contributing to End-to-End Tests

When adding new end-to-end tests:

1. **Use `[TestCategory("EndToEnd")]`** - Ensures inclusion in CI
2. **Add specific category** - Recording, Reading, Analysis, etc.
3. **Set appropriate timeout** - Default 30s, stress tests 60s+
4. **Clean up resources** - Use `[TestCleanup]` and `using` statements
5. **Log comprehensively** - Aid debugging in CI environments
6. **Use synthetic data** - No external dependencies
7. **Cross-platform compatibility** - Test on Windows, Linux, macOS

## Summary

The end-to-end pipeline tests provide comprehensive validation of the entire system using dummy data generated by `SyntheticRecordingGenerator`. They run on multiple platforms in both Visual Studio IDE and GitHub Actions CI/CD, ensuring cross-platform compatibility and quality.

**Key Features**:
- ? **10 comprehensive tests** covering entire pipeline
- ? **Synthetic data generation** - no external dependencies
- ? **Cross-platform** - Windows, Linux, macOS
- ? **CI/CD integration** - GitHub Actions workflow
- ? **Audio quality validation** - comprehensive metrics
- ? **Performance benchmarks** - validates optimization targets

**To run locally**:
```bash
dotnet test --filter "TestCategory=EndToEnd"
```

**To view CI results**:
Go to: Repository ? Actions ? "End-to-End Pipeline Tests"
