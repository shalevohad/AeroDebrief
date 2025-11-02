using NLog;
using System;
using System.Threading.Tasks;
using AeroDebrief.Core.Audio;

namespace AeroDebrief.Core.Playback
{
    /// <summary>
    /// Manages scrubbing detection and audio muting during rapid timeline navigation.
    /// Prevents audio clicks and pops during Tacview timeline scrubbing.
    /// Supports audio output during slow scrubbing (like x3 speed) for preview.
    /// </summary>
    public sealed class ScrubbingManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private bool _scrubbingMode = false;
        private DateTime _lastSeekTime = DateTime.MinValue;
        private DateTime _scrubbingTimeout = DateTime.MinValue;
        private float _savedVolume = 1.0f;
        private double _currentScrubSpeed = 1.0;

        /// <summary>
        /// Scrubbing detection threshold in milliseconds.
        /// Seeks faster than this are considered scrubbing.
        /// </summary>
        public int ScrubbingThresholdMs { get; set; } = 100;

        /// <summary>
        /// How long to wait after last seek before exiting scrubbing mode.
        /// </summary>
        public int ScrubbingTimeoutMs { get; set; } = 500;

        /// <summary>
        /// Duration of fade-in when exiting scrubbing mode.
        /// </summary>
        public int FadeInDurationMs { get; set; } = 200;

        /// <summary>
        /// Maximum scrubbing speed at which audio is still played (default: 3.0x).
        /// Above this speed, audio is muted. Below this speed, audio plays time-stretched.
        /// </summary>
        public double MaxAudioScrubbingSpeed { get; set; } = 3.0;

        /// <summary>
        /// Whether to enable audio output during slow scrubbing.
        /// </summary>
        public bool EnableAudioDuringSlowScrubbing { get; set; } = true;

        /// <summary>
        /// Gets whether scrubbing mode is currently active.
        /// </summary>
        public bool IsScrubbingActive => _scrubbingMode && DateTime.UtcNow < _scrubbingTimeout;

        /// <summary>
        /// Gets whether audio should be played during current scrubbing speed.
        /// </summary>
        public bool ShouldPlayAudioDuringScrubbing => 
            EnableAudioDuringSlowScrubbing && 
            _scrubbingMode && 
            _currentScrubSpeed <= MaxAudioScrubbingSpeed;

        /// <summary>
        /// Gets the current scrubbing speed multiplier.
        /// </summary>
        public double CurrentScrubSpeed => _currentScrubSpeed;

        /// <summary>
        /// Event fired when scrubbing mode is activated.
        /// </summary>
        public event EventHandler? ScrubbingStarted;

        /// <summary>
        /// Event fired when scrubbing mode ends and audio is fading back in.
        /// </summary>
        public event EventHandler? ScrubbingEnded;

        /// <summary>
        /// Event fired when scrubbing speed changes (for UI updates).
        /// </summary>
        public event EventHandler<double>? ScrubSpeedChanged;

        /// <summary>
        /// Processes a seek operation and determines if scrubbing mode should activate.
        /// Calculates scrubbing speed based on drift and time between seeks.
        /// </summary>
        /// <param name="driftMs">Time drift in milliseconds that triggered the seek</param>
        /// <param name="audioEngine">Audio engine to mute/unmute</param>
        /// <returns>True if scrubbing mode is active</returns>
        public bool ProcessSeek(double driftMs, IAudioOutputEngine audioEngine)
        {
            var timeSinceLastSeek = (DateTime.UtcNow - _lastSeekTime).TotalMilliseconds;
            
            // Calculate scrubbing speed (how fast we're moving through time)
            if (timeSinceLastSeek > 0 && timeSinceLastSeek < ScrubbingThresholdMs)
            {
                // Speed = drift / time_elapsed
                // Example: 300ms drift in 100ms real time = 3.0x speed
                _currentScrubSpeed = driftMs / timeSinceLastSeek;
            }
            else
            {
                _currentScrubSpeed = 1.0;
            }
            
            // Detect scrubbing: rapid seeks (< threshold) with significant drift
            if (driftMs > 100 && timeSinceLastSeek < ScrubbingThresholdMs && timeSinceLastSeek > 0)
            {
                if (!_scrubbingMode)
                {
                    // Entering scrubbing mode
                    _scrubbingMode = true;
                    _savedVolume = 1.0f; // TODO: Get actual volume from audio engine
                    
                    Logger.Info($"Scrubbing mode activated (drift: {driftMs:F0}ms, time since last: {timeSinceLastSeek:F0}ms, speed: {_currentScrubSpeed:F1}x)");
                    
                    try
                    {
                        ScrubbingStarted?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error invoking ScrubbingStarted event");
                    }
                }
                
                // Update audio based on scrubbing speed
                if (audioEngine != null)
                {
                    if (ShouldPlayAudioDuringScrubbing)
                    {
                        // Slow scrubbing - keep audio on at reduced volume
                        var volumeFactor = CalculateScrubbingVolumeFactor(_currentScrubSpeed);
                        audioEngine.SetMasterVolume(_savedVolume * volumeFactor);
                        
                        Logger.Debug($"Slow scrubbing at {_currentScrubSpeed:F1}x - audio on at {volumeFactor:F2} volume");
                    }
                    else
                    {
                        // Fast scrubbing - mute audio
                        audioEngine.SetMasterVolume(0.0f);
                        
                        Logger.Debug($"Fast scrubbing at {_currentScrubSpeed:F1}x - audio muted");
                    }
                }
                
                // Notify speed change
                try
                {
                    ScrubSpeedChanged?.Invoke(this, _currentScrubSpeed);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error invoking ScrubSpeedChanged event");
                }
                
                // Extend timeout
                _scrubbingTimeout = DateTime.UtcNow.AddMilliseconds(ScrubbingTimeoutMs);
            }
            
            _lastSeekTime = DateTime.UtcNow;
            return _scrubbingMode;
        }

        /// <summary>
        /// Calculates volume factor based on scrubbing speed.
        /// Gradually reduces volume as speed increases to prevent audio overload.
        /// </summary>
        /// <param name="speed">Current scrubbing speed</param>
        /// <returns>Volume factor (0.0 to 1.0)</returns>
        private float CalculateScrubbingVolumeFactor(double speed)
        {
            if (speed <= 1.0)
                return 1.0f; // Normal speed = full volume
            
            if (speed >= MaxAudioScrubbingSpeed)
                return 0.0f; // Max speed = muted
            
            // Linear interpolation between 1.0x and max speed
            // At 2.0x: 66% volume
            // At 2.5x: 33% volume
            // At 3.0x: 0% volume
            var factor = 1.0 - ((speed - 1.0) / (MaxAudioScrubbingSpeed - 1.0));
            return (float)Math.Max(0.0, Math.Min(1.0, factor));
        }

        /// <summary>
        /// Checks if scrubbing has ended and handles fade-in if necessary.
        /// Should be called periodically (e.g., in update loop).
        /// </summary>
        /// <param name="audioEngine">Audio engine to fade in</param>
        public async Task CheckScrubbingTimeout(IAudioOutputEngine audioEngine)
        {
            if (_scrubbingMode && DateTime.UtcNow > _scrubbingTimeout)
            {
                // Scrubbing ended - fade audio back in
                _scrubbingMode = false;
                _currentScrubSpeed = 1.0;
                
                Logger.Info("Scrubbing mode ended, fading audio back in");
                
                try
                {
                    ScrubbingEnded?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error invoking ScrubbingEnded event");
                }
                
                if (audioEngine != null)
                {
                    await FadeInAudio(audioEngine, _savedVolume);
                }
            }
        }

        /// <summary>
        /// Manually exits scrubbing mode and fades audio back in.
        /// </summary>
        public async Task ExitScrubbingMode(IAudioOutputEngine audioEngine)
        {
            if (!_scrubbingMode)
                return;

            _scrubbingMode = false;
            _scrubbingTimeout = DateTime.MinValue;
            _currentScrubSpeed = 1.0;
            
            Logger.Info("Manually exiting scrubbing mode");
            
            try
            {
                ScrubbingEnded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error invoking ScrubbingEnded event");
            }
            
            if (audioEngine != null)
            {
                await FadeInAudio(audioEngine, _savedVolume);
            }
        }

        /// <summary>
        /// Fades audio volume back in over the configured duration.
        /// </summary>
        private async Task FadeInAudio(IAudioOutputEngine audioEngine, float targetVolume)
        {
            const int steps = 20;
            var stepDelay = FadeInDurationMs / steps;
            
            Logger.Debug($"Fading audio in over {FadeInDurationMs}ms to volume {targetVolume:F2}");
            
            for (int i = 0; i <= steps; i++)
            {
                var volume = (float)i / steps * targetVolume;
                audioEngine.SetMasterVolume(volume);
                await Task.Delay(stepDelay);
            }
            
            Logger.Debug("Audio fade-in complete");
        }

        /// <summary>
        /// Resets scrubbing state (e.g., when playback stops).
        /// </summary>
        public void Reset()
        {
            _scrubbingMode = false;
            _lastSeekTime = DateTime.MinValue;
            _scrubbingTimeout = DateTime.MinValue;
            _currentScrubSpeed = 1.0;
            
            Logger.Debug("Scrubbing manager reset");
        }
    }
}
