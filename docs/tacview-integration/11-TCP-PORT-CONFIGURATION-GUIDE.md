# TCP Port Configuration Guide

## Overview

Both Tacview and AeroDebrief use TCP port **52001** by default for communication. This guide explains how to change the port if needed (firewall conflicts, multiple instances, etc.).

## Why Change the Port?

### Common Reasons
- **Port Conflict**: Another application is using port 52001
- **Firewall Rules**: Your firewall blocks port 52001
- **Multiple Instances**: Running multiple Tacview/AeroDebrief pairs
- **Security Policy**: Your organization requires specific port ranges
- **Testing**: Separating test and production environments

### Default Port
- **Port**: 52001
- **Protocol**: TCP
- **Binding**: localhost (127.0.0.1) only - no external network access
- **Range**: Can be changed to any port between 1024-65535

---

## Configuration Steps

### Step 1: Configure Tacview Addon

#### Option A: Using Tacview Menu (Recommended)

1. Start Tacview with AeroDebrief Sync addon loaded
2. Go to: `Tacview Menu ? AeroDebrief Sync ? Settings...`
3. Select: "Change TCP Port..."
4. Enter new port number (e.g., 52002)
5. Click "Save Settings"
6. **Restart Tacview**

**Menu Navigation**:
```
Tacview Menu Bar
?? AeroDebrief Sync
   ?? Configure Audio Pan...
   ?? ...
   ?? Settings...              ??? Click here
   ?  ?? Change TCP Port...    ??? Then here
   ?  ?? ...
   ?  ?? Save Settings
   ?? About
```

#### Option B: Manual Configuration File

1. Close Tacview if running
2. Navigate to: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\`
3. Edit or create: `config.txt`
4. Add or modify:
   ```
   Port=52002
   ```
5. Save file
6. Start Tacview

**Example config.txt**:
```ini
Port=52002
BindAddress=127.0.0.1
UpdateRate=10
EnableLogging=true
EnableAutoReconnect=true
MaxClients=5
```

---

### Step 2: Configure AeroDebrief

#### Option A: Using UI Settings (Recommended)

1. Start AeroDebrief
2. Open Tacview Integration settings panel
3. Change Port field to match Tacview (e.g., 52002)
4. Click "Save Settings"
5. Click "Reconnect Now" (or restart AeroDebrief)

**UI Location**:
```
AeroDebrief Main Window
?? Tacview Integration Panel
   ?? ?? Settings (Expander)
      ?? Connection:
      ?  ?? Host: [127.0.0.1]
      ?  ?? Port: [52002]       ??? Change here
      ?? [Save Settings]        ??? Then click
```

#### Option B: Configuration File

1. Close AeroDebrief if running
2. Navigate to: `%APPDATA%\AeroDebrief\config\` (or install directory)
3. Edit: `tacview-config.json`
4. Modify port value:
   ```json
   {
     "Host": "127.0.0.1",
     "Port": 52002,
     "AutoConnect": true,
     ...
   }
   ```
5. Save file
6. Start AeroDebrief

**Example tacview-config.json**:
```json
{
  "Host": "127.0.0.1",
  "Port": 52002,
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

---

## Verification

### Check Connection Status

**In AeroDebrief**:
```
Tacview Integration Panel
Status: ? Connected        ??? Should be green
Sync Quality: Good
```

**In Tacview Log**:
```
Window ? Show Log (or Help ? Show Log)

Look for:
[INFO] AeroDebrief Sync: Initialized successfully on port 52002
[INFO] TCP Server listening on 127.0.0.1:52002
[INFO] AeroDebrief client connected from 127.0.0.1:xxxxx
```

**In AeroDebrief Log**:
```
Look for:
[INFO] Tacview: Connecting to 127.0.0.1:52002
[INFO] Tacview: Connected successfully
[INFO] Tacview: Sync established
```

---

## Troubleshooting

### Problem: Connection Failed

**Symptoms**:
- AeroDebrief shows "Disconnected" (red indicator)
- Tacview log shows "No clients connected"
- Error: "Connection refused" or "Connection timeout"

**Solutions**:

1. **Verify Port Match**:
   - Tacview: Check `Settings ? Change TCP Port`
   - AeroDebrief: Check `Settings ? Port`
   - **They MUST be the same**

2. **Check Port Availability**:
   - Open Command Prompt (Windows) or Terminal
   - Run: `netstat -an | findstr :52002` (replace with your port)
   - If port is in use by another app, choose different port

3. **Restart Both Applications**:
   - Close AeroDebrief completely
   - Close Tacview completely
   - Start Tacview first (server starts)
   - Start AeroDebrief second (client connects)

4. **Check Firewall**:
   - Windows Firewall may block localhost connections
   - Add exception for:
     - Tacview.exe
     - AeroDebrief.UI.exe
   - Or temporarily disable firewall for testing

5. **Verify Configuration Files**:
   - Tacview: `%APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt`
   - AeroDebrief: `%APPDATA%\AeroDebrief\config\tacview-config.json`
   - Ensure Port values match

---

### Problem: Port Already in Use

**Symptoms**:
- Tacview log shows: "Failed to start TCP server: Address already in use"
- AeroDebrief can't connect

**Solutions**:

1. **Find Conflicting Application**:
   ```cmd
   netstat -ano | findstr :52001
   ```
   
   Output example:
   ```
   TCP    127.0.0.1:52001    0.0.0.0:0    LISTENING    12345
   ```
   
   The last number (12345) is the Process ID

2. **Identify Process**:
   ```cmd
   tasklist | findstr 12345
   ```

3. **Choose Different Port**:
   - If another app needs port 52001, choose different port
   - Recommended alternatives: 52002, 52003, 53001, etc.
   - Configure both Tacview and AeroDebrief with new port

---

### Problem: Port Change Not Taking Effect

**Symptoms**:
- Changed port but still trying old port
- Tacview log still shows old port

**Solutions**:

1. **Fully Restart Both Applications**:
   - Don't just reconnect - fully close and restart
   - Task Manager ? End Task if needed

2. **Verify Configuration Saved**:
   - Tacview: Click "Save Settings" after changing port
   - AeroDebrief: Click "Save Settings" or "Apply"

3. **Check Configuration File Manually**:
   - Open config files in text editor
   - Verify port value is correct
   - Save and close file before starting app

4. **Clear Cache** (if issue persists):
   - Delete configuration files
   - Restart applications
   - Reconfigure from scratch

---

## Advanced Scenarios

### Multiple Tacview/AeroDebrief Pairs

**Scenario**: Running multiple Tacview instances with separate AeroDebrief instances

**Configuration**:

**Instance 1**:
- Tacview: Port 52001
- AeroDebrief: Port 52001

**Instance 2**:
- Tacview: Port 52002
- AeroDebrief: Port 52002

**Steps**:
1. Configure each Tacview addon with unique port
2. Configure each AeroDebrief with matching port
3. Ensure each pair uses different port number

---

### Remote Connection (Advanced)

**Default Behavior**: Localhost only (127.0.0.1)

**To Allow Remote Connection** (NOT RECOMMENDED for security):

1. **In Tacview config.txt**:
   ```ini
   BindAddress=0.0.0.0
   Port=52001
   ```

2. **In AeroDebrief**:
   ```
   Host: [192.168.1.100]  (Tacview computer's IP)
   Port: [52001]
   ```

3. **Security Considerations**:
   - ?? Opens port to network
   - ?? No authentication/encryption
   - ?? Only use on trusted networks
   - ?? Configure firewall rules carefully

**Recommendation**: Only use localhost (127.0.0.1) unless absolutely necessary

---

## Port Configuration Best Practices

### Do's ?
- ? Keep port consistent between Tacview and AeroDebrief
- ? Use default port (52001) unless there's a conflict
- ? Save configuration before restarting
- ? Test connection after changing port
- ? Document custom port choices
- ? Stay in localhost mode (127.0.0.1) for security

### Don'ts ?
- ? Don't use ports below 1024 (system ports)
- ? Don't use well-known ports (80, 443, 3306, etc.)
- ? Don't change port without updating both applications
- ? Don't expose port to external network without security
- ? Don't forget to restart after port change

---

## Quick Reference

### Default Values
| Setting | Default | Range | Notes |
|---------|---------|-------|-------|
| Port | 52001 | 1024-65535 | Must match in both apps |
| Host | 127.0.0.1 | Valid IP | Localhost recommended |
| Bind | 127.0.0.1 | Valid IP | Tacview only |

### Configuration Files
| Application | Location |
|-------------|----------|
| Tacview | `%APPDATA%\Tacview\AddOns\AeroDebriefSync\config.txt` |
| AeroDebrief | `%APPDATA%\AeroDebrief\config\tacview-config.json` |

### Menu Locations
| Application | Menu Path |
|-------------|-----------|
| Tacview | `AeroDebrief Sync ? Settings... ? Change TCP Port...` |
| AeroDebrief | `Tacview Integration Panel ? Settings ? Port` |

---

## Frequently Asked Questions

**Q: Do I need to change the port?**  
A: No, most users can keep the default port 52001. Only change if you have a specific need.

**Q: Can I use any port number?**  
A: Use ports between 1024 and 65535. Avoid well-known ports already in use by other services.

**Q: What happens if ports don't match?**  
A: AeroDebrief won't be able to connect to Tacview. You'll see "Connection Failed" errors.

**Q: Can I run multiple pairs with different ports?**  
A: Yes! Each Tacview/AeroDebrief pair can use a unique port (52001, 52002, 52003, etc.).

**Q: Does changing the port affect performance?**  
A: No, port number has no performance impact. All ports work equally well.

**Q: Can I connect over network instead of localhost?**  
A: Technically yes, but NOT recommended due to security concerns. Use localhost (127.0.0.1).

**Q: Do I need to restart after changing port?**  
A: Yes, both Tacview and AeroDebrief must be restarted for port changes to take effect.

**Q: How do I check if the port is available?**  
A: Run `netstat -an | findstr :52001` in Command Prompt (replace with your port number).

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Complete
