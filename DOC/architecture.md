# Architecture Overview

This document describes the architecture of AeroDebrief as implemented in the current codebase. It focuses on the main components, responsibilities, and interactions between subsystems.

## Goals
- Capture and store networked voice packets from SRS-style servers
- Provide analysis tools for frequency presence and activity
- Support playback and export of recorded sessions
- Offer integration points for third-party tools (TacView, Lua exports)

## High-level components

- `AudioPacketRecorder` (Core)
  - Responsible for connecting to SRS servers (TCP control, UDP voice), extracting packet metadata and writing packets to disk.
  - Uses `TCPClientHandler` and `UDPVoiceHandler` from external SRS libraries.
  - Writes `AudioPacketMetadata` structures to a binary `.srs` file using a writer loop.

- `FileAnalyzer` (Core.Analysis)
  - Static utilities to scan `.srs` files and produce summaries such as `GetAllFrequencyModulations`, `CalculateTotalDuration`, and `AnalyzeAudioActivity`.
  - Used when loading files to populate frequency lists and to precompute analysis metrics.

- `AudioPacketReader` (Core)
  - High-performance playback engine that reads packets from a `.srs` file, applies frequency filtering, decodes OPUS frames, buffers audio, and writes to an audio output.
  - Integrates `AudioProcessingEngine`, `AudioOutputEngine`, `PlaybackController`, `AudioBufferManager`, and `FrequencyFilter`.
  - Supports exporting mixed audio to WAV and debugging auto-export.

- `FrequencyAnalysisService` (Core.Analysis)
  - Maintains live analysis state for frequencies and players.
  - Processes incoming `AudioPacketMetadata` objects to update player activity and triggers UI updates.

- UI layer (AeroDebrief.UI)
  - WPF frontend providing visualizations: waveform viewer, frequency tree, mixer, analytics dashboards.
  - Integration service `FrequencyAnalysisIntegrationService` orchestrates communication between UI view models and the core audio/analysis services.

- `AeroDebrief.Integrations`
  - Currently a placeholder project intended for external connectors such as TacView exporters.

## Data flow

1. Recording (live)
   - `AudioPacketRecorder` connects to SRS server -> receives UDP encoded audio packets -> extracts metadata into `AudioPacketMetadata` -> enqueues metadata for disk writer -> `AudioPacketMetadata` is written into `.srs` file.

2. Analysis/Loading
   - UI requests to load a `.srs` file -> `AudioPacketReader`/`FileAnalyzer` scan the file -> `GetAllFrequencyModulations` returns frequency groups with player summaries -> `FrequencyAnalysisIntegrationService` populates view models.

3. Playback
   - User triggers playback -> `AudioPacketReader` uses `AudioBufferManager` to pre-buffer decoded audio -> `AudioOutputEngine` outputs audio to WASAPI/NAudio -> `PlaybackController` manages timeline, seeks, and progress events -> UI updates.

4. Real-time presence
   - During playback or live capture, `FrequencyAnalysisService.ProcessPacket` and related events propagate player activity updates which the UI consumes for presence graphs and analytics.

## Deployment

The project is a solution of multiple .NET projects. The main deliverable for users is the WPF `AeroDebrief.UI` executable. CLI tooling and integrations are separate projects.

## Diagram

See `DOC/architecture.puml` for PlantUML source describing the main components and their relationships. Render the file to generate PNG/SVG/UML diagrams.
