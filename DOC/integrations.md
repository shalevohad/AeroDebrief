# Integrations

This document describes the current state of the `AeroDebrief.Integrations` project and suggested patterns for implementing TacView and DCS Lua integrations using the existing codebase.

## Current state

- `src/AeroDebrief.Integrations/Placeholder.cs` contains a placeholder `IntegrationsPlaceholder` class with documentation comments describing intended integration points.
- No concrete integration code exists in the repository at this time.

## Integration goals

Typical target integrations are:

- TacView exporter: convert recorded session data (player tracks, positions, events) into TacView-compatible flight tracks or markers, allowing synchronized visualization of audio events with flight telemetry.
- DCS Lua export: generate Lua mission or export artifacts that can be used inside DCS workflows (e.g. markers, telemetry snapshots).

## Suggested implementation approach

1. Add a dedicated `TacViewExporter` class in `AeroDebrief.Integrations` that accepts:
   - `IEnumerable<AudioPacketMetadata>` (packets) or `FileAnalyzer` results
   - Optional player position/time information (if available from `ConnectedClientsSingleton` or other sources)

2. The exporter should map `AudioPacketMetadata.Timestamp` to TacView timeline timestamps and produce `Track` and `Event` entries accordingly.

3. Provide configurable options for the exporter:
   - Time windowing and aggregation (group packets into events)
   - Mapping of player GUIDs to readable names and colors
   - Output format (TacView .TVF, JSON, or tacview compatible script)

4. Add unit tests that feed a small `.srs` test file and validate the generated TacView output structure.

## Hook points in the codebase

- Use `FileAnalyzer.ReadAllPackets` to iterate packets for the export
- Use `FrequencyAnalysisService` to identify the most active players and period boundaries
- Use `ConnectedClientsSingleton.Instance` to enrich player position/unit data (if the external snapshot is available)

## Automation and CI

- The repository's docs CI can be extended to run exporter unit tests on push/PR.
- If an exporter auto-commits artifacts (e.g., generated TacView files), follow the repository's contribution model and avoid committing generated artifacts directly to `main` without a PR.
