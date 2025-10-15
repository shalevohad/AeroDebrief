using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NLog;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using ShalevOhad.DCS.SRS.Recorder.Core.Audio;
using ShalevOhad.DCS.SRS.Recorder.Core.Playback;
using ShalevOhad.DCS.SRS.Recorder.Core.Filtering;
using ShalevOhad.DCS.SRS.Recorder.Core.Analysis;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    /// <summary>
    /// Advanced SRS audio playback engine with WASAPI output and SRS Common integration.
    /// Provides high-quality audio processing, seeking, filtering, and effects.
    /// </summary>
    public sealed class AudioPacketReader : IDisposable
    {
        #region Private Fields

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly string _filePath;
        private readonly AudioProcessingEngine _processingEngine;
        private readonly AudioOutputEngine _outputEngine;
        private readonly PlaybackController _playbackController;
        private readonly FrequencyFilter _frequencyFilter;
        private readonly SeekController _seekController;
        private readonly AudioBufferManager _bufferManager; // Add buffer manager
        
        private bool _disposed;
        private bool _hasExportedDebugWav; // Track if we've already exported debug WAV for this file

        #endregion

        #region Events

        /// <summary>Raised when playback starts</summary>
        public event Action? PlaybackStarted;
        
        /// <summary>Raised when playback stops</summary>
        public event Action? PlaybackStopped;
        
        /// <summary>Raised when playback is paused</summary>
        public event Action? PlaybackPaused;
        
        /// <summary>Raised when playback is resumed</summary>
        public event Action? PlaybackResumed;
        
        /// <summary>Raised when a playback error occurs</summary>
        public event Action<Exception>? PlaybackError;
        
        /// <summary>Raised when playback progress changes (0-100)</summary>
        public event Action<double>? PlaybackProgressChanged;
        
        /// <summary>Raised when a packet begins processing</summary>
        public event Action<AudioPacketMetadata>? PacketStarted;
        
        /// <summary>Raised when playback time changes (current, total)</summary>
        public event Action<TimeSpan, TimeSpan>? PlaybackTimeChanged;

        #endregion

        #region Public Properties

        /// <summary>Total duration of the recording</summary>
        public TimeSpan TotalDuration => _playbackController.TotalDuration;
        
        /// <summary>Current playback position</summary>
        public TimeSpan CurrentPosition => _playbackController.CurrentPosition;
        
        /// <summary>Whether playback is currently active (playing)</summary>
        public bool IsPlaying => _playbackController.IsPlaying;
        
        /// <summary>Whether playback is currently paused</summary>
        public bool IsPaused => _playbackController.IsPaused;
        
        /// <summary>Whether frequency filtering is enabled</summary>
        public bool IsFrequencyFilterEnabled => _frequencyFilter.IsEnabled;
        
        /// <summary>Currently selected frequency-modulation combinations</summary>
        public IReadOnlySet<(double Frequency, Modulation Modulation)> SelectedFrequencyModulations => 
            _frequencyFilter.SelectedCombinations;

        #endregion

        #region Constructor

        public AudioPacketReader(string filePath) : this(filePath, TimeSpan.FromSeconds(3), 1000)
        {
            // Default constructor delegates to main constructor with default buffer settings
        }

        /// <summary>
        /// Creates an AudioPacketReader with custom buffer settings
        /// </summary>
        /// <param name="filePath">Path to the audio file</param>
        /// <param name="bufferAheadTime">How many seconds to buffer ahead of playback</param>
        /// <param name="maxBufferedChunks">Maximum number of audio chunks to keep in buffer</param>
        public AudioPacketReader(string filePath, TimeSpan bufferAheadTime, int maxBufferedChunks = 1000)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (bufferAheadTime <= TimeSpan.Zero)
                throw new ArgumentException("Buffer ahead time must be positive", nameof(bufferAheadTime));
            if (maxBufferedChunks <= 0)
                throw new ArgumentException("Max buffered chunks must be positive", nameof(maxBufferedChunks));

            _filePath = filePath;
            
            _processingEngine = new AudioProcessingEngine();
            _outputEngine = new AudioOutputEngine();
            _playbackController = new PlaybackController();
            _frequencyFilter = new FrequencyFilter();
            _seekController = new SeekController();
            _bufferManager = new AudioBufferManager(_processingEngine, bufferAheadTime, maxBufferedChunks);
            
            WireUpEventHandlers();
            
            Logger.Info($"AudioPacketReader initialized for file: {filePath} with {bufferAheadTime.TotalSeconds:F1}-second buffering (max {maxBufferedChunks} chunks)");
        }

        #endregion

        #region Public Methods

        /// <summary>Starts audio playback</summary>
        public void StartPlayback()
        {
            ThrowIfDisposed();
            
            // CRITICAL FIX: Ensure volume is set before playback starts
            SetMasterVolume(1.0f);
            Logger.Info("?? AudioPacketReader: Master volume explicitly set to 1.0 for playback");
            
            // ADDITIONAL FIX: Initialize processing engine explicitly
            try
            {
                _processingEngine.Initialize();
                _processingEngine.SetMasterVolume(1.0f);
                Logger.Info("?? AudioProcessingEngine: Master volume explicitly set to 1.0 for playback");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize audio processing engine");
                throw;
            }
            
            _playbackController.Start(_filePath, ProcessPlaybackAsync);
        }

        /// <summary>Stops audio playback</summary>
        public void StopPlayback()
        {
            // Use async version but don't block the calling thread
            _ = StopPlaybackAsync();
        }

        /// <summary>Stops audio playback asynchronously</summary>
        public async Task StopPlaybackAsync()
        {
            try
            {
                await _playbackController.StopAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during async stop playback");
                // Don't rethrow - we want stop to always succeed
            }
        }

        /// <summary>Pauses audio playback</summary>
        public void PausePlayback()
        {
            ThrowIfDisposed();
            _playbackController.Pause();
        }

        /// <summary>Resumes audio playback</summary>
        public void ResumePlayback()
        {
            ThrowIfDisposed();
            _playbackController.Resume();
        }

        /// <summary>Seeks to a specific position in the recording</summary>
        public void SeekTo(TimeSpan position)
        {
            ThrowIfDisposed();
            _seekController.SeekTo(position, TotalDuration);
        }

        /// <summary>Indicates whether the user is currently seeking</summary>
        public void SetUserSeeking(bool seeking)
        {
            _seekController.SetUserSeeking(seeking);
        }

        /// <summary>Sets the master volume (0.0 - 1.0)</summary>
        public void SetMasterVolume(float volume)
        {
            ThrowIfDisposed();
            _processingEngine.SetMasterVolume(volume);
            _outputEngine.SetMasterVolume(volume);
        }

        /// <summary>Gets the current buffering status for debugging</summary>
        public (int BufferedChunks, TimeSpan BufferedDuration) GetBufferStatus()
        {
            ThrowIfDisposed();
            return (_bufferManager.BufferedChunkCount, _bufferManager.BufferedDuration);
        }

        /// <summary>Sets frequency filter for specific frequency-modulation combinations</summary>
        public void SetFrequencyFilter(IEnumerable<FrequencyModulationInfo> frequencyModulations)
        {
            ThrowIfDisposed();
            _frequencyFilter.SetFilter(frequencyModulations);
        }

        /// <summary>Clears the frequency filter</summary>
        public void ClearFrequencyFilter()
        {
            ThrowIfDisposed();
            _frequencyFilter.ClearFilter();
        }

        /// <summary>Gets all unique frequency-modulation combinations from the file</summary>
        public List<FrequencyModulationInfo> GetAllFrequencyModulations(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileAnalyzer.GetAllFrequencyModulations(_filePath, cancellationToken);
        }

        /// <summary>Calculates the total duration of the recording</summary>
        public TimeSpan CalculateTotalDuration()
        {
            ThrowIfDisposed();
            var duration = FileAnalyzer.CalculateTotalDuration(_filePath);
            _playbackController.SetTotalDuration(duration);
            return duration;
        }

        /// <summary>Reads all packets from the file (for analysis purposes)</summary>
        public IEnumerable<AudioPacketMetadata> ReadAllPackets(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return FileAnalyzer.ReadAllPackets(_filePath, cancellationToken);
        }

        /// <summary>Legacy method for backward compatibility</summary>
        public List<double> GetAllFrequencies(CancellationToken cancellationToken = default)
        {
            return GetAllFrequencyModulations(cancellationToken)
                .Select(fm => fm.Frequency)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
        }

        /// <summary>
        /// Export the combined audio of all currently selected frequency-modulation combinations
        /// into two WAV files: one "before" audio manipulations (decoded & resampled)
        /// and one "after" audio manipulations (volume/effects applied).
        /// </summary>
        public async Task ExportSelectedFrequenciesToWavAsync(string outputPath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var selected = SelectedFrequencyModulations?.ToList() ?? new List<(double, Modulation)>();
            if (selected.Count == 0)
                throw new InvalidOperationException("No selected frequency-modulation combinations to export.");

            Logger.Info($"Exporting combined WAV for {selected.Count} selected frequency(s) to: {outputPath}");

            // Read and filter packets from file
            var allPackets = ReadAllPackets(cancellationToken).ToList();
            var packets = allPackets.Where(p => selected.Any(s => Math.Abs(s.Item1 - p.Frequency) < 0.01 && s.Item2 == (Modulation)p.Modulation))
                                    .OrderBy(p => p.Timestamp)
                                    .ToList();

            if (packets.Count == 0)
            {
                Logger.Warn("No packets found for the selected frequencies");
                throw new InvalidOperationException("No packets found for the selected frequencies");
            }

            // Timeline: determine start and end timestamps using decoded lengths
            var firstTs = packets.First().Timestamp;
            DateTime lastTs = firstTs;
            var sampleRate = Constants.OUTPUT_SAMPLE_RATE;

            foreach (var pkt in packets)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var decoded = _processingEngine.DecodePacketToFloat(pkt);
                if (decoded != null && decoded.Length > 0)
                {
                    var pktEnd = pkt.Timestamp + TimeSpan.FromSeconds((double)decoded.Length / sampleRate);
                    if (pktEnd > lastTs) lastTs = pktEnd;
                }
            }

            var totalSeconds = (lastTs - firstTs).TotalSeconds;
            if (totalSeconds <= 0) totalSeconds = Constants.OPUS_FRAME_DURATION_MS / 1000.0; // fallback

            var totalSamples = (int)Math.Ceiling(totalSeconds * sampleRate) + 1;
            Logger.Debug($"Mix buffer: duration={totalSeconds:F3}s, samples={totalSamples}");

            var beforeMix = new float[totalSamples];
            var afterMix = new float[totalSamples];

            // Mix packets into buffers by timestamp
            foreach (var pkt in packets)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var decoded = _processingEngine.DecodePacketToFloat(pkt);
                var processed = _processingEngine.ProcessPacket(pkt);

                if ((decoded == null || decoded.Length == 0) && (processed == null || processed.Length == 0))
                    continue;

                var offsetSamples = (int)Math.Round((pkt.Timestamp - firstTs).TotalSeconds * sampleRate);
                if (offsetSamples < 0) offsetSamples = 0;

                if (decoded != null && decoded.Length > 0)
                {
                    for (int i = 0; i < decoded.Length; i++)
                    {
                        var idx = offsetSamples + i;
                        if (idx >= beforeMix.Length) break;
                        beforeMix[idx] += decoded[i];
                    }
                }

                if (processed != null && processed.Length > 0)
                {
                    for (int i = 0; i < processed.Length; i++)
                    {
                        var idx = offsetSamples + i;
                        if (idx >= afterMix.Length) break;
                        afterMix[idx] += processed[i];
                    }
                }
            }

            // Normalize/clamp both buffers
            void NormalizeBuffer(float[] buffer)
            {
                float max = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var a = Math.Abs(buffer[i]);
                    if (a > max) max = a;
                }

                if (max > 1.0f)
                {
                    Logger.Debug($"Normalizing mixed audio (max={max:F4})");
                    var inv = 1.0f / max;
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = Math.Clamp(buffer[i] * inv, -1.0f, 1.0f);
                }
                else
                {
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = Math.Clamp(buffer[i], -1.0f, 1.0f);
                }
            }

            NormalizeBuffer(beforeMix);
            NormalizeBuffer(afterMix);

            // Prepare output file paths (add _before/_after suffix if original path provided)
            var dir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
            var name = Path.GetFileNameWithoutExtension(outputPath) ?? "combined";
            var beforePath = Path.Combine(dir, name + "_before.wav");
            var afterPath = Path.Combine(dir, name + "_after.wav");

            try
            {
                var pcmBefore = AudioConverter.FloatToPcm16(beforeMix);
                using (var writer = new WaveFileWriter(beforePath, new WaveFormat(sampleRate, 16, 1)))
                {
                    writer.Write(pcmBefore, 0, pcmBefore.Length);
                    writer.Flush();
                }

                var pcmAfter = AudioConverter.FloatToPcm16(afterMix);
                using (var writer = new WaveFileWriter(afterPath, new WaveFormat(sampleRate, 16, 1)))
                {
                    writer.Write(pcmAfter, 0, pcmAfter.Length);
                    writer.Flush();
                }

                Logger.Info($"Exported combined WAVs: {beforePath} and {afterPath} ({beforeMix.Length} samples)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to write combined WAVs to: {outputPath}");
                throw;
            }
        }

        /// <summary>
        /// Export the entire recording (all packets) into two WAV files: one decoded raw (before processing)
        /// and one after passing through the audio processing pipeline (after processing).
        /// </summary>
        public async Task ExportFullRecordingToWavAsync(string outputPath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            Logger.Info($"Exporting full recording to WAV: {outputPath}");

            // Load all packets from file (no filtering)
            var allPackets = ReadAllPackets(cancellationToken).ToList();
            if (allPackets.Count == 0)
            {
                Logger.Warn("No packets found in recording for export");
                throw new InvalidOperationException("No packets found in recording");
            }

            var firstTs = allPackets.First().Timestamp;
            DateTime lastTs = firstTs;
            var sampleRate = Constants.OUTPUT_SAMPLE_RATE;

            foreach (var pkt in allPackets)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var decoded = _processingEngine.DecodePacketToFloat(pkt);
                if (decoded != null && decoded.Length > 0)
                {
                    var pktEnd = pkt.Timestamp + TimeSpan.FromSeconds((double)decoded.Length / sampleRate);
                    if (pktEnd > lastTs) lastTs = pktEnd;
                }
            }

            var totalSeconds = (lastTs - firstTs).TotalSeconds;
            if (totalSeconds <= 0) totalSeconds = Constants.OPUS_FRAME_DURATION_MS / 1000.0; // fallback

            var totalSamples = (int)Math.Ceiling(totalSeconds * sampleRate) + 1;
            Logger.Debug($"Full export mix buffer: duration={totalSeconds:F3}s, samples={totalSamples}");

            var beforeMix = new float[totalSamples];
            var afterMix = new float[totalSamples];

            // Mix packets into buffers by timestamp
            foreach (var pkt in allPackets)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var decoded = _processingEngine.DecodePacketToFloat(pkt);
                var processed = _processingEngine.ProcessPacket(pkt);

                if ((decoded == null || decoded.Length == 0) && (processed == null || processed.Length == 0))
                    continue;

                var offsetSamples = (int)Math.Round((pkt.Timestamp - firstTs).TotalSeconds * sampleRate);
                if (offsetSamples < 0) offsetSamples = 0;

                if (decoded != null && decoded.Length > 0)
                {
                    for (int i = 0; i < decoded.Length; i++)
                    {
                        var idx = offsetSamples + i;
                        if (idx >= beforeMix.Length) break;
                        beforeMix[idx] += decoded[i];
                    }
                }

                if (processed != null && processed.Length > 0)
                {
                    for (int i = 0; i < processed.Length; i++)
                    {
                        var idx = offsetSamples + i;
                        if (idx >= afterMix.Length) break;
                        afterMix[idx] += processed[i];
                    }
                }
            }

            // Normalize/clamp both buffers
            void NormalizeBuffer(float[] buffer)
            {
                float max = 0f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var a = Math.Abs(buffer[i]);
                    if (a > max) max = a;
                }

                if (max > 1.0f)
                {
                    Logger.Debug($"Normalizing mixed audio (max={max:F4})");
                    var inv = 1.0f / max;
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = Math.Clamp(buffer[i] * inv, -1.0f, 1.0f);
                }
                else
                {
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = Math.Clamp(buffer[i], -1.0f, 1.0f);
                }
            }

            NormalizeBuffer(beforeMix);
            NormalizeBuffer(afterMix);

            // Prepare output file paths
            var dir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
            var name = Path.GetFileNameWithoutExtension(outputPath) ?? "combined";
            var beforePath = Path.Combine(dir, name + "_before.wav");
            var afterPath = Path.Combine(dir, name + "_after.wav");

            try
            {
                var pcmBefore = AudioConverter.FloatToPcm16(beforeMix);
                using (var writer = new WaveFileWriter(beforePath, new WaveFormat(sampleRate, 16, 1)))
                {
                    writer.Write(pcmBefore, 0, pcmBefore.Length);
                    writer.Flush();
                }

                var pcmAfter = AudioConverter.FloatToPcm16(afterMix);
                using (var writer = new WaveFileWriter(afterPath, new WaveFormat(sampleRate, 16, 1)))
                {
                    writer.Write(pcmAfter, 0, pcmAfter.Length);
                    writer.Flush();
                }

                Logger.Info($"Exported full recording WAVs: {beforePath} and {afterPath} ({beforeMix.Length} samples)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to write full recording WAVs to: {outputPath}");
                throw;
            }
        }

        #endregion

        #region Private Methods

        private void WireUpEventHandlers()
        {
            _playbackController.PlaybackStarted += () => PlaybackStarted?.Invoke();
            _playbackController.PlaybackStopped += () => PlaybackStopped?.Invoke();
            _playbackController.PlaybackPaused += () => PlaybackPaused?.Invoke();
            _playbackController.PlaybackResumed += () => PlaybackResumed?.Invoke();
            _playbackController.PlaybackError += (ex) => PlaybackError?.Invoke(ex);
            _playbackController.ProgressChanged += (progress) => PlaybackProgressChanged?.Invoke(progress);
            _playbackController.TimeChanged += (current, total) => PlaybackTimeChanged?.Invoke(current, total);
            _playbackController.PacketStarted += (packet) => PacketStarted?.Invoke(packet);
        }

        private async Task ProcessPlaybackAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info("Starting high-quality SRS audio playback with buffering");

                // Initialize components with error handling
                try
                {
                    await _outputEngine.InitializeAsync();
                    Logger.Info("Audio output engine initialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize audio output engine");
                    throw new InvalidOperationException("Cannot start playback: Audio output initialization failed", ex);
                }

                try
                {
                    _processingEngine.Initialize();
                    Logger.Info("Audio processing engine initialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize audio processing engine");
                    throw new InvalidOperationException("Cannot start playback: Audio processing initialization failed", ex);
                }

                // Load and filter packets
                var packets = await LoadAndFilterPacketsAsync(cancellationToken);
                if (packets.Count == 0)
                {
                    Logger.Warn("No packets to play after filtering");
                    return;
                }

                Logger.Info($"Starting buffered playback of {packets.Count} packets");

                // Calculate and set total duration
                if (packets.Count > 0)
                {
                    var firstPacket = packets[0];
                    var lastPacket = packets[^1];
                    var calculatedDuration = lastPacket.Timestamp - firstPacket.Timestamp;
                    if (calculatedDuration > TimeSpan.Zero)
                    {
                        _playbackController.SetTotalDuration(calculatedDuration);
                        Logger.Debug($"Set total duration: {calculatedDuration}");
                    }
                }

                // Start audio output
                try
                {
                    _outputEngine.Start();
                    Logger.Debug("Audio output started");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to start audio output");
                    throw;
                }

                // Start buffering system
                var recordingStart = _playbackController.RecordingStart;
                _bufferManager.StartBuffering(packets, recordingStart);
                Logger.Info("Audio buffering started for smooth playback");

                // Process packets using buffered approach
                await ProcessBufferedPacketsAsync(packets, cancellationToken);

                Logger.Info("Playback completed successfully");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Playback cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Playback failed");
                throw;
            }
            finally
            {
                try
                {
                    _bufferManager.StopBuffering();
                    _outputEngine.Stop();
                    Logger.Debug("Audio output and buffering stopped");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error stopping audio output and buffering");
                }
            }
        }

        private async Task<List<AudioPacketMetadata>> LoadAndFilterPacketsAsync(CancellationToken cancellationToken)
        {
            var packets = new List<AudioPacketMetadata>();
            DateTime? recordingStart = null;
            int totalPacketsRead = 0;
            int filteredPackets = 0;

            try
            {
                Logger.Info($"Loading packets from file: {_filePath}");
                
                if (!File.Exists(_filePath))
                {
                    throw new FileNotFoundException($"Recording file not found: {_filePath}");
                }

                using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                Logger.Debug($"File size: {fs.Length} bytes");

                while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                        {
                            totalPacketsRead++;
                            
                            if (_frequencyFilter.ShouldIncludePacket(metadata))
                            {
                                packets.Add(metadata);
                                recordingStart ??= metadata.Timestamp;
                                filteredPackets++;
                                
                                if (filteredPackets % 1000 == 0)
                                {
                                    Logger.Debug($"Loaded {filteredPackets} packets ({totalPacketsRead} total read)");
                                }
                            }
                        }
                        else
                        {
                            Logger.Debug($"End of readable data at position {fs.Position}/{fs.Length}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Error reading packet at file position {fs.Position}, skipping to next");
                        // Try to recover by skipping some bytes
                        if (fs.Position < fs.Length - 100)
                        {
                            fs.Position += 10; // Skip ahead a bit
                            continue;
                        }
                        else
                        {
                            break; // Near end of file, stop reading
                        }
                    }
                }

                if (recordingStart.HasValue)
                {
                    _playbackController.SetRecordingStart(recordingStart.Value);
                    Logger.Info($"Recording start time: {recordingStart.Value}");
                }

                var sortedPackets = packets.OrderBy(p => p.Timestamp).ToList();
                Logger.Info($"?? PACKET LOADING COMPLETE: Loaded {sortedPackets.Count} packets for playback (filtered from {totalPacketsRead} total packets)");
                
                if (sortedPackets.Count > 0)
                {
                    var duration = sortedPackets[^1].Timestamp - sortedPackets[0].Timestamp;
                    Logger.Info($"   Playback duration: {duration}");
                    
                    // Log frequency distribution of loaded packets
                    var freqGroups = sortedPackets.GroupBy(p => (Frequency: p.Frequency, Modulation: (Modulation)p.Modulation))
                                                 .OrderBy(g => g.Key.Frequency)
                                                 .ToList();
                    Logger.Info($"   Frequency distribution in loaded packets:");
                    foreach (var group in freqGroups.Take(10)) // Show first 10
                    {
                        Logger.Info($"      - {group.Key.Frequency:F1} Hz ({group.Key.Modulation}): {group.Count()} packets");
                    }
                    if (freqGroups.Count > 10)
                    {
                        Logger.Info($"      ... and {freqGroups.Count - 10} more frequencies");
                    }
                }

                // If auto-export is enabled and we haven't exported yet, export full recording WAVs
                try
                {
                    if (!_hasExportedDebugWav && sortedPackets.Count > 0)
                    {
                        var autoExportEnv = Environment.GetEnvironmentVariable("AUTO_EXPORT_DEBUG");
                        var shouldAutoExport = Debugger.IsAttached || string.Equals(autoExportEnv, "1", StringComparison.OrdinalIgnoreCase);

                        if (shouldAutoExport)
                        {
                            _hasExportedDebugWav = true; // Mark as exported to prevent duplicate exports
                            
                            // Run export in background so loading is not blocked
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    Logger.Info($"Auto-export enabled: exporting full recording WAVs (once per file)");

                                    var outDir = Path.GetDirectoryName(_filePath) ?? Environment.CurrentDirectory;
                                    var baseName = Path.GetFileNameWithoutExtension(_filePath) ?? "recording";
                                    var outPath = Path.Combine(outDir, baseName + "_debug_export.wav");

                                    await ExportFullRecordingToWavAsync(outPath, CancellationToken.None);

                                    Logger.Info($"Auto-export completed: {outPath}_before.wav and {outPath}_after.wav");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn(ex, "Auto-export failed");
                                    _hasExportedDebugWav = false; // Reset flag on failure so user can retry
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to trigger auto-export");
                }

                return sortedPackets;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load and filter packets from {_filePath}");
                throw;
            }
        }

        private async Task ProcessBufferedPacketsAsync(List<AudioPacketMetadata> packets, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var recordingStart = _playbackController.RecordingStart;
            var lastProgressUpdate = DateTime.UtcNow;
            var currentPacketIndex = 0; // Track current packet index for seeking
            
            Logger.Debug("Starting buffered packet processing");

            while (!cancellationToken.IsCancellationRequested)
            {
                // Check for pause
                await _playbackController.WaitIfPausedAsync(cancellationToken);

                // Handle seeking
                if (_seekController.HandleSeekIfRequested(packets, ref currentPacketIndex, ref startTime))
                {
                    Logger.Info($"Seek operation detected, clearing buffers and updating position");
                    
                    // Clear audio output buffers
                    _outputEngine.ClearBuffer();
                    
                    // Seek the buffer manager to the new position
                    _bufferManager.SeekTo(currentPacketIndex);
                    
                    // Update the playback controller position immediately
                    if (currentPacketIndex < packets.Count)
                    {
                        var seekedPacketTime = packets[currentPacketIndex].Timestamp - recordingStart;
                        _playbackController.SetPosition(seekedPacketTime);
                        Logger.Debug($"Position immediately updated after seek to: {seekedPacketTime}");
                    }
                    
                    // Small delay to ensure audio system has flushed
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                // Calculate current playback position
                var elapsedTime = DateTime.UtcNow - startTime;
                var currentPlaybackPosition = elapsedTime;
                
                // Update position tracking
                _playbackController.UpdatePosition(currentPlaybackPosition);
                
                // Update progress periodically (every 100ms) to avoid excessive UI updates
                if ((DateTime.UtcNow - lastProgressUpdate).TotalMilliseconds >= 100)
                {
                    if (!_seekController.IsUserSeeking)
                    {
                        _playbackController.UpdateProgress();
                    }
                    lastProgressUpdate = DateTime.UtcNow;
                }

                // Skip processing if seeking is in progress
                if (_seekController.IsUserSeeking)
                {
                    Logger.Trace("Skipping buffered audio retrieval - user is seeking");
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                // Try to get the next buffered audio chunk
                var bufferedChunk = _bufferManager.GetNextAudioChunk(currentPlaybackPosition);
                
                if (bufferedChunk != null)
                {
                    // We have buffered audio ready to play
                    try
                    {
                        await _outputEngine.WriteAudioAsync(bufferedChunk.AudioData);
                        
                        // Notify packet started
                        _playbackController.NotifyPacketStarted(bufferedChunk.SourcePacket);
                        
                        Logger.Trace($"Played buffered audio chunk at {bufferedChunk.PlaybackTime} (buffered chunks: {_bufferManager.BufferedChunkCount})");
                        
                        bufferedChunk.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error playing buffered audio chunk");
                        bufferedChunk.Dispose();
                        
                        // Output silence to maintain stream continuity
                        try
                        {
                            var silenceData = new byte[Constants.OPUS_FRAME_SIZE * 2];
                            await _outputEngine.WriteAudioAsync(silenceData);
                        }
                        catch (Exception silenceEx)
                        {
                            Logger.Warn(silenceEx, "Failed to output silence after buffered audio error");
                        }
                    }
                }
                else
                {
                    // No buffered audio available, check if we've reached the end
                    if (currentPlaybackPosition >= _playbackController.TotalDuration)
                    {
                        Logger.Info("Reached end of playback duration");
                        break;
                    }
                    
                    // Log buffer status for debugging
                    var bufferInfo = $"buffered chunks: {_bufferManager.BufferedChunkCount}, buffered duration: {_bufferManager.BufferedDuration.TotalSeconds:F1}s";
                    
                    if (_bufferManager.BufferedChunkCount == 0)
                    {
                        Logger.Debug($"No buffered audio available at {currentPlaybackPosition} ({bufferInfo}) - waiting for buffer");
                        
                        // Output silence to prevent audio gaps
                        try
                        {
                            var silenceData = new byte[Constants.OPUS_FRAME_SIZE * 2];
                            await _outputEngine.WriteAudioAsync(silenceData);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Failed to output silence during buffer underrun");
                        }
                    }
                    
                    // Wait a bit before checking again
                    await Task.Delay(10, cancellationToken);
                }
            }
            
            Logger.Debug("Buffered packet processing completed");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPacketReader));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _playbackController?.Dispose();
            _outputEngine?.Dispose();
            _processingEngine?.Dispose();
            _seekController?.Dispose();
            _bufferManager?.Dispose(); // Dispose buffer manager

            _disposed = true;
            
            Logger.Debug("AudioPacketReader disposed");
        }

        #endregion
    }
}