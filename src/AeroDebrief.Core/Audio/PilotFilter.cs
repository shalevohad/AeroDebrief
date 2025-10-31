using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Filter modes for pilot audio gating
    /// </summary>
    public enum PilotFilterMode
    {
        /// <summary>
        /// Allow audio through (default state)
        /// </summary>
        Allow,
        
        /// <summary>
        /// Block audio completely (silent)
        /// </summary>
        Block,
        
        /// <summary>
        /// Mute temporarily (can be unmuted)
        /// </summary>
        Mute,
        
        /// <summary>
        /// Solo this pilot (silences all non-soloed pilots)
        /// </summary>
        Solo
    }

    /// <summary>
    /// High-performance pilot audio filter with smooth fade-out to prevent clicks.
    /// Supports Solo/Mute/Allow/Block modes with 64-sample crossfade for seamless transitions.
    /// Thread-safe for filter updates at any rate (tested at 20Hz+).
    /// </summary>
    public sealed class PilotFilter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // Fade duration: 64 samples at 48kHz = 1.33ms (imperceptible but prevents clicks)
        private const int FadeSamples = 64;
        private const float FadeIncrement = 1.0f / FadeSamples;

        private readonly ConcurrentDictionary<string, PilotState> _pilotStates;
        private readonly object _soloLock = new();
        private int _soloCount;

        public PilotFilter()
        {
            _pilotStates = new ConcurrentDictionary<string, PilotState>();
            _soloCount = 0;
        }

        /// <summary>
        /// Sets the filter mode for a specific pilot
        /// </summary>
        public void SetPilotMode(string pilotId, PilotFilterMode mode)
        {
            if (string.IsNullOrEmpty(pilotId))
                throw new ArgumentNullException(nameof(pilotId));

            var state = _pilotStates.GetOrAdd(pilotId, _ => new PilotState { PilotId = pilotId });
            
            lock (state.Lock)
            {
                var oldMode = state.Mode;
                
                // Update solo count
                if (oldMode == PilotFilterMode.Solo && mode != PilotFilterMode.Solo)
                {
                    lock (_soloLock)
                    {
                        _soloCount = Math.Max(0, _soloCount - 1);
                    }
                }
                else if (oldMode != PilotFilterMode.Solo && mode == PilotFilterMode.Solo)
                {
                    lock (_soloLock)
                    {
                        _soloCount++;
                    }
                }

                state.Mode = mode;
                
                // If switching to a state that should produce silence, start fade-out
                if (ShouldFadeOut(mode, oldMode))
                {
                    state.FadeState = FadeState.FadingOut;
                    state.CurrentGain = state.LastAppliedGain; // Start from current gain
                    Logger.Trace($"PilotFilter: {pilotId} fading out from {state.CurrentGain:F3} (mode: {oldMode} ? {mode})");
                }
                // If switching to a state that should produce audio, start fade-in
                else if (ShouldFadeIn(mode, oldMode))
                {
                    state.FadeState = FadeState.FadingIn;
                    state.CurrentGain = state.LastAppliedGain; // Start from current gain
                    Logger.Trace($"PilotFilter: {pilotId} fading in from {state.CurrentGain:F3} (mode: {oldMode} ? {mode})");
                }
                
                Logger.Debug($"PilotFilter: {pilotId} mode changed: {oldMode} ? {mode}, SoloCount={_soloCount}");
            }
        }

        /// <summary>
        /// Gets the current filter mode for a pilot
        /// </summary>
        public PilotFilterMode GetPilotMode(string pilotId)
        {
            if (string.IsNullOrEmpty(pilotId))
                return PilotFilterMode.Allow;

            return _pilotStates.TryGetValue(pilotId, out var state) ? state.Mode : PilotFilterMode.Allow;
        }

        /// <summary>
        /// Processes audio through the pilot filter with smooth fade-in/fade-out.
        /// This method is highly optimized and can be called at any rate without causing clicks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[] Process(string pilotId, float[] input)
        {
            if (input == null || input.Length == 0)
                return input;

            if (string.IsNullOrEmpty(pilotId))
                return input; // Pass through if no pilot ID

            var state = _pilotStates.GetOrAdd(pilotId, _ => new PilotState { PilotId = pilotId });
            
            // Fast path: if in stable state and fully silent, return zeros
            if (state.FadeState == FadeState.Silent && state.CurrentGain == 0.0f)
            {
                return new float[input.Length]; // Silent output
            }

            // Fast path: if in stable state and fully audible, pass through
            if (state.FadeState == FadeState.FullVolume && state.CurrentGain == 1.0f && 
                ShouldBeAudible(state.Mode, _soloCount))
            {
                return input; // Pass through unchanged
            }

            // Slow path: need to apply fade or check state
            var output = new float[input.Length];
            ProcessInPlace(pilotId, input, output, state);
            return output;
        }

        /// <summary>
        /// In-place processing for zero-copy scenarios (SRS pattern)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ProcessInPlace(string pilotId, ReadOnlySpan<float> input, Span<float> output)
        {
            if (input.Length == 0)
                return;

            if (string.IsNullOrEmpty(pilotId))
            {
                input.CopyTo(output);
                return;
            }

            var state = _pilotStates.GetOrAdd(pilotId, _ => new PilotState { PilotId = pilotId });
            ProcessInPlace(pilotId, input, output, state);
        }

        /// <summary>
        /// Core processing logic with smooth fade transitions
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ProcessInPlace(string pilotId, ReadOnlySpan<float> input, Span<float> output, PilotState state)
        {
            bool shouldBeAudible = ShouldBeAudible(state.Mode, _soloCount);

            lock (state.Lock)
            {
                int sampleIndex = 0;

                while (sampleIndex < input.Length)
                {
                    // Determine target gain based on current state
                    float targetGain = shouldBeAudible ? 1.0f : 0.0f;

                    // Update fade state machine
                    if (state.FadeState == FadeState.FadingOut)
                    {
                        // Fade out to zero
                        state.CurrentGain -= FadeIncrement;
                        if (state.CurrentGain <= 0.0f)
                        {
                            state.CurrentGain = 0.0f;
                            state.FadeState = FadeState.Silent;
                            Logger.Trace($"PilotFilter: {pilotId} fade-out complete ? Silent");
                        }
                    }
                    else if (state.FadeState == FadeState.FadingIn)
                    {
                        // Fade in to full volume
                        state.CurrentGain += FadeIncrement;
                        if (state.CurrentGain >= 1.0f)
                        {
                            state.CurrentGain = 1.0f;
                            state.FadeState = FadeState.FullVolume;
                            Logger.Trace($"PilotFilter: {pilotId} fade-in complete ? FullVolume");
                        }
                    }
                    else if (state.FadeState == FadeState.Silent && shouldBeAudible)
                    {
                        // Start fading in from silence
                        state.FadeState = FadeState.FadingIn;
                        state.CurrentGain = 0.0f;
                        Logger.Trace($"PilotFilter: {pilotId} starting fade-in from Silent");
                    }
                    else if (state.FadeState == FadeState.FullVolume && !shouldBeAudible)
                    {
                        // Start fading out from full volume
                        state.FadeState = FadeState.FadingOut;
                        state.CurrentGain = 1.0f;
                        Logger.Trace($"PilotFilter: {pilotId} starting fade-out from FullVolume");
                    }

                    // Apply current gain to output sample
                    output[sampleIndex] = input[sampleIndex] * state.CurrentGain;
                    state.LastAppliedGain = state.CurrentGain;
                    sampleIndex++;
                }
            }
        }

        /// <summary>
        /// Determines if audio should be audible based on filter mode and solo state
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldBeAudible(PilotFilterMode mode, int soloCount)
        {
            // If any pilots are soloed, only solo pilots are audible
            if (soloCount > 0)
            {
                return mode == PilotFilterMode.Solo;
            }

            // Otherwise, follow the mode
            return mode switch
            {
                PilotFilterMode.Allow => true,
                PilotFilterMode.Block => false,
                PilotFilterMode.Mute => false,
                PilotFilterMode.Solo => true,
                _ => true
            };
        }

        /// <summary>
        /// Determines if we should fade out when transitioning between modes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldFadeOut(PilotFilterMode newMode, PilotFilterMode oldMode)
        {
            // Fade out if going from audible to silent
            bool wasAudible = ShouldBeAudible(oldMode, _soloCount);
            bool willBeAudible = ShouldBeAudible(newMode, _soloCount);
            return wasAudible && !willBeAudible;
        }

        /// <summary>
        /// Determines if we should fade in when transitioning between modes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldFadeIn(PilotFilterMode newMode, PilotFilterMode oldMode)
        {
            // Fade in if going from silent to audible
            bool wasAudible = ShouldBeAudible(oldMode, _soloCount);
            bool willBeAudible = ShouldBeAudible(newMode, _soloCount);
            return !wasAudible && willBeAudible;
        }

        /// <summary>
        /// Resets all pilot states (useful for testing or playback restart)
        /// </summary>
        public void ResetAll()
        {
            _pilotStates.Clear();
            lock (_soloLock)
            {
                _soloCount = 0;
            }
            Logger.Debug("PilotFilter: All states reset");
        }

        /// <summary>
        /// Gets statistics about current filter state
        /// </summary>
        public PilotFilterStats GetStats()
        {
            var stats = new PilotFilterStats
            {
                TotalPilots = _pilotStates.Count,
                SoloPilots = _pilotStates.Count(kvp => kvp.Value.Mode == PilotFilterMode.Solo),
                MutedPilots = _pilotStates.Count(kvp => kvp.Value.Mode == PilotFilterMode.Mute),
                BlockedPilots = _pilotStates.Count(kvp => kvp.Value.Mode == PilotFilterMode.Block),
                FadingPilots = _pilotStates.Count(kvp => 
                    kvp.Value.FadeState == FadeState.FadingIn || 
                    kvp.Value.FadeState == FadeState.FadingOut)
            };
            return stats;
        }
    }

    /// <summary>
    /// Internal state for a single pilot's filter
    /// </summary>
    internal sealed class PilotState
    {
        public string PilotId { get; set; } = string.Empty;
        public PilotFilterMode Mode { get; set; } = PilotFilterMode.Allow;
        public FadeState FadeState { get; set; } = FadeState.FullVolume;
        public float CurrentGain { get; set; } = 1.0f;
        public float LastAppliedGain { get; set; } = 1.0f;
        public readonly object Lock = new();
    }

    /// <summary>
    /// Fade state machine states
    /// </summary>
    public enum FadeState
    {
        /// <summary>
        /// Audio is at full volume (gain = 1.0)
        /// </summary>
        FullVolume,
        
        /// <summary>
        /// Audio is currently fading in (0.0 ? 1.0)
        /// </summary>
        FadingIn,
        
        /// <summary>
        /// Audio is currently fading out (1.0 ? 0.0)
        /// </summary>
        FadingOut,
        
        /// <summary>
        /// Audio is completely silent (gain = 0.0)
        /// </summary>
        Silent
    }

    /// <summary>
    /// Statistics about pilot filter state
    /// </summary>
    public readonly struct PilotFilterStats
    {
        public int TotalPilots { get; init; }
        public int SoloPilots { get; init; }
        public int MutedPilots { get; init; }
        public int BlockedPilots { get; init; }
        public int FadingPilots { get; init; }

        public override string ToString()
        {
            return $"Total={TotalPilots}, Solo={SoloPilots}, Muted={MutedPilots}, " +
                   $"Blocked={BlockedPilots}, Fading={FadingPilots}";
        }
    }
}
