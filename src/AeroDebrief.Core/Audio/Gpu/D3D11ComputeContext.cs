using System.Runtime.InteropServices;
using NLog;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Direct3D 11 compute shader implementation for GPU-accelerated waveform generation
    /// </summary>
    public sealed class D3D11ComputeContext : IGpuComputeContext
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11ComputeShader? _computeShader;
        private bool _isAvailable;
        private bool _disposed;
        private string _deviceName = "Unknown";
        private int _maxThreads = 1024;
        
        // Synchronization for thread-safe GPU access
        // ID3D11DeviceContext is NOT thread-safe and must be accessed from only one thread at a time
        private readonly SemaphoreSlim _contextLock = new SemaphoreSlim(1, 1);

        // NEW: Expose Device and Context for LayeredWaveformRenderer
        /// <summary>
        /// Gets the Direct3D 11 device (for creating resources)
        /// </summary>
        public ID3D11Device Device => _device ?? throw new InvalidOperationException("Device not initialized");
        
        /// <summary>
        /// Gets the Direct3D 11 device context (for GPU operations)
        /// </summary>
        public ID3D11DeviceContext Context => _context ?? throw new InvalidOperationException("Context not initialized");

        // Compute shader HLSL code for waveform generation
        private const string ComputeShaderSource = @"
            struct PacketData
            {
                int2 timestampTicks;  // 64-bit timestamp as two 32-bit ints (low, high)
                float amplitude;
                float padding;        // Padding for 16-byte alignment
            };

            StructuredBuffer<PacketData> inputPackets : register(t0);
            RWStructuredBuffer<float> outputWaveform : register(u0);

            cbuffer Constants : register(b0)
            {
                int2 startTimeTicks;   // 64-bit as two 32-bit ints (low, high)
                int2 timeStepTicks;    // 64-bit as two 32-bit ints (low, high)
                int outputPoints;
                int packetCount;
                int2 padding;          // Padding to 16-byte boundary
            };

            // Helper function to compare two 64-bit values represented as int2
            bool isGreaterOrEqual(int2 a, int2 b)
            {
                if (a.y > b.y) return true;
                if (a.y < b.y) return false;
                return (uint)a.x >= (uint)b.x;
            }

            bool isLessThan(int2 a, int2 b)
            {
                if (a.y < b.y) return true;
                if (a.y > b.y) return false;
                return (uint)a.x < (uint)b.x;
            }

            // Helper function to add two 64-bit values represented as int2
            int2 add64(int2 a, int2 b)
            {
                int2 result;
                result.x = a.x + b.x;
                // Handle carry from low to high word
                uint carry = ((uint)a.x + (uint)b.x) < (uint)a.x ? 1 : 0;
                result.y = a.y + b.y + carry;
                return result;
            }

            // FIXED: Proper 64-bit multiplication for scalar * int2
            int2 multiply64Scalar(int2 a, int scalar)
            {
                int2 result;
                
                // Convert to unsigned for proper multiplication
                uint aLow = (uint)a.x;
                uint aHigh = (uint)a.y;
                uint scalarU = (uint)scalar;
                
                // Multiply low word: this can produce a 64-bit result
                uint lowProduct = aLow * scalarU;
                
                // Multiply high word
                uint highProduct = aHigh * scalarU;
                
                // Get the high 32 bits of the low product (overflow/carry)
                // We need to do this carefully to avoid losing precision
                // Split aLow into two 16-bit parts for 32x32=64 bit multiply
                uint aLow_lo = aLow & 0xFFFF;
                uint aLow_hi = aLow >> 16;
                
                uint prod_lo = aLow_lo * scalarU;
                uint prod_hi = aLow_hi * scalarU;
                
                // Add the high part of prod_lo to prod_hi
                prod_hi += (prod_lo >> 16);
                
                // The carry to the high word is the high 16 bits of prod_hi
                uint carry = prod_hi >> 16;
                
                result.x = (int)lowProduct;
                result.y = (int)(highProduct + carry);
                
                return result;
            }

            [numthreads(256, 1, 1)]
            void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
            {
                uint pointIndex = dispatchThreadID.x;
                
                if (pointIndex >= (uint)outputPoints)
                    return;

                // Calculate time window for this waveform point
                int2 windowStart = add64(startTimeTicks, multiply64Scalar(timeStepTicks, (int)pointIndex));
                int2 windowEnd = add64(windowStart, timeStepTicks);

                // Find peak amplitude from packets in this time window
                float maxAmplitude = 0.0;

                for (int i = 0; i < packetCount; i++)
                {
                    int2 packetTime = inputPackets[i].timestampTicks;
                    
                    if (isGreaterOrEqual(packetTime, windowStart) && isLessThan(packetTime, windowEnd))
                    {
                        float amplitude = inputPackets[i].amplitude;
                        maxAmplitude = max(maxAmplitude, amplitude);
                    }
                }

                outputWaveform[pointIndex] = maxAmplitude;
            }
        ";

        public bool IsAvailable => _isAvailable;
        public string DeviceName => _deviceName;
        public string ComputeCapability => "DirectX 11.0 Compute Shader 5.0";
        public int MaxThreads => _maxThreads;

        public D3D11ComputeContext()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                Logger.Debug("Initializing Direct3D 11 compute context...");

                // Create D3D11 device and context
                var result = D3D11.D3D11CreateDevice(
                    null,
                    DriverType.Hardware,
                    DeviceCreationFlags.None,
                    new[] { FeatureLevel.Level_11_0 },
                    out _device,
                    out var featureLevel,
                    out _context);

                if (result.Failure || _device == null || _context == null)
                {
                    Logger.Warn("Failed to create Direct3D 11 device");
                    _isAvailable = false;
                    return;
                }

                // Get adapter information
                using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
                using var adapter = dxgiDevice.GetAdapter();
                var desc = adapter.Description;
                _deviceName = desc.Description;

                Logger.Info($"Created D3D11 device: {_deviceName}");
                Logger.Info($"Feature Level: {featureLevel}");
                Logger.Info($"Dedicated Video Memory: {desc.DedicatedVideoMemory / (1024 * 1024)} MB");

                // Compile compute shader
                if (!CompileComputeShader())
                {
                    Logger.Warn("Failed to compile compute shader");
                    _isAvailable = false;
                    return;
                }

                _isAvailable = true;
                Logger.Info("? Direct3D 11 compute context initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize Direct3D 11 compute context");
                _isAvailable = false;
                Cleanup();
            }
        }

        private bool CompileComputeShader()
        {
            try
            {
                Logger.Debug("Compiling compute shader...");

                // Compiler.Compile returns ReadOnlyMemory<byte> in Vortice 3.x
                var bytecode = Compiler.Compile(
                    ComputeShaderSource,
                    "CSMain",
                    "main", // Entry point
                    "cs_5_0");

                if (bytecode.IsEmpty)
                {
                    Logger.Error("Compute shader compilation failed - bytecode is empty");
                    return false;
                }

                _computeShader = _device!.CreateComputeShader(bytecode.Span);
                
                Logger.Info("? Compute shader compiled successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during compute shader compilation");
                return false;
            }
        }

        public async Task<float[]> ComputeWaveformAsync(
            GpuPacketData[] packetData,
            long startTimeTicks,
            long timeStepTicks,
            int outputPoints,
            CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || _device == null || _context == null || _computeShader == null)
            {
                throw new InvalidOperationException("GPU compute context not available");
            }

            // Acquire exclusive access to the device context
            await _contextLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                // Execute GPU operations synchronously while holding the lock
                // ID3D11DeviceContext is NOT thread-safe and requires exclusive access
                cancellationToken.ThrowIfCancellationRequested();

                // Create input buffer for packet data
                var inputBufferDesc = new BufferDescription
                {
                    BindFlags = BindFlags.ShaderResource,
                    ByteWidth = Marshal.SizeOf<GpuPacketData>() * packetData.Length,
                    Usage = ResourceUsage.Default,
                    StructureByteStride = Marshal.SizeOf<GpuPacketData>(),
                    MiscFlags = ResourceOptionFlags.BufferStructured
                };

                using var inputBuffer = _device.CreateBuffer(inputBufferDesc);
                _context.UpdateSubresource(packetData, inputBuffer);
                using var inputSRV = _device.CreateShaderResourceView(inputBuffer);

                // Create output buffer for waveform data
                var outputBufferDesc = new BufferDescription
                {
                    BindFlags = BindFlags.UnorderedAccess,
                    ByteWidth = sizeof(float) * outputPoints,
                    Usage = ResourceUsage.Default,
                    StructureByteStride = sizeof(float),
                    MiscFlags = ResourceOptionFlags.BufferStructured
                };

                using var outputBuffer = _device.CreateBuffer(outputBufferDesc);
                using var outputUAV = _device.CreateUnorderedAccessView(outputBuffer);

                // Create constant buffer for parameters
                var constants = CreateConstants(startTimeTicks, timeStepTicks, outputPoints, packetData.Length);

                var constantBufferDesc = new BufferDescription
                {
                    BindFlags = BindFlags.ConstantBuffer,
                    ByteWidth = Marshal.SizeOf<ComputeConstants>(),
                    Usage = ResourceUsage.Default
                };

                using var constantBuffer = _device.CreateBuffer(constantBufferDesc);
                _context.UpdateSubresource(ref constants, constantBuffer);

                // Set up compute shader pipeline
                _context.CSSetShader(_computeShader);
                _context.CSSetShaderResource(0, inputSRV);
                _context.CSSetUnorderedAccessView(0, outputUAV);
                _context.CSSetConstantBuffer(0, constantBuffer);

                // Dispatch compute shader (256 threads per group)
                var threadGroups = (outputPoints + 255) / 256;
                _context.Dispatch(threadGroups, 1, 1);

                // Read back results
                var stagingBufferDesc = new BufferDescription
                {
                    BindFlags = BindFlags.None,
                    ByteWidth = sizeof(float) * outputPoints,
                    Usage = ResourceUsage.Staging,
                    CPUAccessFlags = CpuAccessFlags.Read
                };

                using var stagingBuffer = _device.CreateBuffer(stagingBufferDesc);
                _context.CopyResource(stagingBuffer, outputBuffer);

                // Map and copy data
                var output = new float[outputPoints];
                var mappedResource = _context.Map(stagingBuffer, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    Marshal.Copy(mappedResource.DataPointer, output, 0, outputPoints);
                }
                finally
                {
                    _context.Unmap(stagingBuffer, 0);
                }

                // Unbind resources
                _context.CSSetShader(null);
                _context.CSSetShaderResource(0, null);
                _context.CSSetUnorderedAccessView(0, null);
                _context.CSSetConstantBuffer(0, null);

                Logger.Debug($"GPU computed {outputPoints} waveform points from {packetData.Length} packets");
                return output;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GPU compute failed");
                throw;
            }
            finally
            {
                // Always release the lock, even if an exception occurred
                _contextLock.Release();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComputeConstants
        {
            public int StartTimeTicksLow;
            public int StartTimeTicksHigh;
            public int TimeStepTicksLow;
            public int TimeStepTicksHigh;
            public int OutputPoints;
            public int PacketCount;
            public int Padding1;  // Padding to ensure 16-byte alignment
            public int Padding2;
        }

        private static ComputeConstants CreateConstants(long startTimeTicks, long timeStepTicks, int outputPoints, int packetCount)
        {
            return new ComputeConstants
            {
                StartTimeTicksLow = (int)(startTimeTicks & 0xFFFFFFFF),
                StartTimeTicksHigh = (int)((startTimeTicks >> 32) & 0xFFFFFFFF),
                TimeStepTicksLow = (int)(timeStepTicks & 0xFFFFFFFF),
                TimeStepTicksHigh = (int)((timeStepTicks >> 32) & 0xFFFFFFFF),
                OutputPoints = outputPoints,
                PacketCount = packetCount,
                Padding1 = 0,
                Padding2 = 0
            };
        }

        private void Cleanup()
        {
            _computeShader?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
            
            _computeShader = null;
            _context = null;
            _device = null;
        }

        public void Dispose()
        {
            if (_disposed) return;

            Cleanup();
            _contextLock.Dispose();
            _disposed = true;
            
            Logger.Debug("D3D11ComputeContext disposed");
        }
    }
}
