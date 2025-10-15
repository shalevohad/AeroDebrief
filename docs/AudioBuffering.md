# Audio Analysis & Buffering Implementation

## Overview

The DCS-SRS Recording Player now includes advanced audio buffering to provide smooth, stutter-free playback, plus comprehensive audio activity analysis to understand when voice communications occur in your recordings.

## Key Features

### Audio Buffering
- **Smooth Playback**: Pre-processes audio packets 3 seconds ahead by default
- **Configurable Buffer Size**: Adjust buffer time and maximum chunk count as needed
- **Seek-Aware Buffering**: Automatically rebuilds buffer when seeking to new positions
- **Resource Managed**: Automatically disposes of processed audio data to prevent memory leaks
- **Robust Error Handling**: Continues playback even if individual packets fail to process

### Audio Activity Analysis
- **Voice Activity Detection**: Identifies periods when actual voice communication occurs (not silence)
- **Detailed Timeline**: Reports exact start/end times of voice activity
- **Player Tracking**: Shows which players were active and when
- **Frequency Analysis**: Breaks down activity by radio frequency
- **Configurable Thresholds**: Adjust silence detection sensitivity
- **Export Capabilities**: Export activity data to CSV for further analysis

## Audio Activity Analysis

### What It Does

The audio activity analysis feature examines your recorded files to identify when actual voice communications occur, filtering out periods of silence or background noise. This is extremely useful for:

- **Mission Debriefing**: Quickly find when important communications happened
- **Highlight Extraction**: Identify the most active periods for creating highlights
- **Communication Analysis**: Understand communication patterns and frequency usage
- **Quality Assessment**: Check if recordings captured voice data properly

### Usage Examples

#### Basic Analysis
```bash
# Analyze a recording file for voice activity
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw

# Adjust sensitivity (lower = more sensitive to quiet audio)
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw --threshold 200

# Filter out very short transmissions
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw --min-duration 500
```

#### Player and Frequency Filtering
```bash
# Show activity for specific player
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw --player "Viper1"

# Show activity on specific frequency
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw --frequency 251.0

# Export filtered results to CSV
DCS-SRS-RecordingClient.exe --analyze-activity recording.raw --player "Viper" --export-csv viper_comms.csv
```

### Understanding the Results

The analysis provides several types of information:

#### Overall Statistics
- **Total Duration**: Complete recording length
- **Activity Duration**: Time with actual voice communications
- **Activity Percentage**: Proportion of recording with voice activity
- **Unique Players**: Number of different people who transmitted
- **Unique Frequencies**: Number of different radio frequencies used

#### Activity Periods
Each voice transmission is reported with:
- **Start/End Times**: Exact timestamps (HH:mm:ss.fff format)
- **Duration**: Length of the transmission
- **Player**: Who was transmitting
- **Frequency**: Which radio frequency was used
- **Audio Quality**: Maximum and average amplitude levels

#### Player Summary
For each player who transmitted:
- **Total Transmission Time**: How long they talked overall
- **Number of Transmissions**: How many separate voice transmissions
- **Active Periods**: When they were most active

#### Frequency Summary
For each radio frequency used:
- **Total Activity**: How much voice traffic occurred
- **Number of Transmissions**: How many separate transmissions
- **Primary Users**: Who used this frequency most

### Configuration Parameters

#### Silence Threshold (--threshold)
- **Range**: 0-32767 (16-bit audio amplitude)
- **Default**: 500
- **Lower Values**: More sensitive, picks up quieter audio and background noise
- **Higher Values**: Less sensitive, only picks up clear, loud transmissions
- **Recommended**: Start with 500, adjust based on your audio quality

#### Minimum Duration (--min-duration)
- **Range**: 10ms and up
- **Default**: 100ms
- **Purpose**: Filters out very brief audio spikes or button clicks
- **Recommended**: 100-500ms depending on how clean you want the results

### CSV Export Format

When you export to CSV, you get a file with these columns:
- **StartTime**: When the transmission started (yyyy-MM-dd HH:mm:ss.fff)
- **EndTime**: When the transmission ended
- **DurationSeconds**: Length in seconds (decimal)
- **PrimaryPlayer**: Main player transmitting
- **PrimaryFrequency**: Radio frequency used (MHz)
- **MaxAmplitude**: Peak audio level (0-32767)
- **AverageAmplitude**: Average audio level
- **PacketCount**: Number of audio packets
- **AllPlayers**: All players involved (semicolon-separated)
- **AllFrequencies**: All frequencies used (semicolon-separated)

This CSV can be imported into Excel, analyzed with scripts, or used for creating communication timelines.

## Audio Buffering (Technical)

### How It Works

1. **Background Processing**: A separate background task continuously processes upcoming audio packets
2. **Buffer Management**: Maintains a queue of pre-processed audio chunks ready for playback
3. **Timing Coordination**: Delivers audio chunks to the output engine at precisely the right time
4. **Seek Handling**: When seeking, clears the buffer and starts processing from the new position

### Usage

#### Default Buffering (3 seconds)
```csharp
var reader = new AudioPacketReader("path/to/recording.raw");
reader.StartPlayback();
```

#### Custom Buffering
```csharp
// 5 second buffer with up to 2000 chunks
var reader = new AudioPacketReader(
    "path/to/recording.raw", 
    TimeSpan.FromSeconds(5),    // Buffer 5 seconds ahead
    2000                        // Max 2000 audio chunks in buffer
);
reader.StartPlayback();
```

#### Monitor Buffer Status
```csharp
var (chunkCount, duration) = reader.GetBufferStatus();
Console.WriteLine($"Buffer: {chunkCount} chunks, {duration.TotalSeconds:F1} seconds");
```

### Configuration Guidelines

#### Buffer Ahead Time
- **1-2 seconds**: Minimal buffering, lower memory usage, may stutter on slow systems
- **3 seconds (default)**: Good balance of smoothness and memory usage
- **5+ seconds**: Maximum smoothness, higher memory usage, good for complex recordings

#### Maximum Buffered Chunks
- **500-1000**: Lower memory usage, suitable for basic playback
- **1000 (default)**: Good balance for most use cases
- **2000+**: Higher memory usage, best for continuous long recordings

### Memory Usage

The buffering system uses approximately:
- **PCM Audio Data**: ~96 KB per second of buffered audio (48kHz, 16-bit, mono)
- **Metadata**: ~1 KB per audio chunk
- **Total**: For 3 seconds default buffering ? 300 KB of memory

## Performance Benefits

### Before (Without Buffering)
- Audio processing happened in real-time during playback
- CPU spikes could cause audio stuttering
- Seeking often resulted in brief audio gaps
- Complex recordings with many frequencies could stutter

### After (With Buffering)
- Audio processing happens ahead of time in background
- Playback is smooth even during CPU spikes
- Seeking provides immediate, clean audio transitions
- Consistent playback regardless of recording complexity

## Troubleshooting

### Audio Stuttering
- Increase buffer ahead time: `new AudioPacketReader(path, TimeSpan.FromSeconds(5))`
- Increase max buffered chunks: `new AudioPacketReader(path, TimeSpan.FromSeconds(3), 2000)`

### High Memory Usage
- Decrease buffer ahead time: `new AudioPacketReader(path, TimeSpan.FromSeconds(1))`
- Decrease max buffered chunks: `new AudioPacketReader(path, TimeSpan.FromSeconds(3), 500)`

### Seeking Delays
- Ensure buffer ahead time is not too large (reduces seek responsiveness)
- Consider using 2-3 second buffer for better seek performance

### Audio Activity Analysis Issues

#### Too Many Short Periods
- Increase `--min-duration` parameter: `--min-duration 500`
- Increase `--threshold` to filter background noise

#### Missing Quiet Transmissions
- Decrease `--threshold` parameter: `--threshold 200`
- Check your recording levels and audio quality

#### Analysis Taking Too Long
- The analysis processes the entire file and may take time for large recordings
- Progress is logged every 10,000 packets
- Consider analyzing smaller portions of very large files

## Compatibility

This implementation is:
- ? Compatible with all existing frequency filtering
- ? Compatible with volume control and audio effects
- ? Compatible with pause/resume functionality
- ? Compatible with both WASAPI and SimpleAudioOutputEngine
- ? Thread-safe and disposal-safe
- ? Works with both legacy and new recording file formats