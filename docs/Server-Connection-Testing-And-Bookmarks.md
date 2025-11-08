# Server Connection Testing and Bookmark Management

## Overview

This document describes the connection testing and bookmark import/export features for SRS server management.

## Connection Testing

### Purpose

The connection test feature allows you to verify server reachability and compatibility **before** establishing a full connection. This helps identify connection issues early without committing to a full connection session.

### How to Use

1. **Enter Server Details**: Fill in Server Name, IP Address, and Port
2. **Click "Test Connection"**: Button appears in the Connection Status panel
3. **Wait for Results**: Test takes 1-2 seconds
4. **Review Status**: See pass/fail indication with server version

### Test Results

#### Success (?)
```
? Test passed - Server reachable (v2.3.20)
```
- Server is online and reachable
- Server version is displayed
- You can proceed to connect

#### Failure (?)
```
? Test failed - Connection timeout
? Test failed - Server version incompatible
? Test failed - Could not reach server
```
- Server is offline or unreachable
- Network/firewall issue
- Incorrect IP/Port
- Server version incompatible

### Test vs. Connect

| Feature | Connection Test | Full Connect |
|---------|----------------|--------------|
| **Duration** | 1-2 seconds | Persistent |
| **Purpose** | Verify reachability | Start session |
| **Auto-disconnect** | Yes | No |
| **Recording** | No | Optional |
| **Panel closes** | No | Yes (on success) |

### When to Use

- ? **Before connecting** to verify server is online
- ? **After changing** IP/Port to validate settings
- ? **When troubleshooting** connection issues
- ? **To check** server version compatibility

---

## Bookmark Export/Import

### Overview

Export and import bookmark collections to:
- **Share** server lists with your team
- **Backup** your favorite servers
- **Migrate** bookmarks between computers
- **Merge** bookmark lists from multiple sources

### Export Bookmarks

#### Steps

1. Click the **Upload icon** button (?) in the bookmarks header
2. Choose destination folder (defaults to Documents)
3. Enter filename (defaults to `server_bookmarks.json`)
4. Click "Save"
5. Confirmation appears in Connection Status panel

#### Export File Format

```json
[
  {
    "Name": "Production Server",
    "Ip": "192.168.1.100",
    "Port": 5002
  },
  {
    "Name": "Training Server",
    "Ip": "10.0.0.50",
    "Port": 5002
  }
]
```

#### Use Cases

- **Team Sharing**: Export and share via email/chat
- **Backup**: Regular exports for safekeeping
- **Documentation**: Include in server documentation
- **Migration**: Transfer to new computer

### Import Bookmarks

#### Steps

1. Click the **Download icon** button (?) in the bookmarks header
2. Navigate to the JSON file location
3. Select the bookmark file
4. Click "Open"
5. Review import results in Connection Status panel

#### Import Behavior

The import process uses **smart merging**:

| Scenario | Behavior |
|----------|----------|
| **New server** (unique IP:Port) | Added to list |
| **Existing server** (matching IP:Port) | Name updated |
| **Duplicate prevention** | No duplicate IP:Port combinations |
| **Sort preservation** | Auto-sorted alphabetically (Z-A) |

#### Import Results

```
Imported 5 new bookmarks, updated 2 existing
```
- **New**: Bookmarks that didn't exist before
- **Updated**: Existing bookmarks with name changes

### File Format Validation

#### Valid Import File

```json
[
  {
    "Name": "My Server",
    "Ip": "192.168.1.1",
    "Port": 5002
  }
]
```

#### Required Fields

- `Name`: Server display name (string)
- `Ip`: IP address (string)
- `Port`: Port number (integer)

#### Invalid Files

The following will be rejected:

```json
// Empty array
[]

// Missing required fields
[
  {
    "Name": "Server"
    // Missing Ip and Port
  }
]

// Invalid JSON syntax
[
  {
    "Name": "Server",
    "Ip": "192.168.1.1"
    "Port": 5002
  ]  // Missing comma
]
```

---

## UI Controls

### Bookmark Header Buttons

```
[Bookmark Icon] Server Bookmarks    [?] [?] [+ Add]
```

| Button | Icon | Tooltip | Action |
|--------|------|---------|--------|
| **Import** | ? (Download) | Import bookmarks from file | Opens file picker |
| **Export** | ? (Upload) | Export bookmarks to file | Opens save dialog |
| **Add** | + Plus | Add current server | Adds/updates bookmark |

### Connection Status Panel

```
?? Connection Status Message

[Test Connection Button]
```

- **Visible**: When not connected
- **Hidden**: When already connected
- **Disabled**: During active test or connection

---

## Workflow Examples

### Example 1: Testing Before Connecting

```
1. User enters: IP=192.168.1.100, Port=5002, Name=Production
2. Click "Test Connection"
3. Status shows: "Testing connection..."
4. Result: "? Test passed - Server reachable (v2.3.20)"
5. Click "Connect to Server"
6. Panel closes, session starts
```

### Example 2: Sharing Team Bookmarks

**Team Lead:**
```
1. Has 10 bookmarks for team servers
2. Click Export (?)
3. Save to "team_servers.json"
4. Share file via email/chat
```

**Team Member:**
```
1. Receives "team_servers.json"
2. Click Import (?)
3. Select file
4. Result: "Imported 8 new bookmarks, updated 2 existing"
5. All team servers now available
```

### Example 3: Computer Migration

**Old Computer:**
```
1. Click Export (?)
2. Save to USB drive
3. Copy to cloud storage
```

**New Computer:**
```
1. Install AeroDebrief
2. Click Import (?)
3. Select backup file
4. All bookmarks restored
```

---

## Error Handling

### Connection Test Errors

| Error Message | Cause | Solution |
|--------------|-------|----------|
| Connection timeout | Server offline or unreachable | Check server is running |
| Server version incompatible | Version mismatch | Update client or server |
| Could not reach server | Network/firewall issue | Check firewall/network settings |
| Invalid server address | Malformed IP/Port | Verify IP and Port values |

### Import Errors

| Error Message | Cause | Solution |
|--------------|-------|----------|
| No bookmarks found in file | Empty or invalid JSON | Check file contents |
| Import failed: Invalid JSON | Syntax error | Validate JSON format |
| Import failed: File not found | Wrong path | Verify file location |

### Export Errors

| Error Message | Cause | Solution |
|--------------|-------|----------|
| No bookmarks to export | Empty bookmark list | Add bookmarks first |
| Export failed: Access denied | Permission issue | Choose different location |
| Export failed: Disk full | No storage space | Free up disk space |

---

## Keyboard Shortcuts

Currently no keyboard shortcuts are implemented for these features, but they could be added in the future:

| Shortcut | Action | Status |
|----------|--------|--------|
| Ctrl+T | Test Connection | Future |
| Ctrl+I | Import Bookmarks | Future |
| Ctrl+E | Export Bookmarks | Future |
| Enter | Connect (when bookmark selected) | Implemented (double-click) |

---

## Tips & Best Practices

### Connection Testing

- ? **Always test** before connecting to a new server
- ? **Test after** changing IP/Port values
- ? **Use test results** to troubleshoot connection issues
- ? **Don't test repeatedly** in quick succession (wait 2 seconds between tests)

### Bookmark Management

- ? **Export regularly** for backups
- ? **Use descriptive names** for bookmarks (e.g., "Production - US East")
- ? **Organize by purpose** (Production, Training, Testing, etc.)
- ? **Share with team** to standardize server lists
- ? **Don't rely solely** on auto-saved bookmarks (export for safety)

### File Organization

```
Documents/
??? AeroDebrief/
?   ??? server_bookmarks_backup_2024-01.json
?   ??? team_servers.json
?   ??? production_servers.json
```

- Keep backups in a dedicated folder
- Use dated filenames for backups
- Separate team/personal bookmarks

---

## Technical Details

### Storage Location

| Data Type | Path | Format |
|-----------|------|--------|
| Auto-saved bookmarks | `%APPDATA%\AeroDebrief\server_bookmarks.json` | JSON |
| Exported bookmarks | User-selected location | JSON |

### File Compatibility

- **Forward compatible**: Older clients can read newer exports
- **Backward compatible**: Newer clients can read older exports
- **Cross-platform**: JSON format works on all platforms

### Merge Algorithm

```csharp
foreach (imported bookmark)
{
    existing = FindByIpPort(imported.Ip, imported.Port);
    
    if (existing == null)
        Add(imported);  // New bookmark
    else
        existing.Name = imported.Name;  // Update name
}

SaveAndSort();  // Auto-sort by name (Z-A)
```

---

## Future Enhancements

Potential future features:

1. **Bulk Operations**
   - Select multiple bookmarks
   - Delete multiple at once
   - Export selected only

2. **Advanced Testing**
   - Ping test (latency)
   - Bandwidth test
   - Concurrent player count

3. **Cloud Sync**
   - Auto-sync across devices
   - Team bookmark repositories
   - Version control for bookmark lists

4. **Categories/Tags**
   - Organize by region
   - Tag by purpose (training/ops)
   - Custom categories

5. **History**
   - Recently connected servers
   - Connection success rate
   - Average connection time

---

## Troubleshooting

### Test Connection Never Completes

**Symptoms**: Button stays disabled, "Testing connection..." forever

**Causes**:
- Server is not responding
- Firewall blocking connection
- Network timeout

**Solutions**:
1. Wait up to 10 seconds for timeout
2. Check firewall settings
3. Verify server is running
4. Try different network

### Import Shows "0 new bookmarks"

**Symptoms**: Import completes but no bookmarks added

**Causes**:
- All bookmarks already exist (same IP:Port)
- File contains only duplicates

**Solutions**:
1. Check if bookmarks already exist in list
2. Review import results message
3. Verify file contents match expectations

### Export File Empty

**Symptoms**: Exported file contains `[]`

**Causes**:
- No bookmarks in list when exported

**Solutions**:
1. Add bookmarks before exporting
2. Import existing bookmark file first
3. Check if bookmarks were saved

---

## See Also

- [Server Bookmarks Storage](Server-Bookmarks-Storage.md) - Storage location and format
- [Technical Architecture](Technical-Architecture.md) - System architecture
- SRS Server Documentation - Server setup and configuration
