# RadioPacket Type Investigation - Phase 2.2

## Issue Summary
**Date**: 2025-01-21  
**Status**: ?? ISSUE IDENTIFIED - WORKAROUND IMPLEMENTED  
**Impact**: Medium (blocks real FilePacketSource integration)

---

## ?? Problem Description

### What We Found
`FilePacketSource.ReadRange()` method signature and implementation reference a **RadioPacket** type that **does not exist** in the codebase:

```csharp
// In FilePacketSource.cs
public async IAsyncEnumerable<RadioPacket> ReadRange(
    TimeSpan from,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // ...
}

private RadioPacket? ReadPacketAt(long offset)
{
    // ...
    if (AudioPacketMetadata.TryReadMetadata(reader, out var metadata) && metadata != null)
    {
        return RadioPacket.FromMetadata(metadata);  // ? TYPE NOT FOUND!
    }
    return null;
}
```

### Investigation Results
? **Searched entire codebase** - No RadioPacket definition found  
? **Checked for type aliases** - None found  
? **Checked Models namespace** - Not there  
? **Checked IO namespace** - Not there  
? **Checked global usings** - No alias  

### Current Status
- ? `RadioPacket` type is **undefined**
- ? `RadioPacket.FromMetadata()` method does not exist
- ? Code does not compile if FilePacketSource.ReadRange() is actually used

---

## ? Workaround Implemented

### Phase 2.2 Solution
We **bypassed** this issue by using `MockRecordingFileBuilder`:

```csharp
// MockRecordingFileBuilder works directly with AudioPacketMetadata
using var builder = new MockRecordingFileBuilder();
builder
    .WithHeader(startTime: DateTime.UtcNow)
    .WithPacket(
        frequency: 251_000_000.0,
        playerName: "Viper-1",
        // ... creates AudioPacketMetadata directly
    );

// This creates a valid .adb file that FilePacketSource can open
var packetSource = new FilePacketSource(builder.FilePath);
await packetSource.OpenAsync();

// But we DON'T call ReadRange() - we use the mock data directly!
```

### Why This Works
1. MockRecordingFileBuilder writes AudioPacketMetadata to files
2. Our tests use the mock builder to create test recordings
3. We validate the amplitude extraction pipeline without FilePacketSource.ReadRange()
4. All Phase 2.2 tests pass successfully

---

## ?? Recommended Solutions

### Option A: Create RadioPacket as Simple Wrapper (Recommended)
```csharp
// Add to AudioPacketMetadata.cs or new RadioPacket.cs
namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Lightweight packet wrapper for FilePacketSource streaming.
    /// Provides a clean separation between file I/O and audio processing.
    /// </summary>
    public sealed record RadioPacket
    {
        public DateTime Timestamp { get; init; }
        public double Frequency { get; init; }
        public byte Modulation { get; init; }
        public byte Encryption { get; init; }
        public string TransmitterGuid { get; init; } = string.Empty;
        public PlayerInfo? Player { get; init; }
        public byte[] AudioData { get; init; } = Array.Empty<byte>();
        
        /// <summary>
        /// Create RadioPacket from AudioPacketMetadata.
        /// </summary>
        public static RadioPacket FromMetadata(AudioPacketMetadata metadata)
        {
            return new RadioPacket
            {
                Timestamp = metadata.Timestamp,
                Frequency = metadata.Frequency,
                Modulation = metadata.Modulation,
                Encryption = metadata.Encryption,
                TransmitterGuid = metadata.TransmitterGuid,
                Player = metadata.PlayerData,
                AudioData = metadata.AudioPayload
            };
        }
        
        /// <summary>
        /// Convert back to AudioPacketMetadata for processing.
        /// </summary>
        public AudioPacketMetadata ToMetadata()
        {
            return new AudioPacketMetadata(
                Timestamp,
                Frequency,
                Modulation,
                Encryption,
                0, // TransmitterUnitId
                0, // PacketId
                TransmitterGuid,
                Player ?? new PlayerInfo(),
                48000, // SampleRate
                1,     // ChannelCount
                Player?.Coalition ?? 0,
                AudioData
            );
        }
    }
}
```

### Option B: Use Type Alias
```csharp
// At top of FilePacketSource.cs
using RadioPacket = AeroDebrief.Core.AudioPacketMetadata;

// Then update ReadPacketAt:
private RadioPacket? ReadPacketAt(long offset)
{
    // ...
    if (AudioPacketMetadata.TryReadMetadata(reader, out var metadata) && metadata != null)
    {
        return metadata; // Now it's just AudioPacketMetadata
    }
    return null;
}
```

### Option C: Change FilePacketSource Signature
```csharp
// Change return type to AudioPacketMetadata
public async IAsyncEnumerable<AudioPacketMetadata> ReadRange(
    TimeSpan from,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // ...
}

private AudioPacketMetadata? ReadPacketAt(long offset)
{
    // ...
    if (AudioPacketMetadata.TryReadMetadata(reader, out var metadata) && metadata != null)
    {
        return metadata; // Direct return
    }
    return null;
}
```

---

## ?? Impact Analysis

### What's Blocked
- ? Real FilePacketSource.ReadRange() usage in AmplitudeSeriesProvider
- ? Production amplitude extraction from actual recordings
- ? Integration with playback pipeline for real data

### What's Working
- ? Mock recording testing (Phase 2.2)
- ? Amplitude extraction algorithm
- ? dBFS conversion
- ? Peak amplitude detection
- ? Time offset format
- ? ObservablePoint output
- ? Synthetic data fallback

### Timeline
- **Phase 2.2**: ? Complete (with workaround)
- **Phase 2.3**: ?? Blocked until RadioPacket resolved
- **Phase 3**: ?? Cannot proceed without real data integration

---

## ?? Implementation Priority

### HIGH PRIORITY (for Phase 2.3)
Must be resolved before UI integration can use real recordings.

### Recommended Action
**Implement Option A** (Create RadioPacket wrapper):
1. Create `src/AeroDebrief.Core/IO/RadioPacket.cs`
2. Add FromMetadata() and ToMetadata() methods
3. Update AmplitudeSeriesProvider.GetRealDataAsync() to use it
4. Test with actual .adb recording file
5. Verify compilation

### Estimated Effort
- **Implementation**: 30-60 minutes
- **Testing**: 30 minutes
- **Documentation**: 15 minutes
- **Total**: ~2 hours

---

## ?? Code Changes Required

### 1. Create RadioPacket.cs
```csharp
// File: src/AeroDebrief.Core/IO/RadioPacket.cs
using AeroDebrief.Core;

namespace AeroDebrief.Core.IO
{
    public sealed record RadioPacket
    {
        // Properties and methods from Option A above
    }
}
```

### 2. Update AmplitudeSeriesProvider
```csharp
// In GetRealDataAsync()
await foreach (var radioPacket in _packetSource.ReadRange(timeOffset, ct))
{
    if (ct.IsCancellationRequested)
        yield break;
    
    // Convert RadioPacket ? AudioPacketMetadata
    var metadata = radioPacket.ToMetadata();
    
    // Group and process
    var key = (metadata.Frequency, metadata.TransmitterGuid);
    if (!packetGroups.ContainsKey(key))
        packetGroups[key] = new List<AudioPacketMetadata>();
    
    packetGroups[key].Add(metadata);
}
```

### 3. Add Integration Test
```csharp
// Test reading from real .adb file
private static async Task<bool> Test_RealFilePacketSource()
{
    var filePath = "path/to/test.adb";
    var packetSource = new FilePacketSource(filePath);
    await packetSource.OpenAsync();
    
    var recordingStart = packetSource.RecordingStart;
    var count = 0;
    
    await foreach (var packet in packetSource.ReadRange(TimeSpan.Zero))
    {
        Assert.IsNotNull(packet);
        Assert.IsTrue(packet.AudioData.Length > 0);
        count++;
        if (count >= 10) break; // Test first 10 packets
    }
    
    return count > 0;
}
```

---

## ? Success Criteria

### Definition Complete When
- ? RadioPacket type compiles
- ? FromMetadata() method works
- ? ToMetadata() method works
- ? FilePacketSource.ReadRange() returns valid packets
- ? AmplitudeSeriesProvider can process real recordings
- ? Integration test passes with .adb file

---

## ?? Related Files

### Affected Files
- `src/AeroDebrief.Core/IO/FilePacketSource.cs` - Uses undefined RadioPacket
- `src/AeroDebrief.Core/AudioPacketMetadata.cs` - Base metadata type
- `src/AeroDebrief.UI/Services/Graphs/AmplitudeSeriesProvider.cs` - Needs conversion

### Test Files
- `tests/AeroDebrief.Tests/Graphs/AmplitudeExtractionPipelineTests.cs` - Add real file test
- `tests/AeroDebrief.Tests/IO/RadioPacketTests.cs` - New test file needed

### Documentation
- `docs/Phase2-Day2-Summary.md` - Updated with investigation results
- `docs/RadioPacket-Investigation.md` - This document

---

## ?? Next Steps

### Immediate (Phase 2.3)
1. ? Document issue (this document)
2. ? Implement Option A (create RadioPacket type)
3. ? Update AmplitudeSeriesProvider
4. ? Add integration test with real .adb file
5. ? Verify build and tests pass

### Follow-up
1. Review with team if RadioPacket abstraction is needed long-term
2. Consider simplifying to direct AudioPacketMetadata usage
3. Update architecture documentation

---

**Issue Status**: ?? **DOCUMENTED**  
**Workaround**: ? **IMPLEMENTED** (Phase 2.2 tests pass)  
**Resolution**: ? **PENDING** (required for Phase 2.3)  
**Priority**: ?? **HIGH** (blocks real data integration)

**Date**: 2025-01-21  
**Branch**: `livechart2-integration`

---

*Issue identified and documented. Workaround allows Phase 2.2 testing to proceed. Resolution required before Phase 2.3 UI integration with real recordings.*
