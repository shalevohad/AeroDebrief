using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace AeroDebrief.Core
{
    /// <summary>
    /// Optimized audio packet metadata with efficient serialization.
    /// Uses fixed-size buffers, Buffer.BlockCopy, and minimal allocations.
    /// </summary>
    public record AudioPacketMetadata(
        DateTime Timestamp,
        double Frequency,
        byte Modulation,
        byte Encryption,
        uint TransmitterUnitId,
        ulong PacketId,
        string TransmitterGuid,
        PlayerInfo PlayerData,
        int SampleRate,
        int ChannelCount,
        int Coalition,
        byte[] AudioPayload
    )
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Fixed-size constants
        public const int GuidLength = 22;
        public const int FixedHeaderLength = 
            sizeof(long) +    // Timestamp.Ticks
            sizeof(double) +  // Frequency
            sizeof(byte) +    // Modulation
            sizeof(byte) +    // Encryption
            sizeof(uint) +    // TransmitterUnitId
            sizeof(ulong) +   // PacketId
            GuidLength;       // TransmitterGuid (fixed ASCII)

        /// <summary>
        /// Optimized write using Buffer.BlockCopy for performance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool TryWriteMetadata(BinaryWriter writer)
        {
            try
            {
                // Write fixed header
                writer.Write(Timestamp.Ticks);
                writer.Write(Frequency);
                writer.Write(Modulation);
                writer.Write(Encryption);
                writer.Write(TransmitterUnitId);
                writer.Write(PacketId);

                // Fixed-size GUID
                Span<byte> guidBuffer = stackalloc byte[GuidLength];
                var guidBytes = Encoding.ASCII.GetBytes(TransmitterGuid ?? string.Empty);
                var copyLength = Math.Min(guidBytes.Length, GuidLength);
                guidBytes.AsSpan(0, copyLength).CopyTo(guidBuffer);
                guidBuffer.Slice(copyLength).Clear();
                writer.Write(guidBuffer);

                // Variable player data
                PlayerData?.WriteToStream(writer);

                // Audio payload with length prefix
                writer.Write(AudioPayload?.Length ?? 0);
                if (AudioPayload != null && AudioPayload.Length > 0)
                    writer.Write(AudioPayload);

                writer.Write(Coalition);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write audio packet metadata");
                return false;
            }
        }

        /// <summary>
        /// Optimized read with minimal allocations and robust error handling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool TryReadMetadata(BinaryReader reader, out AudioPacketMetadata? metadata)
        {
            metadata = null;
            
            // Save stream position for recovery on error
            long startPosition = -1;
            try
            {
                if (reader.BaseStream.CanSeek)
                {
                    startPosition = reader.BaseStream.Position;
                    
                    // Check if we have enough bytes remaining for the fixed header
                    if (reader.BaseStream.Length - startPosition < FixedHeaderLength)
                    {
                        return false; // Not enough data - this is normal at EOF
                    }
                }
                
                // Read fixed header
                long ticks = reader.ReadInt64();
                
                // Validate timestamp - must be reasonable (between year 2000 and 2100)
                if (ticks < Constants.MinValidTimestamp.Ticks || ticks > Constants.MaxValidTimestamp.Ticks)
                {
                    return false;
                }
                
                double frequency = reader.ReadDouble();
                
                // Validate frequency - must be reasonable (typically 30 MHz to 400 MHz for radios)
                if (frequency < Constants.MinValidFrequencyHz || frequency > Constants.MaxValidFrequencyHz || double.IsNaN(frequency) || double.IsInfinity(frequency))
                {
                    return false;
                }
                
                byte modulation = reader.ReadByte();
                byte encryption = reader.ReadByte();
                uint transmitterUnitId = reader.ReadUInt32();
                ulong packetId = reader.ReadUInt64();
                
                // Fixed-size GUID read
                byte[] guidBytes = reader.ReadBytes(GuidLength);
                if (guidBytes.Length != GuidLength)
                {
                    return false; // Couldn't read GUID - likely corrupted or EOF
                }
                string transmitterGuid = Encoding.ASCII.GetString(guidBytes).TrimEnd('\0');

                PlayerInfo? playerData = null;
                try
                {
                    if (PlayerInfo.TryReadFromStream(reader, out playerData))
                    {
                        int audioLength = reader.ReadInt32();
                        
                        // Validate audio payload length
                        if (audioLength < 0 || audioLength > Constants.MaxAudioPayloadBytes)
                        {
                            return false;
                        }
                        
                        // Early exit if there's not enough data remaining
                        if (reader.BaseStream.CanSeek)
                        {
                            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
                            if (remaining < audioLength + sizeof(int))
                            {
                                return false; // Not enough data for payload + coalition
                            }
                        }
                        
                        byte[] audioPayload = audioLength > 0 ? reader.ReadBytes(audioLength) : Array.Empty<byte>();
                        
                        if (audioPayload.Length != audioLength)
                        {
                            return false; // Couldn't read full payload
                        }
                        
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
                }
                catch (Exception ex)
                {
                    // Try to recover stream position and read as legacy format
                    if (reader.BaseStream.CanSeek && startPosition >= 0)
                    {
                        try
                        {
                            // Reset to after fixed header + GUID to try legacy format
                            reader.BaseStream.Position = startPosition + FixedHeaderLength;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                // Legacy format - no player data
                int legacyAudioLength = reader.ReadInt32();
                
                if (legacyAudioLength < 0 || legacyAudioLength > Constants.MaxAudioPayloadBytes)
                {
                    return false;
                }
                
                // Early exit if there's not enough data remaining
                if (reader.BaseStream.CanSeek)
                {
                    var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
                    if (remaining < legacyAudioLength + sizeof(int))
                    {
                        return false; // Not enough data for payload + coalition
                    }
                }
                
                byte[] legacyAudioPayload = legacyAudioLength > 0 ? reader.ReadBytes(legacyAudioLength) : Array.Empty<byte>();
                
                if (legacyAudioPayload.Length != legacyAudioLength)
                {
                    return false; // Couldn't read full payload
                }
                
                int legacyCoalition = reader.ReadInt32();

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
                return false; // Normal EOF condition
            }
            catch (ArgumentOutOfRangeException)
            {
                // Corrupted data - handled by caller's error recovery
                return false;
            }
            catch (Exception)
            {
                // Unexpected error - handled by caller's error recovery
                return false;
            }
        }

        /// <summary>
        /// Calculates total packet size for pre-allocation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculatePacketSize()
        {
            return FixedHeaderLength + 
                   (PlayerData?.CalculateSize() ?? 0) + 
                   sizeof(int) + 
                   (AudioPayload?.Length ?? 0) +
                   sizeof(int);
        }
    }

    /// <summary>
    /// Optimized player info with size calculation for efficient allocation
    /// </summary>
    public class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TransmitterGuid { get; set; } = string.Empty;
        public int Coalition { get; set; }
        public int Seat { get; set; }
        public bool AllowRecord { get; set; }
        public Position Position { get; set; } = new();
        public AircraftInfo AircraftInfo { get; set; } = new();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void WriteToStream(BinaryWriter writer)
        {
            var nameBytes = Encoding.UTF8.GetBytes(Name ?? string.Empty);
            writer.Write(nameBytes.Length);
            if (nameBytes.Length > 0)
                writer.Write(nameBytes);

            var guidBytes = Encoding.UTF8.GetBytes(TransmitterGuid ?? string.Empty);
            writer.Write(guidBytes.Length);
            if (guidBytes.Length > 0)
                writer.Write(guidBytes);

            writer.Write(Coalition);
            writer.Write(Seat);
            writer.Write(AllowRecord);

            Position.WriteToStream(writer);
            AircraftInfo?.WriteToStream(writer);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool TryReadFromStream(BinaryReader reader, out PlayerInfo? playerInfo)
        {
            playerInfo = null;
            try
            {
                int nameLength = reader.ReadInt32();
                if (nameLength < 0 || nameLength > Constants.MaxStringLength) return false;
                
                string name = string.Empty;
                if (nameLength > 0)
                {
                    byte[] nameBytes = reader.ReadBytes(nameLength);
                    name = Encoding.UTF8.GetString(nameBytes);
                }

                int guidLength = reader.ReadInt32();
                if (guidLength < 0 || guidLength > Constants.MaxStringLength) return false;
                
                string transmitterGuid = string.Empty;
                if (guidLength > 0)
                {
                    byte[] guidBytes = reader.ReadBytes(guidLength);
                    transmitterGuid = Encoding.UTF8.GetString(guidBytes);
                }

                int coalition = reader.ReadInt32();
                int seat = reader.ReadInt32();
                bool allowRecord = reader.ReadBoolean();

                if (!Position.TryReadFromStream(reader, out var position))
                    return false;

                if (!AircraftInfo.TryReadFromStream(reader, out var aircraftInfo))
                    return false;

                playerInfo = new PlayerInfo
                {
                    Name = name,
                    TransmitterGuid = transmitterGuid,
                    Coalition = coalition,
                    Seat = seat,
                    AllowRecord = allowRecord,
                    Position = position,
                    AircraftInfo = aircraftInfo ?? new AircraftInfo()
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculateSize()
        {
            return sizeof(int) + Encoding.UTF8.GetByteCount(Name ?? string.Empty) +
                   sizeof(int) + Encoding.UTF8.GetByteCount(TransmitterGuid ?? string.Empty) +
                   sizeof(int) + sizeof(int) + sizeof(bool) +
                   Position.FixedSize + AircraftInfo.CalculateSize();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetCoalitionName()
        {
            return Coalition switch
            {
                1 => "Red",
                2 => "Blue",
                _ => "Spectator"
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetDisplayName()
        {
            return !string.IsNullOrEmpty(Name) && Name != TransmitterGuid 
                ? Name 
                : $"Unknown ({TransmitterGuid})";
        }
    }

    /// <summary>
    /// Fixed-size position struct for optimal performance
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Position
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;

        public const int FixedSize = sizeof(double) * 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToStream(BinaryWriter writer)
        {
            writer.Write(Latitude);
            writer.Write(Longitude);
            writer.Write(Altitude);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool TryReadFromStream(BinaryReader reader, out Position position)
        {
            position = default;
            try
            {
                position.Latitude = reader.ReadDouble();
                position.Longitude = reader.ReadDouble();
                position.Altitude = reader.ReadDouble();
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValid()
        {
            return Latitude != 0 && Longitude != 0;
        }

        public readonly override string ToString()
        {
            return IsValid() ? $"Lat: {Latitude:F5}, Lng: {Longitude:F5}, Alt: {Altitude:F0}m" : "Unknown Position";
        }
    }

    /// <summary>
    /// Aircraft info with size calculation
    /// </summary>
    public class AircraftInfo
    {
        public string UnitType { get; set; } = string.Empty;
        public uint UnitId { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void WriteToStream(BinaryWriter writer)
        {
            var unitTypeBytes = Encoding.UTF8.GetBytes(UnitType ?? string.Empty);
            writer.Write(unitTypeBytes.Length);
            if (unitTypeBytes.Length > 0)
                writer.Write(unitTypeBytes);

            writer.Write(UnitId);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool TryReadFromStream(BinaryReader reader, out AircraftInfo? aircraftInfo)
        {
            aircraftInfo = null;
            try
            {
                int unitTypeLength = reader.ReadInt32();
                if (unitTypeLength < 0 || unitTypeLength > Constants.MaxStringLength) return false;
                
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculateSize()
        {
            return sizeof(int) + Encoding.UTF8.GetByteCount(UnitType ?? string.Empty) + sizeof(uint);
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(UnitType) ? $"{UnitType} (ID: {UnitId})" : $"Unknown Aircraft (ID: {UnitId})";
        }
    }
}