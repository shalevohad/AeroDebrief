# LiveCharts Amplitude Visualization - Realistic Data Generation

## Overview

Phase 1 now includes realistic amplitude data generation that simulates actual voice radio transmissions, showing proper amplitude variation instead of straight lines.

## What Changed

### Before (Straight Lines Issue)
- Simple sine waves with minimal variation
- 1 sample per second (low resolution)
- No silence periods
- Constant amplitude patterns
- **Result**: Lines appeared straight or very smooth

### After (Realistic Waveforms)
- **4 samples per second** for visible detail
- **Push-to-talk simulation** with bursts and silence
- **Multi-frequency voice modulation** (low, mid, high components)
- **Envelope shaping** (ramp up/down at transmission edges)
- **Noise floor** during silence (-75 to -70 dBFS)
- **Voice activity** during transmission (-60 to -20 dBFS typical)

## Data Generation Algorithm

### Voice Activity Simulation

```
Talking Patterns:
?? Talk Duration: 2-6 seconds (random)
?? Silence Duration: 1-4 seconds (random)
?? Amplitude Components:
?  ?? Low Frequency: ±15 dB (speech fundamental)
?  ?? Mid Frequency: ±10 dB (vowel formants)
?  ?? High Frequency: ±5 dB (consonants/detail)
?  ?? Noise: ±4 dB (natural variation)
?? Envelope: Smooth ramp up/down
?? Range: -80 to -10 dBFS (voice + silence)
```

### Visual Results

**What You Should See Now**:
- **Bursting patterns**: Active transmissions as thick waveforms
- **Silence gaps**: Thin lines at -70 to -75 dBFS (noise floor)
- **Amplitude variation**: Visible peaks and valleys during speech
- **Color coding**: Each frequency has distinct color
- **Multiple pilots**: 2-4 traces per frequency

---

**Status**: ? Realistic amplitude visualization working  
**Points**: ~2,640 total (4x more detail)  
**Ready For**: Phase 2 (Real audio integration)
