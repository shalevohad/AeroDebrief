# Phase 5: Minimap & Zoom UX - Implementation Plan

## ?? Overview

**Goal**: Add minimap with viewport selection and zoom/pan gestures to the unified chart.

**Status**: Step 2 Complete ? (Zoom/Pan gestures & viewport sync)  
**Progress**: 50% complete  
**Estimated Time**: 1-2 days total  
**Prerequisites**: ? All met (Phase 3 & 4 complete)

---

## ?? Objectives

### Core Features
1. ? DataTileCache ready (L2/L3 tiles for aggregated data)
2. ? Add viewport management to ViewModel
3. ? Add zoom/pan gestures (mouse wheel, click-drag)
4. ? Add minimap click navigation
5. ? Implement viewport rectangle overlay on minimap
6. ? Two-way sync between main chart and minimap
7. ? Add keyboard shortcuts and visual polish

### User Experience
- ? User can zoom with mouse wheel
- ? User can pan with middle-button drag or Ctrl+Left drag
- ? User can click minimap to jump to time
- User can see full recording in minimap
- User can see viewport rectangle on minimap (? Step 3)
- Main chart zoom updates minimap viewport
- Smooth, responsive interactions

---

## ? Step 1: ViewModel Viewport Management (COMPLETE)

**Status**: ? **COMPLETE**  
**Tests**: 17/17 passing  
**Files**: 
- Modified: `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
- Added: `tests/AeroDebrief.Tests/ViewModels/UnifiedGraphViewModelPhase5Tests.cs`

**Features Implemented**:
- ? ViewportStart/ViewportEnd properties
- ? ViewportChanged event
- ? SetViewport, ZoomIn, ZoomOut, Pan, ResetViewport methods
- ? DateTime overflow protection
- ? Data range clamping
- ? 17 comprehensive tests

**See**: `docs/Phase5-Step1-Complete.md` for details

---

## ? Step 2: Zoom/Pan Gestures & Viewport Sync (COMPLETE)

**Status**: ? **COMPLETE**  
**Tests**: 12/12 passing (39 total ViewModel+Control tests)  
**Files**:
- Modified: `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`
- Added: `tests/AeroDebrief.Tests/Controls/UnifiedGraphControlPhase5Tests.cs`

**Features Implemented**:
- ? Mouse wheel zoom (scroll up/down)
- ? Click-drag pan (middle button or Ctrl+Left)
- ? Minimap click navigation
- ? Viewport synchronization (ViewModel ? Chart)
- ? Pan state management
- ? Cursor feedback
- ? 12 integration tests

**User Gestures**:
- Scroll wheel: Zoom in/out
- Middle button drag or Ctrl+Left drag: Pan
- Click minimap: Jump to time
- All gestures respect data range boundaries

**See**: `docs/Phase5-Step2-Complete.md` for details

---

## ?? Implementation Steps

### ~~Step 1: Update ViewModel~~ ? COMPLETE

### ~~Step 2: Zoom/Pan Gestures & Sync~~ ? COMPLETE

### Step 3: Visual Enhancements ? NEXT

---

## ?? Files to Create/Modify

### New Files (1)
1. `tests/AeroDebrief.Tests/Controls/UnifiedGraphControlPhase5Tests.cs`

### Modified Files (3)
1. `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
2. `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.xaml`
3. `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`

---

## ?? Considerations

### LiveCharts2 Specifics
- RectangularSection for viewport overlay
- Axis MinLimit/MaxLimit for zoom
- ChartPointPointerDown for click events
- Manual update trigger for sync

### Performance
- Use L2/L3 tiles for minimap (not L0)
- Limit minimap series count (top 10 frequencies?)
- Debounce rapid zoom/pan events
- Virtual rendering for large datasets

### UX Edge Cases
- Zoom too far in (minimum zoom?)
- Zoom too far out (already at full range)
- Pan beyond data range (clamp)
- Empty data (show message)

---

## ?? Next Phase Preview

**Phase 6: Playhead & Seek Sync**
- Add vertical playhead line
- Sync with audio playback
- Click to seek
- Follow mode toggle

**Prerequisites from Phase 5**:
- ? Viewport management (for scroll-to-playhead)
- ? Time-to-pixel conversion (for playhead positioning)

---

**Status**: Ready to implement  
**Estimated Completion**: January 23, 2025  
**Confidence**: High
