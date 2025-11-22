namespace AeroDebrief.Core
{
    /// <summary>
    /// Phase 3 DuckDB Recording Constants
    /// Controls CVR compression behavior for recordings
    /// </summary>
    public static class RecordingConstants
    {
        /// <summary>
        /// Phase 3: Force CVR compression for all user recordings.
        /// MUST be true to ensure users ONLY see .cvr files.
        /// 
        /// Internal database files (.db) must NEVER be exposed to users.
        /// All recordings MUST be saved as .cvr archives.
        /// 
        /// PRODUCTION: true (MANDATORY - always compress)
        /// DEVELOPMENT: false ONLY for internal testing (never for release)
        /// </summary>
        public const bool FORCE_CVR_COMPRESSION = true;
        
        /// <summary>
        /// Phase 3: Allow developers to override CVR compression via config.
        /// When false, OutputFormat and AutoCompress settings are ignored.
        /// Should be true ONLY in development builds.
        /// 
        /// DEBUG builds: true (respect config settings)
        /// RELEASE builds: false (force compression)
        /// </summary>
#if DEBUG
        public const bool ALLOW_COMPRESSION_OVERRIDE = true;
#else
        public const bool ALLOW_COMPRESSION_OVERRIDE = false;
#endif
        
        /// <summary>
        /// Batch size for packet inserts during recording.
        /// Larger batches = better performance but higher memory usage.
        /// </summary>
        public const int RECORDING_BATCH_SIZE = 100;
        
        /// <summary>
        /// Flush interval in milliseconds for database writes.
        /// Smaller intervals = more frequent writes = better data safety.
        /// </summary>
        public const int RECORDING_FLUSH_INTERVAL_MS = 2000;
    }
}
