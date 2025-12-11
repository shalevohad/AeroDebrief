# UI Integration Guide for Amplitude Cache

## ?? Quick Integration Checklist

- [ ] Update waveform loading code to use `QueryRangeWithGainAsync()`
- [ ] Pass mixer gain to query method
- [ ] Remove audio decoding from graph rendering path
- [ ] Add cache statistics to UI (optional)
- [ ] Test with new recordings (100% cached)
- [ ] Test with old recordings (0% cached - graceful degradation)

## ?? Where to Make Changes

### 1. Waveform Loading (Most Critical)

**File:** `src/AeroDebrief.UI/ViewModels/WaveformViewModel.cs` (or similar)

**OLD CODE (SLOW):**
```csharp
private async Task LoadWaveformDataAsync(double frequency, TimeSpan from, TimeSpan to)
{
    // This is SLOW - re-decodes every packet!
    var packets = await _packetSource.GetPacketsAsync(frequency, from, to);
    var waveformPoints = new List<float>();
    
    foreach (var packet in packets)
    {
        var decoded = _audioEngine.DecodePacketToFloat(packet); // 2ms per packet!
        var amplitude = decoded.Max(Math.Abs);
        waveformPoints.Add(amplitude);
    }
    
    UpdateWaveformDisplay(waveformPoints);
}
```

**NEW CODE (FAST):**
```csharp
private async Task LoadWaveformDataAsync(double frequency, TimeSpan from, TimeSpan to)
{
    // Get recording unit of work
    var unitOfWork = GetCurrentUnitOfWork() as SqliteUnitOfWork;
    if (unitOfWork == null)
    {
        // Fallback to old method if not SQLite (backward compatibility)
        await LoadWaveformDataOldMethodAsync(frequency, from, to);
        return;
    }

    // Get current mixer gain for this frequency
    var mixerGain = GetMixerGain(frequency);

    // Query pre-computed amplitude with gain applied
    var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
        frequency,
        (long)from.TotalMilliseconds,
        (long)to.TotalMilliseconds,
        mixerGain);

    // Extract waveform points
    var waveformPoints = amplitudes.Select(a => a.MaxAmplitude).ToList();
    
    UpdateWaveformDisplay(waveformPoints);
}
```

### 2. Mixer Gain Integration

**File:** `src/AeroDebrief.UI/Controls/FrequencyMixer.cs`

**When user changes gain:**
```csharp
private void OnGainChanged(double frequency, float newGain)
{
    // Store new gain
    _frequencyGains[frequency] = newGain;
    
    // Notify waveform to reload with new gain
    // NO cache invalidation needed - gain applied at query time!
    WaveformRefreshRequested?.Invoke(this, new WaveformRefreshEventArgs
    {
        Frequency = frequency,
        NewGain = newGain
    });
}
```

**Helper method:**
```csharp
public float GetMixerGain(double frequency)
{
    return _frequencyGains.TryGetValue(frequency, out var gain) 
        ? gain 
        : 1.0f; // Default gain
}
```

### 3. Detailed Waveform for Zoom

**File:** `src/AeroDebrief.UI/ViewModels/WaveformViewModel.cs`

**For zoomed-in views (use peak envelope):**
```csharp
private async Task LoadDetailedWaveformAsync(double frequency, TimeSpan from, TimeSpan to)
{
    var unitOfWork = GetCurrentUnitOfWork() as SqliteUnitOfWork;
    if (unitOfWork == null) return;

    var mixerGain = GetMixerGain(frequency);
    
    var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
        frequency,
        (long)from.TotalMilliseconds,
        (long)to.TotalMilliseconds,
        mixerGain);

    // Use peak envelope for higher resolution
    var detailedPoints = amplitudes
        .SelectMany(a => a.PeakEnvelope)
        .ToList();
    
    UpdateWaveformDisplay(detailedPoints);
}
```

**Zoom level detection:**
```csharp
private async Task LoadWaveformForZoomLevelAsync(double frequency, TimeSpan from, TimeSpan to)
{
    var zoomLevel = CalculateZoomLevel(from, to);
    
    if (zoomLevel > 10.0) // Very zoomed in
    {
        // Use detailed peak envelope
        await LoadDetailedWaveformAsync(frequency, from, to);
    }
    else
    {
        // Use packet-level amplitude
        await LoadWaveformDataAsync(frequency, from, to);
    }
}
```

### 4. Cache Statistics Display (Optional)

**File:** `src/AeroDebrief.UI/Views/FileInfoView.xaml` (or similar)

**Add cache stats to file info:**
```csharp
private async Task LoadFileInfoAsync(string filePath)
{
    var unitOfWork = _repositoryFactory.OpenRecording(filePath) as SqliteUnitOfWork;
    if (unitOfWork == null) return;

    using (unitOfWork)
    {
        // Existing file info loading...
        
        // Add cache statistics
        var cacheStats = await unitOfWork.Amplitudes.GetStatsAsync();
        
        CacheStatsText = $"Amplitude Cache: {cacheStats.CachePercentage:F1}% " +
                        $"({cacheStats.CachedPackets:N0}/{cacheStats.TotalPackets:N0} packets)";
        
        if (cacheStats.CachePercentage < 100)
        {
            CacheWarningVisible = true;
            CacheWarningText = "Some packets are not cached. Waveform loading may be slower.";
        }
    }
}
```

**XAML:**
```xml
<TextBlock Text="{Binding CacheStatsText}" 
           Style="{StaticResource BodyTextStyle}"
           Margin="0,4,0,0"/>

<Border Background="#FFF3CD" 
        Padding="8" 
        Margin="0,8,0,0"
        Visibility="{Binding CacheWarningVisible, Converter={StaticResource BoolToVisibilityConverter}}">
    <TextBlock Text="{Binding CacheWarningText}" 
               Foreground="#856404"/>
</Border>
```

### 5. Loading Progress Indicator

**File:** `src/AeroDebrief.UI/ViewModels/WaveformViewModel.cs`

**Show progress during amplitude query:**
```csharp
private async Task LoadWaveformDataAsync(double frequency, TimeSpan from, TimeSpan to)
{
    try
    {
        IsLoading = true;
        LoadingMessage = "Loading waveform data...";

        var unitOfWork = GetCurrentUnitOfWork() as SqliteUnitOfWork;
        if (unitOfWork == null)
        {
            LoadingMessage = "Opening recording...";
            await LoadWaveformDataOldMethodAsync(frequency, from, to);
            return;
        }

        LoadingMessage = "Querying amplitude cache...";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var mixerGain = GetMixerGain(frequency);
        var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
            frequency,
            (long)from.TotalMilliseconds,
            (long)to.TotalMilliseconds,
            mixerGain);

        sw.Stop();
        
        LoadingMessage = $"Rendering waveform ({amplitudes.Count:N0} points)...";
        var waveformPoints = amplitudes.Select(a => a.MaxAmplitude).ToList();
        
        UpdateWaveformDisplay(waveformPoints);
        
        StatusMessage = $"Loaded in {sw.ElapsedMilliseconds}ms ({amplitudes.Count:N0} packets)";
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to load waveform data");
        ErrorMessage = $"Error loading waveform: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}
```

## ?? Helper Methods

### Get Current UnitOfWork

```csharp
private IUnitOfWork? _currentUnitOfWork;

private IUnitOfWork? GetCurrentUnitOfWork()
{
    if (_currentUnitOfWork == null && !string.IsNullOrEmpty(CurrentFilePath))
    {
        var factory = new SqliteRepositoryFactory();
        _currentUnitOfWork = factory.OpenRecording(CurrentFilePath);
    }
    return _currentUnitOfWork;
}
```

### Get Mixer Gain

```csharp
private readonly Dictionary<double, float> _frequencyGains = new();

private float GetMixerGain(double frequency)
{
    return _frequencyGains.TryGetValue(frequency, out var gain) 
        ? gain 
        : 1.0f; // Default gain
}

public void SetMixerGain(double frequency, float gain)
{
    _frequencyGains[frequency] = Math.Clamp(gain, 0.0f, 2.0f);
    
    // Reload waveform with new gain (fast - just re-query with new gain)
    _ = LoadWaveformDataAsync(frequency, _currentViewStart, _currentViewEnd);
}
```

## ?? UI Enhancements

### Cache Status Indicator

**Add to status bar:**
```xml
<StatusBar>
    <!-- Existing status items... -->
    
    <StatusBarItem>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="??" Margin="0,0,4,0"/>
            <TextBlock Text="Cache: "/>
            <TextBlock Text="{Binding CachePercentage, StringFormat={}{0:F1}%}"
                      FontWeight="Bold"/>
        </StackPanel>
    </StatusBarItem>
</StatusBar>
```

### Performance Metrics

**Add to debug/info panel:**
```xml
<StackPanel Visibility="{Binding ShowDebugInfo, Converter={StaticResource BoolToVisibilityConverter}}">
    <TextBlock Text="Performance Metrics" FontWeight="Bold"/>
    <TextBlock Text="{Binding LastQueryTime, StringFormat=Last query: {0}ms}"/>
    <TextBlock Text="{Binding PacketsPerSecond, StringFormat=Speed: {0:N0} packets/ms}"/>
    <TextBlock Text="{Binding MemoryUsage, StringFormat=Memory: {0}}"/>
</StackPanel>
```

## ?? Testing Scenarios

### Test Case 1: New Recording (100% Cache)

1. Start new recording
2. Stop recording (amplitude cache populated automatically)
3. Load recording in UI
4. **Expected:** Instant waveform loading (<5 seconds)
5. Change mixer gain
6. **Expected:** Instant waveform update (<200ms)

### Test Case 2: Old Recording (0% Cache)

1. Load pre-Phase 7 recording
2. Check cache stats: Should show 0%
3. **Expected:** Graceful degradation to old method
4. **Expected:** Slower loading (but still functional)

### Test Case 3: Mixer Gain Changes

1. Load recording
2. Adjust mixer gain for frequency
3. **Expected:** Waveform updates instantly
4. **Expected:** No cache invalidation warnings
5. **Expected:** Query time < 200ms

### Test Case 4: Zoom Operations

1. Load recording at overview zoom
2. Use packet-level amplitude
3. **Expected:** Fast rendering
4. Zoom in 10x
5. Switch to peak envelope
6. **Expected:** Higher resolution waveform
7. **Expected:** Query time < 500ms

## ?? Expected Performance

### Metrics to Track

```csharp
private readonly PerformanceMetrics _metrics = new();

private async Task LoadWaveformDataAsync(double frequency, TimeSpan from, TimeSpan to)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    // Load amplitude data...
    
    sw.Stop();
    
    _metrics.RecordQuery(sw.ElapsedMilliseconds, amplitudes.Count);
    
    Logger.Info($"?? Waveform loaded: {amplitudes.Count:N0} points in {sw.ElapsedMilliseconds}ms");
    Logger.Info($"   Speed: {amplitudes.Count / Math.Max(1, sw.ElapsedMilliseconds):N0} points/ms");
}
```

### Performance Targets

| Scenario | Packets | Expected Time | Status |
|----------|---------|---------------|--------|
| 1 minute recording | 1,500 | <50ms | ? |
| 10 minute recording | 15,000 | <200ms | ? |
| 1 hour recording | 90,000 | <1s | ? |
| 3 hour recording | 270,000 | <3s | ? |

**If slower:** Check cache stats, run `ANALYZE amplitude_cache`

## ?? Common Issues

### Issue 1: Slow Queries

**Symptom:** Queries take >5 seconds

**Check:**
```csharp
var stats = await unitOfWork.Amplitudes.GetStatsAsync();
Console.WriteLine($"Cache: {stats.CachePercentage:F1}%");
```

**Fix:** If cache is 0%, use old method as fallback

### Issue 2: Waveform Not Updating on Mixer Change

**Symptom:** Changing gain doesn't update waveform

**Check:** Are you using `QueryRangeWithGainAsync()` with current gain?

**Fix:**
```csharp
// WRONG:
var amplitudes = await unitOfWork.Amplitudes.QueryRangeAsync(...); // ? No gain!

// RIGHT:
var gain = GetMixerGain(frequency);
var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(..., gain);
```

### Issue 3: Memory Leak

**Symptom:** Memory grows over time

**Check:** Are you disposing UnitOfWork?

**Fix:**
```csharp
using var unitOfWork = GetCurrentUnitOfWork();
// ... use unitOfWork ...
// Automatically disposed at end of using block
```

## ?? Resources

- **Implementation Guide:** `docs/Amplitude-Cache-Guide.md`
- **Code Examples:** `src/AeroDebrief.Core/Examples/AmplitudeCacheExample.cs`
- **Summary:** `docs/Amplitude-Cache-Implementation-Summary.md`

---

**Questions?** Check the examples or implementation guide!
