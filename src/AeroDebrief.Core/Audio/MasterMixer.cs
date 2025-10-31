using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AeroDebrief.Core.IO;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Frequency-level gate controlling which frequencies contribute to the master mix.
    /// </summary>
    public enum FrequencyGateMode
    {
        Allow,    // Frequency audio passes through (default)
        Block,    // Frequency audio blocked completely
        Mute,     // Frequency temporarily muted
        Solo      // Solo this frequency (silences all non-soloed frequencies)
    }

    /// <summary>
    /// Pilot-level gate controlling which pilots contribute to the mix on their frequency.
    /// </summary>
    public enum PilotGateMode
    {
        Allow,    // Pilot audio passes through (default)
        Block,    // Pilot audio blocked completely
        Mute,     // Pilot temporarily muted
        Solo      // Solo this pilot (silences all non-soloed pilots on this frequency)
    }

    /// <summary>
    /// High-performance master mixer with frequency and pilot filtering at the orchestrator layer.
    /// 
    /// Architecture Benefits:
    /// - ALL audio pre-decoded and cached in FrequencyWorkers/UserWorkers
    /// - Filters applied at mix time (instant switching, no re-decoding)
    /// - Smooth crossfades prevent clicks/pops
    /// - SIMD vectorized mixing for performance
    /// - Zero-latency mute/solo/unmute
    /// 
    /// Filter Flow:
    /// 1. Pull DecodedAudioBlock from UserWorkers (pre-decoded)
    /// 2. Apply pilot-level filter (Mute/Solo/Block per pilot)
    /// 3. Mix pilots ? FrequencyAudioFrame
    /// 4. Apply frequency-level filter (Mute/Solo/Block per frequency)
    /// 5. Mix frequencies ? Master output
    /// 6. Submit to WASAPI
    /// </summary>
    public sealed class MasterMixer : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Shared;

        // 10ms frame size at 48kHz (matches FrequencyWorker output)
        private const int FrameSizeMs = 10;
        private const int SamplesPerFrame = Constants.OUTPUT_SAMPLE_RATE * FrameSizeMs / 1000; // 480 samples

        // Fade parameters for smooth transitions
        private const int FadeSamples = 64; // 1.33ms at 48kHz
        private const float FadeIncrement = 1.0f / FadeSamples;

        private readonly ConcurrentDictionary<double, FrequencyWorker> _frequencyWorkers;
        private readonly ConcurrentDictionary<double, FrequencyGateState> _frequencyGates;
        private readonly ConcurrentDictionary<(double Frequency, string PilotId), PilotGateState> _pilotGates;
        
        // NEW: Track UserWorkers for per-pilot mixing
        private readonly ConcurrentDictionary<(double Frequency, string PilotId), UserWorker> _userWorkers;
        
        private readonly IAudioOutputEngine _audioOutput;
        private readonly object _frequencySoloLock = new();
        private readonly object _pilotSoloLock = new();
        private int _frequencySoloCount;
        private readonly ConcurrentDictionary<double, int> _pilotSoloCounts; // Per-frequency solo counts
        
        private readonly CancellationTokenSource _cts;
        private readonly Task _mixingTask;
        private readonly Stopwatch _runTime;
        
        private long _framesMixed;
        private long _underruns;
        private long _silentFramesDrained;
        private bool _disposed;

        // Statistics
        public long FramesMixed => Interlocked.Read(ref _framesMixed);
        public long Underruns => Interlocked.Read(ref _underruns);
        public long SilentFramesDrained => Interlocked.Read(ref _silentFramesDrained);
        public TimeSpan RunTime => _runTime.Elapsed;
        public int ActiveFrequencies => _frequencyWorkers.Count;

        public MasterMixer(IAudioOutputEngine audioOutput)
        {
            _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
            _frequencyWorkers = new ConcurrentDictionary<double, FrequencyWorker>();
            _frequencyGates = new ConcurrentDictionary<double, FrequencyGateState>();
            _pilotGates = new ConcurrentDictionary<(double, string), PilotGateState>();
            _pilotSoloCounts = new ConcurrentDictionary<double, int>();
            _userWorkers = new ConcurrentDictionary<(double, string), UserWorker>(); // NEW
            _cts = new CancellationTokenSource();
            _runTime = Stopwatch.StartNew();
            
            // Start mixing task
            _mixingTask = Task.Run(() => MixingLoopAsync(_cts.Token));
            
            Logger.Info("MasterMixer initialized with per-pilot filtering support");
        }

        #region Frequency Management

        /// <summary>
        /// Registers a FrequencyWorker to be mixed into the master output
        /// NOTE: This is kept for backwards compatibility but per-pilot mixing uses RegisterUserWorker
        /// </summary>
        public bool RegisterFrequency(double frequency, FrequencyWorker worker)
        {
            if (worker == null)
                throw new ArgumentNullException(nameof(worker));

            if (_frequencyWorkers.TryAdd(frequency, worker))
            {
                // Initialize frequency gate state
                _frequencyGates.TryAdd(frequency, new FrequencyGateState
                {
                    Frequency = frequency,
                    Mode = FrequencyGateMode.Allow,
                    FadeState = FadeState.FullVolume,
                    CurrentGain = 1.0f
                });
                
                // Initialize pilot solo count for this frequency
                _pilotSoloCounts.TryAdd(frequency, 0);

                Logger.Info($"Registered frequency: {frequency / 1_000_000.0:F3} MHz");
                return true;
            }

            return false;
        }

        /// <summary>
        /// NEW: Registers a UserWorker for per-pilot audio mixing
        /// This enables instant pilot-level mute/solo/filtering
        /// </summary>
        public bool RegisterUserWorker(double frequency, string pilotId, UserWorker worker)
        {
            if (worker == null)
                throw new ArgumentNullException(nameof(worker));
            if (string.IsNullOrEmpty(pilotId))
                throw new ArgumentException("Pilot ID cannot be null or empty", nameof(pilotId));

            var key = (frequency, pilotId);
            if (_userWorkers.TryAdd(key, worker))
            {
                // Ensure frequency gate exists
                _frequencyGates.TryAdd(frequency, new FrequencyGateState
                {
                    Frequency = frequency,
                    Mode = FrequencyGateMode.Allow,
                    FadeState = FadeState.FullVolume,
                    CurrentGain = 1.0f
                });

                // Initialize pilot gate state
                _pilotGates.TryAdd(key, new PilotGateState
                {
                    PilotId = pilotId,
                    Frequency = frequency,
                    Mode = PilotGateMode.Allow,
                    FadeState = FadeState.FullVolume,
                    CurrentGain = 1.0f
                });

                // Initialize pilot solo count for this frequency
                _pilotSoloCounts.TryAdd(frequency, 0);

                Logger.Info($"Registered UserWorker: Pilot={pilotId}, Frequency={frequency / 1_000_000.0:F3} MHz");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Unregisters a FrequencyWorker from the mixer
        /// </summary>
        public bool UnregisterFrequency(double frequency)
        {
            if (_frequencyWorkers.TryRemove(frequency, out _))
            {
                _frequencyGates.TryRemove(frequency, out _);
                _pilotSoloCounts.TryRemove(frequency, out _);
                
                // Remove all pilot gates for this frequency
                var pilotsToRemove = _pilotGates.Keys.Where(k => k.Frequency == frequency).ToList();
                foreach (var key in pilotsToRemove)
                {
                    _pilotGates.TryRemove(key, out _);
                }
                
                Logger.Info($"Unregistered frequency: {frequency / 1_000_000.0:F3} MHz");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the gate mode for a specific frequency (instant switching, no re-decoding!)
        /// </summary>
        public void SetFrequencyGate(double frequency, FrequencyGateMode mode)
        {
            var state = _frequencyGates.GetOrAdd(frequency, _ => new FrequencyGateState
            {
                Frequency = frequency,
                Mode = FrequencyGateMode.Allow
            });

            lock (state.Lock)
            {
                var oldMode = state.Mode;

                // Update solo count
                if (oldMode == FrequencyGateMode.Solo && mode != FrequencyGateMode.Solo)
                {
                    lock (_frequencySoloLock)
                    {
                        _frequencySoloCount = Math.Max(0, _frequencySoloCount - 1);
                    }
                }
                else if (oldMode != FrequencyGateMode.Solo && mode == FrequencyGateMode.Solo)
                {
                    lock (_frequencySoloLock)
                    {
                        _frequencySoloCount++;
                    }
                }

                state.Mode = mode;

                // Start fade if needed
                if (ShouldFadeOut(mode, oldMode, _frequencySoloCount))
                {
                    state.FadeState = FadeState.FadingOut;
                    state.CurrentGain = state.LastAppliedGain;
                }
                else if (ShouldFadeIn(mode, oldMode, _frequencySoloCount))
                {
                    state.FadeState = FadeState.FadingIn;
                    state.CurrentGain = state.LastAppliedGain;
                }

                Logger.Info($"Frequency {frequency / 1_000_000.0:F3} MHz gate: {oldMode} ? {mode}, SoloCount={_frequencySoloCount}");
            }
        }

        #endregion

        #region Pilot Filtering (NEW!)

        /// <summary>
        /// Sets the gate mode for a specific pilot on a frequency (instant switching!)
        /// </summary>
        public void SetPilotGate(string pilotId, double frequency, PilotGateMode mode)
        {
            if (string.IsNullOrEmpty(pilotId))
                throw new ArgumentException("Pilot ID cannot be null or empty", nameof(pilotId));

            var key = (frequency, pilotId);
            var state = _pilotGates.GetOrAdd(key, _ => new PilotGateState
            {
                PilotId = pilotId,
                Frequency = frequency,
                Mode = PilotGateMode.Allow,
                FadeState = FadeState.FullVolume,
                CurrentGain = 1.0f
            });

            lock (state.Lock)
            {
                var oldMode = state.Mode;

                // Update per-frequency pilot solo count
                if (oldMode == PilotGateMode.Solo && mode != PilotGateMode.Solo)
                {
                    _pilotSoloCounts.AddOrUpdate(frequency, 0, (_, count) => Math.Max(0, count - 1));
                }
                else if (oldMode != PilotGateMode.Solo && mode == PilotGateMode.Solo)
                {
                    _pilotSoloCounts.AddOrUpdate(frequency, 1, (_, count) => count + 1);
                }

                state.Mode = mode;

                // Get current solo count for this frequency
                var soloCount = _pilotSoloCounts.GetOrAdd(frequency, 0);

                // Start fade if needed
                if (ShouldPilotFadeOut(mode, oldMode, soloCount))
                {
                    state.FadeState = FadeState.FadingOut;
                    state.CurrentGain = state.LastAppliedGain;
                }
                else if (ShouldPilotFadeIn(mode, oldMode, soloCount))
                {
                    state.FadeState = FadeState.FadingIn;
                    state.CurrentGain = state.LastAppliedGain;
                }

                Logger.Info($"Pilot {pilotId} on {frequency / 1_000_000.0:F3} MHz gate: {oldMode} ? {mode}, SoloCount={soloCount}");
            }
        }

        /// <summary>
        /// Clears pilot gate (returns to Allow mode)
        /// </summary>
        public void ClearPilotGate(string pilotId, double frequency)
        {
            SetPilotGate(pilotId, frequency, PilotGateMode.Allow);
        }

        /// <summary>
        /// Gets all pilot gates for a specific frequency
        /// </summary>
        public Dictionary<string, PilotGateMode> GetPilotGates(double frequency)
        {
            return _pilotGates
                .Where(kvp => kvp.Key.Frequency == frequency)
                .ToDictionary(kvp => kvp.Key.PilotId, kvp => kvp.Value.Mode);
        }

        #endregion

        #region Mixing Loop

        /// <summary>
        /// Main mixing loop: pulls frames from FrequencyWorkers OR UserWorkers, applies filters, mixes, and outputs
        /// NEW ARCHITECTURE: Pulls DecodedAudioBlocks from UserWorkers for per-pilot filtering
        /// </summary>
        private async Task MixingLoopAsync(CancellationToken cancellationToken)
        {
            Logger.Info("Master mixing loop started with per-pilot and frequency filtering");

            float[]? rentedMasterBuffer = null;

            try
            {
                rentedMasterBuffer = FloatPool.Rent(SamplesPerFrame);

                var framePeriod = TimeSpan.FromMilliseconds(FrameSizeMs);
                var nextFrameTime = DateTime.UtcNow;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Create span INSIDE the loop, never crossing await
                    var masterBuffer = rentedMasterBuffer.AsSpan(0, SamplesPerFrame);
                    
                    // Clear master buffer
                    masterBuffer.Clear();

                    int audibleFrequencies = 0;
                    int drainedFrequencies = 0;

                    // NEW ARCHITECTURE: Pull from UserWorkers for per-pilot mixing
                    if (_userWorkers.Count > 0)
                    {
                        // Group UserWorkers by frequency for per-frequency mixing
                        var frequencyGroups = _userWorkers.GroupBy(kvp => kvp.Key.Frequency);

                        foreach (var freqGroup in frequencyGroups)
                        {
                            var frequency = freqGroup.Key;
                            
                            if (!_frequencyGates.TryGetValue(frequency, out var freqGate))
                                continue;

                            bool freqShouldBeAudible = ShouldFrequencyBeAudible(freqGate.Mode, _frequencySoloCount);

                            if (!freqShouldBeAudible)
                            {
                                // Drain all pilot audio for this frequency silently
                                foreach (var kvp in freqGroup)
                                {
                                    var worker = kvp.Value;
                                    while (worker.OutputReader.TryRead(out var _)) { }
                                }
                                drainedFrequencies++;
                                continue;
                            }

                            // Mix all pilots on this frequency into a temporary buffer
                            float[]? tempFreqBuffer = null;
                            try
                            {
                                tempFreqBuffer = FloatPool.Rent(SamplesPerFrame);
                                var freqSpan = tempFreqBuffer.AsSpan(0, SamplesPerFrame);
                                freqSpan.Clear();

                                int audiblePilotsOnFreq = 0;
                                var pilotSoloCount = _pilotSoloCounts.GetOrAdd(frequency, 0);

                                // Pull and mix DecodedAudioBlocks from each pilot's UserWorker
                                foreach (var kvp in freqGroup)
                                {
                                    var pilotKey = kvp.Key;
                                    var worker = kvp.Value;

                                    if (!_pilotGates.TryGetValue(pilotKey, out var pilotGate))
                                        continue;

                                    bool pilotShouldBeAudible = ShouldPilotBeAudible(pilotGate.Mode, pilotSoloCount);

                                    // Pull DecodedAudioBlock from UserWorker
                                    if (worker.OutputReader.TryRead(out var block))
                                    {
                                        if (pilotShouldBeAudible && block.AudioData != null && block.AudioData.Length > 0)
                                        {
                                            // Apply pilot-level filtering and mix into frequency buffer
                                            var blockSpan = block.AudioData.AsSpan(0, Math.Min(block.AudioData.Length, SamplesPerFrame));
                                            MixPilotBlock(freqSpan, blockSpan, pilotGate);
                                            audiblePilotsOnFreq++;
                                        }
                                        // Silent draining happens automatically by not mixing
                                    }
                                }

                                // Apply frequency-level filtering and mix into master
                                if (audiblePilotsOnFreq > 0)
                                {
                                    MixFrequencyFrame(masterBuffer, freqSpan, freqGate);
                                    audibleFrequencies++;
                                }
                            }
                            finally
                            {
                                if (tempFreqBuffer != null)
                                    FloatPool.Return(tempFreqBuffer);
                            }
                        }
                    }
                    else
                    {
                        // LEGACY: Pull frames from each FrequencyWorker (old architecture)
                        foreach (var kvp in _frequencyWorkers)
                        {
                            var frequency = kvp.Key;
                            var worker = kvp.Value;

                            if (!_frequencyGates.TryGetValue(frequency, out var freqGate))
                                continue;

                            bool freqShouldBeAudible = ShouldFrequencyBeAudible(freqGate.Mode, _frequencySoloCount);

                            // Pull frame from FrequencyWorker.OutputReader
                            if (worker.OutputReader.TryRead(out var frame))
                            {
                                if (freqShouldBeAudible)
                                {
                                    // Apply frequency-level filtering and mix into master buffer
                                    var frameSpan = frame.AudioData.AsSpan();
                                    MixFrequencyFrame(masterBuffer, frameSpan, freqGate);
                                    audibleFrequencies++;
                                }
                                else
                                {
                                    // Silent draining: consume frame but don't mix
                                    Interlocked.Increment(ref _silentFramesDrained);
                                    drainedFrequencies++;
                                }
                            }
                        }
                    }

                    // Convert to bytes BEFORE await (no Span allowed across await)
                    byte[] audioBytes;
                    if (audibleFrequencies > 0 || drainedFrequencies > 0)
                    {
                        audioBytes = ConvertToBytes(masterBuffer);
                        Interlocked.Increment(ref _framesMixed);
                    }
                    else
                    {
                        // No frames available - write silence to prevent underrun
                        audioBytes = new byte[SamplesPerFrame * 2];
                        Interlocked.Increment(ref _underruns);
                    }

                    // NOW we can await (Span is no longer in scope)
                    await _audioOutput.WriteAudioAsync(audioBytes);

                    // Maintain timing
                    nextFrameTime = nextFrameTime.Add(framePeriod);
                    var delay = nextFrameTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    // Log stats periodically
                    if (FramesMixed % 6000 == 0 && FramesMixed > 0)
                    {
                        var stats = GetStats();
                        Logger.Info($"Mixer: {stats}");
                    }
                }

                Logger.Info($"Master mixing loop completed. Mixed {FramesMixed} frames over {RunTime.TotalMinutes:F1} minutes");
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Master mixing loop cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Master mixing loop failed");
            }
            finally
            {
                if (rentedMasterBuffer != null)
                    FloatPool.Return(rentedMasterBuffer);
            }
        }

        #endregion

        #region Mixing Helpers

        /// <summary>
        /// NEW: Mixes a pilot's DecodedAudioBlock into the frequency buffer with SIMD and pilot gating
        /// This enables instant per-pilot mute/solo with smooth crossfades
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void MixPilotBlock(Span<float> frequencyBuffer, ReadOnlySpan<float> pilotAudio, PilotGateState gateState)
        {
            if (pilotAudio.Length == 0)
                return;

            var soloCount = _pilotSoloCounts.GetOrAdd(gateState.Frequency, 0);
            bool shouldBeAudible = ShouldPilotBeAudible(gateState.Mode, soloCount);

            lock (gateState.Lock)
            {
                UpdatePilotFadeState(gateState, shouldBeAudible);

                // Fast path: completely silent
                if (gateState.FadeState == FadeState.Silent && gateState.CurrentGain == 0.0f)
                {
                    return;
                }

                // Fast path: full volume (use SIMD)
                if (gateState.FadeState == FadeState.FullVolume && gateState.CurrentGain == 1.0f)
                {
                    MixSIMD(frequencyBuffer, pilotAudio);
                    return;
                }

                // Slow path: apply per-sample gain during fade
                var length = Math.Min(frequencyBuffer.Length, pilotAudio.Length);
                for (int i = 0; i < length; i++)
                {
                    if (gateState.FadeState == FadeState.FadingOut)
                    {
                        gateState.CurrentGain -= FadeIncrement;
                        if (gateState.CurrentGain <= 0.0f)
                        {
                            gateState.CurrentGain = 0.0f;
                            gateState.FadeState = FadeState.Silent;
                        }
                    }
                    else if (gateState.FadeState == FadeState.FadingIn)
                    {
                        gateState.CurrentGain += FadeIncrement;
                        if (gateState.CurrentGain >= 1.0f)
                        {
                            gateState.CurrentGain = 1.0f;
                            gateState.FadeState = FadeState.FullVolume;
                        }
                    }

                    frequencyBuffer[i] += pilotAudio[i] * gateState.CurrentGain;
                    gateState.LastAppliedGain = gateState.CurrentGain;
                }
            }
        }

        /// <summary>
        /// Mixes a frequency frame into the master buffer with SIMD and gating
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void MixFrequencyFrame(Span<float> master, ReadOnlySpan<float> frequency, FrequencyGateState gateState)
        {
            if (frequency.Length == 0)
                return;

            bool shouldBeAudible = ShouldFrequencyBeAudible(gateState.Mode, _frequencySoloCount);

            lock (gateState.Lock)
            {
                UpdateFadeState(gateState, shouldBeAudible);

                // Fast path: completely silent
                if (gateState.FadeState == FadeState.Silent && gateState.CurrentGain == 0.0f)
                {
                    return;
                }

                // Fast path: full volume (use SIMD)
                if (gateState.FadeState == FadeState.FullVolume && gateState.CurrentGain == 1.0f)
                {
                    MixSIMD(master, frequency);
                    return;
                }

                // Slow path: apply per-sample gain during fade
                var length = Math.Min(master.Length, frequency.Length);
                for (int i = 0; i < length; i++)
                {
                    if (gateState.FadeState == FadeState.FadingOut)
                    {
                        gateState.CurrentGain -= FadeIncrement;
                        if (gateState.CurrentGain <= 0.0f)
                        {
                            gateState.CurrentGain = 0.0f;
                            gateState.FadeState = FadeState.Silent;
                        }
                    }
                    else if (gateState.FadeState == FadeState.FadingIn)
                    {
                        gateState.CurrentGain += FadeIncrement;
                        if (gateState.CurrentGain >= 1.0f)
                        {
                            gateState.CurrentGain = 1.0f;
                            gateState.FadeState = FadeState.FullVolume;
                        }
                    }

                    master[i] += frequency[i] * gateState.CurrentGain;
                    gateState.LastAppliedGain = gateState.CurrentGain;
                }
            }
        }

        /// <summary>
        /// SIMD-optimized mixing
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void MixSIMD(Span<float> target, ReadOnlySpan<float> source)
        {
            var vectorSize = Vector<float>.Count;
            var remainder = source.Length % vectorSize;

            for (var i = 0; i < source.Length - remainder; i += vectorSize)
            {
                var v_source = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)i);
                var v_current = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(target), (nuint)i);
                (v_current + v_source).CopyTo(target.Slice(i, vectorSize));
            }

            for (var i = source.Length - remainder; i < source.Length; ++i)
            {
                target[i] += source[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFadeState(FrequencyGateState state, bool shouldBeAudible)
        {
            if (state.FadeState == FadeState.Silent && shouldBeAudible)
            {
                state.FadeState = FadeState.FadingIn;
                state.CurrentGain = 0.0f;
            }
            else if (state.FadeState == FadeState.FullVolume && !shouldBeAudible)
            {
                state.FadeState = FadeState.FadingOut;
                state.CurrentGain = 1.0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePilotFadeState(PilotGateState state, bool shouldBeAudible)
        {
            if (state.FadeState == FadeState.Silent && shouldBeAudible)
            {
                state.FadeState = FadeState.FadingIn;
                state.CurrentGain = 0.0f;
            }
            else if (state.FadeState == FadeState.FullVolume && !shouldBeAudible)
            {
                state.FadeState = FadeState.FadingOut;
                state.CurrentGain = 1.0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldFrequencyBeAudible(FrequencyGateMode mode, int soloCount)
        {
            if (soloCount > 0)
                return mode == FrequencyGateMode.Solo;

            return mode == FrequencyGateMode.Allow || mode == FrequencyGateMode.Solo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldPilotBeAudible(PilotGateMode mode, int soloCount)
        {
            if (soloCount > 0)
                return mode == PilotGateMode.Solo;

            return mode == PilotGateMode.Allow || mode == PilotGateMode.Solo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldFadeOut(FrequencyGateMode newMode, FrequencyGateMode oldMode, int soloCount)
        {
            bool wasAudible = ShouldFrequencyBeAudible(oldMode, soloCount);
            bool willBeAudible = ShouldFrequencyBeAudible(newMode, soloCount);
            return wasAudible && !willBeAudible;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldFadeIn(FrequencyGateMode newMode, FrequencyGateMode oldMode, int soloCount)
        {
            bool wasAudible = ShouldFrequencyBeAudible(oldMode, soloCount);
            bool willBeAudible = ShouldFrequencyBeAudible(newMode, soloCount);
            return !wasAudible && willBeAudible;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldPilotFadeOut(PilotGateMode newMode, PilotGateMode oldMode, int soloCount)
        {
            bool wasAudible = ShouldPilotBeAudible(oldMode, soloCount);
            bool willBeAudible = ShouldPilotBeAudible(newMode, soloCount);
            return wasAudible && !willBeAudible;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldPilotFadeIn(PilotGateMode newMode, PilotGateMode oldMode, int soloCount)
        {
            bool wasAudible = ShouldPilotBeAudible(oldMode, soloCount);
            bool willBeAudible = ShouldPilotBeAudible(newMode, soloCount);
            return !wasAudible && willBeAudible;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private byte[] ConvertToBytes(ReadOnlySpan<float> samples)
        {
            var bytes = new byte[samples.Length * 2];

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = Math.Clamp(samples[i], -1.0f, 1.0f);
                short pcmSample = (short)(sample * 32767);
                bytes[i * 2] = (byte)(pcmSample & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            return bytes;
        }

        #endregion

        #region Statistics

        public MixerStats GetStats()
        {
            return new MixerStats
            {
                FramesMixed = FramesMixed,
                Underruns = Underruns,
                SilentFramesDrained = SilentFramesDrained,
                RunTime = RunTime,
                ActiveFrequencies = ActiveFrequencies,
                SoloFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Solo),
                MutedFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Mute),
                BlockedFrequencies = _frequencyGates.Count(kvp => kvp.Value.Mode == FrequencyGateMode.Block),
                SoloPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Solo),
                MutedPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Mute),
                BlockedPilots = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Block),
                AverageFrameRate = FramesMixed / Math.Max(1, RunTime.TotalSeconds)
            };
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _cts?.Cancel();

                if (_mixingTask != null && !_mixingTask.IsCompleted)
                {
                    _mixingTask.Wait(TimeSpan.FromSeconds(5));
                }

                _cts?.Dispose();
                _runTime?.Stop();

                var finalStats = GetStats();
                Logger.Info($"MasterMixer disposed. Stats: {finalStats}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error disposing MasterMixer");
            }
        }
    }

    #region State Classes

    internal sealed class FrequencyGateState
    {
        public double Frequency { get; set; }
        public FrequencyGateMode Mode { get; set; } = FrequencyGateMode.Allow;
        public FadeState FadeState { get; set; } = FadeState.FullVolume;
        public float CurrentGain { get; set; } = 1.0f;
        public float LastAppliedGain { get; set; } = 1.0f;
        public readonly object Lock = new();
    }

    internal sealed class PilotGateState
    {
        public string PilotId { get; set; } = string.Empty;
        public double Frequency { get; set; }
        public PilotGateMode Mode { get; set; } = PilotGateMode.Allow;
        public FadeState FadeState { get; set; } = FadeState.FullVolume;
        public float CurrentGain { get; set; } = 1.0f;
        public float LastAppliedGain { get; set; } = 1.0f;
        public readonly object Lock = new();
    }

    public readonly struct MixerStats
    {
        public long FramesMixed { get; init; }
        public long Underruns { get; init; }
        public long SilentFramesDrained { get; init; }
        public TimeSpan RunTime { get; init; }
        public int ActiveFrequencies { get; init; }
        public int SoloFrequencies { get; init; }
        public int MutedFrequencies { get; init; }
        public int BlockedFrequencies { get; init; }
        public int SoloPilots { get; init; }
        public int MutedPilots { get; init; }
        public int BlockedPilots { get; init; }
        public double AverageFrameRate { get; init; }

        public double UnderrunRate => FramesMixed > 0 ? (Underruns / (double)FramesMixed) * 100.0 : 0.0;

        public override string ToString()
        {
            return $"Frames={FramesMixed}, Underruns={Underruns} ({UnderrunRate:F2}%), " +
                   $"Drained={SilentFramesDrained}, Runtime={RunTime.TotalMinutes:F1}min, " +
                   $"Freqs={ActiveFrequencies} (Solo={SoloFrequencies}, Muted={MutedFrequencies}), " +
                   $"Pilots=(Solo={SoloPilots}, Muted={MutedPilots}), FPS={AverageFrameRate:F1}";
        }
    }

    #endregion
}
