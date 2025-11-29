# Feature: Mixed UTC and Relative Time Labels on X-Axis

**Date**: January 2025  
**Feature**: X-axis shows UTC time at edges and relative time in middle  
**Status**: ? **IMPLEMENTED**

---

## ?? Feature Description

The X-axis now uses intelligent time labeling:
- **Left edge (start)**: Shows actual UTC start time of recording (e.g., "14:30:00")
- **Right edge (end)**: Shows actual UTC end time of recording (e.g., "14:35:00")
- **Middle labels**: Show relative time from start (e.g., "2:30" for 2 minutes 30 seconds)

This provides both absolute context (when the recording happened) and relative navigation (how far into the recording).

---

## ? Implementation

### Custom Time Labeler

**File**: `src\AeroDebrief.UI\ViewModels\UnifiedGraphViewModel.cs`

```csharp
// In constructor - X-axis configuration
XAxes = new List<Axis>
{
    new Axis
    {
        Name = "Time",
        Labeler = value => FormatTimeLabel(value),  // Custom labeling
        ShowSeparatorLines = true,
        LabelsPaint = new SolidColorPaint(SKColors.White),
        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 1 },
    }
};
```

### Labeling Logic

```csharp
/// <summary>
/// Phase 5.1: Format time label for X-axis.
/// Shows UTC time at edges (start/end) and relative time in middle.
/// </summary>
private string FormatTimeLabel(double seconds)
{
    // Calculate viewport range (what we're currently showing)
    var viewportDuration = (ViewportEnd - ViewportStart).TotalSeconds;
    var fullDuration = (End - Start).TotalSeconds;
    
    // If we're showing the full recording or close to it
    if (Math.Abs(viewportDuration - fullDuration) < 1.0)
    {
        // Full view: Show UTC time at edges
        if (seconds < 1.0)
        {
            return Start.ToString("HH:mm:ss");  // Start edge: "14:30:00"
        }
        else if (seconds > fullDuration - 1.0)
        {
            return End.ToString("HH:mm:ss");    // End edge: "14:35:00"
        }
        else
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");  // Middle: "2:30"
        }
    }
    else
    {
        // Zoomed in: Show all as relative time for clarity
        return TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
    }
}
```

---

## ?? Visual Examples

### Full View (Showing Entire Recording)
```
14:30:00 ?????? 1:00 ???? 2:00 ???? 3:00 ???? 4:00 ?????? 14:35:00
   ?                                                           ?
   ?                                                           ?
UTC Start                                               UTC End
```

**Labels**:
- Far left: `14:30:00` (UTC start time)
- Middle labels: `1:00`, `2:00`, `3:00`, `4:00` (relative time)
- Far right: `14:35:00` (UTC end time)

### Zoomed In View (Showing 1 Minute of Recording)
```
0:30 ???? 0:40 ???? 0:50 ???? 1:00
```

**Labels**:
- All labels show relative time for consistency
- No UTC times shown when zoomed (would be confusing)

---

## ?? Technical Details

### Decision Logic

**When to show UTC times**:
- Viewport showing full recording (or within 1 second of it)
- Only at the very edges (< 1 second from start/end)

**When to show relative times**:
- All middle labels
- All labels when zoomed in

**Why this works**:
1. Gives user context: "This recording is from 14:30"
2. Provides navigation: "I'm 2 minutes into the recording"
3. Avoids confusion when zoomed: All labels use same format

### Edge Detection

```csharp
if (seconds < 1.0)              // First label (within 1 second of start)
if (seconds > fullDuration - 1.0)  // Last label (within 1 second of end)
```

**Why 1 second threshold?**:
- Chart typically shows 5-10 labels
- Edge labels are usually at exactly 0 and fullDuration
- 1 second buffer catches floating-point precision issues

---

## ?? Files Modified

| File | Changes | Lines |
|------|---------|-------|
| `UnifiedGraphViewModel.cs` | Added FormatTimeLabel method + custom Labeler | ~40 |

**Total**: ~40 lines of new code

---

## ? Expected Behavior

### Full View
- [ ] **Left edge**: Shows UTC start time (e.g., "14:30:00")
- [ ] **Middle labels**: Show relative time (e.g., "1:30", "3:00")
- [ ] **Right edge**: Shows UTC end time (e.g., "14:35:00")

### Zoomed In (2x or more)
- [ ] **All labels**: Show relative time (e.g., "0:30", "1:00", "1:30")
- [ ] **No UTC times**: Too confusing when not showing full range

### Pan/Zoom Operations
- [ ] Labels update correctly as viewport changes
- [ ] UTC times only appear when full range visible
- [ ] Smooth transition between label formats

---

## ?? Testing Checklist

### Basic Functionality
- [ ] Load a recording file
- [ ] **Full View**: 
  - [ ] See UTC time at left edge
  - [ ] See relative times in middle
  - [ ] See UTC time at right edge
- [ ] **Zoom In** (mouse wheel):
  - [ ] Labels switch to all relative times
  - [ ] No UTC times shown
- [ ] **Zoom Out** (R key to reset):
  - [ ] UTC times reappear at edges
  - [ ] Middle labels remain relative

### Edge Cases
- [ ] **Very Short Recording** (< 1 minute):
  - [ ] UTC times still show at edges
  - [ ] Middle labels readable
- [ ] **Very Long Recording** (> 1 hour):
  - [ ] UTC times correct
  - [ ] Middle labels scaled appropriately
- [ ] **Pan While Zoomed**:
  - [ ] Labels update correctly
  - [ ] Format remains consistent

### Time Zone Considerations
- [ ] UTC times match recording metadata
- [ ] No local time zone conversion issues
- [ ] Times consistent across UI

---

## ?? Design Rationale

### Why Mixed Formats?

**Problem**: Pure relative time loses context
- User doesn't know when recording happened
- Hard to correlate with logs or events
- Example: "2:30" - is that 14:32:30 or 09:02:30?

**Problem**: Pure UTC time loses navigation
- Hard to calculate: "How far into recording am I?"
- Mental math required: "14:32:30 - 14:30:00 = 2:30"
- Example: "14:32:30" - requires knowing start time

**Solution**: Show both
- UTC at edges: Provides absolute context
- Relative in middle: Provides navigation
- Best of both worlds

### Why Only at Edges?

**Clarity**: Mixing formats across all labels is confusing
- "14:30:00" next to "1:30" next to "14:31:30" - hard to read

**Visual Hierarchy**: Edges are anchors, middle is content
- Edges frame the timeline
- Middle labels are for navigation within that frame

**Chart Real Estate**: Limited space for labels
- Relative times are shorter: "2:30" vs "14:32:30"
- More labels fit on screen

---

## ?? Future Enhancements

### Possible Improvements

1. **Tooltip Enhancement**
   - Show both UTC and relative on hover
   - Example: "2:30 (14:32:30 UTC)"

2. **User Preference**
   - Setting to show UTC only, relative only, or mixed
   - Saved in user preferences

3. **Zoom-Adaptive Format**
   - Very zoomed in: Show milliseconds (e.g., "1:30.500")
   - Very zoomed out: Show hours (e.g., "2h 30m")

4. **Local Time Option**
   - Convert UTC to user's local timezone
   - Show both UTC and local

---

## ?? Summary

**Feature**: Mixed UTC and relative time labels on X-axis

**Benefits**:
- ? Provides absolute context (when recording happened)
- ? Provides relative navigation (position in recording)
- ? Clear, unambiguous labels
- ? Professional appearance

**Implementation**:
- ? Custom `Labeler` function
- ? Intelligent format selection
- ? Edge detection logic
- ? Zoom-aware behavior

**Status**: ? Implemented and ready for testing

---

**Document Version**: 1.0  
**Last Updated**: January 2025  
**Author**: Feature Implementation Session
