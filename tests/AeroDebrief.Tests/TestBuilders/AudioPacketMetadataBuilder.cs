using AeroDebrief.Core;

namespace AeroDebrief.Tests.TestBuilders
{
    /// <summary>
    /// Builder for creating AudioPacketMetadata test objects
    /// </summary>
    public class AudioPacketMetadataBuilder
    {
        private DateTime _timestamp = DateTime.UtcNow;
        private double _frequency = 251_000_000; // 251 MHz default
        private byte _modulation = 0; // AM
        private byte _encryption = 0; // No encryption
        private uint _transmitterUnitId = 1000;
        private ulong _packetId = 1;
        private string _transmitterGuid = Guid.NewGuid().ToString();
        private PlayerInfo _playerData = new PlayerInfoBuilder().Build();
        private int _sampleRate = 48000;
        private int _channelCount = 1;
        private int _coalition = 2; // Blue
        private byte[] _audioPayload = Array.Empty<byte>();

        public AudioPacketMetadataBuilder WithTimestamp(DateTime timestamp)
        {
            _timestamp = timestamp;
            return this;
        }

        public AudioPacketMetadataBuilder WithFrequency(double frequency)
        {
            _frequency = frequency;
            return this;
        }

        /// <summary>
        /// Sets frequency in MHz (e.g., 251.0 for 251 MHz)
        /// </summary>
        public AudioPacketMetadataBuilder WithFrequencyMHz(double frequencyMhz)
        {
            _frequency = frequencyMhz * 1_000_000;
            return this;
        }

        public AudioPacketMetadataBuilder WithModulation(byte modulation)
        {
            _modulation = modulation;
            return this;
        }

        public AudioPacketMetadataBuilder WithAMModulation()
        {
            _modulation = 0;
            return this;
        }

        public AudioPacketMetadataBuilder WithFMModulation()
        {
            _modulation = 1;
            return this;
        }

        public AudioPacketMetadataBuilder WithEncryption(byte encryption)
        {
            _encryption = encryption;
            return this;
        }

        public AudioPacketMetadataBuilder WithTransmitterUnitId(uint unitId)
        {
            _transmitterUnitId = unitId;
            return this;
        }

        public AudioPacketMetadataBuilder WithPacketId(ulong packetId)
        {
            _packetId = packetId;
            return this;
        }

        public AudioPacketMetadataBuilder WithTransmitterGuid(string guid)
        {
            _transmitterGuid = guid;
            return this;
        }

        public AudioPacketMetadataBuilder WithPlayerData(PlayerInfo playerData)
        {
            _playerData = playerData;
            return this;
        }

        public AudioPacketMetadataBuilder WithPlayerData(Action<PlayerInfoBuilder> configure)
        {
            var builder = new PlayerInfoBuilder();
            configure(builder);
            _playerData = builder.Build();
            return this;
        }

        public AudioPacketMetadataBuilder WithSampleRate(int sampleRate)
        {
            _sampleRate = sampleRate;
            return this;
        }

        public AudioPacketMetadataBuilder WithChannelCount(int channelCount)
        {
            _channelCount = channelCount;
            return this;
        }

        public AudioPacketMetadataBuilder WithCoalition(int coalition)
        {
            _coalition = coalition;
            return this;
        }

        public AudioPacketMetadataBuilder WithRedCoalition()
        {
            _coalition = 1;
            return this;
        }

        public AudioPacketMetadataBuilder WithBlueCoalition()
        {
            _coalition = 2;
            return this;
        }

        public AudioPacketMetadataBuilder WithAudioPayload(byte[] audioPayload)
        {
            _audioPayload = audioPayload;
            return this;
        }

        /// <summary>
        /// Creates a test Opus audio payload (silence)
        /// </summary>
        public AudioPacketMetadataBuilder WithOpusSilence()
        {
            _audioPayload = new byte[] { 0xFC, 0x00 }; // Minimal Opus silence frame
            return this;
        }

        /// <summary>
        /// Creates a test PCM audio payload (1 second of silence)
        /// </summary>
        public AudioPacketMetadataBuilder WithPcmSilence()
        {
            _audioPayload = new byte[_sampleRate * 2]; // 1 second of 16-bit PCM silence
            return this;
        }

        /// <summary>
        /// Creates a test PCM audio payload with a simple tone
        /// </summary>
        public AudioPacketMetadataBuilder WithPcmTone(double frequency = 440.0, double durationSeconds = 0.02)
        {
            int sampleCount = (int)(_sampleRate * durationSeconds);
            var samples = new short[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                double time = (double)i / _sampleRate;
                samples[i] = (short)(8000 * Math.Sin(2 * Math.PI * frequency * time));
            }

            _audioPayload = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, _audioPayload, 0, _audioPayload.Length);
            return this;
        }

        public AudioPacketMetadata Build()
        {
            return new AudioPacketMetadata(
                _timestamp,
                _frequency,
                _modulation,
                _encryption,
                _transmitterUnitId,
                _packetId,
                _transmitterGuid,
                _playerData,
                _sampleRate,
                _channelCount,
                _coalition,
                _audioPayload
            );
        }
    }
}
