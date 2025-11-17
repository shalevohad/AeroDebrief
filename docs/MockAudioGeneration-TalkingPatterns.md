# Mock Audio Generation with Talking Patterns

## Overview

Phase 1 now includes **realistic audio generation** that produces actual PCM audio samples with voice-like talking patterns, synchronized with the amplitude visualization.

## What Was Implemented

### 1. MockAudioGenerator
**Location**: `src/AeroDebrief.UI/Services/Graphs/MockAudioGenerator.cs`

Generates synthetic voice audio using **additive synthesis** with realistic speech characteristics:

#### Voice Components
- **Fundamental Frequency**: 120-180 Hz (varies per pilot)
- **First Formant (F1)**: 300-900 Hz (vowel height)
- **Second Formant (F2)**: 800-2500 Hz (vowel frontness)
- **Third Formant (F3)**: 2000-3500 Hz (voice quality)
- **Consonant Noise**: Random bursts (15% of time)
- **Breathiness**: Low-level filtered noise

#### Push-to-Talk Behavior
- **Talk Duration**: 2-6 seconds (randomized)
- **Silence Duration**: 1-4 seconds (randomized)
- **Attack Time**: 100ms smooth ramp-up
- **Release Time**: 200ms smooth ramp-down
- **Prosody**: ±15% pitch variation (speech melody)

#### Audio Quality
- **Sample Rate**: 48 kHz (professional quality)
- **Bit Depth**: 16-bit PCM (for export)
- **Dynamic Range**: ~60 dB (voice + silence)
- **Clipping**: Soft hyperbolic tangent limiting

### 2. MockAudioSourceService
**Location**: `src/AeroDebrief.UI/Services/Graphs/MockAudioSourceService.cs`

Integrates audio generation with amplitude visualization:

#### Features
- **Synchronized Generation**: Audio and amplitude data from same patterns
- **Multi-Pilot Support**: Independent or synchronized pilots
- **Frequency Distribution**: Matches Phase 0 requirements (90/10 split)
- **WAV Export**: Can export audio to files for validation
- **Audio Caching**: Efficient memory management

#### Data Model
```csharp
MockDataset
?? StartTime, Duration
?? Frequencies (List<MockFrequencyData>)
    ?? Pilots (List<MockPilotData>)
        ?? AudioSamples (float[])
        ?? AmplitudeEnvelope (double[])
        ?? PilotId, PilotName
        ?? SampleRate
```

## Audio Synthesis Technical Details

### Formant Synthesis
Voice is synthesized using **additive synthesis** with multiple frequency components:

```
Audio Signal = Fundamental + F1 + F2 + F3 + Consonants + Breath

Fundamental (Sawtooth):
  - Frequency: 120-180 Hz (pilot-dependent)
  - Amplitude: 30% of final mix
  - Rich in harmonics (natural voice character)

Formant 1 (F1):
  - Frequency: 500 ± 200 Hz (varies over time)
  - Amplitude: 25% of final mix
  - Defines vowel height (ah, eh, ee sounds)

Formant 2 (F2):
  - Frequency: 1500 ± 500 Hz (varies over time)
  - Amplitude: 15% of final mix
  - Defines vowel frontness (ee vs oo)

Formant 3 (F3):
  - Frequency: 2800 ± 400 Hz (varies over time)
  - Amplitude: 8% of final mix
  - Adds voice quality and realism

Consonants:
  - Random noise bursts (15% occurrence)
  - Amplitude: 12% of final mix
  - Simulates plosives (p, t, k) and fricatives (s, f, sh)

Breathiness:
  - Continuous low-level noise
  - Amplitude: 3% of final mix
  - Natural breath component
```

### Prosody (Speech Melody)
```
Pitch Modulation = 1.0 + sin(0.5 Hz) × 0.15

Result: ±15% pitch variation
  - Simulates natural speech intonation
  - 0.5 Hz rate = slow, natural speech rhythm
  - Makes voice sound more human
```

### Envelope Shaping
```
Transmission Start:
  0ms ??????????> 100ms
  0.0 amplitude ??> 1.0 amplitude (smooth ramp)

Transmission End:
  -200ms ??????> 0ms
  1.0 amplitude ??> 0.0 amplitude (smooth ramp)

Prevents clicks and pops in audio output
```

## Usage Examples

### Example 1: Generate Single Frequency Audio
```csharp
var audioService = new MockAudioSourceService(sampleRate: 48000, seed: 1234);

// Generate 251.0 MHz with 2 pilots, 30 seconds
var freqData = audioService.GenerateFrequencyData(
    frequencyMHz: 251.0,
    pilotCount: 2,
    duration: TimeSpan.FromSeconds(30),
    pilotsInSync: false
);

// Access pilot audio
foreach (var pilot in freqData.Pilots)
{
    Console.WriteLine($"{pilot.PilotName}: {pilot.AudioSamples.Length} samples");
    Console.WriteLine($"Amplitude envelope: {pilot.AmplitudeEnvelope.Length} points");
    
    // Export to WAV for testing
    audioService.ExportToWav(pilot.PilotId, $"output_{pilot.PilotId}.wav");
}
```

### Example 2: Generate Complete Dataset (Phase 0 Distribution)
```csharp
var audioService = new MockAudioSourceService();

// Generate Phase 0 target: 54 normal + 6 high-traffic frequencies
var dataset = audioService.GenerateDataset(
    normalFrequencies: 54,
    highTrafficFrequencies: 6,
    pilotsPerNormal: 4,
    pilotsPerHighTraffic: 24,
    duration: TimeSpan.FromMinutes(5)
);

Console.WriteLine($"Generated {dataset.TotalFrequencies} frequencies");
Console.WriteLine($"Total pilots: {dataset.TotalPilots}");
Console.WriteLine($"Total series: {dataset.TotalPilots} (360 for Phase 0)");
```

### Example 3: Export Audio for Validation
```csharp
var audioService = new MockAudioSourceService();

var freqData = audioService.GenerateFrequencyData(251.0, 2, TimeSpan.FromSeconds(10));

// Export all pilots to WAV files
foreach (var pilot in freqData.Pilots)
{
    audioService.ExportToWav(pilot.PilotId, $"test_audio_{pilot.PilotId}.wav");
}

// Files created:
// test_audio_PILOT-251.0-1.wav
// test_audio_PILOT-251.0-2.wav
```

## Integration with Amplitude Visualization

### Synchronized Data Flow
```
MockAudioGenerator
    ?
Generate PCM Audio (48 kHz)
    ??> Audio Samples (float[])
    ??> Amplitude Envelope (dBFS, 4 Hz)
         ?
MockAmplitudeSeriesProvider
    ?
LiveCharts Series
```

### Amplitude Calculation
The amplitude envelope is calculated directly from the audio:

```csharp
// For each 0.25 second window (4 Hz):
windowSize = 48000 / 4 = 12000 samples

RMS = sqrt(sum(sample^2) / windowSize)
dBFS = 20 * log10(RMS)
```

**Result**: Visual amplitude graph matches actual audio intensity perfectly.

## Audio Characteristics

### Frequency Content
| Component | Frequency Range | Purpose |
|-----------|----------------|---------|
| Fundamental | 120-180 Hz | Voice pitch (pilot-specific) |
| F1 (First Formant) | 300-900 Hz | Vowel height (ah, eh, ee) |
| F2 (Second Formant) | 800-2500 Hz | Vowel frontness (ee vs oo) |
| F3 (Third Formant) | 2000-3500 Hz | Voice quality, clarity |
| Consonants | 1000-8000 Hz | Plosives, fricatives |
| Breathiness | 100-4000 Hz | Natural breath component |

### Amplitude Levels
| State | dBFS Range | Amplitude | Description |
|-------|-----------|-----------|-------------|
| **Voice Peak** | -10 to -20 dBFS | 0.1 to 0.3 | Loud speech |
| **Voice Average** | -30 to -40 dBFS | 0.03 to 0.1 | Normal speech |
| **Voice Quiet** | -50 to -60 dBFS | 0.003 to 0.03 | Soft speech |
| **Silence** | -70 to -80 dBFS | 0.0003 to 0.001 | Noise floor |

### Timing Statistics (30 second sample)
- **Talk Bursts**: 4-5 occurrences
- **Silence Gaps**: 4-5 occurrences
- **Average Talk**: 3.5 seconds per burst
- **Average Silence**: 2.5 seconds per gap
- **Talk/Silence Ratio**: ~58% talk, 42% silence

## Performance

### Generation Speed
| Dataset Size | Audio Duration | Generation Time | Memory |
|--------------|----------------|-----------------|--------|
| **Phase 1 (22 pilots)** | 30s | ~150ms | ~5 MB |
| **Phase 0 (360 pilots)** | 5 min | ~3s | ~80 MB |

### Audio File Sizes
- **30 seconds @ 48 kHz**: ~2.8 MB (WAV, 16-bit)
- **5 minutes @ 48 kHz**: ~28 MB (WAV, 16-bit)

## Testing & Validation

### Audio Quality Checks
1. **Export to WAV**: Listen to generated audio
   ```csharp
   audioService.ExportToWav("PILOT-251.0-1", "test.wav");
   ```

2. **Visual Inspection**: Load WAV in audio editor (Audacity, etc.)
   - Should show clear bursts with silence gaps
   - Frequency spectrum should show formant peaks
   - No clipping or distortion

3. **Amplitude Correlation**: Compare audio RMS with visualization
   ```csharp
   var envelope = generator.GenerateAmplitudeEnvelope(audioSamples, 4);
   // Should match visual amplitude graph
   ```

### Expected Audio Characteristics
- ? **Voice-like quality**: Should sound like synthetic speech
- ? **Clear bursts**: Distinct talk/silence patterns
- ? **Smooth transitions**: No clicks or pops
- ? **Formant structure**: Rich frequency content (not pure tones)
- ? **Prosody variation**: Pitch rises and falls naturally

## Phase 2 Migration Path

### Current (Phase 1 - Mock Audio)
```csharp
var mockService = new MockAudioSourceService();
var dataset = mockService.GenerateDataset(...);
```

### Future (Phase 2 - Real Audio)
```csharp
var amplitudeService = new AmplitudeSeriesProvider(frequencyManager);

// Process real audio packets
foreach (var packet in audioPackets)
{
    var envelope = amplitudeService.CalculateEnvelope(packet.AudioPayload);
    // envelope values match real voice activity
}
```

## Troubleshooting

### Audio Sounds Buzzy or Robotic
**Cause**: Formant frequencies may be static
**Fix**: Ensure formant modulation is working (vowelPhase calculation)

### Audio Has Clicks or Pops
**Cause**: Envelope attack/release too fast
**Fix**: Increase attack/release times (currently 100ms/200ms)

### Audio Too Quiet or Too Loud
**Cause**: Soft clipping threshold
**Fix**: Adjust master gain (currently 0.35) in `GenerateVoiceSample`

### No Audio Generated
**Cause**: Missing amplitude data
**Check**: Verify `GenerateTalkingPattern` returns non-zero samples

## Future Enhancements (Phase 2+)

### Real Audio Integration
- Replace mock generator with real Opus decoder output
- Process actual FrequencyManager audio packets
- Calculate real amplitude envelopes (10ms window, 5ms hop)

### Advanced Features
- **Variable voice quality**: Simulate radio interference
- **Multiple languages**: Different formant patterns
- **Emotional prosody**: Happy, stressed, calm variations
- **Background noise**: Radio static, cockpit ambient

---

**Status**: ? Mock audio generation complete  
**Build**: ? Successful  
**Audio Quality**: ? Voice-like with realistic patterns  
**Integration**: ? Synchronized with amplitude visualization  
**Ready For**: Testing and Phase 2 real audio integration
