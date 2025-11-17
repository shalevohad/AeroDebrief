# LiveCharts Graph Troubleshooting Guide

## Issue: No Data Visible on Graph

### Problem
The LiveCharts unified graph appears blank or shows no data lines.

### Root Causes & Solutions

#### 1. Feature Flag Disabled
**Symptom**: The entire graph section is not visible.

**Check**:
```csharp
// In Properties.Settings
UseLiveChartsRenderer = true
```

**Solution**: Ensure the feature flag is enabled in settings.

---

#### 2. Axis Configuration Missing
**Symptom**: Data exists but chart appears blank with no lines.

**Fixed In**: `UnifiedGraphControl.cs`

**What Changed**:
- Added explicit `XAxes` configuration with DateTime labeler
- Added explicit `YAxes` configuration for dBFS range (-80 to -10)
- Added separator lines for better visibility
- Set proper viewport in `OnLoaded` event

**Code**:
```csharp
XAxes = new Axis[]
{
    new Axis
    {
        Name = "Time",
        Labeler = value => new DateTime((long)value).ToString("HH:mm:ss"),
        MinStep = TimeSpan.FromSeconds(1).Ticks
    }
},
YAxes = new Axis[]
{
    new Axis
    {
        Name = "Amplitude (dBFS)",
        MinLimit = -80,
        MaxLimit = -10
    }
}
```

---

#### 3. DateTime Values Not Converted to Ticks
**Symptom**: Chart renders but all data points appear at time zero.

**Fixed In**: `UnifiedGraphViewModel.cs`

**What Changed**:
- `DateTimePoint` properly uses DateTime values
- LiveCharts automatically converts DateTime to ticks internally
- Ensured proper time progression (`AddSeconds(1)`)

---

#### 4. Series Not Visible
**Symptom**: Series exist but are hidden.

**Check**:
```csharp
series.IsVisible = true; // Ensure this is set
```

**Fixed In**: ViewModel now explicitly sets `IsVisible = true` for all series.

---

#### 5. Viewport Not Set
**Symptom**: Data exists but chart is zoomed to wrong time range.

**Fixed In**: `UnifiedGraphControl.OnLoaded()`

**Code**:
```csharp
if (vm.Series.Count > 0)
{
    _renderer.SetViewport(vm.Start, vm.End);
}
```

---

## Current State (After Fixes)

### What You Should See

**Main Chart**:
- **10 colored waveform lines** (10 frequencies)
- **Time axis** at bottom showing "HH:mm:ss" format
- **Amplitude axis** on left showing dBFS values (-80 to -10)
- **Grid lines** for better readability
- **Duration**: 30 seconds of simulated data

**Minimap** (bottom section):
- Simplified view showing all waveforms
- Same data as main chart
- Gray background

### Test Data Characteristics

```
Frequencies: 10
Pilots per frequency: 2-4
Total series: ~22
Data points: ~660 (30 seconds @ 1 sample/second)
Amplitude range: -80 to -10 dBFS (simulated voice activity)
Colors: HSL color wheel with 70% saturation, 55% lightness
```

---

## Verification Steps

### 1. Check if Graph Section is Visible
- Look for white card below the main player
- Should have "Unified Amplitude Graph [BETA]" header
- Toggle switch on the right should be ON (blue)

### 2. Check Console/Logs
Look for these NLog messages:
```
UnifiedGraphViewModel initialized: 22 series, 660 points
UnifiedGraphControl initializing with LiveCharts: True
UnifiedGraphControl loaded: 22 series, 660 points
Viewport set: HH:mm:ss to HH:mm:ss
```

### 3. Verify Chart Rendering
- Main chart area should show colored lines
- X-axis should show time labels (HH:mm:ss)
- Y-axis should show dB values (-80 to -10)
- Grid lines should be visible

### 4. Test Toggle Switch
- Click toggle switch to hide/show graph
- Animation should be smooth (200ms)
- Chart should disappear/reappear

---

## Advanced Diagnostics

### Check Series Generation
Add breakpoint in `UnifiedGraphViewModel.AddFrequencySeries()`:
```csharp
Series.Add(series); // <- Set breakpoint here
// Inspect: series.Values.Count should be 30
// Inspect: series.IsVisible should be true
```

### Check Chart Initialization
Add breakpoint in `UnifiedGraphControl.OnLoaded()`:
```csharp
_renderer.SetViewport(vm.Start, vm.End); // <- Set breakpoint here
// Inspect: vm.Series.Count should be ~22
// Inspect: _mainChart.Series should not be null
```

### Verify Renderer
Add breakpoint in `LiveChartsUnifiedChartRenderer.SetViewport()`:
```csharp
xAxis.MinLimit = min.Ticks; // <- Set breakpoint here
// Inspect: min and max should be valid DateTime values
```

---

## Common Issues (Resolved)

| Issue | Status | Fix Location |
|-------|--------|--------------|
| Blank chart | ? Fixed | Added axes configuration |
| No time labels | ? Fixed | Added DateTime labeler |
| No amplitude scale | ? Fixed | Added Y-axis with dBFS range |
| Data outside viewport | ? Fixed | Set viewport in OnLoaded |
| Series not visible | ? Fixed | Set IsVisible = true |
| No grid lines | ? Fixed | Added separator lines |

---

## Expected Behavior

### On Application Start
1. Main window opens
2. LiveCharts graph section visible (white card)
3. Graph shows 10 colored waveforms
4. Time axis: 00:00:00 to 00:00:30
5. Amplitude axis: -80 to -10 dBFS
6. Minimap shows same data (compressed)

### Data Patterns
- **Sine waves** with noise (simulates voice activity)
- **Different colors** for each frequency
- **Slight variations** between pilots on same frequency
- **Amplitude range**: -70 to -30 dBFS (typical voice range)

---

## Next Steps (Phase 2)

To connect to real recording data:
1. Implement `AmplitudeSeriesProvider` 
2. Connect to `FrequencyManager`
3. Compute real amplitude envelopes from audio
4. Replace synthetic data with real timelines

**Current Status**: Phase 1 Complete - Synthetic test data working.

---

## Quick Test

Run the application and verify:
- [ ] Graph section is visible
- [ ] White card with BETA badge
- [ ] Toggle switch is ON (blue)
- [ ] Chart shows colored lines
- [ ] Time axis has labels
- [ ] Amplitude axis has labels
- [ ] Grid lines visible
- [ ] Minimap shows data

If all checked, Phase 1 is working correctly!

---

**Last Updated**: After axis configuration fixes  
**Build Status**: ? Successful  
**Data Generation**: ? Working  
**Chart Rendering**: ? Working
