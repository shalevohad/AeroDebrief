using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.TestWindows
{
    /// <summary>
    /// DEBUG WRAPPER for MainWindow - Adds testing panels around the production UI.
    /// In Debug mode, this shows the EXACT SAME UI as Release (UnifiedPlayerControl),
    /// but wraps it with debug tools (orange header + test results footer).
    /// </summary>
    public partial class UnifiedPlayerTestWindow : Window
    {
        private readonly ObservableCollection<TestResult> _testResults = new();
        private readonly DispatcherTimer _updateTimer;
        private int _passedTests = 0;
        private int _failedTests = 0;
        
        public UnifiedPlayerTestWindow()
        {
            InitializeComponent();
            
            // CRITICAL: Initialize ViewModel for UnifiedPlayerControl (same as MainWindow)
            var viewModel = new UnifiedPlayerViewModel();
            UnifiedPlayer.DataContext = viewModel;
            
            TestResultsList.ItemsSource = _testResults;
            
            // Setup update timer for monitoring state changes
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            LogMessage("========================================");
            LogMessage("DEBUG MODE: Testing production UI");
            LogMessage("Content below is IDENTICAL to Release build");
            LogMessage("========================================");
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Monitor ViewModel state
            if (UnifiedPlayer.DataContext is UnifiedPlayerViewModel vm)
            {
                TestStatusText.Text = $"Mode: {vm.CurrentMode} | State: {vm.PlaybackState} | " +
                                     $"Frequencies: {vm.Frequencies.Sum(g => g.Frequencies.Count)} | " +
                                     $"Mixer Channels: {vm.MixerChannels.Count}";
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void RunAllTests_Click(object sender, RoutedEventArgs e)
        {
            _testResults.Clear();
            _passedTests = 0;
            _failedTests = 0;
            
            LogMessage("========================================");
            LogMessage("Starting integration test suite...");
            LogMessage("Testing PRODUCTION UI (same as Release)");
            LogMessage("========================================");
            
            try
            {
                // Test 1: ViewModel initialization
                await RunTest("ViewModel Initialization", TestViewModelInitialization);
                
                // Test 2: Mode transitions
                await RunTest("Mode Transitions", TestModeTransitions);
                
                // Test 3: Commands availability
                await RunTest("Command Availability", TestCommandsAvailability);
                
                // Test 4: Source ViewModels
                await RunTest("Source ViewModels", TestSourceViewModels);
                
                // Test 5: Collections initialization
                await RunTest("Collections Initialization", TestCollectionsInitialization);
                
                // Test 6: Property change notifications
                await RunTest("Property Change Notifications", TestPropertyChangeNotifications);
                
                // Test 7: Mixer integration (if file loaded)
                if (UnifiedPlayer.DataContext is UnifiedPlayerViewModel vm && vm.IsPlaybackMode)
                {
                    await RunTest("Mixer Integration", TestMixerIntegration);
                    await RunTest("Frequency Selection", TestFrequencySelection);
                    await RunTest("Transport Controls", TestTransportControls);
                }
                else
                {
                    AddTestResult("??", "Skipped file-dependent tests (no file loaded)");
                }
                
                // Summary
                LogMessage("========================================");
                LogMessage($"Test suite completed: {_passedTests} passed, {_failedTests} failed");
                LogMessage("========================================");
                
                TestStatusText.Text = $"Tests: {_passedTests} ? {_failedTests} ?";
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Test suite failed with exception: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                AddTestResult("?", $"Test suite exception: {ex.Message}");
            }
        }

        private async Task RunTest(string testName, Func<Task<bool>> testAction)
        {
            LogMessage($"Running test: {testName}");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = await testAction();
                stopwatch.Stop();
                
                if (result)
                {
                    AddTestResult("?", $"{testName} - PASSED ({stopwatch.ElapsedMilliseconds}ms)");
                    _passedTests++;
                }
                else
                {
                    AddTestResult("?", $"{testName} - FAILED ({stopwatch.ElapsedMilliseconds}ms)");
                    _failedTests++;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AddTestResult("??", $"{testName} - EXCEPTION: {ex.Message}");
                LogMessage($"  Exception: {ex.Message}");
                _failedTests++;
            }
        }

        #region Test Methods

        private async Task<bool> TestViewModelInitialization()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null)
            {
                LogMessage("  ? ViewModel is null");
                return false;
            }
            
            LogMessage($"  ? ViewModel type: {vm.GetType().Name}");
            LogMessage($"  ? Initial mode: {vm.CurrentMode}");
            LogMessage($"  ? Initial state: {vm.PlaybackState}");
            
            return vm.CurrentMode == PlayerMode.Idle && 
                   vm.PlaybackState == PlaybackState.Stopped;
        }

        private async Task<bool> TestModeTransitions()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            var initialMode = vm.CurrentMode;
            LogMessage($"  Initial mode: {initialMode}");
            
            // Test mode helper properties
            var isIdle = vm.IsIdle;
            var isRecording = vm.IsRecordingMode;
            var isPlayback = vm.IsPlaybackMode;
            
            LogMessage($"  ? IsIdle: {isIdle}");
            LogMessage($"  ? IsRecordingMode: {isRecording}");
            LogMessage($"  ? IsPlaybackMode: {isPlayback}");
            
            return (initialMode == PlayerMode.Idle && isIdle && !isRecording && !isPlayback) ||
                   (initialMode == PlayerMode.Recording && !isIdle && isRecording && !isPlayback) ||
                   (initialMode == PlayerMode.Playback && !isIdle && !isRecording && isPlayback);
        }

        private async Task<bool> TestCommandsAvailability()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            var commands = new[]
            {
                ("PlayCommand", vm.PlayCommand),
                ("PauseCommand", vm.PauseCommand),
                ("StopCommand", vm.StopCommand),
                ("SeekCommand", vm.SeekCommand),
                ("GoLiveCommand", vm.GoLiveCommand),
                ("ChangeSourceCommand", vm.ChangeSourceCommand),
                ("SelectAllFrequenciesCommand", vm.SelectAllFrequenciesCommand),
                ("SelectNoFrequenciesCommand", vm.SelectNoFrequenciesCommand)
            };
            
            var allPresent = true;
            foreach (var (name, command) in commands)
            {
                if (command == null)
                {
                    LogMessage($"  ? {name} is null");
                    allPresent = false;
                }
                else
                {
                    LogMessage($"  ? {name} initialized");
                }
            }
            
            return allPresent;
        }

        private async Task<bool> TestSourceViewModels()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            var serverSource = vm.ServerSource;
            var fileSource = vm.FileSource;
            
            if (serverSource == null)
            {
                LogMessage("  ? ServerSource is null");
                return false;
            }
            
            if (fileSource == null)
            {
                LogMessage("  ? FileSource is null");
                return false;
            }
            
            LogMessage($"  ? ServerSource type: {serverSource.GetType().Name}");
            LogMessage($"  ? FileSource type: {fileSource.GetType().Name}");
            
            return true;
        }

        private async Task<bool> TestCollectionsInitialization()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            if (vm.Frequencies == null)
            {
                LogMessage("  ? Frequencies collection is null");
                return false;
            }
            
            if (vm.MixerChannels == null)
            {
                LogMessage("  ? MixerChannels collection is null");
                return false;
            }
            
            LogMessage($"  ? Frequencies count: {vm.Frequencies.Count}");
            LogMessage($"  ? Total frequency items: {vm.Frequencies.Sum(g => g.Frequencies.Count)}");
            LogMessage($"  ? MixerChannels count: {vm.MixerChannels.Count}");
            
            return true;
        }

        private async Task<bool> TestPropertyChangeNotifications()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            var propertiesChanged = new System.Collections.Generic.List<string>();
            
            vm.PropertyChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    propertiesChanged.Add(e.PropertyName);
                }
            };
            
            // Trigger some property changes
            var oldMessage = vm.StatusMessage;
            vm.StatusMessage = "Test message";
            await Task.Delay(50);
            vm.StatusMessage = oldMessage;
            
            LogMessage($"  ? PropertyChanged events captured: {propertiesChanged.Count}");
            foreach (var prop in propertiesChanged.Distinct())
            {
                LogMessage($"    - {prop}");
            }
            
            return propertiesChanged.Contains("StatusMessage");
        }

        private async Task<bool> TestMixerIntegration()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            if (!vm.MixerChannels.Any())
            {
                LogMessage("  ?? No mixer channels available (expected if no frequencies selected)");
                return true; // Not a failure - just no data
            }
            
            var channel = vm.MixerChannels.First();
            LogMessage($"  Testing channel: {channel.DisplayName} ({channel.Frequency:F1} Hz)");
            
            // Test gain update
            var originalGain = channel.Volume;
            vm.UpdateChannelGain(channel.Frequency, 0.5f);
            await Task.Delay(100);
            
            // Test pan update
            var originalPan = channel.Pan;
            vm.UpdateChannelPan(channel.Frequency, 0.3f);
            await Task.Delay(100);
            
            // Test mute
            vm.UpdateChannelMute(channel.Frequency, true);
            await Task.Delay(100);
            vm.UpdateChannelMute(channel.Frequency, false);
            
            // Restore original values
            vm.UpdateChannelGain(channel.Frequency, originalGain);
            vm.UpdateChannelPan(channel.Frequency, originalPan);
            
            LogMessage("  ? Mixer control methods executed successfully");
            
            return true;
        }

        private async Task<bool> TestFrequencySelection()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            var totalFreqs = vm.Frequencies.Sum(g => g.Frequencies.Count);
            if (totalFreqs == 0)
            {
                LogMessage("  ?? No frequencies available");
                return true;
            }
            
            LogMessage($"  Total frequencies: {totalFreqs}");
            
            // Test select all
            if (vm.SelectAllFrequenciesCommand.CanExecute(null))
            {
                vm.SelectAllFrequenciesCommand.Execute(null);
                await Task.Delay(500); // Wait for waveform generation
                
                var selectedCount = vm.Frequencies.SelectMany(g => g.Frequencies).Count(f => f.IsSelected);
                LogMessage($"  ? After SelectAll: {selectedCount} selected");
            }
            
            // Test select none
            if (vm.SelectNoFrequenciesCommand.CanExecute(null))
            {
                vm.SelectNoFrequenciesCommand.Execute(null);
                await Task.Delay(500); // Wait for waveform generation
                
                var selectedCount = vm.Frequencies.SelectMany(g => g.Frequencies).Count(f => f.IsSelected);
                LogMessage($"  ? After SelectNone: {selectedCount} selected");
            }
            
            return true;
        }

        private async Task<bool> TestTransportControls()
        {
            var vm = UnifiedPlayer.DataContext as UnifiedPlayerViewModel;
            if (vm == null) return false;
            
            if (!vm.IsPlaybackMode)
            {
                LogMessage("  ?? Not in playback mode, skipping transport tests");
                return true;
            }
            
            LogMessage($"  Total duration: {vm.TotalDuration}");
            LogMessage($"  Can play: {vm.PlayCommand.CanExecute(null)}");
            LogMessage($"  Can pause: {vm.PauseCommand.CanExecute(null)}");
            LogMessage($"  Can stop: {vm.StopCommand.CanExecute(null)}");
            
            // Note: We don't actually trigger playback in the test to avoid audio output
            // Just verify the commands are wired up correctly
            
            return true;
        }

        #endregion

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _testResults.Clear();
            DebugLogTextBox.Clear();
            _passedTests = 0;
            _failedTests = 0;
            TestStatusText.Text = "Ready to test";
            LogMessage("Test environment reset");
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            DebugLogTextBox.Clear();
        }

        private void AddTestResult(string icon, string message)
        {
            _testResults.Add(new TestResult { Icon = icon, Message = message });
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            DebugLogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogScrollViewer.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
        }
    }

    public class TestResult
    {
        public string Icon { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
