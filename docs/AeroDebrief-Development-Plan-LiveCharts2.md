# AeroDebrief LiveCharts2 Development Plan (Detailed)

Scope
- Replace legacy waveform rendering with a LiveCharts2-based unified amplitude (dBFS) visualization.
- Meet performance goals (<=10s load for 60+ freqs), synchronization goals (<=10 frames), and memory goal (peak <1 GB).
- Maintain simple end-user settings; advanced tuning via constants/config/env (no developer UI).
- Keep the existing Frequency Tree as the authoritative selection/visibility UI for pilots/frequencies; do not add a separate chart-side selection UX.
- Drop legacy waveform code and feature flag once DoD is met.

Milestones (ordered, with outputs)
- M0: Project setup and spike (1–2 days)
  - Add packages: LiveChartsCore, LiveChartsCore.SkiaSharpView, LiveChartsCore.SkiaSharpView.WPF to `AeroDebrief.UI`.
  - Create `UnifiedGraphControl` (shell) with 2 `CartesianChart` instances (main + minimap) and bind to a simple `UnifiedGraphViewModel`.
  - Generate synthetic data for 60 freqs with pilot distribution (54x4, 6x24) and measure initial render + memory.
  - Output: basic window with charts visible; baseline metrics logged.

- M1: Abstractions and feature flag (1 day)
  - Introduce interfaces and base implementations:
    - `src/AeroDebrief.UI/Charts/IUnifiedChartRenderer.cs`
    - `src/AeroDebrief.UI/Charts/LiveChartsUnifiedChartRenderer.cs`
    - `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`
  - Add temporary setting `UseLiveChartsRenderer` (config only). Wire `UnifiedPlayerControl` path to pick renderer.
  - Output: app runs with old or new renderer via flag.

- M2: Amplitude pipeline (2–3 days)
  - Add envelope calculation and provider:
    - `src/AeroDebrief.UI/Services/Graphs/IAmplitudeSeriesProvider.cs`
    - `src/AeroDebrief.UI/Services/Graphs/AmplitudeSeriesProvider.cs`
    - `src/AeroDebrief.UI/Services/Graphs/AmplitudeEnvelopeCalculator.cs` (short-term envelope, dBFS, floor)
  - Integrate with `FrequencyManager` metadata and packet timeline indexing.
  - Output: main chart renders real amplitude data for sample recordings.

- M3: Multi-resolution tiling + cache (1–2 days)
  - Add memory-bounded tile cache:
    - `src/AeroDebrief.UI/Services/Graphs/IDataTileCache.cs`
    - `src/AeroDebrief.UI/Services/Graphs/DataTileCache.cs` (LRU, budgets, levels L0..L3)
  - Implement viewport-driven tile loading, per-zoom decimation (LTTB/MaxReduce).
  - Output: panning/zooming stays responsive, working set bounded.

- M4: Unified chart MVP (2–3 days)
  - Color palette per frequency: `src/AeroDebrief.UI/Charts/ChartColors.cs` (deterministic).
  - Pilot markers: `src/AeroDebrief.UI/Charts/PilotMarkers.cs` (>=32 shapes; cached geometries).
  - Density-aware pilot expansion for high-pilot frequencies; lazy series creation.
  - Integrate chart visibility with Frequency Tree selection and mute/show events; no chart-side selection model.
  - Output: full graph shows freqs/pilots with correct colors/markers; responsive at scale; visibility controlled by Frequency Tree.

- M5: Minimap and zoom UX (1–2 days)
  - Bind minimap to aggregated tiles (L3/L2). Implement selection range controlling main chart `MinLimit/MaxLimit`.
  - Implement mouse wheel + drag zoom; keyboard shortcuts if present.
  - Output: zoom/minimap parity with legacy behavior.

- M6: Playhead and seek synchronization (2 days)
  - Implement playback sync:
    - `src/AeroDebrief.UI/Services/Graphs/IPlayheadSyncService.cs`
    - `src/AeroDebrief.UI/Services/Graphs/PlayheadSyncService.cs`
  - Bind to `PlaybackSessionManager` events for play, pause, rate changes, and seek.
  - Render playhead (vertical line) as LiveCharts `VisualElement` or `Section`.
  - Output: playhead follows audio; big seeks and rate changes stay within tolerance.

- M7: Visibility toggles and audio routing (1 day)
  - Drive frequency/pilot show/hide exclusively from Frequency Tree selection/mute state; subscribe in `UnifiedGraphViewModel`.
  - Update chart series `IsVisible` only; avoid data rebuilds.
  - Coordinate with `MixerController` for mute/solo without affecting chart data.
  - Output: instant toggles for pilots/frequencies driven by Frequency Tree.

- M8: Collision overlay (1 day)
  - Implement collision detection and overlay:
    - `src/AeroDebrief.UI/Services/Graphs/ICollisionDetector.cs`
    - `src/AeroDebrief.UI/Services/Graphs/CollisionDetector.cs`
  - Render shaded sections across Y for intervals with overlap count >=2; per-frequency toggle.
  - Output: optional collision view.

- M9: Progress and status (1 day)
  - Add loading phases with progress text/percentage:
    - `src/AeroDebrief.UI/Services/Graphs/ILoadingProgress.cs`
    - `src/AeroDebrief.UI/Services/Graphs/LoadingProgress.cs`
  - Show progress UI overlay in `UnifiedGraphControl`.
  - Output: clear user feedback during load.

- M10: Tests and perf gates (2–3 days)
  - End-to-end tests (AeroDebrief.Tests):
    - Load performance: 60+ freqs, given pilot distribution, charts ready in <=10s.
    - Memory ceiling: peak WorkingSet64 < 1 GB during load/play/seek stress.
    - Sync: playhead vs audio time drift <= 10 frames across seeks/rate changes; 2h recordings.
    - Visibility: toggles via Frequency Tree update series instantly without recreating series or reloading data.
    - High-pilot expansion: lazy series creation works, stays within memory budget.
  - Unit tests:
    - Envelope calculator correctness (window/hop, dB conversion, floor) and throughput.
    - Decimation algorithm correctness (LTTB/MaxReduce) and stability.
    - Collision interval correctness with synthetic overlaps.
    - Color/marker mapping determinism; >=32 unique markers.
    - Tile cache LRU eviction and budget enforcement.
    - Frequency Tree selection -> renderer visibility binding (observer updates do not cause data rebuilds).
  - Output: automated verification in CI.

- M11: Cleanup and removal of legacy (0.5–1 day)
  - Remove legacy waveform controls and code paths.
  - Delete `UseLiveChartsRenderer` and fallbacks.
  - Update docs and release notes.
  - Output: single renderer (LiveCharts2) remains.

Work Breakdown Structure (WBS)
- Charts and UI
  - Create `UnifiedGraphControl` (layout grid: toolbar, main chart, minimap, progress overlay).
  - Bind axes (DateTime X, dB Y). Disable heavy animations; tuned tooltip behavior.
  - Render playhead and collision `Sections`.
  - Implement `LegendVirtualizedControl` for large pilot lists (virtualizing stackpanel).
  - No separate chart-side selection UI; visibility driven by Frequency Tree.

- Rendering adapters
  - `IUnifiedChartRenderer` API
    - Methods: Initialize, SetViewport(range), SetSeries(series), UpdatePlayhead(time), ToggleFrequency(id, visible), TogglePilot(freqId, pilotId, visible), SetCollisionVisibility(freqId, visible), Dispose.
    - Events: ViewportChanged, HitTestRequested (optional).
  - `LiveChartsUnifiedChartRenderer` implements mapping to LiveCharts series/axes/visuals.

- Colors and markers
  - `ChartColors`: stable palette from seed; fallback with HCL or HSV hue rotation; ensure contrast.
  - `PilotMarkers`: pre-generate geometries; cache by pilot key; provide mapping function.

- Data pipeline
  - `AmplitudeEnvelopeCalculator`:
    - Inputs: PCM float stream or packetized audio; parameters: windowMs, hopMs, ref level.
    - Output: `AmplitudeSample` sequence (time, dBFS with floor).
  - `AmplitudeSeriesProvider`:
    - Build timelines per `AmplitudeSeriesKey`.
    - Provide enumerators for tiles; async prefetch for viewport +/- margin.

- Tiling and decimation
  - Tile levels: L0=10ms, L1=50ms, L2=250ms, L3=1s (configurable constants).
  - `DataTileCache`:
    - Keys: (freqId, pilotId, level, time span index).
    - Storage: `ReadOnlyMemory<AmplitudeSample>`; allocate via pooled buffers; expose `Dispose`/return-to-pool.
    - LRU policy: track size by estimated bytes; budgets from constants/config.
    - Optional file-backed storage for evicted tiles (MMF or temp files).
  - Decimation strategies:
    - LTTB or max-reduction per pixel bucket; plug via strategy interface.

- Playback synchronization
  - `PlayheadSyncService`:
    - Listen to `PlaybackSessionManager` ticks and state changes.
    - Broadcast `CurrentPlaybackTime` (DateTimeOffset) to chart.
    - Apply rate multiplier; handle seeks by snapping axes & notifying renderer.

- Visibility & routing
  - Subscribe to Frequency Tree selection and mute/show events (via `FrequencyTreeView`, `FrequencyManager`, or `PlayerEvents`).
  - Maintain a lookup of series by `AmplitudeSeriesKey`.
  - On toggle, update `IsVisible` and mute/solo in `MixerController` accordingly.
  - No data re-processing on toggle.

- Progress and logging
  - Expose phases via `ILoadingProgress` (enum Phase, int Percent, string Detail).
  - Log timings: open/index, envelope build, tiles created, first paint, seek latency.
  - Log memory samples: WorkingSet64, GC.GetTotalMemory(false).

- Memory watchdog
  - Periodic sampler service; when >90% of budget:
    - Increase decimation step, shrink tile retention, drop markers.
  - When <70%:
    - Restore previous detail gradually.

Configuration and constants
- User-facing (UI):
  - ShowCollisionOverlay (bool), MarkerDensity (Auto/Off), PerformanceMode (Auto/Quality), InvertZoomWheel (bool).
- Developer/config-only:
  - UseLiveChartsRenderer (temporary), EnvelopeWindowMs, EnvelopeHopMs,
  - DecimationStrategy, MaxPointsPerSeriesVisible, MaxMarkersVisible,
  - MaxMemoryMB, ChartDataBudgetMB, TileCacheBudgetMB, AudioBuffersBudgetMB,
  - TileSpanSeconds, TileLevels[], MaxPilotsExpandedPerFreq,
  - DeveloperMode, ShowPerfOverlay, EnablePerfLogs.

Acceptance criteria (aligned with DoD)
- Charts load <= 10s for 60+ freqs with specified pilot distribution.
- Working set < 1 GB during load/play/seek stress; watchdog reduces detail under pressure.
- Playhead sync drift <= 10 frames across seeks/rate changes over 2h recording.
- Distinct color per frequency; >=32 unique markers per frequency for pilots.
- Toggle pilot/frequency visibility instantly via the Frequency Tree without data rebuild.
- Collision overlay computed and toggleable.
- Legacy waveform removed; feature flag deleted.
- The chart has no separate selection model; Frequency Tree remains the sole selection/visibility source.

CI and tooling
- Add test categories: E2E_Perf, E2E_Sync, Unit_Graphs, Unit_Tiles.
- Add CI pipeline step to run E2E with synthetic dataset; assert time/memory budgets.
- Optional: export perf logs as build artifacts for trend tracking.

Risks and mitigations
- Too many visible series: lazy creation; cap visible pilots initially; decimate aggressively.
- Memory spikes: strict budgets; LRU eviction; file-backed tiles; pooled buffers.
- GPU issues: Skia falls back to CPU; guard with try/catch and log renderer info.
- Sync drift: treat audio engine as source of truth; periodic resync; snap playhead on seeks.

Change management
- Implement behind temporary flag; gather feedback.
- After M10 validated, execute M11 to remove legacy and the flag.

Next steps
- Execute M0 tasks; create scaffolding branches; open PR with packages and `UnifiedGraphControl` shell plus synthetic loader and baseline metrics.
- In parallel with M4/M7, wire `UnifiedGraphViewModel` to Frequency Tree selection/mute events and drive series visibility accordingly.
