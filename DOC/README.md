# AeroDebrief Documentation

This folder contains developer-oriented documentation for the AeroDebrief project. The goal is to document current implementation details, architecture, and usage for intermediate+ programmers and technical users.

Files in this folder:

- `architecture.md` — High-level architecture, component responsibilities and data flow.
- `architecture.puml` — PlantUML source for the architecture and several class diagrams. Render with PlantUML to generate diagrams.
- `core-audio.md` — Details of the audio capture, recording, decoding, buffering, processing and playback systems implemented in `AeroDebrief.Core`.
- `analysis.md` — File-level analysis and real-time frequency analysis subsystems (`FileAnalyzer`, `FrequencyAnalysisService`).
- `ui.md` — How the UI integrates with core services (integration service, view models, controls).
- `integrations.md` — Current state of the `AeroDebrief.Integrations` project (placeholder) and guidance for TacView/DCS Lua connectors based on existing code.
- `devops.md` — Build, test and CI guidance. Includes a GitHub Actions workflow that runs on push/PR to validate builds.

Rendering diagrams
------------------
1. Install PlantUML (or use an online renderer)
2. From the repository root run:
   `plantuml DOC/architecture.puml` (or open the file with your PlantUML-enabled editor)

CI behaviour
------------
A basic GitHub Actions workflow `/.github/workflows/docs-ci.yml` is included that runs on push and pull_request and performs a `dotnet build` to validate the repository. This workflow can be extended to run doc generation steps if/when a generator is added.

Notes
-----
All documentation here reflects the current code in the repository and avoids speculation about future or unimplemented features. If you want this documentation kept in sync automatically (e.g. regenerated or validated on PR), extend the Action in `/.github/workflows/docs-ci.yml` to run the desired generation and commit steps.
