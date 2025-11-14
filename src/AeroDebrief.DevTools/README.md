# AeroDebrief Developer Tools

## Overview

**AeroDebrief.DevTools.exe** is a standalone developer tool for replaying and analyzing user action logs. This tool ships **separately** from the main AeroDebrief application and is intended for developers only.

## Purpose

When users report bugs, they can provide action log files (`session_*.json`) from their `%APPDATA%\AeroDebrief\ActionLogs\` directory. Developers can then use this tool to:

1. **Load** the user's action log
2. **Replay** the exact sequence of actions
3. **Reproduce** the bug in a controlled environment
4. **Debug** with breakpoints and step-through
5. **Fix** the issue quickly

## Installation

### For Developers

1. Build the solution in Release mode:
   ```
   dotnet build -c Release
   ```

2. The DevTools executable will be in:
   ```
   src\AeroDebrief.DevTools\bin\Release\net9.0-windows\AeroDebrief.DevTools.exe
   ```

3. Copy to your developer tools folder (not included in releases)

### For Support Team

Distribute this tool only to developers. **Never** include it in end-user releases.

## Usage

### Step 1: Get User's Log File

Ask the user to provide their action log from:
```
%APPDATA%\AeroDebrief\ActionLogs\session_YYYYMMDD_HHmmss_GUID.json
```

### Step 2: Launch DevTools

Double-click `AeroDebrief.DevTools.exe` or run from command line:
```cmd
AeroDebrief.DevTools.exe
```

### Step 3: Load Log File

1. Click **"Browse..."** and select user's log file
2. Or select from **"Recent Sessions"** list
3. Click **"Load"**

### Step 4: Review Summary

The tool shows:
- Total actions
- Duration
- Error count
- Actions by type

### Step 5: Replay Actions

1. Click **"? Play"** to start replay
2. Adjust speed (0.1x to 10x) if needed
3. Click **"? Pause"** to pause
4. Click **"? Stop"** to stop

### Step 6: Inspect Errors

1. Use **Filter** dropdown ? "Errors Only"
2. Click on error in list
3. View full stack trace in details pane

### Step 7: Debug

1. Set breakpoints in Visual Studio
2. Run main AeroDebrief.UI with debugger
3. Replay actions in DevTools
4. Watch debugger hit breakpoints

## Features

- ? **Load Action Logs** - Browse and load user logs
- ? **Replay with Timing** - Replays with original timing
- ? **Variable Speed** - 0.1x to 10x playback speed
- ? **Filter Actions** - By type (errors, file ops, playback, etc.)
- ? **Action Details** - View parameters and results
- ? **Export Actions** - Export individual actions to text
- ? **Recent Sessions** - Quick access to recent logs
- ? **Progress Tracking** - See replay progress
- ? **Error Highlighting** - Easily spot errors

## Architecture

```
AeroDebrief.UI (User App)
     ?
  Records Actions ? JSON Log Files
                         ?
              AeroDebrief.DevTools (Developer Tool)
                         ?
                   Loads & Replays
```

## File Structure

```
AeroDebrief.DevTools/
??? AeroDebrief.DevTools.csproj  - Project file
??? App.xaml / App.xaml.cs       - Application entry point
??? NLog.config                  - Logging configuration
??? Replay/
?   ??? UserActionReplayEngine.cs - Replay logic
??? Windows/
    ??? ActionReplayWindow.xaml/cs - Main UI
```

## Dependencies

- .NET 9
- WPF
- NLog
- AeroDebrief.Core (for UserAction types)

## Log Format

Action logs are JSON files with this structure:

```json
[
  {
    "Timestamp": "2024-12-15T14:30:22.123Z",
    "ActionType": "FileLoad",
    "ControlName": "FileOperations",
    "Parameters": "{\"FileName\":\"example.srs\"}",
    "Result": null,
    "ThreadId": 1
  }
]
```

## Example Workflow

### Bug Report: "App crashes when seeking to 5 minutes"

1. **User provides**: `session_20241215_143022_abc123.json`
2. **Developer loads** log in DevTools
3. **DevTools shows** sequence:
   ```
   14:30:22 - FileLoad: "large.srs"
   14:30:25 - PlaybackStart
   14:30:28 - PlaybackSeek: 00:05:00
   14:30:28 - ERROR: NullReferenceException
   ```
4. **Developer sets** breakpoint in seek code
5. **Developer replays** actions
6. **Debugger stops** at breakpoint
7. **Developer finds**: waveform not initialized after seek
8. **Developer fixes** bug
9. **Developer verifies** by replaying again

**Result**: Bug fixed in 30 minutes! ??

## Command Line Options

### Open specific log file:
```cmd
AeroDebrief.DevTools.exe "C:\path\to\session_*.json"
```

### Open logs folder:
```cmd
AeroDebrief.DevTools.exe --open-logs
```

## Logging

DevTools logs are stored in:
```
%APPDATA%\AeroDebrief\DevTools\logs\devtools-YYYY-MM-DD.log
```

## Troubleshooting

### "No recent sessions found"
- Check user's `%APPDATA%\AeroDebrief\ActionLogs\` folder
- Verify action recording is enabled in main app

### "Failed to load log file"
- Verify JSON file is valid
- Check file permissions
- Try opening in text editor to inspect

### "Replay doesn't reproduce bug"
- Bug may depend on external state (files, network)
- Check if file references in log still exist
- Verify same AeroDebrief version

## Privacy & Security

- ? Never share action logs publicly
- ? Never upload logs to public repositories
- ? Treat logs as confidential user data
- ? Delete logs after debugging
- ? Ask user permission before sharing

## Distribution

### Internal Distribution
- Share with development team via secure channels
- Include in developer setup documentation
- Keep separate from user installers

### Never Include In
- End-user releases
- Public downloads
- App store distributions
- Auto-update packages

## Support

**For Developers**:
- See full documentation in `docs/USER_ACTION_REPLAY_SYSTEM.md`
- Contact development team lead

**For Support Team**:
- See `docs/ACTION_REPLAY_INTEGRATION_CHECKLIST.md`
- Forward logs to developers

## Version History

- **v1.0** (December 2024) - Initial release
  - Load and replay action logs
  - Variable speed playback
  - Action filtering
  - Error highlighting

## License

Internal tool - Not for public distribution

---

**Built for developers, by developers.** ???

**Help us make AeroDebrief better by quickly reproducing and fixing user-reported bugs!**
