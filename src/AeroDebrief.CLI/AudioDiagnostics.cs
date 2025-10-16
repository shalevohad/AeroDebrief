using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AeroDebrief.Core.Analysis;

namespace AeroDebrief.CLI{
    public static class AudioDiagnostics
    {
        public class AnalysisResult
        {
            public List<string> PotentialIssues { get; set; } = new List<string>();
            private readonly AudioActivityAnalysis _analysis;

            public AnalysisResult(AudioActivityAnalysis analysis)
            {
                _analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            }

            public override string ToString() => _analysis.ToString();
        }

        public static Task PlayTestToneAsync(double frequency = 440.0, double durationSeconds = 2.0)
        {
            // Best-effort simple implementation using Console.Beep on supported platforms
            return Task.Run(() =>
            {
                try
                {
                    var freq = (int)Math.Clamp(frequency, 37, 32767);
                    var dur = (int)Math.Max(1, durationSeconds * 1000);
                    Console.Beep(freq, dur);
                }
                catch
                {
                    // Console.Beep may not be supported on all platforms; ignore failures
                }
            });
        }

        public static Task<AnalysisResult> AnalyzeRecordedFileAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var analysis = FileAnalyzer.AnalyzeAudioActivity(filePath);
                var result = new AnalysisResult(analysis);

                // Basic potential issue detection
                if (analysis.TotalPackets == 0)
                    result.PotentialIssues.Add("No packets found in file");

                if (analysis.PacketsWithAudio == 0 && analysis.TotalPackets > 0)
                    result.PotentialIssues.Add("No audio payloads detected in packets");

                return result;
            });
        }
    }
}
