# Time Axis Change: Time Offset Instead of Absolute Time

## Date: 2025-01-21
## Context: Phase 2 Day 2 - X-Axis Format

---

## ?? Change Summary

**Changed from**: DateTime (absolute time-of-day)  
**Changed to**: Time offset in seconds from recording start

**Impact**: Better visualization, simpler synchronization, no timezone issues

---

## ?? Before vs After

### Before (DateTime/DateTimePoint)
```
X-Axis: 14:23:45.123, 14:23:45.873, 14:23:46.623, ...
Display: "2:23 PM", "2:24 PM", "2:25 PM", ...
Issues:
  - Timezone dependent
  - Hard to see relative timing
  - Awkward for playback sync
  - Date changes cause problems
```

### After (TimeSpan/ObservablePoint)
```
X-Axis: 0.0, 0.25, 0.5, 0.75, 1.0, ... (seconds)
Display: "00:00", "00:15", "00:30", "00:45", "01:00", ...
Benefits:
  ? Always starts at 0:00:00
  ? Easy to see duration
  ? Simple playback sync
  ? No timezone concerns
  ? Better performance
```

---

## ?? Technical Changes

### 1. AmplitudeExtractor

**Before**:
```csharp
public IEnumerable<DateTimePoint> ExtractEnvelope(AudioPacketMetadata packet)
{
    var timestamp = packet.Timestamp;
    yield return new DateTimePoint(timestamp, dbFS);
}
```

**After**:
```csharp
public IEnumerable<ObservablePoint> ExtractEnvelope(
    AudioPacketMetadata packet, 
    DateTime recordingStart)
{
    var timeOffset = (packet.Timestamp - recordingStart).TotalSeconds;
    yield return new ObservablePoint(timeOffset, dbFS);
}
```

### 2. AmplitudeSeriesProvider

**Interface Change**:
```csharp
// Before
IAsyncEnumerable<(string key, IEnumerable<DateTimePoint> points)> GetSeriesAsync(...)

// After  
IAsyncEnumerable<(string key, IEnumerable<ObservablePoint> points)> GetSeriesAsync(...)
```

**Synthetic Data**:
```csharp
// Before
var currentTime = start;
for (int sample = 0; sample < totalSeconds * 4; sample++)
{
    points.Add(new DateTimePoint(currentTime, amplitude));
    currentTime = currentTime.AddMilliseconds(250);
}

// After
for (int sample = 0; sample < totalSeconds * 4; sample++)
{
    var timeOffsetSeconds = sample * 0.25; // 250ms intervals
    points.Add(new ObservablePoint(timeOffsetSeconds, amplitude));
}
```

### 3. UnifiedGraphViewModel

**Series Creation**:
```csharp
// Before
LineSeries<DateTimePoint>

// After
LineSeries<ObservablePoint>
```

---

## ?? Axis Configuration

### X-Axis Setup (LiveCharts2)
```csharp
// Use TimeSpan axis or numeric axis with custom formatter
XAxes = new[]
{
    new Axis
    {
        Name = "Time",
        Labeler = value => TimeSpan.FromSeconds(value).ToString(@"mm\:ss"),
        MinStep = 1.0, // 1 second minimum
        // For longer recordings, auto-adjust step
    }
};
```

### Y-Axis (unchanged)
```csharp
YAxes = new[]
{
    new Axis
    {
        Name = "Amplitude (dBFS)",
        MinLimit = -120,
        MaxLimit = 0,
        MinStep = 10
    }
};
```

---

## ? Benefits

### 1. **Simplified Visualization**
- Graph always starts at `00:00:00`
- Easy to understand recording duration
- Clear relative timing between events

### 2. **Better Playback Sync**
- Current playback position maps directly to X-axis value
- No time zone conversions needed
- Simpler seek calculations

### 3. **Performance**
- `ObservablePoint` uses `double` for X (not DateTime struct)
- Smaller memory footprint
- Faster comparisons and lookups

### 4. **User Experience**
- Professional audio tool standard (DAWs use time offset)
- Matches waveform displays
- Intuitive for radio communications analysis

---

## ?? Use Cases

### Playback Position
```csharp
// Get current playback offset in seconds
var playbackOffsetSeconds = (currentTime - recordingStart).TotalSeconds;

// Map directly to graph X-axis
var playheadXPosition = playbackOffsetSeconds;
```

### Zooming to Time Range
```csharp
// User wants to see 1:30 to 2:00
var startOffset = TimeSpan.FromMinutes(1.5).TotalSeconds; // 90.0
var endOffset = TimeSpan.FromMinutes(2.0).TotalSeconds;   // 120.0

// Set axis limits
XAxis.MinLimit = startOffset;
XAxis.MaxLimit = endOffset;
```

### Finding Event
```csharp
// User clicks graph at X = 45.3 seconds
var eventOffsetSeconds = 45.3;

// Calculate absolute time
var eventTime = recordingStart + TimeSpan.FromSeconds(eventOffsetSeconds);
```

---

## ?? Data Point Structure

### ObservablePoint
```csharp
public class ObservablePoint
{
    public double X { get; set; }  // Time offset in seconds (0.0, 0.25, 0.5, ...)
    public double Y { get; set; }  // Amplitude in dBFS (-120.0 to 0.0)
}
```

### Series Key Format
```
Format: F{frequency}-P{pilotIndex}
Examples:
  - F251.0-P0  (251.0 MHz, Pilot 0)
  - F305.5-P12 (305.5 MHz, Pilot 12)
```

---

## ?? Example Data

### 30-Second Recording
```
Recording Start: 2024-01-21 14:23:45.000
Recording End:   2024-01-21 14:24:15.000

X-Axis Range: 0.0 to 30.0 seconds
X-Axis Labels: "00:00", "00:05", "00:10", "00:15", "00:20", "00:25", "00:30"

Sample Points:
  ObservablePoint(0.0, -45.2)    // 14:23:45.000
  ObservablePoint(0.25, -38.1)   // 14:23:45.250
  ObservablePoint(0.5, -42.3)    // 14:23:45.500
  ...
  ObservablePoint(29.75, -52.1)  // 14:24:14.750
```

### 2-Hour Recording
```
Recording Start: 2024-01-21 10:00:00.000
Recording End:   2024-01-21 12:00:00.000

X-Axis Range: 0.0 to 7200.0 seconds (2 hours)
X-Axis Labels: "00:00", "15:00", "30:00", "45:00", "1:00:00", "1:15:00", ...

Advantages:
  ? No confusion with wall clock time
  ? Easy to measure event durations
  ? Simple to navigate
```

---

## ?? Migration Notes

### If You Have Existing Code

**Converting DateTime to TimeOffset**:
```csharp
// You have
DateTime eventTime;
DateTime recordingStart;

// Convert to offset
double offsetSeconds = (eventTime - recordingStart).TotalSeconds;
```

**Converting TimeOffset to DateTime**:
```csharp
// You have
double offsetSeconds;
DateTime recordingStart;

// Convert to absolute time
DateTime eventTime = recordingStart + TimeSpan.FromSeconds(offsetSeconds);
```

---

## ?? Related Standards

### Audio Industry Practice
- **DAWs** (Pro Tools, Audacity, etc.): Use time offset
- **Video Editors**: Use timecode from clip start
- **Live Sound**: Use SMPTE timecode
- **Broadcasting**: Use timecode, not wall clock

### Why This Matches Standards
- Audio analysis tools display waveforms starting at 0:00
- Makes recordings portable (no timezone dependency)
- Easier to trim/edit recordings
- Standard for any time-based signal analysis

---

## ? Files Changed

1. **AmplitudeExtractor.cs**
   - Changed return type: `DateTimePoint` ? `ObservablePoint`
   - Added `recordingStart` parameter to calculate offsets
   - Updated all methods to use seconds instead of DateTime

2. **AmplitudeSeriesProvider.cs**
   - Updated interface: `DateTimePoint` ? `ObservablePoint`
   - Modified real data pipeline to pass `recordingStart`
   - Updated synthetic data to start at 0.0 seconds

3. **IAmplitudeSeriesProvider.cs**
   - Updated interface signature
   - Added documentation about time offset format

4. **UnifiedGraphViewModel.cs**
   - Changed series type: `LineSeries<DateTimePoint>` ? `LineSeries<ObservablePoint>`
   - Updated point counting logic

---

## ?? Summary

**Change**: X-axis now uses **time offset in seconds** from recording start  
**Format**: `0.0, 0.25, 0.5, ...` (seconds)  
**Display**: `"00:00", "00:15", "00:30", ...` (MM:SS format)  
**Benefits**: Simpler, faster, more intuitive, industry standard  

**Status**: ? Implemented and building successfully

---

**Date**: 2025-01-21  
**Phase**: 2 Day 2  
**Branch**: `livechart2-integration`
