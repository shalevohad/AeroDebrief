# Phase 5.1 Zoom/Pan - Quick Testing Guide

## ?? Quick Start Testing

### Prerequisites
1. Build the solution (should be successful)
2. Run AeroDebrief.UI
3. Load a recording file (CVR, ADB, or DB format)

---

## ? Basic Functionality Tests (5 minutes)

### Test 1: Mouse Wheel Zoom
1. **Action**: Scroll mouse wheel up (away from you) over the chart
2. **Expected**: Chart zooms IN at cursor position
3. **Verify**: Zoom badge appears in top-left showing "Zoom: X.Xx"
4. **Action**: Scroll mouse wheel down (toward you)
5. **Expected**: Chart zooms OUT
6. **Action**: Keep zooming out
7. **Expected**: Zoom badge disappears when fully zoomed out (1.0x)

### Test 2: Pan with Mouse
1. **Action**: Hold middle mouse button and drag left/right
2. **Expected**: Chart pans smoothly in opposite direction (natural scrolling)
3. **Alternative**: Hold Ctrl + Left mouse button and drag
4. **Expected**: Same panning behavior

### Test 3: Keyboard Zoom
1. **Action**: Press + key (or Numpad +)
2. **Expected**: Chart zooms in at center
3. **Action**: Press - key (or Numpad -)
4. **Expected**: Chart zooms out
5. **Action**: Press R key
6. **Expected**: Chart resets to full view (zoom 1.0x)

### Test 4: Keyboard Pan
1. **Action**: Zoom in first (+ key)
2. **Action**: Press Right Arrow
3. **Expected**: Chart pans right by 10% of viewport
4. **Action**: Press Shift + Right Arrow
5. **Expected**: Chart pans right faster (50% of viewport)
6. **Action**: Press Home key
7. **Expected**: Chart jumps to start of recording
8. **Action**: Press End key
9. **Expected**: Chart jumps to end of recording

### Test 5: Page Up/Down
1. **Action**: Zoom in to about 5x
2. **Action**: Press Page Down
3. **Expected**: Chart pans right by one full viewport width
4. **Action**: Press Page Up
5. **Expected**: Chart pans left by one full viewport width

---

## ?? Edge Case Tests (3 minutes)

### Test 6: Zoom Limits
1. **Action**: Keep zooming in (mouse wheel or + key)
2. **Expected**: Eventually stops at minimum zoom (1 second viewport)
3. **Action**: Keep zooming out (mouse wheel down or - key)
4. **Expected**: Eventually stops at maximum zoom (full recording)

### Test 7: Pan Limits
1. **Action**: Zoom in, then pan all the way left
2. **Expected**: Stops at start of recording (doesn't go beyond)
3. **Action**: Pan all the way right
4. **Expected**: Stops at end of recording (doesn't go beyond)

### Test 8: Rapid Operations
1. **Action**: Rapidly zoom in and out with mouse wheel
2. **Expected**: No crashes, smooth operation, zoom badge updates correctly
3. **Action**: Rapidly pan left/right with mouse drag
4. **Expected**: No crashes, smooth tracking

---

## ?? Performance Tests (2 minutes)

### Test 9: Responsiveness
1. **Action**: Zoom in/out with mouse wheel
2. **Expected**: < 50ms latency (feels instant)
3. **Action**: Pan with mouse drag
4. **Expected**: Smooth tracking, no lag

### Test 10: With Multiple Series
1. **Action**: Load a recording with 10+ frequencies
2. **Action**: Make all frequencies visible
3. **Action**: Zoom and pan
4. **Expected**: Still smooth, acceptable performance

---

## ?? Bug Check (2 minutes)

### Test 11: Zoom Badge
1. **Action**: Start fully zoomed out
2. **Verify**: Zoom badge is hidden
3. **Action**: Zoom in to 1.1x
4. **Verify**: Zoom badge appears
5. **Action**: Zoom out to 1.0x
6. **Verify**: Zoom badge disappears

### Test 12: Viewport Synchronization
1. **Action**: Press F3 to show performance stats
2. **Action**: Zoom in/out and pan
3. **Verify**: Chart visuals update in real-time (no stale display)
4. **Verify**: Performance stats show reasonable FPS (target 60)

### Test 13: Keyboard Focus
1. **Action**: Click on chart
2. **Action**: Press keyboard shortcuts
3. **Expected**: All shortcuts work
4. **Action**: Click outside chart
5. **Action**: Press keyboard shortcuts
6. **Expected**: Might not work (needs focus)

---

## ? Success Checklist

After running all tests, confirm:
- [ ] Mouse wheel zoom works smoothly
- [ ] Middle-click pan works smoothly
- [ ] All 13 keyboard shortcuts work
- [ ] Zoom badge appears/disappears correctly
- [ ] Zoom limits are enforced (1 sec min, full rec max)
- [ ] Pan limits are enforced (start/end of recording)
- [ ] No crashes during rapid operations
- [ ] Performance is acceptable (< 50ms feel)
- [ ] Chart updates in real-time
- [ ] No visual glitches or artifacts

---

## ?? Common Issues & Solutions

### Issue: Keyboard shortcuts don't work
**Solution**: Click on the chart area to give it focus first

### Issue: Zoom badge doesn't disappear at 1.0x
**Possible Cause**: Converter threshold is > 1.1, zoom might be at 1.05
**Check**: Look at actual ZoomLevel property value in debugger

### Issue: Pan feels backwards
**This is correct**: Natural scrolling (drag left = content moves right)
**Alternative**: Can be inverted in code if preferred

### Issue: Chart doesn't update during zoom/pan
**Possible Cause**: ViewportChanged event not firing
**Check**: Look for errors in Output window
**Check**: Verify ViewModel.ViewportStart/End are changing

### Issue: Crashes on rapid zoom
**Possible Cause**: DateTime.Ticks overflow
**Check**: Clamping logic in zoom calculations
**Check**: Min/Max limits are being enforced

---

## ?? What to Log/Report

If you find issues, please note:
1. **What you did** (exact steps to reproduce)
2. **What you expected** (correct behavior)
3. **What happened** (actual behavior)
4. **Recording details** (file format, duration, size)
5. **Performance** (any lag, stuttering, or crashes)
6. **Console output** (any errors or warnings)

---

## ?? Success = Ready for Next Phase!

If all tests pass:
- ? Phase 5.1 is validated and complete
- ? Ready to proceed with Action Item 1 (Tile System)
- ? Ready to proceed with Action Item 3 (Minimap)
- ? Ready to proceed with Action Item 4 (Playhead)

**Total Testing Time**: ~12 minutes for thorough validation

---

**Version**: 1.0  
**Last Updated**: January 2025  
**For**: Phase 5.1 Zoom/Pan Implementation
