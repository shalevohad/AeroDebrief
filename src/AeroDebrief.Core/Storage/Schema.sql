-- AeroDebrief DuckDB Schema
-- Optimized for concurrent read/write during live recording
-- Version: 1.0

-- =============================================================================
-- Main packets table with columnar storage for optimal compression and queries
-- =============================================================================
CREATE TABLE packets (
    -- Primary key (auto-increment for live recording)
    id UBIGINT PRIMARY KEY,
    
    -- Time indexes (most common query pattern)
    timestamp_utc TIMESTAMP NOT NULL,
    relative_ms BIGINT NOT NULL,  -- Milliseconds from recording start
    
    -- Radio metadata (frequently filtered)
    frequency DOUBLE NOT NULL,
    modulation UTINYINT NOT NULL,
    
    -- Transmitter info (for player filtering)
    player_name VARCHAR NOT NULL,
    transmitter_guid VARCHAR NOT NULL,
    coalition UTINYINT NOT NULL,
    unit_type VARCHAR,
    unit_id UINTEGER,
    
    -- Audio data (BLOB with compression)
    audio_data BLOB NOT NULL,
    sample_rate UINTEGER NOT NULL DEFAULT 48000,
    
    -- Less frequently accessed metadata
    encryption UTINYINT DEFAULT 0,
    channel_count UTINYINT DEFAULT 1
);

-- Indexes for common query patterns
-- DuckDB automatically creates optimal indexes for columnar data
CREATE INDEX idx_time ON packets(relative_ms);
CREATE INDEX idx_frequency ON packets(frequency);
CREATE INDEX idx_player ON packets(player_name);
CREATE INDEX idx_freq_time ON packets(frequency, relative_ms);

-- =============================================================================
-- Recording metadata table (SINGLE ROW - one file = one recording)
-- =============================================================================
CREATE TABLE recording_info (
    -- Single row constraint enforced by constant primary key
    id INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    
    version VARCHAR NOT NULL DEFAULT 'DuckDB-v1',
    server_ip VARCHAR NOT NULL,
    server_port INTEGER NOT NULL,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP,  -- NULL during live recording
    packet_count BIGINT DEFAULT 0,
    duration_ms BIGINT DEFAULT 0,
    is_live BOOLEAN DEFAULT TRUE,  -- TRUE during recording, FALSE after finalize
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =============================================================================
-- Pre-computed frequency statistics (materialized view)
-- Updated periodically during live recording and once at finalize
-- =============================================================================
CREATE TABLE frequency_stats (
    frequency DOUBLE NOT NULL,
    modulation UTINYINT NOT NULL,
    packet_count BIGINT NOT NULL,
    first_seen TIMESTAMP NOT NULL,
    last_seen TIMESTAMP NOT NULL,
    player_count INTEGER NOT NULL,
    total_duration_ms BIGINT NOT NULL,
    PRIMARY KEY (frequency, modulation)
);

-- =============================================================================
-- Pre-computed player statistics (materialized view)
-- Updated periodically during live recording and once at finalize
-- =============================================================================
CREATE TABLE player_stats (
    player_name VARCHAR NOT NULL,
    transmitter_guid VARCHAR NOT NULL,
    coalition UTINYINT NOT NULL,
    unit_type VARCHAR,
    transmission_count BIGINT NOT NULL,
    first_seen TIMESTAMP NOT NULL,
    last_seen TIMESTAMP NOT NULL,
    frequencies DOUBLE[] NOT NULL,  -- Array of frequencies used
    PRIMARY KEY (player_name, transmitter_guid)
);

-- =============================================================================
-- Waveform tiles for fast UI rendering (future optimization)
-- Pre-computed amplitude data at different zoom levels
-- =============================================================================
CREATE TABLE waveform_tiles (
    tile_id UBIGINT PRIMARY KEY,
    frequency DOUBLE NOT NULL,
    start_ms BIGINT NOT NULL,
    end_ms BIGINT NOT NULL,
    zoom_level INTEGER NOT NULL,  -- 0=1ms, 1=10ms, 2=100ms, 3=1s
    sample_count INTEGER NOT NULL,
    min_amplitude FLOAT NOT NULL,
    max_amplitude FLOAT NOT NULL,
    rms_amplitude FLOAT NOT NULL,
    -- Optional: compressed waveform samples for higher zoom levels
    waveform_data BLOB
);

CREATE INDEX idx_waveform_query ON waveform_tiles(frequency, zoom_level, start_ms);

-- =============================================================================
-- Configuration for DuckDB optimizations
-- =============================================================================
-- Enable parallel processing (will be set programmatically)
-- PRAGMA threads=4;
-- PRAGMA memory_limit='2GB';

-- Enable write-ahead logging for concurrent access
-- PRAGMA wal_autocheckpoint=1000;

-- Optimize for columnar compression
-- PRAGMA enable_object_cache=true;
