using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// High-performance OPUS decoder with buffer pooling and minimal allocations.
    /// Optimized with SRS-inspired techniques: ArrayPool, stackalloc, aggressive inlining.
    /// </summary>
    public sealed class OpusDecoder : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Shared;

        private IntPtr _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private bool _disposed;

        private const int MaxFrameSize = 5760; // 120ms at 48kHz
        private const int OpusOk = 0;

        // Frame size cache for common rates (avoid recalculation)
        private readonly int _defaultFrameSize;

        public OpusDecoder(int sampleRate = 48000, int channels = 1)
        {
            if (sampleRate != 8000 && sampleRate != 12000 && sampleRate != 16000 && 
                sampleRate != 24000 && sampleRate != 48000)
            {
                throw new ArgumentException($"Invalid sample rate: {sampleRate}", nameof(sampleRate));
            }

            if (channels != 1 && channels != 2)
            {
                throw new ArgumentException("Channels must be 1 (mono) or 2 (stereo)", nameof(channels));
            }

            _sampleRate = sampleRate;
            _channels = channels;
            _defaultFrameSize = sampleRate * 20 / 1000; // Cache 20ms frame size

            int error;
            _decoder = NativeMethods.opus_decoder_create(sampleRate, channels, out error);

            if (error != OpusOk || _decoder == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to create OPUS decoder: error={error}");
            }

            Logger.Debug($"OpusDecoder created: {sampleRate}Hz, {channels}ch");
        }

        /// <summary>
        /// Optimized decode with buffer pooling (zero allocation on hot path)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[]? DecodeToFloat(byte[] packet, int frameSize = 0)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusDecoder));

            if (packet == null || packet.Length == 0)
                return null;

            try
            {
                // Use cached frame size if not specified
                if (frameSize == 0)
                    frameSize = _defaultFrameSize;

                var bufferLength = frameSize * _channels;
                
                // Always use ArrayPool to avoid stackalloc Span exposure issues
                var rentedBuffer = FloatPool.Rent(bufferLength);
                var pcmBuffer = rentedBuffer.AsSpan(0, bufferLength);

                try
                {
                    int samplesDecoded = NativeMethods.opus_decode_float(
                        _decoder,
                        packet,
                        packet.Length,
                        ref MemoryMarshal.GetReference(pcmBuffer),
                        frameSize,
                        0
                    );

                    if (samplesDecoded < 0)
                    {
                        Logger.Warn($"OPUS decode error: {samplesDecoded}");
                        return null;
                    }

                    // Allocate exact size needed
                    var actualLength = samplesDecoded * _channels;
                    var result = new float[actualLength];
                    pcmBuffer.Slice(0, actualLength).CopyTo(result);
                    return result;
                }
                finally
                {
                    FloatPool.Return(rentedBuffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error decoding OPUS packet");
                return null;
            }
        }

        /// <summary>
        /// Decode with FEC (optimized)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[]? DecodeWithFEC(byte[] packet, int frameSize, bool useFEC = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusDecoder));

            try
            {
                var bufferLength = frameSize * _channels;
                var rentedBuffer = FloatPool.Rent(bufferLength);

                try
                {
                    var pcmBuffer = rentedBuffer.AsSpan(0, bufferLength);
                    
                    int samplesDecoded = NativeMethods.opus_decode_float(
                        _decoder,
                        packet,
                        packet.Length,
                        ref MemoryMarshal.GetReference(pcmBuffer),
                        frameSize,
                        useFEC ? 1 : 0
                    );

                    if (samplesDecoded < 0)
                        return null;

                    var result = new float[samplesDecoded * _channels];
                    pcmBuffer.Slice(0, result.Length).CopyTo(result);
                    return result;
                }
                finally
                {
                    FloatPool.Return(rentedBuffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error decoding OPUS packet with FEC");
                return null;
            }
        }

        /// <summary>
        /// Generates PLC samples (optimized)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public float[]? GeneratePLC(int frameSize)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusDecoder));

            try
            {
                var bufferLength = frameSize * _channels;
                var rentedBuffer = FloatPool.Rent(bufferLength);

                try
                {
                    var pcmBuffer = rentedBuffer.AsSpan(0, bufferLength);

                    int samplesGenerated = NativeMethods.opus_decode_float(
                        _decoder,
                        null,
                        0,
                        ref MemoryMarshal.GetReference(pcmBuffer),
                        frameSize,
                        0
                    );

                    if (samplesGenerated < 0)
                        return null;

                    var result = new float[bufferLength];
                    pcmBuffer.CopyTo(result);
                    return result;
                }
                finally
                {
                    FloatPool.Return(rentedBuffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating PLC");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            if (_disposed)
                return;

            NativeMethods.opus_decoder_ctl(_decoder, OpusCtl.OPUS_RESET_STATE, 0);
            Logger.Debug("OpusDecoder reset");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_decoder != IntPtr.Zero)
            {
                NativeMethods.opus_decoder_destroy(_decoder);
                _decoder = IntPtr.Zero;
            }

            _disposed = true;
            Logger.Debug("OpusDecoder disposed");
        }

        #region Native Methods

        private static class NativeMethods
        {
            private const string LibOpus = "opus.dll";

            [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

            [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
            public static extern void opus_decoder_destroy(IntPtr decoder);

            [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
            public static extern int opus_decode_float(
                IntPtr st,
                byte[]? data,
                int len,
                ref float pcm,
                int frame_size,
                int decode_fec);

            [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
            public static extern int opus_decoder_ctl(IntPtr st, OpusCtl request, int value);
        }

        private enum OpusCtl
        {
            OPUS_RESET_STATE = 4028
        }

        #endregion
    }

    /// <summary>
    /// Optimized OPUS helper methods with minimal allocations
    /// </summary>
    public static class OpusHelper
    {
        private static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Shared;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static float[]? DecodePacket(RadioPacket packet, OpusDecoder decoder)
        {
            if (packet?.AudioPayload == null || packet.AudioPayload.Length == 0)
                return null;

            bool isOpus = IsOpusEncoded(packet);

            if (!isOpus)
            {
                return ConvertPCM16ToFloat(packet.AudioPayload);
            }

            return decoder.DecodeToFloat(packet.AudioPayload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOpusEncoded(RadioPacket packet)
        {
            return packet.Encryption > 0 || packet.AudioPayload.Length < 2000;
        }

        /// <summary>
        /// Optimized PCM16 to float conversion with SIMD-friendly loop
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static float[] ConvertPCM16ToFloat(byte[] pcmData)
        {
            var samples = new float[pcmData.Length / 2];
            var pcmSpan = MemoryMarshal.Cast<byte, short>(pcmData.AsSpan());
            
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = pcmSpan[i] / 32768.0f;
            }

            return samples;
        }

        /// <summary>
        /// Optimized float to PCM16 conversion
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] ConvertFloatToPCM16(float[] samples)
        {
            var pcm = new byte[samples.Length * 2];
            var pcmSpan = MemoryMarshal.Cast<byte, short>(pcm.AsSpan());

            for (int i = 0; i < samples.Length; i++)
            {
                pcmSpan[i] = (short)Math.Clamp(samples[i] * 32768.0f, short.MinValue, short.MaxValue);
            }

            return pcm;
        }
    }
}
