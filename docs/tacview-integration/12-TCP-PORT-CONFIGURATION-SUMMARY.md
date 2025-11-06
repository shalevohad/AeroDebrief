# TCP Port Configuration - Feature Summary

## Overview

Added comprehensive TCP port configuration capability to both Tacview addon and AeroDebrief integration, allowing users to customize the communication port from the default 52001 to any valid port (1024-65535).

## Key Features

### 1. Configurable in Both Applications
- **Tacview Addon**: Port configuration via menu UI or config file
- **AeroDebrief**: Port configuration via settings UI or JSON config file
- **Must Match**: Both applications must use the same port to communicate

### 2. Multiple Configuration Methods
- **UI-Based** (Recommended): Interactive menu dialogs with validation
- **File-Based**: Direct editing of configuration files for automation
- **Persistent**: Settings save across restarts

### 3. Port Validation
- **Range Check**: Port must be between 1024-65535
- **Availability Check**: Warns if port is already in use
- **Conflict Detection**: Helps identify and resolve port conflicts

### 4. Multiple Instance Support
- **Unique Ports**: Each Tacview/AeroDebrief pair can use different port
- **No Interference**: Multiple sessions run simultaneously
- **Easy Management**: Clear configuration per instance

---

## Changes Made

### 1. Tacview Lua Addon

#### New File: `config.lua`

Complete configuration management system:

```lua
local Config = {
    Port = 52001,              -- Configurable TCP port
    BindAddress = "127.0.0.1",
    UpdateRate = 10,
    EnableLogging = true,
    // ... other settings
}

function Config.Load()  -- Load from config.txt
function Config.Save()  -- Save to config.txt
function Config.Initialize()  -- Setup at startup
```

**Features**:
- Auto-load from `config.txt` on startup
- Auto-save on shutdown or manual save
- Type-safe parsing (numbers, booleans, strings)
- Default values if file not found

#### Updated: `main.lua`

```lua
function AeroDebriefSync:OnInitialize()
    config.Initialize()  -- Load config
    tcpServer.Start(config.Port, config.BindAddress)  -- Use configured port
    // ...
end

function AeroDebriefSync:OnShutdown()
    config.Save()  -- Persist settings
    // ...
end
```

#### Updated: `menu_ui.lua`

Added "Settings..." menu item with full configuration dialog:

```lua
Tacview UI Menu Items:
?? Configure Audio Pan...
?? Auto/Manual Pan Mode
?? Configure Frequencies...
?? Settings...           ??? NEW
?  ?? Change TCP Port...
?  ?? Change Update Rate...
?  ?? Change Max Clients...
?  ?? Toggle Options
?  ?? Save Settings
?? About
```

**Settings Dialog Features**:
- Interactive port input with validation
- Real-time feedback on valid/invalid ports
- Warning about restart requirement
- Quick access to all configuration options
- Persistent save on user confirmation

---

### 2. AeroDebrief Integration

#### New Class: `TacviewConfiguration`

```csharp
public class TacviewConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 52001;  // Configurable
    public bool AutoConnect { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int ReconnectIntervalSeconds { get; set; } = 5;
    // ... other settings
    
    public bool Validate(out string? error)  // Validation
    public void SaveToFile(string filePath)  // Persistence
    public static TacviewConfiguration LoadFromFile(string filePath)
}
```

**Features**:
- Full validation with error messages
- JSON serialization for persistence
- Default value creation
- Type safety with range checks

#### Updated: `TacviewClient`

```csharp
public class TacviewClient
{
    private readonly TacviewConfiguration _config;
    
    public async Task ConnectAsync()
    {
        // Use configured host and port
        await _tcpClient.ConnectAsync(_config.Host, _config.Port);
    }
}
```

#### Updated: UI Design

**Settings Panel**:
```xaml
<Expander Header="?? Tacview Integration Settings">
    <Grid>
        <TextBlock Text="Host:"/>
        <TextBox Text="{Binding Config.Host}"/>
        
        <TextBlock Text="Port:"/>
        <TextBox Text="{Binding Config.Port}"/>
        
        <Button Content="Save Settings"/>
        <Button Content="Test Connection"/>
    </Grid>
</Expander>
```

**Features**:
- Two-way data binding to configuration
- Real-time validation in UI
- Save button to persist changes
- Test connection without reconnect
- Warning about restart requirement

---

## Configuration Files

### Tacview: `config.txt`

**Location**: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt`

**Format**:
```ini
Port=52001
BindAddress=127.0.0.1
UpdateRate=10
EnableLogging=true
EnableAutoReconnect=true
MaxClients=5
EnableSpeakingIndicators=true
HighlightColor=0x00FF00
HighlightScale=1.5
```

**Parsing**: Key-value pairs with type detection

### AeroDebrief: `tacview-config.json`

**Location**: `%APPDATA%\AeroDebrief\config\tacview-config.json`

**Format**:
```json
{
  "Host": "127.0.0.1",
  "Port": 52001,
  "AutoConnect": true,
  "AutoReconnect": true,
  "MaxReconnectAttempts": 10,
  "ReconnectIntervalSeconds": 5,
  "EnableSyncDriftCorrection": true,
  "MaxAcceptableDriftMs": 500,
  "EnableSpatialAudio": true,
  "EnableFrequencyFiltering": true,
  "ConnectionTimeoutMs": 10000,
  "ReceiveTimeoutMs": 5000
}
```

**Parsing**: Standard JSON with C# deserialization

---

## User Experience

### Configuration Flow

#### Initial Setup (Default Port)
```
1. Install Tacview addon ? Uses port 52001
2. Install AeroDebrief ? Uses port 52001
3. Start both ? Connect automatically ?
```

#### Custom Port Setup
```
1. Tacview: Menu ? Settings ? Change TCP Port ? 52002
2. Save Settings ? Restart Tacview
3. AeroDebrief: Settings ? Port: 52002 ? Save
4. Reconnect or restart AeroDebrief
5. Connection established ?
```

#### Multiple Instances
```
Instance 1:
- Tacview A: Port 52001
- AeroDebrief A: Port 52001

Instance 2:
- Tacview B: Port 52002
- AeroDebrief B: Port 52002

Both pairs work independently ?
```

---

## Use Cases

### Use Case 1: Port Conflict Resolution

**Scenario**: Port 52001 already in use by another application

**Solution**:
1. Check conflicting application: `netstat -ano | findstr :52001`
2. Choose different port (e.g., 52003)
3. Configure Tacview: Settings ? Port ? 52003
4. Configure AeroDebrief: Settings ? Port ? 52003
5. Restart both ? Conflict resolved ?

---

### Use Case 2: Multiple Analysis Sessions

**Scenario**: Analyzing two different missions simultaneously

**Configuration**:
- **Session 1**: Tacview on port 52001 + AeroDebrief on port 52001
- **Session 2**: Tacview on port 52002 + AeroDebrief on port 52002

**Result**: Two independent analysis workflows running concurrently

---

### Use Case 3: Corporate Firewall Policy

**Scenario**: IT department requires specific port range (53000-53999)

**Solution**:
1. Request port allocation from IT (e.g., 53001)
2. Configure both applications to use port 53001
3. Create firewall exception for port 53001
4. Deploy configuration files to users
5. Compliance achieved ?

---

### Use Case 4: Test vs Production

**Scenario**: Separate test and production environments

**Configuration**:
- **Test**: Port 52001 (development builds)
- **Production**: Port 52002 (stable releases)

**Benefit**: No interference between environments

---

## Documentation Updates

### Files Created
1. ? **11-TCP-PORT-CONFIGURATION-GUIDE.md** - Complete user guide
   - Why change port
   - Step-by-step configuration
   - Troubleshooting
   - Advanced scenarios
   - FAQ

### Files Modified
1. ? **01-LUA-ADDON-SPECIFICATION.md**
   - Added `config.lua` specification
   - Updated `main.lua` with config initialization
   - Updated `menu_ui.lua` with settings dialog

2. ? **03-INTEGRATION-PROJECT-DESIGN.md**
   - Added `TacviewConfiguration` class
   - Validation and persistence methods
   - File paths and formats

3. ? **04-UI-DESIGN.md**
   - Updated settings panel XAML
   - Added port input fields
   - Added save/test buttons
   - Added warning about restart requirement

4. ? **README.md**
   - Added "Configurable TCP Port" to key features
   - Updated Tacview menu structure with Settings
   - Updated document reference table

---

## Testing Checklist

### Tacview Addon
- [ ] Config file loads on startup
- [ ] Config file saves on shutdown
- [ ] Settings menu appears correctly
- [ ] Port input accepts valid values (1024-65535)
- [ ] Port input rejects invalid values (<1024, >65535, non-numeric)
- [ ] Port change requires restart (message shown)
- [ ] TCP server starts on configured port
- [ ] Multiple instances use different ports successfully

### AeroDebrief Integration
- [ ] Configuration class validates inputs
- [ ] JSON config file loads on startup
- [ ] JSON config file saves when user clicks Save
- [ ] UI shows current port value
- [ ] UI validates port range
- [ ] Test Connection button works
- [ ] Reconnect uses new port after save
- [ ] Error messages are clear and helpful

### End-to-End
- [ ] Default port (52001) works out of box
- [ ] Custom port works when matching in both apps
- [ ] Mismatched ports show connection error
- [ ] Port conflict detection works
- [ ] Multiple instances with different ports work simultaneously
- [ ] Configuration persists across restarts

---

## Performance Impact

**No Performance Change**: Port number has no impact on:
- Connection speed
- Data transfer rate
- Message latency
- CPU usage
- Memory usage

Only the configuration management adds minimal overhead (<1ms at startup/shutdown).

---

## Security Considerations

### Localhost Binding (Default)
- ? Binds to 127.0.0.1 by default
- ? No external network access
- ? Safe from remote attacks
- ? No firewall configuration needed

### Custom Binding (Advanced)
- ?? Can bind to 0.0.0.0 (all interfaces)
- ?? Opens port to network
- ?? No authentication/encryption
- ?? Only use on trusted networks

**Recommendation**: Always use localhost (127.0.0.1) binding unless you have specific need for remote connection.

---

## Migration Guide

### For Existing Users (Default Port)
- **No action required**
- Addon and AeroDebrief continue using port 52001
- Can optionally change port if needed

### For New Users
- **Default port works automatically**
- No configuration needed for standard setup
- Change port only if conflict occurs

### For Automated Deployment
1. Create configuration files with desired port
2. Deploy files to appropriate locations:
   - Tacview: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt`
   - AeroDebrief: `%APPDATA%\AeroDebrief\config\tacview-config.json`
3. Ensure ports match in both files
4. Users launch applications ? Settings applied automatically

---

## Future Enhancements

1. **Auto-Detect Available Port**
   - Scan for available ports automatically
   - Suggest alternative if default port unavailable

2. **Port Discovery**
   - AeroDebrief discovers Tacview port automatically
   - Eliminates manual configuration

3. **Dynamic Port Assignment**
   - OS assigns available port dynamically
   - Share port via system clipboard or file

4. **Configuration Import/Export**
   - Export configuration as file
   - Share configurations between users/machines

---

## Summary

### What Changed
- ? Added full TCP port configuration to Tacview addon
- ? Added full TCP port configuration to AeroDebrief
- ? Created comprehensive user guide
- ? Updated all relevant documentation

### Benefits
- ? Resolves port conflicts easily
- ? Enables multiple concurrent sessions
- ? Supports corporate firewall policies
- ? Separates test and production environments
- ? Maintains backward compatibility (default port unchanged)

### User Impact
- ? Positive - Flexibility without complexity
- ? Default users: No change needed
- ? Advanced users: Full control available
- ? Clear documentation and troubleshooting

---

**Feature Status**: ? Designed and Documented  
**Ready for**: Implementation  
**Estimated Effort**: 1-2 days  
**Risk Level**: Low  
**Backward Compatible**: Yes (default port unchanged)

---

**Document Version**: 1.0  
**Date**: 2024-01-XX  
**Author**: AeroDebrief Team
