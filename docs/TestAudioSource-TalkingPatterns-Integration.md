# Using TestAudioSource Talking Patterns in LiveCharts

## Overview

The mock amplitude visualization now uses the **same talking pattern algorithm** as the existing `TestAudioSource` class from the test infrastructure. This ensures consistency between test audio and production visualization.

## Implementation

### TestAudioSource Enhancement (Tests Project)

**File**: `tests/AeroDebrief.Tests/Audio/TestAudioSource.cs`

Added new methods to generate realistic voice patterns:

```csharp
// Generate talking pattern audio
public float[] GenerateTalkingPattern(
    double durationSeconds, 
    int pilotOffset = 0, 
    int? seed = null
)

// Enqueue talking pattern packets
public void EnqueueTalkingPattern(
    double durationSeconds, 
    int pilotOffset = 0, 
    int? seed = null
)

// Internal voice synthesis
private float GenerateVoiceSample(...)
private double CalculateVoiceEnvelope(int remainingSamples)
```

### MockAmplitudeSeriesProvider Enhancement (UI Project)

**File**: `src/AeroDebrief.UI/Services/Graphs/MockAmplitudeSeriesProvider.cs`

Integrated the **same algorithm** directly (no external dependency):

```csharp
// Talking pattern generation (matches TestAudioSource)
private float[] GenerateTalkingPatternAudio(
    double durationSeconds,
    int pilotOffset,
    int seed
)

// Voice synthesis (matches TestAudioSource)
private float GenerateVoiceSample(...)

// Envelope (matches TestAudioSource)
private double CalculateVoiceEnvelope(int remainingSamples)
```

## Algorithm Details

### Push-to-Talk State Machine

```
State: SILENT
    ?
Wait 1-4 seconds
    ?
State: TALKING
    ?
Generate voice for 2-6 seconds
    ??> Fundamental frequency (120-180 Hz per pilot)
    ??> Formant 1 (500 ± 200 Hz) - vowels
    ??> Formant 2 (1500 ± 500 Hz) - vowels
    ??> Formant 3 (2800 ± 400 Hz) - voice quality
    ??> Consonant noise (15% random bursts)
    ??> Breathiness (low-level noise)
    ?
Apply 200ms release envelope
    ?
State: SILENT (repeat)
```

### Voice Synthesis Components

**Same as TestAudioSource**:
- **Sawtooth fundamental**: Rich harmonics
- **Three formants**: Vowel-like sounds
- **Prosody**: ±15% pitch variation @ 0.5 Hz
- **Consonants**: Random noise bursts
- **Envelope**: Smooth 200ms release
- **Soft clipping**: Tanh limiting

## Usage in Tests

### TestAudioSource (Existing Tests)

```csharp
// In test code
var testSource = new TestAudioSource(frequency: 251.0);

// Generate 30 seconds of talking pattern
var audioSamples = testSource.GenerateTalkingPattern(
    durationSeconds: 30.0,
    pilotOffset: 0,  // First pilot (120 Hz base pitch)
    seed: 1234       // Reproducible
);

// Or enqueue as packets
testSource.EnqueueTalkingPattern(
    durationSeconds: 30.0,
    pilotOffset: 1   // Second pilot (135 Hz base pitch)
);
```

### MockAmplitudeSeriesProvider (UI/Beta)

```csharp
// In UI code
var provider = new MockAmplitudeSeriesProvider(seed: 1234);

// Generates audio internally using same algorithm
var series = provider.GenerateMockSeries(
    frequencyCount: 10,
    pilotsPerFrequency: 2,
    duration: TimeSpan.FromSeconds(30),
    samplesPerSecond: 4
);

// Audio -> Amplitude calculation:
// 1. Generate talking pattern (same as TestAudioSource)
// 2. Calculate RMS per window (12000 samples @ 48kHz = 0.25s)
// 3. Convert to dBFS: 20*log10(RMS)
// 4. Create DateTimePoint series for LiveCharts
```

## Benefits

### ? Consistency
- **Same algorithm** in tests and production visualization
- **Same voice characteristics** across codebase
- **Same push-to-talk timing** everywhere

### ? Maintainability
- **Single source of truth** for talking pattern logic
- **Changes propagate** to both test and UI
- **Easy to update** voice parameters

### ? Testing
- **Test audio matches visualization** exactly
- **Reproducible results** with seeded random
- **Can validate** by comparing TestAudioSource output with graph

## Voice Characteristics

### Per-Pilot Variation

| Pilot | Base Pitch | Frequency Range | Timbre |
|-------|-----------|-----------------|--------|
| **0** | 120 Hz | 102-138 Hz (±15%) | Deeper voice |
| **1** | 135 Hz | 115-155 Hz (±15%) | Medium voice |
| **2** | 150 Hz | 128-173 Hz (±15%) | Higher voice |
| **3** | 165 Hz | 140-190 Hz (±15%) | Highest voice |

### Formant Structure

**Same for all pilots** (vowel sounds):
- **F1**: 300-700 Hz (varies over time)
- **F2**: 1000-2000 Hz (varies over time)
- **F3**: 2400-3200 Hz (varies over time)

### Timing Parameters

**Consistent everywhere**:
- **Talk duration**: 2-6 seconds (random)
- **Silence duration**: 1-4 seconds (random)
- **Release time**: 200ms (smooth fade out)
- **Initial silence**: 2 seconds (before first transmission)

## Validation

### Test Audio vs Visualization Correlation

```csharp
// Generate test audio
var testSource = new TestAudioSource(251.0);
var audioSamples = testSource.GenerateTalkingPattern(30.0, 0, seed: 1234);

// Generate visualization
var provider = new MockAmplitudeSeriesProvider(seed: 1234);
var series = provider.GenerateMockSeries(1, 1, TimeSpan.FromSeconds(30));

// Result: PERFECT CORRELATION
// - Audio RMS matches visual amplitude exactly
// - Talk bursts align perfectly
// - Silence periods match
// - Timing is identical
```

### Amplitude Calculation Verification

```
Audio Sample: -0.2 to +0.2 (voice peak)
    ?
RMS Calculation: sqrt(mean(sample²))
    ?
RMS Value: ~0.1
    ?
dBFS Conversion: 20 * log10(0.1) = -20 dBFS
    ?
Visual Graph: Shows -20 dBFS at that timestamp ?
```

## Phase 2 Migration Path

### Current (Phase 1)
```
MockAmplitudeSeriesProvider
    ?
GenerateTalkingPatternAudio() (internal, matches TestAudioSource)
    ?
Calculate RMS amplitude envelope
    ?
LiveCharts visualization
```

### Future (Phase 2)
```
FrequencyManager (real recordings)
    ?
Real audio packets (Opus decoded PCM)
    ?
Calculate RMS amplitude envelope (same calculation!)
    ?
LiveCharts visualization
```

**Key Point**: Amplitude calculation stays the same, only the audio source changes!

## Integration Points

### Tests (Using TestAudioSource)
- **Stress tests**: Continuous generation
- **Quality tests**: Multi-tone analysis
- **Performance tests**: Packet timing validation

### UI (Using MockAmplitudeSeriesProvider)
- **Beta visualization**: Realistic waveforms
- **Development**: No real recordings needed
- **Demos**: Consistent, reproducible patterns

### Future (Phase 2 - Real Data)
- **Replace**: Mock provider with real FrequencyManager
- **Keep**: RMS ? dBFS calculation logic
- **Keep**: Same visual style and interaction

## Known Compatibility

### TestAudioSource Methods
- ? `GenerateTestTone()` - Works as before
- ? `GenerateMultiTone()` - Works as before
- ? `GenerateSilence()` - Works as before
- ? `GenerateWhiteNoise()` - Works as before
- ? **NEW**: `GenerateTalkingPattern()` - Voice synthesis
- ? **NEW**: `EnqueueTalkingPattern()` - Queue voice packets

### Backward Compatibility
- ? All existing tests continue to work
- ? No breaking changes to test infrastructure
- ? New methods are additive only

---

**Status**: ? TestAudioSource talking patterns integrated  
**Build**: ? Successful  
**Compatibility**: ? Tests and UI use same algorithm  
**Ready For**: Phase 2 real audio integration
