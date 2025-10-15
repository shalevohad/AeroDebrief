using System.Runtime.InteropServices;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Audio
{
    /// <summary>
    /// Simple, reliable audio output using Windows WaveOut API directly
    /// This bypasses NAudio's complex WASAPI implementation which can have compatibility issues
    /// </summary>
    public sealed class SimpleAudioOutputEngine : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Windows WaveOut API declarations
        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormat lpFormat, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public short FormatTag;
            public short Channels;
            public int SamplesPerSec;
            public int AvgBytesPerSec;
            public short BlockAlign;
            public short BitsPerSample;
            public short Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public int BufferLength;
            public int BytesRecorded;
            public IntPtr User;
            public int Flags;
            public int Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        private const int WAVE_MAPPER = -1;
        private const int WAVE_FORMAT_PCM = 1;
        private const int MMSYSERR_NOERROR = 0;

        private IntPtr _hWaveOut = IntPtr.Zero;
        private bool _disposed = false;

        /// <summary>
        /// Initialize the simple audio output engine
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Logger.Info("Initializing SimpleAudioOutputEngine using Windows WaveOut API...");

                // Create wave format for 48kHz, 16-bit, mono
                var waveFormat = new WaveFormat
                {
                    FormatTag = WAVE_FORMAT_PCM,
                    Channels = 1,
                    SamplesPerSec = Constants.OUTPUT_SAMPLE_RATE,
                    BitsPerSample = 16,
                    BlockAlign = 2, // 1 channel * 16 bits / 8
                    AvgBytesPerSec = Constants.OUTPUT_SAMPLE_RATE * 2 // 48000 * 2 bytes
                };

                // Open the wave output device
                int result = waveOutOpen(out _hWaveOut, WAVE_MAPPER, ref waveFormat, IntPtr.Zero, IntPtr.Zero, 0);
                
                if (result != MMSYSERR_NOERROR)
                {
                    throw new InvalidOperationException($"Failed to open wave output device. Error code: {result}");
                }

                Logger.Info("? SimpleAudioOutputEngine initialized successfully using WaveOut API");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize SimpleAudioOutputEngine");
                throw;
            }
        }

        /// <summary>
        /// Play audio data directly using WaveOut API
        /// </summary>
        public async Task PlayAudioAsync(byte[] audioData)
        {
            if (_hWaveOut == IntPtr.Zero)
            {
                throw new InvalidOperationException("Audio engine not initialized");
            }

            if (audioData == null || audioData.Length == 0)
            {
                Logger.Warn("No audio data to play");
                return;
            }

            try
            {
                Logger.Debug($"Playing {audioData.Length} bytes of audio data...");

                // Allocate memory for the audio data
                IntPtr audioPtr = Marshal.AllocHGlobal(audioData.Length);
                try
                {
                    // Copy audio data to unmanaged memory
                    Marshal.Copy(audioData, 0, audioPtr, audioData.Length);

                    // Prepare wave header
                    var waveHeader = new WaveHeader
                    {
                        Data = audioPtr,
                        BufferLength = audioData.Length,
                        Flags = 0,
                        Loops = 0
                    };

                    // Prepare the header
                    int result = waveOutPrepareHeader(_hWaveOut, ref waveHeader, Marshal.SizeOf<WaveHeader>());
                    if (result != MMSYSERR_NOERROR)
                    {
                        throw new InvalidOperationException($"Failed to prepare wave header. Error: {result}");
                    }

                    try
                    {
                        // Write the audio data
                        result = waveOutWrite(_hWaveOut, ref waveHeader, Marshal.SizeOf<WaveHeader>());
                        if (result != MMSYSERR_NOERROR)
                        {
                            throw new InvalidOperationException($"Failed to write audio data. Error: {result}");
                        }

                        Logger.Debug("? Audio data sent to WaveOut API successfully");

                        // Wait for playback to complete
                        // Calculate duration based on sample rate and data size
                        var durationMs = (audioData.Length * 1000) / (Constants.OUTPUT_SAMPLE_RATE * 2); // 2 bytes per sample
                        var waitTime = Math.Max(100, durationMs + 500); // Add 500ms buffer

                        Logger.Debug($"Waiting {waitTime}ms for audio playback to complete...");
                        await Task.Delay(waitTime);

                        Logger.Debug("? Audio playback completed");
                    }
                    finally
                    {
                        // Unprepare the header
                        waveOutUnprepareHeader(_hWaveOut, ref waveHeader, Marshal.SizeOf<WaveHeader>());
                    }
                }
                finally
                {
                    // Free the allocated memory
                    Marshal.FreeHGlobal(audioPtr);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to play audio using SimpleAudioOutputEngine");
                throw;
            }
        }

        /// <summary>
        /// Set master volume (placeholder - WaveOut API doesn't directly support volume control)
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            // Note: WaveOut API doesn't have direct volume control
            // Volume should be applied to the audio data before calling PlayAudioAsync
            Logger.Debug($"Volume set to {volume:F2} (will be applied to audio data)");
        }

        /// <summary>
        /// Stop all audio playback
        /// </summary>
        public void Stop()
        {
            if (_hWaveOut != IntPtr.Zero)
            {
                try
                {
                    Logger.Debug("Stopping audio playback...");
                    waveOutReset(_hWaveOut);
                    Logger.Debug("? Audio playback stopped");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error stopping audio playback");
                }
            }
        }

        /// <summary>
        /// Start playback (not needed for WaveOut API - audio starts immediately when written)
        /// </summary>
        public void Start()
        {
            Logger.Debug("SimpleAudioOutputEngine ready (no explicit start needed for WaveOut API)");
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                Logger.Debug("Disposing SimpleAudioOutputEngine...");

                if (_hWaveOut != IntPtr.Zero)
                {
                    Stop();
                    waveOutClose(_hWaveOut);
                    _hWaveOut = IntPtr.Zero;
                    Logger.Debug("? WaveOut device closed");
                }

                _disposed = true;
                Logger.Debug("? SimpleAudioOutputEngine disposed");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during SimpleAudioOutputEngine disposal");
            }
        }
    }
}