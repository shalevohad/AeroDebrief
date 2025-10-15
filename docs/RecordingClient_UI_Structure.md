# Recording Client — UI Folder & Component Structure (Concise)

This document describes the folder layout and primary UI components for the Recording Client (capture side). Use this as a quick reference when adding new views or components.

Root: `DCS-SRS-RecordingClient` (UI project: `DCS-SRS-RecordingClient.UI`)

Target framework: .NET 9

> Important: The recording client is part of a larger solution where `Core` holds shared application logic and the `External/SRS` projects provide protocol and audio helpers. Design UI and services so they depend on `Core` and reuse types and services from `External/SRS` rather than introducing duplicate implementations.

- `Infrastructure/`
  - `ServiceConfiguration.cs` - DI/service registration for recorder UI
  - `ServiceContainer.cs` - lightweight DI container
  - `MainFormFactory.cs` - builds and initializes main recording form
  - `RecordingCommands.cs` - central recording command definitions

- `Views/`
  - `MainRecorderForm.cs` - main Windows Form (entry UI for recording)
  - `MainRecorderForm.Designer.cs` - designer file
  - `Tabs/`
    - `CaptureTabView.cs` - primary capture controls and device selection
    - `SessionsTabView.cs` - recorded sessions, file rotation and management
    - `DiagnosticsTabView.cs` - audio device diagnostics and test tone controls
  - `Components/`
    - `RecordingControlComponent.cs` - start/stop/pause controls, recording state
    - `DeviceSelectionComponent.cs` - audio device selection and properties
    - `PacketInspectorComponent.cs` - real-time packet metadata preview
    - `RecordingSettingsComponent.cs` - format, rotation and metadata options
    - `RecentSessionsComponent.cs` - quick access to recent recordings

- `Controls/`
  - `LevelMeterControl.cs` - real-time input level meter
  - `DeviceListControl.cs` - device list with advanced info
  - `TimerIndicatorControl.cs` - recording duration and ETA

- `Services/`
  - `IRecorderServices.cs` - service interfaces (recorder, writer, logger)
  - `RecorderService.cs` - core audio capture and packetization
  - `RecordingFileWriter.cs` - file format writer and rotation logic
  - `DeviceDiagnosticsService.cs` - test tones and device checks

- `Models/`
  - `RecordingModels.cs` - domain models and records (RecordingSession, PacketMetadata)
  - `RecorderViewModel.cs` - view model for binding to UI

- `External/` (shared protocol)
  - `SRS/Common` and `SRS/SharedAudio` - shared packet models and helpers used by both recorder and player

Design notes
- The entire project is organized around the `Core` project and the `External/SRS` libraries. Core contains shared business logic, models, and interfaces; External/SRS contains protocol definitions and audio helpers. Prefer referencing and extending these projects instead of duplicating code.
- Implement each component as a `UserControl` so it can be reused and designer-supported.
- Expose simple events for communication (e.g., `StartRequested`, `StopRequested`, `DeviceChanged`, `PacketCaptured`).
- Keep capture logic inside `RecorderService`; UI should only invoke commands and subscribe to events.
- When adding services or models, check `Core` and `External/SRS` first — extend existing types or add interfaces in `Core` so all internal projects can depend on the same contracts.
- Use `ServiceConfiguration` to register concrete implementations that compose Core and External/SRS functionality; avoid introducing new DI patterns unless necessary.
- Use async patterns for I/O and keep UI thread responsive.

How to add a new view/component
1. Create a `UserControl` file under the appropriate folder in `DCS-SRS-RecordingClient.UI`.
2. Add minimal public API (events/properties) for interaction with presenter/services.
3. Register required services in `Infrastructure/ServiceConfiguration.cs` and prefer wiring through `Core` interfaces.
4. Compose into `MainRecorderForm` via `MainFormFactory` or DI-resolved presenter.

Quick run (UI project)
- `dotnet run --project DCS-SRS-RecordingClient.UI`

This concise layout mirrors the recording client's intended architecture and speeds up navigation and development. See `docs/RecordingClients.md` for higher-level context.

---

AI Authoring Prompt

Use this prompt for an automated documentation author that must generate the Recording Client UI guidance file above:

"You are an automated documentation author. Produce a concise, developer-focused UI folder and component structure document for the Recording Client UI project in this repository. Requirements:
- Target audience: developers working on the solution (concise, factual).
- Output file: `docs/RecordingClient_UI_Structure.md`.
- Keep it short (one page), use plain language and `code`-style for file and folder names.
- Include: root project name, folder layout (`Infrastructure`, `Views`, `Controls`, `Services`, `Models`, `External`), key files/or responsibilities under each folder, design notes, how-to-add instructions, and a one-line quick-run command.
- Emphasize these constraints:
  - The recording client is part of a larger solution. Prefer reusing and referencing shared `Core` logic and the `External/SRS` projects (protocol and audio helpers). Do not duplicate functionality that exists in Core or External/SRS.
  - Internal projects should connect via `Core` to external/shared libraries; register services in `ServiceConfiguration` and prefer Core interfaces.
  - Avoid proposing new code patterns or unnecessary files; recommend extending existing types/services.
  - Use async patterns and keep capture logic inside recorder services; UI should be thin and event-driven.
- Mention target framework: .NET 9.
- Refer developers to `docs/RecordingClients.md` and `docs/PlayerClient.md` for higher-level context.
- Tone: neutral, actionable, and prescriptive."

Include or reuse this prompt text when regenerating or updating this document to ensure consistency and adherence to project constraints.