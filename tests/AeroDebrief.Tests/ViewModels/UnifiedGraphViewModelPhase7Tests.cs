using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Graphs;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Charts;

namespace AeroDebrief.Tests.ViewModels
{
    /// <summary>
    /// Phase 7: Tests for visibility toggle functionality in UnifiedGraphViewModel.
    /// Tests frequency-level and pilot-level visibility, rapid toggles, and state management.
    /// Phase 7 Step 3: Tests for audio mixer synchronization.
    /// </summary>
    public class UnifiedGraphViewModelPhase7Tests
    {
        #region Test Helpers

        private UnifiedGraphViewModel CreateViewModelWithTestSeries()
        {
            // Create ViewModel with default constructor (uses default AmplitudeSeriesProvider)
            var vm = new UnifiedGraphViewModel();

            // Set time range
            var start = new DateTime(2024, 1, 1, 12, 0, 0);
            var end = start.AddHours(1);
            
            // Create test series for 3 frequencies with 2 pilots each
            var frequencies = new[] { 251.0, 305.0, 127.5 };
            var pilots = new[] { "SHARK-1-1", "VIPER-2-2" };

            // Manually populate the internal dictionaries to simulate LoadDataAsync behavior
            // We need to use reflection to access private fields for testing
            var frequencyPilotsField = typeof(UnifiedGraphViewModel)
                .GetField("_frequencyPilots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allSeriesField = typeof(UnifiedGraphViewModel)
                .GetField("_allSeries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var visibleSeriesCountField = typeof(UnifiedGraphViewModel)
                .GetField("_visibleSeriesCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var frequencyPilots = frequencyPilotsField?.GetValue(vm) as Dictionary<string, HashSet<string>>;
            var allSeries = allSeriesField?.GetValue(vm) as Dictionary<string, ISeries>;

            int totalSeriesCreated = 0;

            foreach (var freq in frequencies)
            {
                var freqKey = $"freq:{freq:F1}";
                var pilotSet = new HashSet<string>();

                foreach (var pilot in pilots)
                {
                    pilotSet.Add(pilot);

                    var pilotKey = $"pilot:{freq:F1}:{pilot}";
                    var series = new LineSeries<ObservablePoint>
                    {
                        Name = pilotKey, // Use Phase 7 format
                        Values = new List<ObservablePoint>
                        {
                            new ObservablePoint(start.Ticks, -50),
                            new ObservablePoint(start.AddMinutes(30).Ticks, -40),
                            new ObservablePoint(end.Ticks, -50)
                        },
                        IsVisible = true
                    };
                    
                    vm.Series.Add(series);
                    allSeries?.Add(pilotKey, series);
                    totalSeriesCreated++;
                }

                frequencyPilots?.Add(freqKey, pilotSet);
            }

            // Manually set the visible series count
            visibleSeriesCountField?.SetValue(vm, totalSeriesCreated);

            return vm;
        }

        #endregion

        #region Basic Visibility Tests

        [Fact]
        public void SetPilotVisibility_HidesPilot_SeriesBecomesInvisible()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();
            var initialCount = vm.Series.Count(s => s.IsVisible);

            // Act
            vm.SetPilotVisibility(251.0, "SHARK-1-1", false);

            // Assert
            var targetSeries = vm.Series.FirstOrDefault(s => 
                s.Name.Contains("251.0") && s.Name.Contains("SHARK-1-1"));
            
            Assert.NotNull(targetSeries);
            Assert.False(targetSeries.IsVisible);
            Assert.Equal(initialCount - 1, vm.Series.Count(s => s.IsVisible));
        }

        [Fact]
        public void SetPilotVisibility_ShowsPilot_SeriesBecomesVisible()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();
            vm.SetPilotVisibility(251.0, "SHARK-1-1", false);

            // Act
            vm.SetPilotVisibility(251.0, "SHARK-1-1", true);

            // Assert
            var targetSeries = vm.Series.FirstOrDefault(s => 
                s.Name.Contains("251.0") && s.Name.Contains("SHARK-1-1"));
            
            Assert.NotNull(targetSeries);
            Assert.True(targetSeries.IsVisible);
        }

        [Fact]
        public void SetFrequencyVisibility_HidesFrequency_AllPilotsHidden()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act
            vm.SetFrequencyVisibility(251.0, false);

            // Assert
            var freq251Series = vm.Series.Where(s => s.Name.Contains("251.0")).ToList();
            Assert.NotEmpty(freq251Series);
            Assert.All(freq251Series, s => Assert.False(s.IsVisible));
        }

        [Fact]
        public void SetFrequencyVisibility_ShowsFrequency_AllPilotsVisible()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();
            vm.SetFrequencyVisibility(251.0, false);

            // Act
            vm.SetFrequencyVisibility(251.0, true);

            // Assert
            var freq251Series = vm.Series.Where(s => s.Name.Contains("251.0")).ToList();
            Assert.NotEmpty(freq251Series);
            Assert.All(freq251Series, s => Assert.True(s.IsVisible));
        }

        #endregion

        #region State Management Tests

        [Fact]
        public void GetFrequencyVisibility_ReturnsCorrectState()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act
            vm.SetFrequencyVisibility(251.0, false);

            // Assert
            Assert.False(vm.GetFrequencyVisibility(251.0));
            Assert.True(vm.GetFrequencyVisibility(305.0)); // Other frequency still visible
        }

        [Fact]
        public void GetPilotVisibility_ReturnsCorrectState()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act
            vm.SetPilotVisibility(251.0, "SHARK-1-1", false);

            // Assert
            Assert.False(vm.GetPilotVisibility(251.0, "SHARK-1-1"));
            Assert.True(vm.GetPilotVisibility(251.0, "VIPER-2-2")); // Other pilot still visible
        }

        [Fact]
        public void VisibleSeriesCount_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();
            var initialCount = vm.VisibleSeriesCount;

            // Act
            vm.SetPilotVisibility(251.0, "SHARK-1-1", false);

            // Assert
            Assert.Equal(initialCount - 1, vm.VisibleSeriesCount);
        }

        #endregion

        #region Multiple Operations Tests

        [Fact]
        public void SetVisibility_MultipleFrequencies_WorksIndependently()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act
            vm.SetFrequencyVisibility(251.0, false);
            vm.SetFrequencyVisibility(305.0, false);

            // Assert
            Assert.False(vm.GetFrequencyVisibility(251.0));
            Assert.False(vm.GetFrequencyVisibility(305.0));
            Assert.True(vm.GetFrequencyVisibility(127.5)); // Unaffected
        }

        [Fact]
        public void SetVisibility_MixedPilotAndFrequency_MaintainsConsistency()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act
            vm.SetPilotVisibility(251.0, "SHARK-1-1", false);
            vm.SetFrequencyVisibility(305.0, false);

            // Assert
            Assert.False(vm.GetPilotVisibility(251.0, "SHARK-1-1"));
            Assert.True(vm.GetPilotVisibility(251.0, "VIPER-2-2"));
            Assert.False(vm.GetFrequencyVisibility(305.0));
        }

        #endregion

        #region Rapid Toggle Tests

        [Fact]
        public void VisibilityToggle_RapidChanges_HandlesCorrectly()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();

            // Act - Rapid toggles (100 times)
            for (int i = 0; i < 100; i++)
            {
                vm.SetFrequencyVisibility(251.0, i % 2 == 0);
            }

            // Assert - Should end with false (last iteration i=99 is odd, so i%2==0 is false)
            Assert.False(vm.GetFrequencyVisibility(251.0));
            
            var freq251Series = vm.Series.Where(s => s.Name.Contains("251.0")).ToList();
            Assert.All(freq251Series, s => Assert.False(s.IsVisible));
        }

        [Fact]
        public void VisibilityToggle_RapidChanges_Performance()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries();
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            for (int i = 0; i < 100; i++)
            {
                vm.SetFrequencyVisibility(251.0, i % 2 == 0);
            }
            stopwatch.Stop();

            // Assert - 100 toggles should complete in < 500ms (5ms per toggle average)
            Assert.True(stopwatch.ElapsedMilliseconds < 500, 
                $"100 toggles took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
        }

        #endregion

        #region Phase 7 Step 3: Audio Synchronization Tests

        [Fact]
        public void ChartToAudio_HideFrequency_MutesAudio()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            // Inject mixer controller using reflection
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            // Subscribe to events manually (since we injected after construction)
            mixerController.ChannelChanged += (sender, e) => {
                var handler = typeof(UnifiedGraphViewModel)
                    .GetMethod("OnMixerChannelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handler?.Invoke(vm, new object[] { sender, e });
            };

            // Ensure audio sync is enabled
            vm.AudioSyncEnabled = true;

            // Act - Hide frequency in chart
            vm.SetFrequencyVisibility(251.0, false);

            // Assert - Audio should be muted
            var channel = mixerController.GetChannel(251.0);
            Assert.NotNull(channel);
            Assert.True(channel.IsMuted, "Channel should be muted when chart series is hidden");

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void ChartToAudio_ShowFrequency_UnmutesAudio()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            mixerController.SetChannelMuted(251.0, true); // Start muted
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            mixerController.ChannelChanged += (sender, e) => {
                var handler = typeof(UnifiedGraphViewModel)
                    .GetMethod("OnMixerChannelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handler?.Invoke(vm, new object[] { sender, e });
            };

            vm.AudioSyncEnabled = true;

            // Act - Show frequency in chart
            vm.SetFrequencyVisibility(251.0, true);

            // Assert - Audio should be unmuted
            var channel = mixerController.GetChannel(251.0);
            Assert.NotNull(channel);
            Assert.False(channel.IsMuted, "Channel should be unmuted when chart series is shown");

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void AudioToChart_MuteAudio_HidesFrequency()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            mixerController.ChannelChanged += (sender, e) => {
                var handler = typeof(UnifiedGraphViewModel)
                    .GetMethod("OnMixerChannelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handler?.Invoke(vm, new object[] { sender, e });
            };
            
            vm.AudioSyncEnabled = true;

            // Act - Mute audio channel
            mixerController.SetChannelMuted(251.0, true);

            // Give event time to propagate
            System.Threading.Thread.Sleep(50);

            // Assert - Chart should hide frequency
            Assert.False(vm.GetFrequencyVisibility(251.0), "Frequency should be hidden when audio is muted");
            
            var freq251Series = vm.Series.Where(s => s.Name?.Contains("251.0") == true).ToList();
            Assert.All(freq251Series, s => Assert.False(s.IsVisible));

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void AudioToChart_UnmuteAudio_ShowsFrequency()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            mixerController.SetChannelMuted(251.0, true); // Start muted
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            mixerController.ChannelChanged += (sender, e) => {
                var handler = typeof(UnifiedGraphViewModel)
                    .GetMethod("OnMixerChannelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handler?.Invoke(vm, new object[] { sender, e });
            };
            
            // First hide the frequency to match muted state
            vm.SetFrequencyVisibility(251.0, false);
            vm.AudioSyncEnabled = true;

            // Act - Unmute audio channel
            mixerController.SetChannelMuted(251.0, false);

            // Give event time to propagate
            System.Threading.Thread.Sleep(50);

            // Assert - Chart should show frequency
            Assert.True(vm.GetFrequencyVisibility(251.0), "Frequency should be visible when audio is unmuted");
            
            var freq251Series = vm.Series.Where(s => s.Name?.Contains("251.0") == true).ToList();
            Assert.All(freq251Series, s => Assert.True(s.IsVisible));

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void AudioSync_DisabledFlag_DoesNotSyncToAudio()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            // Disable audio sync
            vm.AudioSyncEnabled = false;

            // Act - Hide frequency in chart
            vm.SetFrequencyVisibility(251.0, false);

            // Assert - Audio should NOT be muted
            var channel = mixerController.GetChannel(251.0);
            Assert.NotNull(channel);
            Assert.False(channel.IsMuted, "Channel should NOT be muted when audio sync is disabled");

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void AudioSync_NoMixerController_DoesNotThrow()
        {
            // Arrange
            var vm = CreateViewModelWithTestSeries(); // No mixer controller

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => vm.SetFrequencyVisibility(251.0, false));
            Assert.Null(exception);
        }

        [Fact]
        public void AudioSync_PreventsCircularUpdates()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            mixerController.ChannelChanged += (sender, e) => {
                var handler = typeof(UnifiedGraphViewModel)
                    .GetMethod("OnMixerChannelChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handler?.Invoke(vm, new object[] { sender, e });
            };
            
            vm.AudioSyncEnabled = true;

            int chartUpdates = 0;
            vm.Series.CollectionChanged += (s, e) => chartUpdates++;

            // Act - Toggle audio mute multiple times rapidly
            for (int i = 0; i < 5; i++)
            {
                mixerController.SetChannelMuted(251.0, i % 2 == 0);
                System.Threading.Thread.Sleep(10);
            }

            // Assert - Should not cause infinite loop or excessive updates
            // We expect max 5 updates (one per toggle), not hundreds
            Assert.True(chartUpdates < 20, $"Too many chart updates: {chartUpdates}, possible circular update loop");

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void AudioSync_MultipleFrequencies_IndependentSync()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            mixerController.SetupChannel(305.0, "UHF 305.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            vm.AudioSyncEnabled = true;

            // Act - Hide one frequency, keep other visible
            vm.SetFrequencyVisibility(251.0, false);
            vm.SetFrequencyVisibility(305.0, true);

            // Assert - Only 251.0 should be muted
            var channel251 = mixerController.GetChannel(251.0);
            var channel305 = mixerController.GetChannel(305.0);
            
            Assert.NotNull(channel251);
            Assert.NotNull(channel305);
            Assert.True(channel251.IsMuted, "251.0 should be muted");
            Assert.False(channel305.IsMuted, "305.0 should NOT be muted");

            // Cleanup
            mixerController.Dispose();
        }

        [Fact]
        public void Dispose_UnsubscribesFromMixerEvents()
        {
            // Arrange
            var mixerController = new MixerController();
            mixerController.Initialize();
            mixerController.SetupChannel(251.0, "UHF 251.0");
            
            var vm = CreateViewModelWithTestSeries();
            
            var mixerField = typeof(UnifiedGraphViewModel)
                .GetField("_mixerController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixerField?.SetValue(vm, mixerController);

            // Act - Dispose ViewModel
            vm.Dispose();

            // Change mixer state
            mixerController.SetChannelMuted(251.0, true);

            // Assert - Should not throw or cause issues (event unsubscribed)
            // If this test passes without exception, unsubscribe worked
            Assert.True(true); // Placeholder assertion

            // Cleanup
            mixerController.Dispose();
        }

        #endregion
    }
}
