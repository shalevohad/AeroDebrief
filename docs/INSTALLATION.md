# AeroDebrief Installation Guide

This guide provides detailed instructions for installing and configuring AeroDebrief on your Windows system.

## Table of Contents

1. [System Requirements](#system-requirements)
2. [Pre-Installation Checklist](#pre-installation-checklist)
3. [Installation Methods](#installation-methods)
4. [Post-Installation Setup](#post-installation-setup)
5. [Configuration](#configuration)
6. [Verification](#verification)
7. [Troubleshooting](#troubleshooting)
8. [Uninstallation](#uninstallation)

## System Requirements

### Minimum Requirements

- **Operating System**: Windows 10 (64-bit) or later
- **Processor**: Intel Core i5 or AMD equivalent (2.0 GHz or faster)
- **Memory**: 4 GB RAM
- **Graphics**: DirectX 11 compatible graphics card (optional but recommended)
- **Storage**: 500 MB for installation + space for recordings
- **Network**: Internet connection for initial setup and SRS connectivity
- **.NET Runtime**: .NET 9.0 Runtime (included in installer or auto-installed)

### Recommended Requirements

- **Operating System**: Windows 11 (64-bit)
- **Processor**: Intel Core i7 or AMD Ryzen 5 (3.0 GHz or faster)
- **Memory**: 8 GB RAM or more
- **Graphics**: NVIDIA GTX 1060 / AMD RX 580 or better (for GPU-accelerated waveforms)
- **Storage**: SSD with 10+ GB free space
- **Network**: Broadband internet connection
- **.NET Runtime**: .NET 9.0 Runtime (latest version)

### Software Dependencies

AeroDebrief works alongside these applications:

- **DCS World**: Any recent version
- **Simple Radio Standalone (SRS)**: Version 2.3.20 or later
  - Download from: [DCS-SimpleRadioStandalone](https://github.com/ciribob/DCS-SimpleRadioStandalone/releases)

## Pre-Installation Checklist

Before installing AeroDebrief, ensure:

- [ ] Your Windows is up to date (Windows Update)
- [ ] You have administrator privileges on your computer
- [ ] You have DCS World installed (if recording from DCS missions)
- [ ] You have SRS installed and configured
- [ ] You have at least 500 MB free disk space
- [ ] You have a backup of any existing AeroDebrief recordings (if upgrading)

## Installation Methods

### Method 1: Release Binary (Recommended for Users)

This is the simplest installation method for end users.

#### Step 1: Download

1. Go to the [AeroDebrief Releases](https://github.com/shalevohad/AeroDebrief/releases) page
2. Download the latest release ZIP file:
   - Look for `AeroDebrief-vX.X.X-win-x64.zip`
   - Check the release notes for any special instructions

#### Step 2: Extract

1. Right-click the downloaded ZIP file
2. Select "Extract All..."
3. Choose a destination folder:
   - Recommended: `C:\Program Files\AeroDebrief`
   - Alternative: `C:\Users\YourUsername\AppData\Local\AeroDebrief`
4. Click "Extract"

#### Step 3: Verify Extraction

Check that the folder contains:
- `AeroDebrief.UI.exe` (main application)
- `AeroDebrief.Core.dll`
- Various other DLL files
- `README.md`

#### Step 4: Create Shortcut (Optional)

1. Right-click `AeroDebrief.UI.exe`
2. Select "Send to" → "Desktop (create shortcut)"
3. Rename the shortcut to "AeroDebrief"

#### Step 5: .NET Runtime Check

When you first run AeroDebrief:
- Windows will check for .NET 9.0 Runtime
- If not installed, you'll be prompted to download it
- Follow the prompts to install .NET 9.0 Runtime
- Restart AeroDebrief after .NET installation

### Method 2: Self-Contained Release (No .NET Required)

If you prefer not to install .NET separately, download the self-contained release:

1. Download `AeroDebrief-vX.X.X-win-x64-selfcontained.zip`
2. Extract as described above
3. The package is larger (~150 MB) but includes .NET runtime
4. No additional .NET installation required

### Method 3: Build from Source (For Developers)

See the [Developer Guide](DEVELOPER_GUIDE.md) for instructions on building from source.

Quick steps:
```bash
git clone https://github.com/shalevohad/AeroDebrief.git
cd AeroDebrief
git submodule update --init --recursive
dotnet restore
dotnet build --configuration Release
dotnet run --project src/AeroDebrief.UI
```

## Post-Installation Setup

### First Launch

1. **Run AeroDebrief**:
   - Double-click `AeroDebrief.UI.exe`
   - Or use the desktop shortcut you created

2. **Initial Configuration Wizard** (if present):
   - Follow the on-screen prompts
   - Configure SRS server settings
   - Set default recording location

3. **Grant Firewall Permissions** (if prompted):
   - Windows Firewall may ask for network access
   - Click "Allow access" to enable SRS connectivity

### Creating Recording Directory

Create a dedicated folder for your recordings:

1. Recommended location: `C:\Users\YourUsername\Documents\AeroDebrief\Recordings`
2. Alternative: External drive with ample space
3. Set this as your default recording location in AeroDebrief settings

### SRS Configuration

Ensure SRS is properly configured:

1. **Launch SRS**: Start Simple Radio Standalone
2. **Connect to Server**:
   - Enter server IP and port
   - Default port: 5002 (UDP voice), 5000 (TCP control)
3. **Verify Connection**: You should see other players if connected

AeroDebrief will automatically detect SRS settings if running on the same machine.

## Configuration

### Application Settings

Access settings through the AeroDebrief UI:

1. **General Settings**:
   - Default recording location
   - Auto-save recordings
   - File naming conventions

2. **Audio Settings**:
   - Output device selection
   - Buffer size (affects latency)
   - Default volume levels

3. **Network Settings**:
   - SRS server IP address
   - SRS server port
   - Auto-connect on startup

4. **Performance Settings**:
   - Enable/disable GPU acceleration
   - Waveform rendering quality
   - Maximum cache size

5. **UI Settings**:
   - Theme (if available)
   - Font sizes
   - Default window layout

### Configuration File

Advanced users can edit the configuration file directly:

**Location**: `C:\Users\YourUsername\AppData\Local\AeroDebrief\config.json`

Example configuration:
```json
{
  "srsServerAddress": "192.168.1.100",
  "srsServerPort": 5002,
  "recordingDirectory": "C:\\Users\\YourUsername\\Documents\\AeroDebrief\\Recordings",
  "autoSaveRecordings": true,
  "gpuAccelerationEnabled": true,
  "bufferSizeMs": 100
}
```

### SRS Server Connection

To configure SRS connection:

1. **Obtain Server Details** from your DCS server administrator:
   - Server IP address
   - Voice port (typically 5002)
   - Control port (typically 5000)

2. **Enter in AeroDebrief**:
   - Settings → Network → SRS Server
   - Enter IP address
   - Enter ports
   - Test connection

3. **Save Configuration**

## Verification

### Verify Installation

Run these checks to ensure AeroDebrief is installed correctly:

1. **Launch Test**:
   - [ ] Application starts without errors
   - [ ] Main window displays properly
   - [ ] No error messages in startup

2. **UI Test**:
   - [ ] All menu items accessible
   - [ ] Controls respond to clicks
   - [ ] Waveform viewer displays

3. **GPU Acceleration Test** (if applicable):
   - Check status bar or logs for "GPU acceleration: Enabled"
   - If disabled, verify DirectX 11 compatibility

4. **Audio System Test**:
   - Settings → Audio → Test Output
   - You should hear a test tone

### Test Recording (Optional)

If you have access to an SRS server:

1. Connect to SRS server
2. Start a test recording in AeroDebrief
3. Transmit on a frequency in SRS
4. Stop recording
5. Play back the recording
6. Verify audio is captured correctly

### Verify .NET Installation

```powershell
# Open PowerShell and run:
dotnet --list-runtimes

# Look for:
# Microsoft.WindowsDesktop.App 9.0.x
```

## Troubleshooting

### Installation Issues

#### Issue: "Cannot extract ZIP file"
**Solution**:
- Right-click ZIP → Properties → Unblock → Apply
- Try extracting to a different location
- Use 7-Zip or WinRAR if Windows Explorer fails

#### Issue: "Missing .NET Runtime"
**Solution**:
- Download .NET 9.0 Runtime manually from [Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0)
- Install "Desktop Runtime" for Windows
- Restart computer after installation

#### Issue: "Application won't start"
**Solution**:
- Check Windows Event Viewer for errors
- Verify all DLL files were extracted
- Try running as Administrator
- Reinstall .NET 9.0 Runtime

#### Issue: "GPU acceleration not available"
**Solution**:
- Update graphics drivers
- Verify DirectX 11 support: Run `dxdiag` and check DirectX version
- GPU acceleration is optional; CPU fallback works fine

### Permission Issues

#### Issue: "Access Denied"
**Solution**:
- Run as Administrator (right-click → Run as administrator)
- Install to a user-writable location
- Check folder permissions

#### Issue: "Cannot write recordings"
**Solution**:
- Verify recording directory exists
- Check folder write permissions
- Ensure sufficient disk space

### Network Issues

#### Issue: "Cannot connect to SRS server"
**Solution**:
- Verify SRS is running and connected
- Check server IP and port are correct
- Disable VPN temporarily
- Check Windows Firewall settings
- Verify network connectivity to server

### Performance Issues

#### Issue: "Application is slow"
**Solution**:
- Close other applications
- Ensure GPU acceleration is enabled
- Increase system RAM if possible
- Store recordings on SSD

## Uninstallation

### Standard Uninstall

1. **Close AeroDebrief**: Ensure application is not running
2. **Backup Recordings**: Copy any recordings you want to keep
3. **Delete Application Folder**: Delete the folder where you extracted AeroDebrief
4. **Remove Configuration** (optional):
   - Delete: `C:\Users\YourUsername\AppData\Local\AeroDebrief`
5. **Remove Desktop Shortcut** (if created)

### Clean Uninstall

To remove all traces:

1. Follow standard uninstall steps above
2. **Remove .NET Runtime** (if not needed for other applications):
   - Settings → Apps → Installed Apps
   - Find ".NET Desktop Runtime 9.0"
   - Click Uninstall
3. **Clean Registry** (advanced users):
   - Use a registry cleaner or manually remove AeroDebrief entries

### Keeping Settings for Reinstall

To preserve settings when reinstalling:
1. Backup `C:\Users\YourUsername\AppData\Local\AeroDebrief`
2. Uninstall AeroDebrief
3. Reinstall AeroDebrief
4. Restore the backed-up folder

## Next Steps

After installation:

1. **Read the User Guide**: [USER_GUIDE.md](USER_GUIDE.md)
2. **Configure SRS Connection**: Set up your SRS server details
3. **Create First Recording**: Record a test session
4. **Explore Features**: Try the analytics and waveform viewer
5. **Join Community**: GitHub Discussions for tips and support

## Getting Help

If you encounter issues:

- **Documentation**: Check [USER_GUIDE.md](USER_GUIDE.md) and [FAQ](USER_GUIDE.md#faq)
- **GitHub Issues**: Search or create an issue at [GitHub Issues](https://github.com/shalevohad/AeroDebrief/issues)
- **Community**: Ask in [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions)

---

**Installation Complete!**  
You're ready to start using AeroDebrief. Happy debriefing!
