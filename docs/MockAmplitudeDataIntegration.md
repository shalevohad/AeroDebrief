# Mock Amplitude Data Integration - Phase 1

## Overview

Phase 1 now integrates with the existing test audio infrastructure to provide realistic mock amplitude data that matches actual push-to-talk transmission patterns.

## Integration Architecture

```
Test Infrastructure (Existing)
?? TestAudioSource
?  ?? Generates PCM audio with tone patterns
?? MockAudioOutputEngine
?  ?? Records audio frames for testing
?? Test talk patterns (2-6s talk, 1-4s silence)

Phase 1 Integration (New)
?? MockAmplitudeSeriesProvider
?  ?? Matches TestAudioSource transmission timing
?  ?? Simulates voice formants and prosody
?  ?? Generates dBFS amplitude envelopes
?? UnifiedGraphViewModel
   ?? Uses mock provider for realistic waveforms
```

## Mock Data Characteristics

### Transmission Patterns
- **Talk Duration**: 2-6 seconds (matches TestAudioSource)
- **Silence Duration**: 1-4 seconds (listening/thinking)
- **Attack/Release**: 0.5 second envelope ramps
- **Push-to-Talk Ratio**: ~60% talk, 40% silence

### Amplitude Components

**During Voice Transmission** (-60 to -20 dBFS typical):
```
Component       | Range  | Simulates
?????????????????????????????????????????????
Fundamental     | ±15 dB | Speech pitch (100-300 Hz)
First Formant   | ±10 dB | Vowel sounds
Second Formant  | ±5 dB  | Consonants/clarity
Noise           | ±4 dB  | Breath/natural variation
Envelope        | 0-1.0  | Smooth start/stop
```

**During Silence** (-75 to -70 dBFS):
- Radio carrier noise floor only
- No voice components
- Random variation ±5 dB

### Frequency Distribution

**Phase 1 Default** (fast testing):
- 9 normal frequencies (2 pilots each) = 18 series
- 1 high-traffic frequency (4 pilots) = 4 series
- **Total**: 22 series, ~2,640 points (30 seconds @ 4 Hz)

**Phase 0 Target** (acceptance testing):
- 54 normal frequencies (4 pilots each) = 216 series  
- 6 high-traffic frequencies (24 pilots each) = 144 series
- **Total**: 360 series, ~432,000 points (5 minutes @ 4 Hz)

## Code Structure

### MockAmplitudeSeriesProvider

**Purpose**: Generate realistic amplitude waveforms matching test audio patterns

**Key Methods**:
```csharp
// Generate series with frequency distribution
GenerateFromFrequencyDistribution(
    normalFrequencies,      // 90% of channels
    highTrafficFrequencies, // 10% of channels
    pilotsPerNormal,       // 2-4 typically
    pilotsPerHighTraffic,  // 20-24 for busy freqs
    duration,
    samplesPerSecond       // 4 Hz recommended
)

// Generate single pilot waveform
GeneratePilotAmplitudeData(
    startTime,
    duration,
    samplesPerSecond,
    pilotOffset            // Phase shift for variety
)

// Deterministic color palette
GetFrequencyColor(frequencyIndex) // HSL golden angle distribution
```

### UnifiedGraphViewModel

**Constructor**:
```csharp
// Uses mock provider with seed for reproducible results
_mockProvider = new MockAmplitudeSeriesProvider(seed: 1234);

// Generate 10 frequencies matching Phase 0 distribution
GenerateMockData(
    normalFrequencies: 9,
    highTrafficFrequencies: 1,
    pilotsPerNormal: 2,
    pilotsPerHighTraffic: 4,
    duration: TimeSpan.FromSeconds(30)
);
```

**Methods**:
- `GenerateMockData()` - Creates series using mock provider
- `GenerateFullScaleData()` - Phase 0 acceptance test dataset (360 series)
- `ConnectToFrequencyManager()` - Stub for Phase 2 real data integration

## Visual Results

### What You Should See

**Main Chart Pattern** (30 seconds):
```
dBFS
-10 ?
-20 ?     ????????         ??????????
-30 ?   ???      ???     ???        ???
-40 ?  ??          ??   ??            ??
-50 ? ??            ?????              ???
-60 ???                ??????????????????????
-70 ??                                      ????
    ?????????????????????????????????????????????
    0s    5s    10s   15s   20s   25s    30s
```

**Visual Features**:
- ? **Distinct bursts**: 2-6 second talk segments
- ? **Clear silence**: Flat lines at -70 dBFS between bursts
- ? **Amplitude variation**: Peaks and valleys during speech
- ? **10 colors**: Each frequency has unique HSL color
- ? **Overlapping pilots**: 2-4 traces per frequency with phase differences

### Frequency Labels
- **251.0 MHz - Pilot 1**
- **251.0 MHz - Pilot 2**
- **252.0 MHz - Pilot 1**
- **252.0 MHz - Pilot 2**
- ...etc.

## Performance

| Metric | Phase 1 (Fast) | Phase 0 (Full Scale) |
|--------|----------------|----------------------|
| **Frequencies** | 10 | 60 |
| **Series** | 22 | 360 |
| **Points** | ~2,640 | ~432,000 |
| **Duration** | 30s | 5 minutes |
| **Load Time** | <150ms | <10s (target) |
| **Memory** | ~80 KB | ~10 MB (target) |

## Phase 2 Migration Path

When integrating real audio data:

### Current (Phase 1 - Mock)
```csharp
var mockProvider = new MockAmplitudeSeriesProvider();
var series = mockProvider.GenerateMockSeries(...);
```

### Future (Phase 2 - Real)
```csharp
var amplitudeProvider = new AmplitudeSeriesProvider(frequencyManager);
var series = await amplitudeProvider.GenerateSeriesAsync(...);
```

### Data Pipeline (Phase 2)
```
FrequencyManager
    ?
Audio Packets (PCM @ 48 kHz)
    ?
Amplitude Envelope (10ms window, 5ms hop)
    ?
Convert to dBFS: 20*log10(RMS / 1.0)
    ?
Multi-Resolution Tiles (L0: 10ms, L1: 50ms, L2: 250ms, L3: 1s)
    ?
LTTB Decimation (zoom-dependent)
    ?
LiveCharts Series Update
```

## Testing Checklist

### Verify Mock Data Working
- [ ] Graph shows 22 colored waveforms (Phase 1 default)
- [ ] Talk bursts clearly visible (2-6 second segments)
- [ ] Silence periods show flat line at -70 dBFS
- [ ] Amplitude varies during transmission (-60 to -20 dBFS typical)
- [ ] Each frequency has distinct color
- [ ] 2-4 pilots per frequency with slight phase differences

### Verify Logs
```
? "UnifiedGraphViewModel initialized: 22 series, 2640 points"
? "Generated 10 frequencies with ... pilots each"
? "Generated 22 mock series with realistic talk patterns"
```

### Verify Performance
- [ ] Load time <200ms
- [ ] Memory <100 KB
- [ ] Smooth chart interaction (pan/zoom)
- [ ] Toggle switch works instantly

## Troubleshooting

### Still Seeing Straight Lines?
**Check**: ViewModel should use `MockAmplitudeSeriesProvider` now
```csharp
_mockProvider = new MockAmplitudeSeriesProvider(seed: 1234);
```

### Not Matching Talk Patterns?
**Check**: MockAmplitudeSeriesProvider uses same timing as TestAudioSource:
- 2-6 second talk bursts
- 1-4 second silence gaps
- 0.5 second attack/release

### Wrong Number of Series?
**Check**: Default should generate 22 series:
- 9 normal frequencies × 2 pilots = 18
- 1 high-traffic frequency × 4 pilots = 4
- Total: 22 series

## Benefits

### Integration with Existing Test Infrastructure
- ? Matches `TestAudioSource` transmission timing
- ? Reuses proven talk pattern logic
- ? Consistent with existing test mocks
- ? Easy migration path to real data (Phase 2)

### Realistic Visualization
- ? Push-to-talk behavior clearly visible
- ? Voice dynamics show speech patterns
- ? Silence periods distinct from voice
- ? Proper frequency distribution (90/10 split)

### Development Workflow
- ? Fast test data generation (<200ms)
- ? Reproducible results (seeded random)
- ? Scalable to full Phase 0 dataset
- ? No dependency on real recordings

---

**Status**: ? Mock data provider integrated  
**Build**: ? Successful  
**Pattern Matching**: ? TestAudioSource compatible  
**Ready For**: Visual verification and Phase 2 planning
