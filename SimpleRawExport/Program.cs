using System.Text;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using NAudio.Wave;
using NAudio.Lame;

namespace SimpleRawExport
{
    class Program
    {
        const int SAMPLE_RATE = 48000; // SRS standard sample rate
        const int FRAME_SIZE_MS = 40;  // 40ms frames
        const int SAMPLES_PER_FRAME = SAMPLE_RATE * FRAME_SIZE_MS / 1000; // 1920 samples

        static void Main(string[] args)
        {
            Console.WriteLine("Simple SRS Raw Export - Decode and Export to MP3");
            Console.WriteLine("=================================================");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: SimpleRawExport <input-file> [output-file]");
                Console.WriteLine("  input-file: Path to the .raw recording file");
                Console.WriteLine("  output-file: (Optional) Path for output MP3 file");
                Console.WriteLine();
                Console.WriteLine("If output-file is not specified, it will be created in the same directory");
                Console.WriteLine("as the input file with the same name but .mp3 extension.");
                return;
            }

            string inputFile = args[0];
            string outputFile = args.Length > 1 
                ? args[1] 
                : Path.ChangeExtension(inputFile, ".mp3");

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file not found: {inputFile}");
                return;
            }

            Console.WriteLine($"Input file: {inputFile}");
            Console.WriteLine($"Output file: {outputFile}");
            Console.WriteLine();

            try
            {
                ProcessFile(inputFile, outputFile);
                Console.WriteLine();
                Console.WriteLine("Export completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Details: {ex}");
            }
        }

        static void ProcessFile(string inputFile, string outputFile)
        {
            Console.WriteLine("Reading packets from file...");
            var packets = ReadPackets(inputFile);
            
            if (packets.Count == 0)
            {
                Console.WriteLine("No packets found in file.");
                return;
            }

            Console.WriteLine($"Found {packets.Count} packets");
            
            Console.WriteLine("Decoding audio...");
            var audioData = DecodeAllPackets(packets);
            
            Console.WriteLine($"Decoded {audioData.Length} audio samples");
            
            Console.WriteLine("Exporting to MP3...");
            ExportToMp3(audioData, outputFile);
        }

        static List<PacketData> ReadPackets(string filePath)
        {
            var packets = new List<PacketData>();
            
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            int count = 0;
            while (fs.Position < fs.Length)
            {
                try
                {
                    if (TryReadPacket(br, out var packet))
                    {
                        packets.Add(packet);
                        count++;
                        
                        if (count % 100 == 0)
                        {
                            Console.Write($"\rRead {count} packets...");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    // Skip corrupted packet
                    if (fs.Position < fs.Length - 100)
                    {
                        fs.Position += 10;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            
            Console.WriteLine($"\rRead {count} packets    ");
            return packets.OrderBy(p => p.Timestamp).ToList();
        }

        static bool TryReadPacket(BinaryReader reader, out PacketData packet)
        {
            packet = default;
            try
            {
                long startPosition = reader.BaseStream.Position;
                
                // Read basic metadata
                long ticks = reader.ReadInt64();
                double frequency = reader.ReadDouble();
                byte modulation = reader.ReadByte();
                byte encryption = reader.ReadByte();
                uint transmitterUnitId = reader.ReadUInt32();
                ulong packetId = reader.ReadUInt64();
                byte[] guidBytes = reader.ReadBytes(22);
                string transmitterGuid = Encoding.ASCII.GetString(guidBytes).TrimEnd('\0');

                // Try to read enhanced player data (new format)
                try
                {
                    // Try reading name length to detect new format
                    int nameLength = reader.ReadInt32();
                    if (nameLength >= 0 && nameLength < 1000)
                    {
                        // This looks like new format, skip enhanced player data
                        if (nameLength > 0)
                            reader.ReadBytes(nameLength); // name
                        
                        int guidLength = reader.ReadInt32();
                        if (guidLength > 0)
                            reader.ReadBytes(guidLength); // guid
                        
                        reader.ReadInt32(); // coalition
                        reader.ReadInt32(); // seat
                        reader.ReadBoolean(); // allowRecord
                        
                        // Position data
                        reader.ReadDouble(); // latitude
                        reader.ReadDouble(); // longitude
                        reader.ReadDouble(); // altitude
                        
                        // Aircraft info
                        int unitTypeLength = reader.ReadInt32();
                        if (unitTypeLength > 0)
                            reader.ReadBytes(unitTypeLength);
                        reader.ReadUInt32(); // unitId
                    }
                    else
                    {
                        // This is legacy format, rewind
                        reader.BaseStream.Position = startPosition + 8 + 8 + 1 + 1 + 4 + 8 + 22;
                    }
                }
                catch
                {
                    // Legacy format, reset position
                    reader.BaseStream.Position = startPosition + 8 + 8 + 1 + 1 + 4 + 8 + 22;
                }

                // Read audio payload
                int audioLength = reader.ReadInt32();
                byte[] audioPayload = audioLength > 0 ? reader.ReadBytes(audioLength) : Array.Empty<byte>();
                
                // Read coalition
                int coalition = reader.ReadInt32();

                packet = new PacketData
                {
                    Timestamp = new DateTime(ticks, DateTimeKind.Utc),
                    Frequency = frequency,
                    AudioPayload = audioPayload
                };
                
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static float[] DecodeAllPackets(List<PacketData> packets)
        {
            if (packets.Count == 0)
                return Array.Empty<float>();

            // Create Opus decoder
            using var decoder = OpusDecoder.Create(SAMPLE_RATE, 1); // 48kHz, mono
            decoder.ForwardErrorCorrection = false;
            
            var firstPacket = packets[0];
            var lastPacket = packets[^1];
            var totalDuration = (lastPacket.Timestamp - firstPacket.Timestamp).TotalSeconds;
            
            // Add a bit extra for the last packet
            totalDuration += 0.1; // 100ms extra
            
            var totalSamples = (int)Math.Ceiling(totalDuration * SAMPLE_RATE);
            var mixBuffer = new float[totalSamples];
            
            int processed = 0;
            foreach (var packet in packets)
            {
                try
                {
                    // Skip empty packets
                    if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                        continue;

                    // Decode Opus to PCM
                    var decodedSamples = new float[SAMPLES_PER_FRAME];
                    int samplesDecoded = decoder.DecodeFloat(packet.AudioPayload, decodedSamples.AsMemory(), false);
                    
                    if (samplesDecoded > 0)
                    {
                        // Calculate position in mix buffer
                        var offsetSeconds = (packet.Timestamp - firstPacket.Timestamp).TotalSeconds;
                        var offsetSamples = (int)Math.Round(offsetSeconds * SAMPLE_RATE);
                        
                        if (offsetSamples >= 0)
                        {
                            // Mix into buffer
                            for (int i = 0; i < samplesDecoded && (offsetSamples + i) < mixBuffer.Length; i++)
                            {
                                mixBuffer[offsetSamples + i] += decodedSamples[i];
                            }
                        }
                    }
                    
                    processed++;
                    if (processed % 100 == 0)
                    {
                        Console.Write($"\rDecoded {processed}/{packets.Count} packets...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\rWarning: Failed to decode packet {processed}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"\rDecoded {processed}/{packets.Count} packets    ");
            
            // Normalize audio
            float max = 0f;
            for (int i = 0; i < mixBuffer.Length; i++)
            {
                var abs = Math.Abs(mixBuffer[i]);
                if (abs > max) max = abs;
            }
            
            if (max > 1.0f)
            {
                Console.WriteLine($"Normalizing audio (peak: {max:F2})...");
                var scale = 0.95f / max; // Leave a bit of headroom
                for (int i = 0; i < mixBuffer.Length; i++)
                {
                    mixBuffer[i] = Math.Clamp(mixBuffer[i] * scale, -1.0f, 1.0f);
                }
            }
            
            return mixBuffer;
        }

        static void ExportToMp3(float[] audioData, string outputFile)
        {
            // Convert float to PCM16
            var pcmData = new byte[audioData.Length * 2];
            for (int i = 0; i < audioData.Length; i++)
            {
                var sample = (short)(audioData[i] * 32767f);
                pcmData[i * 2] = (byte)(sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            
            // Create wave provider
            var waveFormat = new WaveFormat(SAMPLE_RATE, 16, 1);
            var waveProvider = new RawSourceWaveStream(new MemoryStream(pcmData), waveFormat);
            
            // Export to MP3
            using var mp3Writer = new LameMP3FileWriter(outputFile, waveFormat, LAMEPreset.STANDARD);
            waveProvider.CopyTo(mp3Writer);
            
            Console.WriteLine($"Written {outputFile} ({pcmData.Length / 1024.0 / 1024.0:F2} MB raw, {new FileInfo(outputFile).Length / 1024.0 / 1024.0:F2} MB MP3)");
        }

        // Simple packet data structure
        struct PacketData
        {
            public DateTime Timestamp;
            public double Frequency;
            public byte[] AudioPayload;
        }
    }
}
