using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;

namespace AeroDebrief.Core.Storage.Codecs
{
    /// <summary>
    /// Brotli compression codec implementation (alternative to Zstd).
    /// Provides good compression but slower than Zstd.
    /// Included as an example of how to swap compression algorithms.
    /// </summary>
    /// <remarks>
    /// To use this instead of Zstd:
    /// 1. Call CvrFormat.SetCodec(new BrotliArchiveCodec())
    /// 2. Or register in DI: services.AddSingleton&lt;IArchiveCodec, BrotliArchiveCodec&gt;()
    /// </remarks>
    public sealed class BrotliArchiveCodec : IArchiveCodec
    {
        public string CodecId => "brotli";

        public async Task CompressAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            using (var compressor = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                await input.CopyToAsync(compressor, 81920, ct).ConfigureAwait(false);
                await compressor.FlushAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task DecompressAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            using (var decompressor = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                await decompressor.CopyToAsync(output, 81920, ct).ConfigureAwait(false);
                await output.FlushAsync(ct).ConfigureAwait(false);
            }
        }
    }
}
