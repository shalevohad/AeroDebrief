using Xunit;
using FluentAssertions;
using AeroDebrief.Core.Audio;

namespace AeroDebrief.Tests.Audio
{
    public class PilotFilterTests
    {
        private const int SampleRate = 48000;
        private const int FadeSamples = 64; // 1.33ms fade at 48kHz
        private const float FadeIncrement = 1.0f / FadeSamples;

        [Fact]
        public void Constructor_InitializesEmptyFilter()
        {
            // Arrange & Act
            var filter = new PilotFilter();
            
            // Assert
            var stats = filter.GetStats();
            stats.TotalPilots.Should().Be(0);
            stats.SoloPilots.Should().Be(0);
            stats.MutedPilots.Should().Be(0);
            stats.BlockedPilots.Should().Be(0);
        }

        [Fact]
        public void SetPilotMode_AllowMode_PassesAudioThrough()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateSineWave(1000, 440, SampleRate); // 1 second of 440Hz tone

            // Act
            filter.SetPilotMode(pilotId, PilotFilterMode.Allow);
            var output = filter.Process(pilotId, input);

            // Assert
            output.Should().HaveCount(input.Length);
            output.Should().BeEquivalentTo(input);
        }

        [Fact]
        public void SetPilotMode_BlockMode_SilencesAudio()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateSineWave(1000, 440, SampleRate);

            // Act
            filter.SetPilotMode(pilotId, PilotFilterMode.Block);
            
            // Process enough samples to complete fade-out
            var output = filter.Process(pilotId, input);

            // Assert
            output.Should().HaveCount(input.Length);
            
            // After fade-out (64 samples), audio should be completely silent
            for (int i = FadeSamples; i < output.Length; i++)
            {
                output[i].Should().Be(0.0f, $"sample {i} should be silent after fade-out");
            }
        }

        [Fact]
        public void SetPilotMode_MuteMode_SilencesAudio()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateSineWave(1000, 440, SampleRate);

            // Act
            filter.SetPilotMode(pilotId, PilotFilterMode.Mute);
            var output = filter.Process(pilotId, input);

            // Assert
            output.Should().HaveCount(input.Length);
            
            // After fade-out, should be silent
            for (int i = FadeSamples; i < output.Length; i++)
            {
                output[i].Should().Be(0.0f);
            }
        }

        [Fact]
        public void SoloMode_OnePilotSoloed_OnlySoloPilotIsAudible()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilot1 = "Pilot1";
            var pilot2 = "Pilot2";
            var input1 = GenerateSineWave(1000, 440, SampleRate);
            var input2 = GenerateSineWave(1000, 550, SampleRate);

            // Act
            filter.SetPilotMode(pilot1, PilotFilterMode.Solo);
            filter.SetPilotMode(pilot2, PilotFilterMode.Allow);

            var output1 = filter.Process(pilot1, input1);
            var output2 = filter.Process(pilot2, input2);

            // Assert
            // Pilot1 (soloed) should be audible
            output1.Should().NotBeEquivalentTo(new float[output1.Length]);
            
            // Pilot2 (not soloed) should be silent after fade
            for (int i = FadeSamples; i < output2.Length; i++)
            {
                output2[i].Should().Be(0.0f);
            }
        }

        [Fact]
        public void SoloMode_MultiplePilotsSoloed_AllSolosAreAudible()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilot1 = "Pilot1";
            var pilot2 = "Pilot2";
            var pilot3 = "Pilot3";
            var input = GenerateSineWave(1000, 440, SampleRate);

            // Act
            filter.SetPilotMode(pilot1, PilotFilterMode.Solo);
            filter.SetPilotMode(pilot2, PilotFilterMode.Solo);
            filter.SetPilotMode(pilot3, PilotFilterMode.Allow);

            var output1 = filter.Process(pilot1, input);
            var output2 = filter.Process(pilot2, input);
            var output3 = filter.Process(pilot3, input);

            // Assert
            // Both solo pilots should be audible
            output1.Should().NotBeEquivalentTo(new float[output1.Length]);
            output2.Should().NotBeEquivalentTo(new float[output2.Length]);
            
            // Non-solo pilot should be silent
            for (int i = FadeSamples; i < output3.Length; i++)
            {
                output3[i].Should().Be(0.0f);
            }
        }

        [Fact]
        public void FadeOut_AppliesSmoothTransition_64Samples()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateConstantLevel(1.0f, 1000); // Constant level for testing

            // Start with audible
            filter.SetPilotMode(pilotId, PilotFilterMode.Allow);
            var _ = filter.Process(pilotId, input);

            // Act - switch to mute, which triggers fade-out
            filter.SetPilotMode(pilotId, PilotFilterMode.Mute);
            var output = filter.Process(pilotId, input);

            // Assert - verify smooth fade
            for (int i = 0; i < FadeSamples && i < output.Length; i++)
            {
                // During fade-out, gain should decrease linearly
                float expectedGain = 1.0f - (i * FadeIncrement);
                float actualGain = output[i] / input[i];
                
                actualGain.Should().BeApproximately(expectedGain, 0.02f, 
                    $"sample {i} should have gain ~{expectedGain:F3}");
            }

            // After fade, should be silent
            for (int i = FadeSamples; i < output.Length; i++)
            {
                output[i].Should().Be(0.0f, $"sample {i} should be silent after fade");
            }
        }

        [Fact]
        public void FadeIn_AppliesSmoothTransition_64Samples()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateConstantLevel(1.0f, 1000);

            // Start with muted (silent)
            filter.SetPilotMode(pilotId, PilotFilterMode.Mute);
            var _ = filter.Process(pilotId, input);

            // Act - switch to allow, which triggers fade-in
            filter.SetPilotMode(pilotId, PilotFilterMode.Allow);
            var output = filter.Process(pilotId, input);

            // Assert - verify smooth fade-in
            for (int i = 0; i < FadeSamples && i < output.Length; i++)
            {
                // During fade-in, gain should increase linearly
                float expectedGain = i * FadeIncrement;
                float actualGain = Math.Abs(output[i] / input[i]);
                
                actualGain.Should().BeApproximately(expectedGain, 0.02f,
                    $"sample {i} should have gain ~{expectedGain:F3}");
            }

            // After fade, should be at full volume
            for (int i = FadeSamples; i < output.Length; i++)
            {
                float actualGain = Math.Abs(output[i] / input[i]);
                actualGain.Should().BeApproximately(1.0f, 0.01f,
                    $"sample {i} should be at full volume");
            }
        }

        [Fact]
        public async Task HighFrequencyToggling_20Hz_NoAudibleDiscontinuities()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            const int toggleRateHz = 20;
            const int testDurationMs = 100; // 100ms test
            const int toggleIntervalMs = 1000 / toggleRateHz; // 50ms per toggle
            const int samplesPerToggle = SampleRate * toggleIntervalMs / 1000;

            // Generate continuous sine wave
            var fullInput = GenerateSineWave(testDurationMs, 440, SampleRate);
            var output = new List<float>();

            // Act - toggle filter at 20Hz while processing
            int currentSample = 0;
            bool isMuted = false;

            while (currentSample < fullInput.Length)
            {
                // Toggle mode
                filter.SetPilotMode(pilotId, isMuted ? PilotFilterMode.Mute : PilotFilterMode.Allow);
                isMuted = !isMuted;

                // Process one toggle interval
                int samplesToProcess = Math.Min(samplesPerToggle, fullInput.Length - currentSample);
                var chunk = fullInput.Skip(currentSample).Take(samplesToProcess).ToArray();
                var processed = filter.Process(pilotId, chunk);
                
                output.AddRange(processed);
                currentSample += samplesToProcess;
            }

            // Assert - verify no audible discontinuities
            // Check for clicks (sharp transients) by measuring sample-to-sample differences
            var maxDelta = 0.0f;
            var clickCount = 0;
            const float clickThreshold = 0.5f; // 50% amplitude jump = click

            for (int i = 1; i < output.Count; i++)
            {
                float delta = Math.Abs(output[i] - output[i - 1]);
                maxDelta = Math.Max(maxDelta, delta);
                
                if (delta > clickThreshold)
                {
                    clickCount++;
                }
            }

            // Verify no clicks detected
            clickCount.Should().Be(0, "no clicks should be present with smooth fading");
            maxDelta.Should().BeLessThan(clickThreshold, 
                "maximum sample-to-sample change should be gradual");
        }

        [Fact]
        public void NumericDiscontinuityTest_MeasuresFadeQuality()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateConstantLevel(1.0f, 1000);

            // Start audible
            filter.SetPilotMode(pilotId, PilotFilterMode.Allow);
            _ = filter.Process(pilotId, input);

            // Act - trigger fade-out
            filter.SetPilotMode(pilotId, PilotFilterMode.Mute);
            var output = filter.Process(pilotId, input);

            // Assert - measure fade quality numerically
            var discontinuities = MeasureDiscontinuities(output);
            
            discontinuities.MaxSampleDelta.Should().BeLessThan(FadeIncrement + 0.01f,
                "max sample-to-sample change should not exceed fade increment");
            
            discontinuities.AverageFadeSlope.Should().BeApproximately(-FadeIncrement, 0.01f,
                "average fade slope should match expected fade rate");
            
            discontinuities.ClickCount.Should().Be(0,
                "no clicks should be detected during fade");
        }

        [Fact]
        public void ResetAll_ClearsAllPilotStates()
        {
            // Arrange
            var filter = new PilotFilter();
            filter.SetPilotMode("Pilot1", PilotFilterMode.Solo);
            filter.SetPilotMode("Pilot2", PilotFilterMode.Mute);
            filter.SetPilotMode("Pilot3", PilotFilterMode.Block);

            var statsBefore = filter.GetStats();
            statsBefore.TotalPilots.Should().Be(3);

            // Act
            filter.ResetAll();

            // Assert
            var statsAfter = filter.GetStats();
            statsAfter.TotalPilots.Should().Be(0);
            statsAfter.SoloPilots.Should().Be(0);
            statsAfter.MutedPilots.Should().Be(0);
            statsAfter.BlockedPilots.Should().Be(0);
        }

        [Fact]
        public void GetStats_ReturnsAccurateStatistics()
        {
            // Arrange
            var filter = new PilotFilter();
            
            // Act
            filter.SetPilotMode("Pilot1", PilotFilterMode.Solo);
            filter.SetPilotMode("Pilot2", PilotFilterMode.Solo);
            filter.SetPilotMode("Pilot3", PilotFilterMode.Mute);
            filter.SetPilotMode("Pilot4", PilotFilterMode.Block);
            filter.SetPilotMode("Pilot5", PilotFilterMode.Allow);

            var stats = filter.GetStats();

            // Assert
            stats.TotalPilots.Should().Be(5);
            stats.SoloPilots.Should().Be(2);
            stats.MutedPilots.Should().Be(1);
            stats.BlockedPilots.Should().Be(1);
        }

        [Fact]
        public void ProcessInPlace_WorksWithSpans()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilotId = "Pilot1";
            var input = GenerateSineWave(100, 440, SampleRate);
            var output = new float[input.Length];

            // Act
            filter.SetPilotMode(pilotId, PilotFilterMode.Allow);
            filter.ProcessInPlace(pilotId, input.AsSpan(), output.AsSpan());

            // Assert
            output.Should().BeEquivalentTo(input);
        }

        [Fact]
        public void ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var filter = new PilotFilter();
            var pilots = Enumerable.Range(0, 10).Select(i => $"Pilot{i}").ToArray();
            var input = GenerateSineWave(100, 440, SampleRate);
            var modes = new[] 
            { 
                PilotFilterMode.Allow, 
                PilotFilterMode.Mute, 
                PilotFilterMode.Solo, 
                PilotFilterMode.Block 
            };

            // Act - concurrent access from multiple threads
            Parallel.For(0, 100, iteration =>
            {
                foreach (var pilot in pilots)
                {
                    // Randomly set mode and process
                    var mode = modes[iteration % modes.Length];
                    filter.SetPilotMode(pilot, mode);
                    _ = filter.Process(pilot, input);
                }
            });

            // Assert - no exceptions should be thrown
            var stats = filter.GetStats();
            stats.TotalPilots.Should().Be(pilots.Length);
        }

        #region Helper Methods

        private float[] GenerateSineWave(int durationMs, float frequencyHz, int sampleRate)
        {
            int sampleCount = sampleRate * durationMs / 1000;
            var samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = (float)Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            }
            
            return samples;
        }

        private float[] GenerateConstantLevel(float level, int sampleCount)
        {
            var samples = new float[sampleCount];
            Array.Fill(samples, level);
            return samples;
        }

        private DiscontinuityMetrics MeasureDiscontinuities(float[] samples)
        {
            float maxDelta = 0.0f;
            float totalSlope = 0.0f;
            int clickCount = 0;
            const float clickThreshold = 0.5f;

            for (int i = 1; i < samples.Length; i++)
            {
                float delta = Math.Abs(samples[i] - samples[i - 1]);
                float slope = samples[i] - samples[i - 1];
                
                maxDelta = Math.Max(maxDelta, delta);
                totalSlope += slope;
                
                if (delta > clickThreshold)
                {
                    clickCount++;
                }
            }

            return new DiscontinuityMetrics
            {
                MaxSampleDelta = maxDelta,
                AverageFadeSlope = totalSlope / (samples.Length - 1),
                ClickCount = clickCount
            };
        }

        private struct DiscontinuityMetrics
        {
            public float MaxSampleDelta { get; set; }
            public float AverageFadeSlope { get; set; }
            public int ClickCount { get; set; }
        }

        #endregion
    }
}
