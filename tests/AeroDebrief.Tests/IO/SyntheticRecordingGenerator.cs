using AeroDebrief.Core;
using System.IO;
using System.Text;

namespace AeroDebrief.Tests.IO
{
    /// <summary>
    /// Generates synthetic 100MB+ SRS recording files for testing
    /// </summary>
    public static class SyntheticRecordingGenerator
    {
        private static readonly Random _random = new();

        /// <summary>
        /// Generates a synthetic recording file of approximately targetSizeMB
        /// </summary>
        public static async Task<string> GenerateAsync(
            int targetSizeMB = 100,
            int packetIntervalMs = 40,
            CancellationToken cancellationToken = default)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"synthetic_{Guid.NewGuid()}.adb");
            
            await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new BinaryWriter(fs);

            // Write header
            writer.Write(Constants.RECORDING_FILE_MAGIC);
            writer.Write("192.168.1.100");
            writer.Write(5002);
            writer.Write(DateTime.UtcNow.Ticks);

            // Calculate packets needed for target size
            const int avgPacketSize = 2048; // Approximate packet size
            long targetBytes = targetSizeMB * 1024L * 1024L;
            int estimatedPackets = (int)(targetBytes / avgPacketSize);

            var startTime = DateTime.UtcNow;
            var frequencies = new[] { 251_000_000.0, 243_000_000.0, 305_000_000.0 }; // VHF AM frequencies
            var players = new[] { "Viper-1", "Enfield-2-1", "Uzi-1", "Hawg-1-1" };

            for (int i = 0; i < estimatedPackets && !cancellationToken.IsCancellationRequested; i++)
            {
                var timestamp = startTime.AddMilliseconds(i * packetIntervalMs);
                var frequency = frequencies[i % frequencies.Length];
                var playerName = players[i % players.Length];
                var guid = $"player-{i % players.Length:D4}";

                // Generate synthetic audio packet
                var audioLength = _random.Next(960, 1920); // OPUS frame sizes
                var audioData = GenerateSyntheticAudio(audioLength);

                var playerInfo = new PlayerInfo
                {
                    Name = playerName,
                    TransmitterGuid = guid,
                    Coalition = (i % 2) + 1,
                    Seat = 0,
                    AllowRecord = true,
                    Position = new Position
                    {
                        Latitude = 42.0 + (_random.NextDouble() * 0.1),
                        Longitude = 42.0 + (_random.NextDouble() * 0.1),
                        Altitude = 5000 + (_random.Next(0, 10000))
                    },
                    AircraftInfo = new AircraftInfo
                    {
                        UnitType = "F-16C_50",
                        UnitId = (uint)i
                    }
                };

                var metadata = new AudioPacketMetadata(
                    timestamp,
                    frequency,
                    (byte)0, // AM
                    (byte)0, // No encryption
                    (uint)i,
                    (ulong)i,
                    guid,
                    playerInfo,
                    48000,
                    1,
                    playerInfo.Coalition,
                    audioData
                );

                metadata.TryWriteMetadata(writer);

                // Yield periodically
                if (i % 100 == 0)
                    await Task.Yield();
            }

            return tempFile;
        }

        private static byte[] GenerateSyntheticAudio(int length)
        {
            var data = new byte[length];
            
            // Generate simple sine wave
            for (int i = 0; i < length / 2; i++)
            {
                var t = i / 48000.0;
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * t) * 3000); // 440Hz tone
                var bytes = BitConverter.GetBytes(sample);
                data[i * 2] = bytes[0];
                data[i * 2 + 1] = bytes[1];
            }

            return data;
        }
    }
}
