# Phase 2: Amplitude Pipeline Integration

## Status: ?? IN PROGRESS

Phase 2 focuses on integrating real amplitude data from the audio pipeline into the LiveCharts visualization.

## Objectives

### 1. Real Data Integration ?
- [x] Remove mock data providers
- [x] Implement real `AmplitudeSeriesProvider`
- [ ] Connect to `FrequencyManager` and audio pipeline
- [ ] Extract amplitude envelopes from audio packets

### 2. Amplitude Calculation Pipeline
- [ ] Implement real-time amplitude extraction from Opus packets
- [ ] Convert raw audio samples to dBFS
- [ ] Apply sliding window for smooth visualization
- [ ] Cache computed amplitudes for performance

### 3. Timeline Integration
- [ ] Sync amplitude data with playback timeline
- [ ] Index amplitude data by timestamp
- [ ] Support time-range queries for zooming
- [ ] Handle frequency selection changes

### 4. Performance Optimization
- [ ] Async data loading with cancellation support
- [ ] Background thread processing
- [ ] Incremental updates during playback
- [ ] Memory-efficient data structures

## Architecture

### Data Flow
```
Audio Packets (Opus)
    ?
FrequencyManager
    ?
Amplitude Extractor
    ?
dBFS Converter
    ?
AmplitudeSeriesProvider
    ?
UnifiedGraphViewModel
    ?
LiveCharts Renderer
    ?
UI Display
```

### Key Components

#### AmplitudeSeriesProvider (Real Implementation)
```csharp
public class AmplitudeSeriesProvider : IAmplitudeSeriesProvider
{
    private readonly FrequencyManager _frequencyManager;
    private readonly IAudioPipeline _audioPipeline;
    
    public async IAsyncEnumerable<(string key, IEnumerable<DateTimePoint> points)> 
        GetSeriesAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        // Real implementation using FrequencyManager
    }
}
```

#### AmplitudeExtractor
```csharp
public class AmplitudeExtractor
{
    public double[] ExtractEnvelope(byte[] opusPacket)
    {
        // Decode Opus ? PCM samples
        // Apply RMS calculation
        // Convert to dBFS
    }
}
```

### Integration Points

1. **FrequencyManager**
   - Access to all loaded frequencies
   - Pilot/frequency associations
   - Selection state management

2. **Audio Pipeline**
   - Access to decoded audio packets
   - Timeline information
   - Packet timestamps

3. **UnifiedPlayerViewModel**
   - Playback state
   - Time position
   - Frequency selection

## Implementation Plan

### Phase 2.1: Cleanup Mock Infrastructure (Day 1)
- [x] Remove `MockAmplitudeSeriesProvider`
- [x] Remove `MockAudioGenerator`
- [x] Remove `MockAudioSourceService`
- [x] Remove test windows if not needed
- [x] Update `UnifiedGraphViewModel` to use real provider

### Phase 2.2: Amplitude Extraction (Day 2)
- [ ] Implement `AmplitudeExtractor` class
- [ ] Add Opus decoder integration
- [ ] Implement RMS amplitude calculation
- [ ] Add dBFS conversion utilities
- [ ] Unit tests for extraction logic

### Phase 2.3: Real Provider Implementation (Day 2-3)
- [ ] Connect `AmplitudeSeriesProvider` to `FrequencyManager`
- [ ] Implement packet iteration
- [ ] Build amplitude timeline index
- [ ] Add caching layer
- [ ] Support time-range queries

### Phase 2.4: UI Integration (Day 3)
- [ ] Wire up real provider in `UnifiedGraphViewModel`
- [ ] Handle loading states
- [ ] Add progress indicators
- [ ] Error handling and recovery
- [ ] Performance monitoring

### Phase 2.5: Testing & Optimization (Day 3)
- [ ] Test with real flight recordings
- [ ] Profile performance
- [ ] Optimize memory usage
- [ ] Handle edge cases
- [ ] Documentation

## Files to Modify

### To Remove
- [x] `src/AeroDebrief.UI/Services/Graphs/MockAmplitudeSeriesProvider.cs`
- [x] `src/AeroDebrief.UI/Services/Graphs/MockAudioGenerator.cs`
- [x] `src/AeroDebrief.UI/Services/Graphs/MockAudioSourceService.cs`
- [ ] `src/AeroDebrief.UI/TestWindows/UnifiedPlayerTestWindow.xaml` (evaluate if needed)
- [ ] `src/AeroDebrief.UI/TestWindows/UnifiedPlayerTestWindow.xaml.cs`

### To Create
- [ ] `src/AeroDebrief.UI/Services/Audio/AmplitudeExtractor.cs`
- [ ] `src/AeroDebrief.UI/Services/Audio/AudioEnvelopeCalculator.cs`
- [ ] `src/AeroDebrief.UI/Services/Audio/dBFSConverter.cs`
- [ ] `src/AeroDebrief.UI/Services/Graphs/AmplitudeCache.cs`

### To Modify
- [x] `src/AeroDebrief.UI/Services/Graphs/AmplitudeSeriesProvider.cs` (real implementation)
- [x] `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs` (use real provider)
- [ ] `src/AeroDebrief.UI/Services/FrequencyManager.cs` (add amplitude access)
- [ ] `src/AeroDebrief.UI/Controls/UnifiedPlayerControl.xaml.cs` (wire up provider)

## Technical Details

### Amplitude Calculation

**RMS (Root Mean Square)**:
```csharp
double rms = Math.Sqrt(samples.Select(s => s * s).Average());
```

**dBFS Conversion**:
```csharp
double dbFS = 20 * Math.Log10(rms / 32768.0); // 16-bit audio
// Result: -120 to 0 dBFS
```

**Smoothing Window**:
- Window size: 250ms (typical voice envelope)
- Update rate: 4-10 Hz for visualization
- Overlap: 50% for smooth transitions

### Data Structure

**Amplitude Timeline Entry**:
```csharp
public record AmplitudePoint(
    DateTime Timestamp,
    double Frequency,
    string PilotId,
    double AmplitudedBFS
);
```

**Series Key Format**:
```
"F{frequency}-P{pilotIndex}"
Example: "F251.0-P0", "F251.0-P1"
```

### Memory Considerations

- **Raw Audio**: ~100 KB/s per frequency
- **Amplitude Data**: ~50 bytes per point @ 4Hz = 200 bytes/s
- **30 min recording, 60 frequencies**: ~360 KB (acceptable)
- **Cache Strategy**: LRU cache with 100 MB limit

## Performance Targets

| Metric | Target | Notes |
|--------|--------|-------|
| **Initial Load** | < 2s | For 30-minute recording |
| **Memory Usage** | < 200 MB | Including cached amplitudes |
| **Frame Rate** | 60 FPS | During playback |
| **Zoom Response** | < 100ms | For time-range updates |
| **Frequency Toggle** | < 50ms | Show/hide series |

## Testing Strategy

### Unit Tests
- Amplitude extraction accuracy
- dBFS conversion correctness
- Timeline indexing
- Cache eviction

### Integration Tests
- Full pipeline with real recordings
- Multiple frequencies/pilots
- Long recordings (> 1 hour)
- Frequency selection changes

### Performance Tests
- Load time benchmarks
- Memory profiling
- CPU usage during playback
- Zoom/pan responsiveness

## Success Criteria

Phase 2 is complete when:
- [x] All mock providers removed
- [ ] Real amplitude data displayed
- [ ] Synced with audio playback
- [ ] Performance targets met
- [ ] No regressions in existing features
- [ ] Documentation updated

## Known Challenges

### 1. Opus Decoding Performance
**Challenge**: Decoding Opus packets in real-time  
**Solution**: Pre-decode during file loading, cache results

### 2. Memory Management
**Challenge**: Large recordings consume too much memory  
**Solution**: Windowed caching, lazy loading, compression

### 3. Timeline Sync
**Challenge**: Keeping amplitude graph in sync with playback  
**Solution**: Event-based updates, shared timeline model

### 4. Frequency Selection
**Challenge**: Updating graph when frequencies are toggled  
**Solution**: Reactive binding, efficient series updates

## Next Steps

1. **Immediate**: Remove mock infrastructure (Phase 2.1)
2. **Day 2**: Implement amplitude extraction (Phase 2.2)
3. **Day 3**: Real provider integration (Phase 2.3-2.4)
4. **Day 4**: Testing and optimization (Phase 2.5)

---

**Phase 2 Started**: 2025-01-21  
**Target Completion**: 2025-01-24  
**Branch**: `livechart2-integration`
