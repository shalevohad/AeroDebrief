# Phase 0 Acceptance Metrics - Ready for Testing

## Status: ? READY FOR ACCEPTANCE TEST

All Phase 0 components are in place and the full-scale acceptance test is ready to run.

## What's Been Implemented

### 1. Performance-Optimized Components ?
- **UnifiedGraphViewModel** with dual-mode data generation:
  - Fast mode (default): 10 freqs, ~660 points, <1s load
  - Full-scale mode: 60 freqs, ~432K points, <10s load target
- **UnifiedGraphControl** with critical optimizations:
  - Animations disabled
  - Tooltips/legend hidden
  - GPU acceleration enabled
- **LiveCharts2 RC3.3** packages installed and working

### 2. Test Infrastructure ?
- **LiveChartsTestWindow** - Fast iteration testing (optimized dataset)
- **Phase0AcceptanceTestWindow** - Full-scale validation (NEW)
- **Menu Integration** - Both tests accessible from Debug menu

### 3. Documentation ?
- Performance optimization guide
- Acceptance test instructions
- Results template for documentation

## How to Run Acceptance Test

### Quick Start
1. **Launch**: AeroDebrief.UI
2. **Navigate**: Debug ? Phase 0 Acceptance Test (Full Scale)
3. **Execute**: Click "START FULL SCALE TEST"
4. **Wait**: ~3-10 seconds (target <10s)
5. **Review**: Check window and NLog for PASS/FAIL

### What Gets Tested

```
Dataset: 60 frequencies (54×4 pilots, 6×24 pilots)
Series: 360 total
Points: ~432,000
Target Load: <10 seconds
Target Memory: <1 GB
```

## Acceptance Criteria

| # | Metric | Target | Pass Criteria |
|---|--------|--------|---------------|
| 1 | Load Time | <10s | Measured time <10,000ms |
| 2 | Memory | <1GB | Working set <1024MB |
| 3 | Series | 360 | Count 350-370 |
| 4 | Points | ~432K | Count 400K-450K |

**All 4 must PASS** for Phase 0 completion.

## Expected Results

### Optimistic (Best Case)
```
========================================
PHASE 0 ACCEPTANCE TEST RESULTS
========================================
Load Time: 3500 ms (3.50s) ? PASS
Series Count: 360 ? PASS
Total Points: 432,000 ? PASS
Working Set: 185.3 MB ? PASS
GC Memory: 67.8 MB ? PASS
========================================
OVERALL STATUS: ??? ALL TESTS PASSED ???
========================================
```

### Realistic (Expected)
```
Load Time: 5.5s ? PASS
Memory: 250MB ? PASS
Series: 360 ? PASS
Points: 432,000 ? PASS
Status: ??? ALL TESTS PASSED ???
```

### Threshold (Acceptable)
```
Load Time: 9.2s ? PASS (close to limit)
Memory: 890MB ? PASS (close to limit)
Series: 360 ? PASS
Points: 432,000 ? PASS
Status: ??? ALL TESTS PASSED ???
```

## Performance Factors

### Speed Variations
- **First run**: +2-5s (JIT compilation)
- **Debug build**: +3-7s vs Release
- **No GPU**: +1-2s (CPU fallback)
- **Background load**: +1-3s

### Memory Variations
- **Debug symbols**: +50-100MB
- **GC timing**: ±50MB variance
- **System overhead**: +20-50MB

## If Tests FAIL

### Load Time >10s
1. Run test 2-3 times (first run has JIT overhead)
2. Switch to Release build
3. Close background applications
4. Check CPU utilization

### Memory >1GB
1. Check for memory leaks in ViewModel
2. Verify List usage (not ObservableCollection)
3. Review series creation logic
4. Monitor with Task Manager

### Wrong Series/Point Count
1. Debug GenerateFullScaleData() method
2. Verify pilot distribution calculation
3. Check data generation loop logic

## Phase 0 Completion Checklist

Once acceptance test **PASSES**:

- [ ] Run Phase0AcceptanceTestWindow
- [ ] Verify all 4 criteria PASS
- [ ] Document results using template
- [ ] Take screenshot of passed test
- [ ] Update Phase0-Progress.md with actual metrics
- [ ] Commit changes to Git
- [ ] Update Phase0-Checklist.md status
- [ ] Mark Phase 0 as COMPLETE
- [ ] Begin Phase 1 planning

## Files Ready for Test

### Test Executables
- `Phase0AcceptanceTestWindow.xaml/.cs` ?
- `LiveChartsTestWindow.xaml/.cs` ? (fast test)

### Core Components
- `UnifiedGraphViewModel.cs` ? (with GenerateFullScaleData)
- `UnifiedGraphControl.cs` ? (optimized)
- `IUnifiedChartRenderer.cs` ?

### Documentation
- `Phase0-AcceptanceTest-Instructions.md` ?
- `Phase0-AcceptanceTest-Results-Template.md` ?
- `PerformanceOptimization-QuickRef.md` ?

## Next Steps After PASS

### Phase 1 Objectives
1. Feature flag (`UseLiveChartsRenderer`)
2. Renderer implementation
3. Settings integration
4. Fallback logic

### Phase 2 Objectives
1. Real amplitude data pipeline
2. Frequency/Pilot metadata integration
3. dB conversion
4. Timeline indexing

## Build Status

? **All components built successfully**  
? **Zero compilation errors**  
? **Test window accessible from Debug menu**  
? **Ready for execution**

## Command to Execute

From AeroDebrief.UI main window:
```
Debug ? Phase 0 Acceptance Test (Full Scale)
```

Or programmatically:
```csharp
var window = new TestWindows.Phase0AcceptanceTestWindow();
window.Show();
```

---

## Summary

**Status**: ?? READY  
**Action**: Run acceptance test  
**Expected**: ??? ALL TESTS PASSED ???  
**Duration**: ~5-10 seconds  
**Result**: Phase 0 COMPLETE

**After successful test**: Phase 0 is officially complete and Phase 1 can begin.
