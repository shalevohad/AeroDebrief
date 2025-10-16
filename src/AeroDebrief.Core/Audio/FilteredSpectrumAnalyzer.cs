using AeroDebrief.Core.Models;
using AeroDebrief.Core.Analysis;
using AeroDebrief.Core.Helpers;
using NLog;
using System.Numerics;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Enhanced spectrum analyzer that supports frequency filtering and real-time analysis
    /// </summary>
    public sealed class FilteredSpectrumAnalyzer : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new();
        private readonly Dictionary<double, SpectrumChannel> _channels = new();
        private readonly FrequencyAnalysisService _analysisService;
        
        private Complex[]? _fftBuffer;
        private float[]? _window;
        private float[]? _magnitudes;
        private double[]? _frequencies;
        
        private bool _disposed;
        private int _fftSize = 1024;
        private double _sampleRate = 48000;

        public event EventHandler<SpectrumAnalysisEventArgs>? SpectrumUpdated;

        /// <summary>
        /// Gets or sets the FFT size for spectrum analysis
        /// </summary>
        public int FftSize
        {
            get => _fftSize;
            set
            {
                if (value <= 0 || (value & (value - 1)) != 0) // Must be power of 2
                    throw new ArgumentException("FFT size must be a positive power of 2", nameof(value));
                
                _fftSize = value;
                InitializeBuffers();
            }
        }

        /// <summary>
        /// Gets or sets the sample rate for frequency calculation
        /// </summary>
        public double SampleRate
        {
            get => _sampleRate;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Sample rate must be positive", nameof(value));
                
                _sampleRate = value;
                InitializeBuffers();
            }
        }

        /// <summary>
        /// Gets the spectrum channels
        /// </summary>
        public IReadOnlyDictionary<double, SpectrumChannel> Channels
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<double, SpectrumChannel>(_channels);
                }
            }
        }

        public FilteredSpectrumAnalyzer(FrequencyAnalysisService analysisService)
        {
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
            InitializeBuffers();
            Logger.Debug("FilteredSpectrumAnalyzer initialized");
        }

        /// <summary>
        /// Analyzes audio data and updates spectrum for filtered frequencies
        /// </summary>
        public void AnalyzeAudio(byte[] audioData, HashSet<double> activeFrequencies)
        {
            if (_disposed || audioData == null || audioData.Length == 0 || _fftBuffer == null)
                return;

            try
            {
                // Convert audio data to float samples (assuming 16-bit PCM)
                var samples = ConvertToFloatSamples(audioData);
                if (samples.Length < _fftSize)
                    return;

                lock (_lockObject)
                {
                    // Perform FFT analysis
                    var spectrum = PerformFFTAnalysis(samples);
                    
                    // Update only channels for active frequencies
                    foreach (var frequency in activeFrequencies)
                    {
                        if (!_channels.ContainsKey(frequency))
                        {
                            _channels[frequency] = new SpectrumChannel
                            {
                                Frequency = frequency,
                                IsActive = true,
                                Magnitudes = new float[_fftSize / 2],
                                Frequencies = new double[_fftSize / 2]
                            };
                        }

                        var channel = _channels[frequency];
                        UpdateChannelSpectrum(channel, spectrum, frequency);
                    }

                    // Mark inactive channels
                    foreach (var channel in _channels.Values)
                    {
                        channel.IsActive = activeFrequencies.Contains(channel.Frequency);
                        if (!channel.IsActive)
                        {
                            // Gradually fade out inactive channels
                            for (int i = 0; i < channel.Magnitudes.Length; i++)
                            {
                                channel.Magnitudes[i] *= 0.9f; // Fade factor
                            }
                        }
                    }
                }

                // Notify listeners
                SpectrumUpdated?.Invoke(this, new SpectrumAnalysisEventArgs(Channels));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during spectrum analysis");
            }
        }

        /// <summary>
        /// Gets combined spectrum data for all selected frequencies
        /// </summary>
        public SpectrumData GetCombinedSpectrum()
        {
            lock (_lockObject)
            {
                if (!_channels.Any() || _frequencies == null || _magnitudes == null)
                {
                    return new SpectrumData
                    {
                        Magnitudes = new float[_fftSize / 2],
                        Frequencies = _frequencies ?? new double[_fftSize / 2],
                        Timestamp = DateTime.UtcNow,
                        SampleRate = _sampleRate
                    };
                }

                // Combine magnitudes from all active channels
                var combinedMagnitudes = new float[_fftSize / 2];
                var activeChannelCount = 0;

                foreach (var channel in _channels.Values.Where(c => c.IsActive))
                {
                    for (int i = 0; i < combinedMagnitudes.Length && i < channel.Magnitudes.Length; i++)
                    {
                        combinedMagnitudes[i] += channel.Magnitudes[i];
                    }
                    activeChannelCount++;
                }

                // Average the magnitudes
                if (activeChannelCount > 0)
                {
                    for (int i = 0; i < combinedMagnitudes.Length; i++)
                    {
                        combinedMagnitudes[i] /= activeChannelCount;
                    }
                }

                return new SpectrumData
                {
                    Magnitudes = combinedMagnitudes,
                    Frequencies = _frequencies,
                    Timestamp = DateTime.UtcNow,
                    SampleRate = _sampleRate
                };
            }
        }

        /// <summary>
        /// Gets spectrum data for a specific frequency channel
        /// </summary>
        public SpectrumData? GetChannelSpectrum(double frequency)
        {
            lock (_lockObject)
            {
                if (_channels.TryGetValue(frequency, out var channel) && _frequencies != null)
                {
                    return new SpectrumData
                    {
                        Magnitudes = (float[])channel.Magnitudes.Clone(),
                        Frequencies = _frequencies,
                        Timestamp = DateTime.UtcNow,
                        SampleRate = _sampleRate
                    };
                }
            }
            
            return null;
        }

        /// <summary>
        /// Clears all spectrum data
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _channels.Clear();
            }
            Logger.Debug("Spectrum data cleared");
        }

        private void InitializeBuffers()
        {
            lock (_lockObject)
            {
                _fftBuffer = new Complex[_fftSize];
                _window = CreateHannWindow(_fftSize);
                _magnitudes = new float[_fftSize / 2];
                
                // Calculate frequency bins
                _frequencies = new double[_fftSize / 2];
                for (int i = 0; i < _frequencies.Length; i++)
                {
                    _frequencies[i] = i * _sampleRate / _fftSize;
                }
            }
            
            Logger.Debug($"Initialized FFT buffers: size={_fftSize}, sample_rate={_sampleRate}");
        }

        private float[] CreateHannWindow(int size)
        {
            var window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1))));
            }
            return window;
        }

        private float[] ConvertToFloatSamples(byte[] audioData)
        {
            try
            {
                // Decode audio using AudioHelpers (handles both Opus and PCM)
                var pcmSamples = AudioHelpers.DecodeAudioToPcm(audioData);
                
                // Convert to float for FFT (normalize to -1 to 1)
                var samples = new float[pcmSamples.Length];
                for (int i = 0; i < pcmSamples.Length; i++)
                {
                    samples[i] = pcmSamples[i] / 32768.0f;
                }
                
                Logger.Debug($"Converted {audioData.Length} bytes to {samples.Length} float samples for FFT");
                return samples;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to convert audio data to float samples ({audioData.Length} bytes)");
                // Return empty array on error to prevent FFT from crashing
                return Array.Empty<float>();
            }
        }

        private float[] PerformFFTAnalysis(float[] samples)
        {
            if (_fftBuffer == null || _window == null || _magnitudes == null)
                return new float[_fftSize / 2];

            // Take a window of samples
            var windowStart = Math.Max(0, samples.Length - _fftSize);
            for (int i = 0; i < _fftSize; i++)
            {
                var sampleIndex = windowStart + i;
                var sample = sampleIndex < samples.Length ? samples[sampleIndex] : 0f;
                _fftBuffer[i] = new Complex(sample * _window[i], 0);
            }

            // Perform FFT
            FFT(_fftBuffer);

            // Calculate magnitudes
            for (int i = 0; i < _magnitudes.Length; i++)
            {
                _magnitudes[i] = (float)_fftBuffer[i].Magnitude;
            }

            return _magnitudes;
        }

        private void UpdateChannelSpectrum(SpectrumChannel channel, float[] spectrum, double targetFrequency)
        {
            if (_frequencies == null)
                return;

            // Find the frequency bin closest to the target frequency
            var targetBin = Array.BinarySearch(_frequencies, targetFrequency);
            if (targetBin < 0)
                targetBin = ~targetBin;

            // Copy spectrum data with emphasis on the target frequency region
            var emphasisRange = Math.Max(1, _fftSize / 20); // Emphasize around 5% of spectrum
            var startBin = Math.Max(0, targetBin - emphasisRange);
            var endBin = Math.Min(spectrum.Length, targetBin + emphasisRange);

            for (int i = 0; i < channel.Magnitudes.Length && i < spectrum.Length; i++)
            {
                var magnitude = spectrum[i];
                
                // Apply emphasis to frequencies near the target
                if (i >= startBin && i <= endBin)
                {
                    var distance = Math.Abs(i - targetBin);
                    var emphasis = 1.0f + (1.0f - (float)distance / emphasisRange) * 0.5f; // Up to 50% boost
                    magnitude *= emphasis;
                }
                
                // Smooth the update to avoid flickering
                channel.Magnitudes[i] = channel.Magnitudes[i] * 0.7f + magnitude * 0.3f;
            }

            channel.LastUpdate = DateTime.UtcNow;
        }

        // Simple in-place FFT implementation (Cooley-Tukey algorithm)
        private static void FFT(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return;

            // Bit-reverse permutation
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, (int)Math.Log2(n));
                if (i < j)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            // FFT computation
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2 * Math.PI / len;
                var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = buffer[i + j];
                        var v = buffer[i + j + len / 2] * w;
                        buffer[i + j] = u + v;
                        buffer[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
        }

        private static int BitReverse(int n, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (n & 1);
                n >>= 1;
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
            Logger.Debug("FilteredSpectrumAnalyzer disposed");
        }
    }

    /// <summary>
    /// Spectrum data for a specific frequency channel
    /// </summary>
    public class SpectrumChannel
    {
        public double Frequency { get; set; }
        public bool IsActive { get; set; }
        public float[] Magnitudes { get; set; } = Array.Empty<float>();
        public double[] Frequencies { get; set; } = Array.Empty<double>();
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Event args for spectrum analysis updates
    /// </summary>
    public class SpectrumAnalysisEventArgs : EventArgs
    {
        public IReadOnlyDictionary<double, SpectrumChannel> Channels { get; }

        public SpectrumAnalysisEventArgs(IReadOnlyDictionary<double, SpectrumChannel> channels)
        {
            Channels = channels;
        }
    }

    /// <summary>
    /// Extended spectrum data model with filtering support
    /// </summary>
    public class SpectrumData
    {
        public float[] Magnitudes { get; set; } = Array.Empty<float>();
        public double[] Frequencies { get; set; } = Array.Empty<double>();
        public DateTime Timestamp { get; set; }
        public double SampleRate { get; set; }
        public HashSet<double> ActiveFrequencies { get; set; } = new();
        public bool IsFiltered { get; set; }
    }
}