# Analysis Subsystems

This document describes the implementation of file-based analysis and real-time frequency analysis.

## FileAnalyzer (src/AeroDebrief.Core/Analysis/FileAnalyzer.cs)

Responsibilities:
- Scan `.srs` files and extract frequency/modulation combinations
- Produce player statistics per frequency (`GetAllFrequencyModulations`)
- Calculate recording duration (`CalculateTotalDuration`)
- Perform audio activity analysis to detect active periods (`AnalyzeAudioActivity`)
- Provide a streaming iterator over packets (`ReadAllPackets`)

Key implementation points:
- Reads `AudioPacketMetadata` structures from the file using `BinaryReader`.
- Maintains a dictionary of `(frequency, modulation)` to `FrequencyModulationInfo` while scanning.
- Uses a private `PlayerStatsCollector` to accumulate per-transmitter stats and then converts them into `PlayerFrequencyInfo` objects.
- `AnalyzeAudioActivity` inspects raw audio payload bytes to detect amplitude above a silence threshold and groups successive audio-bearing packets into `AudioActivityPeriod` objects.

Outputs:
- `FrequencyModulationInfo` list used by the UI when populating the frequency tree
- `AudioActivityAnalysis` summary used for diagnostics and activity visualization

## FrequencyAnalysisService (src/AeroDebrief.Core/Analysis/FrequencyAnalysisService.cs)

Responsibilities:
- Maintain an in-memory model of frequency channels and player analyses for real-time or playback-driven updates
- Accept `AudioPacketMetadata` via `ProcessPacket` and update player activity, packet counts, and last activity timestamps
- Provide selection/filtering for frequencies
- Notify listeners (UI) via `AnalysisUpdated` and `PlayerActivityDetected` events

Key implementation points:
- Uses a `ConcurrentDictionary` keyed by `(double Frequency, Modulation Modulation)` to store `FrequencyChannelAnalysis` objects
- Uses a recurring timer (500ms) to update transient `IsActive` flags for players based on `LastActivity` (inactive after 2 seconds)
- `ProcessPacket` updates the relevant channel's `LastActivity` and the player `LastActivity`, `PacketCount`, and optionally replaces player display data when a richer `PlayerData` is available

Concurrency:
- A `_lockObject` protects selection updates and ensures safe snapshots are returned via `SelectedFrequencies` and `ChannelAnalysis` properties
- `ConcurrentDictionary` reduces lock contention for frequent `ProcessPacket` updates

Events:
- `AnalysisUpdated` (raised when selection changes or periodic updates flip active states)
- `PlayerActivityDetected` (raised immediately when a packet updates a player's state)

Usage:
- Typically wired via `FrequencyAnalysisIntegrationService` which bridges core analysis to the UI view models
- Can be reused for live analysis of incoming packets (recording) or driven by playback of file packets
