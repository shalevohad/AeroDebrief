// using System;
// using System.Diagnostics;
// using System.Linq;
// using System.Threading.Tasks;
// using Windows.Devices.Enumeration;
// using Windows.Media.MediaProperties; 
// using Windows.Media.Audio;
// using Windows.Media.Capture;
// using Windows.Media.Devices;
// using Windows.Media.Render;
//
// //TODO - try audiograph!
// public sealed class AudioGraphManager : IDisposable
// {
//     private AudioGraph? _graph;
//     private AudioDeviceInputNode? _deviceInputNode;
//     private AudioDeviceOutputNode? _deviceOutputNode;
//
//     // Optional: Store device info for reference
//     private DeviceInformation? _currentInputDevice;
//     private DeviceInformation? _currentOutputDevice;
//
//     // Device Watchers
//     private DeviceWatcher? _inputDeviceWatcher;
//     private DeviceWatcher? _outputDeviceWatcher;
//
//     public event EventHandler<string>? StatusChanged;
//     public event EventHandler? AudioDevicesChanged; // To notify UI to update lists
//
//     // --- Initialization and Control ---
//
// public async Task InitializeAsync(DeviceInformation? inputDevice = null, DeviceInformation? outputDevice = null)
// {
//     await CleanupGraphAsync();
//
//     OnStatusChanged("Initializing Audio Graph with specific formats...");
//
//     // --- Define Desired Encoding Properties ---
//     // Input: 16 kHz, 1 channel (Mono), Float (usually preferred by graph)
//     AudioEncodingProperties inputEncodingProps = AudioEncodingProperties.CreatePcm(16000, 1, 32); // SampleRate, Channels, BitDepth
//     inputEncodingProps.Subtype = MediaEncodingSubtypes.Float; // Use Float for processing
//
//     // Graph/Output: 48 kHz, 2 channels (Stereo), Float
//     AudioEncodingProperties graphEncodingProps = AudioEncodingProperties.CreatePcm(48000, 2, 32);
//     graphEncodingProps.Subtype = MediaEncodingSubtypes.Float;
//
//     // --- Configure Graph Settings ---
//     AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Communications)
//     {
//         EncodingProperties = graphEncodingProps, // Set graph's processing format (matches desired output),
//         
//         QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency,
//
//     };
//
//     // Determine and set Primary Render (Output) Device
//     DeviceInformation? actualOutputDevice = outputDevice ?? await GetDefaultDeviceAsync(DeviceClass.AudioRender);
//     if (actualOutputDevice != null)
//     {
//         settings.PrimaryRenderDevice = actualOutputDevice;
//         _currentOutputDevice = actualOutputDevice;
//         OnStatusChanged($"Using output device: {actualOutputDevice.Name}");
//     }
//     else
//     {
//         OnStatusChanged("Error: No output device found.");
//         return;
//     }
//
//     // --- Create Graph ---
//     CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
//
//     if (result.Status != AudioGraphCreationStatus.Success)
//     {
//         OnStatusChanged($"Error creating AudioGraph: {result.Status} (Check if output device supports {graphEncodingProps.SampleRate}Hz Stereo?)");
//         if (result.ExtendedError != null) Debug.WriteLine($"Extended Error: {result.ExtendedError.Message}");
//         await CleanupGraphAsync();
//         return;
//     }
//     _graph = result.Graph;
//     _graph.UnrecoverableErrorOccurred += Graph_UnrecoverableErrorOccurred;
//     OnStatusChanged($"Audio Graph created (Targeting {graphEncodingProps.SampleRate}Hz Stereo).");
//
//     // --- Create Output Node ---
//     // It will use the graph's EncodingProperties (48kHz Stereo)
//     CreateAudioDeviceOutputNodeResult outputNodeResult = await _graph.CreateDeviceOutputNodeAsync();
//     if (outputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
//     {
//         OnStatusChanged($"Error creating output node: {outputNodeResult.Status} (Check if output device '{actualOutputDevice.Name}' supports {graphEncodingProps.SampleRate}Hz Stereo)");
//         await CleanupGraphAsync();
//         return;
//     }
//     _deviceOutputNode = outputNodeResult.DeviceOutputNode;
//     OnStatusChanged("Output node created.");
//
//     // --- Create Input Node (Requesting Specific Format) ---
//     DeviceInformation? actualInputDevice = inputDevice ?? await GetDefaultDeviceAsync(DeviceClass.AudioCapture);
//     if (actualInputDevice == null)
//     {
//         OnStatusChanged("Error: No input device found.");
//         await CleanupGraphAsync();
//         return;
//     }
//     _currentInputDevice = actualInputDevice;
//     OnStatusChanged($"Attempting input device: {_currentInputDevice.Name} (Requesting {inputEncodingProps.SampleRate}Hz Mono)");
//
//     // *** Critical Step: Request specific format for input node ***
//     CreateAudioDeviceInputNodeResult inputNodeResult = await _graph.CreateDeviceInputNodeAsync(
//         MediaCategory.Communications,
//         inputEncodingProps, // Request 16kHz Mono Float
//         _currentInputDevice);
//
//     if (inputNodeResult.Status !=  AudioDeviceNodeCreationStatus.Success)
//     {
//         // *** This is where format incompatibility is often detected ***
//         if (inputNodeResult.Status ==  AudioDeviceNodeCreationStatus.FormatNotSupported)
//         {
//             OnStatusChanged($"Error: Input device '{_currentInputDevice.Name}' does not support the requested format ({inputEncodingProps.SampleRate}Hz Mono) or a compatible format the system can convert from.");
//             // You might want to query the device's supported formats here for debugging
//             // See: https://docs.microsoft.com/en-us/uwp/api/windows.media.devices.audiodevicecontroller.getavailablemediastreamproperties
//         }
//         else if (inputNodeResult.Status ==  AudioDeviceNodeCreationStatus.DeviceNotAvailable)
//         {
//              OnStatusChanged($"Error: Input device '{_currentInputDevice.Name}' not available.");
//         }
//         else
//         {
//             OnStatusChanged($"Error creating input node: {inputNodeResult.Status}");
//         }
//         await CleanupGraphAsync();
//         return;
//     }
//
//     _deviceInputNode = inputNodeResult.DeviceInputNode;
//     // If successful, the node IS operating at 16kHz Mono (or compatible)
//     OnStatusChanged("Input node created (Format accepted).");
//     Debug.WriteLine($"Input Node Encoding: {_deviceInputNode.EncodingProperties.SampleRate}Hz, {_deviceInputNode.EncodingProperties.ChannelCount} channels, {_deviceInputNode.EncodingProperties.Subtype}");
//
//
//     // --- Connect Nodes ---
//     // Graph engine will handle conversion if input (16k Mono) != output (48k Stereo)
//     _deviceInputNode.AddOutgoingConnection(_deviceOutputNode);
//     OnStatusChanged("Nodes connected. Graph should handle format conversion.");
//
//     SetupDeviceWatchers(); // Setup watchers as before
// }
//     public void Start()
//     {
//         if (_graph != null && _deviceInputNode != null && _deviceOutputNode != null)
//         {
//             try
//             {
//                 _graph.Start();
//                 OnStatusChanged("Audio Graph started.");
//             }
//             catch (Exception ex)
//             {
//                  OnStatusChanged($"Error starting graph: {ex.Message}");
//                  // Consider cleaning up if start fails catastrophically
//             }
//         }
//         else
//         {
//             OnStatusChanged("Cannot start: Graph not fully initialized.");
//         }
//     }
//
//     public void Stop()
//     {
//         if (_graph != null)
//         {
//              try
//              {
//                  _graph.Stop();
//                  OnStatusChanged("Audio Graph stopped.");
//              }
//              catch(Exception ex)
//              {
//                  // Stopping can sometimes throw if the graph is in a bad state
//                  OnStatusChanged($"Error stopping graph: {ex.Message}");
//              }
//         }
//     }
//
//     // --- Device Handling ---
//
//     private void SetupDeviceWatchers()
//     {
//         // Stop existing watchers first
//         StopDeviceWatchers();
//
//         // Watch for Audio Input devices
//         _inputDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);
//         _inputDeviceWatcher.Added += OnDeviceAdded;
//         _inputDeviceWatcher.Removed += OnDeviceRemoved;
//         _inputDeviceWatcher.Updated += OnDeviceUpdated;
//         _inputDeviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
//         _inputDeviceWatcher.Stopped += OnWatcherStopped;
//
//         // Watch for Audio Output devices
//         _outputDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioRender);
//         _outputDeviceWatcher.Added += OnDeviceAdded;
//         _outputDeviceWatcher.Removed += OnDeviceRemoved;
//         _outputDeviceWatcher.Updated += OnDeviceUpdated;
//         _outputDeviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
//         _outputDeviceWatcher.Stopped += OnWatcherStopped;
//
//         OnStatusChanged("Starting device watchers...");
//         _inputDeviceWatcher.Start();
//         _outputDeviceWatcher.Start();
//
//         // Subscribe to default device changes
//         MediaDevice.DefaultAudioRenderDeviceChanged += MediaDevice_DefaultAudioRenderDeviceChanged;
//         MediaDevice.DefaultAudioCaptureDeviceChanged += MediaDevice_DefaultAudioCaptureDeviceChanged;
//     }
//
//     private void StopDeviceWatchers()
//     {
//         // Unsubscribe from default device changes
//         MediaDevice.DefaultAudioRenderDeviceChanged -= MediaDevice_DefaultAudioRenderDeviceChanged;
//         MediaDevice.DefaultAudioCaptureDeviceChanged -= MediaDevice_DefaultAudioCaptureDeviceChanged;
//
//         if (_inputDeviceWatcher != null)
//         {
//             if (_inputDeviceWatcher.Status == DeviceWatcherStatus.Started || _inputDeviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
//             {
//                 _inputDeviceWatcher.Stop();
//             }
//             _inputDeviceWatcher.Added -= OnDeviceAdded;
//             _inputDeviceWatcher.Removed -= OnDeviceRemoved;
//             _inputDeviceWatcher.Updated -= OnDeviceUpdated;
//             _inputDeviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
//             _inputDeviceWatcher.Stopped -= OnWatcherStopped;
//             _inputDeviceWatcher = null;
//         }
//         if (_outputDeviceWatcher != null)
//         {
//              if (_outputDeviceWatcher.Status == DeviceWatcherStatus.Started || _outputDeviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
//              {
//                 _outputDeviceWatcher.Stop();
//              }
//             _outputDeviceWatcher.Added -= OnDeviceAdded;
//             _outputDeviceWatcher.Removed -= OnDeviceRemoved;
//             _outputDeviceWatcher.Updated -= OnDeviceUpdated;
//             _outputDeviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
//             _outputDeviceWatcher.Stopped -= OnWatcherStopped;
//             _outputDeviceWatcher = null;
//         }
//          OnStatusChanged("Device watchers stopped.");
//     }
//
//     // --- Event Handlers ---
//
//     private async void Graph_UnrecoverableErrorOccurred(AudioGraph graph, AudioGraphUnrecoverableErrorOccurredEventArgs args)
//     {
//         // This is critical for handling device removal WHILE IN USE
//         OnStatusChanged($"FATAL: Audio Graph Unrecoverable Error: {args.Error}");
//
//
//         // Corrected check:
//         bool deviceRemovedError = args.Error == AudioGraphUnrecoverableError.AudioDeviceLost ||
//                                   args.Error == AudioGraphUnrecoverableError.AudioSessionDisconnected; 
//         // AudioSessionDisconnected can also occur in some device removal scenarios
//
//         if (deviceRemovedError)
//         {
//             OnStatusChanged("Error likely due to active audio device removal. Resetting graph.");
//             // The graph is dead. Clean up and potentially try to restart with defaults.
//             await CleanupGraphAsync(); // Dispose graph and nodes
//
//             // Optional: Automatically try to reinitialize with default devices
//             // Run on UI thread if updating UI elements
//             // Consider adding a delay or user prompt before re-initializing
//             // await DispatcherQueue.TryEnqueueAsync(async () => // Example for WinUI 3
//             // {
//                  OnStatusChanged("Attempting to reinitialize with default devices...");
//                  await InitializeAsync(); // Re-init with defaults
//                  // If auto-restart desired: Start();
//             // });
//         }
//         else
//         {
//              OnStatusChanged("Unhandled unrecoverable error. Manual intervention may be needed.");
//             // Consider more specific error handling based on args.Error HResult
//              await CleanupGraphAsync(); // Clean up anyway
//         }
//     }
//
//     private async void MediaDevice_DefaultAudioRenderDeviceChanged(object? sender, DefaultAudioRenderDeviceChangedEventArgs args)
//     {
//         // The system default OUTPUT device changed.
//         OnStatusChanged($"System Default Output Device Changed: {args.Id} ({args.Role})");
//
//         // Get the new default device info
//         DeviceInformation? newDefaultOutput = await GetDefaultDeviceAsync(DeviceClass.AudioRender);
//         string newDefaultName = newDefaultOutput?.Name ?? "None";
//         OnStatusChanged($"New default output device: {newDefaultName}");
//
//         // Option 1: Informational only (let user manually switch via UI)
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Notify UI to update
//
//         // Option 2: Automatically switch the graph (more complex, involves recreation)
//         // Check if the new default is different from the *currently used* device
//          if (_graph != null && _currentOutputDevice != null && newDefaultOutput != null && newDefaultOutput.Id != _currentOutputDevice.Id)
//          {
//              OnStatusChanged($"Switching graph to new default output device: {newDefaultName}");
//              // Need to stop, dispose, and re-create the graph with the new device
//              await ReinitializeGraphAsync(_currentInputDevice, newDefaultOutput);
//          }
//          else if (_currentOutputDevice == null && newDefaultOutput != null) {
//              // Maybe we didn't have one before, try initializing now
//              OnStatusChanged($"New default output device detected. Attempting initialization.");
//              await ReinitializeGraphAsync(_currentInputDevice, newDefaultOutput);
//          }
//     }
//
//     private async void MediaDevice_DefaultAudioCaptureDeviceChanged(object? sender, DefaultAudioCaptureDeviceChangedEventArgs args)
//     {
//         // The system default INPUT device changed.
//         OnStatusChanged($"System Default Input Device Changed: {args.Id} ({args.Role})");
//
//         DeviceInformation? newDefaultInput = await GetDefaultDeviceAsync(DeviceClass.AudioCapture);
//         string newDefaultName = newDefaultInput?.Name ?? "None";
//         OnStatusChanged($"New default input device: {newDefaultName}");
//
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Notify UI
//
//         // Automatically switch graph
//          if (_graph != null && _currentInputDevice != null && newDefaultInput != null && newDefaultInput.Id != _currentInputDevice.Id)
//          {
//              OnStatusChanged($"Switching graph to new default input device: {newDefaultName}");
//              await ReinitializeGraphAsync(newDefaultInput, _currentOutputDevice);
//          }
//          else if (_currentInputDevice == null && newDefaultInput != null) {
//              OnStatusChanged($"New default input device detected. Attempting initialization.");
//              await ReinitializeGraphAsync(newDefaultInput, _currentOutputDevice);
//          }
//     }
//
//     // Device Watcher Events - Primarily for updating available device lists (UI)
//     private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
//     {
//         string type = (sender == _inputDeviceWatcher) ? "Input" : "Output";
//         Debug.WriteLine($"{type} device added: {deviceInfo.Name} ({deviceInfo.Id})");
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Notify UI
//     }
//
//     private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
//     {
//         string type = (sender == _inputDeviceWatcher) ? "Input" : "Output";
//         Debug.WriteLine($"{type} device removed: {deviceInfoUpdate.Id}");
//
//         // Check if the *currently used* device was removed.
//         // Note: Graph_UnrecoverableErrorOccurred is usually the primary handler for this
//         //       when the graph is active. This watcher event is more general.
//         bool removedCurrentInput = type == "Input" && _currentInputDevice?.Id == deviceInfoUpdate.Id;
//         bool removedCurrentOutput = type == "Output" && _currentOutputDevice?.Id == deviceInfoUpdate.Id;
//
//         if ((removedCurrentInput || removedCurrentOutput) && _graph != null)
//         {
//             OnStatusChanged($"Currently used {type} device ({deviceInfoUpdate.Id}) removed (detected by watcher).");
//             // The UnrecoverableError event should handle the graph reset if it was running.
//             // If the graph wasn't running, we might just need to nullify the device ref.
//             if (_graph.Status != AudioGraphStatus.Started)
//             {
//                 if (removedCurrentInput) _currentInputDevice = null;
//                 if (removedCurrentOutput) _currentOutputDevice = null;
//                 OnStatusChanged("Graph was not running. Device reference cleared.");
//                 // Consider prompting user or attempting re-init later if needed.
//             }
//         }
//
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Notify UI
//     }
//
//     private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
//     {
//         string type = (sender == _inputDeviceWatcher) ? "Input" : "Output";
//        // Debug.WriteLine($"{type} device updated: {deviceInfoUpdate.Id}"); // Can be noisy
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Notify UI (e.g., state change)
//     }
//
//      private void OnEnumerationCompleted(DeviceWatcher sender, object args)
//     {
//         string type = (sender == _inputDeviceWatcher) ? "Input" : "Output";
//         Debug.WriteLine($"{type} device enumeration completed.");
//         AudioDevicesChanged?.Invoke(this, EventArgs.Empty); // Final update to UI list
//     }
//
//      private void OnWatcherStopped(DeviceWatcher sender, object args)
//     {
//         string type = (sender == _inputDeviceWatcher) ? "Input" : "Output";
//         Debug.WriteLine($"{type} device watcher stopped.");
//     }
//
//     // --- Helper Methods ---
//
//     private async Task ReinitializeGraphAsync(DeviceInformation? inputDevice, DeviceInformation? outputDevice)
//     {
//          bool wasRunning = _graph?.Status == AudioGraphStatus.Started;
//          Stop(); // Stop current graph if any
//          await CleanupGraphAsync(); // Dispose current graph
//          OnStatusChanged("Re-initializing graph due to device change...");
//          await InitializeAsync(inputDevice, outputDevice); // Init with new devices
//          if (wasRunning && _graph != null) // If it was running before, try starting again
//          {
//              Start();
//          }
//     }
//
//     private async Task CleanupGraphAsync()
//     {
//         OnStatusChanged("Cleaning up audio graph resources...");
//         StopDeviceWatchers(); // Stop watchers first to prevent race conditions
//
//         if (_graph != null)
//         {
//             Stop(); // Ensure graph is stopped
//
//             // It's generally recommended to dispose nodes explicitly before the graph,
//             // though disposing the graph should handle contained nodes.
//             _deviceInputNode?.Dispose();
//             _deviceOutputNode?.Dispose();
//
//              _graph.UnrecoverableErrorOccurred -= Graph_UnrecoverableErrorOccurred;
//             _graph.Dispose(); // Dispose the graph itself
//         }
//
//         _deviceInputNode = null;
//         _deviceOutputNode = null;
//         _graph = null;
//         _currentInputDevice = null;
//         _currentOutputDevice = null;
//          OnStatusChanged("Graph cleanup complete.");
//     }
//
//     private static async Task<DeviceInformation?> GetDefaultDeviceAsync(DeviceClass deviceClass)
//     {
//         string deviceSelector = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
//         if (deviceClass == DeviceClass.AudioCapture)
//         {
//             deviceSelector = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
//         }
//
//         if (string.IsNullOrEmpty(deviceSelector))
//         {
//             // No default device found for the role
//             Debug.WriteLine($"No default device found for {deviceClass}");
//             // Fallback: Try finding *any* device of that class? Or return null.
//             var allDevices = await DeviceInformation.FindAllAsync(deviceClass);
//              if (allDevices.Any()) {
//                  Debug.WriteLine($"Warning: No default {deviceClass} device. Will try using first available: {allDevices.First().Name}");
//                  // This might not be what the user expects.
//                  return allDevices.First();
//              }
//             return null;
//         }
//
//         try
//         {
//             return await DeviceInformation.CreateFromIdAsync(deviceSelector);
//         }
//         catch (Exception ex)
//         {
//              Debug.WriteLine($"Error getting default device info for ID {deviceSelector}: {ex.Message}");
//              return null; // Device might exist but is inaccessible?
//         }
//     }
//
//      // Helper to raise status changed event (ensure UI thread if needed)
//     private void OnStatusChanged(string message)
//     {
//         Debug.WriteLine($"AudioGraphManager Status: {message}");
//         // If updating UI, use dispatcher:
//         // DispatcherQueue.TryEnqueue(() => StatusChanged?.Invoke(this, message));
//         StatusChanged?.Invoke(this, message); // Direct invoke if not updating UI directly
//     }
//
//     // --- IDisposable ---
//
//     public async void Dispose() // Note: Async void in Dispose is generally discouraged, but needed for CleanupGraphAsync
//     {
//         // Best practice: Call an async cleanup method and wait if possible,
//         // but Dispose itself cannot be async. This is a common pattern.
//         await CleanupGraphAsync();
//         // Ensure watchers are stopped if CleanupGraphAsync failed partially
//         StopDeviceWatchers();
//         GC.SuppressFinalize(this); // Prevent finalizer from running
//          Debug.WriteLine("AudioGraphManager disposed.");
//     }
//
//      // Optional: Finalizer as a safeguard (if Dispose isn't called)
//      ~AudioGraphManager()
//      {
//          // Don't call async methods from finalizer.
//          // Do minimal cleanup possible. Direct graph/node disposal might be okay
//          // but interacting with watchers or async operations is risky.
//          StopDeviceWatchers(); // Try to stop watchers synchronously if possible
//          _deviceInputNode?.Dispose();
//          _deviceOutputNode?.Dispose();
//          _graph?.Dispose();
//          Debug.WriteLine("AudioGraphManager finalizer executed.");
//      }
// }