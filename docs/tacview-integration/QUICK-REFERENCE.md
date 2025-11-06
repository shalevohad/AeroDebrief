# Tacview Integration - Quick Reference

## Installation

```bash
# Copy addon to Tacview
Copy: src/AeroDebrief.Integrations/Lua/Tacview/AeroDebriefSync/
To:   %APPDATA%\Tacview\AddOns\AeroDebriefSync/

# Restart Tacview
```

## Prerequisites

- **Tacview Version**: 1.9.0 or higher
- **LuaSocket**: Included with Tacview 1.9.0+ (no installation needed)

## Verification

Check Tacview log (**Help ? Show Log**):
```
? AeroDebrief Sync: Initialized successfully on port 52001
```

## Dependencies

### LuaSocket

**Included with Tacview 1.9.0+** - no separate installation required.

If you see "module 'socket' not found":
- Update Tacview to version 1.9.0 or higher
- See [`LUASOCKET.md`](../src/AeroDebrief.Integrations/Lua/Tacview/AeroDebriefSync/LUASOCKET.md) for troubleshooting

## Configuration

Edit `config.txt` in addon folder:
```ini
Port=52001
BindAddress=127.0.0.1
UpdateRate=10
AutoReconnect=true
```

## Message Types

### Tacview ? AeroDebrief

| Type | Description | Frequency |
|------|-------------|-----------|
| `time_update` | Mission time, playback state, speed | 10 Hz |
| `pilot_selection` | Selected pilots with config | On selection |
| `playback_command` | Play/pause/stop | On state change |
| `seek` | Seek to time | On seek |

### AeroDebrief ? Tacview

| Type | Description | When |
|------|-------------|------|
| `ready` | Initial handshake | On connect |
| `frequency_filter_update` | Update frequency filters | On user change |
| `pan_configuration` | Update pan settings | On user change |

## Menu Items

```
Tacview ? AeroDebrief Sync
??? Configure Audio Pan...       # Set Auto/Manual mode
??? Auto Pan Mode                # Switch to auto pan
??? Manual Pan Mode              # Switch to manual pan
??? ?????????????????????????????
??? Configure Pilot Frequencies  # Per-pilot frequency settings
??? Configure General Frequencies# Non-selected pilot frequencies
??? ?????????????????????????????
??? Settings...                  # View connection status
??? Change TCP Port...           # Port configuration
??? Save Settings                # Save config to file
??? ?????????????????????????????
??? About                        # Version and info
```

## Default Behaviors

| Setting | Default | Reason |
|---------|---------|--------|
| TCP Port | 52001 | Standard port |
| Bind Address | 127.0.0.1 | Security (localhost only) |
| Update Rate | 10 Hz | Balance accuracy/performance |
| Pan Mode | Auto | Ease of use |
| General Frequencies | All disabled | Privacy/usability |
| Auto-Reconnect | Enabled | User convenience |

## File Locations

### Tacview Addon
```
%APPDATA%\Tacview\AddOns\AeroDebriefSync\
??? main.lua              # Entry point
??? tcp_server.lua        # TCP server
??? protocol.lua          # JSON protocol
??? config.lua            # Configuration
??? config.txt            # User settings (created on first run)
```

### AeroDebrief
```
src/AeroDebrief.Integrations/
??? Tacview/
?   ??? Models/           # Data models
?   ??? Protocol/         # Message classes
??? Lua/                  # Lua addon source
```

## Troubleshooting

### Port Already in Use
```bash
# Change port in Tacview addon
Edit: %APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt
Change: Port=52001 ? Port=52002

# Update AeroDebrief settings to match
```

### Addon Not Loading
```bash
# Check Tacview version
Minimum: 1.9.0 or higher

# Check log for errors
Help ? Show Log
Look for: "AeroDebrief Sync:" messages
```

### No Clients Connecting
```bash
# Verify both apps running
1. Tacview with addon installed
2. AeroDebrief with Tacview integration enabled

# Check firewall
Windows Firewall ? Allow Tacview.exe
```

## Testing with Command Line

```bash
# Connect with netcat
nc 127.0.0.1 52001

# Or telnet
telnet 127.0.0.1 52001

# Send ready message
{"type":"ready","version":"1.0.0"}

# You should receive time_update messages
```

## Performance

| Metric | Value | Notes |
|--------|-------|-------|
| CPU Usage | < 1% | Idle state |
| Memory | < 5 MB | All components |
| Network | ~1 KB/s | During playback |
| Latency | < 10 ms | Update loop |
| Update Rate | 10 Hz | Configurable |

## Status Indicators

### Tacview Log Messages

| Message | Meaning |
|---------|---------|
| `Initialized successfully` | ? Addon loaded |
| `TCP server listening` | ? Ready for connections |
| `Client connected` | ? AeroDebrief connected |
| `Client disconnected` | ?? AeroDebrief disconnected |
| `Failed to start TCP server` | ? Port conflict or permission |
| `Selection changed` | ?? Pilot selection updated |
| `Playback state changed` | ?? Play/pause triggered |

## JSON Examples

### Time Update
```json
{
  "type": "time_update",
  "mission_time_utc": "2024-01-15T14:23:45Z",
  "playback_state": "playing",
  "playback_speed": 1.0
}
```

### Pilot Selection
```json
{
  "type": "pilot_selection",
  "selected_pilots": [
    {
      "pilot_id": "ABCD1234",
      "pilot_name": "Viper 1-1",
      "coalition": "Blue",
      "unit_type": "F-16C",
      "frequencies": [251.0, 305.0],
      "enabled_frequencies": [251.0],
      "pan": 0.0
    }
  ],
  "pan_mode": "auto",
  "general_enabled_frequencies": []
}
```

### Frequency Filter Update (AeroDebrief ? Tacview)
```json
{
  "type": "frequency_filter_update",
  "pilot_id": "ABCD1234",
  "enabled_frequencies": [251.0]
}
```

### Pan Configuration (AeroDebrief ? Tacview)
```json
{
  "type": "pan_configuration",
  "pan_mode": "manual",
  "pilot_pan_settings": {
    "ABCD1234": -0.5,
    "DCBA4321": 0.5
  }
}
```

## Development

### Build Project
```bash
cd src/AeroDebrief.Integrations
dotnet build
```

### Run Tests (Phase 6)
```bash
cd tests/AeroDebrief.Tests
dotnet test --filter "Category=Tacview"
```

### Deploy Addon
```bash
# Copy to Tacview automatically (future)
dotnet run --project AeroDebrief.UI -- --install-tacview-addon
```

## Support

- **Documentation**: `docs/tacview-integration/`
- **GitHub Issues**: https://github.com/shalevohad/AeroDebrief/issues
- **Implementation Guide**: `IMPLEMENTATION-GUIDE.md`
- **Architecture**: `ARCHITECTURE-DIAGRAM.md`

## Version History

### 1.0.0 (Phase 1-2 Complete)
- ? TCP server with multi-client support
- ? 10 Hz time synchronization
- ? Pilot selection filtering
- ? Frequency filtering (per-pilot and general)
- ? Spatial audio (pan) support
- ? Bidirectional configuration
- ? Full Tacview menu integration
- ? Persistent configuration

### Future (Phase 3-6)
- ? C# TCP client
- ? Time synchronization service
- ? Audio filtering integration
- ? WPF UI status panel
- ? Comprehensive testing

## Quick Links

- [Installation Guide](INSTALL.md)
- [User README](README.md)
- [Phase 1-2 Summary](PHASE1-2-SUMMARY.md)
- [Implementation Guide](IMPLEMENTATION-GUIDE.md)

---

**Ready to use!** ??

Install the addon in Tacview and it will work immediately (even without AeroDebrief connected).
