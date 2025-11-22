# SQLite Storage - Quick Reference Guide

## Overview

AeroDebrief uses SQLite with Dapper ORM and the Repository Pattern for all storage operations. This guide provides quick reference for common tasks.

---

## Quick Start

### Opening a Database
```csharp
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Sqlite;

// Create factory
var factory = new SqliteRepositoryFactory();

// Open existing recording
using var uow = factory.OpenRecording("recording.db");

// Or create new recording
var metadata = new RecordingMetadata 
{ 
    Title = "My Recording",
    StartTime = DateTime.UtcNow
};
using var uow = factory.CreateRecording("new-recording.db", metadata);
```

---

## Common Operations

### Inserting Packets

#### Single Packet
```csharp
var packet = new RadioPacket
{
    Frequency = 251.0,
    Modulation = 0,
    PlayerName = "Viper 1-1",
    Timestamp = DateTime.UtcNow,
    AudioData = audioBytes
};

await uow.Packets.InsertAsync(packet, ct);
await uow.CommitAsync(ct);
```

#### Batch Insert (Recommended)
```csharp
var packets = new List<RadioPacket>();
// ... add packets ...

await uow.Packets.InsertBatchAsync(packets, ct);
await uow.CommitAsync(ct);
```

### Querying Packets

#### By Time Range
```csharp
var startTime = TimeSpan.FromMinutes(5);
var endTime = TimeSpan.FromMinutes(10);

await foreach (var packet in uow.Packets.StreamAsync(startTime, endTime, ct))
{
    Console.WriteLine($"{packet.PlayerName} on {packet.Frequency} MHz");
}
```

#### By Frequency
```csharp
double frequency = 251.0;
int modulation = 0;

await foreach (var packet in uow.Packets.StreamAsync(
    frequency: frequency, 
    modulation: modulation, 
    cancellationToken: ct))
{
    Console.WriteLine($"{packet.PlayerName}: {packet.AudioData.Length} bytes");
}
```

#### By Player
```csharp
string playerName = "Viper 1-1";

await foreach (var packet in uow.Packets.StreamAsync(
    playerName: playerName, 
    cancellationToken: ct))
{
    Console.WriteLine($"Frequency: {packet.Frequency} MHz");
}
```

#### Count Packets
```csharp
long totalPackets = await uow.Packets.CountAsync(ct);
long frequencyPackets = await uow.Packets.CountAsync(251.0, 0, ct);
```

### Recording Metadata

#### Get Recording Info
```csharp
var metadata = await uow.Recording.GetMetadataAsync(ct);
Console.WriteLine($"Title: {metadata.Title}");
Console.WriteLine($"Duration: {metadata.Duration}");
Console.WriteLine($"Start: {metadata.StartTime}");
```

#### Update Recording Info
```csharp
var metadata = await uow.Recording.GetMetadataAsync(ct);
metadata.Title = "Updated Title";
await uow.Recording.UpdateMetadataAsync(metadata, ct);
await uow.CommitAsync(ct);
```

#### Get Recording Statistics
```csharp
var stats = await uow.Recording.GetStatsAsync(ct);
Console.WriteLine($"Total packets: {stats.TotalPackets}");
Console.WriteLine($"Duration: {stats.Duration}");
Console.WriteLine($"First packet: {stats.FirstPacketTime}");
Console.WriteLine($"Last packet: {stats.LastPacketTime}");
```

#### Finalize Live Recording
```csharp
await uow.Recording.MarkFinalizedAsync(ct);
await uow.CommitAsync(ct);
```

### Frequency Analysis

#### Get All Frequencies
```csharp
var frequencies = await uow.Frequencies.GetAllAsync(ct);
foreach (var freq in frequencies)
{
    Console.WriteLine($"{freq.Frequency} MHz ({freq.ModulationName}): {freq.PacketCount} packets");
}
```

#### Get Specific Frequency
```csharp
var freq = await uow.Frequencies.GetAsync(251.0, 0, ct);
if (freq != null)
{
    Console.WriteLine($"First seen: {freq.FirstSeen}");
    Console.WriteLine($"Last seen: {freq.LastSeen}");
    Console.WriteLine($"Packet count: {freq.PacketCount}");
}
```

### Player Analysis

#### Get All Players
```csharp
var players = await uow.Players.GetAllAsync(ct);
foreach (var player in players)
{
    Console.WriteLine($"{player.Name}: {player.PacketCount} transmissions");
    Console.WriteLine($"  Frequencies: {string.Join(", ", player.Frequencies)}");
    Console.WriteLine($"  Coalition: {player.CoalitionName}");
}
```

#### Get Specific Player
```csharp
var player = await uow.Players.GetAsync("Viper 1-1", ct);
if (player != null)
{
    Console.WriteLine($"First seen: {player.FirstSeen}");
    Console.WriteLine($"Last seen: {player.LastSeen}");
    Console.WriteLine($"Unit: {player.UnitType}");
}
```

---

## Transaction Management

### Manual Transaction Control
```csharp
// Unit of Work automatically manages transactions
using var uow = factory.OpenRecording("recording.db");

// Make changes
await uow.Packets.InsertAsync(packet1, ct);
await uow.Packets.InsertAsync(packet2, ct);

// Commit (writes to database)
await uow.CommitAsync(ct);

// Or rollback on error
// await uow.RollbackAsync(ct);
```

### Auto-commit Pattern
```csharp
// Dispose automatically commits if not rolled back
using (var uow = factory.OpenRecording("recording.db"))
{
    await uow.Packets.InsertAsync(packet, ct);
    // Automatically committed on dispose
}
```

---

## Advanced Patterns

### Streaming Large Result Sets
```csharp
// Use IAsyncEnumerable for memory efficiency
await foreach (var packet in uow.Packets.StreamAsync(ct))
{
    // Process one packet at a time
    // Memory usage stays low even with millions of packets
    ProcessPacket(packet);
}
```

### Batch Processing with Progress
```csharp
var batchSize = 1000;
var batch = new List<RadioPacket>(batchSize);

foreach (var packet in packetSource)
{
    batch.Add(packet);
    
    if (batch.Count >= batchSize)
    {
        await uow.Packets.InsertBatchAsync(batch, ct);
        await uow.CommitAsync(ct);
        batch.Clear();
        
        // Report progress
        Console.WriteLine($"Processed {totalProcessed} packets");
    }
}

// Process remaining
if (batch.Count > 0)
{
    await uow.Packets.InsertBatchAsync(batch, ct);
    await uow.CommitAsync(ct);
}
```

### Concurrent Read Access (WAL Mode)
```csharp
// SQLite WAL mode allows multiple readers + 1 writer
var factory = new SqliteRepositoryFactory();

// Reader 1
var reader1 = factory.OpenRecording("live-recording.db");
var metadata1 = await reader1.Recording.GetMetadataAsync(ct);

// Reader 2 (concurrent)
var reader2 = factory.OpenRecording("live-recording.db");
var stats2 = await reader2.Recording.GetStatsAsync(ct);

// Writer (single writer allowed)
var writer = factory.OpenRecording("live-recording.db");
await writer.Packets.InsertAsync(packet, ct);
```

---

## File Formats

### Database File (.db)
```
Uncompressed SQLite database
- Extension: .db
- WAL mode enabled
- Direct SQL access
- ~30-40 MB per hour of multi-frequency recording
```

### Compressed Archive (.cvr)
```
Zstandard compressed database
- Extension: .cvr
- 67-75% size reduction
- ~10-13 MB per hour of recording
- Automatic decompression on load
```

### Creating CVR Archive
```csharp
using AeroDebrief.Core.Storage;

// Compress database to CVR
var cvrPath = await CvrFormat.CompressAsync(
    dbPath: "recording.db",
    cvrPath: "recording.cvr",
    ct: cancellationToken
);

// Decompress CVR to database
var dbPath = await CvrFormat.DecompressAsync(
    cvrPath: "recording.cvr",
    ct: cancellationToken
);
```

---

## Performance Tips

### 1. Use Batch Inserts
```csharp
// ? Good: Batch insert (1000x faster)
await uow.Packets.InsertBatchAsync(packets, ct);

// ? Bad: Individual inserts
foreach (var packet in packets)
    await uow.Packets.InsertAsync(packet, ct);
```

### 2. Use Streaming for Large Queries
```csharp
// ? Good: Stream results (low memory)
await foreach (var packet in uow.Packets.StreamAsync(ct))
    Process(packet);

// ? Bad: Load all into memory
var allPackets = await uow.Packets.GetAllAsync(ct);
```

### 3. Commit After Batch Operations
```csharp
// ? Good: Commit after batch
await uow.Packets.InsertBatchAsync(packets, ct);
await uow.CommitAsync(ct);

// ? Bad: Commit after each insert
foreach (var packet in packets)
{
    await uow.Packets.InsertAsync(packet, ct);
    await uow.CommitAsync(ct); // Too many commits!
}
```

### 4. Reuse UnitOfWork
```csharp
// ? Good: Reuse connection
using var uow = factory.OpenRecording("recording.db");
for (int i = 0; i < 10; i++)
{
    var stats = await uow.Recording.GetStatsAsync(ct);
    // ... process ...
}

// ? Bad: Create new connection each time
for (int i = 0; i < 10; i++)
{
    using var uow = factory.OpenRecording("recording.db");
    var stats = await uow.Recording.GetStatsAsync(ct);
}
```

---

## Common Pitfalls

### ? Forgetting to Commit
```csharp
// Changes not saved!
using var uow = factory.OpenRecording("recording.db");
await uow.Packets.InsertAsync(packet, ct);
// Forgot: await uow.CommitAsync(ct);
```

### ? Not Disposing UnitOfWork
```csharp
// Connection leak!
var uow = factory.OpenRecording("recording.db");
await uow.Packets.InsertAsync(packet, ct);
// Forgot: uow.Dispose() or using statement
```

### ? Loading Large Result Sets into Memory
```csharp
// OutOfMemoryException for large recordings!
var allPackets = new List<RadioPacket>();
await foreach (var packet in uow.Packets.StreamAsync(ct))
    allPackets.Add(packet); // Don't do this!
```

---

## Testing

### In-Memory Database (Fast Tests)
```csharp
var factory = new SqliteRepositoryFactory();
using var uow = factory.CreateRecording(
    path: ":memory:",
    metadata: new RecordingMetadata { Title = "Test" }
);

// Run tests...
```

### Temporary File Database
```csharp
var tempPath = Path.GetTempFileName() + ".db";
try
{
    using var uow = factory.CreateRecording(tempPath, metadata);
    // Run tests...
}
finally
{
    File.Delete(tempPath);
}
```

---

## Troubleshooting

### Database Locked Error
```
Issue: SQLite returns "database is locked"
Cause: Multiple writers or unclosed connections

Solutions:
1. Ensure all UnitOfWork instances are disposed
2. Use WAL mode (already enabled by default)
3. Check for concurrent writers (only 1 allowed)
```

### Out of Memory
```
Issue: Application runs out of memory
Cause: Loading too many packets at once

Solutions:
1. Use StreamAsync() instead of loading all
2. Process packets one at a time
3. Use batch processing with smaller batches
```

### Slow Queries
```
Issue: Queries taking too long
Cause: Missing indexes or full table scans

Solutions:
1. Ensure schema includes proper indexes
2. Use specific queries (frequency, player, time range)
3. Avoid SELECT * in hot paths
```

---

## API Reference

### Interfaces
```csharp
IRepositoryFactory      // Factory for creating/opening recordings
IUnitOfWork            // Transaction coordinator
IPacketRepository      // Packet CRUD operations
IFrequencyRepository   // Frequency statistics
IPlayerRepository      // Player statistics
IRecordingRepository   // Recording metadata
```

### Implementations
```csharp
SqliteRepositoryFactory    // SQLite factory implementation
SqliteUnitOfWork          // SQLite transaction management
SqlitePacketRepository    // SQLite packet operations
SqliteFrequencyRepository // SQLite frequency stats
SqlitePlayerRepository    // SQLite player stats
SqliteRecordingRepository // SQLite recording metadata
```

### Data Models
```csharp
RadioPacket           // Packet data transfer object
FrequencyInfo         // Frequency statistics
PlayerInfo            // Player information
RecordingMetadata     // Recording metadata
RecordingStats        // Recording statistics
```

---

## Additional Resources

- **Implementation Plan**: `docs/SQLite-Migration-Implementation-Plan.md`
- **Architecture**: `docs/SQLite-Migration-Complete.md`
- **Test Examples**: `tests/AeroDebrief.Tests/Storage/`
- **Schema Definition**: `src/AeroDebrief.Core/Storage/Schema.sqlite.sql`

---

**Quick Reference Guide**  
**Version**: 1.0  
**Last Updated**: Phase 8 Complete
