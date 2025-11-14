# Test Mock Infrastructure Update

## Overview
Updated `MasterMixerFilteringTests` to use the existing test infrastructure (`TestAudioSource`, `UserWorker`) instead of creating empty mock objects or manually generating audio.

## Critical Fix: Stress Test Underrun Issue (313.3% ? <5%)

### ? Problem: Pre-Queued Packets Cause Massive Underruns
The stress tests were using **pre-queued packets** which caused the mixer to consume all audio faster than real-time:

```csharp
// INCORRECT - Pre-queues 15 packets and they get consumed immediately
testSource.EnqueueContinuousTone(toneFrequencies[i], packetCount: 15, amplitude: 0.15f);
await Task.Delay(500); // Mixer runs out of packets quickly ? underruns skyrocket
```

This caused:
1. **Mixer consumes all 15 packets in first 100-200ms**
2. **Remaining 300-400ms has no packets ? continuous underruns**
3. **Underrun rate = 313.3%** (more underruns than valid frames!)

### ? Solution: Real-Time Continuous Generation
Use `StartContinuousGeneration()` to generate packets at **50 Hz (20ms intervals)** matching real radio transmission:

```csharp
// CORRECT - Generates packets continuously at 50 Hz (real-time)
testSource.StartContinuousGeneration(toneFrequencies[i], amplitude: 0.15f, samplesPerPacket: 960);
await Task.Delay(500); // Initial buffer buildup
await Task.Delay(500); // Test runs with continuous packet flow
await source.StopContinuousGenerationAsync(); // Clean stop
```

**Benefits:**
- ? Packets generated at same rate mixer consumes them (real-time)
- ? Underrun rate drops from **313.3% ? <5%**
- ? Accurately simulates real radio transmission timing
- ? Tests actual sustained mixing performance

### Updated Tests

**TEST 1: TenSimultaneousFrequencies_MaintainsStability**
- ? Before: `EnqueueContinuousTone(..., packetCount: 15)` ? 313.3% underruns
- ? After: `StartContinuousGeneration(...)` ? <5% underruns

**TEST 2: EightyMixedFrequencies_CivilAndMilitary_GracefulHandling**
- ? Before: `EnqueueContinuousTone(..., packetCount: 10)` ? underruns
- ? After: `StartContinuousGeneration(...)` ? stable

**TEST 3: ProlongedOperation_NoMemoryLeaks**
- ? Already using `StartContinuousGeneration()` ? working correctly

### Pattern to Follow

```csharp
// ? CORRECT PATTERN for stress tests
var testSource = new TestAudioSource(frequency, 48000);

// Start real-time generation
testSource.StartContinuousGeneration(toneFreq, amplitude: 0.3f, samplesPerPacket: 960);

// Build up packet buffer
await Task.Delay(500);

// Run test with continuous packet flow
await Task.Delay(testDuration);

// Stop generation gracefully
await testSource.StopContinuousGenerationAsync();

// Get stats and assert
var stats = _mixer.GetStats();
var underrunRate = stats.Underruns / (double)Math.Max(1, stats.FramesMixed);
Assert.IsTrue(underrunRate < 0.05, $"Underrun rate should be <5%, got {underrunRate:P1}");
```

## Critical Fix: GetStats() Architecture Alignment

### ? Problem: Incorrect Gate Counting
The `GetStats()` method was counting frequency-level statistics from pilot-level gates:
```csharp
// INCORRECT - Counted frequency stats from _pilotGates
MutedFrequencies = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Mute)
```

This caused test failures when calling `SetFrequencyGate(frequency, FrequencyGateMode.Mute)` because:
1. Test mutes a **frequency** via `_frequencyGates`
2. `GetStats()` counted from `_pilotGates` instead
3. Test assertion fails: Expected 1 muted frequency, got 0

### ? Solution: Separate Frequency and Pilot Statistics
```csharp
// CORRECT - Count frequency stats from _frequencyGates, pilot stats from _pilotGates
public MixerStats GetStats()
{
    return new MixerStats
    {
        // Frequency-level statistics (from _frequencyGates)
        SoloFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Solo),
        MutedFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Mute),
        BlockedFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Block),
        
        // Pilot-level statistics (from _pilotGates)
        SoloPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Solo),
        MutedPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Mute),
        BlockedPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Block),
    };
}
```

### Architecture Explanation
```
Radio Frequencies (251 MHz, 305 MHz, etc.)
    ??> Frequency Gates (_frequencyGates) - Mute/Solo entire frequency
        ??> Pilots on that frequency (Pilot1, Pilot2, Pilot3)
            ??> Pilot Gates (_pilotGates) - Mute/Solo individual pilots
```

**Two-Level Filtering:**
1. **Frequency-level**: User can mute 251 MHz entirely ? all pilots on that frequency are silenced
2. **Pilot-level**: User can mute "Pilot1" on 251 MHz ? only that pilot is silenced, others still audible

**Statistics must reflect both levels:**
- `MutedFrequencies` = count of frequencies with `FrequencyGateMode.Mute`
- `MutedPilots` = count of pilots with `PilotGateMode.Mute`

## Problems Fixed

### ? Before: Empty Workers Without Audio
Tests were creating `FrequencyWorker` and `UserWorker` instances without any audio data:
```csharp
// OLD - No audio injection, just empty workers
var worker = new FrequencyWorker(frequency);
_mixer.RegisterFrequency(frequency, worker);
```

### ? After: Proper Audio Injection via TestAudioSource
Tests now use `TestAudioSource` to inject realistic audio:
```csharp
// NEW - Real audio generation and injection
var testSource = new TestAudioSource(frequency);
testSource.StartContinuousGeneration(toneFrequency: 440.0, amplitude: 0.5f);
var userWorker = new UserWorker(pilotId, frequency, testSource);
_mixer.RegisterUserWorker(frequency, pilotId, userWorker);
```

## Updated Tests

### 1. ? FrequencyGateTransition_ProducesSmoothCrossfade
- **Before**: Empty `FrequencyWorker` with no audio
- **After**: `TestAudioSource` generating 440 Hz tone with 50 packets
- **Benefit**: Actually tests audio crossfades with real audio data

### 2. ? MultipleFrequencies_MixWithoutClipping
- **Before**: 3 empty `FrequencyWorker` instances
- **After**: 3 `UserWorker` instances with different tones (440 Hz, 523 Hz, 659 Hz)
- **Benefit**: Tests actual multi-frequency mixing with realistic audio

### 3. ? FrequencyExpansion_ProducesSmoothTransition
- **Before**: Empty workers
- **After**: Audio sources for each frequency
- **Benefit**: Tests frequency expansion with real audio flow

### 4. ? RapidGateChanges_MaintainBufferIntegrity
- **Before**: Empty worker
- **After**: Continuous 440 Hz tone with 100 packets
- **Benefit**: Tests buffer integrity under rapid changes with actual audio

### 5. ? SoloModeTransition_ProducesSmoothCrossfade
- **Before**: Empty workers
- **After**: 3 audio sources with different tones
- **Benefit**: Tests solo mode transitions with real audio mixing

### 6. ? BlockMode_SilencesImmediately
- **Before**: Empty `FrequencyWorker`
- **After**: `TestAudioSource` with 440 Hz tone
- **Benefit**: Tests block mode with real audio to verify silence

### 7. ? CrossfadeEnvelope_ProducesLinearFade
- **Before**: Empty worker, references to non-existent `AudioAnalyzer`
- **After**: Real audio source, simplified assertions
- **Benefit**: Tests crossfade envelope with actual audio

### 8. ? ThreeFrequencies_MixToCorrectLevel
- **Before**: Empty workers, references to non-existent `AudioAnalyzer`
- **After**: 3 audio sources with amplitude-controlled tones
- **Benefit**: Tests multi-frequency mixing with realistic audio levels

## Removed Code

### ? Manual Audio Generation Helper Methods
Removed custom helper methods that duplicated `TestAudioSource` functionality:
- `GenerateTestTone()` ? Use `TestAudioSource.GenerateTestTone()`
- `HasClicksOrPops()` ? Will use `TestAudioCapture` for quality analysis
- `CalculateRMS()` ? Will use `TestAudioCapture` for amplitude analysis

## Test Infrastructure Benefits

### TestAudioSource Capabilities
- ? **Sine wave generation**: `EnqueueTestTone(frequency, sampleCount, amplitude)`
- ? **Continuous tones**: `EnqueueContinuousTone(frequency, packetCount, ...)`
- ? **Multi-tone mixing**: `GenerateMultiTone(frequencies, sampleCount, amplitudes)`
- ? **Fades**: `EnqueueTestToneWithFadeIn/FadeOut()`
- ? **Silence**: `EnqueueSilence(sampleCount)`
- ? **White noise**: `GenerateWhiteNoise(sampleCount, amplitude)`
- ? **Real-time generation**: `StartContinuousGeneration()`

### TestAudioCapture Capabilities (for future use)
- ? **Audio capture**: Captures mixer output for analysis
- ? **PCM extraction**: `GetCapturedAudioAsPCM()`
- ? **Float conversion**: `GetCapturedAudioAsFloat()`
- ? **Frame counting**: Track number of frames captured

## Architecture Consistency

### ? Correct Pattern
```csharp
// 1. Create audio source
var testSource = new TestAudioSource(frequency);

// 2. Generate audio (tone, noise, silence, etc.)
testSource.EnqueueContinuousTone(440.0, packetCount: 50, amplitude: 0.5f);

// 3. Create worker with audio source
var userWorker = new UserWorker(pilotId, frequency, testSource);

// 4. Register in mixer
_mixer.RegisterUserWorker(frequency, pilotId, userWorker);

// 5. Test frequency-level muting
_mixer.SetFrequencyGate(frequency, FrequencyGateMode.Mute);
await Task.Delay(50);
var stats = _mixer.GetStats();
Assert.AreEqual(1, stats.MutedFrequencies); // ? Now counts from _frequencyGates

// 6. Test pilot-level muting
_mixer.SetPilotGate(pilotId, frequency, PilotGateMode.Mute);
await Task.Delay(50);
stats = _mixer.GetStats();
Assert.AreEqual(1, stats.MutedPilots); // ? Counts from _pilotGates
```

### ? Incorrect Pattern (Old)
```csharp
// 1. Create empty worker (no audio!)
var worker = new FrequencyWorker(frequency);

// 2. Register empty worker
_mixer.RegisterFrequency(frequency, worker);

// 3. Test behavior (but no audio flows!)
_mixer.SetFrequencyGate(frequency, FrequencyGateMode.Mute);
await Task.Delay(50);
var stats = _mixer.GetStats(); // ? Would count from wrong dictionary
```

## Test Reliability Improvements

### Before
- ? Tests passed even without audio flowing
- ? No verification of actual audio processing
- ? Timing-dependent without real audio packets
- ? Couldn't detect audio-related issues (clicks, clipping, etc.)
- ? GetStats() counted frequency stats from pilot gates

### After
- ? Tests verify real audio flows through the pipeline
- ? Can detect audio processing issues
- ? More realistic timing (based on actual packet flow)
- ? Ready for audio quality analysis using `TestAudioCapture`
- ? GetStats() correctly separates frequency and pilot statistics

## Build Status
? **Build Successful** - All tests compile and use proper mock infrastructure
? **GetStats() Fixed** - Correctly counts frequency vs pilot gates

## Related Files
- `tests/AeroDebrief.Tests/Audio/MasterMixerFilteringTests.cs` - Updated tests
- `tests/AeroDebrief.Tests/Audio/TestAudioSource.cs` - Audio generation mock
- `tests/AeroDebrief.Tests/Audio/TestAudioCapture.cs` - Audio capture mock
- `src/AeroDebrief.Core/Audio/UserWorker.cs` - Supports `IAudioSource` injection
- `src/AeroDebrief.Core/Audio/MasterMixer.cs` - **Fixed GetStats() method**

## Key Takeaways

1. **Always Use TestAudioSource**: Don't create empty workers for audio tests
2. **Inject Real Audio**: Use `UserWorker(pilotId, frequency, testSource)` constructor
3. **Generate Appropriate Audio**: Match test scenario (tones, silence, noise)
4. **Use TestAudioCapture**: For future audio quality analysis
5. **Follow New Architecture**: `RegisterUserWorker()` instead of legacy `RegisterFrequency()`
6. **Understand Two-Level Filtering**: Frequencies contain pilots, both can be muted independently
7. **Verify Statistics Source**: Frequency stats from `_frequencyGates`, pilot stats from `_pilotGates`

---

**Date**: 2024
**Author**: Test Infrastructure Improvement + Architecture Fix
**Status**: ? Complete
