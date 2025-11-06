# AeroDebrief Sync - Tacview Addon

## Overview

This Tacview addon enables real-time synchronization between Tacview mission replays and AeroDebrief voice recordings.

## Features

- **Time Synchronization**: 10 Hz updates ensure voice and video stay in sync
- **Pilot Selection**: Filter audio to only selected pilots
- **Frequency Filtering**: Enable/disable specific radio frequencies
- **Spatial Audio**: Pan audio left/right based on pilot position
- **Bidirectional Configuration**: Configure from either Tacview or AeroDebrief

## Dependencies

### LuaSocket

This addon requires **LuaSocket** for TCP/IP networking.

**Good news**: Tacview 1.9.0+ includes LuaSocket by default - no installation needed! ?

For more information, see [`LUASOCKET.md`](LUASOCKET.md).

## Installation

### Method 1: Automatic (Recommended)

When you run AeroDebrief, it will automatically copy this addon to your Tacview AddOns folder:
```
%APPDATA%\Tacview\AddOns\AeroDebriefSync\
```

### Method 2: Manual

1. Copy the `AeroDebriefSync` folder to:
   ```
   C:\Users\<YourName>\AppData\Local\Tacview\AddOns\
   ```
2. Restart Tacview
3. Check the log for: "AeroDebrief Sync: Initialized successfully"

For detailed installation instructions, see [`INSTALL.md`](INSTALL.md).

## Configuration

### TCP Port

Default port: **52001**

To change:
1. Edit `config.txt` in the addon folder
2. Set `Port=<your port>`
3. Restart Tacview
4. Update AeroDebrief settings to match

### Update Rate

Default: **10 Hz** (recommended)

Higher rates increase CPU usage with minimal benefit.

## Usage

### Menu

Access features via: **Tacview ? AeroDebrief Sync**

- **Configure Audio Pan**: Switch between Auto/Manual pan modes
- **Configure Frequencies**: Enable/disable radio frequencies
- **Settings**: View connection status and configuration
- **About**: Version and feature information

### Workflow

1. Open a mission replay in Tacview
2. Open the corresponding recording in AeroDebrief
3. Select aircraft in Tacview (Shift+Click for multiple)
4. AeroDebrief automatically filters audio to selected pilots
5. Play/pause in Tacview - AeroDebrief follows

## Troubleshooting

### "Failed to start TCP server"

- **Port already in use**: Another application is using port 52001
  - Solution: Change port in `config.txt` and AeroDebrief settings
  
- **Permission denied**: Windows firewall blocking
  - Solution: Allow Tacview through Windows Firewall

### "No clients connected"

- Check AeroDebrief is running
- Verify both use the same port number
- Check Windows Firewall isn't blocking localhost connections

### "Sync drift detected"

- Normal occasional drift corrections are expected
- Persistent drift may indicate system performance issues
- Try closing other applications

## Technical Details

### Protocol

- **Transport**: TCP/IP over localhost (127.0.0.1)
- **Format**: Newline-delimited JSON
- **Update Rate**: 10 Hz (100ms between updates)

### Message Types

From Tacview ? AeroDebrief:
- `time_update`: Mission time, playback state, speed
- `pilot_selection`: Selected pilots with frequencies and pan
- `playback_command`: Play/pause/stop commands
- `seek`: Seek to specific time

From AeroDebrief ? Tacview:
- `ready`: Initial handshake
- `frequency_filter_update`: Update frequency filters
- `pan_configuration`: Update pan settings

## Security

This addon only accepts connections from localhost (127.0.0.1) for security.
No external connections are possible.

## Performance

- **CPU Overhead**: < 1% on modern systems
- **Memory**: < 5 MB
- **Network**: Minimal (localhost only)

## Version History

### 1.0.0 (Current)
- Initial release
- Time synchronization
- Pilot selection filtering
- Frequency filtering
- Spatial audio (pan)
- Bidirectional configuration

## Support

For issues, questions, or feature requests:
- GitHub: https://github.com/shalevohad/AeroDebrief
- Documentation: See `docs/tacview-integration/` folder

## License

Part of the AeroDebrief project.
See main project LICENSE file for details.
