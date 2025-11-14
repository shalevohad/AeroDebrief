# Mock Audio Engines

This directory contains mock implementations of audio processing and output engines for testing purposes.

## Overview

The mock audio engines allow you to test audio-related functionality without requiring actual audio hardware or dealing with the complexities of real audio processing. They record all operations for verification and provide deterministic behavior.

## Available Mocks

### MockAudioProcessingEngine

Simulates audio processing without actual Opus decoding. Located in `MockAudioProcessingEngine.cs`.

**Features:**
- Simulates audio packet decoding (converts byte[] to float[] without real Opus decoding)
- Tracks processed packet count
- Supports volume control (master and per-transmitter)
- Implements `IAudioProcessingEngine` interface
- Records initialization and disposal state

**Example Usage:**

```csharp
// Create and initialize the mock engine
var engine = new MockAudioProcessingEngine();
engine.Initialize();

// Process packets
var packet = CreateTestPacket(); // Your test packet creation method
var audioData = engine.ProcessPacket(packet);

// Verify operations
Assert.AreEqual(1, engine.ProcessedPacketCount);
Assert.IsTrue(engine.IsInitialized);

// Set volume
engine.SetMasterVolume(0.5f);
engine.SetTransmitterVolume("GUID_123", 0.7f);

// Reset state
engine.ResetDecoders();
Assert.AreEqual(0, engine.ProcessedPacketCount);

// Cleanup
engine.Dispose();
Assert.IsTrue(engine.IsDisposed);
```

### MockAudioOutputEngine

Simulates audio output without requiring actual audio hardware. Located in `MockAudioOutputEngine.cs`.

**Features:**
- Records all written audio frames for verification
- Tracks method call counts (Initialize, Start, Stop, ClearBuffer)
- Implements `IAudioOutputEngine` interface
- Tracks running state and volume
- Records total bytes written

**Example Usage:**

```csharp
// Create and initialize the mock engine
var engine = new MockAudioOutputEngine();
await engine.InitializeAsync();

// Start playback
engine.Start();
Assert.IsTrue(engine.IsRunning);

// Write audio data
var audioData = CreateTestAudioData(); // Your test data creation method
await engine.WriteAudioAsync(audioData);

// Verify operations
Assert.AreEqual(1, engine.WrittenFrames.Count);
Assert.IsTrue(engine.TotalBytesWritten > 0);

// Control volume
engine.SetMasterVolume(0.8f);
Assert.AreEqual(0.8f, engine.CurrentVolume, 0.01f);

// Clear buffer
engine.ClearBuffer();
Assert.AreEqual(0, engine.WrittenFrames.Count);

// Stop and cleanup
engine.Stop();
Assert.IsFalse(engine.IsRunning);

engine.Dispose();
Assert.IsTrue(engine.IsDisposed);
```

## Helper Methods in MockRecordingFileBuilder

The `MockRecordingFileBuilder` class provides convenience methods to create mock engines:

```csharp
// Create a mock audio processing engine
var processingEngine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();

// Create a mock audio output engine
var outputEngine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
```

## Full Pipeline Example

Here's an example of using both mock engines together:

```csharp
[TestMethod]
public async Task TestFullAudioPipeline()
{
    // Arrange
    var processingEngine = MockRecordingFileBuilder.CreateMockAudioProcessingEngine();
    var outputEngine = await MockRecordingFileBuilder.CreateMockAudioOutputEngineAsync();
    
    var packet = CreateTestAudioPacket();
    
    // Act - Process audio
    var processedAudio = processingEngine.ProcessPacket(packet);
    
    // Convert to bytes for output
    var audioBytes = AudioHelpers.ConvertFloatToPcm16Bytes(processedAudio);
    
    // Write to output
    outputEngine.Start();
    await outputEngine.WriteAudioAsync(audioBytes);
    
    // Assert
    Assert.AreEqual(1, processingEngine.ProcessedPacketCount);
    Assert.AreEqual(1, outputEngine.WrittenFrames.Count);
    Assert.IsTrue(outputEngine.IsRunning);
    
    // Cleanup
    outputEngine.Stop();
    processingEngine.Dispose();
    outputEngine.Dispose();
}
```

## When to Use Mock Engines

Use mock audio engines when:

1. **Unit Testing**: Testing individual components that depend on audio processing or output
2. **CI/CD Pipelines**: Running tests in environments without audio hardware
3. **Verification**: Checking that audio operations are called correctly without needing to listen to output
4. **Performance Testing**: Measuring performance without the overhead of real audio processing
5. **Deterministic Behavior**: Need predictable test results without audio hardware variations

## When to Use Real Engines

Use real audio engines when:

1. **Integration Testing**: Testing actual audio output quality
2. **Manual Testing**: Verifying audible output is correct
3. **Hardware Testing**: Validating audio hardware compatibility
4. **Production Code**: All non-test scenarios

## Properties Available for Verification

### MockAudioProcessingEngine

- `IsInitialized` - Whether the engine has been initialized
- `ProcessedPacketCount` - Number of packets processed
- `IsDisposed` - Whether the engine has been disposed

### MockAudioOutputEngine

- `IsInitialized` - Whether the engine has been initialized
- `IsRunning` - Whether playback is active
- `IsDisposed` - Whether the engine has been disposed
- `WrittenFrames` - List of all written audio frames
- `TotalBytesWritten` - Total bytes written across all frames
- `CurrentVolume` - Current master volume setting
- `InitializeCallCount` - Number of times `InitializeAsync()` was called
- `StartCallCount` - Number of times `Start()` was called
- `StopCallCount` - Number of times `Stop()` was called
- `ClearBufferCallCount` - Number of times `ClearBuffer()` was called

## Complete Test Example

See `MockAudioEnginesTests.cs` for comprehensive examples of using both mock engines.

## Related Files

- `MockAudioProcessingEngine.cs` - Mock audio processing implementation
- `MockAudioOutputEngine.cs` - Mock audio output implementation
- `MockRecordingFileBuilder.cs` - Helper methods for creating mocks and test files
- `MockAudioEnginesTests.cs` - Example usage and comprehensive tests

## Notes

- Mock engines implement the same interfaces as real engines (`IAudioProcessingEngine`, `IAudioOutputEngine`)
- They can be used as drop-in replacements in dependency injection scenarios
- They are thread-safe for basic operations
- They are designed to be fast and deterministic for testing
