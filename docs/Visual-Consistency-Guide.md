# Visual Consistency Guide - Colors & Markers

## Quick Reference for Developers

---

## ?? Frequency Colors

### Rule: Same Frequency = Same Color (Always)

**DO**:
```csharp
// Use ChartColors for ALL frequency color assignment
var frequencyId = $"{frequencyMHz:F1}-{modulation}"; // e.g., "251.0-AM"
var color = ChartColors.GetColorForFrequency(frequencyId);
```

**DON'T**:
```csharp
// ? Don't use sequential/random colors
var colors = new[] { Red, Blue, Green };
var color = colors[index % colors.Length];

// ? Don't create colors based on index
var color = ColorFromHSV(index * 30, 1.0, 1.0);
```

### Locations Using Frequency Colors
- ? FrequencyManager (WaveformColor)
- ? UnifiedGraphViewModel (Series stroke)
- ? FrequencyTreeView (Color indicator)
- ? WaveformViewer (Legacy - will be removed)
- ? Legend (Phase 9)
- ? Minimap (Phase 5)

---

## ?? Pilot Markers

### Rule: Same Pilot = Same Marker (Always)

**DO**:
```csharp
// Use PilotMarkers for ALL pilot marker assignment
var marker = PilotMarkers.GetMarkerForPilot(player.PilotId);

// For WPF UI
var wpfMarker = PilotMarkerHelper.GetSmallMarkerIcon(player, colorBrush);

// For LiveCharts (future)
var geometry = new PilotMarkerGeometry(marker);
series.Geometry = geometry;
```

**DON'T**:
```csharp
// ? Don't use arbitrary shapes
var marker = new Ellipse(); // Everyone gets circle

// ? Don't assign markers sequentially
var shapes = new[] { Circle, Square, Triangle };
var marker = shapes[index % shapes.Length];
```

### Locations Using Pilot Markers
- ? FrequencyTreeView (Next to pilot name)
- ? UnifiedGraphControl (Series geometry) - Phase 9
- ? Legend (Next to pilot name) - Phase 9
- ? Tooltips (Hover display) - Phase 9
- ? Status badges (Optional) - Phase 9

---

## ?? Marker + Color Combination

### Rule: Marker Colored with Frequency Color

**DO**:
```csharp
// Marker uses frequency color, not pilot-specific color
var frequencyColor = ChartColors.GetColorForFrequency(frequencyId);
var marker = PilotMarkerHelper.GetPilotMarkerPath(
    player,
    size: 12.0,
    color: new SolidColorBrush(frequencyColor)
);
```

**Rationale**: Pilots are grouped by frequency. Same frequency = same color family.

**Visual Example**:
```
Frequency 251.0 (Blue color):
  ? Alpha-1  (Blue triangle)
  ? Bravo-2  (Blue circle)
  ? Charlie-3 (Blue square)

Frequency 305.0 (Red color):
  ? Delta-1  (Red triangle)
  ? Echo-2   (Red circle)
  ? Foxtrot-3 (Red square)
```

---

## ?? Size Guidelines

### Standard Sizes
| Context | Size (px) | Method |
|---------|-----------|--------|
| **Small** (Lists, Tree) | 8 | `GetSmallMarkerIcon()` |
| **Medium** (Tooltips) | 12 | `GetMediumMarkerIcon()` |
| **Chart** (Series) | 4-12 | Zoom-dependent |
| **Custom** | Any | `GetPilotMarkerPath(size)` |

### Zoom-Dependent Sizing
```csharp
// In UnifiedGraphViewModel
var markerSize = ZoomLevel > 2.0 ? 4.0 : 0.0;
series.GeometrySize = markerSize;
```

---

## ?? Consistency Checklist

### When Adding a New View
- [ ] Does it display frequency information?
  - [ ] Uses `ChartColors.GetColorForFrequency()`
- [ ] Does it display pilot information?
  - [ ] Uses `PilotMarkers.GetMarkerForPilot()`
  - [ ] Marker colored with frequency color
- [ ] Does it show both frequency and pilot?
  - [ ] Color indicates frequency
  - [ ] Marker shape indicates pilot
- [ ] Is the display consistent with existing views?
  - [ ] Same pilot shows same marker
  - [ ] Same frequency shows same color

### When Modifying Existing Code
- [ ] Don't change `ChartColors` or `PilotMarkers` core logic
- [ ] Don't add custom color/marker assignment
- [ ] Maintain deterministic behavior
- [ ] Preserve caching

---

## ?? Testing Consistency

### Color Consistency Test
```csharp
[Fact]
public void SameFrequency_SameColorInAllViews()
{
    var freqId = "251.0-AM";
    
    // Get color from different contexts
    var color1 = ChartColors.GetColorForFrequency(freqId); // Direct
    var color2 = frequencyManager.GetFrequencyColor(freqId); // Manager
    var color3 = viewModel.GetSeriesColor(freqId); // ViewModel
    
    // Should all be the same
    Assert.Equal(color1, color2);
    Assert.Equal(color2, color3);
}
```

### Marker Consistency Test
```csharp
[Fact]
public void SamePilot_SameMarkerInAllViews()
{
    var pilotId = "Alpha-1";
    
    // Get marker from different contexts
    var marker1 = PilotMarkers.GetMarkerForPilot(pilotId); // Direct
    var marker2 = PilotMarkerHelper.GetSmallMarkerIcon(player); // UI
    
    // Should return same cached instance
    Assert.Same(marker1, ExtractSKPath(marker2));
}
```

---

## ?? Common Mistakes

### Mistake 1: Creating New Colors
```csharp
// ? WRONG
var color = Color.FromRgb(random.Next(255), random.Next(255), random.Next(255));

// ? CORRECT
var color = ChartColors.GetColorForFrequency(frequencyId);
```

### Mistake 2: Sequential Marker Assignment
```csharp
// ? WRONG
var markerType = pilotIndex % 10; // Different pilot might get same marker

// ? CORRECT
var marker = PilotMarkers.GetMarkerForPilot(pilot.PilotId); // Deterministic
```

### Mistake 3: Pilot-Specific Colors
```csharp
// ? WRONG
var pilotColor = GetColorForPilot(pilotId); // Each pilot gets own color

// ? CORRECT
var frequencyColor = ChartColors.GetColorForFrequency(frequencyId);
var marker = PilotMarkerHelper.GetColoredPilotMarker(
    pilot, frequencyHz, modulation, size);
// Marker gets frequency color
```

### Mistake 4: Clearing Caches Unnecessarily
```csharp
// ? WRONG - Don't clear in normal operation
ChartColors.ClearCache();
PilotMarkers.ClearCache();

// ? CORRECT - Only clear in tests or reset scenarios
[TestCleanup]
public void Cleanup()
{
    ChartColors.ClearCache();
    PilotMarkers.ClearCache();
}
```

---

## ?? Code Examples

### Example 1: FrequencyTreeView (Current)
```csharp
// Color indicator
var colorBorder = new Border
{
    Background = new SolidColorBrush(frequency.WaveformColor),
    Width = 12, Height = 12
};

// Pilot marker
var marker = PilotMarkerHelper.GetSmallMarkerIcon(
    player,
    new SolidColorBrush(frequency.WaveformColor)
);
```

### Example 2: Chart Series (Future - Phase 9)
```csharp
// In UnifiedGraphViewModel
var frequencyColor = ChartColors.GetColorForFrequency(frequencyId);
var pilotMarker = PilotMarkers.GetMarkerForPilot(pilotId);

var series = new LineSeries<ObservablePoint>
{
    Name = $"{frequencyId} - {pilotId}",
    Stroke = new SolidColorPaint(frequencyColor),
    Geometry = new PilotMarkerGeometry(pilotMarker),
    GeometryFill = new SolidColorPaint(frequencyColor)
};
```

### Example 3: Legend (Future - Phase 9)
```xaml
<!-- Legend item template -->
<DataTemplate>
    <StackPanel Orientation="Horizontal">
        <!-- Frequency color -->
        <Border Width="12" Height="12" 
                Background="{Binding FrequencyColor}"/>
        
        <!-- Pilot marker -->
        <Path Data="{Binding PilotMarkerGeometry}"
              Fill="{Binding FrequencyColor}"
              Width="12" Height="12"/>
        
        <!-- Label -->
        <TextBlock Text="{Binding Label}"/>
    </StackPanel>
</DataTemplate>
```

---

## ?? Quick Decision Tree

```
Adding visual element for frequency/pilot?
    ?
    ?? Shows frequency only?
    ?   ?? Use ChartColors.GetColorForFrequency()
    ?
    ?? Shows pilot only?
    ?   ?? Use PilotMarkers.GetMarkerForPilot()
    ?       + Color with frequency color
    ?
    ?? Shows both frequency and pilot?
        ?? Color = ChartColors.GetColorForFrequency()
        ?? Marker = PilotMarkers.GetMarkerForPilot()
```

---

## ?? References

- **ChartColors**: `src/AeroDebrief.UI/Charts/ChartColors.cs`
- **PilotMarkers**: `src/AeroDebrief.UI/Charts/PilotMarkers.cs`
- **PilotMarkerHelper**: `src/AeroDebrief.UI/Helpers/PilotMarkerHelper.cs`
- **Implementation Plan**: `docs/Pilot-Marker-Consistency-Plan.md`
- **Tests**: `tests/AeroDebrief.Tests/Charts/`

---

## ? Summary

**Two Golden Rules**:
1. **Frequency = Color** (via ChartColors)
2. **Pilot = Marker Shape** (via PilotMarkers)

**Always Consistent**:
- Same frequency ? Same color (everywhere)
- Same pilot ? Same marker shape (everywhere)
- Marker colored with frequency color (not pilot-specific)

**Result**: Professional, cohesive UI with instant visual recognition.
