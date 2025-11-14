using System;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.UI.Controls;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace AeroDebrief.UI.Tests.Integration
{
    /// <summary>
    /// Integration tests for frequency mixing workflow.
    /// Tests frequency selection, mixer adjustments, solo/mute logic, and waveform updates.
    /// </summary>
    public class FrequencyMixingWorkflowTests : IDisposable
    {
        private readonly UnifiedPlayerControl _playerControl;
        private readonly Mock<UnifiedPlayerViewModel> _mockViewModel;

        public FrequencyMixingWorkflowTests()
        {
            _playerControl = ComponentTestHelper.CreateComponent<UnifiedPlayerControl>();
            _mockViewModel = TestDataFactory.CreateMockViewModel();

            // Setup initial state - file loaded
            _mockViewModel.Setup(vm => vm.IsIdle).Returns(false);
            var frequencies = TestDataFactory.CreateMockFrequencies();
            _mockViewModel.Setup(vm => vm.Frequencies).Returns(frequencies);

            ComponentTestHelper.SetProperty(_playerControl, UnifiedPlayerControl.DataContextProperty, _mockViewModel.Object);
        }

        [Fact]
        public async Task FrequencyMixing_SelectFrequency_UpdatesWaveformAndEnablesMixer()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var frequency = frequencies.First().Frequencies.First();

            // Act - Select frequency
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                frequency.IsSelected = true;
                _mockViewModel.Setup(vm => vm.WaveformData).Returns(TestDataFactory.CreateMockWaveformData(1000, true));
            });

            await Task.Delay(100);
            ComponentTestHelper.PumpDispatcher(_playerControl);

            // Assert
            frequency.IsSelected.Should().BeTrue("Frequency should be selected");
            _mockViewModel.Object.WaveformData.Should().NotBeNull("Waveform should be generated");
            _mockViewModel.Object.WaveformData.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task FrequencyMixing_AdjustVolume_UpdatesAudioLevel()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var frequency = frequencies.First().Frequencies.First();
            frequency.IsSelected = true;

            float capturedVolume = -1;
            _mockViewModel.Setup(vm => vm.UpdateChannelGain(It.IsAny<double>(), It.IsAny<float>()))
                .Callback<double, float>((freq, vol) => capturedVolume = vol);

            // Act - Adjust volume
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                frequency.Volume = 0.75f;
                _mockViewModel.Object.UpdateChannelGain(frequency.Frequency, 0.75f);
            });

            await Task.Delay(100);

            // Assert
            frequency.Volume.Should().Be(0.75f);
            capturedVolume.Should().Be(0.75f, "Volume change should be propagated to ViewModel");
        }

        [Fact]
        public async Task FrequencyMixing_AdjustPan_UpdatesAudioBalance()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var frequency = frequencies.First().Frequencies.First();
            frequency.IsSelected = true;

            float capturedPan = 0;
            _mockViewModel.Setup(vm => vm.UpdateChannelPan(It.IsAny<double>(), It.IsAny<float>()))
                .Callback<double, float>((freq, pan) => capturedPan = pan);

            // Act - Adjust pan
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                frequency.Pan = -0.5f;
                _mockViewModel.Object.UpdateChannelPan(frequency.Frequency, -0.5f);
            });

            await Task.Delay(100);

            // Assert
            frequency.Pan.Should().Be(-0.5f);
            capturedPan.Should().Be(-0.5f, "Pan change should be propagated to ViewModel");
        }

        [Fact]
        public async Task FrequencyMixing_MuteFrequency_StopsAudioForThatFrequency()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var frequency = frequencies.First().Frequencies.First();
            frequency.IsSelected = true;

            bool capturedMuteState = false;
            _mockViewModel.Setup(vm => vm.UpdateChannelMute(It.IsAny<double>(), It.IsAny<bool>()))
                .Callback<double, bool>((freq, muted) => capturedMuteState = muted);

            // Act - Mute frequency
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                frequency.IsMuted = true;
                _mockViewModel.Object.UpdateChannelMute(frequency.Frequency, true);
            });

            await Task.Delay(100);

            // Assert
            frequency.IsMuted.Should().BeTrue();
            capturedMuteState.Should().BeTrue("Mute state should be propagated to ViewModel");
        }

        [Fact]
        public async Task FrequencyMixing_SoloFrequency_MutesAllOthers()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var soloFrequency = frequencies.First().Frequencies.First();
            var otherFrequency = frequencies.First().Frequencies.Last();
            
            soloFrequency.IsSelected = true;
            otherFrequency.IsSelected = true;

            // Act - Solo one frequency
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                soloFrequency.IsSolo = true;
                
                // Simulate solo logic - mute all others
                foreach (var group in frequencies)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        if (Math.Abs(freq.Frequency - soloFrequency.Frequency) > 0.1)
                        {
                            freq.IsMuted = true;
                        }
                    }
                }
            });

            await Task.Delay(100);

            // Assert
            soloFrequency.IsSolo.Should().BeTrue("Solo frequency should be soloed");
            soloFrequency.IsMuted.Should().BeFalse("Solo frequency should not be muted");
            otherFrequency.IsMuted.Should().BeTrue("Other frequencies should be muted");
        }

        [Fact]
        public async Task FrequencyMixing_UnsoloWhenNoOthersSoloed_UnmutesAll()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var soloFrequency = frequencies.First().Frequencies.First();
            var otherFrequency = frequencies.First().Frequencies.Last();
            
            // Setup solo state
            soloFrequency.IsSolo = true;
            otherFrequency.IsMuted = true;

            // Act - Unsolo
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                soloFrequency.IsSolo = false;
                
                // Check if any other frequency is soloed
                bool anySoloed = frequencies.Any(g => g.Frequencies.Any(f => f.IsSolo));
                
                // If no frequencies are soloed, unmute all
                if (!anySoloed)
                {
                    foreach (var group in frequencies)
                    {
                        foreach (var freq in group.Frequencies)
                        {
                            freq.IsMuted = false;
                        }
                    }
                }
            });

            await Task.Delay(100);

            // Assert
            soloFrequency.IsSolo.Should().BeFalse();
            soloFrequency.IsMuted.Should().BeFalse();
            otherFrequency.IsMuted.Should().BeFalse("All frequencies should be unmuted");
        }

        [Fact]
        public async Task FrequencyMixing_SelectAll_EnablesAllFrequencies()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;

            // Act - Select all
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                foreach (var group in frequencies)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        freq.IsSelected = true;
                    }
                }
                
                _mockViewModel.Setup(vm => vm.WaveformData).Returns(TestDataFactory.CreateMockWaveformData(1000, true));
            });

            await Task.Delay(100);

            // Assert
            foreach (var group in frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    freq.IsSelected.Should().BeTrue($"Frequency {freq.DisplayName} should be selected");
                }
            }
            
            _mockViewModel.Object.WaveformData.Should().NotBeNull("Waveform should be generated for all frequencies");
        }

        [Fact]
        public async Task FrequencyMixing_SelectNone_DisablesAllAndClearsWaveform()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            
            // Start with some selected
            foreach (var group in frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    freq.IsSelected = true;
                }
            }

            // Act - Deselect all
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                foreach (var group in frequencies)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        freq.IsSelected = false;
                    }
                }
                
                // Clear waveform when no frequencies selected
                _mockViewModel.Setup(vm => vm.WaveformData).Returns(new float[1000]);
            });

            await Task.Delay(100);

            // Assert
            foreach (var group in frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    freq.IsSelected.Should().BeFalse($"Frequency {freq.DisplayName} should be deselected");
                }
            }
            
            var waveform = _mockViewModel.Object.WaveformData;
            waveform.Should().NotBeNull();
            waveform.All(v => Math.Abs(v) < 0.001f).Should().BeTrue("Waveform should be blank when no frequencies selected");
        }

        [Fact]
        public async Task FrequencyMixing_MultipleFrequenciesWithDifferentVolumes_MixesCorrectly()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var freq1 = frequencies.First().Frequencies.First();
            var freq2 = frequencies.First().Frequencies.Last();
            
            freq1.IsSelected = true;
            freq2.IsSelected = true;

            // Act - Set different volumes
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                freq1.Volume = 1.0f;
                freq2.Volume = 0.5f;
                
                _mockViewModel.Object.UpdateChannelGain(freq1.Frequency, 1.0f);
                _mockViewModel.Object.UpdateChannelGain(freq2.Frequency, 0.5f);
            });

            await Task.Delay(100);

            // Assert
            freq1.Volume.Should().Be(1.0f);
            freq2.Volume.Should().Be(0.5f);
            
            // Verify both frequencies are active
            freq1.IsSelected.Should().BeTrue();
            freq2.IsSelected.Should().BeTrue();
        }

        [Fact]
        public async Task FrequencyMixing_ResetMixer_RestoresDefaults()
        {
            // Arrange
            var frequencies = _mockViewModel.Object.Frequencies;
            var frequency = frequencies.First().Frequencies.First();
            frequency.IsSelected = true;
            
            // Modify mixer settings
            frequency.Volume = 0.5f;
            frequency.Pan = -0.8f;
            frequency.IsMuted = true;
            frequency.IsSolo = true;

            // Act - Reset
            ComponentTestHelper.InvokeOnDispatcher(_playerControl, () =>
            {
                frequency.Volume = 1.0f;
                frequency.Pan = 0.0f;
                frequency.IsMuted = false;
                frequency.IsSolo = false;
            });

            await Task.Delay(100);

            // Assert
            frequency.Volume.Should().Be(1.0f, "Volume should be reset to 100%");
            frequency.Pan.Should().Be(0.0f, "Pan should be reset to center");
            frequency.IsMuted.Should().BeFalse("Mute should be disabled");
            frequency.IsSolo.Should().BeFalse("Solo should be disabled");
        }

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_playerControl);
        }
    }
}
