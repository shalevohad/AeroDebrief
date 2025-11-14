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
            CancellationToken cancellationToken = default,
            IProgress<int>? progress = null)
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
            // 
            // PACKET STRUCTURE BREAKDOWN (with ±3.5% audio variance):
            // ???????????????????????????????????????????????????????????
            // ? Component                    ? Size (bytes)             ?
            // ???????????????????????????????????????????????????????????
            // ? Fixed Header                 ? 48                       ?
            // ?   - Timestamp (Int64)        ?   8                      ?
            // ?   - Frequency (Double)       ?   8                      ?
            // ?   - Modulation (Byte)        ?   1                      ?
            // ?   - Encryption (Byte)        ?   1                      ?
            // ?   - TransmitterUnitId (UInt) ?   4                      ?
            // ?   - PacketId (UInt64)        ?   8                      ?
            // ?   - TransmitterGuid (ASCII)  ?   22                     ?
            // ???????????????????????????????????????????????????????????
            // ? PlayerInfo                   ? ~150                     ?
            // ?   - Name length + string     ?   ~30-50                 ?
            // ?   - GUID length + string     ?   ~30                    ?
            // ?   - Coalition (Int32)        ?   4                      ?
            // ?   - Seat (Int32)             ?   4                      ?
            // ?   - AllowRecord (Boolean)    ?   1                      ?
            // ???????????????????????????????????????????????????????????
            // ? Position (struct)            ? 24                       ?
            // ?   - Latitude (Double)        ?   8                      ?
            // ?   - Longitude (Double)       ?   8                      ?
            // ?   - Altitude (Double)        ?   8                      ?
            // ???????????????????????????????????????????????????????????
            // ? AircraftInfo                 ? ~50                      ?
            // ?   - UnitType length + string ?   ~40-45                 ?
            // ?   - UnitId (UInt32)          ?   4                      ?
            // ???????????????????????????????????????????????????????????
            // ? Audio Payload                ? 1390-1490 (avg: 1440)    ?
            // ?   - Length prefix (Int32)    ?   4                      ?
            // ?   - Audio data               ?   1440 ± 50              ?
            // ???????????????????????????????????????????????????????????
            // ? Coalition (Int32)            ? 4                        ?
            // ???????????????????????????????????????????????????????????
            //
            // TOTAL SIZE PER PACKET:
            //   Minimum: 48 + 150 + 24 + 50 + 4 + 1390 + 4 = ~1670 bytes
            //   Average: 48 + 150 + 24 + 50 + 4 + 1440 + 4 = ~1720 bytes
            //   Maximum: 48 + 150 + 24 + 50 + 4 + 1490 + 4 = ~1770 bytes
            //
            // FILE SIZE CALCULATION:
            //   100MB target ÷ 1720 bytes/packet = ~58,140 packets
            //   With ±3.5% variance: 95MB - 105MB (±5% final size)
            
            const int avgPacketSize = 1720; // Accurate estimate based on actual packet structure
            long targetBytes = targetSizeMB * 1024L * 1024L;
            int estimatedPackets = (int)(targetBytes / avgPacketSize);

            await GeneratePacketsAsync(writer, estimatedPackets, packetIntervalMs, cancellationToken, progress);
            
            return tempFile;
        }

        /// <summary>
        /// Generates a synthetic recording file with an exact number of packets
        /// </summary>
        public static async Task<string> GenerateWithPacketCountAsync(
            int packetCount,
            int packetIntervalMs = 40,
            CancellationToken cancellationToken = default,
            IProgress<int>? progress = null)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"synthetic_{Guid.NewGuid()}.adb");
            
            await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new BinaryWriter(fs);

            // Write header
            writer.Write(Constants.RECORDING_FILE_MAGIC);
            writer.Write("192.168.1.100");
            writer.Write(5002);
            writer.Write(DateTime.UtcNow.Ticks);

            await GeneratePacketsAsync(writer, packetCount, packetIntervalMs, cancellationToken, progress);
            
            return tempFile;
        }

        private static async Task GeneratePacketsAsync(
            BinaryWriter writer,
            int packetCount,
            int packetIntervalMs,
            CancellationToken cancellationToken,
            IProgress<int>? progress = null)
        {
            var startTime = DateTime.UtcNow;
            var frequencies = new[] { 251_000_000.0, 243_000_000.0, 305_000_000.0 }; // VHF AM frequencies
            var players = new[] { "Viper-1", "Enfield-2-1", "Uzi-1", "Hawg-1-1" };

            for (int i = 0; i < packetCount && !cancellationToken.IsCancellationRequested; i++)
            {
                var timestamp = startTime.AddMilliseconds(i * packetIntervalMs);
                var frequency = frequencies[i % frequencies.Length];
                var playerName = players[i % players.Length];
                var guid = $"player-{i % players.Length:D4}";

                // Generate synthetic audio packet with reduced variance for predictable file sizes
                // Reduced from ±200 (±14%) to ±50 (±3.5%) for better consistency
                // This ensures generated files are within ±5% of target size
                var audioLength = 1440 + _random.Next(-50, 50); // 1390-1490 bytes, centered around 1440
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

                // Report progress and yield periodically
                if (i % 1000 == 0)
                {
                    progress?.Report(i);
                    await Task.Yield();
                }
            }
            
            // Report completion
            progress?.Report(packetCount);
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
