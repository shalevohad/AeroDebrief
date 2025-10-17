# UI Integration and Components

This document explains how the WPF UI (AeroDebrief.UI) connects to and uses core services for playback, analysis, and visualization.

## Main integration points

- `FrequencyAnalysisIntegrationService` (src/AeroDebrief.UI/Services/FrequencyAnalysisIntegrationService.cs)
  - Acts as the glue between `AudioPacketReader`/`AudioSession`/core analysis and the `MainViewModel` used by the WPF UI.
  - Subscribes to `AudioSession` events: `FrequencyAnalysisUpdated`, `SpectrumUpdated`, `WaveformUpdated` and updates `MainViewModel` properties accordingly.
  - Handles user actions: load file, change frequency selection, change gain/pan and triggers the corresponding core service methods.

- ViewModels and Controls
  - `MainViewModel` contains `AvailableFrequencies` (grouped `FrequencyGroupViewModel`) and waveform/spectrum data bound to controls.
  - Custom controls include `WaveformViewer`, `WaveformMiniMap`, `FrequencyTreeView`, and `FrequencyMixer`. These controls consume the view model data to render complex visuals.

## Loading a file
1. UI requests `FrequencyAnalysisIntegrationService.LoadFileAsync(filePath)`
2. The integration service calls `AudioSession.LoadFileAsync(filePath)` which uses `AudioPacketReader`/`FileAnalyzer` to scan and prepare data
3. Integration service calls `PopulateFrequencyTreeAsync()` to convert `FrequencyModulationInfo` into `FrequencyViewModel` objects and groups them by coalition
4. The UI displays the groups and frequency entries; selecting entries updates the core filter via `SetChannelActive` and triggers waveform updates

## Real-time updates
- During playback or live capture, the core services fire events that propagate through the integration service to the UI. For example:
  - `FrequencyAnalysisService` -> Integration service -> `MainViewModel` -> UI controls update activity indicators
  - Spectrum and waveform updates are bound to visualization controls to show live signal content

## Threading and UI safety
- The integration service marshals updates to the UI thread using `Application.Current.Dispatcher.BeginInvoke` or `InvokeAsync` to ensure WPF thread-safety.
- UI actions call into services which may be asynchronous; the integration service sets `StatusText` and `IsFileLoaded` flags for feedback.

## Extending the UI
- To add a new visualization or exporter, extend `MainViewModel` and add a new service method in `FrequencyAnalysisIntegrationService` that wires user commands to core actions.
- For performance-sensitive visuals, prefer immutable or lightweight DTOs that the UI binds to, and update them at a controlled rate to avoid excessive redraws (e.g., 100-500ms update frequency).
