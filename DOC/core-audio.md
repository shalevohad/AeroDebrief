# Core Audio Subsystems

This document describes the current implementation of the audio capture, recording, buffering, decoding, and playback systems.

## Major classes

- `AudioPacketRecorder` (src/AeroDebrief.Core/AudioPacketRecorder.cs)
  - Connects to SRS server using `TCPClientHandler` (control) and `UDPVoiceHandler` (voice).
  - Receives raw UDP packets, extracts metadata via `ExtractAudioMetadata` and creates `AudioPacketMetadata` objects.
  - Enqueues metadata into `_writeQueue` and a background `WriterLoop` serializes metadata to the configured `.srs` file.
  - Start/stop recording logic ensures writer tasks and file handles are disposed.
  - Implements careful handling of `ConnectedClientsSingleton` to enrich packet metadata with player details.

- `AudioPacketReader` (src/AeroDebrief.Core/AudioPacketReader.cs)
  - Responsible for loading `.srs` files and playing them back.
  - Uses `FileAnalyzer` to enumerate frequencies and packets.
  - `AudioProcessingEngine` decodes OPUS frames into float arrays and applies volume/filters.
  - `AudioBufferManager` buffers processed audio in small chunks for low-latency playback.
  - `AudioOutputEngine` writes buffered PCM to the OS audio subsystem (WASAPI) using NAudio.
  - `PlaybackController` tracks play/pause/seek, progress and raises events for the UI.
  - Supports exporting the mixed audio for selected frequency sets into WAV files.

- `AudioProcessingEngine` (src/AeroDebrief.Core/Audio/AudioProcessingEngine.cs)
  - Provides decoding via OPUS, signal processing, and effects pipeline.
  - Exposes `DecodePacketToFloat(AudioPacketMetadata)` and `ProcessPacket(AudioPacketMetadata)` used by the reader when mixing and exporting audio.

- `AudioBufferManager` (src/AeroDebrief.Core/Audio/AudioBufferManager.cs)
  - Manages a producer/consumer buffer of audio chunks to smooth playback.
  - Supports pre-buffering based on a `bufferAheadTime` and seeking support to jump to a packet index.

- `AudioOutputEngine` and `SimpleAudioOutputEngine` (src/AeroDebrief.Core/Audio)
  - Wraps NAudio/WASAPI: initialize device, write PCM bytes, flush/stop.
  - `SimpleAudioOutputEngine` provides a lightweight fallback.

## File format (.srs)

`AudioPacketMetadata` serializes packet metadata followed by the (possibly empty) audio payload. The file format is a sequence of metadata blocks with binary-encoded fields used by `AudioPacketReader` and `FileAnalyzer`.

`AudioPacketMetadata` is used throughout core classes and includes timestamps, frequency, modulation, transmitter GUID, and optional `PlayerInfo`.

## Buffering and timing

The playback system establishes a `RecordingStart` timestamp and maps packet timestamps to a playback timeline. `AudioBufferManager` pre-decodes packets near the current playback position and exposes `GetNextAudioChunk(currentPlaybackPosition)`. The reader requests audio chunks and pushes them to `AudioOutputEngine`.

Seeking is handled by `SeekController` which updates the `currentPacketIndex` and asks `_bufferManager` to reposition its buffer. The audio output buffer is cleared to avoid artifacts after seek.

## Concurrency and stability

- `AudioPacketRecorder` uses a `ConcurrentQueue` for write operations and a dedicated writer task for disk I/O.
- `AudioPacketReader` uses async/await heavily and cancels via a `CancellationToken` when playback stops.
- Error handling uses NLog and attempts to recover from read/processing errors by skipping corrupted bytes or outputting silence when audio output fails.

## Exporting

`AudioPacketReader` supports two main export scenarios:

1. `ExportSelectedFrequenciesToWavAsync` — combines and exports only packets from user-selected frequency-modulation combinations, producing `_before.wav` and `_after.wav` files.
2. `ExportFullRecordingToWavAsync` — blends all packets and exports the entire decoded and processed recording.

These exports decode packets, align them on a timeline, mix samples into a buffer, normalize/clamp samples and write 16-bit PCM WAV files using `NAudio.Wave.WaveFileWriter`.
