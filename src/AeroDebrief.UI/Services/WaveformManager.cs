using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Analysis;
using NLog;

namespace AeroDebrief.UI.Services
{
    /// <summary>
    /// Service responsible for waveform generation and GPU layer management.
    /// Implements Separation of Concerns by handling ONLY waveform-related operations.
    /// </summary>
    public sealed class WaveformManager : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private IWaveformGenerator? _waveformGenerator;
        private readonly Dictionary<double, Guid> _layerIds = new();
        private bool _disposed;
        private bool _isLoadingWaveform;
        private double _waveformGenerationProgress;

        /// <summary>
        /// Gets whether a waveform is currently being generated.
        /// </summary>
        public bool IsLoadingWaveform => _isLoadingWaveform;

        /// <summary>
        /// Gets the current waveform generation progress (0-100).
        /// </summary>
        public double WaveformGenerationProgress => _waveformGenerationProgress;

        /// <summary>
        /// Gets whether GPU acceleration is available and active.
        /// </summary>
        public bool IsUsingGpu => _waveformGenerator is GpuWaveformGenerator gpuGen && gpuGen.IsUsingGpu;

        /// <summary>
        /// Gets whether GPU-layered rendering is active.
        /// </summary>
        public bool IsUsingLayeredRendering => _waveformGenerator is GpuWaveformGenerator gpuGen && gpuGen.IsUsingLayeredRendering;

        /// <summary>
        /// Gets the maximum data points for waveform generation.
        /// </summary>
        public int MaxDataPoints => _waveformGenerator?.MaxDataPoints ?? 2000;

        /// <summary>
        /// Raised when waveform generation progress changes.
        /// </summary>
        public event EventHandler<WaveformProgressChangedEventArgs>? ProgressChanged;

        /// <summary>
        /// Raised when waveform generation completes.
        /// </summary>
        public event EventHandler<WaveformGeneratedEventArgs>? WaveformGenerated;

        /// <summary>
        /// Raised when a GPU layer is added.
        /// </summary>
        public event EventHandler<LayerAddedEventArgs>? LayerAdded;

        /// <summary>
        /// Raised when a GPU layer is removed.
        /// </summary>
        public event EventHandler<LayerRemovedEventArgs>? LayerRemoved;

        public WaveformManager()
        {
            Logger.Debug("WaveformManager initialized");
        }

        /// <summary>
        /// Initializes the waveform generator (GPU or CPU fallback).
        /// </summary>
        public void Initialize(FrequencyAnalysisService analysisService)
        {
            if (_waveformGenerator != null)
            {
                Logger.Warn("WaveformManager already initialized");
                return;
            }

            try
            {
                Logger.Info("Initializing waveform generator...");
                
                var gpuGenerator = new GpuWaveformGenerator();
                if (gpuGenerator.IsUsingGpu)
                {
                    Logger.Info("?? GPU acceleration enabled for waveform generation");
                    _waveformGenerator = gpuGenerator;
                }
                else
                {
                    Logger.Info("?? GPU not available, falling back to CPU-based waveform generation");
                    gpuGenerator.Dispose();
                    _waveformGenerator = new FilteredWaveformGenerator(analysisService);
                }

                Logger.Info("? Waveform generator initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize waveform generator");
                throw;
            }
        }

        /// <summary>
        /// Generates a waveform from the packet source for the selected frequencies.
        /// </summary>
        public async Task<WaveformData> GenerateWaveformAsync(
            FilePacketSource packetSource,
            HashSet<double> selectedFrequencies,
            IProgress<double>? progress = null)
        {
            if (_waveformGenerator == null)
                throw new InvalidOperationException("WaveformManager not initialized. Call Initialize() first.");

            if (packetSource == null)
                throw new ArgumentNullException(nameof(packetSource));

            if (selectedFrequencies == null || !selectedFrequencies.Any())
            {
                Logger.Warn("No frequencies selected for waveform generation");
                return new WaveformData
                {
                    CombinedWaveform = new float[MaxDataPoints],
                    //Channels = new Dictionary<double, WaveformData.ChannelData>()
                };
            }

            try
            {
                _isLoadingWaveform = true;
                _waveformGenerationProgress = 0.0;

                Logger.Info($"Generating waveform for {selectedFrequencies.Count} frequencies...");

                var progressReporter = new Progress<double>(percent =>
                {
                    _waveformGenerationProgress = percent;
                    ProgressChanged?.Invoke(this, new WaveformProgressChangedEventArgs(percent));
                });

                var combinedProgress = progress != null
                    ? new Progress<double>(p =>
                    {
                        ((IProgress<double>)progressReporter).Report(p);
                        progress.Report(p);
                    })
                    : progressReporter;

                var waveformData = await _waveformGenerator.GenerateWaveformFromSourceAsync(
                    packetSource,
                    TimeSpan.Zero,
                    packetSource.TotalDuration,
                    selectedFrequencies,
                    combinedProgress);

                _waveformGenerationProgress = 100.0;
                Logger.Info($"? Waveform generated: {selectedFrequencies.Count} frequencies");

                WaveformGenerated?.Invoke(this, new WaveformGeneratedEventArgs(waveformData, selectedFrequencies.Count));

                return waveformData;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Waveform generation failed");
                throw;
            }
            finally
            {
                _isLoadingWaveform = false;
            }
        }

        /// <summary>
        /// Adds a GPU layer for a specific frequency.
        /// </summary>
        public async Task<Guid> AddLayerAsync(
            double frequency,
            string displayName,
            System.Windows.Media.Color color,
            FilePacketSource packetSource,
            IProgress<double>? progress = null)
        {
            if (!IsUsingLayeredRendering)
                throw new InvalidOperationException("GPU-layered rendering is not available");

            if (_waveformGenerator is not GpuWaveformGenerator gpuGen)
                throw new InvalidOperationException("GPU waveform generator is not active");

            try
            {
                Logger.Debug($"Adding GPU layer for {displayName} ({frequency:F1} Hz)");

                // Convert WPF color to ARGB uint
                uint argbColor = ((uint)color.A << 24) |
                               ((uint)color.R << 16) |
                               ((uint)color.G << 8) |
                               (uint)color.B;

                var layerId = await gpuGen.AddLayerAsync(
                    frequency,
                    displayName,
                    argbColor,
                    packetSource,
                    progress);

                _layerIds[frequency] = layerId;

                Logger.Info($"? GPU layer created: {displayName} (LayerId: {layerId})");

                LayerAdded?.Invoke(this, new LayerAddedEventArgs(frequency, layerId, displayName));

                return layerId;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to add GPU layer for {displayName}");
                throw;
            }
        }

        /// <summary>
        /// Removes a GPU layer for a specific frequency.
        /// </summary>
        public void RemoveLayer(double frequency)
        {
            if (!IsUsingLayeredRendering)
                return;

            if (_waveformGenerator is not GpuWaveformGenerator gpuGen)
                return;

            if (_layerIds.TryGetValue(frequency, out var layerId))
            {
                try
                {
                    Logger.Debug($"Removing GPU layer for {frequency:F1} Hz (LayerId: {layerId})");

                    gpuGen.RemoveLayer(layerId);
                    _layerIds.Remove(frequency);

                    Logger.Info($"? GPU layer removed: {frequency:F1} Hz");

                    LayerRemoved?.Invoke(this, new LayerRemovedEventArgs(frequency, layerId));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to remove GPU layer for {frequency:F1} Hz");
                }
            }
        }

        /// <summary>
        /// Sets the visibility of a GPU layer.
        /// </summary>
        public void SetLayerVisible(double frequency, bool visible)
        {
            if (!IsUsingLayeredRendering)
                return;

            if (_waveformGenerator is not GpuWaveformGenerator gpuGen)
                return;

            if (_layerIds.TryGetValue(frequency, out var layerId))
            {
                try
                {
                    gpuGen.SetLayerVisible(layerId, visible);
                    Logger.Debug($"Layer {layerId} visibility set to: {visible}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to set layer visibility for {frequency:F1} Hz");
                }
            }
        }

        /// <summary>
        /// Gets a specific channel waveform (CPU rendering fallback).
        /// </summary>
        public float[]? GetChannelWaveform(double frequency)
        {
            return _waveformGenerator?.GetChannelWaveform(frequency);
        }

        /// <summary>
        /// Composes GPU layers into a single texture.
        /// </summary>
        public async Task<object?> ComposeLayersAsync(
            int width,
            int height,
            double zoomStart,
            double zoomEnd)
        {
            if (!IsUsingLayeredRendering)
                return null;

            if (_waveformGenerator is not GpuWaveformGenerator gpuGen)
                return null;

            try
            {
                Logger.Debug($"Composing {_layerIds.Count} GPU layers: {width}x{height}");

                var compositeTexture = await gpuGen.ComposeLayersAsync(
                    width,
                    height,
                    zoomStart,
                    zoomEnd);

                Logger.Debug($"? GPU composition complete");

                return compositeTexture;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GPU composition failed");
                return null;
            }
        }

        /// <summary>
        /// Clears all GPU layers.
        /// </summary>
        public void ClearLayers()
        {
            Logger.Debug($"Clearing {_layerIds.Count} GPU layers");

            foreach (var frequency in _layerIds.Keys.ToList())
            {
                RemoveLayer(frequency);
            }

            _layerIds.Clear();
        }

        /// <summary>
        /// Gets diagnostic information about the waveform engine.
        /// </summary>
        public WaveformEngineInfo GetEngineInfo()
        {
            return new WaveformEngineInfo
            {
                IsUsingGpu = IsUsingGpu,
                IsUsingLayeredRendering = IsUsingLayeredRendering,
                EngineType = _waveformGenerator?.GetType().Name ?? "Not Initialized",
                LayerCount = _layerIds.Count,
                MaxDataPoints = MaxDataPoints
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Logger.Debug("WaveformManager disposing");

            ClearLayers();
            _waveformGenerator?.Dispose();
            _waveformGenerator = null;

            _disposed = true;
        }
    }

    #region Event Args

    public class WaveformProgressChangedEventArgs : EventArgs
    {
        public double Progress { get; }

        public WaveformProgressChangedEventArgs(double progress)
        {
            Progress = progress;
        }
    }

    public class WaveformGeneratedEventArgs : EventArgs
    {
        public WaveformData WaveformData { get; }
        public int FrequencyCount { get; }

        public WaveformGeneratedEventArgs(WaveformData waveformData, int frequencyCount)
        {
            WaveformData = waveformData;
            FrequencyCount = frequencyCount;
        }
    }

    public class LayerAddedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public Guid LayerId { get; }
        public string DisplayName { get; }

        public LayerAddedEventArgs(double frequency, Guid layerId, string displayName)
        {
            Frequency = frequency;
            LayerId = layerId;
            DisplayName = displayName;
        }
    }

    public class LayerRemovedEventArgs : EventArgs
    {
        public double Frequency { get; }
        public Guid LayerId { get; }

        public LayerRemovedEventArgs(double frequency, Guid layerId)
        {
            Frequency = frequency;
            LayerId = layerId;
        }
    }

    public class WaveformEngineInfo
    {
        public bool IsUsingGpu { get; init; }
        public bool IsUsingLayeredRendering { get; init; }
        public string EngineType { get; init; } = string.Empty;
        public int LayerCount { get; init; }
        public int MaxDataPoints { get; init; }
    }

    #endregion
}
