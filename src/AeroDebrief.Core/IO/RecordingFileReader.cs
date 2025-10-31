using NLog;
using System;
using System.Collections.Generic;
using System.IO;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// Centralized reader for AeroDebrief recording files (.adb)
    /// Handles file header parsing and packet iteration
    /// </summary>
    public sealed class RecordingFileReader : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly string _filePath;
        private FileStream? _fileStream;
        private BinaryReader? _reader;
        private bool _disposed;
        private bool _headerRead;
        
        /// <summary>
        /// Recording header information (null if legacy file without header)
        /// </summary>
        public RecordingFileHeader? Header { get; private set; }
        
        /// <summary>
        /// File position where packets begin (after header, if present)
        /// </summary>
        public long PacketsStartPosition { get; private set; }
        
        /// <summary>
        /// Creates a new recording file reader
        /// </summary>
        public RecordingFileReader(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Recording file not found: {filePath}", filePath);
            
            _filePath = filePath;
            Logger.Debug($"RecordingFileReader created for: {filePath}");
        }
        
        /// <summary>
        /// Opens the file and reads the header (if present)
        /// </summary>
        public void Open()
        {
            if (_fileStream != null)
            {
                Logger.Warn("File already open, skipping");
                return;
            }
            
            try
            {
                _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader = new BinaryReader(_fileStream);
                
                Logger.Debug($"Opened file: {_filePath} ({_fileStream.Length} bytes)");
                
                // Read header
                ReadHeader();
                
                // Mark where packets start
                PacketsStartPosition = _fileStream.Position;
                Logger.Debug($"Packets start at position: {PacketsStartPosition}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to open recording file: {_filePath}");
                Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// Reads and parses the optional file header
        /// </summary>
        private void ReadHeader()
        {
            if (_reader == null || _fileStream == null)
                throw new InvalidOperationException("File not opened");
            
            if (_headerRead)
                return;
            
            _headerRead = true;
            var headerStartPos = _fileStream.Position;
            
            try
            {
                Logger.Debug($"Checking for recording header at position {headerStartPos}");
                
                // Try to read the magic string
                var possibleMagic = _reader.ReadString();
                Logger.Debug($"Read possible magic: '{possibleMagic}'");
                
                if (possibleMagic == Constants.RECORDING_FILE_MAGIC)
                {
                    // This IS a header - read the rest
                    var headerIp = _reader.ReadString();
                    var headerPort = _reader.ReadInt32();
                    var headerStartTicks = _reader.ReadInt64();
                    
                    // Validate header data
                    if (headerStartTicks >= DateTime.MinValue.Ticks && 
                        headerStartTicks <= DateTime.MaxValue.Ticks &&
                        headerPort > 0 && headerPort <= 65535)
                    {
                        var headerStart = new DateTime(headerStartTicks, DateTimeKind.Utc);
                        var headerSize = _fileStream.Position - headerStartPos;
                        
                        Header = new RecordingFileHeader(
                            possibleMagic,
                            headerIp,
                            headerPort,
                            headerStart,
                            headerSize
                        );
                        
                        Logger.Info($"? Valid recording header found:");
                        Logger.Info($"   Magic: '{possibleMagic}'");
                        Logger.Info($"   Server: {headerIp}:{headerPort}");
                        Logger.Info($"   Start: {headerStart:o}");
                        Logger.Info($"   Header size: {headerSize} bytes");
                    }
                    else
                    {
                        Logger.Warn($"Header validation failed: ticks={headerStartTicks}, port={headerPort}");
                        Logger.Warn("Rewinding to treat as legacy file (no header)");
                        _fileStream.Position = headerStartPos;
                    }
                }
                else
                {
                    // Not a header - rewind to start of packets
                    Logger.Debug("No header magic found, treating as legacy file");
                    _fileStream.Position = headerStartPos;
                }
            }
            catch (EndOfStreamException)
            {
                Logger.Debug("End of stream while reading header - file too small, treating as legacy");
                _fileStream.Position = headerStartPos;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read header, treating as legacy file");
                _fileStream.Position = headerStartPos;
            }
        }
        
        /// <summary>
        /// Reads the next packet from the file
        /// </summary>
        /// <returns>The packet metadata, or null if end of file or read error</returns>
        public AudioPacketMetadata? ReadNextPacket()
        {
            if (_reader == null)
                throw new InvalidOperationException("File not opened. Call Open() first.");
            
            try
            {
                if (AudioPacketMetadata.TryReadMetadata(_reader, out var metadata))
                {
                    return metadata;
                }
            }
            catch (EndOfStreamException)
            {
                // Normal end of file
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Error reading packet at position {_fileStream?.Position ?? -1}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Reads all packets from the file
        /// </summary>
        public List<AudioPacketMetadata> ReadAllPackets(CancellationToken cancellationToken = default)
        {
            if (_reader == null)
                throw new InvalidOperationException("File not opened. Call Open() first.");
            
            var packets = new List<AudioPacketMetadata>();
            var packetsRead = 0;
            
            Logger.Debug("Reading all packets from file...");
            
            while (_fileStream!.Position < _fileStream.Length && !cancellationToken.IsCancellationRequested)
            {
                var packet = ReadNextPacket();
                if (packet == null)
                    break;
                
                packets.Add(packet);
                packetsRead++;
                
                if (packetsRead % 1000 == 0)
                {
                    Logger.Debug($"Read {packetsRead} packets...");
                }
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Info($"Packet reading cancelled after {packetsRead} packets");
            }
            else
            {
                Logger.Info($"Read {packetsRead} total packets from file");
            }
            
            return packets;
        }
        
        /// <summary>
        /// Enumerates packets from the file (lazy loading)
        /// </summary>
        public IEnumerable<AudioPacketMetadata> EnumeratePackets(CancellationToken cancellationToken = default)
        {
            if (_reader == null)
                throw new InvalidOperationException("File not opened. Call Open() first.");
            
            var packetsRead = 0;
            
            while (_fileStream!.Position < _fileStream.Length && !cancellationToken.IsCancellationRequested)
            {
                var packet = ReadNextPacket();
                if (packet == null)
                    break;
                
                packetsRead++;
                yield return packet;
                
                if (packetsRead % 1000 == 0)
                {
                    Logger.Debug($"Enumerated {packetsRead} packets...");
                }
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Info($"Packet enumeration cancelled after {packetsRead} packets");
            }
        }
        
        /// <summary>
        /// Seeks to the beginning of packets (after header)
        /// </summary>
        public void SeekToStart()
        {
            if (_fileStream == null)
                throw new InvalidOperationException("File not opened");
            
            _fileStream.Position = PacketsStartPosition;
            Logger.Debug($"Seeked to start of packets at position {PacketsStartPosition}");
        }
        
        /// <summary>
        /// Gets the current file position
        /// </summary>
        public long Position => _fileStream?.Position ?? -1;
        
        /// <summary>
        /// Gets the file length
        /// </summary>
        public long Length => _fileStream?.Length ?? 0;
        
        /// <summary>
        /// Closes the file
        /// </summary>
        public void Close()
        {
            _reader?.Dispose();
            _reader = null;
            
            _fileStream?.Dispose();
            _fileStream = null;
            
            Logger.Debug($"Closed file: {_filePath}");
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            Close();
            _disposed = true;
        }
        
        /// <summary>
        /// Static helper to quickly read all packets from a file
        /// </summary>
        public static List<AudioPacketMetadata> ReadAllPackets(string filePath, CancellationToken cancellationToken = default)
        {
            using var reader = new RecordingFileReader(filePath);
            reader.Open();
            return reader.ReadAllPackets(cancellationToken);
        }
        
        /// <summary>
        /// Static helper to enumerate packets from a file (lazy loading)
        /// </summary>
        public static IEnumerable<AudioPacketMetadata> EnumeratePackets(string filePath, CancellationToken cancellationToken = default)
        {
            using var reader = new RecordingFileReader(filePath);
            reader.Open();
            
            foreach (var packet in reader.EnumeratePackets(cancellationToken))
            {
                yield return packet;
            }
        }
        
        /// <summary>
        /// Static helper to read just the file header
        /// </summary>
        public static RecordingFileHeader? ReadHeader(string filePath)
        {
            using var reader = new RecordingFileReader(filePath);
            reader.Open();
            return reader.Header;
        }
    }
    
    /// <summary>
    /// Represents the header of a recording file
    /// </summary>
    public record RecordingFileHeader(
        string Magic,
        string ServerIp,
        int ServerPort,
        DateTime RecordingStart,
        long HeaderSizeBytes
    )
    {
        public override string ToString()
        {
            return $"Recording Header: {Magic} | Server: {ServerIp}:{ServerPort} | Start: {RecordingStart:o} | Size: {HeaderSizeBytes} bytes";
        }
    }
}
