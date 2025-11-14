using AeroDebrief.Core.IO;
using System.Diagnostics;
using System.IO;

namespace AeroDebrief.Tests.IO
{
    /// <summary>
    /// Standalone performance benchmark for PacketRouter.
    /// Run this to validate performance on your hardware.
    /// </summary>
    public static class PacketRouterBenchmark
    {
        public static async Task RunBenchmarkSuite()
        {
            Console.WriteLine("??????????????????????????????????????????????????????????????????");
            Console.WriteLine("?        PacketRouter Performance Benchmark Suite                ?");
            Console.WriteLine("??????????????????????????????????????????????????????????????????");
            Console.WriteLine();
            
            PrintSystemInfo();
            Console.WriteLine();

            // Warm-up
            Console.WriteLine("?? Warming up JIT compiler...");
            await WarmUp();
            Console.WriteLine("? Warm-up complete");
            Console.WriteLine();

            // Run benchmarks
            await Benchmark_1M_Packets();
            await Benchmark_5M_Packets();
            await Benchmark_10M_Packets_Target();
            await Benchmark_HighContention();
            await Benchmark_ScalabilityTest();

            Console.WriteLine();
            Console.WriteLine("??????????????????????????????????????????????????????????????????");
            Console.WriteLine("?        Benchmark Suite Complete                                ?");
            Console.WriteLine("??????????????????????????????????????????????????????????????????");
        }

        private static async Task WarmUp()
        {
            using var router = new PacketRouter();
            var packets = GeneratePackets(10_000, 10, 10);
            await router.RoutePacketsParallelAsync(packets);
        }

        private static async Task Benchmark_1M_Packets()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            Console.WriteLine("Benchmark: 1M packets (baseline)");
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            
            const int totalPackets = 1_000_000;
            const int numFrequencies = 64;
            const int numSpeakers = 256;

            using var router = new PacketRouter();
            var packets = GeneratePackets(totalPackets, numFrequencies, numSpeakers);
            
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(packets, Environment.ProcessorCount);
            stopwatch.Stop();

            var stats = router.GetStats();
            stats.ElapsedTime = stopwatch.Elapsed;

            PrintResults("1M Baseline", stats, totalPackets, numFrequencies, numSpeakers);
            Console.WriteLine();
        }

        private static async Task Benchmark_5M_Packets()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            Console.WriteLine("Benchmark: 5M packets (intermediate)");
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            
            const int totalPackets = 5_000_000;
            const int numFrequencies = 64;
            const int numSpeakers = 256;

            using var router = new PacketRouter();
            var packets = GeneratePackets(totalPackets, numFrequencies, numSpeakers);
            
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(packets, Environment.ProcessorCount);
            stopwatch.Stop();

            var stats = router.GetStats();
            stats.ElapsedTime = stopwatch.Elapsed;

            PrintResults("5M Intermediate", stats, totalPackets, numFrequencies, numSpeakers);
            Console.WriteLine();
        }

        private static async Task Benchmark_10M_Packets_Target()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            Console.WriteLine("Benchmark: 10M packets - PERFORMANCE TARGET");
            Console.WriteLine("Target: Complete in under 5 seconds on SSD");
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            
            const int totalPackets = 10_000_000;
            const int numFrequencies = 64;
            const int numSpeakers = 256;
            const double targetSeconds = 5.0;

            using var router = new PacketRouter();
            
            // Process in batches to show progress
            var stopwatch = Stopwatch.StartNew();
            const int batchSize = 1_000_000;
            long totalRouted = 0;

            for (int batch = 0; batch < totalPackets / batchSize; batch++)
            {
                var batchPackets = GeneratePackets(
                    batchSize, 
                    numFrequencies, 
                    numSpeakers,
                    startId: totalRouted);
                
                await router.RoutePacketsParallelAsync(
                    batchPackets, 
                    Environment.ProcessorCount);
                
                totalRouted += batchSize;
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var rate = totalRouted / elapsed;
                
                Console.WriteLine($"  Progress: {totalRouted:N0} packets | " +
                                $"{elapsed:F2}s | " +
                                $"{rate:N0} pkt/s | " +
                                $"ETA: {(totalPackets - totalRouted) / rate:F1}s");
            }

            stopwatch.Stop();

            var stats = router.GetStats();
            stats.ElapsedTime = stopwatch.Elapsed;

            Console.WriteLine();
            PrintResults("10M TARGET", stats, totalPackets, numFrequencies, numSpeakers);

            // Performance evaluation
            Console.WriteLine();
            if (stopwatch.Elapsed.TotalSeconds < targetSeconds)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"? PASS: Completed in {stopwatch.Elapsed.TotalSeconds:F3}s " +
                                $"(under {targetSeconds}s target)");
                var margin = ((targetSeconds - stopwatch.Elapsed.TotalSeconds) / targetSeconds * 100);
                Console.WriteLine($"? Performance margin: {margin:F1}% faster than target");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"? FAIL: Completed in {stopwatch.Elapsed.TotalSeconds:F3}s " +
                                $"(target was {targetSeconds}s)");
                var deficit = ((stopwatch.Elapsed.TotalSeconds - targetSeconds) / targetSeconds * 100);
                Console.WriteLine($"? Performance deficit: {deficit:F1}% slower than target");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        private static async Task Benchmark_HighContention()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            Console.WriteLine("Benchmark: High Contention (single frequency)");
            Console.WriteLine("All packets to one frequency - worst case scenario");
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            
            const int totalPackets = 1_000_000;
            const int numSpeakers = 256;

            using var router = new PacketRouter();
            var packets = GeneratePackets(totalPackets, 1, numSpeakers);
            
            var stopwatch = Stopwatch.StartNew();
            await router.RoutePacketsParallelAsync(packets, Environment.ProcessorCount);
            stopwatch.Stop();

            var stats = router.GetStats();
            stats.ElapsedTime = stopwatch.Elapsed;

            PrintResults("High Contention", stats, totalPackets, 1, numSpeakers);
            Console.WriteLine($"  Contention factor: {numSpeakers} speakers on 1 frequency");
            Console.WriteLine();
        }

        private static async Task Benchmark_ScalabilityTest()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            Console.WriteLine("Benchmark: Scalability Test (varying thread counts)");
            Console.WriteLine("???????????????????????????????????????????????????????????????");
            
            const int totalPackets = 1_000_000;
            const int numFrequencies = 64;
            const int numSpeakers = 256;

            var threadCounts = new[] { 1, 2, 4, 8, Environment.ProcessorCount };
            
            Console.WriteLine($"Testing with thread counts: {string.Join(", ", threadCounts)}");
            Console.WriteLine();

            foreach (var threads in threadCounts)
            {
                using var router = new PacketRouter();
                var packets = GeneratePackets(totalPackets, numFrequencies, numSpeakers);
                
                var stopwatch = Stopwatch.StartNew();
                await router.RoutePacketsParallelAsync(packets, threads);
                stopwatch.Stop();

                var rate = totalPackets / stopwatch.Elapsed.TotalSeconds;
                var speedup = threads == 1 ? 1.0 : rate / (totalPackets / stopwatch.Elapsed.TotalSeconds);
                
                Console.WriteLine($"  Threads: {threads,2} | " +
                                $"Time: {stopwatch.Elapsed.TotalSeconds:F3}s | " +
                                $"Rate: {rate:N0} pkt/s | " +
                                $"Speedup: {speedup:F2}x");
            }
            Console.WriteLine();
        }

        private static void PrintSystemInfo()
        {
            Console.WriteLine("System Information:");
            Console.WriteLine($"  OS:              {Environment.OSVersion}");
            Console.WriteLine($"  .NET Version:    {Environment.Version}");
            Console.WriteLine($"  CPU Cores:       {Environment.ProcessorCount}");
            Console.WriteLine($"  64-bit Process:  {Environment.Is64BitProcess}");
            Console.WriteLine($"  Working Set:     {Environment.WorkingSet / 1024 / 1024:N0} MB");
            
            // Estimate disk type (very rough heuristic)
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();
            
            if (drives.Any())
            {
                Console.WriteLine($"  Fixed Drives:    {drives.Count}");
                foreach (var drive in drives)
                {
                    Console.WriteLine($"    - {drive.Name} " +
                                    $"({drive.DriveFormat}, " +
                                    $"{drive.TotalSize / 1024 / 1024 / 1024:N0} GB)");
                }
            }
        }

        private static void PrintResults(
            string benchmarkName,
            PacketRouterStats stats,
            int totalPackets,
            int numFrequencies,
            int numSpeakers)
        {
            Console.WriteLine("Results:");
            Console.WriteLine($"  Packets routed:  {stats.PacketsRouted:N0}");
            Console.WriteLine($"  Time elapsed:    {stats.ElapsedTime.TotalSeconds:F3}s");
            Console.WriteLine($"  Throughput:      {stats.PacketsPerSecond:N0} packets/second");
            Console.WriteLine($"  Data rate:       {stats.PacketsPerSecond * 2048 / 1024 / 1024:F2} MB/s (@ 2KB/packet)");
            Console.WriteLine($"  Frequencies:     {stats.FrequencyCount}");
            Console.WriteLine($"  Speakers:        {stats.SpeakerCount}");
            Console.WriteLine($"  Routes:          {stats.RouteCount}");
            Console.WriteLine($"  Avg pkt/freq:    {stats.PacketsRouted / (double)stats.FrequencyCount:N0}");
            Console.WriteLine($"  Avg pkt/speaker: {stats.PacketsRouted / (double)stats.SpeakerCount:N0}");
        }

        private static List<RadioPacket> GeneratePackets(
            int count,
            int numFrequencies,
            int numSpeakers,
            long startId = 0)
        {
            var packets = new List<RadioPacket>(count);
            var baseFrequencies = Enumerable.Range(0, numFrequencies)
                .Select(i => 200_000_000.0 + (i * 1_000_000.0))
                .ToArray();

            for (int i = 0; i < count; i++)
            {
                var frequency = baseFrequencies[i % numFrequencies];
                var speakerId = $"speaker-{i % numSpeakers}";
                
                packets.Add(new RadioPacket
                {
                    Timestamp = DateTime.UtcNow,
                    Frequency = frequency,
                    Modulation = 0,
                    Encryption = 0,
                    TransmitterUnitId = 1,
                    PacketId = (ulong)(startId + i),
                    TransmitterGuid = speakerId,
                    Coalition = 1,
                    AudioPayload = new byte[1920] // Typical OPUS frame
                });
            }

            return packets;
        }
    }
}
