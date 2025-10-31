using AeroDebrief.Core.Models;
using AeroDebrief.Core.Audio.Waveform;
using AeroDebrief.Core.IO;
using NLog;
using Vortice.Direct3D11;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// GPU-accelerated waveform generator using DirectX 11 compute shaders
    /// Phase 2: GPU-layered rendering with instant frequency toggling
    /// Phase 3: GPU compositor with advanced effects and adaptive resolution
    /// </summary>
    public class GpuWaveformGenerator : WaveformGeneratorBase
    {
        private bool _useGpu = false;
        private IGpuComputeContext? _gpuContext;
        
        // NEW: Layered rendering support
        private LayeredWaveformRenderer? _layeredRenderer;
        
        /// <summary>
        /// Gets whether GPU acceleration is currently active
        /// </summary>
        public bool IsUsingGpu => _useGpu;
        
        /// <summary>
        /// Gets whether layered rendering is active (instant frequency toggling)
        /// </summary>
        public bool IsUsingLayeredRendering => _layeredRenderer != null && Constants.USE_LAYERED_WAVEFORM_RENDERING;

        public GpuWaveformGenerator()
        {
            InitializeGpu();
            Logger.Debug($"GpuWaveformGenerator initialized (GPU: {_useGpu}, Layered: {IsUsingLayeredRendering})");
        }

        private void InitializeGpu()
        {
            try
            {
                Logger.Info("Attempting to initialize GPU compute for waveform generation...");
                
                _gpuContext = GpuComputeContextFactory.CreateContext();
                
                if (_gpuContext != null && _gpuContext.IsAvailable)
                {
                    _useGpu = true;
                    Logger.Info($"? GPU acceleration enabled: {_gpuContext.DeviceName}");
                    Logger.Info($"  - Compute capability: {_gpuContext.ComputeCapability}");
                    Logger.Info($"  - Max threads: {_gpuContext.MaxThreads}");
                    
                    // NEW: Initialize layered renderer if GPU is available and feature is enabled
                    if (Constants.USE_LAYERED_WAVEFORM_RENDERING)
                    {
                        try
                        {
                            var d3d11Context = _gpuContext as D3D11ComputeContext;
                            if (d3d11Context != null)
                            {
                                _layeredRenderer = new LayeredWaveformRenderer(
                                    d3d11Context.Device, 
                                    d3d11Context.Context);
                                Logger.Info("? Layered waveform renderer initialized (instant frequency toggling enabled)");
                                Logger.Info($"  - Max layers: {Constants.MAX_FREQUENCY_LAYERS}");
                                Logger.Info($"  - Frequency tolerance: {Constants.FREQUENCY_MATCH_TOLERANCE_HZ} Hz");
                            }
                            else
                            {
                                Logger.Warn("GPU context is not D3D11 - layered rendering disabled");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Failed to initialize layered renderer - will use standard approach");
                            _layeredRenderer = null;
                        }
                    }
                    else
                    {
                        Logger.Info($"Layered rendering disabled by configuration (USE_LAYERED_WAVEFORM_RENDERING = false)");
                    }
                }
                else
                {
                    Logger.Warn("GPU compute not available, falling back to CPU-based waveform generation");
                    _useGpu = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize GPU compute, falling back to CPU");
                _useGpu = false;
                _gpuContext?.Dispose();
                _gpuContext = null;
            }
        }

        // NEW: Layered rendering API
        
        /// <summary>
        /// Adds a frequency layer to the layered renderer
        /// </summary>
        public async Task<Guid> AddLayerAsync(
            double frequencyHz,
            string displayName,
            uint color,
            FilePacketSource packetSource,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_layeredRenderer == null)
                throw new InvalidOperationException("Layered rendering not available");
            
            return await _layeredRenderer.AddLayerAsync(
                frequencyHz, 
                displayName, 
                color, 
                packetSource, 
                progress,
                cancellationToken);
        }
        
        /// <summary>
        /// Sets layer visibility (instant - no regeneration!)
        /// </summary>
        public void SetLayerVisible(Guid layerId, bool visible)
        {
            if (_layeredRenderer == null)
                throw new InvalidOperationException("Layered rendering not available");
            
            _layeredRenderer.SetLayerVisible(layerId, visible);
        }
        
        /// <summary>
        /// Updates a specific layer (for pilot filtering)
        /// </summary>
        public async Task UpdateLayerAsync(
            Guid layerId,
            FilePacketSource packetSource,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_layeredRenderer == null)
                throw new InvalidOperationException("Layered rendering not available");
            
            await _layeredRenderer.UpdateLayerAsync(layerId, packetSource, progress, cancellationToken);
        }
        
        /// <summary>
        /// Removes a frequency layer
        /// </summary>
        public void RemoveLayer(Guid layerId)
        {
            if (_layeredRenderer == null)
                throw new InvalidOperationException("Layered rendering not available");
            
            _layeredRenderer.RemoveLayer(layerId);
        }
        
        /// <summary>
        /// Gets all layers for UI display
        /// </summary>
        public IReadOnlyList<WaveformLayer> GetAllLayers()
        {
            if (_layeredRenderer == null)
                return Array.Empty<WaveformLayer>();
            
            return _layeredRenderer.GetAllLayers();
        }

        /// <summary>
        /// Composes all visible frequency layers into a final output texture (Phase 3)
        /// </summary>
        public async Task<ID3D11Texture2D?> ComposeLayersAsync(
            int outputWidth,
            int outputHeight,
            double zoomStart = 0.0,
            double zoomEnd = 1.0,
            CancellationToken cancellationToken = default)
        {
            if (!Constants.USE_LAYERED_WAVEFORM_RENDERING || _layeredRenderer == null)
            {
                Logger.Debug("GPU-layered rendering disabled or unavailable");
                return null;
            }

            return await _layeredRenderer.ComposeLayersAsync(
                outputWidth,
                outputHeight,
                zoomStart,
                zoomEnd,
                cancellationToken);
        }

        /// <summary>
        /// Updates zoom range for GPU compositor (instant operation - Phase 3)
        /// </summary>
        public void UpdateZoomRange(double startTime, double endTime)
        {
            if (!Constants.USE_LAYERED_WAVEFORM_RENDERING || _layeredRenderer == null)
                return;

            _layeredRenderer.UpdateZoomRange(startTime, endTime);
        }

        /// <summary>
        /// Sets visual effect for a specific frequency layer (Phase 3)
        /// </summary>
        public void SetLayerEffect(Guid layerId, WaveformEffect effect, float intensity = 1.0f)
        {
            if (!Constants.USE_LAYERED_WAVEFORM_RENDERING || _layeredRenderer == null)
                return;

            if (!Constants.USE_ADVANCED_EFFECTS)
            {
                Logger.Debug("Advanced effects disabled via feature flag");
                return;
            }

            _layeredRenderer.SetLayerEffect(layerId, effect, intensity);
        }

        // OVERRIDE: Implement abstract method from base class
        protected override async Task GenerateChannelWaveformsAsync(
            WaveformData waveformData,
            List<AudioPacketMetadata> packets,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var (startTime, endTime, totalDuration) = GetTimeRange(packets);

            if (totalDuration.TotalMilliseconds <= 0)
            {
                Logger.Warn("Invalid time range for waveform generation");
                return;
            }

            var frequencyGroups = GroupPacketsByFrequency(packets);

            if (_useGpu && _gpuContext != null)
            {
                Logger.Debug($"GPU: Processing {frequencyGroups.Count} frequency groups");
                await GenerateWaveformsGpuAsync(waveformData, frequencyGroups, startTime, totalDuration, progress, cancellationToken);
            }
            else
            {
                await GenerateWaveformsCpuAsync(waveformData, frequencyGroups, startTime, totalDuration, progress, cancellationToken);
            }
        }

        private async Task GenerateWaveformsGpuAsync(
            WaveformData waveformData,
            Dictionary<double, List<AudioPacketMetadata>> frequencyGroups,
            DateTime startTime,
            TimeSpan totalDuration,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            if (_gpuContext == null) return;

            var totalGroups = frequencyGroups.Count;
            var processedGroups = 0;
            
            foreach (var (frequency, frequencyPackets) in frequencyGroups)
            {
                var channel = CreateChannelWaveform(frequency);
                await GenerateChannelDataGpuAsync(channel, frequencyPackets, startTime, totalDuration, cancellationToken);
                waveformData.Channels[frequency] = channel;
                
                processedGroups++;
                if (progress != null && totalGroups > 0)
                {
                    // Report progress in the 30-85% range
                    var progressPercent = 30 + (55 * processedGroups / totalGroups);
                    progress.Report(progressPercent);
                }
            }
        }

        private async Task GenerateWaveformsCpuAsync(
            WaveformData waveformData,
            Dictionary<double, List<AudioPacketMetadata>> frequencyGroups,
            DateTime startTime,
            TimeSpan totalDuration,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var totalGroups = frequencyGroups.Count;
            var processedGroups = 0;
            
            foreach (var (frequency, frequencyPackets) in frequencyGroups)
            {
                var channel = CreateChannelWaveform(frequency);
                await GenerateChannelDataCpuAsync(channel, frequencyPackets, startTime, totalDuration, cancellationToken);
                waveformData.Channels[frequency] = channel;
                
                processedGroups++;
                if (progress != null && totalGroups > 0)
                {
                    // Report progress in the 30-85% range
                    var progressPercent = 30 + (55 * processedGroups / totalGroups);
                    progress.Report(progressPercent);
                }
            }
        }

        private async Task GenerateChannelDataGpuAsync(
            WaveformChannel channel,
            List<AudioPacketMetadata> packets,
            DateTime startTime,
            TimeSpan totalDuration,
            CancellationToken cancellationToken)
        {
            if (_gpuContext == null) return;

            var timeStepTicks = totalDuration.Ticks / MaxDataPointsValue;

            // Prepare input data for GPU
            var packetData = new List<GpuPacketData>();
            foreach (var packet in packets)
            {
                if (packet.AudioPayload != null && packet.AudioPayload.Length > 0)
                {
                    var pcmSamples = Helpers.AudioHelpers.DecodeAudioToPcm(packet.AudioPayload);
                    var amplitude = Helpers.AudioHelpers.CalculateNormalizedAmplitude(pcmSamples);
                    
                    packetData.Add(GpuPacketData.FromTicks(packet.Timestamp.Ticks, amplitude));
                }
            }

            if (packetData.Count == 0)
            {
                Array.Fill(channel.Data, 0f);
                FillTimestamps(channel, startTime, timeStepTicks);
                return;
            }

            // DIAGNOSTIC: Log amplitude statistics
            var minAmp = packetData.Min(p => p.Amplitude);
            var maxAmp = packetData.Max(p => p.Amplitude);
            var avgAmp = packetData.Average(p => p.Amplitude);
            Logger.Info($"?? INPUT AMPLITUDES: min={minAmp:F6}, max={maxAmp:F6}, avg={avgAmp:F6}, packets={packetData.Count}");

            // Execute GPU compute shader
            var waveformOutput = await _gpuContext.ComputeWaveformAsync(
                packetData.ToArray(),
                startTime.Ticks,
                timeStepTicks,
                MaxDataPointsValue,
                cancellationToken);

            Array.Copy(waveformOutput, channel.Data, Math.Min(waveformOutput.Length, channel.Data.Length));
            FillTimestamps(channel, startTime, timeStepTicks);
            channel.LastUpdate = DateTime.UtcNow;
            
            // DIAGNOSTIC: Log output waveform statistics
            var outputMin = waveformOutput.Min();
            var outputMax = waveformOutput.Max();
            var outputAvg = waveformOutput.Average();
            var nonZeroPoints = channel.Data.Count(d => d > 0.001f);
            var significantPoints = channel.Data.Count(d => d > 0.01f);
            
            Logger.Info($"?? OUTPUT WAVEFORM: min={outputMin:F6}, max={outputMax:F6}, avg={outputAvg:F6}");
            Logger.Info($"?? POINT STATS: total={MaxDataPointsValue}, nonZero={nonZeroPoints}, significant(>0.01)={significantPoints}");
            
            if (outputMax < 0.01f)
            {
                Logger.Warn($"?? OUTPUT AMPLITUDE VERY LOW! Max={outputMax:F6} - waveform may appear flat!");
            }
        }

        private async Task GenerateChannelDataCpuAsync(
            WaveformChannel channel,
            List<AudioPacketMetadata> packets,
            DateTime startTime,
            TimeSpan totalDuration,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var timeStep = TimeSpan.FromTicks(totalDuration.Ticks / MaxDataPointsValue);

                for (int i = 0; i < MaxDataPointsValue && !cancellationToken.IsCancellationRequested; i++)
                {
                    var windowStart = startTime + TimeSpan.FromTicks(i * timeStep.Ticks);
                    var windowEnd = windowStart + timeStep;

                    channel.TimeStamps[i] = windowStart;

                    var windowPackets = packets.Where(p => p.Timestamp >= windowStart && p.Timestamp < windowEnd).ToList();

                    if (windowPackets.Count == 0)
                    {
                        channel.Data[i] = 0f;
                        continue;
                    }

                    // Find peak amplitude in this time window
                    var maxAmplitude = 0.0f;

                    foreach (var packet in windowPackets)
                    {
                        if (packet.AudioPayload != null && packet.AudioPayload.Length > 0)
                        {
                            var pcmSamples = Helpers.AudioHelpers.DecodeAudioToPcm(packet.AudioPayload);
                            var amplitude = Helpers.AudioHelpers.CalculateNormalizedAmplitude(pcmSamples);
                            maxAmplitude = Math.Max(maxAmplitude, amplitude);
                        }
                    }

                    channel.Data[i] = maxAmplitude;
                }

                channel.LastUpdate = DateTime.UtcNow;
            }, cancellationToken);
        }

        // OVERRIDE: Implement abstract Dispose method from base class
        public override void Dispose()
        {
            if (Disposed) return;

            Clear();
            
            // NEW: Dispose layered renderer
            _layeredRenderer?.Dispose();
            _layeredRenderer = null;
            
            _gpuContext?.Dispose();
            _gpuContext = null;
            Disposed = true;
            
            Logger.Debug($"GpuWaveformGenerator disposed (was using {(_useGpu ? "GPU" : "CPU")}, layered: {Constants.USE_LAYERED_WAVEFORM_RENDERING})");
        }
    }

    /// <summary>
    /// Packet data structure for GPU processing
    /// Matches HLSL shader layout with 64-bit values split into two 32-bit integers
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GpuPacketData
    {
        /// <summary>Low 32 bits of timestamp ticks</summary>
        public int TimestampTicksLow;
        
        /// <summary>High 32 bits of timestamp ticks</summary>
        public int TimestampTicksHigh;
        
        /// <summary>Audio amplitude (0.0 to 1.0)</summary>
        public float Amplitude;
        
        /// <summary>Padding for 16-byte alignment</summary>
        public float Padding;

        /// <summary>
        /// Creates GpuPacketData from a 64-bit timestamp
        /// </summary>
        public static GpuPacketData FromTicks(long timestampTicks, float amplitude)
        {
            return new GpuPacketData
            {
                TimestampTicksLow = (int)(timestampTicks & 0xFFFFFFFF),
                TimestampTicksHigh = (int)((timestampTicks >> 32) & 0xFFFFFFFF),
                Amplitude = amplitude,
                Padding = 0f
            };
        }
    }
}
