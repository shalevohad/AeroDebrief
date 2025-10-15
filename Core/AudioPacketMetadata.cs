using System;
using System.IO;
using System.Text;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public record AudioPacketMetadata(
        DateTime Timestamp,
        double Frequency,
        byte Modulation,
        byte Encryption,
        uint TransmitterUnitId,
        ulong PacketId,
        string TransmitterGuid,
        PlayerInfo PlayerData, // Enhanced player information
        int SampleRate,
        int ChannelCount,
        int Coalition,
        byte[] AudioPayload
    )
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool TryWriteMetadata(BinaryWriter writer)
        {
            try
            {
                writer.Write(Timestamp.Ticks);
                writer.Write(Frequency);
                writer.Write(Modulation);
                writer.Write(Encryption);
                writer.Write(TransmitterUnitId);
                writer.Write(PacketId);

                var guidBytes = Encoding.ASCII.GetBytes(TransmitterGuid ?? string.Empty);
                Array.Resize(ref guidBytes, 22);
                writer.Write(guidBytes);

                // Write comprehensive player data
                PlayerData?.WriteToStream(writer);

                writer.Write(AudioPayload?.Length ?? 0);
                if (AudioPayload != null && AudioPayload.Length > 0)
                    writer.Write(AudioPayload);

                writer.Write(Coalition);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during AudioPacketMetadata serialization.");
                return false;
            }
        }

        public static bool TryReadMetadata(BinaryReader reader, out AudioPacketMetadata? metadata)
        {
            metadata = null;
            try
            {
                long startPosition = reader.BaseStream.Position;
                
                long ticks = reader.ReadInt64();
                double frequency = reader.ReadDouble();
                byte modulation = reader.ReadByte();
                byte encryption = reader.ReadByte();
                uint transmitterUnitId = reader.ReadUInt32();
                ulong packetId = reader.ReadUInt64();
                byte[] guidBytes = reader.ReadBytes(22);
                string transmitterGuid = Encoding.ASCII.GetString(guidBytes).TrimEnd('\0');

                // Try to read enhanced player data (new format)
                PlayerInfo? playerData = null;
                try
                {
                    if (PlayerInfo.TryReadFromStream(reader, out playerData))
                    {
                        // Continue with new format
                        int audioLength = reader.ReadInt32();
                        byte[] audioPayload = audioLength > 0 ? reader.ReadBytes(audioLength) : Array.Empty<byte>();
                        int coalition = reader.ReadInt32();

                        metadata = new AudioPacketMetadata(
                            new DateTime(ticks, DateTimeKind.Utc),
                            frequency,
                            modulation,
                            encryption,
                            transmitterUnitId,
                            packetId,
                            transmitterGuid,
                            playerData,
                            48000,
                            1,
                            coalition,
                            audioPayload
                        );
                        return true;
                    }
                    else
                    {
                        // This might be legacy format, reset and try legacy read
                        reader.BaseStream.Position = startPosition + 8 + 8 + 1 + 1 + 4 + 8 + 22; // Reset to after GUID
                    }
                }
                catch
                {
                    // This is likely legacy format, reset position and continue with legacy read
                    reader.BaseStream.Position = startPosition + 8 + 8 + 1 + 1 + 4 + 8 + 22; // Reset to after GUID
                }

                // Legacy format - no enhanced player data stored
                int legacyAudioLength = reader.ReadInt32();
                byte[] legacyAudioPayload = legacyAudioLength > 0 ? reader.ReadBytes(legacyAudioLength) : Array.Empty<byte>();
                int legacyCoalition = reader.ReadInt32();

                // Create basic player info from GUID for legacy files
                var legacyPlayerInfo = new PlayerInfo
                {
                    Name = transmitterGuid,
                    TransmitterGuid = transmitterGuid,
                    Coalition = legacyCoalition,
                    Seat = -1,
                    AllowRecord = true,
                    Position = new Position(),
                    AircraftInfo = new AircraftInfo()
                };

                metadata = new AudioPacketMetadata(
                    new DateTime(ticks, DateTimeKind.Utc),
                    frequency,
                    modulation,
                    encryption,
                    transmitterUnitId,
                    packetId,
                    transmitterGuid,
                    legacyPlayerInfo,
                    48000,
                    1,
                    legacyCoalition,
                    legacyAudioPayload
                );
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during AudioPacketMetadata deserialization.");
                return false;
            }
        }
    }

    // Comprehensive player information structure
    public class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public int Coalition { get; set; }
        public int Seat { get; set; }
        public bool AllowRecord { get; set; }
        public Position Position { get; set; } = new();
        public AircraftInfo AircraftInfo { get; set; } = new();

        public void WriteToStream(BinaryWriter writer)
        {
            // Write player name with length prefix
            var nameBytes = Encoding.UTF8.GetBytes(Name ?? string.Empty);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);

            // Write transmitter GUID with length prefix
            var guidBytes = Encoding.UTF8.GetBytes(TransmitterGuid ?? string.Empty);
            writer.Write(guidBytes.Length);
            writer.Write(guidBytes);

            writer.Write(Coalition);
            writer.Write(Seat);
            writer.Write(AllowRecord);

            // Write position data
            Position?.WriteToStream(writer);

            // Write aircraft info
            AircraftInfo?.WriteToStream(writer);
        }

        public static bool TryReadFromStream(BinaryReader reader, out PlayerInfo? playerInfo)
        {
            playerInfo = null;
            try
            {
                // Read player name
                int nameLength = reader.ReadInt32();
                if (nameLength < 0 || nameLength > 1000) return false; // Sanity check
                
                string name = string.Empty;
                if (nameLength > 0)
                {
                    byte[] nameBytes = reader.ReadBytes(nameLength);
                    name = Encoding.UTF8.GetString(nameBytes);
                }

                // Read transmitter GUID
                int guidLength = reader.ReadInt32();
                if (guidLength < 0 || guidLength > 1000) return false; // Sanity check
                
                string transmitterGuid = string.Empty;
                if (guidLength > 0)
                {
                    byte[] guidBytes = reader.ReadBytes(guidLength);
                    transmitterGuid = Encoding.UTF8.GetString(guidBytes);
                }

                int coalition = reader.ReadInt32();
                int seat = reader.ReadInt32();
                bool allowRecord = reader.ReadBoolean();

                // Read position data
                if (!Position.TryReadFromStream(reader, out var position))
                    return false;

                // Read aircraft info
                if (!AircraftInfo.TryReadFromStream(reader, out var aircraftInfo))
                    return false;

                playerInfo = new PlayerInfo
                {
                    Name = name,
                    TransmitterGuid = transmitterGuid,
                    Coalition = coalition,
                    Seat = seat,
                    AllowRecord = allowRecord,
                    Position = position ?? new Position(),
                    AircraftInfo = aircraftInfo ?? new AircraftInfo()
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetCoalitionName()
        {
            return Coalition switch
            {
                1 => "Red",
                2 => "Blue",
                _ => "Spectator"
            };
        }

        public string GetDisplayName()
        {
            return !string.IsNullOrEmpty(Name) && Name != TransmitterGuid 
                ? Name 
                : $"Unknown ({TransmitterGuid})";
        }
    }

    // Position information
    public class Position
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }

        public void WriteToStream(BinaryWriter writer)
        {
            writer.Write(Latitude);
            writer.Write(Longitude);
            writer.Write(Altitude);
        }

        public static bool TryReadFromStream(BinaryReader reader, out Position? position)
        {
            position = null;
            try
            {
                double latitude = reader.ReadDouble();
                double longitude = reader.ReadDouble();
                double altitude = reader.ReadDouble();

                position = new Position
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Altitude = altitude
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsValid()
        {
            return Latitude != 0 && Longitude != 0;
        }

        public override string ToString()
        {
            return IsValid() ? $"Lat: {Latitude:F5}, Lng: {Longitude:F5}, Alt: {Altitude:F0}m" : "Unknown Position";
        }
    }

    // Aircraft/Unit information
    public class AircraftInfo
    {
        public string UnitType { get; set; } = string.Empty;
        public uint UnitId { get; set; }

        public void WriteToStream(BinaryWriter writer)
        {
            // Write unit type with length prefix
            var unitTypeBytes = Encoding.UTF8.GetBytes(UnitType ?? string.Empty);
            writer.Write(unitTypeBytes.Length);
            writer.Write(unitTypeBytes);

            writer.Write(UnitId);
        }

        public static bool TryReadFromStream(BinaryReader reader, out AircraftInfo? aircraftInfo)
        {
            aircraftInfo = null;
            try
            {
                // Read unit type
                int unitTypeLength = reader.ReadInt32();
                if (unitTypeLength < 0 || unitTypeLength > 1000) return false; // Sanity check
                
                string unitType = string.Empty;
                if (unitTypeLength > 0)
                {
                    byte[] unitTypeBytes = reader.ReadBytes(unitTypeLength);
                    unitType = Encoding.UTF8.GetString(unitTypeBytes);
                }

                uint unitId = reader.ReadUInt32();

                aircraftInfo = new AircraftInfo
                {
                    UnitType = unitType,
                    UnitId = unitId
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(UnitType) ? $"{UnitType} (ID: {UnitId})" : $"Unknown Aircraft (ID: {UnitId})";
        }
    }
}