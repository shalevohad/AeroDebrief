# Phase 0 Acceptance Test - Instructions

## Purpose
Validate that the LiveCharts2 integration meets all Phase 0 performance and functional requirements using the **full-scale dataset** (60 frequencies, 360 series, ~432K points).

## How to Run

### Method 1: From Main Window (Recommended)
1. Launch AeroDebrief.UI
2. Go to menu: **Debug ? Phase 0 Acceptance Test (Full Scale)**
3. Click the **"START FULL SCALE TEST"** button
4. Wait for test to complete (~10 seconds expected)
5. Review results in window and NLog

### Method 2: Direct Launch
```csharp
var acceptanceWindow = new TestWindows.Phase0AcceptanceTestWindow();
acceptanceWindow.Show();
```

## Acceptance Criteria

The test validates these Phase 0 requirements:

| # | Requirement | Target | Pass Criteria |
|---|------------|--------|---------------|
| 1 | **Load Time** | <10 seconds | Load completes in <10s |
| 2 | **Memory Usage** | <1 GB | Working set <1024 MB |
| 3 | **Series Count** | 360 series | 350-370 series generated |
| 4 | **Point Count** | ~432K points | 400K-450K points |

### Dataset Composition
- **60 frequencies total**
- **54 frequencies** with 4 pilots each = 216 series
- **6 frequencies** with 24 pilots each = 144 series
- **Total**: 360 series
- **Data points**: 5 minutes @ 4 samples/second = ~1,200 points per series = ~432,000 total

## Test Flow

1. **Click START** - Begins test timer
2. **Generate Data** - Creates full-scale synthetic dataset
   - 60 frequencies with pilot distribution
   - ~432K DateTimePoint objects
3. **Render Chart** - LiveCharts2 renders all series
4. **Measure Metrics** - Captures:
   - Load time (ms)
   - Process working set (MB)
   - GC memory (MB)
   - Actual series/point counts
5. **Display Results** - Shows PASS/FAIL for each criterion

## Expected Results

### Optimistic (Target)
```
Load Time: 3-5 seconds ? PASS
Memory: 100-300 MB ? PASS
Series: 360 ? PASS
Points: 432,000 ? PASS
Overall: ??? ALL TESTS PASSED ???
```

### Acceptable (Threshold)
```
Load Time: 7-9 seconds ? PASS
Memory: 500-900 MB ? PASS
Series: 360 ? PASS
Points: 432,000 ? PASS
Overall: ??? ALL TESTS PASSED ???
```

### Failure Indicators
```
Load Time: >10 seconds ? FAIL
Memory: >1024 MB ? FAIL
Series: <350 or >370 ? FAIL
Points: <400K or >450K ? FAIL
```

## Output Locations

### 1. Window Title
```
Phase 0 Acceptance - PASS - 4.23s, 234MB
```

### 2. Status Bar
```
? PASSED - 4.23s load, 234MB RAM, 360 series, 432,000 points
```

### 3. NLog Output
```
========================================
PHASE 0 ACCEPTANCE TEST RESULTS
========================================
Load Time: 4234 ms (4.23s)
Series Count: 360
Total Points: 432,000
Working Set: 234.5 MB
GC Memory: 89.2 MB
========================================
ACCEPTANCE CRITERIA:
1. Load Time <10s: ? PASS (4.23s)
2. Memory <1GB: ? PASS (234MB)
3. Series Count ?360: ? PASS (360)
4. Point Count ?432K: ? PASS (432,000)
========================================
OVERALL STATUS: ??? ALL TESTS PASSED ???
========================================
```

## Interpreting Results

### All Tests PASS ?
- **Action**: Phase 0 is COMPLETE
- **Next**: Proceed to Phase 1 (Feature Flag Integration)
- **Document**: Record actual metrics in Phase0-Progress.md

### Load Time FAIL ??
- **Possible Causes**:
  - First run (JIT compilation overhead)
  - Background processes consuming CPU
  - Debug build (Release is faster)
- **Action**: Run test 2-3 times; use best result

### Memory FAIL ??
- **Possible Causes**:
  - Memory leak in data generation
  - ObservableCollection instead of List
  - Series not being GC'd
- **Action**: Review ViewModel and Control for leaks

### Series/Points FAIL ??
- **Possible Causes**:
  - Bug in GenerateFullScaleData()
  - Wrong pilot distribution calculation
- **Action**: Debug data generation logic

## Troubleshooting

### Test Window Freezes
- **Expected**: Brief freeze (3-10s) during data generation
- **Action**: Wait for completion; check NLog for errors

### Out of Memory Exception
- **Cause**: System RAM <2GB available
- **Action**: Close other apps; check Task Manager

### No Chart Displayed
- **Cause**: LiveCharts rendering error
- **Action**: Check NLog for SkiaSharp errors

### Test Fails to Start
- **Cause**: Missing dependencies or build errors
- **Action**: Rebuild solution; check package restore

## Performance Tips

### For Best Results
1. Close unnecessary applications
2. Run test 2-3 times (JIT warmup)
3. Use Release build (not Debug)
4. Monitor Task Manager during test
5. Check GPU availability

### Known Variations
- **First run**: +2-5s (JIT compilation)
- **Debug build**: +3-7s slower than Release
- **No GPU**: +1-2s (CPU fallback)
- **Low RAM**: +2-4s (paging overhead)

## Phase 0 Sign-Off

Once acceptance test **PASSES**, Phase 0 is complete:

- [ ] Test executed and PASSED
- [ ] Metrics documented in Phase0-Progress.md
- [ ] Results committed to Git
- [ ] Phase 1 planning can begin

## Files Involved

- **Test Window**: `src/AeroDebrief.UI/TestWindows/Phase0AcceptanceTestWindow.cs`
- **ViewModel**: `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
- **Chart Control**: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`
- **Menu Item**: `src/AeroDebrief.UI/MainWindow.xaml` (Debug menu)

## Comparison to Fast Test

| Aspect | Fast Test (Default) | Acceptance Test (Full Scale) |
|--------|---------------------|------------------------------|
| **Purpose** | Development iteration | Phase 0 validation |
| **Frequencies** | 10 | 60 |
| **Series** | ~22 | 360 |
| **Points** | ~660 | ~432,000 |
| **Load Time** | <1 second | <10 seconds (target) |
| **Memory** | <50 MB | <1 GB (target) |
| **Use When** | Daily development | Milestone completion |

---

**Status**: Ready for Phase 0 sign-off  
**Command**: Debug ? Phase 0 Acceptance Test (Full Scale)  
**Expected**: ??? ALL TESTS PASSED ???
