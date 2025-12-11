-- =============================================================================
-- SQLite Schema for AeroDebrief Recordings
-- Version: 2.1 (Added Amplitude Cache for instant waveform rendering)
-- =============================================================================

-- =============================================================================
-- Main packets table
-- Stores all audio transmission packets with metadata
-- UNIT STANDARDS:
--   - frequency: Hz (e.g., 127500000.0 for 127.5 MHz)
--   - relative_ms: milliseconds since recording start
--   - sample_rate: Hz (typically 48000)
-- =============================================================================
CREATE TABLE IF NOT EXISTS packets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- Time indexes (most common query pattern)
    timestamp_utc TEXT NOT NULL,
    relative_ms INTEGER NOT NULL,
    
    -- Radio metadata (frequently filtered)
    frequency REAL NOT NULL,  -- Hz (e.g., 127500000.0 = 127.5 MHz)
    modulation INTEGER NOT NULL,
    
    -- Transmitter info (for player filtering)
    player_name TEXT NOT NULL,
    transmitter_guid TEXT NOT NULL,
    coalition INTEGER NOT NULL,
    unit_type TEXT,
    unit_id INTEGER,
    
    -- Audio data (BLOB with efficient storage)
    audio_data BLOB NOT NULL,
    sample_rate INTEGER NOT NULL DEFAULT 48000,
    
    -- Additional metadata
    encryption INTEGER DEFAULT 0,
    channel_count INTEGER DEFAULT 1
);

-- Indexes for common query patterns (optimized for filtering and sorting)
CREATE INDEX IF NOT EXISTS idx_time ON packets(relative_ms);
CREATE INDEX IF NOT EXISTS idx_frequency ON packets(frequency);
CREATE INDEX IF NOT EXISTS idx_player ON packets(player_name);
CREATE INDEX IF NOT EXISTS idx_freq_time ON packets(frequency, relative_ms);
CREATE INDEX IF NOT EXISTS idx_coalition ON packets(coalition);

-- =============================================================================
-- ? NEW: Amplitude cache for instant waveform rendering (Phase 7)
-- Stores pre-computed RAW amplitude data (before volume/gain/pan adjustments).
-- CRITICAL: Volume/gain/pan are applied at QUERY time, not storage time!
-- This ensures cached data remains valid regardless of mixer settings.
--
-- Performance benefit: 10-100x faster graph loading by eliminating re-decoding.
-- Memory footprint: ~100-200 bytes per packet (vs. 10-50 KB for raw audio).
-- =============================================================================
CREATE TABLE IF NOT EXISTS amplitude_cache (
    packet_id INTEGER PRIMARY KEY,
    
    -- RAW amplitude values (0.0-1.0 range, before volume adjustment)
    max_amplitude REAL NOT NULL,      -- Peak amplitude in packet
    rms_amplitude REAL NOT NULL,      -- Root mean square amplitude
    
    -- Peak envelope for detailed waveform rendering (BLOB: float array)
    -- Each point represents peak amplitude in a 10ms window (480 samples @ 48kHz)
    peak_envelope BLOB,               
    envelope_points INTEGER DEFAULT 0, -- Number of points in envelope
    
    -- Metadata for cache management
    computed_at TEXT NOT NULL,        -- UTC timestamp when computed
    
    FOREIGN KEY (packet_id) REFERENCES packets(id) ON DELETE CASCADE
);

-- Index for fast amplitude queries (critical for graph rendering)
CREATE INDEX IF NOT EXISTS idx_amplitude_packet ON amplitude_cache(packet_id);

-- =============================================================================
-- Recording metadata table (SINGLE ROW - one file = one recording)
-- =============================================================================
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

-- =============================================================================
-- Pre-computed frequency statistics (materialized view)
-- Updated periodically during live recording and once at finalize
-- =============================================================================
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

CREATE INDEX IF NOT EXISTS idx_freq_stats_count ON frequency_stats(packet_count DESC);

-- =============================================================================
-- Pre-computed player statistics (materialized view)
-- Updated periodically during live recording and once at finalize
-- =============================================================================
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

CREATE INDEX IF NOT EXISTS idx_player_stats_count ON player_stats(transmission_count DESC);
CREATE INDEX IF NOT EXISTS idx_player_stats_coalition ON player_stats(coalition);

-- =============================================================================
-- NOTE: WAL mode and performance PRAGMAs are configured in SqliteUnitOfWork.cs
-- Do NOT include PRAGMA statements in this schema file as they are executed
-- separately by ConfigureConnectionAsync() before schema creation.
-- =============================================================================
