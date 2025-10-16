# AeroDebrief

**A comprehensive post-flight analysis and debriefing tool for DCS World radio communications**

AeroDebrief is an advanced audio analysis system designed to record, replay, and analyze radio communications from [SRS (SimpleRadio Standalone)](https://github.com/ciribob/DCS-SimpleRadioStandalone) during DCS World flight sessions. Built with .NET 9 and WPF, it provides powerful visualization and analytics tools for reviewing communications, understanding network activity, and conducting detailed post-mission debriefings.

---

## What is AeroDebrief?

AeroDebrief captures all SRS radio traffic during your DCS missions and presents it through an intuitive interface with advanced analytics capabilities:

* Multi-frequency recording - Capture communications across all active radio frequencies simultaneously
* Advanced waveform visualization - Interactive waveform display with zoom, pan, and timeline navigation
* Frequency-based filtering - Isolate and analyze specific frequencies with individual gain and pan controls
* Real-time analytics - Visualize user presence, power distribution, signal quality, and communication statistics
* Playback controls - Precise playback with transport controls, seeking, and timeline markers
* Network visualization - See who's connected to which frequencies in real-time with animated presence graphs

Whether you're conducting after-action reviews, training sessions, or just want to relive your missions, AeroDebrief provides the tools you need.

---

## System Architecture

AeroDebrief is built as a modular .NET 9 solution with clear separation of concerns.

---

## Projects in the Solution

### AeroDebrief.Core - Audio Processing Engine

The heart of the system, providing all core functionality for audio recording, processing, and analysis.

Key Capabilities:
* Audio Recording - Captures SRS audio packets with metadata (frequency, modulation, player info)
* Audio Playback - High-performance playback engine with seeking and timeline control
* Frequency Analysis - Real-time analysis of active frequencies and communication patterns
* Signal Processing - Audio mixing, filtering, and frequency-based waveform generation
* File Management - Read/write custom .srs recording format with efficient packet storage

Core Components: AudioPacketReader, AudioPacketRecorder, AudioProcessingEngine, PlaybackController, FrequencyAnalyzer, FilteredWaveformGenerator, FrequencyChannelMixer

Technologies: NAudio, OPUS codec, .NET 9

### AeroDebrief.UI - WPF User Interface

Modern, responsive WPF application providing visualization and control over recorded communications.

Key Features:
* Waveform Viewer - Interactive multi-frequency waveform display with zoom and pan
* Frequency Tree - Hierarchical display of all detected frequencies with filtering
* Transport Controls - Professional playback controls (play, pause, stop, seek)
* Frequency Mixer - Individual gain and pan controls for each frequency
* Analytics Dashboard - 4-tab analytics system (Presence Network, Power Levels, Signal Quality, Statistics)

Custom Controls: WaveformViewer, WaveformMiniMap, FrequencyTreeView, FrequencyMixer, PresenceGraphView

Technologies: WPF, XAML, MVVM pattern, .NET 9

### AeroDebrief.CLI - Command-Line Interface

Lightweight command-line tool for headless recording and automation scenarios.

Key Capabilities:
* Unattended Recording - Record SRS traffic without UI overhead
* Server Monitoring - Connect to SRS server and capture all communications
* Batch Processing - Process multiple recordings with scripting
* CI/CD Integration - Suitable for automated testing and analysis pipelines

Technologies: .NET 9 Console, Command-line parsing

### AeroDebrief.Integrations - External Integrations

Extension point for integrating with external systems and services.

Potential Integrations: DCS Integration, Discord Webhooks, Cloud Storage, Data Export, API Services

Technologies: HTTP clients, REST APIs, cloud SDKs

### AeroDebrief.Tests - Test Suite

Comprehensive test coverage for core functionality and critical paths.

Test Categories: Unit Tests, Integration Tests, Performance Tests, UI Tests

Technologies: xUnit, FluentAssertions, Moq

### External/SRS - SRS Common Libraries

Shared libraries from the SRS project providing protocol definitions, network communication, audio codecs, and audio processing utilities.

---

## Getting Started

### Prerequisites

* Windows 10/11 (64-bit)
* .NET 9 Runtime (included in installer)
* SRS Server version 2.3.20 or later
* DCS World (for recording live sessions)

### Quick Start

1. Download the latest release from GitHub Releases
2. Extract to your preferred location
3. Run AeroDebrief.UI.exe
4. Click "Load File" to select a recording
5. Use transport controls to play/pause/seek
6. Explore analytics tabs for communication insights

---

## Building from Source

Requirements: Visual Studio 2022 or later, .NET 9 SDK, Git

Build Steps:
```
git clone https://github.com/shalevohad/AeroDebrief.git
cd AeroDebrief
dotnet restore
dotnet build --configuration Release
dotnet run --project src/AeroDebrief.UI
```

---

## Key Features in Detail

### Multi-Frequency Recording
* Captures all active SRS frequencies simultaneously
* Preserves metadata (modulation, coalition, player information)
* Efficient binary format (.srs) with OPUS compression

### Advanced Waveform Visualization
* Per-frequency waveform rendering with color coding
* Zoom and pan with minimap overview
* Activity heatmap showing communication density
* Timeline markers for important events

### Analytics Dashboard
* Presence Network - Visual representation of who's connected where
* Power Levels - Audio intensity trends over time
* Signal Quality - Communication reliability metrics
* Statistics - Comprehensive frequency usage reports

---

## Technology Stack

* .NET 9 - Modern, high-performance runtime
* WPF - Rich desktop UI framework
* NAudio - Professional audio processing
* OPUS - High-quality audio compression
* MVVM - Clean separation of concerns
* xUnit - Comprehensive test coverage

---

## Documentation

* Analytics Developer Guide - Extending the analytics system
* Analytics TODO - Planned features and enhancements
* Cleanup Summary - Recent project reorganization

---

## Contributing

Contributions are welcome! Fork the repository, create a feature branch, commit your changes, push to the branch, and open a Pull Request.

---

## License

This project is open source. See the LICENSE file for details.

---

## Acknowledgments

* SRS (SimpleRadio Standalone) - For the excellent radio communication system
* NAudio - For audio processing capabilities
* The DCS World community - For feedback and support

---

## Support

* Issues: GitHub Issues
* Discussions: GitHub Discussions

---

Built with love for the DCS World community
