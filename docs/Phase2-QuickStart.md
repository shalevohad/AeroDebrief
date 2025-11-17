# Phase 2 Quick Start Guide

## ?? You Are Here
Phase 1 is complete and cleaned. Phase 2 implementation starts now.

---

## ?? Key Files to Know

### 1. Core Implementation Files

#### `src/AeroDebrief.UI/Services/Graphs/AmplitudeSeriesProvider.cs`
**Status**: ?? Phase 2 structure ready, needs real implementation  
**Purpose**: Provides amplitude time series data to the graph  
**What to do**: 
- Replace `GenerateSyntheticDataAsync()` with real implementation
- Uncomment and implement TODO methods at bottom of file
- Inject `FrequencyManager` and audio pipeline dependencies

#### `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
**Status**: ? Updated for Phase 2, ready to use  
**Purpose**: ViewModel for LiveCharts graph  
**What to do**:
- Wire up to real `AmplitudeSeriesProvider` (already structured)
- Implement `ConnectToFrequencyManager()` method
- Add loading state management

#### `src/AeroDebrief.UI/Services/FrequencyManager.cs`
**Status**: ? Existing, needs minor additions  
**Purpose**: Manages frequency data and selection  
**What to do**:
- Add method to expose audio packet timeline
- Add amplitude data query API
- Minimal changes needed

### 2. Files to Create

#### `src/AeroDebrief.UI/Services/Audio/AmplitudeExtractor.cs` ? START HERE
**Priority**: HIGH - Day 1  
**Purpose**: Extract amplitude envelopes from audio packets  
**Implement**:
```csharp
public class AmplitudeExtractor
{
    // Decode Opus packet to PCM samples
    public double[] DecodeToPCM(byte[] opusPacket) { }
    
    // Calculate RMS amplitude
    public double CalculateRMS(double[] samples) { }
    
    // Convert to dBFS
    public double ConvertToDbFS(double rms) { }
    
    // Extract full envelope
    public IEnumerable<AmplitudePoint> ExtractEnvelope(
        byte[] opusPacket, 
        DateTime timestamp,
        TimeSpan windowSize) { }
}
```

#### `src/AeroDebrief.UI/Services/Audio/dBFSConverter.cs`
**Priority**: MEDIUM - Day 1  
**Purpose**: Utility for dBFS conversions  
**Implement**:
```csharp
public static class DbFSConverter
{
    public static double RmsToDbFS(double rms, int bitDepth = 16);
    public static double LinearToDbFS(double linear);
    public static double DbFSToLinear(double dbfs);
}
```

---

## ?? Implementation Order (3 Days)

### Day 1: Foundation (4-6 hours)
1. ? **Create `AmplitudeExtractor.cs`**
   - Implement RMS calculation
   - Add dBFS conversion
   - Write unit tests

2. ? **Create `dBFSConverter.cs`**
   - Static utility methods
   - Handle edge cases (log of 0, etc.)

3. ? **Test Extraction Logic**
   - Unit tests with synthetic audio
   - Verify dBFS range (-120 to 0)
   - Profile performance

### Day 2: Integration (6-8 hours)
1. ? **Update `AmplitudeSeriesProvider.cs`**
   - Add constructor with dependencies
   - Implement real `GetSeriesAsync()`
   - Connect to FrequencyManager

2. ? **Modify `FrequencyManager.cs`**
   - Add audio packet access API
   - Expose timeline data
   - Add amplitude query methods

3. ? **Wire Up Dependencies**
   - Update DI container (if applicable)
   - Pass FrequencyManager to provider
   - Test data flow

### Day 3: UI & Polish (4-6 hours)
1. ? **Update `UnifiedPlayerControl.xaml.cs`**
   - Wire up real provider
   - Add loading indicators
   - Handle errors gracefully

2. ? **Test with Real Data**
   - Load actual flight recordings
   - Verify amplitude accuracy
   - Check performance

3. ? **Optimization**
   - Profile memory usage
   - Optimize hot paths
   - Add caching if needed

---

## ?? Code Snippets

### RMS Calculation
```csharp
public double CalculateRMS(double[] samples)
{
    if (samples.Length == 0) return 0;
    
    double sumOfSquares = 0;
    foreach (var sample in samples)
    {
        sumOfSquares += sample * sample;
    }
    
    return Math.Sqrt(sumOfSquares / samples.Length);
}
```

### dBFS Conversion
```csharp
public double ConvertToDbFS(double rms, int bitDepth = 16)
{
    // Maximum amplitude for bit depth
    var maxAmplitude = Math.Pow(2, bitDepth - 1);
    
    // Ratio of RMS to maximum
    var ratio = rms / maxAmplitude;
    
    // Prevent log(0) - treat as -120 dBFS floor
    if (ratio < 1e-6) return -120.0;
    
    // Convert to dBFS: 20 * log10(ratio)
    return 20.0 * Math.Log10(ratio);
}
```

### Async Series Generation Pattern
```csharp
public async IAsyncEnumerable<(string key, IEnumerable<DateTimePoint> points)> 
    GetSeriesAsync(DateTime start, DateTime end, CancellationToken ct)
{
    // Query FrequencyManager for active frequencies
    var frequencies = _frequencyManager.GetFrequenciesInTimeRange(start, end);
    
    foreach (var freq in frequencies)
    {
        foreach (var pilot in freq.Pilots)
        {
            // Extract amplitude for this pilot
            var points = await ExtractAmplitudeAsync(freq, pilot, start, end, ct);
            
            var key = $"F{freq.Frequency:F1}-P{pilot.Index}";
            yield return (key, points);
            
            // Allow cancellation
            if (ct.IsCancellationRequested) yield break;
        }
    }
}
```

---

## ?? Where to Find Examples

### Existing Audio Processing
Look at these files for audio handling patterns:
- `src/AeroDebrief.Core/Audio/FrequencyChannelMixer.cs` - Audio mixing
- `src/AeroDebrief.Core/Playback/FilePlaybackPipeline.cs` - Packet processing
- `External/SRS/SharedAudio/` - Opus codec integration

### Existing Data Providers
Similar patterns to follow:
- `src/AeroDebrief.UI/Services/WaveformManager.cs` - Timeline data
- `src/AeroDebrief.UI/ViewModels/FrequencyPresenceViewModel.cs` - Real-time updates

---

## ?? Testing Strategy

### Unit Tests
```csharp
[Test]
public void RMS_Calculation_Accuracy()
{
    var samples = new double[] { 0.5, -0.5, 0.8, -0.8 };
    var rms = _extractor.CalculateRMS(samples);
    Assert.That(rms, Is.EqualTo(0.65).Within(0.01));
}

[Test]
public void dBFS_Conversion_Range()
{
    var dbfs = DbFSConverter.RmsToDbFS(16384); // Half max
    Assert.That(dbfs, Is.EqualTo(-6.02).Within(0.1));
}
```

### Integration Tests
1. Load small test recording (5-10 seconds)
2. Extract amplitude data
3. Verify point count and timing
4. Check dBFS range (-120 to 0)
5. Measure performance (< 100ms)

---

## ?? Performance Targets

| Metric | Target | How to Measure |
|--------|--------|----------------|
| **Initial Load** | < 2s | Stopwatch around `LoadDataAsync()` |
| **Memory** | < 200 MB | Task Manager / Profiler |
| **Frame Rate** | 60 FPS | WPF Performance Profiler |
| **Extraction** | < 10ms/packet | Benchmark `ExtractEnvelope()` |

---

## ?? Common Issues & Solutions

### Issue: "Opus decoder not found"
**Solution**: Check External/SRS/SharedAudio integration, ensure Opus DLLs are copied

### Issue: "Out of memory during load"
**Solution**: Implement windowed loading, don't load entire timeline at once

### Issue: "Timeline not syncing"
**Solution**: Verify DateTime stamps match between audio packets and graph points

### Issue: "Graph is slow/laggy"
**Solution**: Reduce point density, implement decimation, disable animations

---

## ?? Need Help?

### Documentation
- `docs/Phase2-AmplitudePipeline.md` - Detailed implementation plan
- `docs/AeroDebrief-Development-Plan-LiveCharts2.md` - Overall architecture
- Code comments - Extensive TODOs in `AmplitudeSeriesProvider.cs`

### Debugging
1. Enable verbose logging: Set NLog level to Debug
2. Check Output window for errors
3. Use breakpoints in `GetSeriesAsync()`
4. Inspect `_logger` messages

### Validation
```powershell
# Verify build
dotnet build

# Run with verbose output
dotnet run --project src/AeroDebrief.UI --verbosity detailed

# Run specific tests
dotnet test --filter "FullyQualifiedName~Amplitude"
```

---

## ? Definition of Done

Phase 2 is complete when:
- [ ] All synthetic/mock code removed
- [ ] Real amplitude data displaying in graph
- [ ] Synced with audio playback timeline
- [ ] Performance targets met
- [ ] Unit tests passing
- [ ] Integration tests with real files working
- [ ] No regressions in existing features
- [ ] Documentation updated

---

## ?? Quick Wins

Start with these for immediate progress:

1. **30 minutes**: Create `AmplitudeExtractor.cs` skeleton
2. **1 hour**: Implement and test RMS calculation
3. **1 hour**: Implement and test dBFS conversion
4. **2 hours**: Wire up to one frequency/pilot
5. **2 hours**: Expand to all frequencies
6. **1 hour**: Add UI loading indicators

**Total**: ~7-8 hours to working prototype

---

**Current Status**: ?? Ready to start  
**Next File**: `src/AeroDebrief.UI/Services/Audio/AmplitudeExtractor.cs`  
**Next Method**: `CalculateRMS(double[] samples)`

**Let's build this! ??**
