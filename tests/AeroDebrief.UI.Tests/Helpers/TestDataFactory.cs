using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Tests.Helpers
{
    /// <summary>
    /// Factory class for creating test data for component tests.
    /// </summary>
    public static class TestDataFactory
    {
        /// <summary>
        /// Creates a collection of mock frequency groups for testing.
        /// </summary>
        public static ObservableCollection<FrequencyGroupViewModel> CreateMockFrequencies()
        {
            var frequencies = new ObservableCollection<FrequencyGroupViewModel>();

            // UHF Group
            var uhfGroup = new FrequencyGroupViewModel { Name = "UHF" };
            uhfGroup.Frequencies.Add(CreateFrequency(251.0, "UHF Guard"));
            uhfGroup.Frequencies.Add(CreateFrequency(305.0, "Tactical 1"));
            uhfGroup.Frequencies.Add(CreateFrequency(312.5, "Tactical 2"));
            frequencies.Add(uhfGroup);

            // VHF Group
            var vhfGroup = new FrequencyGroupViewModel { Name = "VHF" };
            vhfGroup.Frequencies.Add(CreateFrequency(133.25, "Tower"));
            vhfGroup.Frequencies.Add(CreateFrequency(121.5, "Emergency"));
            frequencies.Add(vhfGroup);

            return frequencies;
        }

        /// <summary>
        /// Creates a single frequency view model for testing.
        /// </summary>
        public static FrequencyViewModel CreateFrequency(double frequency, string name)
        {
            return new FrequencyViewModel
            {
                Frequency = frequency,
                DisplayName = name,
                Modulation = "AM",
                IsSelected = false,
                WaveformColor = Colors.Blue,
                Volume = 1.0f,
                Pan = 0.0f,
                IsMuted = false,
                IsSolo = false,
                PacketCount = 100
            };
        }

        /// <summary>
        /// Creates mock waveform data for testing.
        /// </summary>
        public static float[] CreateMockWaveformData(int sampleCount = 1000, bool withActivity = true)
        {
            var waveform = new float[sampleCount];

            if (withActivity)
            {
                var random = new Random(42); // Fixed seed for consistency
                for (int i = 0; i < sampleCount; i++)
                {
                    // Create some activity peaks
                    if (i % 100 < 20)
                    {
                        waveform[i] = (float)(random.NextDouble() * 0.8 - 0.4);
                    }
                    else
                    {
                        waveform[i] = (float)(random.NextDouble() * 0.1 - 0.05);
                    }
                }
            }

            return waveform;
        }

        /// <summary>
        /// Creates mock frequency waveform data for testing.
        /// </summary>
        public static Dictionary<double, AeroDebrief.UI.Controls.FrequencyWaveformData> CreateMockFrequencyWaveforms()
        {
            var waveforms = new Dictionary<double, AeroDebrief.UI.Controls.FrequencyWaveformData>();

            waveforms[251.0] = new AeroDebrief.UI.Controls.FrequencyWaveformData
            {
                Frequency = 251.0,
                WaveformData = CreateMockWaveformData(1000, true),
                Color = Colors.Red,
                DisplayName = "UHF Guard (251.000 MHz)"
            };

            waveforms[305.0] = new AeroDebrief.UI.Controls.FrequencyWaveformData
            {
                Frequency = 305.0,
                WaveformData = CreateMockWaveformData(1000, true),
                Color = Colors.Blue,
                DisplayName = "Tactical 1 (305.000 MHz)"
            };

            return waveforms;
        }

        /// <summary>
        /// Creates a mock UnifiedPlayerViewModel for testing.
        /// </summary>
        public static Mock<UnifiedPlayerViewModel> CreateMockViewModel()
        {
            var mock = new Mock<UnifiedPlayerViewModel>();

            // Setup basic properties
            mock.Setup(vm => vm.IsIdle).Returns(false);
            mock.Setup(vm => vm.IsPlaying).Returns(false);
            mock.Setup(vm => vm.IsPaused).Returns(false);
            mock.Setup(vm => vm.StatusMessage).Returns("Ready");
            mock.Setup(vm => vm.CurrentSourceName).Returns("Test Recording");
            mock.Setup(vm => vm.CurrentMode).Returns(PlayerMode.Idle);
            mock.Setup(vm => vm.TotalDuration).Returns(TimeSpan.FromSeconds(30));
            mock.Setup(vm => vm.PlayheadPositionNormalized).Returns(0.0);
            mock.Setup(vm => vm.WaveformData).Returns(CreateMockWaveformData());
            mock.Setup(vm => vm.Frequencies).Returns(CreateMockFrequencies());

            // Setup commands
            mock.Setup(vm => vm.PlayCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.PauseCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.StopCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.SeekCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.ChangeSourceCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.OpenSettingsCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.SelectAllFrequenciesCommand).Returns(new MockCommand());
            mock.Setup(vm => vm.SelectNoFrequenciesCommand).Returns(new MockCommand());

            return mock;
        }

        /// <summary>
        /// Simple mock command for testing.
        /// </summary>
        public class MockCommand : System.Windows.Input.ICommand
        {
#pragma warning disable CS0067 // Event is never used
            public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                ExecuteCount++;
            }

            public int ExecuteCount { get; private set; }
        }
    }
}
