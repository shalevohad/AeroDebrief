# DuckDB Quick Reference for Developers

## ?? Quick Start

### Opening Any Recording File

```csharp
// CoreApiService handles everything automatically
await _coreApi.LoadFileAsync(filePath, progress, cancellationToken);

// Behind the scenes:
// 1. RecordingFileLoader detects format (.cvr, .adb, or .duckdb)
// 2. Creates appropriate IPacketSource (FilePacketSource or DuckDBPacketSource)
// 3. Initializes FilePlaybackPipeline
// 4. Ready for playback!
```

### Recording

```csharp
// Recording always creates CVR files
var recorder = new AudioPacketRecorder();
await recorder.ConnectAsync(serverIp, port);
recorder.StartRecording(outputPath); // Will create .cvr file

// On stop:
// 1. Finalizes DuckDB
// 2. Compresses to CVR (mandatory in RELEASE)
// 3. Deletes temp .duckdb
// 4. Output: recording.cvr
```

---

## ?? Key Interfaces & Classes

### IPacketSource (Core Abstraction)
```csharp
public interface IPacketSource : IDisposable
{
    long TotalPackets { get; }
    TimeSpan TotalDuration { get; }
    DateTime RecordingStart { get; }
    
    Task OpenAsync(IProgress<string>? progress, CancellationToken ct);
    IAsyncEnumerable<RadioPacket> ReadRange(TimeSpan from, CancellationToken ct);
    IAsyncEnumerable<RadioPacket[]> ReadRangeBatched(TimeSpan from, int batchSize, CancellationToken ct);
    Dictionary<double, FrequencyMetadata> GetFrequencyMetadata();
}
```

### Implementations

#### FilePacketSource (.adb files)
```csharp
var source = new FilePacketSource("recording.adb");
await source.OpenAsync(progress, ct);

// Memory-mapped file access
// Fast seeking with PTS index
// Works with existing .adb files
```

#### DuckDBPacketSource (.cvr/.duckdb files)
```csharp
// Usually created by RecordingFileLoader
var (store, tempPath) = await RecordingFileLoader.OpenAsync("recording.cvr", progress, ct);
var source = new DuckDBPacketSource(store);
await source.OpenAsync(progress, ct);

// SQL streaming
// Efficient filtering
// Metadata pre-loaded
```

---

## ?? Common Patterns

### Pattern 1: Opening Files
```csharp
// ? CORRECT: Use RecordingFileLoader
var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath, progress, ct);
var packetSource = new DuckDBPacketSource(store);
await packetSource.OpenAsync(progress, ct);

// Track for cleanup
_duckDbStore = store;
_tempDbPath = tempPath;

// ? INCORRECT: Don't instantiate FilePacketSource directly for CVR/DuckDB
var source = new FilePacketSource("recording.cvr"); // Won't work!
```

### Pattern 2: Playback Pipeline
```csharp
// Works with ANY IPacketSource
IPacketSource packetSource = ...; // FilePacketSource or DuckDBPacketSource

var pipeline = new FilePlaybackPipeline(packetSource);
await pipeline.OpenAsync();

// Use pipeline for playback
await pipeline.PlayAsync();
await pipeline.SeekAsync(position);
await pipeline.StopAsync();
```

### Pattern 3: Cleanup
```csharp
// Always cleanup resources
try
{
    // Use packet source...
}
finally
{
    packetSource?.Dispose();
    _duckDbStore?.Dispose();
    
    if (_tempDbPath != null)
    {
        RecordingFileLoader.Cleanup(_tempDbPath);
        _tempDbPath = null;
    }
}
```

---

## ?? Architecture Patterns

### Dependency Injection
```csharp
// IPacketSource allows easy testing
public class MyPlaybackService
{
    private readonly IPacketSource _packetSource;
    
    public MyPlaybackService(IPacketSource packetSource)
    {
        _packetSource = packetSource;
    }
    
    // Works with ANY packet source implementation
    public async Task PlayAsync()
    {
        await foreach (var packet in _packetSource.ReadRange(TimeSpan.Zero))
        {
            // Process packet...
        }
    }
}
```

### Factory Pattern
```csharp
public static class PacketSourceFactory
{
    public static async Task<IPacketSource> CreateAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        switch (ext)
        {
            case ".adb":
                var fileSource = new FilePacketSource(filePath);
                await fileSource.OpenAsync();
                return fileSource;
                
            case ".cvr":
            case ".duckdb":
                var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath);
                var duckDbSource = new DuckDBPacketSource(store);
                await duckDbSource.OpenAsync();
                return duckDbSource;
                
            default:
                throw new NotSupportedException($"Unsupported format: {ext}");
        }
    }
}
```

---

## ?? Troubleshooting

### Issue: "RecordingFileLoader not found"
```csharp
// ? Add using statement
using AeroDebrief.Core.Storage;
```

### Issue: "Cannot convert IPacketSource to FilePacketSource"
```csharp
// ? Use runtime type checking
if (packetSource is FilePacketSource fileSource)
{
    // Use FilePacketSource-specific features
    var metadata = fileSource.GetFrequencyMetadata();
}
else if (packetSource is DuckDBPacketSource duckDbSource)
{
    // Use DuckDBPacketSource-specific features
    var metadata = duckDbSource.GetFrequencyMetadata();
}

// Or use interface method
var metadata = packetSource.GetFrequencyMetadata(); // ? Works for both!
```

### Issue: "Waveform generation not working for CVR files"
```csharp
// ?? Known limitation: Waveform generators currently expect FilePacketSource
// Workaround: Check type before generating waveform

if (packetSource is FilePacketSource fileSource)
{
    var waveform = await waveformGenerator.GenerateWaveformFromSourceAsync(
        fileSource, from, to, frequencies, progress);
}
else
{
    // DuckDB waveform generation not yet implemented
    logger.Warn($"Waveform not supported for {packetSource.GetType().Name}");
}
```

---

## ?? Performance Tips

### 1. Use Batched Reading
```csharp
// ? GOOD: Batched reading (reduces async overhead)
await foreach (var batch in packetSource.ReadRangeBatched(from, batchSize: 100))
{
    foreach (var packet in batch)
    {
        // Process packet
    }
}

// ?? OK but slower: Single packet reading
await foreach (var packet in packetSource.ReadRange(from))
{
    // Process packet
}
```

### 2. Pre-load Metadata
```csharp
// ? GOOD: Load metadata once at startup
var metadata = packetSource.GetFrequencyMetadata();
var frequencies = metadata.Keys.ToList();

// Use metadata multiple times without re-querying
```

### 3. Dispose Properly
```csharp
// ? GOOD: Use using statement
await using var packetSource = await PacketSourceFactory.CreateAsync(filePath);
await packetSource.OpenAsync();
// Automatically disposed

// Or manual disposal
try { /* use */ }
finally { packetSource?.Dispose(); }
```

---

## ?? Testing

### Unit Testing with IPacketSource
```csharp
public class MockPacketSource : IPacketSource
{
    private readonly List<RadioPacket> _packets;
    
    public long TotalPackets => _packets.Count;
    public TimeSpan TotalDuration => TimeSpan.FromMinutes(5);
    public DateTime RecordingStart => DateTime.UtcNow;
    
    public async IAsyncEnumerable<RadioPacket> ReadRange(TimeSpan from, CancellationToken ct)
    {
        foreach (var packet in _packets)
        {
            yield return packet;
        }
    }
    
    // Implement other members...
}

// Use in tests
[Test]
public async Task TestPlayback()
{
    var mockSource = new MockPacketSource(testPackets);
    var pipeline = new FilePlaybackPipeline(mockSource);
    await pipeline.OpenAsync();
    
    // Test playback logic...
}
```

---

## ?? Code Snippets

### Complete File Opening Example
```csharp
public async Task<bool> OpenRecordingAsync(string filePath)
{
    try
    {
        // Clean up previous
        _packetSource?.Dispose();
        _duckDbStore?.Dispose();
        if (_tempDbPath != null)
        {
            RecordingFileLoader.Cleanup(_tempDbPath);
        }
        
        // Open new file
        var (store, tempPath) = await RecordingFileLoader.OpenAsync(
            filePath, 
            progress: new Progress<string>(msg => Logger.Info(msg)),
            cancellationToken: CancellationToken.None);
        
        _duckDbStore = store;
        _tempDbPath = tempPath;
        
        // Create packet source
        _packetSource = new DuckDBPacketSource(store);
        await _packetSource.OpenAsync();
        
        // Create playback pipeline
        _pipeline = new FilePlaybackPipeline(_packetSource);
        await _pipeline.OpenAsync();
        
        Logger.Info($"Opened: {_packetSource.TotalPackets:N0} packets, {_packetSource.TotalDuration}");
        return true;
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to open recording");
        return false;
    }
}
```

### Complete Recording Example
```csharp
public async Task<string> RecordSessionAsync(string serverIp, int port, string outputPath)
{
    var recorder = new AudioPacketRecorder();
    
    try
    {
        // Connect
        await recorder.ConnectAsync(serverIp, port);
        
        // Start recording (creates temp .duckdb)
        recorder.StartRecording(outputPath);
        Logger.Info($"Recording started: {outputPath}");
        
        // Wait for user to stop...
        await _stopRecordingSignal.Task;
        
        // Stop recording (finalizes + compresses to .cvr)
        await recorder.StopRecordingAsync();
        
        // Output file is now .cvr format
        var cvrPath = Path.ChangeExtension(outputPath, ".cvr");
        Logger.Info($"Recording saved: {cvrPath}");
        
        return cvrPath;
    }
    finally
    {
        recorder.Disconnect();
    }
}
```

---

## ?? Best Practices

### 1. Always Use RecordingFileLoader
```csharp
// ? DO: Use RecordingFileLoader for all file openings
var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath);

// ? DON'T: Manually handle format detection
if (filePath.EndsWith(".cvr"))
{
    // Decompress...
}
else if (filePath.EndsWith(".adb"))
{
    // Convert...
}
```

### 2. Prefer IPacketSource Over Concrete Types
```csharp
// ? DO: Accept interface
public void ProcessPackets(IPacketSource source) { }

// ? DON'T: Depend on concrete type
public void ProcessPackets(FilePacketSource source) { }
```

### 3. Track Temp Files for Cleanup
```csharp
// ? DO: Track temp path
private string? _tempDbPath;

var (store, tempPath) = await RecordingFileLoader.OpenAsync(filePath);
_tempDbPath = tempPath;

// Later, in Dispose or cleanup:
if (_tempDbPath != null)
{
    RecordingFileLoader.Cleanup(_tempDbPath);
}
```

### 4. Handle Format-Specific Features Gracefully
```csharp
// ? DO: Check type when using format-specific features
if (packetSource is FilePacketSource fileSource)
{
    // Use FilePacketSource-specific feature
}
else
{
    // Fallback or alternative approach
}
```

---

## ?? Related Documentation

- `DuckDB-Implementation-Complete-Plan.md` - Overall roadmap
- `DuckDB-Playback-Implementation-Complete.md` - Playback details
- `DuckDB-Final-Verification.md` - Verification checklist
- `CVR-Format-Specification.md` - CVR format details

---

**Last Updated**: 2025-01-18  
**Version**: 1.0  
**Status**: ? Complete

?? Happy Coding! ??
