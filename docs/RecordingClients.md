# Recording Clients — Concise Reference

This document summarizes the recording-related projects in this repository and explains how they fit together, how to build/run them, and common usage scenarios.

## Projects

- `DCS-SRS-RecordingClient` (main recording client)
  - Purpose: Captures DCS SimpleRadio (SRS) audio and metadata to a recording file format used by the Player Client.
  - Key responsibilities: packet capture, timestamping, player/frequency metadata extraction, file write/rotation.

- `DCS-SRS-PlayerClient` (playback & analysis UI)
  - Purpose: Windows Forms application for playback, waveform visualization, frequency filtering and post-recording analysis.
  - Notable features: waveform seeking, frequency tree filtering, live analysis panels, bookmarks and recent files.

- `External/SRS/Common` and `External/SRS/SharedAudio`
  - Purpose: Shared SRS protocol code and audio helper utilities used by both recording and player projects.
  - Contains common models, packet parsing, and audio processing helpers.

- `AudioTestConsole` / `DCS-SRS-RecordingClient.CLI`
  - Purpose: Console tools for quick audio/device diagnostics and headless recording or analysis tasks.

## File / Format

- Primary recording format: SRS raw recording (.raw) with embedded metadata (player/frequency/timestamps).
- Files produced by the recording client are the canonical input for the Player Client. They contain both audio payload and packet metadata required for full analysis.

## Build & Run (Concise)

Prerequisites: .NET 9 SDK/Runtime

1. Restore and build:
   - `dotnet restore`
   - `dotnet build --configuration Release`

2. Run the recording client or player from their project directories:
   - Recording client (UI/CLI) — use Visual Studio or `dotnet run --project DCS-SRS-RecordingClient`
   - Player client (playback) — `dotnet run --project DCS-SRS-PlayerClient`

3. For console utilities: `dotnet run --project AudioTestConsole` or `DCS-SRS-RecordingClient.CLI`.

## Integration Notes

- The Player Client depends on recorded file metadata to reconstruct player/frequency information and visualizations. Ensure the recording client is configured to include full metadata when recording.
- Shared SRS libraries in `External/SRS` provide packet format compatibility. Changes to these libraries must be kept backward compatible.

## Troubleshooting (Common)

- No audio in playback: verify the recording file contains audio (non-zero size) and use `AudioTestConsole` to validate device playback.
- Missing metadata: check recorder configuration and that packet parsing succeeds (inspect logs). The recorder must capture and write metadata fields for full Player features.
- Build issues: ensure .NET 9 SDK is installed and restore NuGet packages before building.

## Contribution

- Add new recording features in the `DCS-SRS-RecordingClient` project and keep shared protocol changes in `External/SRS`.
- Update the Player Client only when backwards-compatible changes are made to the recording format, or update both in a single PR.

For more detailed architecture and usage, see `docs/PlayerClient.md` and inline code documentation in each project.
