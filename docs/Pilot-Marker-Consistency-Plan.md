# Pilot Marker Consistency - Implementation Plan

## Date: 2025-01-21
## Status: ?? PLANNED

---

## ?? Objective

Ensure pilot markers are consistently displayed across ALL views in the application, similar to how frequency colors are already consistent.

---

## ?? Current State

### ? Already Implemented
- **FrequencyTreeView**: Pilot markers displayed next to pilot names
- **PilotMarkerHelper**: WPF bridge for marker display
- **PilotMarkers**: 32 unique geometries, deterministic assignment
- **PlayerFrequencyInfo.PilotId**: Consistent identifier

### ? Missing Consistency
- **UnifiedGraphControl/Chart**: Markers not yet applied to series geometries
- **WaveformViewer**: No pilot markers in legacy waveform display
- **Legend**: No pilot markers in chart legend
- **Tooltips**: Markers not shown in hover tooltips
- **Status displays**: No visual marker indicators

---

## ??? Views Requiring Pilot Markers

### 1. UnifiedGraphControl (LiveCharts2)
**Priority**: ?? HIGH
**Status**: ? TODO

**Current**:
- Series created with frequency color
- GeometrySize controlled by zoom level
- No custom geometry (uses default circle)

**Required**:
```csharp
// In UnifiedGraphViewModel.CreateLineSeries()
var series = new LineSeries<ObservablePoint>
{
    Name = $"{frequencyId} - {pilotId}",
    Values = points.ToArray(),
    Stroke = new SolidColorPaint(color) { StrokeThickness = 1.5f },
    
    // ADD: Custom geometry for pilot
    GeometrySize = markerSize,
    GeometryStroke = new SolidColorPaint(color),
    GeometryFill = new SolidColorPaint(color),
    // TODO: Set custom SKPath geometry from PilotMarkers
};
```

**Challenge**: LiveCharts2 geometry customization
- Need to convert SKPath to LiveCharts-compatible format
- May require custom `IGeometry<TDrawingContext>` implementation

---

### 2. Legend (LegendVirtualizedControl)
**Priority**: ?? HIGH
**Status**: ? TODO (Phase 9)

**Current**:
- No legend implementation yet (planned for Phase 9)

**Required**:
```xaml
<!-- For each pilot in legend -->
<StackPanel Orientation="Horizontal">
    <!-- Frequency color indicator -->
    <Border Width="12" Height="12" Background="{Binding FrequencyColor}"/>
    
    <!-- Pilot marker (NEW) -->
    <Path Data="{Binding PilotMarkerGeometry}" 
          Fill="{Binding FrequencyColor}"
          Width="12" Height="12"/>
    
    <!-- Pilot name -->
    <TextBlock Text="{Binding PilotName}"/>
</StackPanel>
```

**Implementation**:
- Add `PilotMarkerGeometry` property to legend item ViewModel
- Use `PilotMarkerHelper.GetSmallMarkerIcon()` to create Path
- Bind to frequency color for consistency

---

### 3. Chart Tooltips
**Priority**: ?? MEDIUM
**Status**: ? TODO (Phase 9)

**Current**:
- Tooltips disabled for performance (`TooltipPosition = Hidden`)

**Required** (when tooltips enabled):
```csharp
// Custom tooltip content
var tooltip = new StackPanel();

// Pilot marker
var marker = PilotMarkerHelper.GetSmallMarkerIcon(player, colorBrush);
tooltip.Children.Add(marker);

// Pilot info
var info = new TextBlock { Text = $"{pilotName}: {value:F1} dBFS" };
tooltip.Children.Add(info);
```

---

### 4. FrequencyTreeView (? DONE)
**Priority**: ? COMPLETE
**Status**: ? IMPLEMENTED

**Current**:
```csharp
// In CreatePlayerInfoPanel()
var markerIcon = PilotMarkerHelper.GetSmallMarkerIcon(
    player,
    new SolidColorBrush(frequency.WaveformColor)
);
```

**Layout**:
```
[Coalition Dot] [Pilot Marker] [Name] [Aircraft] ...
```

---

### 5. Status Badges / Headers
**Priority**: ?? LOW
**Status**: ? TODO (Optional)

**Potential Uses**:
- Player header control (show active pilot marker)
- Status overlay (show current speaker marker)
- Frequency mixer panel (marker next to frequency name)

---

### 6. WaveformViewer (Legacy)
**Priority**: ?? LOW (Will be replaced)
**Status**: ? SKIP (Legacy code removal planned)

**Note**: Legacy waveform viewer will be removed in Phase 11. No need to add markers here.

---

## ?? Implementation Strategy

### Phase 5 Additions (Minimap & Zoom UX)
- ? Ensure minimap uses same marker logic as main chart
- ? Markers should appear/disappear based on zoom in both views

### Phase 6 Additions (Playhead & Seek Sync)
- ? No marker-specific changes needed
- ? Existing marker implementation compatible

### Phase 7 Additions (Visibility Toggles)
- ? Ensure marker visibility follows pilot visibility
- ? Grayed-out or hidden markers for hidden pilots

### Phase 8 Additions (Collision Overlay)
- ? Show all pilot markers involved in collision
- ? Highlight markers when collision detected

### Phase 9 Additions (Progress & UX Polish)
- ?? **Implement legend with pilot markers**
- ?? **Add marker display to chart series**
- ?? Add markers to tooltips (if tooltips enabled)
- ?? Add markers to status displays (optional)

---

## ?? Marker Consistency Rules

### Rule 1: Deterministic Assignment
```csharp
// Same pilot ID = same marker ALWAYS
var marker1 = PilotMarkers.GetMarkerForPilot("Alpha-1");
var marker2 = PilotMarkers.GetMarkerForPilot("Alpha-1");
Assert.Same(marker1, marker2); // Cached
```

### Rule 2: Frequency Color Applied
```csharp
// Marker should use frequency color, not pilot-specific color
var markerColor = ChartColors.GetColorForFrequency(frequencyId);
var marker = PilotMarkerHelper.GetColoredPilotMarker(
    player, frequencyHz, modulation, size: 12.0
);
// marker.Fill will be frequency color
```

### Rule 3: Size Consistency
| Context | Size | Method |
|---------|------|--------|
| Legend | 8px | `GetSmallMarkerIcon()` |
| Tree View | 8px | `GetSmallMarkerIcon()` |
| Chart Series | 4-12px | Zoom-dependent |
| Tooltips | 12px | `GetMediumMarkerIcon()` |
| Status | 12px | `GetMediumMarkerIcon()` |

### Rule 4: Visibility Follows Pilot
```csharp
// When pilot hidden, marker should be hidden or grayed
if (!pilot.IsSelected)
{
    marker.Opacity = 0.3; // or Visibility = Collapsed
}
```

---

## ?? Technical Challenges

### Challenge 1: LiveCharts2 Custom Geometry
**Problem**: LiveCharts2 uses `IGeometry<TDrawingContext>` interface, not WPF Path

**Solutions**:
1. **Option A**: Custom `IGeometry<SkiaSharpDrawingContext>` implementation
   ```csharp
   public class PilotMarkerGeometry : IGeometry<SkiaSharpDrawingContext>
   {
       private readonly SKPath _path;
       
       public void Draw(SkiaSharpDrawingContext context, SKPaint paint)
       {
           context.Canvas.DrawPath(_path, paint);
       }
   }
   ```

2. **Option B**: Use existing LiveCharts geometries with color coding
   - Circle for pilot 1, Square for pilot 2, Triangle for pilot 3, etc.
   - Limited to ~10-15 built-in geometries

3. **Option C**: Wait for LiveCharts2 custom geometry support
   - Check if newer version has this feature
   - May be in development

**Recommendation**: Start with Option B (built-in geometries) for Phase 5-7, implement Option A in Phase 9 if needed.

---

### Challenge 2: WPF Path to LiveCharts Conversion
**Problem**: `PilotMarkerHelper` creates WPF Path, LiveCharts uses SkiaSharp

**Solution**: 
- Keep existing SKPath from `PilotMarkers.GetMarkerForPilot()`
- Create LiveCharts wrapper that uses SKPath directly
- No need for WPF conversion in chart context

```csharp
// For UI (WPF)
var wpfPath = PilotMarkerHelper.GetSmallMarkerIcon(player);

// For Charts (SkiaSharp)
var skPath = PilotMarkers.GetMarkerForPilot(player.PilotId);
var geometry = new PilotMarkerGeometry(skPath);
series.Geometry = geometry;
```

---

### Challenge 3: Performance with Many Markers
**Problem**: 60 frequencies × 4-24 pilots × markers = high draw call count

**Mitigation**:
- ? Already implemented: Zoom-based marker density
- ? Already implemented: Auto-collapse high-pilot frequencies
- ? Future: Instancing/batching for same marker type
- ? Future: LOD (Level of Detail) - simpler markers when zoomed out

---

## ?? Action Items

### Immediate (Phase 5)
- [ ] Document marker consistency requirements
- [ ] Test LiveCharts2 custom geometry support
- [ ] Prototype custom `IGeometry` if needed

### Phase 9 (UX Polish)
- [ ] Implement `LegendVirtualizedControl` with markers
- [ ] Add pilot markers to chart series
- [ ] Create `PilotMarkerGeometry` class for LiveCharts
- [ ] Add markers to tooltips (if enabled)
- [ ] Update all documentation with marker consistency

### Testing
- [ ] Add test: "Same pilot shows same marker in all views"
- [ ] Add test: "Marker color matches frequency color"
- [ ] Add test: "Marker visible/hidden follows pilot state"
- [ ] Add visual regression tests for marker display

---

## ?? Success Criteria

### Definition of Done
- [ ] Pilot markers displayed in chart series
- [ ] Pilot markers displayed in legend
- [ ] Same pilot = same marker across all views
- [ ] Marker color = frequency color (consistent)
- [ ] Marker visibility follows pilot visibility
- [ ] Performance acceptable (60 FPS with markers)
- [ ] Tests validate consistency
- [ ] Documentation updated

### Visual Consistency Check
```
FrequencyTreeView: [?] Alpha-1
Chart Series:      [?] shows same marker shape
Legend:            [?] Alpha-1 (same marker)
Tooltip:           [?] Alpha-1: -40.5 dBFS (same marker)
```

---

## ?? References

### Existing Implementation
- `PilotMarkers.cs` - SKPath geometry generation
- `PilotMarkerHelper.cs` - WPF bridge
- `ChartColors.cs` - Color consistency model (follow this pattern)
- `FrequencyTreeView.cs` - Current marker display

### LiveCharts2 Documentation
- Custom geometries: https://lvcharts.com/docs/WPF/latest/CartesianChart.Geometries
- Series styling: https://lvcharts.com/docs/WPF/latest/CartesianChart.Series

---

## ?? Expected Outcome

After implementation, users will see:
- **Consistent visual language**: Same pilot = same marker everywhere
- **Quick identification**: "That triangle is always Alpha-1"
- **Professional appearance**: Polished, cohesive UI
- **Better UX**: No confusion about pilot identity

**Example**:
```
Frequency Tree:  ?? 251.0 MHz
                    ? Alpha-1 (triangle)
                    ? Bravo-2 (circle)
                    ? Charlie-3 (square)

Chart:           [Line with ? markers for Alpha-1]
                 [Line with ? markers for Bravo-2]
                 [Line with ? markers for Charlie-3]

Legend:          ? Alpha-1   ? Bravo-2   ? Charlie-3
```

---

**Status**: Plan documented, ready for Phase 9 implementation
**Priority**: HIGH (Core UX feature)
**Estimate**: 1-2 days in Phase 9
