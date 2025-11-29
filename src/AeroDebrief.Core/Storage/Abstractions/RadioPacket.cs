using System;

namespace AeroDebrief.Core.Storage.Abstractions
{
    /// <summary>
    /// Radio packet data transfer object for storage operations.
    /// Represents a single audio transmission packet with metadata and player information.
    /// </summary>
    public class RadioPacket
    {
        /// <summary>
        /// Unique packet identifier
        /// </summary>
        public ulong PacketId { get; set; }

        /// <summary>
        /// UTC timestamp when packet was received
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Radio frequency in Hz
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Modulation type (0=AM, 1=FM, 2=INTERCOM, 3=DISABLED)
        /// </summary>
        public byte Modulation { get; set; }

        // Player Information
        /// <summary>
        /// Name of the player/transmitter
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Unique transmitter GUID
        /// </summary>
        public string TransmitterGuid { get; set; } = string.Empty;

        /// <summary>
        /// Transmitter unit ID
        /// </summary>
        public uint TransmitterUnitId { get; set; }

        /// <summary>
        /// Coalition (0=Spectator, 1=Red, 2=Blue)
        /// </summary>
        public byte Coalition { get; set; }

        /// <summary>
        /// Aircraft/unit type (e.g., "F-16C")
        /// </summary>
        public string? UnitType { get; set; }

        /// <summary>
        /// Unit ID (optional)
        /// </summary>
        public int? UnitId { get; set; }

        /// <summary>
        /// Full player information (for compatibility with AudioPacketMetadata)
        /// </summary>
        public PlayerInfo? PlayerData { get; set; }

        // Audio Data
        /// <summary>
        /// Raw audio data payload
        /// </summary>
        public byte[] AudioPayload { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Audio sample rate (typically 16000 or 48000)
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Encryption level
        /// </summary>
        public byte Encryption { get; set; }

        /// <summary>
        /// Number of audio channels
        /// </summary>
        public byte ChannelCount { get; set; }
        
        // Phase 2.1: Pre-computed amplitude data
        /// <summary>
        /// Pre-computed amplitude data (float32 array as byte BLOB)
        /// NULL for legacy recordings, populated for new recordings.
        /// Each float represents peak amplitude in a time window (linear 0.0-1.0 scale).
        /// </summary>
        public byte[]? AmplitudeData { get; set; }
        
        /// <summary>
        /// Time resolution of amplitude data in milliseconds (typically 5ms)
        /// NULL for legacy recordings without amplitude data.
        /// </summary>
        public int? AmplitudeResolutionMs { get; set; }

        // Computed Properties
        /// <summary>
        /// Human-readable coalition name
        /// </summary>
        public string CoalitionName => Coalition switch
        {
            0 => "Spectator",
            1 => "Red",
            2 => "Blue",
            _ => "Unknown"
        };

        /// <summary>
        /// Human-readable frequency (e.g., "251.000 MHz")
        /// </summary>
        public string FormattedFrequency => $"{Frequency / 1_000_000.0:F3} MHz";

        /// <summary>
        /// Human-readable modulation name
        /// </summary>
        public string ModulationName => Modulation switch
        {
            0 => "AM",
            1 => "FM",
            2 => "INTERCOM",
            3 => "DISABLED",
            _ => "UNKNOWN"
        };

        /// <summary>
        /// Convert RadioPacket to AudioPacketMetadata for audio processing
        /// </summary>
        public AudioPacketMetadata ToMetadata()
        {
            return new AudioPacketMetadata(
                Timestamp,
                Frequency,
                Modulation,
                Encryption,
                TransmitterUnitId,
                PacketId,
                TransmitterGuid,
                PlayerData ?? CreateDefaultPlayerInfo(),
                SampleRate,
                ChannelCount,
                Coalition,
                AudioPayload
            );
        }

        /// <summary>
        /// Convert AudioPacketMetadata to RadioPacket for storage
        /// </summary>
        public static RadioPacket FromMetadata(AudioPacketMetadata metadata)
        {
            return new RadioPacket
            {
                PacketId = metadata.PacketId,
                Timestamp = metadata.Timestamp,
                Frequency = metadata.Frequency,
                Modulation = metadata.Modulation,
                Encryption = metadata.Encryption,
                TransmitterUnitId = metadata.TransmitterUnitId,
                TransmitterGuid = metadata.TransmitterGuid,
                Coalition = (byte)metadata.Coalition,
                PlayerName = metadata.PlayerData?.Name ?? string.Empty,
                UnitType = metadata.PlayerData?.AircraftInfo?.UnitType,
                UnitId = (int?)metadata.PlayerData?.AircraftInfo?.UnitId,
                PlayerData = metadata.PlayerData,
                SampleRate = metadata.SampleRate,
                ChannelCount = (byte)metadata.ChannelCount,
                AudioPayload = metadata.AudioPayload
            };
        }

        /// <summary>
        /// Create default PlayerInfo when none is provided
        /// </summary>
        private PlayerInfo CreateDefaultPlayerInfo()
        {
            return new PlayerInfo
            {
                Name = PlayerName,
                TransmitterGuid = TransmitterGuid,
                Coalition = Coalition,
                Seat = -1,
                AllowRecord = true,
                Position = new Position(),
                AircraftInfo = new AircraftInfo
                {
                    UnitType = UnitType ?? string.Empty,
                    UnitId = (uint)(UnitId ?? 0)
                }
            };
        }
    }
}
