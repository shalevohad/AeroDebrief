# Running Phase 2 Tests - Quick Guide

## Overview
Phase 2 includes unit tests (Day 1) and integration tests (Day 2.2) that validate the amplitude extraction pipeline without requiring real flight recordings.

---

## ?? Quick Start

### Option 1: Run from Test Project
```csharp
// Add to your test entry point or create a console app reference
using AeroDebrief.Tests;

// Run all Phase 2 tests
await Phase2TestRunner.RunAllPhase2TestsAsync();
```

### Option 2: Create Test Window in UI
```csharp
// In UnifiedPlayerControl or MainWindow
private async void OnRunPhase2Tests_Click(object sender, RoutedEventArgs e)
{
    await AeroDebrief.Tests.Phase2TestRunner.RunAllPhase2TestsAsync();
}
```

### Option 3: CLI Test Command
```bash
# If CLI test runner is implemented
dotnet run --project src/AeroDebrief.CLI -- test-phase2
```

---

## ?? Test Suites

### Unit Tests (Phase 2 Day 1)
**File**: `tests/AeroDebrief.Tests/AmplitudeExtractionTests.cs`  
**Focus**: dBFS conversion and peak amplitude calculation  
**Count**: 16 tests  
**Duration**: <1 second

```csharp
// Run unit tests
AmplitudeExtractionTests.RunAllTests();
```

**Tests**:
- dBFS conversion (9 tests)
- Peak amplitude calculation (7 tests)

### Integration Tests (Phase 2.2)
**File**: `tests/AeroDebrief.Tests/Graphs/AmplitudeExtractionPipelineTests.cs`  
**Focus**: End-to-end pipeline with mock recordings  
**Count**: 4 tests  
**Duration**: ~3-4 seconds

```csharp
// Run integration tests
await AmplitudeExtractionPipelineTests.RunAllTests();
```

**Tests**:
1. Simple single frequency (1 pilot, 4 seconds)
2. Multiple frequencies (3 pilots, different coalitions)
3. Realistic chatter (500 packets, performance test)
4. Synthetic fallback (no file required)

---

## ?? What Gets Tested

### Test 1: Simple Single Frequency
- **Creates**: Mock .adb file with 100 packets
- **Frequency**: 251 MHz
- **Pilot**: Viper-1 (Blue)
- **Duration**: ~4 seconds
- **Validates**:
  - Series extraction works
  - Points in time order
  - dBFS range correct (-120 to 0)
  - Time offsets from recording start

### Test 2: Multiple Frequencies
- **Creates**: Mock .adb file with 150 packets
- **Frequencies**: 251, 243, 305 MHz
- **Pilots**: Viper-1, Enfield-1, Frogfoot-1
- **Coalitions**: Blue, Blue, Red
- **Validates**:
  - Multiple series generated
  - Frequency grouping correct
  - Coalition separation

### Test 3: Realistic Chatter
- **Creates**: 1MB synthetic recording
- **Packets**: ~500 (rotating frequencies/pilots)
- **Duration**: ~20 seconds
- **Validates**:
  - Processing performance
  - Memory efficiency
  - Realistic patterns

### Test 4: Synthetic Fallback
- **Creates**: No file (in-memory)
- **Mode**: AmplitudeSeriesProvider without FilePacketSource
- **Duration**: 5 seconds
- **Validates**:
  - Fallback mode works
  - Time offsets start at 0
  - Data generation correct

---

## ?? Expected Output

### Console Log
```
========================================
Phase 2.2: Amplitude Extraction Pipeline Tests
========================================
Creating simple mock recording (single frequency, single pilot)...
Mock recording created: C:\Temp\tmpABC123.tmp
Packets: 100, Duration: ~4 seconds
Recording loaded: 100 packets, 4.0s duration
Series: F251.0-P1, Points: 800
  dBFS range: -85.3 to -32.1 dBFS
  Time range: 0.000s to 3.950s
? Extracted 1 series with 800 total points
? Test 1 PASSED: Simple single frequency

Creating mock recording with multiple frequencies and talking patterns...
Mock recording created with 3 transmitters
Recording loaded: 150 packets
Series: F251.0-P1, Points: 400
Series: F243.0-P2, Points: 400
Series: F305.0-P3, Points: 400
? Extracted 3 series across 3 frequencies
? Test 2 PASSED: Multiple frequencies with talking patterns

Creating realistic radio chatter simulation...
Generating: 0%
Generating: 100%
Synthetic recording generated: 870KB
Recording opened in 234ms
Total packets: 582
Total duration: 23.3s
Series F251.0-P1: 1164 points, time: 0.000s-23.280s, peak: -28.3 dBFS
Series F243.0-P2: 1164 points, time: 0.040s-23.320s, peak: -31.5 dBFS
Series F305.0-P3: 1165 points, time: 0.080s-23.360s, peak: -29.8 dBFS
Series F251.0-P4: 1164 points, time: 0.120s-23.400s, peak: -30.2 dBFS
? Extracted 4 series with 4657 points in 1234ms
Processing speed: 3773 points/sec
? Test 3 PASSED: Realistic radio chatter

Testing synthetic data fallback mode...
Synthetic series F251.0-P0: 20 points
Synthetic series F251.0-P1: 20 points
... (more series)
? Generated 48 synthetic series with 960 points in 12ms
? Test 4 PASSED: Synthetic data fallback

========================================
Tests Completed: 4/4 passed
========================================
```

---

## ?? Test Configuration

### Mock Recording Settings
```csharp
// Packet interval (Opus frame duration)
const int PACKET_INTERVAL_MS = 40;

// Test frequencies (SRS standard)
251_000_000.0  // 251 MHz UHF
243_000_000.0  // 243 MHz UHF
305_000_000.0  // 305 MHz VHF

// Audio payload size (per packet)
~1440 bytes (Opus encoded)

// Total packet size
~1720 bytes (including metadata)
```

### Performance Targets
- **Processing**: <5 seconds for 500 packets
- **Speed**: >5000 points/second
- **Memory**: <100MB peak
- **File Cleanup**: Automatic (temp files deleted)

---

## ?? Troubleshooting

### Test Fails: "Mock recording created but FilePacketSource fails to open"
**Solution**: Check temp directory permissions
```csharp
var tempDir = Path.GetTempPath();
Console.WriteLine($"Temp dir: {tempDir}");
// Ensure write access
```

### Test Fails: "Series has no points"
**Possible Causes**:
1. AudioProcessingEngine not initialized
2. Opus decoding issue
3. AmplitudeExtractor configuration wrong

**Debug**:
```csharp
// Enable detailed logging
Logger.Factory.Configuration.Variables["level"] = "Debug";
```

### Test Hangs: "Generating synthetic recording..."
**Solution**: Check for CPU/disk bottleneck
- Reduce targetSizeMB to 1 (from 100)
- Check available disk space
- Reduce packet count

### Memory Usage Too High
**Solution**: Run tests individually instead of all at once
```csharp
// Run one at a time
await Test_SimpleSingleFrequency();
GC.Collect(); // Force cleanup
await Test_MultipleFrequenciesWithTalkingPatterns();
```

---

## ?? Test File Locations

### Created During Tests
```
%TEMP%\synthetic_*.adb    - Temporary test recordings
%TEMP%\tmp*.tmp           - Mock recording files
```

**Cleanup**: Automatic via `finally` blocks and `using` statements

### Permanent Test Files
```
tests/AeroDebrief.Tests/
  ??? Graphs/
  ?   ??? AmplitudeExtractionPipelineTests.cs   (integration)
  ??? TestHelpers/
  ?   ??? MockRecordingFileBuilder.cs            (builder)
  ??? IO/
  ?   ??? SyntheticRecordingGenerator.cs         (generator)
  ??? AmplitudeExtractionTests.cs                (unit)
  ??? Phase2TestRunner.cs                        (coordinator)
```

---

## ?? Customizing Tests

### Change Test Duration
```csharp
// In AmplitudeExtractionPipelineTests.cs
builder.WithPackets(
    count: 200,  // Increase from 100
    startTime: startTime,
    // ...
);
```

### Add Custom Frequencies
```csharp
// Test custom frequency
builder.WithPacket(
    frequency: 123_000_000.0,  // Your frequency
    playerName: "YourCallsign",
    // ...
);
```

### Test Specific Patterns
```csharp
// Create custom talking pattern
for (int i = 0; i < 10; i++)
{
    // Burst transmission
    builder.WithPackets(5, startTime, frequency: freq);
    startTime = startTime.AddSeconds(1);
    
    // Silence
    startTime = startTime.AddSeconds(2);
}
```

---

## ? Passing Criteria

### All Tests Must
- ? Complete without exceptions
- ? Generate expected number of series
- ? Produce points in time order
- ? Have dBFS values in range [-120, 0]
- ? Complete within performance targets
- ? Clean up resources properly

### Integration Tests Should
- ? Create temporary files
- ? Open files with FilePacketSource
- ? Extract amplitude data
- ? Validate output format
- ? Delete temp files on completion

---

## ?? Adding New Tests

### Template
```csharp
private static async Task<bool> Test_YourNewTest()
{
    string? recordingFile = null;
    try
    {
        Logger.Info("Creating test recording...");
        
        // Create mock recording
        using var builder = new MockRecordingFileBuilder();
        // ... build recording ...
        recordingFile = builder.FilePath;
        builder.Dispose();
        
        // Open and test
        var packetSource = new FilePacketSource(recordingFile);
        await packetSource.OpenAsync();
        
        // Your test logic here
        
        packetSource.Dispose();
        return true; // Test passed
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Test failed");
        return false;
    }
    finally
    {
        // Cleanup
        if (recordingFile != null && File.Exists(recordingFile))
            File.Delete(recordingFile);
    }
}
```

### Add to Test Suite
```csharp
// In RunAllTests()
totalTests++;
if (await Test_YourNewTest())
    passedTests++;
```

---

## ?? Next Steps

After tests pass:
1. ? Verify all 4 integration tests pass
2. ? Check console output for performance metrics
3. ? Review NLog output for warnings/errors
4. ? Proceed to Phase 2.3 (UI Integration)

---

**Test Suite Status**: ? Ready to Run  
**Documentation**: ? Complete  
**Build**: ? Successful

Run tests with: `await Phase2TestRunner.RunAllPhase2TestsAsync();`
