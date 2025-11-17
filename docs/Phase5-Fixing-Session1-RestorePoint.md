# Phase 5 Test Fixing - Session 1 Restore Point

**Date**: January 21, 2025  
**Status**: In Progress - 6 Failures Remaining

---

## ?? Current Status

### Test Results
```
Total Tests: 28
Passed: 22 (78.6%)
Failed: 6 (21.4%)
```

### Remaining Failures
1. ? `ZoomOut_IncreasesViewportSize` - **FIXED** (was failing, now passing)
2. ? `ViewModel_CanPan` (Control test)
3. ? `ViewModel_PanByPercentage_Works` (Control test)
4. ? `ViewModel_CanZoomOut` (Control test)
5. ? `Pan_MovesViewport` (ViewModel test)
6. ? `SetViewport_ClampsToDataRange` - **NEW FAILURE** (regressed)

---

## ?? Changes Made So Far

### Fix 1: Add Clamping to SetViewport ?
**Problem**: SetViewport didn't clamp to data range, allowing out-of-bounds viewports.

**Solution**: Added clamping logic before setting viewport properties.

### Fix 2: Fix DateTime Overflow in Pan ?
**Problem**: `End - ViewportDuration` caused overflow when End was DateTime.MinValue (default).

**Solution**: Cached `ViewportDuration` before clamping and added validation for data range.

### Fix 3: Add Data Range Validation to Zoom/Pan ?
**Problem**: Zoom/Pan methods didn't check if valid data range existed.

**Solution**: Added `Start >= End` checks at beginning of ZoomIn/ZoomOut/Pan methods.

### Fix 4: SetViewport Auto-Initialize Data Range ?
**Problem**: Tests call SetViewport without LoadDataAsync, expecting it to work.

**Solution**: SetViewport now initializes Start/End from viewport if not already set.

### Fix 5: Track Explicit Data Loading ?
**Problem**: SetViewport should clamp when data was loaded via LoadDataAsync, but expand when data wasn't loaded.

**Solution**: 
- Added `_isDataLoadedExplicitly` flag
- Set flag in `LoadDataAsync()`
- SetViewport logic:
  - If no data range: initialize from viewport
  - If data not explicitly loaded: expand to accommodate viewport
  - If data explicitly loaded: clamp viewport to data range

---

## ?? Progress Metrics

| Metric | Before Session | Current | Change |
|--------|---------------|---------|--------|
| Phase 5 Failures | 18-21 | 6 | **-12 to -15** |
| Phase 5 Pass Rate | 25-32% | 78.6% | **+46-54%** |
| Tests Fixed | 0 | 15-18 | **+15-18 tests** |

---

## ?? Remaining Issues

### Issue Pattern Analysis

The remaining 5-6 failures appear to be related to Control tests that test the same viewport methods as ViewModel tests, but the Control tests are failing while ViewModel tests pass.

**Control Tests Failing**:
- `ViewModel_CanPan` 
- `ViewModel_PanByPercentage_Works`
- `ViewModel_CanZoomOut`

**Similar ViewModel Tests Passing**:
- `Pan_MovesViewport`
- `ZoomOut_IncreasesViewportSize`

**Hypothesis**: Control tests may be using a different setup pattern or have different expectations.

---

## ?? Current Code State

### UnifiedGraphViewModel.cs Key Changes

```csharp
// Added flag to track explicit data loading
private bool _isDataLoadedExplicitly = false;

// Updated LoadDataAsync to set flag
public async Task LoadDataAsync(...)
{
    // ...
    _isDataLoadedExplicitly = true;
    // ...
}

// Updated SetViewport with smart initialization/expansion logic
public void SetViewport(DateTime start, DateTime end)
{
    if (start >= end)
    {
        _logger.Warn($"Invalid viewport range: start={start}, end={end}");
        return;
    }

    // Initialize if no data range
    bool needsInitialization = Start == default || End == default || Start >= End;
    
    if (needsInitialization)
    {
        Start = start;
        End = end;
    }
    else if (!_isDataLoadedExplicitly)
    {
        // Allow expansion if data wasn't explicitly loaded
        if (start < Start) Start = start;
        if (end > End) End = end;
    }
    // else: clamp to loaded data range
    
    // Clamp to data range
    if (start < Start) start = Start;
    if (end > End) end = End;
    
    // Ensure valid range
    if (start >= end)
    {
        _logger.Warn($"Viewport collapsed after clamping");
        return;
    }

    ViewportStart = start;
    ViewportEnd = end;
}

// Added data range validation to Pan/Zoom methods
public void ZoomIn(double factor)
{
    if (factor <= 0 || factor >= 1.0) return;
    
    // Check if we have valid data range
    if (Start >= End)
    {
        _logger.Warn("Cannot zoom: no valid data range loaded");
        return;
    }
    // ... rest of zoom logic
}

// Similar for ZoomOut and Pan
```

---

## ?? Next Steps

1. **Investigate Control Test Failures**
   - Read the actual Control test code
   - Compare with passing ViewModel tests
   - Identify differences in setup or expectations

2. **Potential Issues to Check**
   - Do Control tests use different assertion methods?
   - Do they test at different precision levels?
   - Are there setup differences between test classes?

3. **Fix Remaining Failures**
   - Address Control test specific issues
   - Verify all fixes with full test run
   - Update documentation

---

## ?? Rollback Instructions

If issues arise, revert to this state by:

1. Discard changes to `UnifiedGraphViewModel.cs`
2. Remove the `_isDataLoadedExplicitly` field
3. Restore original SetViewport/ZoomIn/ZoomOut/Pan methods

Or use git:
```bash
git checkout HEAD -- src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs
```

---

## ?? Testing Commands

Run Phase 5 tests:
```powershell
dotnet test tests\AeroDebrief.Tests\AeroDebrief.Tests.csproj --filter "FullyQualifiedName~Phase5" --verbosity normal
```

Run all tests:
```powershell
dotnet test tests\AeroDebrief.Tests\AeroDebrief.Tests.csproj --verbosity normal
```

---

**Next Action**: Investigate Control test failures and complete Phase 5 test fixes.
