using Xunit;
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using FluentAssertions;
using System;
using System.Linq;
using System.Threading;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Tests for MasterMixer - high-performance audio mixer with frequency and pilot filtering.
    /// Note: These tests focus on the mixer's API and statistics without requiring full audio pipeline.
    /// </summary>
    public class MasterMixerTests
    {
        [Fact]
        public void MixerStats_ToString_ReturnsFormattedString()
        {
            // Arrange
            var stats = new MixerStats
            {
                FramesMixed = 1000,
                Underruns = 10,
                SilentFramesDrained = 50,
                RunTime = TimeSpan.FromMinutes(1),
                ActiveFrequencies = 5,
                SoloFrequencies = 1,
                MutedFrequencies = 2,
                BlockedFrequencies = 0,
                SoloPilots = 0,
                MutedPilots = 1,
                BlockedPilots = 0,
                AverageFrameRate = 16.67
            };

            // Act
            var str = stats.ToString();

            // Assert
            str.Should().Contain("Frames=1000");
            str.Should().Contain("Underruns=10");
            str.Should().Contain("Freqs=5");
            str.Should().Contain("Solo=1");
            str.Should().Contain("Muted=2");
        }

        [Fact]
        public void MixerStats_UnderrunRate_CalculatesCorrectly()
        {
            // Arrange
            var stats = new MixerStats
            {
                FramesMixed = 1000,
                Underruns = 10
            };

            // Act
            var rate = stats.UnderrunRate;

            // Assert
            rate.Should().BeApproximately(1.0, 0.01); // 10/1000 = 1%
        }

        [Fact]
        public void MixerStats_WithZeroFrames_ReturnsZeroUnderrunRate()
        {
            // Arrange
            var stats = new MixerStats
            {
                FramesMixed = 0,
                Underruns = 0
            };

            // Act
            var rate = stats.UnderrunRate;

            // Assert
            rate.Should().Be(0.0);
        }

        [Fact]
        public void MixerStats_WithHighUnderrunRate_ShowsPercentageCorrectly()
        {
            // Arrange
            var stats = new MixerStats
            {
                FramesMixed = 100,
                Underruns = 50
            };

            // Act
            var rate = stats.UnderrunRate;

            // Assert
            rate.Should().BeApproximately(50.0, 0.01); // 50/100 = 50%
        }

        [Fact]
        public void MixerStats_AllFields_PopulatedCorrectly()
        {
            // Arrange & Act
            var stats = new MixerStats
            {
                FramesMixed = 5000,
                Underruns = 25,
                SilentFramesDrained = 100,
                RunTime = TimeSpan.FromMinutes(2.5),
                ActiveFrequencies = 3,
                SoloFrequencies = 1,
                MutedFrequencies = 1,
                BlockedFrequencies = 0,
                SoloPilots = 2,
                MutedPilots = 1,
                BlockedPilots = 0,
                AverageFrameRate = 33.33
            };

            // Assert
            stats.FramesMixed.Should().Be(5000);
            stats.Underruns.Should().Be(25);
            stats.SilentFramesDrained.Should().Be(100);
            stats.RunTime.Should().Be(TimeSpan.FromMinutes(2.5));
            stats.ActiveFrequencies.Should().Be(3);
            stats.SoloFrequencies.Should().Be(1);
            stats.MutedFrequencies.Should().Be(1);
            stats.BlockedFrequencies.Should().Be(0);
            stats.SoloPilots.Should().Be(2);
            stats.MutedPilots.Should().Be(1);
            stats.BlockedPilots.Should().Be(0);
            stats.AverageFrameRate.Should().BeApproximately(33.33, 0.01);
        }

        [Fact]
        public void FrequencyGateMode_AllValues_AreDistinct()
        {
            // This test ensures the enum values don't overlap
            var values = Enum.GetValues<FrequencyGateMode>();
            var distinctValues = values.Distinct();

            distinctValues.Should().HaveCount(values.Length);
        }

        [Fact]
        public void FrequencyGateMode_HasExpectedValues()
        {
            // Arrange
            var values = Enum.GetValues<FrequencyGateMode>();

            // Assert
            values.Should().Contain(FrequencyGateMode.Allow);
            values.Should().Contain(FrequencyGateMode.Block);
            values.Should().Contain(FrequencyGateMode.Mute);
            values.Should().Contain(FrequencyGateMode.Solo);
        }

        [Fact]
        public void PilotGateMode_AllValues_AreDistinct()
        {
            // This test ensures the enum values don't overlap
            var values = Enum.GetValues<PilotGateMode>();
            var distinctValues = values.Distinct();

            distinctValues.Should().HaveCount(values.Length);
        }

        [Fact]
        public void PilotGateMode_HasExpectedValues()
        {
            // Arrange
            var values = Enum.GetValues<PilotGateMode>();

            // Assert
            values.Should().Contain(PilotGateMode.Allow);
            values.Should().Contain(PilotGateMode.Block);
            values.Should().Contain(PilotGateMode.Mute);
            values.Should().Contain(PilotGateMode.Solo);
        }

        [Fact]
        public void MixerStats_WithMaxValues_HandlesLargeNumbers()
        {
            // Arrange & Act
            var stats = new MixerStats
            {
                FramesMixed = long.MaxValue,
                Underruns = long.MaxValue / 2,
                SilentFramesDrained = 1000000,
                RunTime = TimeSpan.FromHours(24),
                ActiveFrequencies = 100,
                SoloFrequencies = 50,
                MutedFrequencies = 50,
                BlockedFrequencies = 10,
                SoloPilots = 200,
                MutedPilots = 100,
                BlockedPilots = 50,
                AverageFrameRate = 100.0
            };

            // Assert - should not throw and should calculate rate
            var rate = stats.UnderrunRate;
            rate.Should().BeGreaterThan(0);
            stats.ToString().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void MixerStats_Default_HasZeroValues()
        {
            // Arrange & Act
            var stats = new MixerStats();

            // Assert
            stats.FramesMixed.Should().Be(0);
            stats.Underruns.Should().Be(0);
            stats.SilentFramesDrained.Should().Be(0);
            stats.RunTime.Should().Be(TimeSpan.Zero);
            stats.ActiveFrequencies.Should().Be(0);
            stats.SoloFrequencies.Should().Be(0);
            stats.MutedFrequencies.Should().Be(0);
            stats.BlockedFrequencies.Should().Be(0);
            stats.SoloPilots.Should().Be(0);
            stats.MutedPilots.Should().Be(0);
            stats.BlockedPilots.Should().Be(0);
            stats.AverageFrameRate.Should().Be(0);
            stats.UnderrunRate.Should().Be(0);
        }

        [Fact]
        public void FrequencyGateMode_CanBeCompared()
        {
            // Arrange
            var allow = FrequencyGateMode.Allow;
            var block = FrequencyGateMode.Block;
            var mute = FrequencyGateMode.Mute;
            var solo = FrequencyGateMode.Solo;

            // Assert - Each mode should be unique
            allow.Should().NotBe(block);
            allow.Should().NotBe(mute);
            allow.Should().NotBe(solo);
            block.Should().NotBe(mute);
            block.Should().NotBe(solo);
            mute.Should().NotBe(solo);
        }

        [Fact]
        public void PilotGateMode_CanBeCompared()
        {
            // Arrange
            var allow = PilotGateMode.Allow;
            var block = PilotGateMode.Block;
            var mute = PilotGateMode.Mute;
            var solo = PilotGateMode.Solo;

            // Assert - Each mode should be unique
            allow.Should().NotBe(block);
            allow.Should().NotBe(mute);
            allow.Should().NotBe(solo);
            block.Should().NotBe(mute);
            block.Should().NotBe(solo);
            mute.Should().NotBe(solo);
        }

        [Fact]
        public void MixerStats_ToString_ContainsAllRelevantInformation()
        {
            // Arrange
            var stats = new MixerStats
            {
                FramesMixed = 10000,
                Underruns = 100,
                SilentFramesDrained = 500,
                RunTime = TimeSpan.FromMinutes(5),
                ActiveFrequencies = 10,
                SoloFrequencies = 2,
                MutedFrequencies = 3,
                BlockedFrequencies = 1,
                SoloPilots = 5,
                MutedPilots = 4,
                BlockedPilots = 2,
                AverageFrameRate = 33.33
            };

            // Act
            var str = stats.ToString();

            // Assert - Verify key information is present
            str.Should().Contain("10000"); // FramesMixed
            str.Should().Contain("100"); // Underruns
            str.Should().Contain("500"); // Drained
            str.Should().Contain("5"); // Runtime in minutes
            str.Should().Contain("10"); // ActiveFrequencies
            str.Should().Contain("33.3"); // AverageFrameRate (approximately)
        }

        [Fact]
        public void MixerStats_UnderrunRate_WithRoundingEdgeCases()
        {
            // Test edge case: 1 underrun in 3 frames = 33.33%
            var stats1 = new MixerStats { FramesMixed = 3, Underruns = 1 };
            stats1.UnderrunRate.Should().BeApproximately(33.33, 0.01);

            // Test edge case: 2 underruns in 3 frames = 66.67%
            var stats2 = new MixerStats { FramesMixed = 3, Underruns = 2 };
            stats2.UnderrunRate.Should().BeApproximately(66.67, 0.01);

            // Test edge case: all frames are underruns = 100%
            var stats3 = new MixerStats { FramesMixed = 100, Underruns = 100 };
            stats3.UnderrunRate.Should().Be(100.0);
        }
    }
}
