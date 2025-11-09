# Test Fix Summary: PlaybackPipeline_ValidatesAudioQuality

## Problem
The test `PlaybackPipeline_ValidatesAudioQuality` was failing with:
```
Assert.IsTrue failed. RMS should indicate signal presence, got 0.0000
```

The test captured audio was completely silent (RMS = 0), indicating no audio was being written to the `TestAudioCapture`.

## Root Cause
The test was creating its own `TestAudioCapture` and `MasterMixer`, but the `FilePlaybackPipeline.OpenAsync()` method **creates its own** `AudioOutputEngine` internally. This meant:
1. Test creates `TestAudioCapture` ?
2. Test creates `MasterMixer` with `TestAudioCapture` ? (unused)
3. Pipeline creates its own `AudioOutputEngine` ? (used for playback)
4. Pipeline creates its own `MasterMixer` with `AudioOutputEngine` ? (used for mixing)

Result: Audio went to the production `AudioOutputEngine` (WASAPI), not the test's `TestAudioCapture`.

## Solution
Modified `FilePlaybackPipeline` to support **dependency injection** of audio output engines for testing:

### Changes Made

#### 1. Added Constructor Overload (`FilePlaybackPipeline.cs`)
```csharp
/// <summary>
/// Creates a new FilePlaybackPipeline with a custom audio output engine (for testing).
/// This allows test code to inject TestAudioCapture instead of using production AudioOutputEngine.
/// </summary>
public FilePlaybackPipeline(FilePacketSource packetSource, IAudioOutputEngine audioOutputEngine)
{
    _packetSource = packetSource ?? throw new ArgumentNullException(nameof(packetSource));
    _customAudioOutput = audioOutputEngine ?? throw new ArgumentNullException(nameof(audioOutputEngine));
    Logger.Info("FilePlaybackPipeline created with custom audio output engine (test mode)");
}
```

#### 2. Modified `OpenAsync()` to Use Custom Audio Output
```csharp
// Step 3: Initialize AudioOutputEngine
IAudioOutputEngine audioEngine;
if (_customAudioOutput != null)
{
    // Use custom audio output (test mode)
    Logger.Info("Step 3: Using custom audio output engine (test mode)...");
    audioEngine = _customAudioOutput;
    await audioEngine.InitializeAsync();
    Logger.Info("Custom audio output engine initialized");
}
else
{
    // Use production audio output
    Logger.Info("Step 3: Initializing AudioOutputEngine...");
    _audioOutput = new AudioOutputEngine();
    await _audioOutput.InitializeAsync();
    audioEngine = _audioOutput;
    Logger.Info("AudioOutputEngine initialized");
}

// Step 4: Initialize MasterMixer with AudioOutputEngine
_masterMixer = new MasterMixer(audioEngine);
```

#### 3. Updated `PlayAsync()` and `StopAsync()` to Use Correct Audio Output
```csharp
// Get the correct audio output (production or test)
var audioOutput = _customAudioOutput ?? _audioOutput;
if (audioOutput == null)
{
    throw new InvalidOperationException("Audio output not initialized. Call OpenAsync first.");
}
```

#### 4. Updated Test to Use New Constructor
```csharp
// Setup FilePlaybackPipeline with TestAudioCapture
using var pipeline = new FilePlaybackPipeline(packetSource, _audioCapture!);
await pipeline.OpenAsync();
```

#### 5. Removed Unnecessary Test Setup
```csharp
[TestInitialize]
public async Task Setup()
{
    // Initialize audio capture for playback quality tests
    _audioCapture = new TestAudioCapture();
    await _audioCapture.InitializeAsync();
    
    // REMOVED: _mixer = new MasterMixer(_audioCapture); // Pipeline creates its own
    Logger.Info("End-to-end test setup complete");
}
```

## Results

### Primary Issue: FIXED ?
The test now successfully captures audio:
- RMS > 0 (audio signal present)
- Valid range (samples within -1.0 to 1.0)

### Secondary Issue Revealed: Audio Clipping ??
The fix revealed a new issue:
```
Assert.IsFalse failed. Audio should not clip
```
This indicates the audio processing pipeline is producing samples that exceed the valid range, which needs investigation. This is a **separate issue** from the original "no audio captured" problem.

Possible causes of clipping (to investigate):
1. Audio normalization in `AudioProcessingEngine` may be too aggressive
2. Master mixer gain settings may be too high
3. Multiple frequency mixing may cause amplitude summation beyond valid range

**Note:** This is actually **good news** - the test is now working correctly and detecting a real audio quality issue that was previously hidden by the wiring problem.

## Benefits
1. **Clean Separation**: Production code uses `AudioOutputEngine`, tests use `TestAudioCapture`
2. **No Breaking Changes**: Existing production code continues to work with default constructor
3. **Testability**: Tests can now properly validate audio output quality
4. **Best Practice**: Follows dependency injection pattern for better testability
5. **Issue Detection**: Test now detects audio quality issues that were previously hidden

## Verification
After this fix, the test:
1. Creates `TestAudioCapture` ?
2. Injects it into `FilePlaybackPipeline` ?
3. Pipeline uses injected `TestAudioCapture` for mixing ?
4. Audio flows through `TestAudioCapture` ?
5. Test captures audio successfully ?
6. Test detects clipping issue (needs separate fix) ??

## Related Files
- `src/AeroDebrief.Core/Playback/FilePlaybackPipeline.cs` - Added constructor overload and modified OpenAsync
- `tests/AeroDebrief.Tests/Integration/EndToEndPipelineTests.cs` - Updated test to use new constructor
- `tests/AeroDebrief.Tests/Audio/TestAudioCapture.cs` - Test implementation of IAudioOutputEngine

## Next Steps
Investigate and fix the audio clipping issue detected by the now-working test. This is a separate audio quality problem in the processing pipeline.
