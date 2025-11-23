# ?? Quick Action: Complete Phase 1-2

## What's Done ?

1. ? **Project File Updated**
   - DuckDB removed
   - SQLite + Dapper added
   - Version updated to 2.0.0

2. ? **Repository Pattern Implemented**
   - All interfaces created
   - All SQLite implementations created
   - Converter updated

## What's Needed ??

### Single Manual Step: Populate Schema File

**File**: `src\AeroDebrief.Core\Storage\Schema.sqlite.sql`  
**Status**: Created but empty  
**Action**: Copy schema content below

## Copy This Schema Content

Open `src\AeroDebrief.Core\Storage\Schema.sqlite.sql` and paste:

```sql
CREATE TABLE IF NOT EXISTS packets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    relative_ms INTEGER NOT NULL,
    frequency REAL NOT NULL,
    modulation INTEGER NOT NULL,
    player_name TEXT NOT NULL,
    transmitter_guid TEXT NOT NULL,
    coalition INTEGER NOT NULL,
    unit_type TEXT,
    unit_id INTEGER,
    audio_data BLOB NOT NULL,
    sample_rate INTEGER NOT NULL DEFAULT 48000,
    encryption INTEGER DEFAULT 0,
    channel_count INTEGER DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_time ON packets(relative_ms);
CREATE INDEX IF NOT EXISTS idx_frequency ON packets(frequency);
CREATE INDEX IF NOT EXISTS idx_player ON packets(player_name);
CREATE INDEX IF NOT EXISTS idx_freq_time ON packets(frequency, relative_ms);

CREATE TABLE IF NOT EXISTS recording_info (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    version TEXT NOT NULL DEFAULT 'SQLite-v2',
    server_ip TEXT NOT NULL,
    server_port INTEGER NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT,
    packet_count INTEGER DEFAULT 0,
    duration_ms INTEGER DEFAULT 0,
    is_live INTEGER DEFAULT 1,
    created_at TEXT DEFAULT (datetime('now', 'utc')),
    last_updated TEXT DEFAULT (datetime('now', 'utc'))
);

CREATE TABLE IF NOT EXISTS frequency_stats (
    frequency REAL NOT NULL,
    modulation INTEGER NOT NULL,
    packet_count INTEGER NOT NULL,
    first_seen TEXT NOT NULL,
    last_seen TEXT NOT NULL,
    player_count INTEGER NOT NULL,
    total_duration_ms INTEGER NOT NULL,
    PRIMARY KEY (frequency, modulation)
);

CREATE TABLE IF NOT EXISTS player_stats (
    player_name TEXT NOT NULL,
    transmitter_guid TEXT NOT NULL,
    coalition INTEGER NOT NULL,
    unit_type TEXT,
    transmission_count INTEGER NOT NULL,
    first_seen TEXT NOT NULL,
    last_seen TEXT NOT NULL,
    frequencies TEXT NOT NULL,
    PRIMARY KEY (player_name, transmitter_guid)
);

PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA cache_size=-64000;
PRAGMA temp_store=MEMORY;
PRAGMA mmap_size=30000000000;
```

## Then Run These Commands

```powershell
# 1. Restore packages
dotnet restore

# 2. Build Core project
dotnet build src\AeroDebrief.Core

# 3. Verify packages installed
dotnet list src\AeroDebrief.Core package | Select-String -Pattern "Microsoft.Data.Sqlite|Dapper"
```

## Expected Output

You should see:
```
> Microsoft.Data.Sqlite        9.0.0
> Dapper                        2.1.35
```

## Test It Works

Create a simple test:

```powershell
# Create test file
cd src\AeroDebrief.Core\Storage\Sqlite

# Run quick test (in C# Interactive or create console app)
```

Or just build and verify no compilation errors!

## Success Criteria

- [x] Project file updated
- [ ] Schema file populated (? **DO THIS NOW**)
- [ ] `dotnet restore` succeeds
- [ ] `dotnet build` succeeds
- [ ] SQLite + Dapper packages listed

---

**Time Required**: 2 minutes  
**Current Status**: 95% complete, just needs schema content!
