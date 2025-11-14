# AeroDebrief Test Data Setup

## Overview

The Pure FilePacketSource implementation tests require actual SRS recording files (.srs) to validate performance and functionality.

## Test Data Locations

Tests will automatically look for recording files in these locations (in order):

1. `TestData\sample.srs` (relative to test project)
2. `..\..\..\..\TestData\sample.srs` (workspace root)
3. `C:\Temp\test.srs` (fallback location)

## Creating Test Data

### Option 1: Use Existing Recording

If you have an SRS recording file from AeroDebrief or SimpleRadioStandalone:

```powershell
# Create TestData directory in workspace root
mkdir C:\Users\Ohad\source\repos\AeroDebrief\TestData

# Copy an existing recording
copy "path\to\your\recording.srs" "C:\Users\Ohad\source\repos\AeroDebrief\TestData\sample.srs"
```

### Option 2: Copy to C:\Temp

```powershell
# Create temp directory if it doesn't exist
mkdir C:\Temp

# Copy recording
copy "path\to\your\recording.srs" "C:\Temp\test.srs"
```

## Test Characteristics

### Performance Tests

These tests verify the performance improvements of the Pure FilePacketSource architecture:

| Test | Target | Metric |
|------|--------|--------|
| File Open | < 1 second | File open + index build time |
| Metadata Retrieval | < 1 ms | GetFrequencyMetadata() call time |
| Frequency Filter | < 2 ms | SetFrequencyGate() call time |
| Pilot Filter | < 2 ms | SetPilotGate() call time |
| Memory Usage | < 15 MB | Total RAM consumed |

### Functional Tests

These tests verify correct behavior:

- Batched streaming (100 packets/batch)
- Playback state management (Play/Pause/Resume/Stop)
- Seek operations
- Filter application (frequency and pilot)
- Error handling
- Resource cleanup

## Running Tests Without Test Data

If no test data is available, tests will report as **Inconclusive** rather than failing. This allows the test suite to run in CI/CD environments without requiring large binary files.

Example output:
```
Test: FilePacketSourceTests.OpenAsync_Performance_IsFasterThan1Second
Result: Inconclusive
Message: No test recording file available
```

## Recommended Test File

For consistent testing, use a recording file with these characteristics:

- **Duration**: 30-60 seconds (enough to test seeking)
- **Frequencies**: 2-5 different frequencies (tests filtering)
- **Pilots**: 3-10 pilots (tests per-pilot filtering)
- **Size**: 1-5 MB (reasonable for CI/CD)

## Creating a Minimal Test Recording

If you don't have an existing recording, you can create one:

1. Run SimpleRadioStandalone or AeroDebrief
2. Enable recording
3. Transmit on 2-3 different frequencies for 30-60 seconds
4. Stop recording
5. Copy the generated .srs file to a test data location

## CI/CD Integration

For automated testing pipelines:

1. Store a minimal test recording file in the repository (if size permits)
2. Or, store in a shared location accessible to CI/CD agents
3. Or, skip performance tests if no data available (tests will be inconclusive)

Example Azure DevOps pipeline step:

```yaml
- task: CopyFiles@2
  displayName: 'Copy Test Data'
  inputs:
    SourceFolder: '$(Pipeline.Workspace)/test-data'
    Contents: 'sample.srs'
    TargetFolder: '$(Build.SourcesDirectory)/TestData'
  condition: exists('$(Pipeline.Workspace)/test-data/sample.srs')
```

## Troubleshooting

### Tests are Inconclusive

**Cause**: No test recording file found

**Solution**: Add a recording file to one of the test data locations

### Tests Fail with FileNotFoundException

**Cause**: File path is incorrect or inaccessible

**Solution**: Verify file exists at the expected location with correct permissions

### Tests Fail with Performance Thresholds

**Cause**: System under load or slow disk I/O

**Solution**: 
1. Close other applications
2. Run tests on SSD if possible
3. Check disk performance
4. Verify no background scans running

### Memory Usage Tests Fail

**Cause**: Other tests or processes consuming memory

**Solution**:
1. Run memory tests individually
2. Restart Visual Studio Test Host
3. Force garbage collection before test
4. Check for memory leaks in test setup

## File Format Notes

The .srs file format:
- Binary format
- Contains audio packets with metadata
- Includes timestamps, frequencies, pilot IDs
- Opus-encoded audio data
- Self-contained (no external dependencies)

For details, see: `src\AeroDebrief.Core\Models\AudioPacketMetadata.cs`
