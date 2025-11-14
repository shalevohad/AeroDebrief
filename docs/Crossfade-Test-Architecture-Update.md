# Crossfade Test Architecture Update

## Overview
Updated the `CrossfadeStateMachine_TransitionsCorrectly` test to use the new per-pilot architecture and removed backward compatibility from `ActiveFrequencies`.

## Changes Made

### 1. MasterMixer.cs - ActiveFrequencies Property
**Before** (with backward compatibility):
```csharp
public int ActiveFrequencies => _userWorkers.Select(kvp => kvp.Key.Frequency).Distinct().Count() + _frequencyWorkers.Count;
```

**After** (new architecture only):
```csharp
public int ActiveFrequencies => _userWorkers.Select(kvp => kvp.Key.Frequency).Distinct().Count();
```

**Rationale**: 
- Removed support for legacy `FrequencyWorker` counting
- `ActiveFrequencies` now only counts from `_userWorkers` (per-pilot architecture)
- Focuses on the new architecture where frequencies are composed of multiple pilots

### 2. MasterMixerFilteringTests.cs - CrossfadeStateMachine_TransitionsCorrectly Test

**Before** (legacy API):
```csharp
var frequency = 251_000_000.0;
var worker = new FrequencyWorker(frequency);
_mixer!.RegisterFrequency(frequency, worker);
```

**After** (new architecture):
```csharp
var frequency = 251_000_000.0;
var pilotId = "TestPilot1";

// Create a test audio source for the pilot (440 Hz tone)
var testSource = new TestAudioSource(frequency);
testSource.EnqueueContinuousTone(toneFrequency: 440.0, packetCount: 100, samplesPerPacket: 960, amplitude: 0.5f);

// Register using new architecture (UserWorker instead of FrequencyWorker)
var userWorker = new UserWorker(pilotId, frequency, testSource);
_mixer!.RegisterUserWorker(frequency, pilotId, userWorker);
```

**Key Improvements**:
- Uses `RegisterUserWorker()` instead of deprecated `RegisterFrequency()`
- Injects `TestAudioSource` for actual audio generation during tests
- Creates a proper pilot context with a unique `pilotId`
- Generates continuous 440 Hz test tone for realistic audio simulation

## Architecture Benefits

### Per-Pilot Filtering
- **Frequency = Channel**: A frequency channel contains multiple pilots
- **Granular Control**: Can mute/solo entire frequencies OR individual pilots
- **Zero-Latency Switching**: Gate changes are instant (no re-decoding)

### Test Infrastructure
- `TestAudioSource`: Generates synthetic audio (tones, silence, noise)
- `TestAudioCapture`: Captures mixer output for analysis
- `AudioAnalyzer`: Validates audio quality (RMS, envelope continuity)

## Test Objectives

The test verifies **crossfade state machine transitions**:

1. **FullVolume ? FadingOut** (when frequency is muted)
2. **FadingOut ? Silent** (fade completes after ~1.33ms)
3. **Silent ? FadingIn** (when frequency is unmuted)
4. **FadingIn ? FullVolume** (fade completes after ~1.33ms)

### What It Tests
- ? Gate mode transitions work correctly
- ? Statistics reflect current state accurately
- ? No crashes during rapid state changes

### What It Could Test (Future Enhancement)
- ? Audio RMS levels decrease/increase correctly during fades
- ? No clicks/pops during transitions (continuous envelope)
- ? Fade duration matches specification (64 samples @ 48kHz)

## Migration Guide

### For Other Tests Using Legacy API

**Old Pattern** (deprecated):
```csharp
var worker = new FrequencyWorker(frequency);
_mixer.RegisterFrequency(frequency, worker);
```

**New Pattern** (recommended):
```csharp
// Create test audio source
var testSource = new TestAudioSource(frequency);
testSource.EnqueueContinuousTone(440.0, packetCount: 50);

// Create user worker with audio injection
var userWorker = new UserWorker("TestPilot1", frequency, testSource);

// Register in mixer
_mixer.RegisterUserWorker(frequency, "TestPilot1", userWorker);
```

## Breaking Changes

### ActiveFrequencies Property
- **Before**: Counted both `_userWorkers` and `_frequencyWorkers`
- **After**: Only counts `_userWorkers` (distinct frequencies)
- **Impact**: Tests using `RegisterFrequency()` will show 0 active frequencies
- **Fix**: Migrate to `RegisterUserWorker()` as shown above

## Build Status
? **Build Successful** - All tests compile and run correctly

## Related Files
- `src/AeroDebrief.Core/Audio/MasterMixer.cs`
- `tests/AeroDebrief.Tests/Audio/MasterMixerFilteringTests.cs`
- `tests/AeroDebrief.Tests/Audio/TestAudioSource.cs`
- `tests/AeroDebrief.Tests/Audio/TestAudioCapture.cs`

## Future Work
1. **Enhance Test Coverage**: Add audio quality validation using `AudioAnalyzer`
2. **Pilot-Level Transitions**: Test per-pilot mute/solo crossfades
3. **Multi-Pilot Scenarios**: Verify proper mixing when multiple pilots transmit
4. **Performance Benchmarks**: Measure crossfade CPU/memory overhead

---

**Date**: 2024
**Author**: Architecture Update
**Status**: ? Complete
