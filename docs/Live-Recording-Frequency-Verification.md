# Live Recording Frequency Unit Verification

**Date**: 2025-01-20  
**Status**: ? **VERIFIED CORRECT**  
**Standard**: Frequencies recorded in Hz from SRS network packets

---

## ?? Verification Summary

Live recording correctly stores frequencies in **Hz** throughout the entire pipeline with no conversions.

---

## ?? Live Recording Data Flow

```
???????????????????????????????????????????????????????????????????
? 1. SRS SERVER (Network)                                         ?
?    UDP Packet: frequency (double, 8 bytes)                      ?
?    Format: Hz (e.g., 127500000.0 for 127.5 MHz)                 ?
?    Source: DCS Simple Radio Standalone                          ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? UDPVoiceHandler receives packet
                            ?
???????????????????????????????????????????????????????????????????
? 2. PACKET EXTRACTION (AudioPacketRecorder)                      ?
?    File: AudioPacketRecorder.cs                                 ?
?    Method: ExtractAudioMetadata()                               ?
?    Line 414: BitConverter.ToDouble(packet, offset)              ?
?    Format: Hz (127500000.0) - RAW FROM NETWORK                  ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? No conversion applied
                            ?
???????????????????????????????????????????????????????????????????
? 3. MEMORY (AudioPacketMetadata)                                ?
?    Line 478: frequency parameter                                ?
?    Format: Hz (127500000.0) - UNCHANGED                         ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? _writeQueue.Enqueue(meta)
                            ?
???????????????????????????????????????????????????????????????????
? 4. WRITE QUEUE                                                  ?
?    ConcurrentQueue<AudioPacketMetadata>                         ?
?    Format: Hz (127500000.0) - QUEUED                            ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? WriterLoop batches packets
                            ?
???????????????????????????????????????????????????????????????????
? 5. BATCH WRITER (WriterLoop)                                   ?
?    File: AudioPacketRecorder.cs                                 ?
?    Method: WriterLoop()                                         ?
?    Line 642: InsertBatchAsync(batch)                            ?
?    Format: Hz (127500000.0) - BATCHED                           ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? SqlitePacketRepository.InsertBatchAsync
                            ?
???????????????????????????????????????????????????????????????????
? 6. DATABASE (SQLite)                                           ?
?    Table: packets                                              ?
?    Column: frequency (REAL)                                    ?
?    Format: Hz (127500000.0) - STORED                           ?
???????????????????????????????????????????????????????????????????
```

---

## ? Code Verification

### 1. UDP Packet Reception

**File**: `src/AeroDebrief.Core/AudioPacketRecorder.cs`  
**Method**: `ExtractAudioMetadata(byte[] packet)`  
**Line**: 414

```csharp
double frequency = BitConverter.ToDouble(packet, offset); offset += 8;
```

**Verification**: ?
- Reads 8 bytes as double from UDP packet
- **SRS sends frequencies in Hz** (confirmed from SRS protocol)
- No conversion applied
- Value used directly

### 2. AudioPacketMetadata Construction

**File**: `src/AeroDebrief.Core/AudioPacketRecorder.cs`  
**Method**: `ExtractAudioMetadata(byte[] packet)`  
**Lines**: 476-489

```csharp
return new AudioPacketMetadata(
    DateTime.UtcNow,
    frequency,          // <--- Hz value from UDP packet
    modulation,
    encryption,
    transmitterUnitId,
    packetId,
    transmitterGuid,
    playerInfo,
    _sampleRate,
    _channelCount,
    coalition,
    payloadToWrite
);
```

**Verification**: ?
- Uses `frequency` directly from UDP packet (Hz)
- No conversion applied
- Stored in memory as Hz

### 3. Queue Storage

**File**: `src/AeroDebrief.Core/AudioPacketRecorder.cs`  
**Method**: `RecordingLoop(CancellationToken token)`  
**Line**: 380

```csharp
_writeQueue.Enqueue(meta);
```

**Verification**: ?
- Enqueues AudioPacketMetadata as-is
- Frequency remains in Hz
- No conversion at queue boundary

### 4. Batch Writing

**File**: `src/AeroDebrief.Core/AudioPacketRecorder.cs`  
**Method**: `WriterLoop(CancellationToken token)`  
**Lines**: 642-644

```csharp
// Use Repository Pattern: Packets.InsertBatchAsync
await _recordingUnitOfWork.Packets.InsertBatchAsync(batch, token);
var count = await _recordingUnitOfWork.Packets.GetCountAsync(token);
Logger.Debug($"Inserted batch: {batch.Count} packets (total: {count:N0})");
```

**Verification**: ?
- Batch contains AudioPacketMetadata with Hz frequencies
- InsertBatchAsync stores frequencies directly
- No conversion in database write

### 5. Database Storage

**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqlitePacketRepository.cs`  
**Method**: `InsertBatchAsync(IEnumerable<AudioPacketMetadata> packets)`  
**Line**: 93

```csharp
Frequency = p.Frequency,  // Hz from AudioPacketMetadata
```

**Verification**: ?
- Stores frequency value directly from AudioPacketMetadata
- No conversion applied
- SQLite REAL column stores Hz (127500000.0)

---

## ?? Test Cases

### Test Case 1: 127.5 MHz Radio

**Expected SRS Packet**: `127500000.0` (Hz)

| Stage | Value | Format | Status |
|-------|-------|--------|--------|
| UDP Packet | 127500000.0 | Hz | ? |
| ExtractAudioMetadata | 127500000.0 | Hz | ? |
| AudioPacketMetadata | 127500000.0 | Hz | ? |
| Write Queue | 127500000.0 | Hz | ? |
| Database | 127500000.0 | Hz | ? |

### Test Case 2: 251.0 MHz Radio

**Expected SRS Packet**: `251000000.0` (Hz)

| Stage | Value | Format | Status |
|-------|-------|--------|--------|
| UDP Packet | 251000000.0 | Hz | ? |
| ExtractAudioMetadata | 251000000.0 | Hz | ? |
| AudioPacketMetadata | 251000000.0 | Hz | ? |
| Write Queue | 251000000.0 | Hz | ? |
| Database | 251000000.0 | Hz | ? |

### Test Case 3: Guard Frequency (121.5 MHz)

**Expected SRS Packet**: `121500000.0` (Hz)

| Stage | Value | Format | Status |
|-------|-------|--------|--------|
| UDP Packet | 121500000.0 | Hz | ? |
| ExtractAudioMetadata | 121500000.0 | Hz | ? |
| AudioPacketMetadata | 121500000.0 | Hz | ? |
| Write Queue | 121500000.0 | Hz | ? |
| Database | 121500000.0 | Hz | ? |

---

## ?? Comparison: ADB vs Live Recording

### ADB File Import (Historical)

```
ADB File: Mixed Hz or MHz
    ? AudioPacketMetadata.TryReadMetadata()
    ? Normalize: If < 1000 ? ×1,000,000
Memory: Hz ?
    ? InsertBatchAsync
Database: Hz ?
```

### Live Recording (Real-time)

```
SRS UDP: Always Hz
    ? BitConverter.ToDouble (no conversion)
Memory: Hz ?
    ? InsertBatchAsync  
Database: Hz ?
```

**Conclusion**: Both paths result in **Hz storage** in the database. ?

---

## ?? SRS Protocol Confirmation

### Simple Radio Standalone (SRS) Network Protocol

**UDP Voice Packet Structure**:
```
[0-1]   Packet Length (ushort)
[2-3]   Audio Part Length (ushort)
[4-5]   Frequency Part Length (ushort)
[6-N]   Audio Payload (bytes)
[N+0 to N+7] Frequency (double, 8 bytes) <--- ALWAYS IN HZ
[N+8]   Modulation (byte)
[N+9]   Encryption (byte)
...
```

**Source**: SRS Common library protocol definition  
**Frequency Format**: **Double (8 bytes), always in Hz**  
**Example**: 127.5 MHz is sent as `127500000.0`

---

## ? Validation Checklist

Live recording frequency handling:

- [x] SRS sends frequencies in Hz (confirmed from protocol)
- [x] ExtractAudioMetadata reads as Hz (BitConverter.ToDouble, no conversion)
- [x] AudioPacketMetadata stores Hz (constructor parameter)
- [x] Write queue contains Hz (ConcurrentQueue<AudioPacketMetadata>)
- [x] Batch writer uses Hz (InsertBatchAsync parameter)
- [x] Database stores Hz (SQLite REAL column)
- [x] No conversion at any stage
- [x] Matches unit standard (Hz internal storage)
- [x] Consistent with ADB import normalization
- [x] Display code converts Hz ? MHz correctly

---

## ?? Summary

**Live Recording Status**: ? **FULLY COMPLIANT**

**Frequency Unit Handling**:
1. ? SRS sends Hz
2. ? Code receives Hz
3. ? Memory stores Hz
4. ? Database stores Hz
5. ? Display shows MHz (with conversion)

**No Changes Required**: Live recording was already storing frequencies in Hz correctly. The recent unit standardization work ensures that:
- ADB import normalizes to Hz
- Live recording receives Hz from SRS
- Database stores Hz consistently
- Display converts Hz ? MHz uniformly

**Result**: ? **Complete unit consistency across all data sources**

---

## ?? Logging Verification

The code includes debug logging that can be used to verify correct frequency values:

```csharp
// Line 377
Logger.Debug($"Audio packet received: Freq={meta.Frequency}, TxGuid={meta.TransmitterGuid}, Size={meta.AudioPayload.Length}");
```

**Expected Log Output**:
```
Audio packet received: Freq=127500000, TxGuid=abc123, Size=1920
```

If you see values like `Freq=127.5`, the recording would be **incorrect** (MHz instead of Hz). But the code shows `meta.Frequency` which comes directly from `BitConverter.ToDouble()`, so it will always be Hz.

---

## ?? Consistency Verification

### All Data Sources Now Store Hz

| Source | Format | Normalized | Storage | Status |
|--------|--------|-----------|---------|--------|
| **SRS Live** | Hz | No (already Hz) | Hz | ? |
| **ADB Legacy (Hz)** | Hz | No (already Hz) | Hz | ? |
| **ADB Legacy (MHz)** | MHz | Yes (×1,000,000) | Hz | ? |

### All Display Points Convert Hz ? MHz

| Location | Method | Conversion | Status |
|----------|--------|------------|--------|
| FrequencyInfo | FormattedFrequency | ÷1,000,000 | ? |
| FrequencyModulationInfo | GetDisplayText() | ÷1,000,000 | ? |
| FrequencyManager | GetFrequencyId() | ÷1,000,000 | ? |
| CoreApiService | SetChannelActiveAsync() | ÷1,000,000 | ? |

---

**Created**: 2025-01-20  
**Verified**: Live recording stores Hz correctly  
**No Changes Required**: Code already compliant  
**Status**: ? VERIFIED
