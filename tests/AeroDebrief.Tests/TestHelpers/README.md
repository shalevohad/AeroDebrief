# Test Helpers

This directory contains helper classes for testing AeroDebrief functionality.

## MockRecordingFileBuilder

A fluent API for creating mock AeroDebrief recording files (`.adb` format) for testing purposes.

### Quick Start

```csharp
using AeroDebrief.Tests.TestHelpers;

// Create a minimal test file
var testFile = MockRecordingFileBuilder.CreateMinimalTestFile();

// Create a file with multiple frequencies
var multiFreqFile = MockRecordingFileBuilder.CreateMultiFrequencyTestFile();

// Create a conversation scenario
var conversationFile = MockRecordingFileBuilder.CreateConversationTestFile(durationSeconds: 10);

// Create mock audio engines for testing
var processingEngine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
var outputEngine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
```

### Mock Audio Engines

The `MockRecordingFileBuilder` provides helper methods to create mock audio engines that don't require actual audio hardware:

```csharp
// Create a mock audio processing engine
var processingEngine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
processingEngine.SetMasterVolume(0.8f);
var audioData = processingEngine.ProcessPacket(packet);

// Create a mock audio output engine
var outputEngine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
outputEngine.Start();
await outputEngine.WriteAudioAsync(audioBytes);

// Verify operations
Assert.AreEqual(1, processingEngine.ProcessedPacketCount);
Assert.AreEqual(1, outputEngine.WrittenFrames.Count);
```

**See `../Audio/README_MOCK_ENGINES.md` for comprehensive documentation on using mock audio engines.**

### Fluent API Examples

```csharp
// Custom file with header and specific packets
using var builder = new MockRecordingFileBuilder("test.adb");
var file = builder
    .WithHeader(serverIp: "192.168.1.100", serverPort: 5002)
    .WithPacket(
        frequency: 251_000_000.0,
        playerName: "Viper-1",
        coalition: 2
    )
    .WithPackets(count: 100, interval: TimeSpan.FromMilliseconds(40))
    .Build();

// Multiple transmitters scenario
var file = new MockRecordingFileBuilder()
    .WithHeader()
    .WithMultipleTransmitters(
        packetsPerTransmitter: 20,
        ("Viper-1", 251_000_000.0, 2),    // Blue coalition, UHF
        ("Enfield-1", 127_500_000.0, 1),  // Red coalition, VHF
        ("Overlord", 305_000_000.0, 2)    // Blue coalition, UHF
    )
    .Build();

// Realistic conversation
var file = new MockRecordingFileBuilder()
    .WithHeader()
    .WithConversation(
        player1: "Viper-1",
        player2: "Viper-2",
        frequency: 251_000_000.0,
        durationSeconds: 30
    )
    .Build();
```

### File Format

The generated files follow the AeroDebrief recording format:

1. **Header (optional)**:
   - Magic string: `Constants.RECORDING_FILE_MAGIC` ("AERO_REC_V1")
   - Server IP (string)
   - Server port (int32)
   - Recording start time (int64 ticks)

2. **Packets**:
   - Each packet contains:
     - Timestamp
     - Frequency
     - Modulation
     - Player information
     - Audio payload (generated sine wave)

### Cleanup

Remember to clean up test files after use:

```csharp
[TestCleanup]
public void Cleanup()
{
    if (File.Exists(testFile))
    {
        File.Delete(testFile);
        
        // Also delete index file
        var indexFile = Path.ChangeExtension(testFile, ".pkidx");
        if (File.Exists(indexFile))
            File.Delete(indexFile);
    }
}
```

### Audio Payload

The builder generates realistic audio data using a 440 Hz sine wave (A4 note) at 48kHz sample rate. This ensures:
- Valid audio structure (16-bit PCM)
- Consistent packet sizes
- Predictable test results

### Use Cases

- **Unit Testing**: Create small files for specific test scenarios
- **Performance Testing**: Generate large files with many packets
- **Integration Testing**: Create multi-frequency, multi-player scenarios
- **Edge Case Testing**: Test with various coalitions, frequencies, and modulations
- **Audio Pipeline Testing**: Use mock engines to test audio processing without hardware

### Related Documentation

- `../Audio/README_MOCK_ENGINES.md` - Complete guide to mock audio engines
- `../Audio/MockAudioEnginesTests.cs` - Example usage of mock engines
