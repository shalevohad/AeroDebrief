using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core;
using AeroDebrief.Core.Models;

/// <summary>
/// Command-line tool to export specific frequencies from SRS recordings to WAV files
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== AeroDebrief Frequency Export Tool ===");
            Console.WriteLine();

            // Parse arguments
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string recordingPath = args[0];
            double freqMHz = double.Parse(args[1]);
            string? outputPath = args.Length > 2 ? args[2] : null;

            // Validate input file
            if (!File.Exists(recordingPath))
            {
                Console.WriteLine($"❌ Error: Recording file not found: {recordingPath}");
                return 1;
            }

            // Convert frequency to Hz
            double freqHz = freqMHz * 1_000_000.0;

            // Default output path
            if (string.IsNullOrEmpty(outputPath))
            {
                var dir = Path.GetDirectoryName(recordingPath) ?? Environment.CurrentDirectory;
                var fileName = $"freq_{freqMHz:F1}_export.wav";
                outputPath = Path.Combine(dir, fileName);
            }

            Console.WriteLine($"📂 Input:  {Path.GetFileName(recordingPath)}");
            Console.WriteLine($"🎵 Frequency: {freqMHz:F1} MHz ({freqHz:F0} Hz)");
            Console.WriteLine($"📤 Output: {Path.GetFileName(outputPath)}");
            Console.WriteLine();

            // Create AudioPacketReader
            Console.WriteLine("🔍 Loading recording file...");
            using var reader = new AudioPacketReader(recordingPath);

            // Get all available frequencies
            Console.WriteLine("🔍 Scanning for frequencies...");
            var allFreqs = reader.GetAllFrequencyModulations();
            
            Console.WriteLine($"✅ Found {allFreqs.Count} frequency-modulation combinations");
            Console.WriteLine();

            // Find target frequency (with 1kHz tolerance)
            var targetFreq = allFreqs.FirstOrDefault(f => Math.Abs(f.Frequency - freqHz) < 1000.0);

            if (targetFreq == null)
            {
                Console.WriteLine($"❌ Frequency {freqMHz:F1} MHz not found in recording!");
                Console.WriteLine();
                Console.WriteLine("Available frequencies:");
                
                var sortedFreqs = allFreqs
                    .OrderBy(f => f.Frequency)
                    .GroupBy(f => f.Frequency)
                    .ToList();

                foreach (var group in sortedFreqs)
                {
                    var freqMhz = group.Key / 1_000_000.0;
                    var mods = string.Join(", ", group.Select(f => f.Modulation));
                    var packets = group.Sum(f => f.Players.Sum(p => p.PacketCount));
                    Console.WriteLine($"  - {freqMhz:F3} MHz ({mods}) - {packets} packets");
                }
                
                return 1;
            }

            Console.WriteLine($"✅ Found frequency: {targetFreq.Frequency / 1_000_000.0:F3} MHz ({targetFreq.Modulation})");
            
            if (targetFreq.Players.Any())
            {
                Console.WriteLine($"   Players using this frequency:");
                foreach (var player in targetFreq.Players.Take(5))
                {
                    Console.WriteLine($"     - {player.Name} ({player.Aircraft}) - {player.PacketCount} packets");
                }
                if (targetFreq.Players.Count > 5)
                {
                    Console.WriteLine($"     ... and {targetFreq.Players.Count - 5} more players");
                }
            }
            Console.WriteLine();

            // Set frequency filter
            Console.WriteLine("🎛️  Applying frequency filter...");
            reader.SetFrequencyFilter(new[] { targetFreq });

            // Export to WAV
            Console.WriteLine($"📤 Exporting to WAV (this may take a moment)...");
            await reader.ExportSelectedFrequenciesToWavAsync(outputPath);

            Console.WriteLine();
            Console.WriteLine("✅ Export complete!");
            Console.WriteLine();
            Console.WriteLine("Created files:");
            
            var beforePath = Path.ChangeExtension(outputPath, null) + "_before.wav";
            var afterPath = Path.ChangeExtension(outputPath, null) + "_after.wav";
            
            if (File.Exists(beforePath))
            {
                var beforeSize = new FileInfo(beforePath).Length;
                Console.WriteLine($"  📄 {Path.GetFileName(beforePath)} ({FormatFileSize(beforeSize)})");
                Console.WriteLine($"     Raw decoded audio (before processing)");
            }
            
            if (File.Exists(afterPath))
            {
                var afterSize = new FileInfo(afterPath).Length;
                Console.WriteLine($"  📄 {Path.GetFileName(afterPath)} ({FormatFileSize(afterSize)})");
                Console.WriteLine($"     Processed audio (with effects applied)");
            }

            Console.WriteLine();
            Console.WriteLine("🎧 You can now listen to these files in any audio player!");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"   Details: {ex.GetType().Name}");
            return 1;
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  FreqExport <recording.raw> <frequency_mhz> [output.wav]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  recording.raw    Path to the SRS recording file");
        Console.WriteLine("  frequency_mhz    Frequency to export (in MHz, e.g., 303.1)");
        Console.WriteLine("  output.wav       Optional output path (default: freq_XXX_export.wav)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  FreqExport recording.raw 303.1");
        Console.WriteLine("  FreqExport recording.raw 251.0 output.wav");
        Console.WriteLine("  FreqExport \"C:\\Recordings\\mission.raw\" 303.1");
        Console.WriteLine();
        Console.WriteLine("Output:");
        Console.WriteLine("  Creates two WAV files:");
        Console.WriteLine("    - <name>_before.wav  Raw decoded audio");
        Console.WriteLine("    - <name>_after.wav   Processed audio with effects");
    }

    static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}
