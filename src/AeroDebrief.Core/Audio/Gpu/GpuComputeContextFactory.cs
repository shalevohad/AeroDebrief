using NLog;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Factory for creating GPU compute contexts with automatic fallback
    /// </summary>
    public static class GpuComputeContextFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates a GPU compute context, trying different implementations in order of preference
        /// </summary>
        public static IGpuComputeContext? CreateContext()
        {
            // Try Direct3D 11 first (most compatible on Windows)
            try
            {
                Logger.Debug("Attempting to create Direct3D 11 compute context...");
                var d3d11Context = new D3D11ComputeContext();
                if (d3d11Context.IsAvailable)
                {
                    Logger.Info("? Direct3D 11 compute context created successfully");
                    return d3d11Context;
                }
                d3d11Context.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Direct3D 11 compute context not available");
            }

            // Could add other GPU backends here (Vulkan, OpenCL, etc.)
            // For now, return null to fall back to CPU
            Logger.Info("No GPU compute context available, will use CPU fallback");
            return null;
        }
    }
}
