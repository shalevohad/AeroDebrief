# SRS Signal Analyzer UI

## Overview

The SRS Signal Analyzer UI is a fully interactive WPF application designed for analyzing and visualizing SRS (Simple Radio Standalone) radio communications. This application provides advanced DSP capabilities, real-time spectrum analysis, and frequency mixing capabilities.

## Features

### Core Components

1. **Waveform Viewer**
   - Full file waveform visualization using Canvas/WriteableBitmap
   - Interactive seek functionality (click/drag)
   - Real-time playhead movement during playback
   - Data sourced from `Core.AudioSession.GetWaveformData()`

2. **Spectral Analyzer**
   - Real-time spectrum view using FFT analysis
   - Updated via `Core.FrequencyAnalyzer.GetSpectrumSnapshot()`
   - Frequency grid with dB scale
   - Color-coded frequency bins

3. **Frequency TreeView**
   - Organized frequency groups: VHF (30–88 MHz), UHF (225–400 MHz)
   - Checkboxes for activating/deactivating frequencies
   - Integration with `Core.Mixer.SetChannelActive(...)`
   - Packet count display per frequency

4. **Frequency Mixer Panel**
   - Individual gain sliders per selected frequency
   - Pan controls (Left/Right) for spatial positioning
   - Real-time updates to Core via `SetChannelGain` and `SetChannelPan`

5. **Transport Controls**
   - Play / Pause / Stop functionality
   - Progress bar with interactive seeking
   - Time display with current and total duration
   - Bound to `OnPlaybackProgress` events from Core

### Architecture

- **UI Layer**: WPF with modern Material Design-inspired styling
- **Core Integration**: Exclusively communicates with SRS.Core APIs
- **Data Flow**: UI ? Core APIs ? Audio Processing ? Hardware
- **Real-time Updates**: DispatcherTimer for 20 FPS UI updates, 10 FPS spectrum updates

### Key APIs Used

From `SRS.Core`:
- `AudioSession.GetWaveformData()` - Amplitude envelope data
- `FrequencyAnalyzer.GetSpectrumSnapshot()` - FFT spectrum data  
- `AudioSession.GetAvailableFrequencies()` - Available frequency list
- `Mixer.SetChannelActive(frequency, bool)` - Channel activation
- `Mixer.SetChannelGain(frequency, float)` - Channel gain control
- `Mixer.SetChannelPan(frequency, float)` - Channel pan control
- `AudioSession.Play()`, `Pause()`, `Stop()` - Playback control

### Modern UI Design

- Clean, light color scheme with subtle shadows
- Rounded corners and modern typography (Segoe UI)
- Responsive layout with splitters for user customization
- Material Design-inspired buttons and controls
- Real-time visual feedback during playback

### Technology Stack

- **.NET 9** targeting Windows
- **WPF** for rich desktop UI
- **Canvas-based** custom controls for high-performance rendering
- **MVVM pattern** with ViewModels and data binding
- **Dependency Injection** ready architecture

### Usage

1. **Load File**: Click "Load File" to select an SRS recording
2. **Analyze**: Click "Analyze" to process the file for frequency information
3. **Select Frequencies**: Use the tree view to select frequencies for playback
4. **Adjust Mix**: Use the mixer panel to adjust gain and pan for active frequencies
5. **Playback**: Use transport controls to play, pause, stop, and seek through the recording
6. **Visualize**: Watch the waveform playhead and real-time spectrum during playback

### Custom Controls

All custom controls inherit from WPF base classes and provide:
- **WaveformViewer**: Canvas-based waveform rendering with interactive seeking
- **SpectrumAnalyzer**: Real-time FFT visualization with frequency grid
- **FrequencyTreeView**: Hierarchical frequency selection with checkboxes
- **FrequencyMixer**: Dynamic mixer panel with gain/pan controls per frequency
- **TransportControls**: Media player-style transport with progress tracking

### Styling

Modern, cohesive styling defined in `ModernStyles.xaml`:
- Consistent color palette and typography
- Hover and focus states for interactive elements
- Drop shadows and rounded corners for depth
- Responsive design patterns

This application demonstrates a complete integration with the SRS.Core processing engine while providing a professional, user-friendly interface for signal analysis and mixing.