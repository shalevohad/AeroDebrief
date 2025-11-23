# Frequency Display Bug Fix

**Date**: 2025-01-20  
**Issue**: Frequencies showing as 0.000 MHz in UI  
**Root Cause**: Unit mismatch between storage and display  
**Status**: ? **FIXED**

---

## Problem Description

After migrating from ADB to SQLite, all frequencies in the frequency mixer and tree view showed as **0.000 MHz** instead of the correct values like **127.500 MHz**, **251.000 MHz**, etc.

---

## Root Cause Analysis

### Data Flow

1. **ADB File Format**
   - Stores frequencies in **Hz** (e.g., 127500000.0 for 127.5 MHz)
   
2. **AudioPacketMetadata.TryReadMetadata()** (lines 117-124)
   ```csharp
   double frequency = reader.ReadDouble();
   
   // Handle mixed format: some packets store Hz, others MHz
   // If frequency > 1000, assume it's in Hz and convert to MHz
   if (frequency > 1000.0)
   {
       frequency = frequency / 1_000_000.0;  // Convert Hz to MHz
   }
   ```
   - Normalizes frequencies from Hz ? MHz (127500000.0 ? 127.5)

3. **SqlitePacketRepository.InsertBatchAsync()** (line 93)
   ```csharp
   Frequency = p.Frequency,  // Stores 127.5 (already in MHz!)
   ```
   - Stores the **already normalized MHz value**

4. **frequency_stats table**
   - Built from packets table via `RebuildStatsAsync()`
   - Contains MHz values: 127.5, 251.0, etc.

5. **SqliteFrequencyRepository.MapToFrequencyInfo()** (line 149)
   ```csharp
   Frequency = (double)row.frequency,  // Reads 127.5 MHz
   ```
   - Reads MHz values from database

6. **FrequencyInfo.FormattedFrequency** (line 49) - **THE BUG**
   ```csharp
   // OLD CODE (WRONG):
   public string FormattedFrequency => $"{Frequency / 1_000_000.0:F3} MHz";
   // This divided 127.5 by 1,000,000 = 0.0001275 = "0.000 MHz"
   ```

### The Problem

The `FrequencyInfo` class had a comment saying "Radio frequency in Hz" (line 12), but after ADB normalization, frequencies were stored in **MHz**. The `FormattedFrequency` property divided by 1,000,000 assuming Hz, resulting in:

```
127.5 MHz / 1,000,000 = 0.0001275 MHz = "0.000 MHz" (rounded to 3 decimal places)
```

---

## The Fix

### File Changed
`src/AeroDebrief.Core/Storage/Abstractions/FrequencyInfo.cs`

### Changes Made

1. **Updated documentation** (line 12):
   ```csharp
   /// <summary>
   /// Radio frequency in MHz (stored normalized from ADB files)
   /// </summary>
   public double Frequency { get; set; }
   ```

2. **Fixed FormattedFrequency property** (line 49):
   ```csharp
   /// <summary>
   /// Human-readable frequency (e.g., "251.000 MHz")
   /// NOTE: Frequency is already in MHz from normalized ADB data
   /// </summary>
   public string FormattedFrequency => $"{Frequency:F3} MHz";
   ```
   - **Removed** the division by 1,000,000
   - Now correctly displays: 127.5 MHz ? "127.500 MHz"

---

## Why This Worked with ADB Files

When using ADB files directly (without SQLite migration), the old `DuckDBStore` or file-based packet sources would:
1. Read frequencies in Hz from ADB
2. Store them temporarily in memory structures
3. Display them with the Hz?MHz conversion

The unit conversion was correct when frequencies were always in Hz throughout the pipeline.

---

## Why This Broke with SQLite

The SQLite migration introduced **normalization at read time**:
- ADB read code normalizes Hz ? MHz (for consistency)
- SQLite stores the **normalized MHz values**
- Display code still assumed Hz and divided again

This created a **double conversion**:
```
ADB: 127500000 Hz 
  ? Normalize: 127.5 MHz
  ? Store in SQLite: 127.5
  ? Read from SQLite: 127.5
  ? Display (wrong): 127.5 / 1,000,000 = 0.000 MHz ?
```

---

## Verification

### Before Fix
- Frequency Mixer: All frequencies showed "0.000 MHz"
- Frequency Tree: All frequencies showed "0.000 MHz"
- Database inspection: Values correctly stored as 127.5, 251.0, etc.

### After Fix
- Frequency Mixer: Shows "127.500 MHz", "251.000 MHz", etc. ?
- Frequency Tree: Shows correct MHz values ?
- No data migration needed - only display logic fixed ?

---

## Related Code

### Other files that handle frequency units correctly:

1. **FrequencyModulationInfo.GetDisplayText()** (already fixed)
   ```csharp
   var frequencyMhz = Frequency > 1000.0 ? Frequency / 1_000_000.0 : Frequency;
   return $"{frequencyMhz:F3} MHz ({GetModulationName()})";
   ```
   - Uses heuristic: if > 1000 treat as Hz, else treat as MHz

2. **FrequencyManager.GetFrequencyId()** (already fixed)
   ```csharp
   double frequencyMhz = frequencyHz > 1000.0 ? frequencyHz / 1_000_000.0 : frequencyHz;
   return $"{frequencyMhz:F1}-{modulation}";
   ```
   - Same heuristic approach

These already worked because SQLite stores MHz values (<1000), so the heuristic didn't trigger conversion.

---

## Lessons Learned

1. **Unit Documentation**: Always document units clearly (Hz vs MHz vs GHz)
2. **Normalize Early**: Normalize at data input (read from ADB) ?
3. **Store Normalized**: Store in consistent units (MHz) ?
4. **Display Correctly**: Don't assume units - check documentation ?
5. **Integration Testing**: Test full pipeline ADB ? DB ? Display

---

## Future Improvements

### Option 1: Explicit Unit Field
```csharp
public class FrequencyInfo
{
    public double Frequency { get; set; }
    public FrequencyUnit Unit { get; set; } = FrequencyUnit.MHz;
    
    public string FormattedFrequency => Unit switch
    {
        FrequencyUnit.Hz => $"{Frequency / 1_000_000.0:F3} MHz",
        FrequencyUnit.MHz => $"{Frequency:F3} MHz",
        FrequencyUnit.GHz => $"{Frequency * 1000.0:F3} MHz",
        _ => $"{Frequency} (unknown unit)"
    };
}
```

### Option 2: Value Object Pattern
```csharp
public struct Frequency
{
    private readonly double _valueInMHz;
    
    public static Frequency FromHz(double hz) => new(hz / 1_000_000.0);
    public static Frequency FromMHz(double mhz) => new(mhz);
    
    public double ToHz() => _valueInMHz * 1_000_000.0;
    public double ToMHz() => _valueInMHz;
    
    public override string ToString() => $"{_valueInMHz:F3} MHz";
}
```

These would eliminate unit confusion entirely, but require more refactoring.

---

## Testing Recommendations

### Manual Test
1. Convert an ADB file to SQLite:
   ```bash
   aerodebrief-cli migrate input.adb output.db
   ```

2. Open the database in the UI

3. Verify frequency display:
   - Frequency mixer shows correct MHz values
   - Frequency tree shows correct MHz values
   - Statistics view shows correct frequencies

### Automated Test
```csharp
[Fact]
public async Task FrequencyInfo_FormattedFrequency_Should_Display_MHz_Correctly()
{
    // Arrange - frequency stored in MHz
    var frequencyInfo = new FrequencyInfo
    {
        Frequency = 127.5,  // MHz (normalized from 127500000 Hz)
        Modulation = 0
    };

    // Act
    var formatted = frequencyInfo.FormattedFrequency;

    // Assert
    formatted.Should().Be("127.500 MHz");
}
```

---

## Status

? **Issue Resolved**
- Build: Successful
- Frequencies now display correctly
- No data migration required
- Backward compatible with existing databases

---

**Created**: 2025-01-20  
**Fixed By**: Copilot  
**Files Changed**: 1 (FrequencyInfo.cs)  
**Lines Changed**: 2 (comment + formula)
