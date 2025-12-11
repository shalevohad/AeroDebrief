namespace AeroDebrief.CLI
{
    using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
    using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
    using AeroDebrief.Core;
    using AeroDebrief.Core.Audio;
    using AeroDebrief.Core.Helpers;
    using AeroDebrief.Core.Settings;
    using NLog;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            Console.WriteLine($"SRS Recording Client Version: {Constants.VERSION}");
            Console.WriteLine($"Minimum required server version: {Constants.MINIMUM_SERVER_VERSION}");
            Logger.Info($"SRS Recording Client Version: {Constants.VERSION}");
            Logger.Info($"Minimum required server version: {Constants.MINIMUM_SERVER_VERSION}");

            // Check for special commands
            if (args.Length > 0 && args[0].StartsWith("--"))
            {
                await HandleSpecialCommand(args);
                return;
            }

            // Default recording mode
            var settings = RecorderSettingsStore.Instance;

            string serverIp = args.Length > 0 ? args[0] : settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
            int port = args.Length > 1 ? int.Parse(args[1]) : settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);

            Logger.Info($"Using server IP: {serverIp}, port: {port}");

            string clientGuid = ShortGuid.NewGuid();
            string clientName = "RecordingClient_" + clientGuid;
            Logger.Info($"Generated client GUID: {clientGuid}, client name: {clientName}");
            RecordingClientState.Initialize(clientGuid, clientName);

            var recorder = new AudioPacketRecorder();
            bool isConnected = false;
            TCPClientStatusMessage? lastStatus = null;
            bool shouldReconnect = false;

            // Subscribe to connection status updates
            recorder.ConnectionStatusChanged += status =>
            {
                lastStatus = status;
                if (status.Connected)
                {
                    isConnected = true;
                    shouldReconnect = false;
                    Console.WriteLine($"[Connection Status] Connected to server: {status.Address}");
                    Logger.Info($"Connected to server: {status.Address}");

                    // Print the server version if available
                    if (!string.IsNullOrEmpty(recorder.ServerVersion))
                    {
                        Console.WriteLine($"Server version: {recorder.ServerVersion}");
                        Logger.Info($"Server version: {recorder.ServerVersion}");
                    }
                }
                else
                {
                    isConnected = false;
                    Console.WriteLine($"[Connection Status] Disconnected. Reason: {status.Error}");
                    Logger.Warn($"Disconnected. Reason: {status.Error}");

                    // Check for server-side disconnect reasons
                    if (status.Error == TCPClientStatusMessage.ErrorCode.TIMEOUT ||
                        status.Error == TCPClientStatusMessage.ErrorCode.MISMATCHED_SERVER ||
                        status.Error == TCPClientStatusMessage.ErrorCode.INVALID_SERVER)
                    {
                        shouldReconnect = true;
                    }
                    else
                    {
                        shouldReconnect = false;
                    }
                }

                if (!status.Connected)
                {
                    switch (status.Error)
                    {
                        case TCPClientStatusMessage.ErrorCode.MISMATCHED_SERVER:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: The server version is incompatible with this client. Please update your server or client.");
                            Console.ResetColor();
                            Logger.Error("The server version is incompatible with this client. Please update your server or client.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.TIMEOUT:
                            Console.WriteLine("Error: Connection timed out.");
                            Logger.Error("Connection timed out.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.INVALID_SERVER:
                            Console.WriteLine("Error: Invalid server address or configuration.");
                            Logger.Error("Invalid server address or configuration.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.USER_DISCONNECTED:
                            Console.WriteLine("Disconnected from server.");
                            Logger.Info("Disconnected from server.");
                            break;
                        default:
                            Console.WriteLine("Disconnected from server for an unknown reason.");
                            Logger.Warn("Disconnected from server for an unknown reason.");
                            break;
                    }
                }
            };

            // Try to connect and start recording only if connected
            while (true)
            {   
                Console.WriteLine("\n-----------------------------------------------------");
                Console.WriteLine($"Trying to connect to server at {serverIp} (Port:{port})...");
                Logger.Info($"Trying to connect to server at {serverIp} (Port:{port})...");
                await recorder.ConnectAsync(serverIp, port);

                // Wait a moment for the connection status to update
                int waitMs = 0;
                while (lastStatus == null && waitMs < 2000)
                {
                    await Task.Delay(100);
                    waitMs += 100;
                }

                if (isConnected)
                {
                    // Phase 3: Show recording format information
                    string recordingFile = settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);
                    recorder.StartRecording(recordingFile);
                    
                    Console.WriteLine("???????????????????????????????????????????????????");
                    Console.WriteLine("???  Phase 3 Recording Started");
                    Console.WriteLine("???????????????????????????????????????????????????");
                    Console.WriteLine($"?? Output file: {recordingFile}");
                    
#if DEBUG
                    // DEBUG build: Compression controlled by RecordingConstants
                    if (RecordingConstants.FORCE_CVR_COMPRESSION)
                    {
                        Console.WriteLine($"?? Format: CVR (Compressed)");
                        Console.WriteLine($"???  Compression: FORCED (RecordingConstants)");
                    }
                    else
                    {
                        Console.WriteLine($"?? Format: SQLite (Uncompressed)");
                        Console.WriteLine($"??  Compression: DISABLED (RecordingConstants)");
                    }
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"??  Mode: DEBUG (RecordingConstants.FORCE_CVR_COMPRESSION = {RecordingConstants.FORCE_CVR_COMPRESSION})");
                    Console.ResetColor();
#else
                    // RELEASE build: Always compressed (mandatory)
                    Console.WriteLine($"?? Format: CVR (Compressed) - MANDATORY");
                    Console.WriteLine($"???  Compression: ENFORCED (no user control)");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"??  Mode: RELEASE (compression required)");
                    Console.ResetColor();
#endif
                    
                    Console.WriteLine($"?? Recording to: Temporary SQLite database");
                    Console.WriteLine();
                    Console.WriteLine("Press Ctrl+C to stop recording and disconnect");
                    Console.WriteLine("???????????????????????????????????????????????????");
                    
                    Logger.Info($"Recording started");
                    Console.WriteLine("\n?? Listening for incoming packets:");

                    recorder.PacketReceived += meta =>
                    {
                        // Display comprehensive player information
                        var playerInfo = meta.PlayerData;
                        var displayName = playerInfo?.GetDisplayName() ?? $"Unknown ({meta.TransmitterGuid})";
                        var coalition = playerInfo?.GetCoalitionName() ?? "Unknown";
                        var aircraft = playerInfo?.AircraftInfo?.ToString() ?? "Unknown Aircraft";
                        var position = playerInfo?.Position.ToString() ?? "Unknown Position";
                        var seat = playerInfo?.Seat >= 0 ? $"Seat {playerInfo.Seat}" : "Unknown Seat";
                        
                        Console.WriteLine($"?? Packet received:");
                        Console.WriteLine($"  ?? Time: {meta.Timestamp:HH:mm:ss.fff}");
                        Console.WriteLine($"  ?? Player: {displayName} ({coalition}, {seat})");
                        Console.WriteLine($"  ??  Aircraft: {aircraft}");
                        Console.WriteLine($"  ?? Position: {position}");
                        Console.WriteLine($"  ?? Frequency: {meta.Frequency / 1_000_000.0:F1} MHz, Modulation: {meta.Modulation}");
                        Console.WriteLine($"  ?? Audio: {meta.AudioPayload.Length} bytes");
                        Console.WriteLine();

                        Logger.Debug($"Packet: Player={displayName}, Aircraft={aircraft}, Freq={meta.Frequency}, Mod={meta.Modulation}, Size={meta.AudioPayload.Length}");
                    };

                    // Handle Ctrl+C and window close
                    bool cleanedUp = false;
                    void Cleanup(object? sender, EventArgs? e)
                    {
                        if (cleanedUp) return;
                        cleanedUp = true;

                        Console.WriteLine();
                        Console.WriteLine("---------------------------------------------------");
                        Console.WriteLine("??  Stopping recording...");
                        Console.WriteLine("---------------------------------------------------");
                        
                        recorder.StopRecording();
                        
                        Console.WriteLine("? Recording finalized");
                        
#if DEBUG
                        if (RecordingConstants.FORCE_CVR_COMPRESSION)
                        {
                            Console.WriteLine("???  Compressing to CVR format...");
                            Console.WriteLine("   (This may take a moment for large recordings)");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("??  Saved UNCOMPRESSED (RecordingConstants.FORCE_CVR_COMPRESSION = false)");
                            Console.ResetColor();
                        }
#else
                        Console.WriteLine("???  Compressing to CVR format (mandatory)...");
                        Console.WriteLine("   (This may take a moment for large recordings)");
#endif
                        
                        recorder.Disconnect();
                        Console.WriteLine("-- Disconnected from server");
                        Console.WriteLine("---------------------------------------------------");
                        
                        Logger.Info("Recording stopped and disconnected");
                        Environment.Exit(0);
                    }

                    Console.CancelKeyPress += (s, e) =>
                    {
                        Cleanup(s, e);
                        e.Cancel = false;
                    };
                    AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup(s, e);

                    // Wait until disconnected
                    while (isConnected)
                    {
                        await Task.Delay(500);
                    }

                    // If disconnected due to server issue, ask user if they want to reconnect
                    if (shouldReconnect)
                    {
                        Console.WriteLine("Lost connection due to server issue. Retry? (y/n): ");
                        Logger.Warn("Lost connection due to server issue. Retry? (y/n): ");
                        var key = Console.ReadKey(true);
                        if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                        {
                            lastStatus = null;
                            Logger.Info("User chose to retry connection.");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Exiting.");
                            Logger.Info("Exiting.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Disconnected for non-server reason. Exiting.");
                        Logger.Info("Disconnected for non-server reason. Exiting.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to connect to server. Retry? (y/n): ");
                    Logger.Warn("Failed to connect to server. Retry? (y/n): ");
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                    {
                        lastStatus = null;
                        Logger.Info("User chose to retry connection.");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Exiting.");
                        Logger.Info("Exiting.");
                        return;
                    }
                }
            }
        }

        private static async Task HandleSpecialCommand(string[] args)
        {
            var command = args[0].ToLowerInvariant();
            
            switch (command)
            {
                case "--migrate":
                    await HandleMigrateCommand(args);
                    break;
                    
                case "--analyze":
                    await HandleAnalyzeCommand(args);
                    break;
                    
                case "--analyze-activity":
                    await HandleAnalyzeActivityCommand(args);
                    break;
                    
                case "--help":
                case "--h":
                    ShowHelp();
                    break;
                    
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Use --help to see available commands.");
                    break;
            }
        }

        private static async Task HandleMigrateCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: --migrate <adb_file_or_directory> [output_path] [--compress]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --compress    Compress output to CVR format (7z compressed)");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  AeroDebrief.CLI.exe --migrate recording.adb");
                Console.WriteLine("  AeroDebrief.CLI.exe --migrate recording.adb output.db");
                Console.WriteLine("  AeroDebrief.CLI.exe --migrate recording.adb --compress");
                Console.WriteLine(@"  AeroDebrief.CLI.exe --migrate C:\Recordings\ --compress");
                return;
            }

            var inputPath = args[1];
            string? outputPath = null;
            bool compressToCvr = false;
            
            // Parse arguments
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--compress")
                {
                    compressToCvr = true;
                }
                else if (outputPath == null)
                {
                    outputPath = args[i];
                }
            }

            Console.WriteLine("?? ADB ? SQLite Migration Tool");
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine("??  WARNING: This is a ONE-WAY migration!");
            Console.WriteLine("??  ADB files will remain, but won't be used after migration.");
            if (compressToCvr)
            {
                Console.WriteLine("?? CVR compression: ENABLED");
            }
            Console.WriteLine();
            
            try
            {
                var converter = new AeroDebrief.Core.Storage.AdbToDatabaseConverter();

                // Check if input is a directory or file
                if (Directory.Exists(inputPath))
                {
                    // Batch conversion
                    var adbFiles = Directory.GetFiles(inputPath, "*.adb", SearchOption.AllDirectories);
                    
                    if (adbFiles.Length == 0)
                    {
                        Console.WriteLine($"No .adb files found in: {inputPath}");
                        return;
                    }

                    Console.WriteLine($"Found {adbFiles.Length} ADB file(s) to convert");
                    Console.WriteLine();

                    var batchProgress = new Progress<AeroDebrief.Core.Storage.BatchConversionProgress>(p =>
                    {
                        var fileName = Path.GetFileName(p.CurrentFile);
                        Console.Write($"\r[{p.CompletedFiles}/{p.TotalFiles}] {fileName,-40} {p.CurrentFilePercent,3}% - {p.CurrentStage,-30}");
                    });

                    var results = await converter.ConvertBatchAsync(adbFiles, batchProgress);

                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("=" + new string('=', 60));
                    Console.WriteLine($"? Batch conversion complete:");
                    Console.WriteLine($"   Successful: {results.Count(r => r.Success)}/{results.Count}");
                    Console.WriteLine($"   Failed: {results.Count(r => !r.Success)}");
                    
                    var totalSourceSize = results.Sum(r => r.SourceSizeBytes);
                    var totalOutputSize = results.Sum(r => r.OutputSizeBytes);
                    var totalPackets = results.Sum(r => r.TotalPackets);
                    var avgCompression = results.Where(r => r.Success).Average(r => r.CompressionRatio);
                    
                    Console.WriteLine($"   Total packets: {totalPackets:N0}");
                    Console.WriteLine($"   Total source: {totalSourceSize / 1024.0 / 1024.0:F1} MB");
                    Console.WriteLine($"   Total output: {totalOutputSize / 1024.0 / 1024.0:F1} MB");
                    Console.WriteLine($"   Avg compression: {avgCompression:F1}%");
                    
                    if (results.Any(r => !r.Success))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Failed files:");
                        foreach (var failed in results.Where(r => !r.Success))
                        {
                            Console.WriteLine($"  - {failed.SourceFile}: {failed.Error}");
                        }
                    }
                }
                else if (File.Exists(inputPath))
                {
                    // Single file conversion
                    var progress = new Progress<AeroDebrief.Core.Storage.ConversionProgress>(p =>
                    {
                        Console.Write($"\r{p.Stage,-30} [{p.Percent,3}%] {p.PacketsProcessed:N0} packets");
                    });

                    var result = await converter.ConvertAsync(inputPath, outputPath, compressToCvr, progress: progress);

                    Console.WriteLine();
                    Console.WriteLine();

                    if (result.Success)
                    {
                        Console.WriteLine("=" + new string('=', 60));
                        Console.WriteLine($"? Conversion successful!");
                        Console.WriteLine($"   Output: {result.OutputFile}");
                        Console.WriteLine($"   Packets: {result.TotalPackets:N0}");
                        Console.WriteLine($"   Duration: {result.Duration.TotalSeconds:F1}s");
                        Console.WriteLine($"   Speed: {result.PacketsPerSecond:N0} packets/sec");
                        Console.WriteLine($"   Source: {result.SourceSizeBytes / 1024.0 / 1024.0:F1} MB");
                        Console.WriteLine($"   Output: {result.OutputSizeBytes / 1024.0 / 1024.0:F1} MB");
                        Console.WriteLine($"   Compression: {result.CompressionRatio:F1}% smaller");
                        Console.WriteLine();
                        Console.WriteLine($"?? You can now delete the ADB file: {inputPath}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"? Conversion failed: {result.Error}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine($"? Input path not found: {inputPath}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"? Migration failed: {ex.Message}");
                Console.ResetColor();
                Logger.Error(ex, "Migration failed");
            }
        }

        private static async Task HandleAnalyzeCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: --analyze <file_path> [--export <wav_file>]");
                return;
            }

            var filePath = args[1];
            string? exportPath = null;
            
            // Check for export option
            for (int i = 2; i < args.Length; i += 2)
            {
                if (i + 1 < args.Length && args[i].ToLowerInvariant() == "--export")
                {
                    exportPath = args[i + 1];
                }
            }

            Console.WriteLine($"=== Analyzing Recording File ===");
            Console.WriteLine($"File: {filePath}");
            
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"? File not found: {filePath}");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine("Analyzing file structure and content...");
                var analysis = Core.Analysis.FileAnalyzer.AnalyzeAudioActivity(filePath);
                
                Console.WriteLine("\n" + analysis.ToString());
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("? Analysis complete");
                Console.ResetColor();

                // Export to WAV if requested
                if (!string.IsNullOrEmpty(exportPath))
                {
                    Console.WriteLine($"\nExporting audio to WAV: {exportPath}");
                    await Helpers.ExportToWavAsync(filePath, exportPath, 100);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"? Audio exported successfully to: {exportPath}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"? Analysis failed: {ex.Message}");
                Console.ResetColor();
                Logger.Error(ex, "File analysis failed");
            }
        }

        private static async Task HandleAnalyzeActivityCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: --analyze-activity <file_path> [options]");
                Console.WriteLine("Options:");
                Console.WriteLine("  --threshold <value>     Silence threshold (0-32767, default: 500)");
                Console.WriteLine("  --min-duration <ms>     Minimum activity duration in milliseconds (default: 100)");
                Console.WriteLine("  --export-csv <path>     Export activity periods to CSV file");
                Console.WriteLine("  --player <name>         Show only activity for specific player");
                Console.WriteLine("  --frequency <mhz>       Show only activity for specific frequency");
                return;
            }

            var filePath = args[1];
            var silenceThreshold = 500;
            var minDurationMs = 100;
            string? exportCsvPath = null;
            string? playerFilter = null;
            double? frequencyFilter = null;
            
            // Parse options
            for (int i = 2; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length) break;
                
                switch (args[i].ToLowerInvariant())
                {
                    case "--threshold":
                        if (int.TryParse(args[i + 1], out var threshold))
                            silenceThreshold = Math.Clamp(threshold, 0, 32767);
                        break;
                    case "--min-duration":
                        if (int.TryParse(args[i + 1], out var minDur))
                            minDurationMs = Math.Max(minDur, 10);
                        break;
                    case "--export-csv":
                        exportCsvPath = args[i + 1];
                        break;
                    case "--player":
                        playerFilter = args[i + 1];
                        break;
                    case "--frequency":
                        if (double.TryParse(args[i + 1], out var freq))
                            frequencyFilter = freq;
                        break;
                }
            }

            Console.WriteLine($"=== Audio Activity Analysis ===");
            Console.WriteLine($"File: {filePath}");
            Console.WriteLine($"Silence Threshold: {silenceThreshold}/32767");
            Console.WriteLine($"Minimum Duration: {minDurationMs}ms");
            if (!string.IsNullOrEmpty(playerFilter))
                Console.WriteLine($"Player Filter: {playerFilter}");
            if (frequencyFilter.HasValue)
                Console.WriteLine($"Frequency Filter: {frequencyFilter:F1}MHz");
            Console.WriteLine();
            
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"? File not found: {filePath}");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine("Analyzing audio activity...");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var analysis = Core.Analysis.FileAnalyzer.AnalyzeAudioActivity(
                    filePath, 
                    silenceThreshold, 
                    TimeSpan.FromMilliseconds(minDurationMs)
                );
                
                stopwatch.Stop();
                Console.WriteLine($"Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine();
                
                // Display summary
                Console.WriteLine(analysis.ToString());
                
                // Apply filters and show detailed results
                var periodsToShow = analysis.ActivityPeriods.AsEnumerable();
                
                if (!string.IsNullOrEmpty(playerFilter))
                {
                    periodsToShow = periodsToShow.Where(p => 
                        p.Players.Any(player => player.Contains(playerFilter, StringComparison.OrdinalIgnoreCase)));
                }
                
                if (frequencyFilter.HasValue)
                {
                    periodsToShow = periodsToShow.Where(p => 
                        p.Frequencies.Any(f => Math.Abs(f - frequencyFilter.Value) < 0.1));
                }
                
                var filteredPeriods = periodsToShow.ToList();
                
                if (filteredPeriods.Count != analysis.ActivityPeriods.Count)
                {
                    Console.WriteLine($"\nFiltered Results ({filteredPeriods.Count} periods):");
                    foreach (var period in filteredPeriods.Take(50))
                    {
                        var players = string.Join(", ", period.Players.Take(3));
                        if (period.Players.Count > 3) players += "...";
                        
                        var frequencies = string.Join(", ", period.Frequencies.Take(2).Select(f => f.ToString("F1")));
                        if (period.Frequencies.Count > 2) frequencies += "...";
                        
                        Console.WriteLine($"  {period.StartTime:HH:mm:ss.fff} - {period.EndTime:HH:mm:ss.fff} " +
                                         $"({period.Duration.TotalSeconds:F1}s) [{players}] @ {frequencies}MHz " +
                                         $"[Max: {period.MaxAmplitude}, Avg: {period.AverageAmplitude}]");
                    }
                    
                    if (filteredPeriods.Count > 50)
                    {
                        Console.WriteLine($"  ... and {filteredPeriods.Count - 50} more periods");
                    }
                }
                
                // Export to CSV if requested
                if (!string.IsNullOrEmpty(exportCsvPath))
                {
                    Console.WriteLine($"\nExporting to CSV: {exportCsvPath}");
                    await ExportActivityToCsvAsync(analysis, exportCsvPath, playerFilter, frequencyFilter);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"? Activity data exported to: {exportCsvPath}");
                    Console.ResetColor();
                }
                
                // Show recommendations
                Console.WriteLine($"\nRecommendations:");
                if (analysis.ActivityPercentage < 5)
                {
                    Console.WriteLine($"  - Very low activity ({analysis.ActivityPercentage:F1}%). Consider lowering silence threshold or checking recording quality.");
                }
                else if (analysis.ActivityPercentage > 80)
                {
                    Console.WriteLine($"  - Very high activity ({analysis.ActivityPercentage:F1}%). Consider raising silence threshold to filter background noise.");
                }
                else
                {
                    Console.WriteLine($"  - Good activity level ({analysis.ActivityPercentage:F1}%). Threshold appears appropriate.");
                }
                
                if (analysis.ActivityPeriods.Count > 1000)
                {
                    Console.WriteLine($"  - Many short activity periods ({analysis.ActivityPeriods.Count}). Consider increasing minimum duration.");
                }
                
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"? Activity analysis failed: {ex.Message}");
                Console.ResetColor();
                Logger.Error(ex, "Activity analysis failed");
            }
        }
        
        private static async Task ExportActivityToCsvAsync(
            Core.Analysis.AudioActivityAnalysis analysis, 
            string csvPath, 
            string? playerFilter, 
            double? frequencyFilter)
        {
            var periods = analysis.ActivityPeriods.AsEnumerable();
            
            // Apply filters
            if (!string.IsNullOrEmpty(playerFilter))
            {
                periods = periods.Where(p => 
                    p.Players.Any(player => player.Contains(playerFilter, StringComparison.OrdinalIgnoreCase)));
            }
            
            if (frequencyFilter.HasValue)
            {
                periods = periods.Where(p => 
                    p.Frequencies.Any(f => Math.Abs(f - frequencyFilter.Value) < 0.1));
            }
            
            using var writer = new StreamWriter(csvPath);
            
            // Write header
            await writer.WriteLineAsync("StartTime,EndTime,DurationSeconds,PrimaryPlayer,PrimaryFrequency,MaxAmplitude,AverageAmplitude,PacketCount,AllPlayers,AllFrequencies");
            
            // Write data
            foreach (var period in periods)
            {
                var allPlayers = string.Join(";", period.Players);
                var allFrequencies = string.Join(";", period.Frequencies.Select(f => f.ToString("F1")));
                
                await writer.WriteLineAsync($"{period.StartTime:yyyy-MM-dd HH:mm:ss.fff}," +
                                          $"{period.EndTime:yyyy-MM-dd HH:mm:ss.fff}," +
                                          $"{period.Duration.TotalSeconds:F3}," +
                                          $"\"{period.PrimaryPlayer}\"," +
                                          $"{period.PrimaryFrequency:F1}," +
                                          $"{period.MaxAmplitude}," +
                                          $"{period.AverageAmplitude}," +
                                          $"{period.PacketCount}," +
                                          $"\"{allPlayers}\"," +
                                          $"\"{allFrequencies}\"");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("SRS Recording Client - Usage:");
            Console.WriteLine();
            Console.WriteLine("Recording Mode (Phase 3 - SQLite Recording):");
            Console.WriteLine("  DCS-SRS-RecordingClient.exe <server_ip> <port>");
            Console.WriteLine("  Example: DCS-SRS-RecordingClient.exe 192.168.1.100 5002");
            Console.WriteLine();
            Console.WriteLine("  ?? Note: Records directly to SQLite database");
            Console.WriteLine("  ??? Output: Compressed CVR format by default");
            Console.WriteLine("  ??  Settings: Edit configs/recorder.cfg to change format");
            Console.WriteLine();
            Console.WriteLine("  Available settings:");
            Console.WriteLine("    OutputFormat = \"CVR\" (compressed) or \"Uncompressed\" (SQLite)");
            Console.WriteLine("    AutoCompress = true (compress on stop) or false");
            Console.WriteLine("    EnableLivePlayback = false (Phase 4 feature)");
            Console.WriteLine();
            Console.WriteLine("File Migration:");
            Console.WriteLine("  --migrate <adb_file_or_directory> [output_path]");
            Console.WriteLine("    Convert legacy ADB file(s) to SQLite format");
            Console.WriteLine("    Examples:");
            Console.WriteLine("      --migrate recording.adb");
            Console.WriteLine("      --migrate recording.adb output.db");
            Console.WriteLine(@"      --migrate C:\Recordings\");
            Console.WriteLine();
            Console.WriteLine("Audio Analysis:");
            Console.WriteLine("  --analyze <file_path> [--export <wav_file>]");
            Console.WriteLine("    Analyze recording file and optionally export to WAV");
            Console.WriteLine();
            Console.WriteLine("  --analyze-activity <file_path> [options]");
            Console.WriteLine("    Analyze voice activity periods in recording");
            Console.WriteLine("    Options:");
            Console.WriteLine("      --threshold <value>    : Amplitude threshold (default: 500)");
            Console.WriteLine("      --min-duration <ms>    : Minimum duration in ms (default: 300)");
            Console.WriteLine("      --player <name>        : Filter by player name");
            Console.WriteLine("      --frequency <freq>     : Filter by frequency (MHz)");
            Console.WriteLine("      --csv <output.csv>     : Export results to CSV");
            Console.WriteLine();
            Console.WriteLine("Phase 3 Features:");
            Console.WriteLine("  ? Direct SQLite recording (no ADB conversion needed)");
            Console.WriteLine("  ? Automatic CVR compression on stop");
            Console.WriteLine("  ? 60% smaller files with CVR format");
            Console.WriteLine("  ? Real-time metadata indexing");
            Console.WriteLine("  ? 3x faster write performance");
            Console.WriteLine("  ? Live playback during recording (Phase 4)");
        }
    }
}
