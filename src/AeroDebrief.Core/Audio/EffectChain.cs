using System.Buffers;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// High-performance lock-free effect chain inspired by SRS ClientEffectsPipeline.
    /// Uses SIMD vectorization, buffer pooling, and atomic snapshots for optimal performance.
    /// </summary>
    public sealed class EffectChain
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Shared;

        private volatile ImmutableList<IAudioEffect> _effects;
        private readonly object _updateLock = new();
        private long _snapshotsCreated;

        public int EffectCount => _effects?.Count ?? 0;
        public long SnapshotsCreated => Interlocked.Read(ref _snapshotsCreated);

        public EffectChain()
        {
            _effects = ImmutableList<IAudioEffect>.Empty;
        }

        public EffectChain(params IAudioEffect[] effects)
        {
            _effects = ImmutableList.Create(effects ?? Array.Empty<IAudioEffect>());
        }

        /// <summary>
        /// Optimized processing with SIMD mixing (SRS ClientEffectsPipeline pattern)
        /// Uses ping-pong buffers and vectorized operations for maximum performance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[] Process(float[] input)
        {
            if (input == null || input.Length == 0)
                return input;

            var effects = _effects; // Atomic snapshot
            if (effects == null || effects.Count == 0)
                return input;

            // Rent buffers for ping-pong processing
            float[]? rentedBuffer1 = null;
            float[]? rentedBuffer2 = null;

            try
            {
                rentedBuffer1 = FloatPool.Rent(input.Length);
                rentedBuffer2 = FloatPool.Rent(input.Length);

                var buffer1 = rentedBuffer1.AsSpan(0, input.Length);
                var buffer2 = rentedBuffer2.AsSpan(0, input.Length);

                // Copy input to first buffer
                input.AsSpan().CopyTo(buffer1);

                bool useBuffer1 = true;

                // Process through each enabled effect
                foreach (var effect in effects)
                {
                    if (effect.Enabled)
                    {
                        var inputBuffer = useBuffer1 ? buffer1 : buffer2;
                        var outputBuffer = useBuffer1 ? buffer2 : buffer1;

                        effect.ProcessInPlace(inputBuffer, outputBuffer);
                        useBuffer1 = !useBuffer1;
                    }
                }

                // Copy final result to output array
                var result = new float[input.Length];
                (useBuffer1 ? buffer1 : buffer2).CopyTo(result);
                return result;
            }
            finally
            {
                if (rentedBuffer1 != null) FloatPool.Return(rentedBuffer1);
                if (rentedBuffer2 != null) FloatPool.Return(rentedBuffer2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void UpdateEffects(Action<EffectChainBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            lock (_updateLock)
            {
                var builder = new EffectChainBuilder(_effects);
                configure(builder);
                
                _effects = builder.Build();
                Interlocked.Increment(ref _snapshotsCreated);

                Logger.Debug($"Effect chain updated: {_effects.Count} effects, snapshot #{SnapshotsCreated}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEffect(IAudioEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            UpdateEffects(builder => builder.Add(effect));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEffect(IAudioEffect effect)
        {
            if (effect == null)
                return;

            UpdateEffects(builder => builder.Remove(effect));
        }

        public void Clear()
        {
            lock (_updateLock)
            {
                _effects = ImmutableList<IAudioEffect>.Empty;
                Interlocked.Increment(ref _snapshotsCreated);
                Logger.Debug("Effect chain cleared");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<IAudioEffect> GetEffects()
        {
            return _effects;
        }
    }

    public class EffectChainBuilder
    {
        private readonly List<IAudioEffect> _effects;

        internal EffectChainBuilder(ImmutableList<IAudioEffect> currentEffects)
        {
            _effects = new List<IAudioEffect>(currentEffects ?? Enumerable.Empty<IAudioEffect>());
        }

        public EffectChainBuilder Add(IAudioEffect effect)
        {
            if (effect != null && !_effects.Contains(effect))
                _effects.Add(effect);
            return this;
        }

        public EffectChainBuilder Remove(IAudioEffect effect)
        {
            _effects.Remove(effect);
            return this;
        }

        public EffectChainBuilder Clear()
        {
            _effects.Clear();
            return this;
        }

        public EffectChainBuilder Insert(int index, IAudioEffect effect)
        {
            if (effect != null)
                _effects.Insert(index, effect);
            return this;
        }

        public EffectChainBuilder Replace(IAudioEffect oldEffect, IAudioEffect newEffect)
        {
            int index = _effects.IndexOf(oldEffect);
            if (index >= 0 && newEffect != null)
                _effects[index] = newEffect;
            return this;
        }

        internal ImmutableList<IAudioEffect> Build()
        {
            return ImmutableList.CreateRange(_effects);
        }
    }

    /// <summary>
    /// Interface for audio effects with span-based processing
    /// </summary>
    public interface IAudioEffect
    {
        string Name { get; }
        bool Enabled { get; set; }

        /// <summary>
        /// Legacy array-based processing (for compatibility)
        /// </summary>
        float[] Process(float[] input);

        /// <summary>
        /// Optimized span-based in-place processing
        /// </summary>
        void ProcessInPlace(ReadOnlySpan<float> input, Span<float> output);
    }

    /// <summary>
    /// Optimized gain effect with coefficient caching
    /// </summary>
    public class GainEffect : IAudioEffect
    {
        public string Name => "Gain";
        public bool Enabled { get; set; } = true;
        public float GainDb { get; set; }

        private float _linearGain;
        private bool _gainCacheDirty = true;

        public GainEffect(float gainDb = 0.0f)
        {
            GainDb = gainDb;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[] Process(float[] input)
        {
            if (!Enabled || Math.Abs(GainDb) < 0.01f)
                return input;

            var output = new float[input.Length];
            ProcessInPlace(input, output);
            return output;
        }

        /// <summary>
        /// SIMD-optimized gain processing (SRS pattern)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ProcessInPlace(ReadOnlySpan<float> input, Span<float> output)
        {
            if (!Enabled || Math.Abs(GainDb) < 0.01f)
            {
                input.CopyTo(output);
                return;
            }

            // Cache linear gain calculation
            if (_gainCacheDirty)
            {
                _linearGain = MathF.Pow(10.0f, GainDb / 20.0f);
                _gainCacheDirty = false;
            }

            // SIMD-friendly loop with vectorization
            var vectorSize = Vector<float>.Count;
            var remainder = input.Length % vectorSize;

            // Vectorized processing
            for (var i = 0; i < input.Length - remainder; i += vectorSize)
            {
                var v_input = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(input), (nuint)i);
                var v_gain = new Vector<float>(_linearGain);
                var v_result = v_input * v_gain;
                
                // Clamp to [-1, 1]
                var v_one = Vector<float>.One;
                var v_neg_one = -v_one;
                v_result = Vector.Max(Vector.Min(v_result, v_one), v_neg_one);
                
                v_result.CopyTo(output.Slice(i, vectorSize));
            }

            // Process remainder
            for (var i = input.Length - remainder; i < input.Length; ++i)
            {
                output[i] = Math.Clamp(input[i] * _linearGain, -1.0f, 1.0f);
            }
        }
    }

    /// <summary>
    /// Optimized high-pass filter with coefficient caching
    /// </summary>
    public class HighPassFilter : IAudioEffect
    {
        public string Name => "HighPass";
        public bool Enabled { get; set; } = true;
        public float CutoffHz { get; set; }

        private float _lastSample;
        private float _alpha;
        private bool _coeffDirty = true;

        public HighPassFilter(float cutoffHz = 300.0f)
        {
            CutoffHz = cutoffHz;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[] Process(float[] input)
        {
            if (!Enabled)
                return input;

            var output = new float[input.Length];
            ProcessInPlace(input, output);
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ProcessInPlace(ReadOnlySpan<float> input, Span<float> output)
        {
            if (!Enabled)
            {
                input.CopyTo(output);
                return;
            }

            // Cache filter coefficients
            if (_coeffDirty)
            {
                const float sampleRate = 48000.0f;
                float RC = 1.0f / (2.0f * MathF.PI * CutoffHz);
                float dt = 1.0f / sampleRate;
                _alpha = RC / (RC + dt);
                _coeffDirty = false;
            }

            output[0] = _alpha * (input[0] - _lastSample);

            for (int i = 1; i < input.Length; i++)
            {
                output[i] = _alpha * (output[i - 1] + input[i] - input[i - 1]);
            }

            _lastSample = input[^1];
        }
    }

    /// <summary>
    /// Optimized delay effect with circular buffer (SRS pattern)
    /// </summary>
    public class DelayEffect : IAudioEffect
    {
        public string Name => "Delay";
        public bool Enabled { get; set; } = true;
        public int DelayMs { get; set; }
        public float Feedback { get; set; }

        private readonly float[] _delayLine;
        private int _writePos;
        private readonly int _maxDelaySamples;

        public DelayEffect(int delayMs = 100, float feedback = 0.3f, int sampleRate = 48000)
        {
            DelayMs = delayMs;
            Feedback = Math.Clamp(feedback, 0.0f, 0.95f);
            _maxDelaySamples = sampleRate * delayMs / 1000;
            _delayLine = new float[_maxDelaySamples];
            _writePos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[] Process(float[] input)
        {
            if (!Enabled)
                return input;

            var output = new float[input.Length];
            ProcessInPlace(input, output);
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ProcessInPlace(ReadOnlySpan<float> input, Span<float> output)
        {
            if (!Enabled)
            {
                input.CopyTo(output);
                return;
            }

            int delaySamples = Constants.OUTPUT_SAMPLE_RATE * DelayMs / 1000;

            for (int i = 0; i < input.Length; i++)
            {
                int readPos = (_writePos - delaySamples + _maxDelaySamples) % _maxDelaySamples;
                float delayed = _delayLine[readPos];

                output[i] = input[i] + delayed * Feedback;
                _delayLine[_writePos] = output[i];

                _writePos = (_writePos + 1) % _maxDelaySamples;
            }
        }
    }
}
