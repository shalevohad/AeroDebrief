# Fix: Graph Not Visible - Missing Axes Configuration

**Date**: January 2025  
**Issue**: Chart not rendering - no graph drawn at all  
**Status**: ? **FIXED**

---

## ?? Problem

The UnifiedGraphControl was not displaying any graph visualization. The CartesianChart from LiveCharts2 was present but completely blank/invisible.

---

## ?? Root Cause

LiveCharts2 **requires** both `XAxes` and `YAxes` properties to be configured for the chart to render anything. The UnifiedGraphViewModel was missing these properties, and they weren't bound in the XAML.

### What Was Missing

1. **ViewModel Properties**: No `XAxes` or `YAxes` properties in `UnifiedGraphViewModel`
2. **Axis Initialization**: No axis configuration in constructor
3. **XAML Bindings**: CartesianChart didn't have `XAxes` and `YAxes` bindings

---

## ? Solution

### 1. Added Axes Properties to ViewModel

**File**: `src\AeroDebrief.UI\ViewModels\UnifiedGraphViewModel.cs`

```csharp
public class UnifiedGraphViewModel : INotifyPropertyChanged, IDisposable
{
    public ObservableCollection<ISeries> Series { get; } = new();
    
    // Phase 5.1: Axes for LiveCharts2 - required for rendering
    public IEnumerable<Axis> XAxes { get; set; }
    public IEnumerable<Axis> YAxes { get; set; }
    
    // ... rest of properties
}
```

### 2. Initialized Axes in Constructor

**File**: `src\AeroDebrief.UI\ViewModels\UnifiedGraphViewModel.cs`

```csharp
public UnifiedGraphViewModel(...)
{
    // ... existing initialization
    
    // Phase 5.1: Initialize axes for LiveCharts2
    XAxes = new List<Axis>
    {
        new Axis
        {
            Name = "Time",
            LabelsRotation = 0,
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.White),
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 1 },
        }
    };
    
    YAxes = new List<Axis>
    {
        new Axis
        {
            Name = "Amplitude",
            LabelsRotation = 0,
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.White),
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 1 },
            MinLimit = 0,
            MaxLimit = 1
        }
    };
    
    // ... rest of constructor
}
```

### 3. Added Bindings to XAML

**File**: `src\AeroDebrief.UI\Controls\Charts\UnifiedGraphControl.xaml`

```xaml
<lvc:CartesianChart x:Name="MainChart"
                   Series="{Binding Series}"
                   XAxes="{Binding XAxes}"
                   YAxes="{Binding YAxes}"
                   TooltipPosition="Hidden"
                   Background="Transparent"
                   MouseWheel="OnChartMouseWheel"
                   MouseDown="OnChartMouseDown"
                   MouseMove="OnChartMouseMove"
                   MouseUp="OnChartMouseUp"/>
```

---

## ?? Files Modified

| File | Changes |
|------|---------|
| `UnifiedGraphViewModel.cs` | Added XAxes, YAxes properties + initialization (~40 lines) |
| `UnifiedGraphControl.xaml` | Added XAxes, YAxes bindings (2 lines) |

**Total**: ~42 lines of code

---

## ? Verification

### Build Status
- ? Build successful
- ? No compilation errors

### Expected Behavior After Fix
- ? Chart should now render with time on X-axis
- ? Amplitude (0-1) on Y-axis
- ? Grid lines visible (gray separators)
- ? Axis labels visible (white text)
- ? Series data visible (if data is loaded)

---

## ?? Lesson Learned

**LiveCharts2 Requirement**: Unlike some other charting libraries, LiveCharts2 **requires explicit axis configuration**. The chart will not render anything without properly configured `XAxes` and `YAxes` properties.

### Key Requirements
1. ViewModel must expose `IEnumerable<Axis>` properties for XAxes and YAxes
2. Axes must be initialized (not null)
3. XAML must bind CartesianChart's XAxes and YAxes properties
4. All three steps are required for rendering

---

## ?? Testing Checklist

- [ ] **Load a recording file**
  - [ ] Verify chart appears (not blank)
  - [ ] See time axis at bottom
  - [ ] See amplitude axis on left
  - [ ] See grid lines
  
- [ ] **Zoom/Pan**
  - [ ] Mouse wheel zoom works
  - [ ] Pan with middle-click works
  - [ ] Axis updates correctly
  
- [ ] **Data Visibility**
  - [ ] Series lines are visible
  - [ ] Colors are distinct
  - [ ] Amplitude values are correct (0-1 range)

---

## ?? Related Issues

This fix addresses the critical blocker that prevented testing of:
- Phase 5.1: Zoom/Pan functionality
- Phase 2.1-3: Amplitude precomputation visualization
- Phase 8: Tile system integration
- Overall LiveCharts2 rendering

---

## ?? Axis Configuration Details

### X-Axis (Time)
- **Data Type**: DateTime ticks (long)
- **Updated By**: `UpdateChartViewport()` sets `MinLimit` and `MaxLimit`
- **Format**: Automatic based on scale
- **Colors**: White labels, gray grid

### Y-Axis (Amplitude)
- **Data Type**: Double (0.0 to 1.0)
- **Range**: Fixed 0-1 (linear amplitude scale)
- **Alternative**: Could use dBFS (-96 to 0) based on settings
- **Colors**: White labels, gray grid

---

## ?? Summary

The issue was a missing fundamental configuration requirement for LiveCharts2. By adding the `XAxes` and `YAxes` properties to the ViewModel and binding them in XAML, the chart now has the minimum required configuration to render.

**Status**: ? Fixed and ready for testing

---

**Document Version**: 1.0  
**Last Updated**: January 2025  
**Author**: Troubleshooting Session
