# Phase 4 Feature Reference - Quick Guide

## Overview
Phase 4 implements the core unified chart MVP with colors, markers, and visibility management.

---

## ChartColors API

### Get Color for Frequency
```csharp
using AeroDebrief.UI.Charts;

var color = ChartColors.GetColorForFrequency("251.0-AM");
// Returns: SKColor (deterministic, cached)
```

### Get Color with Opacity
```csharp
var mutedColor = ChartColors.GetColorWithAlpha("251.0-AM", alpha: 128);
// Returns: SKColor with 50% opacity
```

### Palette Info
```csharp
var size = ChartColors.PaletteSize; // 30 colors
var allColors = ChartColors.GetAllColors(); // IReadOnlyList<SKColor>
```

### Cache Management
```csharp
ChartColors.ClearCache(); // Reset color assignments (testing)
```

---

## PilotMarkers API

### Get Marker for Pilot
```csharp
using AeroDebrief.UI.Charts;

var marker = PilotMarkers.GetMarkerForPilot("Alpha-1", size: 8f);
// Returns: SKPath (deterministic geometry, cached)
```

### Available Markers
- **32 unique geometries** including:
  - Basic: Circle, Square, Triangle, Diamond
  - Polygons: Pentagon, Hexagon, Heptagon, Octagon
  - Stars: 4pt, 5pt, 6pt, 8pt
  - Special: Heart, Arrow, Chevron, etc.

### Marker Info
```csharp
var count = PilotMarkers.MarkerTypeCount; // 32
```

### Cache Management
```csharp
PilotMarkers.ClearCache(); // Dispose all geometries (testing)
```

---

## UnifiedGraphViewModel API

### Initialization
```csharp
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Graphs;

var provider = new AmplitudeSeriesProvider(source, engine);
var cache = new DataTileCache(budgetMB: 300.0);
var viewModel = new UnifiedGraphViewModel(provider, cache);
```

### Load Data
```csharp
var start = DateTime.Now;
var end = start.AddMinutes(10);
await viewModel.LoadDataAsync(start, end);

Console.WriteLine($"Loaded {viewModel.VisibleSeriesCount} series");
Console.WriteLine($"Total points: {viewModel.TotalPoints}");
```

### Frequency Visibility
```csharp
// Hide entire frequency (all pilots)
viewModel.SetFrequencyVisible("251.0", visible: false);

// Show frequency
viewModel.SetFrequencyVisible("251.0", visible: true);
```

### Pilot Visibility
```csharp
// Hide specific pilot
viewModel.SetPilotVisible("251.0", "Alpha-1", visible: false);

// Show specific pilot
viewModel.SetPilotVisible("251.0", "Alpha-1", visible: true);
```

### Expand/Collapse High-Pilot Frequencies
```csharp
// Configure collapse threshold
viewModel.MaxPilotsPerFrequency = 8; // Default

// Expand frequency to show all pilots
viewModel.ExpandFrequency("305.0", expanded: true);

// Collapse back to top N
viewModel.ExpandFrequency("305.0", expanded: false);
```

### Zoom Control
```csharp
// Adjust zoom level (affects marker visibility)
viewModel.ZoomLevel = 1.0; // Zoomed out, no markers
viewModel.ZoomLevel = 3.5; // Zoomed in, show markers (>2.0)
```

### FrequencyManager Integration
```csharp
var frequencyManager = new FrequencyManager();
viewModel.ConnectToFrequencyManager(frequencyManager);
// Selection changes in FrequencyManager now update chart visibility
```

### Properties
```csharp
// Read-only
int visibleCount = viewModel.VisibleSeriesCount;
int totalPoints = viewModel.TotalPoints;
DateTime start = viewModel.Start;
DateTime end = viewModel.End;

// Read-write
viewModel.ZoomLevel = 2.5;
viewModel.MaxPilotsPerFrequency = 10;
```

---

## Series Key Format

### Format
`"FrequencyId-PilotId"`

### Examples
- `"251.0-Alpha-1"`
- `"305.0-Bravo-2"`
- `"F127.5-P01"`

### Parsing
```csharp
var key = "251.0-Alpha-1";
var parts = key.Split('-', 2);
var frequencyId = parts[0]; // "251.0"
var pilotId = parts[1];     // "Alpha-1"
```

---

## Density Management

### Behavior
- **Low-pilot frequencies** (? MaxPilotsPerFrequency): All pilots visible
- **High-pilot frequencies** (> MaxPilotsPerFrequency): Top N pilots visible, others collapsed

### Example
```csharp
viewModel.MaxPilotsPerFrequency = 8;

// Load frequency with 4 pilots ? all 4 visible
// Load frequency with 20 pilots ? only top 8 visible

// User can expand to see all 20
viewModel.ExpandFrequency("F305.0", expanded: true);
```

### Current Top-N Selection
First N pilots in the list (simple). TODO: Rank by activity (talk time, energy).

---

## LiveCharts Series Creation

### Series Properties
```csharp
var series = new LineSeries<ObservablePoint>
{
    Name = "251.0 - Alpha-1",
    Values = points.ToArray(),
    GeometrySize = 0, // Zoom-dependent (0 or 4)
    LineSmoothness = 0, // Sharp lines
    Fill = null, // No area fill
    Stroke = new SolidColorPaint(color) { StrokeThickness = 1.5f },
    GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 1 },
    GeometryFill = new SolidColorPaint(color)
};
```

### Marker Control
- **Zoom < 2.0**: `GeometrySize = 0` (no markers)
- **Zoom ? 2.0**: `GeometrySize = 4` (show markers)

---

## Testing Examples

### Test Color Consistency
```csharp
[Fact]
public void ColorConsistency()
{
    var color1 = ChartColors.GetColorForFrequency("251.0");
    var color2 = ChartColors.GetColorForFrequency("251.0");
    Assert.Equal(color1, color2);
}
```

### Test Marker Uniqueness
```csharp
[Fact]
public void MarkerUniqueness()
{
    var marker1 = PilotMarkers.GetMarkerForPilot("P1");
    var marker2 = PilotMarkers.GetMarkerForPilot("P2");
    Assert.NotEqual(marker1.Bounds, marker2.Bounds);
}
```

### Test Visibility Toggle
```csharp
[Fact]
public async Task VisibilityToggle()
{
    var vm = new UnifiedGraphViewModel(provider);
    await vm.LoadDataAsync(start, end);
    
    var initial = vm.VisibleSeriesCount;
    vm.SetFrequencyVisible("251.0", false);
    Assert.True(vm.VisibleSeriesCount < initial);
}
```

---

## Performance Tips

### Memory
- ChartColors and PilotMarkers use caching ? first access slower, subsequent fast
- Series are reused on visibility toggles ? no data reload
- Call `ClearCache()` only when truly resetting (not during normal operation)

### Rendering
- Keep `MaxPilotsPerFrequency` reasonable (8-12) for performance
- Marker density auto-adjusts with zoom to reduce draw calls
- Use `ExpandFrequency()` judiciously for high-pilot cases

### Visibility
- Batch visibility changes if possible (future: batch API)
- Rebuilding visible series is O(visible), not O(total)

---

## Common Patterns

### Load and Display
```csharp
var vm = new UnifiedGraphViewModel(provider, cache);
await vm.LoadDataAsync(DateTime.Now, DateTime.Now.AddMinutes(10));

// All series loaded and visible (subject to density management)
Console.WriteLine($"{vm.VisibleSeriesCount} series ready");
```

### Frequency Tree Sync
```csharp
// In FrequencyManager.SelectionChanged handler:
private void OnSelectionChanged(object sender, FrequencySelectionChangedEventArgs e)
{
    viewModel.SetFrequencyVisible(e.Frequency.ToString(), e.IsSelected);
}
```

### Dynamic Zoom Response
```csharp
// In zoom slider ValueChanged handler:
private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    viewModel.ZoomLevel = e.NewValue;
    // Marker density updates automatically
}
```

### Expand UI
```csharp
// In frequency item context menu or expander control:
private void OnExpandFrequency(string frequencyId, bool expanded)
{
    viewModel.ExpandFrequency(frequencyId, expanded);
    UpdateUI(); // Refresh pilot list display
}
```

---

## Integration Checklist

### ViewModel Setup
- ? Create `UnifiedGraphViewModel` with provider and cache
- ? Call `LoadDataAsync()` when data source ready
- ? Wire up `ConnectToFrequencyManager()` for selection sync

### UI Binding
- ? Bind chart `Series` to `viewModel.Series`
- ? Bind zoom control to `viewModel.ZoomLevel`
- ? Display `viewModel.VisibleSeriesCount` in status

### Visibility Controls
- ? Wire frequency checkboxes to `SetFrequencyVisible()`
- ? Wire pilot checkboxes to `SetPilotVisible()`
- ? Add expand/collapse buttons for high-pilot frequencies

### Legend
- ? Update `LegendVirtualizedControl` to show pilot markers
- ? Display marker geometry next to pilot name
- ? Color-code by frequency

---

## Troubleshooting

### Colors Not Consistent
- Ensure using same frequency ID format
- Check cache hasn't been incorrectly cleared
- Verify hash function stability (should be deterministic)

### Markers Not Showing
- Check `viewModel.ZoomLevel` (must be > 2.0)
- Verify series `GeometrySize` is non-zero
- Confirm marker geometry is valid (non-empty SKPath)

### Visibility Toggle Not Working
- Verify series key format matches `"FreqId-PilotId"`
- Check `_seriesVisibility` dictionary contains the key
- Ensure `RebuildVisibleSeries()` is called

### High Memory Usage
- Reduce `MaxPilotsPerFrequency` to collapse more frequencies
- Check marker cache size (shouldn't exceed a few hundred)
- Verify series are reused, not recreated on toggles

---

## Future Enhancements

### Phase 5+ (Not Yet Implemented)
- ? Custom marker geometries in LiveCharts series
- ? Activity-based top-N pilot ranking
- ? Batch visibility API for performance
- ? Tile-based decimation integration
- ? Collision overlay rendering
- ? Legend with marker display

---

## Summary

**Phase 4 provides:**
- Deterministic color palette (30 colors)
- Unique marker geometries (32 types)
- Visibility management (frequency + pilot level)
- Density-aware rendering (auto-collapse)
- Zoom-based marker control
- FrequencyManager integration hooks

**Ready for Phase 5:**
- Minimap implementation
- Viewport synchronization
- Zoom/pan UX refinement
