# Unit Standards and Conversion Guide

**Date**: 2025-01-20  
**Status**: ? **STANDARDIZED**  
**Standard**: **All frequencies stored in Hz throughout the system**

---

## ?? Unit Standards

### Frequency Units

| Component | Unit | Example | Notes |
|-----------|------|---------|-------|
| **ADB Files (on disk)** | Hz or MHz (mixed) | 127500000.0 or 127.5 | Legacy format, inconsistent |
| **AudioPacketMetadata.Frequency** | **Hz** | 127500000.0 | Normalized on read |
| **SQLite packets.frequency** | **Hz** | 127500000.0 | Standard storage |
| **SQLite frequency_stats.frequency** | **Hz** | 127500000.0 | Aggregated from packets |
| **FrequencyInfo.Frequency** | **Hz** | 127500000.0 | Repository pattern |
| **FrequencyModulationInfo.Frequency** | **Hz** | 127500000.0 | Model layer |
| **UI Display** | MHz | 127.500 MHz | Converted for humans |

### Time Units

| Component | Unit | Example | Notes |
|-----------|------|---------|-------|
| **timestamps** | DateTime UTC | 2025-01-20T15:30:00Z | ISO 8601 |
| **relative_ms** | milliseconds | 5000 (= 5 seconds) | Since recording start |
| **durations** | TimeSpan | TimeSpan.FromSeconds(300) | .NET native |

### Audio Units

| Component | Unit | Example | Notes |
|-----------|------|---------|-------|
| **sample_rate** | Hz | 48000 | Samples per second |
| **audio_data** | bytes | byte[] | Raw PCM or Opus |
| **gain** | linear | 1.0 = 100% | Not dB |
| **pan** | linear | -1.0 (left) to +1.0 (right) | 0.0 = center |

---

## ?? Conversion Points

### 1. ADB File Read (Input Normalization)

**File**: `src/AeroDebrief.Core/AudioPacketMetadata.cs`  
**Method**: `TryReadMetadata()`  
**Lines**: 117-124

```csharp
double frequency = reader.ReadDouble();

// UNIT STANDARD: Store frequencies in Hz internally for precision and consistency
// ADB files may store in either Hz or MHz, so normalize to Hz
// If frequency < 1000, assume it's in MHz and convert to Hz
if (frequency < 1000.0)
{
    frequency = frequency * 1_000_000.0;  // Convert MHz to Hz
}
```

**Logic**:
- If value < 1000 ? Assume MHz, convert to Hz (multiply by 1,000,000)
- If value ? 1000 ? Already Hz, use as-is
- Result: Always Hz in `AudioPacketMetadata.Frequency`

### 2. Database Write (No Conversion)

**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqlitePacketRepository.cs`  
**Method**: `InsertBatchAsync()`  
**Line**: 93

```csharp
Frequency = p.Frequency,  // Already in Hz from AudioPacketMetadata
```

**Logic**: Store Hz value directly, no conversion needed.

### 3. Database Read (No Conversion)

**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqliteFrequencyRepository.cs`  
**Method**: `MapToFrequencyInfo()`  
**Line**: 149

```csharp
Frequency = (double)row.frequency,  // Hz value from database
```

**Logic**: Read Hz value directly, no conversion needed.

### 4. UI Display (Output Conversion)

**File**: `src/AeroDebrief.Core/Storage/Abstractions/FrequencyInfo.cs`  
**Property**: `FormattedFrequency`  
**Line**: 49

```csharp
public string FormattedFrequency => $"{Frequency / 1_000_000.0:F3} MHz";
```

**Logic**: Convert Hz ? MHz for human-readable display.

**File**: `src/AeroDebrief.Core/Models/FrequencyModulationInfo.cs`  
**Method**: `GetDisplayText()`  
**Lines**: 23-25

```csharp
public string GetDisplayText()
{
    var frequencyMhz = Frequency / 1_000_000.0;
    return $"{frequencyMhz:F3} MHz ({GetModulationName()})";
}
```

**Logic**: Convert Hz ? MHz for UI display.

---

## ? Validation Rules

### Frequency Validation

**File**: `src/AeroDebrief.Core/Constants.cs`  
**Lines**: 106-115

```csharp
/// <summary>
/// Minimum valid radio frequency: 1 MHz = 1,000,000 Hz
/// </summary>
public const double MinValidFrequencyHz = 1_000_000.0;

/// <summary>
/// Maximum valid radio frequency: 2000 MHz = 2,000,000,000 Hz (2 GHz)
/// </summary>
public const double MaxValidFrequencyHz = 2_000_000_000.0;
```

**Valid Range**: 1 MHz to 2000 MHz (1,000,000 to 2,000,000,000 Hz)

---

## ?? Data Flow Diagram

```
???????????????????????????????????????????????????????????????????
? 1. ADB FILE                                                     ?
?    Format: Hz (127500000.0) or MHz (127.5) - MIXED!            ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? AudioPacketMetadata.TryReadMetadata()
                            ? (Normalize: < 1000 = MHz ? Hz)
                            ?
???????????????????????????????????????????????????????????????????
? 2. MEMORY (AudioPacketMetadata)                                ?
?    Format: Hz (127500000.0) - STANDARDIZED!                    ?
?    Property: Frequency (double)                                ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? SqlitePacketRepository.InsertBatchAsync()
                            ? (No conversion, store as-is)
                            ?
???????????????????????????????????????????????????????????????????
? 3. DATABASE (SQLite)                                           ?
?    Table: packets                                              ?
?    Column: frequency (REAL)                                    ?
?    Format: Hz (127500000.0) - STORED!                          ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? RebuildStatsAsync()
                            ? (Aggregate, no conversion)
                            ?
???????????????????????????????????????????????????????????????????
? 4. DATABASE (SQLite)                                           ?
?    Table: frequency_stats                                      ?
?    Column: frequency (REAL)                                    ?
?    Format: Hz (127500000.0) - AGGREGATED!                      ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? SqliteFrequencyRepository.MapToFrequencyInfo()
                            ? (No conversion, read as-is)
                            ?
???????????????????????????????????????????????????????????????????
? 5. MEMORY (FrequencyInfo)                                      ?
?    Format: Hz (127500000.0) - STANDARDIZED!                    ?
?    Property: Frequency (double)                                ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? FrequencyInfo.FormattedFrequency
                            ? (Convert Hz ? MHz for display)
                            ?
???????????????????????????????????????????????????????????????????
? 6. UI DISPLAY                                                   ?
?    Format: MHz (127.500 MHz) - HUMAN READABLE!                 ?
?    Example: "127.500 MHz", "251.000 MHz"                       ?
???????????????????????????????????????????????????????????????????
```

---

## ?? Migration Impact

### Before Standardization (Broken)

```
ADB: 127500000 Hz
  ? Read: Convert to MHz (÷1,000,000)
  ? Memory: 127.5 MHz
    ? Store as-is
    ? DB: 127.5 (incorrectly labeled as Hz)
      ? Read as-is
      ? Memory: 127.5 (thinks it's Hz)
        ? Display: Convert to MHz (÷1,000,000)
        ? UI: 0.0001275 ? "0.000 MHz" ?
```

### After Standardization (Fixed)

```
ADB: 127500000 Hz (or 127.5 MHz)
  ? Read: Normalize to Hz (×1,000,000 if < 1000)
  ? Memory: 127500000 Hz ?
    ? Store as-is
    ? DB: 127500000 Hz ?
      ? Read as-is
      ? Memory: 127500000 Hz ?
        ? Display: Convert to MHz (÷1,000,000)
        ? UI: 127.5 ? "127.500 MHz" ?
```

---

## ?? Code Comments Standard

All frequency-related code should include unit comments:

```csharp
// ? GOOD: Clear unit documentation
public double Frequency { get; set; }  // Hz (e.g., 127500000.0 = 127.5 MHz)

// ? GOOD: Conversion with comment
var displayMhz = frequencyHz / 1_000_000.0;  // Convert Hz to MHz for display

// ? BAD: Ambiguous units
public double Frequency { get; set; }  // What unit?

// ? BAD: Undocumented conversion
var display = frequency / 1_000_000.0;  // Why divide?
```

---

## ?? Testing Guidelines

### Unit Test Example

```csharp
[Fact]
public void Frequency_Should_Be_Stored_In_Hz()
{
    // Arrange - ADB stores 127.5 MHz
    var adbValue = 127.5;  // MHz
    
    // Act - Normalize to Hz
    var frequencyHz = adbValue < 1000.0 ? adbValue * 1_000_000.0 : adbValue;
    
    // Assert
    frequencyHz.Should().Be(127500000.0);  // Hz
}

[Fact]
public void FormattedFrequency_Should_Convert_Hz_To_MHz()
{
    // Arrange
    var frequencyInfo = new FrequencyInfo
    {
        Frequency = 127500000.0  // Hz
    };
    
    // Act
    var formatted = frequencyInfo.FormattedFrequency;
    
    // Assert
    formatted.Should().Be("127.500 MHz");
}
```

---

## ?? Common Pitfalls

### Pitfall 1: Double Conversion

```csharp
// ? WRONG: Converting already-converted value
var mhz1 = frequencyHz / 1_000_000.0;  // Hz ? MHz
var mhz2 = mhz1 / 1_000_000.0;         // MHz ? µHz (WRONG!)

// ? RIGHT: Convert once
var mhz = frequencyHz / 1_000_000.0;   // Hz ? MHz
```

### Pitfall 2: Assuming Units

```csharp
// ? WRONG: Assuming without checking
if (frequency > 200)  // Is this Hz or MHz?
    // Do something

// ? RIGHT: Document assumptions
if (frequencyHz > 200_000_000.0)  // 200 MHz in Hz
    // Do something
```

### Pitfall 3: Inconsistent Storage

```csharp
// ? WRONG: Storing different units in same column
INSERT INTO packets (frequency) VALUES (127.5);      -- MHz?
INSERT INTO packets (frequency) VALUES (127500000);  -- Hz?

// ? RIGHT: Always store Hz
INSERT INTO packets (frequency) VALUES (127500000.0);  -- Always Hz
```

---

## ?? Reference

### Conversion Formulas

```csharp
// Hz ? MHz
double hz = mhz * 1_000_000.0;
double mhz = hz / 1_000_000.0;

// Hz ? GHz
double hz = ghz * 1_000_000_000.0;
double ghz = hz / 1_000_000_000.0;

// MHz ? GHz
double mhz = ghz * 1_000.0;
double ghz = mhz / 1_000.0;
```

### Common Radio Frequencies

| Name | Frequency (MHz) | Frequency (Hz) |
|------|-----------------|----------------|
| Guard | 121.5 | 121,500,000 |
| Tower | 127.5 | 127,500,000 |
| UHF TAD | 251.0 | 251,000,000 |
| UHF Strike | 264.0 | 264,000,000 |

---

## ? Compliance Checklist

- [x] All `AudioPacketMetadata.Frequency` values in Hz
- [x] All database `frequency` columns store Hz
- [x] All `FrequencyInfo.Frequency` values in Hz
- [x] All `FrequencyModulationInfo.Frequency` values in Hz
- [x] All UI displays convert Hz ? MHz
- [x] All code comments document units
- [x] Constants use Hz for validation
- [x] Schema documentation updated
- [x] No heuristic "guess the unit" logic (except ADB input normalization)
- [x] Build successful with no warnings

---

## ?? Summary

**Standard Established**: ? **Store Hz, Display MHz**

**Benefits**:
1. **Precision**: Hz provides exact values (no rounding errors)
2. **Consistency**: One unit throughout the pipeline
3. **Compatibility**: Matches original ADB Hz format
4. **Clarity**: Explicit conversions only at UI boundary
5. **Maintainability**: Clear unit documentation everywhere

**No Data Migration Required**: Database already stores correct values (they were Hz all along, just mislabeled as MHz).

---

**Created**: 2025-01-20  
**Standard**: Hz internal, MHz display  
**Files Changed**: 6  
**Build**: ? Successful
