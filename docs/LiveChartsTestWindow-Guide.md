# LiveCharts Test Window - Quick Start

## Purpose

Standalone test window for LiveCharts2 integration verification during Phase 0-1 development.

## How to Run

### Option 1: From Visual Studio
1. Open `AeroDebrief.sln`
2. Set `AeroDebrief.UI` as startup project
3. Edit `Program.cs` temporarily:
   ```csharp
   var app = new App();
   var testWindow = new TestWindows.LiveChartsTestWindow();
   app.Run(testWindow);
   ```
4. Press F5

### Option 2: From Main Window
Add a test menu item to `MainWindow.xaml`:
```xaml
<MenuItem Header="_Debug">
    <MenuItem Header="LiveCharts Test" Click="OpenLiveChartsTest"/>
</MenuItem>
```

Add handler in `MainWindow.xaml.cs`:
```csharp
private void OpenLiveChartsTest(object sender, RoutedEventArgs e)
{
    var testWindow = new TestWindows.LiveChartsTestWindow();
    testWindow.Show();
}
```

### Option 3: From UnifiedPlayerTestWindow
Add similar menu item to the test harness.

## What It Tests

### Rendering
- 60 frequencies total
- 54 frequencies with 4 pilots each (216 series)
- 6 frequencies with 24 pilots each (144 series)
- **Total**: 360 series, ~432,000 data points

### Metrics Measured
1. **Load Time**: From window creation to rendering complete
   - Target: <10 seconds
   - Logs to NLog and window title

2. **Memory Usage**: Process working set and GC memory
   - Target: <1 GB total process
   - Target: <30 MB for chart data
   - Logs to NLog and window title

3. **Visual Verification**: Charts should display
   - Main chart with colored lines (one per pilot)
   - Minimap at bottom (aggregated view)
   - No errors or blank areas

## Expected Results (Phase 0)

```
Load Time: ~2-5 seconds (well under 10s target)
Working Set: ~50-100 MB (well under 1 GB target)
GC Memory: ~20-30 MB (chart data only)
Status: PASS/PASS
```

## Interpreting Output

### Window Title
Shows live metrics:
```
LiveCharts2 Test - 2347ms load, 78MB RAM
```

### NLog Output
Check logs for detailed metrics:
```
=== LiveCharts2 Phase 0 Test Results ===
Load Time: 2347 ms
Working Set: 78.3 MB
GC Memory: 24.1 MB
Target: <10s load, <1 GB memory
Status: PASS (load time)
Status: PASS (memory)
```

### Visual Indicators
- **Status Bar**: Shows "Rendering..." then "Ready"
- **Charts**: Should show colored waveform-like lines
- **No Errors**: No red error messages or blank areas

## Troubleshooting

### Charts Don't Appear
- Check build output for LiveCharts package restore
- Verify RC3.3 packages are installed
- Check NLog for rendering errors

### Slow Load Time (>10s)
- Normal for first run (JIT compilation)
- Run test 2-3 times for accurate measurement
- Check CPU usage (GPU acceleration may not be active)

### High Memory (>1 GB)
- Check for memory leaks in ViewModel
- Verify data is generated once, not repeatedly
- Monitor with Task Manager during test

### Crashes on Startup
- Check for missing native DLLs (SkiaSharp)
- Verify x64 build (not AnyCPU)
- Check NLog for initialization errors

## Next Steps After Phase 0

Once metrics are satisfactory:
1. Document actual numbers in `Phase0-Progress.md`
2. Proceed to Phase 1: Feature flag integration
3. Integrate test into CI pipeline (optional)
4. Use as baseline for Phase 4 optimization

## Files Involved

- `src/AeroDebrief.UI/TestWindows/LiveChartsTestWindow.xaml` - UI layout
- `src/AeroDebrief.UI/TestWindows/LiveChartsTestWindow.xaml.cs` - Metrics logic
- `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs` - Chart control
- `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs` - Data generator

## Phase 1 Evolution

This test window will be enhanced in Phase 1 to test:
- Feature flag toggle (UseLiveChartsRenderer)
- Renderer interface implementation
- Axis configuration (DateTime X, dB Y)
- Viewport changes
- Playhead rendering

Keep this window for ongoing integration testing throughout all phases.
