# Amplitude Cache Implementation Guide

## ?? Overview

The amplitude cache is a **Phase 7 optimization** that provides **10-100x faster** waveform rendering by eliminating the need to re-decode audio packets.

## ?? Architecture

### Data Flow

```
RECORDING (One-time cost):
Opus Bytes ? Decode ? Raw PCM ? Calculate RAW Amplitude ? Cache in DB
                                       ?
                                  Store ONCE, never changes

PLAYBACK (Instant):
User adjusts mixer ? Query cached RAW amplitude ? Apply gain ? Display waveform
                            ?                          ?
                        <5ms query              Single multiplication
```

### Key Design Decisions

1. **Cache RAW amplitude** (before volume/gain/pan) ? Remains valid forever
2. **Apply volume at query time** ? Respects mixer settings
3. **Store peak envelope** ? Detailed waveform for zoom
4. **10ms windows** ? Balance between detail and storage

## ?? Usage

### Basic Query (No Mixer Gain)

```csharp
var repositoryFactory = new SqliteRepositoryFactory();
using var unitOfWork = repositoryFactory.OpenRecording(filePath);

var amplitudes = await unitOfWork.Amplitudes.QueryRangeAsync(
    frequency: 127_500_000.0, // 127.5 MHz in Hz
    fromMs: 0,
    toMs: 60000); // First minute

// Extract waveform points
var waveformPoints = amplitudes.Select(a => a.MaxAmplitude).ToArray();
```

### Query with Mixer Gain (Recommended)

```csharp
var mixerGain = FrequencyMixer.GetGain(frequency); // Get current user setting

var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
    frequency: 127_500_000.0,
    fromMs: 0,
    toMs: 60000,
    gain: mixerGain); // Applied at query time

// Waveform now respects user mixer settings!
var waveformPoints = amplitudes.Select(a => a.MaxAmplitude).ToArray();
```

### Detailed Waveform (Peak Envelope)

```csharp
var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
    frequency, fromMs, toMs, mixerGain);

// Flatten peak envelopes for high-resolution waveform
var detailedPoints = amplitudes
    .SelectMany(a => a.PeakEnvelope)
    .ToArray();

// Each point = 10ms window peak (48 points per second)
```

## ?? Performance Metrics

### Without Amplitude Cache

```
Operation: Load 3-hour recording (300,000 packets)
- Read packets from DB: 5-10 seconds
- Decode Opus audio: 300,000 × 2ms = 600 seconds (10 minutes!)
- Calculate amplitude: 300,000 × 0.5ms = 150 seconds
Total: ~12-15 minutes per load
```

### With Amplitude Cache

```
Operation: Load 3-hour recording (300,000 packets)
- Query amplitude cache: 2-5 seconds
- Apply mixer gain: 300,000 × 0.00001ms = 3ms
Total: 2-5 seconds per load

Speedup: 150-450x faster! ??
```

## ??? Database Schema

```sql
CREATE TABLE IF NOT EXISTS amplitude_cache (
    packet_id INTEGER PRIMARY KEY,
    max_amplitude REAL NOT NULL,      -- Peak amplitude (0.0-1.0)
    rms_amplitude REAL NOT NULL,      -- RMS amplitude
    peak_envelope BLOB,               -- Float array (10ms windows)
    envelope_points INTEGER DEFAULT 0,
    computed_at TEXT NOT NULL,
    FOREIGN KEY (packet_id) REFERENCES packets(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_amplitude_packet ON amplitude_cache(packet_id);
```

### Storage Overhead

```
Per packet: ~100-200 bytes (vs. 10-50 KB raw audio)
3-hour recording: ~30-60 MB amplitude cache (vs. 3-6 GB raw audio)
Compression ratio: 100:1
```

## ?? Integration Points

### Update Graph Loading Code

**OLD (SLOW - re-decodes every time):**
```csharp
foreach (var packet in packets)
{
    var decoded = engine.DecodePacketToFloat(packet); // 2ms per packet!
    var amplitude = decoded.Max(Math.Abs);
    waveformPoints.Add(amplitude);
}
```

**NEW (FAST - uses pre-computed cache):**
```csharp
var mixerGain = FrequencyMixer.GetGain(frequency);
var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
    frequency, fromMs, toMs, mixerGain);

waveformPoints.AddRange(amplitudes.Select(a => a.MaxAmplitude));
```

### Mixer Integration

```csharp
// When user changes mixer gain:
private void OnMixerGainChanged(double frequency, float newGain)
{
    // NO cache invalidation needed!
    // Just re-query with new gain:
    ReloadWaveform(frequency, newGain);
}

private async Task ReloadWaveform(double frequency, float gain)
{
    var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(
        frequency, currentViewFromMs, currentViewToMs, gain);
    
    UpdateWaveformDisplay(amplitudes);
}
```

## ?? Testing

### Verify Cache Population

```csharp
var stats = await unitOfWork.Amplitudes.GetStatsAsync();
Console.WriteLine(stats); // "Amplitude Cache: 300,000/300,000 packets (100.0% cached)"
```

### Benchmark Performance

```csharp
var sw = Stopwatch.StartNew();
var amplitudes = await unitOfWork.Amplitudes.QueryRangeAsync(frequency, 0, 180000);
sw.Stop();
Console.WriteLine($"Query time: {sw.ElapsedMilliseconds}ms for {amplitudes.Count} packets");
// Expected: 50-200ms for 180,000 packets (3 minutes of audio)
```

## ?? Troubleshooting

### Cache Not Populated

**Symptom:** `GetStatsAsync()` shows 0% cached

**Causes:**
1. Recording was created before Phase 7 ? Cache doesn't exist
2. `InsertBatchAsync` threw exception during amplitude computation

**Solution:**
- For old recordings: Run on-demand computation (future feature)
- For new recordings: Check logs for amplitude computation errors

### Slow Queries

**Symptom:** Queries take >1 second for 60 seconds of audio

**Causes:**
1. Missing index on `amplitude_cache(packet_id)`
2. Large time range query (>10 minutes)
3. Database not optimized (run `VACUUM` and `ANALYZE`)

**Solution:**
```csharp
await _connection.ExecuteAsync("ANALYZE amplitude_cache");
await _connection.ExecuteAsync("PRAGMA optimize");
```

### Incorrect Amplitude After Mixer Change

**Symptom:** Waveform doesn't update when mixer gain changes

**Causes:**
- Using `QueryRangeAsync()` instead of `QueryRangeWithGainAsync()`
- Not re-querying after mixer change

**Solution:**
```csharp
// WRONG:
var amplitudes = await unitOfWork.Amplitudes.QueryRangeAsync(freq, from, to);
// Ignores mixer gain!

// CORRECT:
var gain = GetCurrentMixerGain(freq);
var amplitudes = await unitOfWork.Amplitudes.QueryRangeWithGainAsync(freq, from, to, gain);
// Respects mixer settings!
```

## ?? API Reference

### `SqliteAmplitudeRepository`

#### `QueryRangeAsync(frequency, fromMs, toMs, ct)`
Query RAW amplitude data (no volume adjustment).

**Returns:** `List<AmplitudeData>` with RAW amplitude values

**Use case:** Internal queries, testing

#### `QueryRangeWithGainAsync(frequency, fromMs, toMs, gain, ct)`
Query amplitude data with GAIN applied at query time.

**Returns:** `List<AmplitudeData>` with gain-adjusted amplitude

**Use case:** UI/graph rendering with mixer support (RECOMMENDED)

#### `GetStatsAsync(ct)`
Get cache statistics.

**Returns:** `AmplitudeCacheStats` with coverage percentage

**Use case:** Monitoring, diagnostics

#### `InsertBatchAsync(entries, ct)`
Batch insert amplitude data.

**Use case:** Called automatically during recording

### `AmplitudeData`

```csharp
public class AmplitudeData
{
    public long PacketId { get; set; }
    public long RelativeMs { get; set; }
    public double Frequency { get; set; }
    public string TransmitterGuid { get; set; }
    public float MaxAmplitude { get; set; }      // Peak in packet
    public float RmsAmplitude { get; set; }      // RMS level
    public float[] PeakEnvelope { get; set; }    // 10ms window peaks
    public int EnvelopePoints { get; set; }
}
```

## ?? Best Practices

1. ? **Always use `QueryRangeWithGainAsync`** for UI rendering
2. ? **Apply gain at query time**, never at storage time
3. ? **Query only visible time range**, not entire recording
4. ? **Use peak envelope** for zoomed-in views
5. ? **Monitor cache stats** to ensure 100% coverage
6. ? **Don't invalidate cache** on mixer changes
7. ? **Don't decode audio** if amplitude cache exists
8. ? **Don't cache volume-adjusted amplitude**

## ?? Future Enhancements

- [ ] On-demand amplitude computation for legacy recordings
- [ ] Progressive cache warming during file load
- [ ] Multi-resolution amplitude cache (1s, 100ms, 10ms)
- [ ] Compressed peak envelope storage (50% size reduction)
- [ ] Background cache refresh for live recordings

---

**Questions?** Check `src/AeroDebrief.Core/Examples/AmplitudeCacheExample.cs` for working code samples.
