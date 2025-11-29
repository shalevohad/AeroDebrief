using AeroDebrief.Core.Interfaces.Audio;
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

        // 20ms frame size at 48kHz (matches UserWorker output)
        private const int FrameSizeMs = 20;
        private const int SamplesPerFrame = Constants.OUTPUT_SAMPLE_RATE * FrameSizeMs / 1000; // 960 samples

        // Fade parameters for smooth transitions
        private const int FadeSamples = 64; // 1.33ms at 48kHz
        private const float FadeIncrement = 1.0f / FadeSamples;

        private readonly ConcurrentDictionary<double, FrequencyWorker> _frequencyWorkers;
        private readonly ConcurrentDictionary<double, FrequencyGateState> _frequencyGates;
        private readonly ConcurrentDictionary<(double Frequency, string PilotId), PilotGateState> _pilotGates;
        
        // NEW: Track UserWorkers for per-pilot mixing
        private readonly ConcurrentDictionary<(double Frequency, string PilotId), UserWorker> _userWorkers;
        
        // NEW: Cache frequency groups for fast iteration (avoid LINQ GroupBy per frame)
        // Use arrays for O(1) access instead of ConcurrentBag enumeration
        private readonly ConcurrentDictionary<double, (double, string)[]> _frequencyGroupArrays;
        private readonly ConcurrentDictionary<double, ConcurrentBag<(double, string)>> _frequencyGroupBags;
        
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
        public int ActiveFrequencies => _userWorkers.Select(kvp => kvp.Key.Frequency).Distinct().Count();

        public MasterMixer(IAudioOutputEngine audioOutput)
        {
            _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
            _frequencyWorkers = new ConcurrentDictionary<double, FrequencyWorker>();
            _frequencyGates = new ConcurrentDictionary<double, FrequencyGateState>();
            _pilotGates = new ConcurrentDictionary<(double, string), PilotGateState>();
            _pilotSoloCounts = new ConcurrentDictionary<double, int>();
            _userWorkers = new ConcurrentDictionary<(double, string), UserWorker>(); // NEW
            _frequencyGroupBags = new ConcurrentDictionary<double, ConcurrentBag<(double, string)>>(); // Staging
            _frequencyGroupArrays = new ConcurrentDictionary<double, (double, string)[]>(); // Cache for fast iteration
            _cts = new CancellationTokenSource();
            _runTime = Stopwatch.StartNew();
            
            // Start mixing task
            _mixingTask = Task.Run(() => MixingLoopAsync(_cts.Token));
            
            // Log AGC configuration
            var settings = Settings.PlayerSettingsStore.Instance;
            var agcEnabled = settings.GetAGCEnabled();
            var agcTarget = settings.GetAGCTargetDB();
            var agcMaxBoost = settings.GetAGCMaxBoostDB();
            var agcMaxCut = settings.GetAGCMaxCutDB();
            
            Logger.Info($"MasterMixer initialized with per-pilot filtering support");
            Logger.Info($"AGC Configuration: Enabled={agcEnabled}, Target={agcTarget:F1} dB, MaxBoost={agcMaxBoost:F1} dB, MaxCut={agcMaxCut:F1} dB");
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
                // Update frequency groups cache (thread-safe)
                var bag = _frequencyGroupBags.GetOrAdd(frequency, _ => new ConcurrentBag<(double, string)>());
                bag.Add(key);
                
                // Rebuild array for fast iteration (O(1) access vs ConcurrentBag enumeration)
                _frequencyGroupArrays[frequency] = bag.ToArray();

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
            Logger.Info("Master mixing loop started with per-pilot and frequency filtering + AGC");

            float[]? rentedMasterBuffer = null;
            
            // Cache AGC settings to avoid repeated Settings calls per-frame
            var settings = Settings.PlayerSettingsStore.Instance;
            bool agcEnabled = settings.GetAGCEnabled();
            double agcTargetDb = agcEnabled ? settings.GetAGCTargetDB() : 0;
            double agcMaxBoostDb = agcEnabled ? settings.GetAGCMaxBoostDB() : 0;
            double agcMaxCutDb = agcEnabled ? settings.GetAGCMaxCutDB() : 0;
            
            // Fallback to constants if settings return invalid values
            if (agcEnabled)
            {
                if (double.IsNaN(agcTargetDb) || agcTargetDb == 0) agcTargetDb = Constants.AGC_TARGET_DB;
                if (double.IsNaN(agcMaxBoostDb) || agcMaxBoostDb == 0) agcMaxBoostDb = Constants.AGC_MAX_BOOST_DB;
                if (double.IsNaN(agcMaxCutDb) || agcMaxCutDb == 0) agcMaxCutDb = Constants.AGC_MAX_CUT_DB;
            }

            try
            {
                rentedMasterBuffer = FloatPool.Rent(SamplesPerFrame);

                var framePeriod = TimeSpan.FromMilliseconds(FrameSizeMs);
                var nextFrameTime = DateTime.UtcNow;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var frameStartTime = DateTime.UtcNow;
                    
                    // Create span INSIDE the loop, never crossing await
                    var masterBuffer = rentedMasterBuffer.AsSpan(0, SamplesPerFrame);
                    
                    // Clear master buffer
                    masterBuffer.Clear();

                    int audibleFrequencies = 0;
                    int drainedFrequencies = 0;
                    
                    var mixStartTime = DateTime.UtcNow;

                    // NEW ARCHITECTURE: Pull from UserWorkers for per-pilot mixing
                    if (_userWorkers.Count > 0)
                    {
                        // Use cached frequency arrays instead of enumeration (performance!)
                        foreach (var kvp in _frequencyGroupArrays)
                        {
                            var frequency = kvp.Key;
                            var pilotKeys = kvp.Value;
                            
                            if (!_frequencyGates.TryGetValue(frequency, out var freqGate))
                                continue;

                            bool freqShouldBeAudible = ShouldFrequencyBeAudible(freqGate.Mode, _frequencySoloCount);

                            if (!freqShouldBeAudible)
                            {
                                // Drain pilot audio silently (limit to prevent blocking)
                                foreach (var pilotKey in pilotKeys)
                                {
                                    if (_userWorkers.TryGetValue(pilotKey, out var worker))
                                    {
                                        // Limit draining to 10 blocks to prevent long stalls
                                        int drained = 0;
                                        while (drained < 10 && worker.OutputReader.TryRead(out var _)) 
                                        { 
                                            drained++;
                                        }
                                    }
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

                                // Collect audible pilots - pre-size list for efficiency
                                var audiblePilots = new List<(UserWorker worker, float[] audio, PilotGateState gate, float agcGain)>(8);
                                
                                foreach (var pilotKey in pilotKeys)
                                {
                                    if (!_userWorkers.TryGetValue(pilotKey, out var worker))
                                        continue;

                                    if (!_pilotGates.TryGetValue(pilotKey, out var pilotGate))
                                        continue;

                                    bool pilotShouldBeAudible = ShouldPilotBeAudible(pilotGate.Mode, pilotSoloCount);

                                    // Pull DecodedAudioBlock from UserWorker
                                    if (worker.OutputReader.TryRead(out var block))
                                    {
                                        if (pilotShouldBeAudible && block.AudioData != null && block.AudioData.Length > 0)
                                        {
                                            var blockSpan = block.AudioData.AsSpan(0, Math.Min(block.AudioData.Length, SamplesPerFrame));
                                            
                                            // Calculate AGC gain for this pilot (only if AGC enabled)
                                            float agcGain = agcEnabled 
                                                ? CalculateAGCGainFast(blockSpan, agcTargetDb, agcMaxBoostDb, agcMaxCutDb)
                                                : 1.0f;
                                            
                                            audiblePilots.Add((worker, block.AudioData, pilotGate, agcGain));
                                        }
                                        // Silent draining happens automatically by not mixing
                                    }
                                }

                                // Calculate anti-clipping gain based on number of pilots
                                float antiClippingGain = 1.0f;
                                int pilotCount = audiblePilots.Count;
                                
                                if (pilotCount > 1)
                                {
                                    if (agcEnabled)
                                    {
                                        // With AGC: account for average AGC gain
                                        float totalAgcGain = 0f;
                                        for (int i = 0; i < pilotCount; i++)
                                        {
                                            totalAgcGain += audiblePilots[i].agcGain;
                                        }
                                        
                                        float avgAgcGain = totalAgcGain / pilotCount;
                                        antiClippingGain = 1.0f / (MathF.Sqrt(pilotCount) * MathF.Max(1.0f, avgAgcGain * 0.7f));
                                    }
                                    else
                                    {
                                        // Without AGC: simple sqrt scaling
                                        antiClippingGain = 1.0f / MathF.Sqrt(pilotCount);
                                    }
                                }

                                // Mix each pilot's audio with AGC + anti-clipping gain
                                for (int i = 0; i < pilotCount; i++)
                                {
                                    var (worker, audioData, pilotGate, agcGain) = audiblePilots[i];
                                    var blockSpan = audioData.AsSpan(0, Math.Min(audioData.Length, SamplesPerFrame));
                                    
                                    // Combine AGC gain with anti-clipping gain
                                    float combinedGain = agcGain * antiClippingGain;
                                    
                                    MixPilotBlock(freqSpan, blockSpan, pilotGate, combinedGain);
                                    audiblePilotsOnFreq++;
                                }
                                
                                // Apply safety limiter only if multiple pilots (soft clip at ±0.95)
                                if (audiblePilotsOnFreq > 1)
                                {
                                    ApplySafetyLimiter(freqSpan);
                                }

                                // Apply frequency-level filtering and mix into master
                                // Count this frequency as processed even if no pilots had audio
                                // (prevents false underruns when channels are temporarily empty)
                                MixFrequencyFrame(masterBuffer, freqSpan, freqGate);
                                audibleFrequencies++;
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

                    // Apply master-level safety limiter if we have many frequencies
                    if (audibleFrequencies > 1)
                    {
                        // Apply anti-clipping gain based on number of frequencies
                        var masterAntiClipGain = 1.0f / MathF.Sqrt(audibleFrequencies);
                        
                        // Apply gain reduction to prevent summing from causing clipping
                        for (int i = 0; i < masterBuffer.Length; i++)
                        {
                            masterBuffer[i] *= masterAntiClipGain;
                        }
                        
                        // Then apply safety limiter as final protection
                        ApplySafetyLimiter(masterBuffer);
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

                    // Log stats periodically with performance metrics
                    if (FramesMixed % 10 == 0 && FramesMixed > 0)
                    {
                        var frameDuration = (DateTime.UtcNow - frameStartTime).TotalMilliseconds;
                        var mixDuration = (DateTime.UtcNow - mixStartTime).TotalMilliseconds;
                        var stats = GetStats();
                        Logger.Info($"Mixer: {stats}, FrameTime={frameDuration:F2}ms, MixTime={mixDuration:F2}ms");
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

        /// <summary>
        /// Applies a soft limiter to prevent clipping while preserving dynamics.
        /// Uses a gentle soft-clip curve at ±0.90 threshold.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ApplySafetyLimiter(Span<float> audio)
        {
            const float threshold = 0.90f;  // More aggressive threshold to ensure we stay under 0.99
            const float knee = 0.05f; // Soft knee for smooth transition
            
            for (int i = 0; i < audio.Length; i++)
            {
                float sample = audio[i];
                float absample = MathF.Abs(sample);
                
                if (absample > threshold)
                {
                    // Soft clip: gradually compress above threshold
                    float excess = absample - threshold;
                    float compressed = threshold + (excess / (1.0f + excess / knee));
                    audio[i] = MathF.CopySign(compressed, sample);
                }
            }
        }
        /// <summary>
        /// Calculates automatic gain control (AGC) gain for a pilot's audio block using RMS-based analysis (SRS-style).
        /// This normalizes quiet and loud pilots to similar perceived loudness levels.
        /// OPTIMIZED VERSION: Assumes AGC is enabled (checked by caller), uses cached settings
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private float CalculateAGCGainFast(ReadOnlySpan<float> audioBlock, double targetDb, double maxBoostDb, double maxCutDb)
        {
            if (audioBlock.Length == 0)
                return 1.0f;

            // Calculate RMS (Root Mean Square) power - measures average loudness
            double sum = 0;
            for (int i = 0; i < audioBlock.Length; i++)
            {
                double sample = audioBlock[i];
                sum += sample * sample;
            }

            double rms = Math.Sqrt(sum / audioBlock.Length);
            
            // Convert to dB (decibels) - logarithmic scale matching human perception
            double rmsDb;
            if (rms == 0 || double.IsNaN(rms))
            {
                rmsDb = -96.6; // Silence threshold (16-bit minimum)
            }
            else
            {
                rmsDb = 20 * Math.Log10(rms);
            }
            
            // Calculate gain needed to reach target
            double gainDb = targetDb - rmsDb;
            
            // Limit gain range to prevent extreme amplification or attenuation
            gainDb = Math.Clamp(gainDb, maxCutDb, maxBoostDb);
            
            // Convert dB back to linear gain
            float linearGain = (float)Math.Pow(10, gainDb / 20.0);
            
            // Clamp final gain to safe range
            return Math.Clamp(linearGain, 0.1f, 10.0f);
        }
        
        /// <summary>
        /// Calculates automatic gain control (AGC) gain for a pilot's audio block using RMS-based analysis (SRS-style).
        /// This normalizes quiet and loud pilots to similar perceived loudness levels.
        /// LEGACY VERSION: For backwards compatibility, checks settings each call
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private float CalculateAGCGain(ReadOnlySpan<float> audioBlock)
        {
            if (audioBlock.Length == 0)
                return 1.0f;

            // Check if AGC is enabled in settings
            var settings = Settings.PlayerSettingsStore.Instance;
            if (!settings.GetAGCEnabled())
            {
                return 1.0f; // AGC disabled, return unity gain
            }

            // Get AGC parameters from settings (with fallback to constants)
            var targetDb = settings.GetAGCTargetDB();
            var maxBoostDb = settings.GetAGCMaxBoostDB();
            var maxCutDb = settings.GetAGCMaxCutDB();
            
            // Fallback to constants if settings return invalid values
            if (double.IsNaN(targetDb) || targetDb == 0) targetDb = Constants.AGC_TARGET_DB;
            if (double.IsNaN(maxBoostDb) || maxBoostDb == 0) maxBoostDb = Constants.AGC_MAX_BOOST_DB;
            if (double.IsNaN(maxCutDb) || maxCutDb == 0) maxCutDb = Constants.AGC_MAX_CUT_DB;
            
            return CalculateAGCGainFast(audioBlock, targetDb, maxBoostDb, maxCutDb);
        }
        #endregion

        #region Mixing Helpers

        /// <summary>
        /// NEW: Mixes a pilot's DecodedAudioBlock into the frequency buffer with SIMD and pilot gating
        /// This enables instant per-pilot mute/solo with smooth crossfades
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void MixPilotBlock(Span<float> frequencyBuffer, ReadOnlySpan<float> pilotAudio, PilotGateState gateState, float perPilotGain = 1.0f)
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

                // Calculate combined gain (gate gain * per-pilot gain for anti-clipping)
                float combinedGain = gateState.CurrentGain * perPilotGain;

                // Fast path: full volume with per-pilot gain (use SIMD with scaling)
                if (gateState.FadeState == FadeState.FullVolume && gateState.CurrentGain == 1.0f)
                {
                    if (perPilotGain == 1.0f)
                    {
                        // No gain adjustment needed
                        MixSIMD(frequencyBuffer, pilotAudio);
                    }
                    else
                    {
                        // Apply per-pilot gain via SIMD
                        MixSIMDWithGain(frequencyBuffer, pilotAudio, perPilotGain);
                    }
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
                        combinedGain = gateState.CurrentGain * perPilotGain;
                    }
                    else if (gateState.FadeState == FadeState.FadingIn)
                    {
                        gateState.CurrentGain += FadeIncrement;
                        if (gateState.CurrentGain >= 1.0f)
                        {
                            gateState.CurrentGain = 1.0f;
                            gateState.FadeState = FadeState.FullVolume;
                        }
                        combinedGain = gateState.CurrentGain * perPilotGain;
                    }

                    frequencyBuffer[i] += pilotAudio[i] * combinedGain;
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

        /// <summary>
        /// SIMD-optimized mixing with gain multiplication (for per-pilot gain compensation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void MixSIMDWithGain(Span<float> target, ReadOnlySpan<float> source, float gain)
        {
            var vectorSize = Vector<float>.Count;
            var remainder = source.Length % vectorSize;
            var gainVector = new Vector<float>(gain);

            for (var i = 0; i < source.Length - remainder; i += vectorSize)
            {
                var v_source = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)i);
                var v_current = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(target), (nuint)i);
                (v_current + (v_source * gainVector)).CopyTo(target.Slice(i, vectorSize));
            }

            for (var i = source.Length - remainder; i < source.Length; ++i)
            {
                target[i] += source[i] * gain;
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
                SoloFrequencies = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Solo),
                MutedFrequencies = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Mute),
                BlockedFrequencies = _pilotGates.Count(kvp => kvp.Value.Mode == PilotGateMode.Block),
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
