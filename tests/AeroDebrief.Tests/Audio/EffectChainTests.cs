using AeroDebrief.Core.Audio;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace AeroDebrief.Tests.Audio
{
    public class EffectChainTests
    {
        [Fact]
        public void EffectChain_ProcessesSingleEffect()
        {
            // Arrange
            var chain = new EffectChain();
            var gain = new GainEffect(6.0f); // +6dB
            chain.AddEffect(gain);

            var input = new float[] { 0.5f, -0.5f, 0.25f, -0.25f };

            // Act
            var output = chain.Process(input);

            // Assert
            output.Should().NotBeNull();
            output.Length.Should().Be(input.Length);
            
            // Verify gain was applied
            float expectedGain = MathF.Pow(10, 6.0f / 20.0f); // ~2.0
            for (int i = 0; i < input.Length; i++)
            {
                float expected = Math.Clamp(input[i] * expectedGain, -1.0f, 1.0f);
                output[i].Should().BeApproximately(expected, 0.01f, $"Sample {i} should be amplified");
            }
        }

        [Fact]
        public void EffectChain_ProcessesMultipleEffects_InOrder()
        {
            // Arrange
            var chain = new EffectChain();
            var gain1 = new GainEffect(3.0f);
            var gain2 = new GainEffect(3.0f);
            
            chain.AddEffect(gain1);
            chain.AddEffect(gain2);

            var input = new float[] { 0.1f };

            // Act
            var output = chain.Process(input);

            // Assert - Should apply both gains (6dB total)
            float expectedGain = MathF.Pow(10, 6.0f / 20.0f); // ~2.0
            float expected = Math.Clamp(input[0] * expectedGain, -1.0f, 1.0f);
            output[0].Should().BeApproximately(expected, 0.01f);
        }

        [Fact]
        public void EffectChain_SkipsDisabledEffects()
        {
            // Arrange
            var chain = new EffectChain();
            var gain = new GainEffect(20.0f) { Enabled = false }; // Disabled
            chain.AddEffect(gain);

            var input = new float[] { 0.5f, -0.5f };

            // Act
            var output = chain.Process(input);

            // Assert - Output should equal input (no processing)
            output.Should().Equal(input);
        }

        [Fact]
        public void EffectChain_AtomicUpdate_IsLockFree()
        {
            // Arrange
            var chain = new EffectChain();
            var processCount = 0;
            var updateCount = 0;
            var cts = new CancellationTokenSource();

            // Act - Process continuously while updating
            var processTask = Task.Run(() =>
            {
                var input = new float[480]; // 10ms @ 48kHz
                while (!cts.Token.IsCancellationRequested)
                {
                    chain.Process(input);
                    Interlocked.Increment(ref processCount);
                }
            });

            var updateTask = Task.Run(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    chain.UpdateEffects(builder =>
                    {
                        builder.Clear();
                        builder.Add(new GainEffect(i % 10));
                    });
                    Interlocked.Increment(ref updateCount);
                    Thread.Sleep(1);
                }
            });

            updateTask.Wait();
            cts.Cancel();
            processTask.Wait(1000);

            // Assert - Both should have run without deadlock
            processCount.Should().BeGreaterThan(0, "processing should have occurred");
            updateCount.Should().Be(1000, "all updates should complete");
            chain.SnapshotsCreated.Should().BeGreaterOrEqualTo(1000, "should create snapshots");
        }

        [Fact]
        public void EffectChain_UpdateEffects_CreatesNewSnapshot()
        {
            // Arrange
            var chain = new EffectChain();
            var initialSnapshots = chain.SnapshotsCreated;

            // Act
            chain.UpdateEffects(builder =>
            {
                builder.Add(new GainEffect());
                builder.Add(new HighPassFilter());
            });

            // Assert
            chain.SnapshotsCreated.Should().Be(initialSnapshots + 1);
            chain.EffectCount.Should().Be(2);
        }

        [Fact]
        public void EffectChain_Builder_AllowsComplexUpdates()
        {
            // Arrange
            var chain = new EffectChain();
            var gain1 = new GainEffect(3.0f);
            var gain2 = new GainEffect(6.0f);
            var hpf = new HighPassFilter(300);

            // Act
            chain.UpdateEffects(builder =>
            {
                builder.Add(gain1);
                builder.Add(hpf);
                builder.Add(gain2);
                builder.Remove(hpf); // Remove the HPF
                builder.Insert(1, new DelayEffect()); // Insert delay
            });

            // Assert
            var effects = chain.GetEffects();
            effects.Count.Should().Be(3);
            effects[0].Should().BeOfType<GainEffect>();
            effects[1].Should().BeOfType<DelayEffect>();
            effects[2].Should().BeOfType<GainEffect>();
        }

        [Fact]
        public void EffectChain_Clear_RemovesAllEffects()
        {
            // Arrange
            var chain = new EffectChain(
                new GainEffect(),
                new HighPassFilter(),
                new DelayEffect());

            chain.EffectCount.Should().Be(3);

            // Act
            chain.Clear();

            // Assert
            chain.EffectCount.Should().Be(0);
        }

        [Fact]
        public void GainEffect_AppliesCorrectAmplification()
        {
            // Arrange
            var gain = new GainEffect(6.0f); // +6dB = 2x
            var input = new float[] { 0.5f, -0.5f, 0.25f, -0.25f };

            // Act
            var output = gain.Process(input);

            // Assert
            float expectedGain = MathF.Pow(10, 6.0f / 20.0f);
            for (int i = 0; i < input.Length; i++)
            {
                float expected = Math.Clamp(input[i] * expectedGain, -1.0f, 1.0f);
                output[i].Should().BeApproximately(expected, 0.01f);
            }
        }

        [Fact]
        public void GainEffect_ClampsToPeventClipping()
        {
            // Arrange
            var gain = new GainEffect(20.0f); // +20dB = 10x
            var input = new float[] { 0.5f, 0.8f, -0.9f };

            // Act
            var output = gain.Process(input);

            // Assert - Should clamp to [-1, 1]
            output.Should().OnlyContain(sample => sample >= -1.0f && sample <= 1.0f, "output should be clamped");
        }

        [Fact]
        public void HighPassFilter_AttenuatesLowFrequencies()
        {
            // Arrange
            var hpf = new HighPassFilter(cutoffHz: 300);
            
            // Generate DC signal (0 Hz - should be fully attenuated)
            var dcInput = Enumerable.Repeat(0.5f, 4800).ToArray(); // 100ms @ 48kHz

            // Act
            var output = hpf.Process(dcInput);

            // Assert - DC should be heavily attenuated
            var avgOutput = output.Skip(1000).Average(); // Skip transient
            Math.Abs(avgOutput).Should().BeLessThan(0.1f, "DC should be attenuated");
        }

        [Fact]
        public void DelayEffect_CreatesEcho()
        {
            // Arrange
            var delay = new DelayEffect(delayMs: 20, feedback: 0.5f, sampleRate: 48000);
            
            // Create impulse signal
            var input = new float[4800]; // 100ms
            input[0] = 1.0f; // Impulse at start

            // Act
            var output = delay.Process(input);

            // Assert - Should see echo at delay position
            int delaySamples = 48000 * 20 / 1000; // 960 samples
            
            // Echo should be present and have reduced amplitude
            Math.Abs(output[delaySamples]).Should().BeGreaterThan(0.1f, "echo should be present");
            Math.Abs(output[delaySamples]).Should().BeLessThan(1.0f, "echo should be attenuated");
        }

        [Fact]
        public void EffectChain_StressTest_HighThroughput()
        {
            // Arrange
            var chain = new EffectChain(
                new GainEffect(3.0f),
                new HighPassFilter(300),
                new DelayEffect(10, 0.3f));

            const int iterations = 10000;
            var input = new float[480]; // 10ms frame
            Array.Fill(input, 0.5f);

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                chain.Process(input);
            }

            stopwatch.Stop();

            // Assert
            var framesPerSecond = iterations * 1000.0 / stopwatch.ElapsedMilliseconds;
            
            // Should easily handle 100fps (10ms frames)
            framesPerSecond.Should().BeGreaterThan(100, "should process faster than real-time");
        }

        [Fact]
        public void EffectChain_ConcurrentReads_NoDeadlock()
        {
            // Arrange
            var chain = new EffectChain(new GainEffect(3.0f));
            var input = new float[480];
            const int threadCount = 10;
            const int iterationsPerThread = 1000;
            var tasks = new Task[threadCount];

            // Act - Multiple threads processing simultaneously
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        chain.Process(input);
                    }
                });
            }

            var completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(5));

            // Assert
            completed.Should().BeTrue("all threads should complete without deadlock");
        }

        [Fact]
        public void EffectChain_Builder_Replace_SwapsEffects()
        {
            // Arrange
            var chain = new EffectChain();
            var oldGain = new GainEffect(3.0f);
            var newGain = new GainEffect(6.0f);

            chain.AddEffect(oldGain);

            // Act
            chain.UpdateEffects(builder =>
            {
                builder.Replace(oldGain, newGain);
            });

            // Assert
            var effects = chain.GetEffects();
            effects.Count.Should().Be(1);
            effects[0].Should().BeSameAs(newGain);
        }
    }
}
