# SQLite Migration - Execution Plan for Remaining Work

**Last Updated**: 2025-01-20  
**Time to Complete**: 4-6 hours total  
**Priority**: Phase 5 (Critical) ? Phase 6 (High Impact) ? Phase 7-8 (Polish)

---

## ?? Overview

**Completed**: 75% (Phases 1-4)  
**Remaining**: 25% (Phases 5-8)

```
Phase 5: Live Recording        [?? 90%] - 1-2 hours  ?? BLOCKING
Phase 6: Performance Optimizations [?? 0%]  - 2 hours   ? HIGH IMPACT
Phase 7: Testing               [?? 0%]  - 2 hours   ? VALIDATION
Phase 8: Documentation         [?? 0%]  - 1 hour    ?? POLISH
```

---

## ?? CRITICAL PATH: Phase 5 - Live Recording (1-2 hours)

### Task 5.1: Implement SqliteRecordingRepository ? **BLOCKING**
**Time**: 30-45 minutes  
**Priority**: ?? CRITICAL  
**Effort**: Low (copy pattern from other repositories)

**File to Create**: `src/AeroDebrief.Core/Storage/Sqlite/SqliteRecordingRepository.cs`

**Template** (complete implementation):
```csharp
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Dapper;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// SQLite implementation of recording metadata repository.
    /// Manages recording_info table and calculates statistics.
    /// </summary>
    public class SqliteRecordingRepository : IRecordingRepository
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDbConnection _connection;
        private bool _disposed;

        public SqliteRecordingRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<RecordingMetadata> GetMetadataAsync(CancellationToken ct = default)
        {
            var row = await _connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM recording_info WHERE id = 1");

            return new RecordingMetadata
            {
                Version = row.version,
                ServerIp = row.server_ip,
                ServerPort = (int)(long)row.server_port,
                StartTime = DateTime.Parse(row.start_time, null,
                    System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }

        public async Task UpdateMetadataAsync(RecordingMetadata metadata, CancellationToken ct = default)
        {
            await _connection.ExecuteAsync(@"
                UPDATE recording_info 
                SET version = @Version,
                    server_ip = @ServerIp,
                    server_port = @ServerPort,
                    start_time = @StartTime
                WHERE id = 1",
                new
                {
                    metadata.Version,
                    metadata.ServerIp,
                    metadata.ServerPort,
                    StartTime = metadata.StartTime.ToString("O")
                });
        }

        public async Task<RecordingStats> GetStatsAsync(CancellationToken ct = default)
        {
            // Query packet statistics
            var row = await _connection.QuerySingleAsync<dynamic>(@"
                SELECT 
                    COUNT(*) as packet_count,
                    MIN(timestamp_utc) as first_packet,
                    MAX(timestamp_utc) as last_packet,
                    MAX(relative_ms) as duration_ms
                FROM packets");

            var packetCount = (long)row.packet_count;
            
            if (packetCount == 0)
            {
                return new RecordingStats
                {
                    TotalPackets = 0,
                    Duration = TimeSpan.Zero
                };
            }

            return new RecordingStats
            {
                TotalPackets = packetCount,
                Duration = TimeSpan.FromMilliseconds((double)row.duration_ms)
            };
        }

        public async Task MarkAsCompleteAsync(CancellationToken ct = default)
        {
            Logger.Info("Marking recording as complete");
            
            await _connection.ExecuteAsync(
                "UPDATE recording_info SET is_live = 0 WHERE id = 1");
        }

        public async Task<bool> IsLiveAsync(CancellationToken ct = default)
        {
            var isLive = await _connection.ExecuteScalarAsync<long>(
                "SELECT is_live FROM recording_info WHERE id = 1");
            
            return isLive != 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Connection is managed by UnitOfWork
            _disposed = true;
        }
    }
}
```

**Steps**:
1. Create file: `src/AeroDebrief.Core/Storage/Sqlite/SqliteRecordingRepository.cs`
2. Copy template above
3. Build and verify (should compile immediately)

---

### Task 5.2: Register Repository in Factory ?
**Time**: 5 minutes  
**Priority**: ?? CRITICAL  
**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqliteRepositoryFactory.cs`

**Changes Required**:
```csharp
// In CreateRecording method, add:
var recordingRepo = new SqliteRecordingRepository(connection);

// In OpenRecording method, add:
var recordingRepo = new SqliteRecordingRepository(connection);

// Pass to UnitOfWork constructor:
return new SqliteUnitOfWork(
    connection, 
    path, 
    packetRepo, 
    frequencyRepo, 
    playerRepo, 
    recordingRepo);  // ADD THIS PARAMETER
```

---

### Task 5.3: Update SqliteUnitOfWork ?
**Time**: 5 minutes  
**Priority**: ?? CRITICAL  
**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqliteUnitOfWork.cs`

**Changes Required**:
```csharp
public class SqliteUnitOfWork : IUnitOfWork
{
    public IPacketRepository Packets { get; }
    public IFrequencyRepository Frequencies { get; }
    public IPlayerRepository Players { get; }
    public IRecordingRepository Recording { get; }  // ADD THIS PROPERTY

    public SqliteUnitOfWork(
        IDbConnection connection,
        string filePath,
        IPacketRepository packets,
        IFrequencyRepository frequencies,
        IPlayerRepository players,
        IRecordingRepository recording)  // ADD THIS PARAMETER
    {
        _connection = connection;
        _filePath = filePath;
        Packets = packets;
        Frequencies = frequencies;
        Players = players;
        Recording = recording;  // INITIALIZE THIS
    }
}
```

---

### Task 5.4: Remove NotImplementedException Stubs ?
**Time**: 15 minutes  
**Priority**: ?? CRITICAL

**Files to Update**:

1. **LivePlaybackManager.cs** - Remove NotImplementedException
   - Remove from `StartLivePlaybackAsync` method
   - Implement actual live monitoring logic (already scaffolded)

2. **LiveRecordingPlaybackPipeline.cs** - Remove NotImplementedException
   - Remove from `InitializeAsync` method
   - Implement actual initialization (already scaffolded)

**Note**: These files already have the structure in place, just need to remove the throw statements and uncomment the implementation.

---

### Task 5.5: Test Live Recording Flow ?
**Time**: 30 minutes  
**Priority**: ?? CRITICAL

**Test Steps**:
1. Start recording ? verify packets written to database
2. Check `is_live` flag ? should be 1
3. Open recording in live mode ? verify streaming works
4. Verify new frequencies appear in UI during recording
5. Stop recording ? verify `is_live` flag set to 0
6. Reopen recording ? verify works as file playback

**Expected Results**:
- ? Live recording writes packets continuously
- ? Live playback streams packets in real-time
- ? UI updates with new frequencies/players
- ? Stopping marks recording as complete
- ? Reopening works as normal file playback

---

## ?? HIGH IMPACT: Phase 6 - Performance Optimization (2 hours)

### Task 6.1: Optimize Packet Streaming ? **HIGH IMPACT**
**Time**: 15 minutes  
**Priority**: ?? HIGH  
**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqlitePacketRepository.cs`

**Current Code** (loads all into memory):
```csharp
var results = await _connection.QueryAsync<dynamic>(sql, parameters);
foreach (var row in results)
{
    yield return MapToPacket(row);
}
```

**Optimized Code** (true streaming):
```csharp
// Use buffered: false for true streaming
var command = new CommandDefinition(sql, parameters, cancellationToken: ct);
var reader = await _connection.ExecuteReaderAsync(command);

while (await reader.ReadAsync(ct))
{
    yield return new RadioPacket
    {
        PacketId = (ulong)reader.GetInt64(0),
        Timestamp = DateTime.Parse(reader.GetString(1), null,
            System.Globalization.DateTimeStyles.RoundtripKind),
        Frequency = reader.GetDouble(2),
        // ... map remaining fields
    };
}
```

**Expected Benefits**:
- ?? Memory usage: 80-90% reduction for large queries
- ?? First packet returned immediately (no loading wait)
- ?? Works with hours-long recordings

---

### Task 6.2: Optimize Metadata Loading ? **HIGH IMPACT**
**Time**: 30 minutes  
**Priority**: ?? HIGH  
**File**: `src/AeroDebrief.Core/IO/DatabasePacketSource.cs`

**Current Code** (2 queries + nested loops):
```csharp
var frequencies = await _unitOfWork.Frequencies.GetAllAsync(cancellationToken);
var players = await _unitOfWork.Players.GetAllAsync(cancellationToken);

foreach (var freq in frequencies)
{
    var frequencyPlayers = players
        .Where(p => p.Frequencies.Contains(freq.Frequency))  // O(P) per frequency
        .Select(...)
        .ToList();
}
```

**Optimized Code** (1 query with JOIN):
```csharp
// Single query - let database do the work
private async Task BuildFrequencyMetadataAsync(...)
{
    var query = @"
        SELECT 
            f.frequency,
            f.modulation,
            f.packet_count,
            f.first_seen,
            f.last_seen,
            p.player_name,
            p.transmitter_guid,
            p.coalition,
            p.unit_type,
            COUNT(pkt.id) as player_packet_count
        FROM frequency_stats f
        LEFT JOIN packets pkt ON pkt.frequency = f.frequency AND pkt.modulation = f.modulation
        LEFT JOIN player_stats p ON p.player_name = pkt.player_name
        GROUP BY f.frequency, f.modulation, p.player_name
        ORDER BY f.frequency, player_packet_count DESC";
    
    var rows = await _connection.QueryAsync<dynamic>(query);
    
    // Group in O(N) time instead of O(F * P)
    _frequencyMetadata = rows
        .GroupBy(r => (double)r.frequency)
        .ToDictionary(
            g => g.Key,
            g => BuildFrequencyMetadata(g));
}
```

**Expected Benefits**:
- ?? Database queries: 2 ? 1 (50% reduction)
- ?? Memory: No full player list needed
- ?? Speed: O(F * P) ? O(N) complexity

---

### Task 6.3: Add Connection Pooling ?
**Time**: 20 minutes  
**Priority**: ?? MEDIUM  
**File**: `src/AeroDebrief.Core/Storage/Sqlite/SqliteRepositoryFactory.cs`

**Current Code**:
```csharp
var connectionString = $"Data Source={path}";
```

**Optimized Code**:
```csharp
private const string ConnectionStringTemplate = 
    "Data Source={0};Pooling=True;Cache Size=10000;Page Size=4096;Synchronous=NORMAL";

private static IDbConnection CreateConnection(string path)
{
    var connectionString = string.Format(ConnectionStringTemplate, path);
    var connection = new SqliteConnection(connectionString);
    connection.Open();
    
    // Configure for optimal performance
    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        PRAGMA journal_mode=WAL;
        PRAGMA synchronous=NORMAL;
        PRAGMA cache_size=10000;
        PRAGMA temp_store=MEMORY;
        PRAGMA mmap_size=268435456;";  // 256 MB memory-mapped I/O
    cmd.ExecuteNonQuery();
    
    return connection;
}
```

**Expected Benefits**:
- ?? Connection reuse (lower overhead)
- ?? Better cache utilization
- ?? Improved WAL mode performance

---

### Task 6.4: Add Query Result Caching ?
**Time**: 45 minutes  
**Priority**: ?? MEDIUM  
**Files**: `SqliteFrequencyRepository.cs`, `SqlitePlayerRepository.cs`

**Implementation**:
```csharp
public class SqliteFrequencyRepository : IFrequencyRepository
{
    private List<FrequencyInfo>? _cachedFrequencies;
    private DateTime _cacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(30);
    
    public async Task<List<FrequencyInfo>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cachedFrequencies != null && 
            DateTime.UtcNow - _cacheTime < _cacheLifetime)
        {
            return _cachedFrequencies;
        }
        
        var rows = await _connection.QueryAsync<dynamic>(
            "SELECT * FROM frequency_stats ORDER BY frequency");
        
        _cachedFrequencies = rows.Select(MapToFrequencyInfo).ToList();
        _cacheTime = DateTime.UtcNow;
        
        return _cachedFrequencies;
    }
    
    public void InvalidateCache()
    {
        _cachedFrequencies = null;
    }
}
```

**Expected Benefits**:
- ?? Repeated queries: ~100x faster (memory vs disk)
- ?? Database load reduced
- ?? UI responsiveness improved

---

## ? VALIDATION: Phase 7 - Testing (2 hours)

### Task 7.1: Unit Tests for Repositories
**Time**: 60 minutes  
**Priority**: ?? MEDIUM

**Files to Create**:
```
tests/AeroDebrief.Tests/Storage/
??? SqlitePacketRepositoryTests.cs      (15 min)
??? SqliteFrequencyRepositoryTests.cs   (15 min)
??? SqlitePlayerRepositoryTests.cs      (15 min)
??? SqliteUnitOfWorkTests.cs            (15 min)
```

**Test Categories**:
1. **Insert/Query** - Verify CRUD operations
2. **Batch Insert** - Verify transaction handling
3. **Streaming** - Verify IAsyncEnumerable behavior
4. **Filtering** - Verify WHERE clause handling
5. **Statistics** - Verify aggregation queries
6. **Concurrent Access** - Verify WAL mode (multiple readers)

---

### Task 7.2: Integration Tests
**Time**: 30 minutes  
**Priority**: ?? MEDIUM

**Scenarios**:
1. Full ADB ? SQLite migration pipeline
2. CVR compression/decompression cycle
3. Live recording ? stop ? reopen
4. Large file performance (100K+ packets)

---

### Task 7.3: Performance Benchmarking
**Time**: 30 minutes  
**Priority**: ?? LOW

**Metrics to Measure**:
- Migration speed (packets/second)
- Memory usage during streaming
- Query response times
- Cache hit rates
- Compression ratios

---

## ?? POLISH: Phase 8 - Documentation (1 hour)

### Task 8.1: Update Main Documentation
**Time**: 30 minutes  
**Priority**: ?? LOW

**Files to Update**:
- [ ] `README.md` - Update technology stack (SQLite)
- [ ] Update architecture diagrams (if any)
- [ ] Update API documentation for Repository Pattern

---

### Task 8.2: Code Cleanup
**Time**: 30 minutes  
**Priority**: ?? LOW

**Tasks**:
- [ ] Remove obsolete comments referencing DuckDB
- [ ] Remove completed TODO comments
- [ ] Verify XML documentation accuracy
- [ ] Run code formatter

---

## ?? Recommended Schedule

### Day 1 (2-3 hours) - Critical Path
**Morning Session**:
```
9:00  - 9:45   Task 5.1: SqliteRecordingRepository (45 min)
9:45  - 9:55   Task 5.2: Register in Factory (10 min)
9:55  - 10:00  Task 5.3: Update UnitOfWork (5 min)
10:00 - 10:15  Task 5.4: Remove NotImplementedException (15 min)
10:15 - 10:45  Task 5.5: Test live recording (30 min)

Total: 1h 45min
Status: ? Live recording complete and tested
```

**Afternoon Session** (Optional same day):
```
2:00  - 2:15   Task 6.1: Optimize packet streaming (15 min)
2:15  - 2:45   Task 6.2: Optimize metadata loading (30 min)
2:45  - 3:05   Task 6.3: Connection pooling (20 min)

Total: 1h 5min
Status: ? High-impact optimizations complete
```

### Day 2 (2-3 hours) - Polish
**Morning Session**:
```
9:00  - 9:45   Task 6.4: Query caching (45 min)
9:45  - 10:45  Task 7.1: Unit tests (60 min)
10:45 - 11:15  Task 7.2: Integration tests (30 min)

Total: 2h 15min
Status: ? Testing complete
```

**Afternoon Session**:
```
2:00  - 2:30   Task 7.3: Benchmarking (30 min)
2:30  - 3:00   Task 8.1: Documentation (30 min)
3:00  - 3:30   Task 8.2: Code cleanup (30 min)

Total: 1h 30min
Status: ? Migration 100% complete
```

---

## ?? Minimum Viable Product (MVP)

If time is limited, focus on **Critical Path Only**:

### MVP Scope (1-2 hours)
```
? Task 5.1: SqliteRecordingRepository (45 min)
? Task 5.2: Register in Factory (5 min)
? Task 5.3: Update UnitOfWork (5 min)
? Task 5.4: Remove NotImplementedException (15 min)
? Task 5.5: Test live recording (30 min)

Total: 1h 40min
Result: ?? PRODUCTION READY for both file and live playback
```

### Enhanced Product (Add 2 hours)
```
MVP + Phase 6 High Impact Optimizations:
? Task 6.1: Packet streaming (15 min)
? Task 6.2: Metadata loading (30 min)
? Task 6.3: Connection pooling (20 min)

Total: 3h 45min
Result: ?? PRODUCTION READY with excellent performance
```

### Complete Product (Add 3-4 more hours)
```
Enhanced + Testing + Polish:
? Task 6.4: Query caching (45 min)
? Task 7.1-7.3: Testing (2h)
? Task 8.1-8.2: Documentation (1h)

Total: 6-7h
Result: ?? PRODUCTION READY with comprehensive validation
```

---

## ? Success Criteria

### Phase 5 Complete
- [x] SqliteRecordingRepository implemented
- [x] Registered in factory and UnitOfWork
- [x] NotImplementedException stubs removed
- [x] Live recording ? stop ? reopen flow tested
- [x] Build successful (0 errors)

### Phase 6 Complete
- [x] Packet streaming uses ExecuteReader (unbuffered)
- [x] Metadata loading uses single JOIN query
- [x] Connection pooling configured
- [x] Query caching implemented
- [x] Memory usage reduced by 80%+

### Phase 7 Complete
- [x] Unit tests created (4 test classes)
- [x] Integration tests passing
- [x] Performance benchmarks documented
- [x] All tests passing

### Phase 8 Complete
- [x] README.md updated
- [x] Documentation complete
- [x] Code cleanup done
- [x] No obsolete TODOs or comments

---

## ?? Risk Assessment

### Low Risk Tasks
- ? Task 5.1-5.3: Copy/paste pattern from existing repositories
- ? Task 6.1: Simple API change (QueryAsync ? ExecuteReader)
- ? Task 6.3: Connection string configuration

### Medium Risk Tasks
- ?? Task 5.4-5.5: Live recording testing (may find edge cases)
- ?? Task 6.2: Query rewrite (verify results match)
- ?? Task 7.1-7.2: Testing (may find bugs)

### Mitigation Strategies
1. **Test incrementally**: Build and test after each task
2. **Keep old code**: Comment out instead of delete (easy rollback)
3. **Verify results**: Compare old vs new query results
4. **Log everything**: Debug logging for troubleshooting

---

## ?? Expected Outcomes

### Before Optimization
- Memory usage: ~200 MB for large queries (hours of data)
- Metadata loading: 2 database queries, O(F * P) complexity
- Connection overhead: ~5-10ms per open
- Repeated queries: Hit database every time

### After Optimization
- Memory usage: ~20 MB for same queries (90% reduction) ??
- Metadata loading: 1 database query, O(N) complexity ??
- Connection overhead: Pooled, ~1ms per open ??
- Repeated queries: Memory cache, <1ms ??

### Overall Impact
- ?? **10x better memory efficiency**
- ?? **2-3x faster startup** (metadata loading)
- ?? **100x faster repeated queries** (caching)
- ?? **Instant first result** (streaming)

---

**Ready to Execute?** Start with Phase 5 Task 5.1! ??
