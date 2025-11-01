# AeroDebrief User Guide

Welcome to AeroDebrief! This guide will help you get started with recording, analyzing, and playing back voice communications from your DCS World multiplayer sessions.

## Table of Contents

1. [What is AeroDebrief?](#what-is-aerodebrief)
2. [Prerequisites](#prerequisites)
3. [Installation](#installation)
4. [Getting Started](#getting-started)
5. [Recording Communications](#recording-communications)
6. [Playing Back Recordings](#playing-back-recordings)
7. [Understanding the Interface](#understanding-the-interface)
8. [Advanced Features](#advanced-features)
9. [Analytics and Insights](#analytics-and-insights)
10. [Tips and Best Practices](#tips-and-best-practices)
11. [Troubleshooting](#troubleshooting)
12. [FAQ](#faq)

## What is AeroDebrief?

AeroDebrief is a debriefing and analysis tool designed specifically for DCS World flight simulations. It captures and analyzes live voice communications from networked sessions using Simple Radio Standalone (SRS) servers or compatible voice systems.

**Important Note:** AeroDebrief is designed for **online multiplayer** voice communications via SRS. It does **not** capture DCS's internal, offline radio system used in single-player missions.

### Key Capabilities

- **Live Recording**: Capture all radio communications from SRS servers during your missions
- **Multi-Frequency Support**: Record multiple frequencies simultaneously
- **Advanced Playback**: Replay communications with precise timeline control
- **Waveform Visualization**: See visual representations of communications with GPU-accelerated rendering
- **Analytics Dashboard**: Analyze communication patterns, frequency usage, and player activity
- **Frequency Filtering**: Isolate and analyze specific frequencies or communication channels

## Prerequisites

Before using AeroDebrief, ensure you have:

### System Requirements

- **Operating System**: Windows 10/11 (64-bit)
- **Runtime**: .NET 9 Runtime (included in installer)
- **Graphics**: DirectX 11 capable GPU (optional, for GPU-accelerated waveform rendering)
  - Most graphics cards from 2010 or later are compatible
  - CPU fallback available if GPU is not available
- **Network**: Access to an SRS server (for recording)

### Required Software

- **DCS World**: Your flight simulator
- **SRS (Simple Radio Standalone)**: Version 2.3.20 or later
  - Must be configured and running
  - Must be connected to an SRS server during multiplayer sessions

### Recommended

- Headset or speakers for playback
- At least 4GB of free disk space for recordings (varies by session length)

## Installation

See [INSTALLATION.md](INSTALLATION.md) for detailed installation instructions.

### Quick Installation

1. Download the latest release from [GitHub Releases](https://github.com/shalevohad/AeroDebrief/releases)
2. Extract the ZIP file to your preferred location (e.g., `C:\Program Files\AeroDebrief`)
3. Run `AeroDebrief.UI.exe` to start the application
4. The .NET 9 runtime will be automatically installed if needed

## Getting Started

### First Launch

1. **Launch AeroDebrief**: Double-click `AeroDebrief.UI.exe`
2. **Configure SRS Connection**: 
   - Go to Settings (if available in UI)
   - Enter your SRS server IP address and port
   - Default SRS port is typically 5002 (UDP voice) and 5000 (TCP control)

### Basic Workflow

The typical workflow with AeroDebrief involves:

1. **Record**: Start recording before or during your DCS mission
2. **Fly**: Complete your mission with normal radio communications via SRS
3. **Stop**: End the recording after your mission
4. **Playback**: Load and replay the recording
5. **Analyze**: Use analytics tools to review communication patterns
6. **Debrief**: Use insights for after-action reviews and training

## Recording Communications

### Starting a Recording

1. **Connect to SRS**: Ensure you're connected to your SRS server
2. **Open AeroDebrief**: Launch the application
3. **Start Recording**:
   - Click the "Record" button or menu option
   - Specify a filename and location for the recording
   - Default format is `.srs` (AeroDebrief's custom recording format)

### During Recording

While recording:
- AeroDebrief captures all voice packets from the SRS server
- All active frequencies are recorded simultaneously
- Player metadata (callsigns, positions) is captured when available
- A status indicator shows recording is active

### Stopping a Recording

To stop recording:
1. Click the "Stop" button
2. The recording is saved to disk automatically
3. The file is ready for immediate playback

### Recording Tips

- **Start Early**: Begin recording before mission start to capture all communications
- **Storage**: Monitor disk space; long missions generate larger files
- **Privacy**: Always respect server rules and obtain consent before recording
- **Backup**: Consider backing up important recordings to external storage

## Playing Back Recordings

### Loading a Recording

1. **Open AeroDebrief**: Launch the application
2. **Load File**:
   - Click "File" → "Open" or the "Load File" button
   - Navigate to your `.srs` recording file
   - Click "Open"
3. **Wait for Analysis**: AeroDebrief analyzes the file (usually takes a few seconds)

### Playback Controls

AeroDebrief provides professional-grade playback controls:

- **Play/Pause**: Start or pause playback
- **Stop**: Stop playback and return to start
- **Seek**: Click on the timeline or waveform to jump to a specific time
- **Speed Control**: Adjust playback speed (if available)
- **Volume**: Master volume control

### Timeline Navigation

- **Waveform Timeline**: Visual representation of all communications
- **Timeline Markers**: Important events marked on the timeline
- **Minimap**: Overview of entire recording with zoom and pan controls
- **Time Display**: Current position and total duration

## Understanding the Interface

### Main Window Layout

The AeroDebrief interface consists of several key areas:

#### 1. Waveform Viewer (Top)
- Displays visual waveforms of audio communications
- Color-coded by frequency or player
- Interactive: click to seek, zoom with mouse wheel
- **GPU-Accelerated**: Smooth, real-time rendering

#### 2. Frequency Tree (Left Sidebar)
- Hierarchical list of all detected frequencies
- Organized by coalition (Blue, Red, Neutral)
- Shows activity indicators
- Click to select/deselect frequencies

#### 3. Transport Controls (Bottom)
- Play, Pause, Stop buttons
- Timeline scrubber
- Time display
- Volume control

#### 4. Frequency Mixer (Right Sidebar)
- Individual gain controls per frequency
- Pan controls (left/right speaker balance)
- Mute/Solo buttons for each frequency

#### 5. Analytics Tabs (Bottom Panel)
- Presence Network: Visual graph of player connections
- Power Levels: Audio intensity over time
- Signal Quality: Communication reliability metrics
- Statistics: Detailed frequency usage reports

### Color Coding

AeroDebrief uses color coding to help you quickly identify:

- **Blue**: Friendly coalition frequencies
- **Red**: Enemy coalition frequencies
- **Green**: Neutral/shared frequencies
- **Yellow**: Active transmission
- **Gray**: Inactive/silent periods

## Advanced Features

### GPU-Accelerated Waveform Rendering

AeroDebrief includes state-of-the-art GPU acceleration for waveform visualization:

- **10-50x faster** rendering compared to CPU-only processing
- **Real-time updates** with thousands of audio packets
- **Automatic detection**: GPU acceleration is automatically enabled if available
- **Graceful fallback**: If GPU is not available, CPU rendering is used automatically

**Note**: Requires DirectX 11 compatible graphics card (most GPUs since 2010).

### Multi-Frequency Analysis

You can analyze multiple frequencies simultaneously:

1. Select multiple frequencies in the Frequency Tree
2. Each frequency displays as a separate track in the waveform
3. Use the Frequency Mixer to adjust individual volumes
4. Compare communication patterns across frequencies

### Frequency Filtering

Focus on specific frequencies:

1. **Select Frequencies**: Click frequencies in the tree to enable/disable
2. **Solo Mode**: Isolate a single frequency (if available)
3. **Mute**: Temporarily silence frequencies without deselecting
4. **Gain Control**: Boost quiet frequencies or reduce loud ones

### Audio Export

Export mixed audio for external use:

1. Select desired frequencies
2. Choose export format (typically WAV)
3. Specify time range or export entire recording
4. Export processes and saves the audio file

## Analytics and Insights

### Presence Network Graph

The Presence Network shows who communicated with whom:

- **Nodes**: Represent players or units
- **Connections**: Show communication links
- **Animation**: Real-time visualization during playback
- **Insights**: Identify communication patterns and frequency usage

### Power Levels Analysis

View audio intensity over time:

- **Timeline Graph**: Shows transmission power for each frequency
- **Hot Spots**: Identify periods of high activity
- **Quiet Periods**: Find gaps in communications
- **Comparative Analysis**: Compare activity across frequencies

### Signal Quality Metrics

Analyze communication reliability:

- **Packet Loss**: Identify dropped audio packets
- **Quality Indicators**: Overall transmission quality
- **Problem Areas**: Highlight issues during playback

### Statistics Dashboard

Comprehensive frequency usage reports:

- **Total Transmission Time**: Per frequency and per player
- **Packet Counts**: Number of transmissions
- **Active Players**: Who transmitted on which frequencies
- **Time Distribution**: When communications occurred

## Tips and Best Practices

### Recording Best Practices

1. **Test First**: Do a short test recording before important missions
2. **Name Descriptively**: Use clear filenames (e.g., `Mission_2024-01-15_Caucasus.srs`)
3. **Monitor Storage**: Keep an eye on disk space during long recordings
4. **Regular Backups**: Back up important recordings to prevent data loss

### Playback Best Practices

1. **Use Headphones**: Better audio quality and detail recognition
2. **Adjust Volume Levels**: Use the mixer to balance frequencies
3. **Speed Control**: Slow down for detailed analysis, speed up for overview
4. **Take Notes**: Mark important moments for future reference

### Analysis Best Practices

1. **Start with Overview**: Use statistics to understand overall patterns
2. **Zoom In**: Focus on specific periods of interest
3. **Compare Frequencies**: Look for coordination across channels
4. **Export Findings**: Save analytics screenshots or reports

### Performance Optimization

1. **GPU Acceleration**: Ensure GPU is enabled for best performance
2. **Close Background Apps**: Free up resources for smoother playback
3. **SSD Storage**: Store recordings on SSD for faster loading
4. **Regular Cleanup**: Archive or delete old recordings

## Troubleshooting

### Common Issues

#### "Cannot connect to SRS server"
- **Check**: SRS is running and connected
- **Verify**: Server IP and port are correct
- **Firewall**: Ensure firewall allows AeroDebrief
- **Network**: Confirm network connectivity to SRS server

#### "No audio during playback"
- **Check**: Volume levels in mixer are not at zero
- **Verify**: Frequencies are selected in the tree
- **Audio Device**: Ensure correct audio output device is selected
- **File Integrity**: Try loading a different recording

#### "Waveform not displaying"
- **Check**: File has audio data (not an empty recording)
- **GPU Issues**: Try restarting the application
- **Update Drivers**: Ensure graphics drivers are up to date
- **CPU Fallback**: Application should fall back to CPU rendering

#### "Recording file is very large"
- **Normal**: Long missions generate large files due to multi-frequency capture
- **Compression**: Files use OPUS compression but still grow with duration
- **Storage**: Ensure adequate disk space before recording

#### "Application crashes on launch"
- **Runtime**: Ensure .NET 9 runtime is installed
- **Compatibility**: Verify Windows 10/11 64-bit
- **Reinstall**: Try reinstalling AeroDebrief
- **Logs**: Check application logs in the installation directory

### Performance Issues

If you experience lag or stuttering:

1. **Close Other Applications**: Free up system resources
2. **Update Graphics Drivers**: Ensure latest GPU drivers are installed
3. **Check System Resources**: Monitor CPU/RAM usage
4. **Reduce Visual Quality**: Disable GPU acceleration if causing issues
5. **Smaller Time Windows**: Load or analyze smaller sections of large recordings

### Getting Help

If you continue to experience issues:

1. **Check Documentation**: Review this guide and other docs
2. **GitHub Issues**: Search existing issues or create a new one
3. **Community**: Join GitHub Discussions for community support
4. **Logs**: Include error logs when reporting issues

## FAQ

### General Questions

**Q: Can AeroDebrief record DCS's internal radio in single-player?**  
A: No. AeroDebrief only captures networked voice communications via SRS servers. It does not hook into DCS's internal radio system.

**Q: Does AeroDebrief work with all SRS versions?**  
A: AeroDebrief requires SRS version 2.3.20 or later. Older versions may not be compatible.

**Q: Can I use AeroDebrief for other flight simulators?**  
A: AeroDebrief is designed for DCS World but may work with other sims that use SRS-compatible voice systems.

**Q: Is AeroDebrief free?**  
A: Yes, AeroDebrief is open source and free to use.

### Recording Questions

**Q: How much disk space do recordings use?**  
A: This varies by mission length and number of active frequencies. A typical 1-hour mission might use 50-200MB.

**Q: Can I record without participating in the mission?**  
A: Yes, as long as you're connected to the SRS server, you can record all communications.

**Q: Are recordings compressed?**  
A: Yes, audio uses OPUS compression to reduce file size while maintaining quality.

### Playback Questions

**Q: Can I export audio to share with others?**  
A: Yes, you can export selected frequencies to WAV format.

**Q: Can I play back recordings at different speeds?**  
A: Check the playback controls for speed adjustment options (feature availability may vary).

**Q: Will recordings play on computers without AeroDebrief?**  
A: No, the `.srs` format requires AeroDebrief. Export to WAV for universal compatibility.

### Privacy and Legal Questions

**Q: Is it legal to record SRS communications?**  
A: Always check your server's rules and obtain consent. Respect privacy laws in your jurisdiction.

**Q: Should I inform other players I'm recording?**  
A: Yes, it's best practice to inform participants and obtain consent before recording.

**Q: Can I share recordings publicly?**  
A: Only with explicit permission from all recorded participants and in compliance with server rules.

## Next Steps

Now that you understand the basics:

1. **Practice**: Record and play back some missions to get comfortable
2. **Explore**: Try different analytics views and frequency combinations
3. **Optimize**: Adjust settings and workflows for your needs
4. **Share**: Use insights for debriefings and training sessions

For more advanced topics, see:
- [Developer Guide](DEVELOPER_GUIDE.md) - If you want to contribute or extend AeroDebrief
- [Installation Guide](INSTALLATION.md) - Detailed setup instructions
- [Technical Documentation](../DOC/README.md) - Implementation details

---

**Happy Debriefing!**  
Built with ❤️ for the DCS World community
