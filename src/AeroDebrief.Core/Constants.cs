namespace AeroDebrief.Core{
    public static class Constants
    {
        /// <summary>
        /// The version of the SRS Recording Client.
        /// </summary>
        public const string VERSION = "0.9.0";

        /// <summary>
        /// The minimum required SRS server version for compatibility.
        /// </summary>
        public const string MINIMUM_SERVER_VERSION = "2.3.2.1";

        /// <summary>
        /// Output sample rate for audio processing (in Hz).
        /// </summary>
        public const int OUTPUT_SAMPLE_RATE = 48000;

        /// <summary>
        /// OPUS frame size in samples for 40ms at 48kHz (mono)
        /// SRS uses 40ms frames, not 20ms!
        /// </summary>
        public const int OPUS_FRAME_SIZE = 1920;

        /// <summary>
        /// OPUS frame duration in milliseconds
        /// SRS uses 40ms frames, not 20ms!
        /// </summary>
        public const int OPUS_FRAME_DURATION_MS = 40;

        /// <summary>
        /// Minimum buffer size in seconds to maintain during playback.
        /// </summary>
        public const int MINIMUM_BUFFER_SECONDS = 5;

        /// <summary>
        /// Default volume level (100%)
        /// </summary>
        public const float DEFAULT_VOLUME = 1.0f;
        
        /// <summary>
        /// Maximum volume level (200%)
        /// </summary>
        public const float MAX_VOLUME = 2.0f;

        /// <summary>
        /// Default folder for configuration files.
        /// </summary>
        public const string CONFIG_FOLDER = "configs";

        /// <summary>
        /// Magic identifier written at the start of AeroDebrief recording files.
        /// Used to identify file format and version for readers.
        /// </summary>
        public const string RECORDING_FILE_MAGIC = "AERO_REC_V1";
        
        #region Audio Mixing Constants
        
        /// <summary>
        /// Target RMS level in dB for automatic gain control (AGC) normalization.
        /// This is the target loudness level that all pilots' audio will be normalized to.
        /// Value: -20 dB (good speaking level, not too loud, matches SRS Client behavior)
        /// </summary>
        public const double AGC_TARGET_DB = -20.0;
        
        /// <summary>
        /// Maximum AGC boost in dB to prevent over-amplification of very quiet audio.
        /// Value: +20 dB (10x amplification)
        /// </summary>
        public const double AGC_MAX_BOOST_DB = 20.0;
        
        /// <summary>
        /// Maximum AGC cut in dB to prevent over-attenuation of very loud audio.
        /// Value: -10 dB (0.316x attenuation)
        /// </summary>
        public const double AGC_MAX_CUT_DB = -10.0;
        
        #endregion
        
        #region Packet Validation Constants
        
        /// <summary>
        /// Minimum valid timestamp for packet validation: 2000-01-01 00:00:00 UTC
        /// Packets with timestamps before this date are considered corrupted.
        /// </summary>
        public static readonly DateTime MinValidTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        /// <summary>
        /// Maximum valid timestamp for packet validation: 2100-01-01 00:00:00 UTC
        /// Packets with timestamps after this date are considered corrupted.
        /// </summary>
        public static readonly DateTime MaxValidTimestamp = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        /// <summary>
        /// Minimum valid radio frequency: 1 MHz = 1,000,000 Hz
        /// Below typical radio communication frequencies.
        /// </summary>
        public const double MinValidFrequencyHz = 1_000_000.0;  // 1 MHz in Hz
        
        /// <summary>
        /// Maximum valid radio frequency: 2000 MHz = 2,000,000,000 Hz (2 GHz)
        /// Above typical radio communication frequencies.
        /// </summary>
        public const double MaxValidFrequencyHz = 2_000_000_000.0;  // 2000 MHz in Hz
        
        /// <summary>
        /// Maximum audio payload size per packet: 10 MB (10,485,760 bytes)
        /// Prevents memory exhaustion from corrupted packet headers.
        /// </summary>
        public const int MaxAudioPayloadBytes = 10 * 1024 * 1024;
        
        /// <summary>
        /// Maximum string length for player names, GUIDs, aircraft types, etc.
        /// Prevents memory exhaustion from corrupted string length fields.
        /// </summary>
        public const int MaxStringLength = 1000;
        
        #endregion
        
        #region Playback Speed Constants
        
        /// <summary>
        /// Minimum supported playback speed (quarter speed).
        /// Below this speed, audio quality degrades significantly due to time-stretching artifacts.
        /// </summary>
        public const double MIN_PLAYBACK_SPEED = 0.25;
        
        /// <summary>
        /// Maximum supported playback speed (quad speed).
        /// Above this speed, audio becomes unintelligible and loses informational value.
        /// </summary>
        public const double MAX_PLAYBACK_SPEED = 4.0;
        
        /// <summary>
        /// Normal playback speed (real-time).
        /// </summary>
        public const double NORMAL_PLAYBACK_SPEED = 1.0;
        
        /// <summary>
        /// Playback speed threshold for fast playback optimizations.
        /// Above this speed, smaller audio buffers are used (2 seconds).
        /// </summary>
        public const double FAST_PLAYBACK_THRESHOLD = 1.5;
        
        /// <summary>
        /// Playback speed threshold for slow playback optimizations.
        /// Below this speed, larger audio buffers are used (10 seconds).
        /// </summary>
        public const double SLOW_PLAYBACK_THRESHOLD = 0.75;
        
        /// <summary>
        /// Audio buffer size in seconds for fast playback (> 1.5x speed).
        /// Smaller buffer for lower latency and faster response to user actions.
        /// </summary>
        public const int FAST_PLAYBACK_BUFFER_SECONDS = 2;
        
        /// <summary>
        /// Audio buffer size in seconds for normal playback (0.75x - 1.5x speed).
        /// Balanced buffer for general use.
        /// </summary>
        public const int NORMAL_PLAYBACK_BUFFER_SECONDS = 5;
        
        /// <summary>
        /// Audio buffer size in seconds for slow playback (< 0.75x speed).
        /// Larger buffer for stability during time-stretching operations.
        /// </summary>
        public const int SLOW_PLAYBACK_BUFFER_SECONDS = 10;
        
        #endregion
        
        #region GPU Waveform Rendering Constants
        
        /// <summary>
        /// Enable GPU-layered waveform rendering for instant frequency toggling.
        /// Set to false to use legacy CPU-based approach.
        /// </summary>
        public const bool USE_LAYERED_WAVEFORM_RENDERING = true;
        
        /// <summary>
        /// Enable adaptive resolution switching based on zoom level (Phase 3).
        /// Set to false to use fixed resolution.
        /// </summary>
        public const bool USE_ADAPTIVE_RESOLUTION = true;
        
        /// <summary>
        /// Enable advanced visual effects (glow, highlight, pulse) (Phase 3).
        /// Set to false to disable effects.
        /// </summary>
        public const bool USE_ADVANCED_EFFECTS = true;
        
        /// <summary>
        /// Maximum number of frequency layers that can be managed simultaneously.
        /// Prevents excessive GPU memory usage.
        /// </summary>
        public const int MAX_FREQUENCY_LAYERS = 50;
        
        /// <summary>
        /// Maximum number of layers supported by GPU compositor shader.
        /// Hardware limit based on shader resource array size.
        /// Reduced to 16 to prevent shader unroll failures.
        /// </summary>
        public const int MAX_COMPOSITOR_LAYERS = 16;
        
        /// <summary>
        /// GPU compositor compute shader thread group size.
        /// Must match [numthreads] in HLSL shader.
        /// </summary>
        public const int GPU_COMPOSITOR_TILE_SIZE = 16;
        
        /// <summary>
        /// Frequency matching tolerance in Hz for packet filtering.
        /// Packets within this range of target frequency are considered a match.
        /// </summary>
        public const double FREQUENCY_MATCH_TOLERANCE_HZ = 0.1;
        
        /// <summary>
        /// Default waveform resolution level (5 seconds per pixel for overview).
        /// Higher values = more detail but more memory usage.
        /// </summary>
        public const int DEFAULT_WAVEFORM_PIXELS = 2000;
        
        /// <summary>
        /// Enable detailed logging for GPU waveform operations (DEBUG builds only).
        /// </summary>
#if DEBUG
        public const bool LOG_GPU_WAVEFORM_DETAILS = true;
#else
        public const bool LOG_GPU_WAVEFORM_DETAILS = false;
#endif
        
        #endregion
        
        #region Tile-Based Data Loading Constants (Phase 8)
        
        /// <summary>
        /// Default memory budget for tile cache in megabytes.
        /// This is the maximum amount of RAM that can be used for caching amplitude tiles.
        /// Value: 500 MB (allows ~10-20 minutes of high-resolution data in memory)
        /// </summary>
        public const long TILE_CACHE_MEMORY_BUDGET_MB = 500;
        
        /// <summary>
        /// Default memory budget for tile cache in bytes.
        /// Calculated from TILE_CACHE_MEMORY_BUDGET_MB for direct comparisons.
        /// </summary>
        public const long TILE_CACHE_MEMORY_BUDGET_BYTES = TILE_CACHE_MEMORY_BUDGET_MB * 1024 * 1024;
        
        /// <summary>
        /// Default tile size in minutes.
        /// Each tile represents this duration of amplitude data.
        /// Value: 5 minutes (balance between granularity and overhead)
        /// - Too small (1 min): Too many tiles, more overhead
        /// - Too large (15 min): Less granular eviction, larger memory chunks
        /// </summary>
        public const int TILE_SIZE_MINUTES = 5;
        
        /// <summary>
        /// Preload buffer multiplier for smooth scrolling.
        /// Tiles within ±(viewport width × multiplier) are kept loaded.
        /// Value: 1.0 (±1 viewport width)
        /// - Total loaded range: 3× viewport (1 visible + 1 before + 1 after)
        /// </summary>
        public const double TILE_PRELOAD_BUFFER_MULTIPLIER = 1.0;
        
        /// <summary>
        /// Target cache hit rate (percentage).
        /// If hit rate falls below this, consider increasing memory budget.
        /// Value: 0.70 (70% hit rate)
        /// </summary>
        public const double TILE_CACHE_TARGET_HIT_RATE = 0.70;
        
        /// <summary>
        /// Memory size per AmplitudePoint in bytes.
        /// Used for memory usage calculations.
        /// Structure: DateTime (8 bytes) + double (8 bytes) = 16 bytes
        /// </summary>
        public const int AMPLITUDE_POINT_SIZE_BYTES = 16;
        
        /// <summary>
        /// Zoom level threshold for Layer0 (10ms resolution).
        /// Zoom levels >= this value use highest detail.
        /// </summary>
        public const double ZOOM_THRESHOLD_LAYER0 = 10.0;
        
        /// <summary>
        /// Zoom level threshold for Layer1 (50ms resolution).
        /// Zoom levels >= this value and < Layer0 threshold use high detail.
        /// </summary>
        public const double ZOOM_THRESHOLD_LAYER1 = 5.0;
        
        /// <summary>
        /// Zoom level threshold for Layer2 (250ms resolution).
        /// Zoom levels >= this value and < Layer1 threshold use medium detail.
        /// </summary>
        public const double ZOOM_THRESHOLD_LAYER2 = 2.0;
        
        /// <summary>
        /// Zoom level below Layer2 threshold uses Layer3 (1s resolution).
        /// Provides overview with lowest memory footprint.
        /// </summary>
        // Layer3 is used for zoom < ZOOM_THRESHOLD_LAYER2
        
        #endregion
    }
}
