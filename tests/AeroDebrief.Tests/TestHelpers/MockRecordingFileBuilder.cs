using AeroDebrief.Core;
using AeroDebrief.Core.IO;
using AeroDebrief.Tests.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.TestHelpers
{
    /// <summary>
    /// Helper class to create mock AeroDebrief recording files for testing
    /// </summary>
    public class MockRecordingFileBuilder : IDisposable
    {
        private readonly string _filePath;
        private FileStream? _fileStream;
        private BinaryWriter? _writer;
        private bool _headerWritten;
        private int _packetCount;

        public string FilePath => _filePath;
        public int PacketCount => _packetCount;

        public MockRecordingFileBuilder(string? filePath = null)
        {
            _filePath = filePath ?? Path.GetTempFileName();
        }

        /// <summary>
        /// Writes the recording file header
        /// </summary>
        public MockRecordingFileBuilder WithHeader(string serverIp = "127.0.0.1", int serverPort = 5002, DateTime? startTime = null)
        {
            if (_headerWritten)
                throw new InvalidOperationException("Header already written");

            _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            _writer = new BinaryWriter(_fileStream);

            var start = startTime ?? DateTime.UtcNow;

            _writer.Write(Constants.RECORDING_FILE_MAGIC);
            _writer.Write(serverIp);
            _writer.Write(serverPort);
            _writer.Write(start.Ticks);

            _headerWritten = true;
            return this;
        }

        /// <summary>
        /// Writes a single audio packet to the file
        /// </summary>
        public MockRecordingFileBuilder WithPacket(
            DateTime? timestamp = null,
            double frequency = 251_000_000.0, // 251 MHz (common UHF freq)
            byte modulation = 0, // AM
            byte encryption = 0,
            uint transmitterUnitId = 1,
            ulong packetId = 1,
            string? transmitterGuid = null,
            string? playerName = null,
            int coalition = 2, // Blue
            byte[]? audioPayload = null,
            string? unitType = null,
            uint unitId = 0)
        {
            EnsureFileOpen();

            var meta = CreatePacketMetadata(
                timestamp ?? DateTime.UtcNow,
                frequency,
                modulation,
                encryption,
                transmitterUnitId,
                packetId,
                transmitterGuid ?? "TEST_GUID_001",
                playerName ?? "TestPilot",
                coalition,
                audioPayload ?? CreateDummyAudioPayload(),
                unitType ?? "F-16C_50",
                unitId == 0 ? transmitterUnitId : unitId
            );

            if (meta.TryWriteMetadata(_writer!))
            {
                _packetCount++;
            }
            else
            {
                throw new InvalidOperationException("Failed to write packet metadata");
            }

            return this;
        }

        /// <summary>
        /// Writes multiple packets with incrementing timestamps
        /// </summary>
        public MockRecordingFileBuilder WithPackets(
            int count,
            TimeSpan? interval = null,
            DateTime? startTime = null,
            double frequency = 251_000_000.0,
            string? playerName = null,
            int coalition = 2)
        {
            var time = startTime ?? DateTime.UtcNow;
            var delta = interval ?? TimeSpan.FromMilliseconds(40); // OPUS frame duration

            for (int i = 0; i < count; i++)
            {
                WithPacket(
                    timestamp: time,
                    frequency: frequency,
                    packetId: (ulong)(i + 1),
                    playerName: playerName,
                    coalition: coalition
                );
                time = time.Add(delta);
            }

            return this;
        }

        /// <summary>
        /// Writes packets from multiple players/frequencies
        /// </summary>
        public MockRecordingFileBuilder WithMultipleTransmitters(
            int packetsPerTransmitter = 10,
            params (string playerName, double frequency, int coalition)[] transmitters)
        {
            var time = DateTime.UtcNow;
            var delta = TimeSpan.FromMilliseconds(40);

            for (int i = 0; i < packetsPerTransmitter; i++)
            {
                foreach (var (playerName, frequency, coalition) in transmitters)
                {
                    var guid = $"GUID_{playerName}";
                    WithPacket(
                        timestamp: time,
                        frequency: frequency,
                        transmitterGuid: guid,
                        playerName: playerName,
                        coalition: coalition,
                        packetId: (ulong)(i + 1)
                    );
                    time = time.Add(delta);
                }
            }

            return this;
        }

        /// <summary>
        /// Creates a realistic conversation scenario with varying audio activity
        /// </summary>
        public MockRecordingFileBuilder WithConversation(
            string player1 = "Viper-1",
            string player2 = "Viper-2",
            double frequency = 251_000_000.0,
            int durationSeconds = 10)
        {
            var time = DateTime.UtcNow;
            var endTime = time.AddSeconds(durationSeconds);
            var random = new Random(42); // Deterministic for tests

            int packetIndex = 0;
            while (time < endTime)
            {
                // Alternate between players with some gaps
                var speaker = (packetIndex % 3) switch
                {
                    0 => player1,
                    1 => player2,
                    _ => null // Silence
                };

                if (speaker != null)
                {
                    var audioPayload = CreateDummyAudioPayload(random.Next(1000, 3000));
                    WithPacket(
                        timestamp: time,
                        frequency: frequency,
                        transmitterGuid: $"GUID_{speaker}",
                        playerName: speaker,
                        audioPayload: audioPayload,
                        packetId: (ulong)(packetIndex + 1)
                    );
                }

                time = time.Add(TimeSpan.FromMilliseconds(40));
                packetIndex++;
            }

            return this;
        }

        /// <summary>
        /// Builds and closes the file, returning the path
        /// </summary>
        public string Build()
        {
            Dispose();
            return _filePath;
        }

        public void Dispose()
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            _fileStream?.Dispose();
            _fileStream = null;
        }

        private void EnsureFileOpen()
        {
            if (_fileStream == null)
            {
                // Create file without header if not already created
                _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                _writer = new BinaryWriter(_fileStream);
            }
        }

        private static AudioPacketMetadata CreatePacketMetadata(
            DateTime timestamp,
            double frequency,
            byte modulation,
            byte encryption,
            uint transmitterUnitId,
            ulong packetId,
            string transmitterGuid,
            string playerName,
            int coalition,
            byte[] audioPayload,
            string unitType,
            uint unitId)
        {
            var playerInfo = new PlayerInfo
            {
                Name = playerName,
                TransmitterGuid = transmitterGuid,
                Coalition = coalition,
                Seat = 0,
                AllowRecord = true,
                Position = new Position
                {
                    Latitude = 45.0,
                    Longitude = -122.0,
                    Altitude = 10000
                },
                AircraftInfo = new AircraftInfo
                {
                    UnitType = unitType,
                    UnitId = unitId
                }
            };

            return new AudioPacketMetadata(
                timestamp,
                frequency,
                modulation,
                encryption,
                transmitterUnitId,
                packetId,
                transmitterGuid,
                playerInfo,
                Constants.OUTPUT_SAMPLE_RATE,
                1, // Mono
                coalition,
                audioPayload
            );
        }

        private static byte[] CreateDummyAudioPayload(int sampleCount = 1920)
        {
            // Create a simple sine wave for realistic audio data
            var payload = new byte[sampleCount * 2]; // 16-bit samples
            var frequency = 440.0; // A4 note
            var sampleRate = Constants.OUTPUT_SAMPLE_RATE;

            for (int i = 0; i < sampleCount; i++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 8000);
                var bytes = BitConverter.GetBytes(sample);
                payload[i * 2] = bytes[0];
                payload[i * 2 + 1] = bytes[1];
            }

            return payload;
        }

        /// <summary>
        /// Creates a minimal valid recording file for basic tests
        /// </summary>
        public static string CreateMinimalTestFile(string? filePath = null)
        {
            using var builder = new MockRecordingFileBuilder(filePath);
            return builder
                .WithHeader()
                .WithPackets(count: 10)
                .Build();
        }

        /// <summary>
        /// Creates a recording file with multiple frequencies and players
        /// </summary>
        public static string CreateMultiFrequencyTestFile(string? filePath = null)
        {
            using var builder = new MockRecordingFileBuilder(filePath);
            return builder
                .WithHeader()
                .WithMultipleTransmitters(
                    packetsPerTransmitter: 20,
                    ("Viper-1", 251_000_000.0, 2), // Blue, UHF
                    ("Enfield-1", 127_500_000.0, 1), // Red, VHF
                    ("Overlord", 305_000_000.0, 2)  // Blue, UHF
                )
                .Build();
        }

        /// <summary>
        /// Creates a recording file with a conversation scenario
        /// </summary>
        public static string CreateConversationTestFile(string? filePath = null, int durationSeconds = 10)
        {
            using var builder = new MockRecordingFileBuilder(filePath);
            return builder
                .WithHeader()
                .WithConversation(durationSeconds: durationSeconds)
                .Build();
        }

        /// <summary>
        /// Creates a mock audio processing engine for testing.
        /// This eliminates the need for real audio hardware and allows test verification.
        /// </summary>
        public static MockAudioProcessingEngine CreateMockAudioProcessingEngine()
        {
            var engine = new MockAudioProcessingEngine();
            engine.Initialize();
            return engine;
        }

        /// <summary>
        /// Creates a mock audio output engine for testing.
        /// This eliminates the need for real audio hardware and records all operations for verification.
        /// </summary>
        public static async Task<MockAudioOutputEngine> CreateMockAudioOutputEngineAsync()
        {
            var engine = new MockAudioOutputEngine();
            await engine.InitializeAsync();
            return engine;
        }
    }
}
