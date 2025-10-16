using System;
using System.IO;
using NLog;
using NAudio.Wave;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Simple debug utility to dump audio to WAV files when enabled.
    /// Controlled by the environment variable `DUMP_AUDIO_DEBUG` (value "1" enables dumping)
    /// or by setting `DebugAudioDumper.Enabled = true` at runtime.
    /// </summary>
    public static class DebugAudioDumper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string _dumpDir;
        private static int _fileCounter = 0;

        static DebugAudioDumper()
        {
            var env = Environment.GetEnvironmentVariable("DUMP_AUDIO_DEBUG");
            Enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase);

            _dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory, "DebugAudio");
            try
            {
                Directory.CreateDirectory(_dumpDir);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to create DebugAudio directory");
            }
        }

        /// <summary>
        /// When true, audio dumps will be created. Can be toggled at runtime.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Dump float samples (range -1.0..1.0) to a WAV file (16-bit PCM)
        /// </summary>
        public static void DumpFloatAsWav(float[] samples, int sampleRate, string label)
        {
            if (!Enabled || samples == null || samples.Length == 0)
                return;

            try
            {
                var id = System.Threading.Interlocked.Increment(ref _fileCounter);
                var fileName = MakeFileName(id, label, "prepost.wav");
                var path = Path.Combine(_dumpDir, fileName);

                // Convert floats to 16-bit PCM bytes
                var pcm = AudioConverter.FloatToPcm16(samples);

                using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
                writer.Write(pcm, 0, pcm.Length);
                writer.Flush();

                Logger.Info($"Debug audio dumped (float -> WAV): {path} ({samples.Length} samples)");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to dump float samples to WAV");
            }
        }

        /// <summary>
        /// Dump raw PCM16 bytes (little-endian) to a WAV file
        /// </summary>
        public static void DumpPcmBytesAsWav(byte[] pcmBytes, int sampleRate, string label)
        {
            if (!Enabled || pcmBytes == null || pcmBytes.Length == 0)
                return;

            try
            {
                var id = System.Threading.Interlocked.Increment(ref _fileCounter);
                var fileName = MakeFileName(id, label, "out.wav");
                var path = Path.Combine(_dumpDir, fileName);

                using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
                writer.Write(pcmBytes, 0, pcmBytes.Length);
                writer.Flush();

                Logger.Info($"Debug audio dumped (PCM bytes -> WAV): {path} ({pcmBytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to dump PCM bytes to WAV");
            }
        }

        private static string MakeFileName(int id, string label, string suffix)
        {
            var time = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            label = string.IsNullOrWhiteSpace(label) ? "unknown" : SafeFileName(label);
            return $"{id:0000}_{time}_{label}_{suffix}";
        }

        private static string SafeFileName(string input)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }
            return input.Length > 40 ? input.Substring(0, 40) : input;
        }
    }
}
