using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Abstraction for compression/decompression codec used in CVR archives.
    /// Allows swapping between Zstd, Brotli, or other compression algorithms.
    /// </summary>
    public interface IArchiveCodec
    {
        /// <summary>
        /// Logical codec identifier (e.g., "zstd", "brotli")
        /// </summary>
        string CodecId { get; }

        /// <summary>
        /// Compresses input stream into output stream.
        /// Streams are owned by caller and must not be disposed here.
        /// </summary>
        Task CompressAsync(Stream input, Stream output, CancellationToken ct = default);

        /// <summary>
        /// Decompresses input stream into output stream.
        /// Streams are owned by caller and must not be disposed here.
        /// </summary>
        Task DecompressAsync(Stream input, Stream output, CancellationToken ct = default);
    }
}
